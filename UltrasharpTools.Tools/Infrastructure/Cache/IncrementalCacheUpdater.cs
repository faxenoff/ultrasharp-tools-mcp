using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Инкрементальное обновление кэша.
/// Обновляет только изменённые файлы вместо полного пересканирования.
///
/// Стратегия:
/// 1. Сравниваем mtime + size файлов с кэшированными
/// 2. Определяем добавленные/изменённые/удалённые
/// 3. Пересканируем только изменённые
/// 4. Обновляем кэш инкрементально
///
/// Для веток: дельта хранится отдельно от base.
/// </summary>
public sealed class IncrementalCacheUpdater
{
    private readonly BranchAwareCache _cache;
    private readonly ILogger<IncrementalCacheUpdater> _logger;

    /// <summary>
    /// Callback для извлечения символов из файла.
    /// </summary>
    public Func<string, CancellationToken, Task<FileSymbolData>>? SymbolExtractor { get; set; }

    /// <summary>
    /// Callback для генерации embeddings для символов.
    /// </summary>
    public Func<List<SymbolInfo>, CancellationToken, Task<Dictionary<string, float[]>>>? EmbeddingGenerator { get; set; }

    public IncrementalCacheUpdater(
        BranchAwareCache cache,
        ILogger<IncrementalCacheUpdater>? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<IncrementalCacheUpdater>.Instance;
    }

    /// <summary>
    /// Обновить кэш инкрементально на основе изменений файлов.
    /// </summary>
    /// <param name="cachedEntry">Существующий кэш ветки.</param>
    /// <param name="changes">Список изменённых файлов.</param>
    /// <param name="solutionDirectory">Корневая директория solution.</param>
    /// <param name="commitSha">Новый SHA коммита.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Обновлённый кэш и данные для сохранения.</returns>
    public async Task<IncrementalUpdateResult> UpdateAsync(
        BranchCacheEntry cachedEntry,
        FileChanges changes,
        string solutionDirectory,
        string commitSha,
        CancellationToken ct = default)
    {
        if (SymbolExtractor == null)
            throw new InvalidOperationException("SymbolExtractor must be set before calling UpdateAsync");

        var sw = Stopwatch.StartNew();
        var result = new IncrementalUpdateResult
        {
            PreviousEntry = cachedEntry,
            Changes = changes
        };

        _logger.LogInformation(
            "[INCREMENTAL] Starting update: {Added} added, {Modified} modified, {Deleted} deleted",
            changes.Added.Count, changes.Modified.Count, changes.Deleted.Count);

        // Копируем существующие данные файлов
        var updatedFiles = new Dictionary<string, FileState>(cachedEntry.Files);
        var newSymbols = new List<FileSymbolData>();
        var removedSymbolIds = new HashSet<string>();

        // 1. Обрабатываем удалённые файлы
        foreach (var deletedFile in changes.Deleted)
        {
            ct.ThrowIfCancellationRequested();

            if (updatedFiles.TryGetValue(deletedFile, out var fileState))
            {
                // Собираем ID символов для удаления
                // (предполагаем формат: file:symbolId)
                removedSymbolIds.Add($"file:{deletedFile}");
                updatedFiles.Remove(deletedFile);
            }
        }

        // 2. Обрабатываем добавленные файлы
        foreach (var addedFile in changes.Added)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(solutionDirectory, addedFile);
            if (!File.Exists(fullPath))
                continue;

            try
            {
                var symbolData = await SymbolExtractor(fullPath, ct);
                newSymbols.Add(symbolData);

                var fileInfo = new FileInfo(fullPath);
                updatedFiles[addedFile] = new FileState
                {
                    Mtime = fileInfo.LastWriteTimeUtc,
                    Size = fileInfo.Length,
                    Status = FileStatus.Added,
                    SymbolCount = symbolData.Symbols.Count
                };

                _logger.LogDebug("[INCREMENTAL] Added file: {File} ({Symbols} symbols)",
                    addedFile, symbolData.Symbols.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[INCREMENTAL] Failed to process added file: {File}", addedFile);
            }
        }

        // 3. Обрабатываем изменённые файлы
        foreach (var modifiedFile in changes.Modified)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(solutionDirectory, modifiedFile);
            if (!File.Exists(fullPath))
                continue;

            try
            {
                // Удаляем старые символы
                removedSymbolIds.Add($"file:{modifiedFile}");

                // Извлекаем новые символы
                var symbolData = await SymbolExtractor(fullPath, ct);
                newSymbols.Add(symbolData);

                var fileInfo = new FileInfo(fullPath);
                updatedFiles[modifiedFile] = new FileState
                {
                    Mtime = fileInfo.LastWriteTimeUtc,
                    Size = fileInfo.Length,
                    Status = FileStatus.Modified,
                    SymbolCount = symbolData.Symbols.Count
                };

                _logger.LogDebug("[INCREMENTAL] Modified file: {File} ({Symbols} symbols)",
                    modifiedFile, symbolData.Symbols.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[INCREMENTAL] Failed to process modified file: {File}", modifiedFile);
            }
        }

        // 4. Генерируем embeddings для новых символов (если настроено)
        Dictionary<string, float[]>? newEmbeddings = null;
        if (EmbeddingGenerator != null && newSymbols.Count > 0)
        {
            var allNewSymbols = newSymbols.SelectMany(f => f.Symbols).ToList();
            if (allNewSymbols.Count > 0)
            {
                _logger.LogDebug("[INCREMENTAL] Generating embeddings for {Count} symbols", allNewSymbols.Count);
                newEmbeddings = await EmbeddingGenerator(allNewSymbols, ct);
            }
        }

        // 5. Создаём обновлённый entry
        var updatedEntry = new BranchCacheEntry
        {
            BranchName = cachedEntry.BranchName,
            OriginalBranchName = cachedEntry.OriginalBranchName,
            CommitSha = commitSha,
            BaseCommitSha = cachedEntry.BaseCommitSha,
            CreatedAt = cachedEntry.CreatedAt,
            LastValidatedAt = DateTimeOffset.UtcNow,
            Type = cachedEntry.Type,
            Files = updatedFiles,
            AddedFiles = changes.Added,
            ModifiedFiles = changes.Modified,
            DeletedFiles = changes.Deleted,
            Statistics = new CacheStatistics
            {
                TotalFiles = updatedFiles.Count,
                TotalSymbols = updatedFiles.Values.Sum(f => f.SymbolCount),
                AddedFilesCount = changes.Added.Count,
                ModifiedFilesCount = changes.Modified.Count,
                DeletedFilesCount = changes.Deleted.Count,
                IndexingTimeMs = sw.ElapsedMilliseconds
            }
        };

        result.UpdatedEntry = updatedEntry;
        result.NewSymbols = newSymbols;
        result.RemovedSymbolIds = removedSymbolIds;
        result.NewEmbeddings = newEmbeddings;
        result.Success = true;

        sw.Stop();
        _logger.LogInformation(
            "[INCREMENTAL] Update completed in {Time}ms: {NewSymbols} new symbols, {Removed} removed",
            sw.ElapsedMilliseconds,
            newSymbols.Sum(f => f.Symbols.Count),
            removedSymbolIds.Count);

        return result;
    }

    /// <summary>
    /// Проверить, требуется ли обновление кэша.
    /// Быстрая проверка на основе commit SHA.
    /// </summary>
    public bool NeedsUpdate(BranchCacheEntry cachedEntry, string currentCommitSha)
    {
        return cachedEntry.CommitSha != currentCommitSha;
    }

    /// <summary>
    /// Определить стратегию обновления на основе количества изменений.
    /// </summary>
    public UpdateStrategy DetermineStrategy(FileChanges changes, int totalFiles)
    {
        // Если изменено больше 30% файлов - полное пересканирование эффективнее
        var changeRatio = (double)changes.TotalChanges / totalFiles;

        if (changeRatio > 0.3)
        {
            _logger.LogDebug("[INCREMENTAL] {Ratio:P1} files changed, recommending full rescan",
                changeRatio);
            return UpdateStrategy.FullRescan;
        }

        if (changes.TotalChanges == 0)
        {
            return UpdateStrategy.NoChanges;
        }

        return UpdateStrategy.Incremental;
    }
}

/// <summary>
/// Результат инкрементального обновления.
/// </summary>
public sealed class IncrementalUpdateResult
{
    /// <summary>
    /// Успешно ли обновление.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Предыдущий кэш.
    /// </summary>
    public BranchCacheEntry? PreviousEntry { get; set; }

    /// <summary>
    /// Обновлённый кэш.
    /// </summary>
    public BranchCacheEntry? UpdatedEntry { get; set; }

    /// <summary>
    /// Изменения файлов.
    /// </summary>
    public FileChanges? Changes { get; set; }

    /// <summary>
    /// Новые символы из добавленных/изменённых файлов.
    /// </summary>
    public List<FileSymbolData> NewSymbols { get; set; } = new();

    /// <summary>
    /// ID символов для удаления.
    /// </summary>
    public HashSet<string> RemovedSymbolIds { get; set; } = new();

    /// <summary>
    /// Новые embeddings (если генерировались).
    /// </summary>
    public Dictionary<string, float[]>? NewEmbeddings { get; set; }
}

/// <summary>
/// Стратегия обновления.
/// </summary>
public enum UpdateStrategy
{
    /// <summary>
    /// Изменений нет.
    /// </summary>
    NoChanges,

    /// <summary>
    /// Инкрементальное обновление (только изменённые файлы).
    /// </summary>
    Incremental,

    /// <summary>
    /// Полное пересканирование (много изменений).
    /// </summary>
    FullRescan
}

/// <summary>
/// Данные о символах в файле.
/// </summary>
public sealed class FileSymbolData
{
    /// <summary>
    /// Путь к файлу (относительный).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Список символов в файле.
    /// </summary>
    public List<SymbolInfo> Symbols { get; set; } = new();

    /// <summary>
    /// Время извлечения в миллисекундах.
    /// </summary>
    public long ExtractionTimeMs { get; set; }
}

/// <summary>
/// Информация о символе.
/// </summary>
public sealed class SymbolInfo
{
    /// <summary>
    /// Уникальный ID символа.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// FQN символа.
    /// </summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// Тип символа (Class, Method, Property, etc.).
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Сигнатура (для методов).
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Содержимое (тело метода, определение класса).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Номер строки в файле.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Embedding вектор (если сгенерирован).
    /// </summary>
    public float[]? Embedding { get; set; }
}

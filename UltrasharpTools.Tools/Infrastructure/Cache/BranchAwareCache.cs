using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Слоистая система кэширования с поддержкой веток.
///
/// Архитектура:
/// - base/ - полный кэш основной ветки (main/master)
/// - branches/{name}/ - дельта относительно base для каждой ветки
///
/// При переключении веток:
/// 1. Проверяем кэш ветки
/// 2. Если есть и валиден - загружаем (быстро)
/// 3. Если есть но невалиден - инкрементально обновляем
/// 4. Если нет - создаём новый (полное сканирование)
///
/// Для semantic merge - кэши веток готовы к использованию.
/// </summary>
public sealed class BranchAwareCache : IAsyncDisposable
{
    private readonly string _solutionPath;
    private readonly string _cacheRootPath;
    private readonly ILogger<BranchAwareCache> _logger;

    // In-memory кэш загруженных веток
    private readonly ConcurrentDictionary<string, BranchCacheEntry> _loadedBranches = new();

    // Текущая активная ветка
    private string _currentBranch = "main";

    // Lock для безопасного доступа к файлам
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Настройки
    private readonly BranchCacheOptions _options;

    /// <summary>
    /// Путь к корню кэша.
    /// </summary>
    public string CacheRootPath => _cacheRootPath;

    /// <summary>
    /// Текущая активная ветка.
    /// </summary>
    public string CurrentBranch => _currentBranch;

    /// <summary>
    /// Количество закэшированных веток в памяти.
    /// </summary>
    public int LoadedBranchesCount => _loadedBranches.Count;

    public BranchAwareCache(
        string solutionPath,
        BranchCacheOptions? options = null,
        ILogger<BranchAwareCache>? logger = null)
    {
        _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));
        _options = options ?? new BranchCacheOptions();
        _logger = logger ?? NullLogger<BranchAwareCache>.Instance;

        // Используем централизованное хранилище кэшей
        // Windows: %LOCALAPPDATA%\UltraSharpTools\cache\branches\{SolutionName}_{Hash}
        // Linux/macOS: ~/.ultrasharp/cache/branches/{SolutionName}_{Hash}
        _cacheRootPath = ProjectPathHelper.GetBranchCachePath(solutionPath);

        // Создаём директории
        Directory.CreateDirectory(_cacheRootPath);
        Directory.CreateDirectory(GetBaseCachePath());
        Directory.CreateDirectory(GetBranchesCachePath());

        // Cleanup временных файлов от предыдущих crash'ей
        AtomicFileWriter.CleanupTempFiles(_cacheRootPath);
        AtomicFileWriter.CleanupTempFiles(GetBaseCachePath());

        _logger.LogDebug("[CACHE] Initialized branch-aware cache at {Path}", _cacheRootPath);
    }

    #region Public API

    /// <summary>
    /// Переключиться на ветку.
    /// Загружает кэш ветки если есть, иначе возвращает null для полного сканирования.
    /// </summary>
    /// <param name="branchName">Имя ветки.</param>
    /// <param name="commitSha">SHA текущего коммита.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Кэш ветки или null если нужно полное сканирование.</returns>
    public async Task<BranchCacheEntry?> SwitchToBranchAsync(
        string branchName,
        string commitSha,
        CancellationToken ct = default)
    {
        var normalizedName = BranchCacheEntry.NormalizeBranchName(branchName);
        _logger.LogInformation("[CACHE] Switching to branch {Branch} (commit: {Commit})",
            branchName, commitSha[..Math.Min(7, commitSha.Length)]);

        // Проверяем in-memory кэш
        if (_loadedBranches.TryGetValue(normalizedName, out var cached))
        {
            if (cached.CommitSha == commitSha)
            {
                _logger.LogDebug("[CACHE] Branch {Branch} found in memory cache (valid)", branchName);
                _currentBranch = branchName;
                return cached;
            }

            _logger.LogDebug("[CACHE] Branch {Branch} in memory but commit changed, need validation",
                branchName);
        }

        // Загружаем с диска
        var diskCache = await LoadBranchCacheAsync(branchName, ct);
        if (diskCache != null)
        {
            if (diskCache.CommitSha == commitSha)
            {
                // Кэш валиден
                _logger.LogInformation("[CACHE] Branch {Branch} loaded from disk (valid)", branchName);
                _loadedBranches[normalizedName] = diskCache;
                _currentBranch = branchName;
                return diskCache;
            }

            // Кэш устарел - нужна инкрементальная валидация
            _logger.LogInformation("[CACHE] Branch {Branch} cache outdated, needs incremental update",
                branchName);

            // Возвращаем старый кэш для инкрементального обновления
            diskCache.LastValidatedAt = DateTimeOffset.UtcNow;
            _loadedBranches[normalizedName] = diskCache;
            _currentBranch = branchName;
            return diskCache;
        }

        // Кэш не найден - нужно полное сканирование
        _logger.LogInformation("[CACHE] Branch {Branch} not cached, needs full scan", branchName);
        _currentBranch = branchName;
        return null;
    }

    /// <summary>
    /// Сохранить кэш ветки.
    /// </summary>
    public async Task SaveBranchCacheAsync(
        BranchCacheEntry entry,
        byte[] symbolsData,
        byte[]? embeddingsData = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var normalizedName = BranchCacheEntry.NormalizeBranchName(entry.OriginalBranchName);

        await _fileLock.WaitAsync(ct);
        try
        {
            // Используем ProcessLock для межпроцессной синхронизации
            var lockPath = GetBranchLockPath(normalizedName);
            using var processLock = await ProcessLock.TryAcquireAsync(lockPath, _options.LockTimeout, ct);

            if (!processLock.IsAcquired)
            {
                _logger.LogWarning("[CACHE] Failed to acquire lock for branch {Branch}", entry.OriginalBranchName);
                return;
            }

            var branchPath = entry.Type == CacheType.Full
                ? GetBaseCachePath()
                : GetBranchCachePath(normalizedName);

            Directory.CreateDirectory(branchPath);

            // Сохраняем данные символов (с компрессией)
            var symbolsPath = Path.Combine(branchPath, "symbols.bin.gz");
            var compressedSymbols = CompressData(symbolsData);
            await AtomicFileWriter.WriteAllBytesAsync(symbolsPath, compressedSymbols, ct);
            entry.SymbolsChecksum = CacheManifest.ComputeBytesHash(symbolsData);
            entry.Statistics.SymbolsCacheSize = compressedSymbols.Length;

            // Сохраняем embeddings если есть
            if (embeddingsData != null && embeddingsData.Length > 0)
            {
                var embeddingsPath = Path.Combine(branchPath, "embeddings.bin.gz");
                var compressedEmbeddings = CompressData(embeddingsData);
                await AtomicFileWriter.WriteAllBytesAsync(embeddingsPath, compressedEmbeddings, ct);
                entry.EmbeddingsChecksum = CacheManifest.ComputeBytesHash(embeddingsData);
                entry.Statistics.EmbeddingsCacheSize = compressedEmbeddings.Length;
            }

            // Сохраняем манифест
            var manifestPath = Path.Combine(branchPath, "manifest.json");
            entry.LastValidatedAt = DateTimeOffset.UtcNow;
            await AtomicFileWriter.WriteAllTextAsync(manifestPath, entry.ToJson(), ct);

            // Обновляем in-memory кэш
            _loadedBranches[normalizedName] = entry;

            sw.Stop();
            _logger.LogInformation(
                "[CACHE] Saved branch {Branch} cache: {Symbols} symbols, {Size} bytes in {Time}ms",
                entry.OriginalBranchName,
                entry.Statistics.TotalSymbols,
                entry.Statistics.SymbolsCacheSize + entry.Statistics.EmbeddingsCacheSize,
                sw.ElapsedMilliseconds);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Загрузить данные символов для ветки.
    /// </summary>
    public async Task<byte[]?> LoadSymbolsDataAsync(string branchName, CancellationToken ct = default)
    {
        var normalizedName = BranchCacheEntry.NormalizeBranchName(branchName);

        // Определяем путь (base или branches)
        string branchPath;
        if (IsBaseBranch(branchName))
        {
            branchPath = GetBaseCachePath();
        }
        else
        {
            branchPath = GetBranchCachePath(normalizedName);
            if (!Directory.Exists(branchPath))
            {
                // Fallback на base если ветка не закэширована
                branchPath = GetBaseCachePath();
            }
        }

        var symbolsPath = Path.Combine(branchPath, "symbols.bin.gz");
        if (!File.Exists(symbolsPath))
            return null;

        try
        {
            var compressedData = await File.ReadAllBytesAsync(symbolsPath, ct);
            return DecompressData(compressedData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CACHE] Failed to load symbols for branch {Branch}", branchName);
            return null;
        }
    }

    /// <summary>
    /// Загрузить данные embeddings для ветки.
    /// </summary>
    public async Task<byte[]?> LoadEmbeddingsDataAsync(string branchName, CancellationToken ct = default)
    {
        var normalizedName = BranchCacheEntry.NormalizeBranchName(branchName);
        var branchPath = IsBaseBranch(branchName)
            ? GetBaseCachePath()
            : GetBranchCachePath(normalizedName);

        var embeddingsPath = Path.Combine(branchPath, "embeddings.bin.gz");
        if (!File.Exists(embeddingsPath))
            return null;

        try
        {
            var compressedData = await File.ReadAllBytesAsync(embeddingsPath, ct);
            return DecompressData(compressedData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CACHE] Failed to load embeddings for branch {Branch}", branchName);
            return null;
        }
    }

    /// <summary>
    /// Получить список изменённых файлов относительно кэша.
    /// Для инкрементального обновления.
    /// </summary>
    public async Task<FileChanges> GetChangedFilesAsync(
        BranchCacheEntry cachedEntry,
        string solutionDirectory,
        CancellationToken ct = default)
    {
        var changes = new FileChanges();
        var sw = Stopwatch.StartNew();

        // Сканируем текущие .cs файлы
        var currentFiles = Directory.EnumerateFiles(solutionDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("/obj/") &&
                        !f.Contains("\\bin\\") && !f.Contains("/bin/"))
            .ToList();

        foreach (var filePath in currentFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(solutionDirectory, filePath);

            if (cachedEntry.Files.TryGetValue(relativePath, out var cachedFile))
            {
                // Файл был в кэше - проверяем изменения
                var currentMtime = File.GetLastWriteTimeUtc(filePath);
                var currentSize = new FileInfo(filePath).Length;

                if (currentMtime != cachedFile.Mtime.UtcDateTime || currentSize != cachedFile.Size)
                {
                    changes.Modified.Add(relativePath);
                }
            }
            else
            {
                // Новый файл
                changes.Added.Add(relativePath);
            }
        }

        // Ищем удалённые файлы
        var currentFilesSet = currentFiles
            .Select(f => Path.GetRelativePath(solutionDirectory, f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var cachedPath in cachedEntry.Files.Keys)
        {
            if (!currentFilesSet.Contains(cachedPath))
            {
                changes.Deleted.Add(cachedPath);
            }
        }

        sw.Stop();
        _logger.LogDebug(
            "[CACHE] File scan completed in {Time}ms: {Added} added, {Modified} modified, {Deleted} deleted",
            sw.ElapsedMilliseconds, changes.Added.Count, changes.Modified.Count, changes.Deleted.Count);

        return changes;
    }

    /// <summary>
    /// Инвалидировать кэш ветки.
    /// </summary>
    public async Task InvalidateBranchAsync(string branchName, CancellationToken ct = default)
    {
        var normalizedName = BranchCacheEntry.NormalizeBranchName(branchName);

        _loadedBranches.TryRemove(normalizedName, out _);

        var branchPath = GetBranchCachePath(normalizedName);
        if (Directory.Exists(branchPath))
        {
            await _fileLock.WaitAsync(ct);
            try
            {
                Directory.Delete(branchPath, recursive: true);
                _logger.LogInformation("[CACHE] Invalidated cache for branch {Branch}", branchName);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }

    /// <summary>
    /// Получить список всех закэшированных веток.
    /// </summary>
    public async Task<List<string>> GetCachedBranchesAsync(CancellationToken ct = default)
    {
        var branches = new List<string>();

        // Base branch
        var baseManifestPath = Path.Combine(GetBaseCachePath(), "manifest.json");
        if (File.Exists(baseManifestPath))
        {
            var json = await File.ReadAllTextAsync(baseManifestPath, ct);
            var entry = BranchCacheEntry.FromJson(json);
            if (entry != null)
            {
                branches.Add(entry.OriginalBranchName);
            }
        }

        // Feature branches
        var branchesDir = GetBranchesCachePath();
        if (Directory.Exists(branchesDir))
        {
            foreach (var branchDir in Directory.EnumerateDirectories(branchesDir))
            {
                var manifestPath = Path.Combine(branchDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var json = await File.ReadAllTextAsync(manifestPath, ct);
                    var entry = BranchCacheEntry.FromJson(json);
                    if (entry != null)
                    {
                        branches.Add(entry.OriginalBranchName);
                    }
                }
            }
        }

        return branches;
    }

    /// <summary>
    /// Очистить все кэши.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        _loadedBranches.Clear();

        await _fileLock.WaitAsync(ct);
        try
        {
            if (Directory.Exists(_cacheRootPath))
            {
                Directory.Delete(_cacheRootPath, recursive: true);
                Directory.CreateDirectory(_cacheRootPath);
                Directory.CreateDirectory(GetBaseCachePath());
                Directory.CreateDirectory(GetBranchesCachePath());
            }

            _logger.LogInformation("[CACHE] Cleared all caches");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Удалить старые кэши веток (LRU cleanup).
    /// </summary>
    public async Task CleanupOldBranchesAsync(int keepCount = 10, CancellationToken ct = default)
    {
        var branchesDir = GetBranchesCachePath();
        if (!Directory.Exists(branchesDir))
            return;

        var branches = new List<(string Path, DateTimeOffset LastAccess)>();

        foreach (var branchDir in Directory.EnumerateDirectories(branchesDir))
        {
            var manifestPath = Path.Combine(branchDir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct);
                var entry = BranchCacheEntry.FromJson(json);
                if (entry != null)
                {
                    branches.Add((branchDir, entry.LastValidatedAt));
                }
            }
        }

        // Сортируем по времени последнего доступа (новые первые)
        var toDelete = branches
            .OrderByDescending(b => b.LastAccess)
            .Skip(keepCount)
            .ToList();

        foreach (var (path, _) in toDelete)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                _logger.LogDebug("[CACHE] Deleted old branch cache: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CACHE] Failed to delete old cache: {Path}", path);
            }
        }

        if (toDelete.Count > 0)
        {
            _logger.LogInformation("[CACHE] Cleaned up {Count} old branch caches", toDelete.Count);
        }
    }

    #endregion

    #region Private Helpers

    private string GetBaseCachePath() => Path.Combine(_cacheRootPath, "base");
    private string GetBranchesCachePath() => Path.Combine(_cacheRootPath, "branches");
    private string GetBranchCachePath(string normalizedName) =>
        Path.Combine(_cacheRootPath, "branches", normalizedName);
    private string GetBranchLockPath(string normalizedName) =>
        Path.Combine(_cacheRootPath, $".{normalizedName}.lock");

    private bool IsBaseBranch(string branchName) =>
        branchName.Equals("main", StringComparison.OrdinalIgnoreCase) ||
        branchName.Equals("master", StringComparison.OrdinalIgnoreCase);

    private async Task<BranchCacheEntry?> LoadBranchCacheAsync(string branchName, CancellationToken ct)
    {
        var normalizedName = BranchCacheEntry.NormalizeBranchName(branchName);

        string branchPath;
        if (IsBaseBranch(branchName))
        {
            branchPath = GetBaseCachePath();
        }
        else
        {
            branchPath = GetBranchCachePath(normalizedName);
        }

        var manifestPath = Path.Combine(branchPath, "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, ct);
            return BranchCacheEntry.FromJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CACHE] Failed to load manifest for branch {Branch}", branchName);
            return null;
        }
    }

    private static byte[] CompressData(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }

    private static byte[] DecompressData(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        _fileLock.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Изменения файлов для инкрементального обновления.
/// </summary>
public sealed class FileChanges
{
    public List<string> Added { get; } = new();
    public List<string> Modified { get; } = new();
    public List<string> Deleted { get; } = new();

    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Deleted.Count > 0;
    public int TotalChanges => Added.Count + Modified.Count + Deleted.Count;
}

/// <summary>
/// Настройки кэширования веток.
/// </summary>
public sealed class BranchCacheOptions
{
    /// <summary>
    /// Максимальное количество веток для хранения (LRU).
    /// </summary>
    public int MaxBranches { get; set; } = 20;

    /// <summary>
    /// Таймаут для захвата блокировки.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Автоматический cleanup при превышении MaxBranches.
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// Сжимать данные на диске.
    /// </summary>
    public bool CompressData { get; set; } = true;

    /// <summary>
    /// Кэшировать embeddings.
    /// </summary>
    public bool CacheEmbeddings { get; set; } = true;
}

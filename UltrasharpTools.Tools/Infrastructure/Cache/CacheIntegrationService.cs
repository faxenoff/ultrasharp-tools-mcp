using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Сервис интеграции кэширования с Roslyn индексацией.
/// Связывает BranchAwareCache с MultiVersionIndexer.
///
/// Использование:
/// 1. При загрузке solution - проверяем кэш
/// 2. При переключении веток - загружаем из кэша или сканируем
/// 3. При изменении файлов - инкрементально обновляем кэш
/// 4. Для semantic merge - используем кэшированные индексы веток
/// </summary>
public sealed class CacheIntegrationService : IAsyncDisposable
{
    private readonly BranchAwareCache _branchCache;
    private readonly IncrementalCacheUpdater _incrementalUpdater;
    private readonly ILogger<CacheIntegrationService> _logger;

    /// <summary>
    /// Кэш веток.
    /// </summary>
    public BranchAwareCache BranchCache => _branchCache;

    /// <summary>
    /// Инкрементальный апдейтер.
    /// </summary>
    public IncrementalCacheUpdater IncrementalUpdater => _incrementalUpdater;

    public CacheIntegrationService(
        string solutionPath,
        BranchCacheOptions? options = null,
        ILogger<CacheIntegrationService>? logger = null)
    {
        _logger = logger ?? NullLogger<CacheIntegrationService>.Instance;
        _branchCache = new BranchAwareCache(solutionPath, options, null);
        _incrementalUpdater = new IncrementalCacheUpdater(_branchCache, null);

        _logger.LogInformation("[CACHE-INT] Initialized for solution: {Solution}", solutionPath);
    }

    #region Branch Operations

    /// <summary>
    /// Загрузить или создать кэш для ветки.
    /// Возвращает кэшированные символы или null если нужно полное сканирование.
    /// </summary>
    public async Task<CachedBranchData?> LoadBranchAsync(
        string branchName,
        string commitSha,
        string solutionDirectory,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Пробуем загрузить из кэша
        var cachedEntry = await _branchCache.SwitchToBranchAsync(branchName, commitSha, ct);

        if (cachedEntry == null)
        {
            _logger.LogInformation("[CACHE-INT] Branch {Branch} not cached, needs full scan", branchName);
            return null;
        }

        // 2. Проверяем, нужно ли инкрементальное обновление
        if (cachedEntry.CommitSha != commitSha)
        {
            _logger.LogInformation("[CACHE-INT] Branch {Branch} cache outdated, checking for changes", branchName);

            var changes = await _branchCache.GetChangedFilesAsync(cachedEntry, solutionDirectory, ct);

            if (changes.HasChanges)
            {
                var strategy = _incrementalUpdater.DetermineStrategy(changes, cachedEntry.Files.Count);

                if (strategy == UpdateStrategy.FullRescan)
                {
                    _logger.LogInformation("[CACHE-INT] Too many changes ({Count}), need full rescan",
                        changes.TotalChanges);
                    return null;
                }

                _logger.LogInformation("[CACHE-INT] Incremental update for {Count} changed files",
                    changes.TotalChanges);
            }
        }

        // 3. Загружаем данные символов
        var symbolsData = await _branchCache.LoadSymbolsDataAsync(branchName, ct);
        if (symbolsData == null)
        {
            _logger.LogWarning("[CACHE-INT] Failed to load symbols data for branch {Branch}", branchName);
            return null;
        }

        // 4. Десериализуем символы
        CachedSymbolBatch? symbolBatch = null;
        try
        {
            symbolBatch = JsonSerializer.Deserialize<CachedSymbolBatch>(symbolsData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CACHE-INT] Failed to deserialize symbols for branch {Branch}", branchName);
            return null;
        }

        // 5. Загружаем embeddings (опционально)
        CachedEmbeddingBatch? embeddingBatch = null;
        try
        {
            var embeddingsData = await _branchCache.LoadEmbeddingsDataAsync(branchName, ct);
            if (embeddingsData != null)
            {
                embeddingBatch = JsonSerializer.Deserialize<CachedEmbeddingBatch>(embeddingsData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[CACHE-INT] Failed to load embeddings for branch {Branch}", branchName);
        }

        sw.Stop();
        _logger.LogInformation(
            "[CACHE-INT] Loaded branch {Branch} from cache: {Symbols} symbols in {Time}ms",
            branchName, symbolBatch?.Count ?? 0, sw.ElapsedMilliseconds);

        return new CachedBranchData
        {
            Entry = cachedEntry,
            Symbols = symbolBatch ?? new CachedSymbolBatch(),
            Embeddings = embeddingBatch,
            LoadTimeMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Сохранить кэш ветки после полного сканирования.
    /// </summary>
    public async Task SaveBranchAsync(
        string branchName,
        string commitSha,
        string baseCommitSha,
        CachedSymbolBatch symbols,
        Dictionary<string, FileState> files,
        CachedEmbeddingBatch? embeddings = null,
        bool isBaseBranch = false,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var entry = new BranchCacheEntry
        {
            BranchName = BranchCacheEntry.NormalizeBranchName(branchName),
            OriginalBranchName = branchName,
            CommitSha = commitSha,
            BaseCommitSha = baseCommitSha,
            CreatedAt = DateTimeOffset.UtcNow,
            LastValidatedAt = DateTimeOffset.UtcNow,
            Type = isBaseBranch ? CacheType.Full : CacheType.Delta,
            Files = files,
            Statistics = new CacheStatistics
            {
                TotalFiles = files.Count,
                TotalSymbols = symbols.Count,
                SymbolsWithEmbeddings = embeddings?.Embeddings.Count ?? 0
            }
        };

        // Сериализуем данные
        symbols.CommitSha = commitSha;
        symbols.BranchName = branchName;
        symbols.CreatedAt = DateTimeOffset.UtcNow;

        var symbolsData = JsonSerializer.SerializeToUtf8Bytes(symbols);
        byte[]? embeddingsData = embeddings != null
            ? JsonSerializer.SerializeToUtf8Bytes(embeddings)
            : null;

        await _branchCache.SaveBranchCacheAsync(entry, symbolsData, embeddingsData, ct);

        sw.Stop();
        _logger.LogInformation(
            "[CACHE-INT] Saved branch {Branch} cache: {Symbols} symbols in {Time}ms",
            branchName, symbols.Count, sw.ElapsedMilliseconds);
    }

    #endregion

    #region Semantic Merge Integration

    /// <summary>
    /// Загрузить кэшированный VersionedIndex для ветки (для semantic merge).
    /// </summary>
    public async Task<VersionedIndex?> LoadVersionedIndexAsync(
        string branchName,
        string commitSha,
        string solutionDirectory,
        CancellationToken ct = default)
    {
        var cachedData = await LoadBranchAsync(branchName, commitSha, solutionDirectory, ct);
        if (cachedData == null)
            return null;

        // Конвертируем CachedSymbolEntry в CodeUnit
        var units = new Dictionary<string, CodeUnit>();

        foreach (var symbol in cachedData.Symbols.Symbols)
        {
            var embedding = cachedData.Embeddings?.Embeddings.GetValueOrDefault(symbol.FullyQualifiedName);

            var unit = new CodeUnit
            {
                Id = symbol.FullyQualifiedName,
                Name = symbol.Name,
                FullyQualifiedName = symbol.FullyQualifiedName,
                Type = MapSymbolKindToCodeUnitType(symbol.Kind),
                Content = symbol.Content ?? symbol.Signature ?? string.Empty,
                ContentHash = symbol.ContentHash ?? string.Empty,
                StructuralHash = symbol.ContentHash ?? string.Empty, // Используем content hash как fallback
                Signature = symbol.Signature,
                FilePath = symbol.FilePath ?? string.Empty,
                StartLine = symbol.StartLine,
                EndLine = symbol.EndLine,
                Embedding = embedding
            };
            units[unit.Id] = unit;
        }

        return new VersionedIndex
        {
            Version = branchName,
            CommitSha = commitSha,
            BranchName = branchName,
            Units = units,
            VectorStore = new VectorStore(), // Пустой VectorStore
            CreatedAt = cachedData.Entry.CreatedAt,
            Statistics = new IndexStatistics
            {
                TotalUnits = units.Count,
                UnitsWithEmbeddings = cachedData.Embeddings?.Embeddings.Count ?? 0,
                FileCount = cachedData.Entry.Files.Count,
                UnitsByType = units.Values
                    .GroupBy(u => u.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                IndexingTimeMs = cachedData.LoadTimeMs
            }
        };
    }

    /// <summary>
    /// Сохранить VersionedIndex в кэш.
    /// </summary>
    public async Task SaveVersionedIndexAsync(
        VersionedIndex index,
        string baseCommitSha,
        Dictionary<string, FileState> files,
        bool isBaseBranch = false,
        CancellationToken ct = default)
    {
        // Конвертируем CodeUnit в CachedSymbolEntry
        var symbols = new CachedSymbolBatch
        {
            CommitSha = index.CommitSha ?? string.Empty,
            BranchName = index.BranchName ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        CachedEmbeddingBatch? embeddings = null;
        var hasEmbeddings = index.Units.Values.Any(u => u.Embedding != null);

        if (hasEmbeddings)
        {
            embeddings = new CachedEmbeddingBatch();
        }

        foreach (var unit in index.Units.Values)
        {
            var entry = new CachedSymbolEntry
            {
                FullyQualifiedName = unit.FullyQualifiedName,
                Name = unit.Name,
                Kind = MapCodeUnitTypeToSymbolKind(unit.Type),
                Signature = unit.Signature,
                FilePath = unit.FilePath,
                StartLine = unit.StartLine,
                EndLine = unit.EndLine,
                ContentHash = unit.ContentHash,
                Content = unit.Content
            };
            symbols.Symbols.Add(entry);

            if (unit.Embedding != null && embeddings != null)
            {
                embeddings.Embeddings[unit.FullyQualifiedName] = unit.Embedding;
                if (embeddings.Dimension == 0)
                    embeddings.Dimension = unit.Embedding.Length;
            }
        }

        await SaveBranchAsync(
            index.BranchName ?? "unknown",
            index.CommitSha ?? string.Empty,
            baseCommitSha,
            symbols,
            files,
            embeddings,
            isBaseBranch,
            ct);
    }

    /// <summary>
    /// Маппинг SymbolKindEnum в CodeUnitType.
    /// </summary>
    private static CodeUnitType MapSymbolKindToCodeUnitType(SymbolKindEnum kind)
    {
        return kind switch
        {
            SymbolKindEnum.Class or SymbolKindEnum.Interface or
            SymbolKindEnum.Struct or SymbolKindEnum.Enum or
            SymbolKindEnum.Delegate => CodeUnitType.Type,

            SymbolKindEnum.Method or SymbolKindEnum.Constructor => CodeUnitType.Method,

            SymbolKindEnum.Property => CodeUnitType.Property,

            SymbolKindEnum.Field or SymbolKindEnum.EnumMember or
            SymbolKindEnum.Constant => CodeUnitType.Field,

            SymbolKindEnum.Namespace => CodeUnitType.Namespace,

            _ => CodeUnitType.Block
        };
    }

    /// <summary>
    /// Маппинг CodeUnitType в SymbolKindEnum.
    /// </summary>
    private static SymbolKindEnum MapCodeUnitTypeToSymbolKind(CodeUnitType type)
    {
        return type switch
        {
            CodeUnitType.Type => SymbolKindEnum.Class,
            CodeUnitType.Method => SymbolKindEnum.Method,
            CodeUnitType.Property => SymbolKindEnum.Property,
            CodeUnitType.Field => SymbolKindEnum.Field,
            CodeUnitType.Namespace => SymbolKindEnum.Namespace,
            _ => SymbolKindEnum.Other
        };
    }

    #endregion

    #region Git Integration

    /// <summary>
    /// Обработать переключение ветки.
    /// </summary>
    public async Task OnBranchSwitchAsync(
        string newBranch,
        string commitSha,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[CACHE-INT] Branch switch detected: {Branch}", newBranch);
        await _branchCache.SwitchToBranchAsync(newBranch, commitSha, ct);
        await _branchCache.CleanupOldBranchesAsync(keepCount: 20, ct);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Получить статистику кэша.
    /// </summary>
    public async Task<CacheStats> GetStatsAsync(CancellationToken ct = default)
    {
        var branches = await _branchCache.GetCachedBranchesAsync(ct);

        return new CacheStats
        {
            CachedBranchesCount = branches.Count,
            CachedBranches = branches,
            CurrentBranch = _branchCache.CurrentBranch,
            LoadedInMemory = _branchCache.LoadedBranchesCount,
            CacheRootPath = _branchCache.CacheRootPath
        };
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _branchCache.DisposeAsync();
    }
}

/// <summary>
/// Данные закэшированной ветки.
/// </summary>
public sealed class CachedBranchData
{
    public required BranchCacheEntry Entry { get; init; }
    public required CachedSymbolBatch Symbols { get; init; }
    public CachedEmbeddingBatch? Embeddings { get; init; }
    public long LoadTimeMs { get; init; }
}

/// <summary>
/// Статистика кэша.
/// </summary>
public sealed class CacheStats
{
    public int CachedBranchesCount { get; init; }
    public List<string> CachedBranches { get; init; } = new();
    public string CurrentBranch { get; init; } = string.Empty;
    public int LoadedInMemory { get; init; }
    public string CacheRootPath { get; init; } = string.Empty;
}

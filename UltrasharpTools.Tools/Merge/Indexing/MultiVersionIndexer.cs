using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Infrastructure.Cache;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Merge.Indexing;

public sealed class MultiVersionIndexer {
    private readonly CodeUnitExtractor _extractor;
    private readonly ILogger<MultiVersionIndexer> _logger;
    private readonly Func<CacheIntegrationService?>? _cacheServiceFactory;
    private CacheIntegrationService? _cacheService;
    private bool _cacheServiceInitialized;

    public MultiVersionIndexer(
        CodeUnitExtractor extractor,
        ILogger<MultiVersionIndexer>? logger = null,
        Func<CacheIntegrationService?>? cacheServiceFactory = null
    ) {
        _extractor = extractor;
        _logger = logger ?? NullLogger<MultiVersionIndexer>.Instance;
        _cacheServiceFactory = cacheServiceFactory;
    }

    private CacheIntegrationService? GetCacheService() {
        if (!_cacheServiceInitialized && _cacheServiceFactory != null) {
            _cacheService = _cacheServiceFactory();
            _cacheServiceInitialized = true;
            if (_cacheService != null) {
                _logger.LogDebug("[INDEX] Cache service initialized");
            }
        }
        return _cacheService;
    }
    public async Task<VersionedIndex> IndexVersionAsync(
        string versionName,
        string directoryPath,
        string[]? filePatterns = null,
        string? commitSha = null,
        string? branchName = null,
        CancellationToken ct = default
    ) {
        var sw = Stopwatch.StartNew();

        _logger.LogDebug(
            "[INDEX] Indexing version {Version} from {Directory}",
            versionName,
            directoryPath
        );

        // Попробовать загрузить из кэша
        var cacheService = GetCacheService();
        if (cacheService != null && !string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(commitSha)) {
            var cachedIndex = await cacheService.LoadVersionedIndexAsync(branchName, commitSha, directoryPath, ct);
            if (cachedIndex != null) {
                _logger.LogDebug(
                    "[INDEX] Loaded version {Version} from cache: {Count} units in {Time}ms",
                    versionName, cachedIndex.Units.Count, sw.ElapsedMilliseconds);
                return cachedIndex;
            }
            _logger.LogDebug("[INDEX] Cache miss for branch {Branch}, will scan directory", branchName);
        }

        // Default patterns
        filePatterns ??= new[] { "*.cs", "*.json" };
        _logger.LogDebug("[INDEX] File patterns: {Patterns}", string.Join(", ", filePatterns));

        // 1. Извлечь CodeUnits
        _logger.LogDebug("[INDEX] Step 1: Extracting CodeUnits from directory...");
        var units = await _extractor.ExtractFromDirectoryAsync(directoryPath, filePatterns, ct);
        _logger.LogDebug("[INDEX] Extracted {Count} CodeUnits", units.Count);

        // 2. Собрать состояние файлов СРАЗУ (пока они существуют - до удаления temp директории)
        Dictionary<string, FileState>? fileStates = null;
        if (cacheService != null && !string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(commitSha)) {
            fileStates = new Dictionary<string, FileState>();
            foreach (var unit in units.Where(u => !string.IsNullOrEmpty(u.FilePath))) {
                var fullPath = Path.Combine(directoryPath, unit.FilePath);
                if (File.Exists(fullPath) && !fileStates.ContainsKey(unit.FilePath)) {
                    var fileInfo = new FileInfo(fullPath);
                    fileStates[unit.FilePath] = new FileState {
                        Mtime = fileInfo.LastWriteTimeUtc,
                        Size = fileInfo.Length,
                        Status = FileStatus.Unchanged
                    };
                }
            }
        }

        // 3. Построить иерархию
        _extractor.BuildHierarchy(units);

        // 4. Создать словарь units (с дедупликацией на случай конфликтов Id)
        var unitsDict = new Dictionary<string, CodeUnit>(units.Count);
        foreach (var unit in units) {
            if (!unitsDict.TryAdd(unit.Id, unit)) {
                _logger.LogWarning(
                    "[Indexer] Duplicate CodeUnit Id detected: {Id} (FilePath: {FilePath}). Keeping first occurrence.",
                    unit.Id,
                    unit.FilePath
                );
            }
        }

        // 5. Создать VectorStore (пустой, embeddings добавятся позже)
        var vectorStore = new VectorStore();

        // 6. Собрать статистику
        var statistics = ComputeStatistics(units, sw.ElapsedMilliseconds);

        sw.Stop();

        _logger.LogDebug(
            "Indexed version {Version}: {Count} units in {Time}ms",
            versionName,
            units.Count,
            sw.ElapsedMilliseconds
        );

        var index = new VersionedIndex {
            Version = versionName,
            CommitSha = commitSha,
            BranchName = branchName,
            Units = unitsDict,
            VectorStore = vectorStore,
            CreatedAt = DateTimeOffset.UtcNow,
            Statistics = statistics,
        };

        // Сохранить в кэш (асинхронно, не блокируем)
        // fileStates уже собраны, файлы могут быть удалены - это нормально
        if (cacheService != null && !string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(commitSha) && fileStates != null) {
            _ = SaveToCacheAsync(index, fileStates, cacheService, ct);
        }

        return index;
    }
    private async Task SaveToCacheAsync(
        VersionedIndex index,
        Dictionary<string, FileState> files,
        CacheIntegrationService cacheService,
        CancellationToken ct) {
        try {
            // Определить base commit (для delta)
            var isBaseBranch = index.BranchName?.Contains("main", StringComparison.OrdinalIgnoreCase) == true
                || index.BranchName?.Contains("master", StringComparison.OrdinalIgnoreCase) == true;

            await cacheService.SaveVersionedIndexAsync(
                index,
                index.CommitSha ?? string.Empty,
                files,
                isBaseBranch,
                ct);

            _logger.LogDebug("[INDEX] Saved version {Version} ({Branch}) to cache: {Files} files",
                index.Version, index.BranchName, files.Count);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "[INDEX] Failed to save version {Version} to cache", index.Version);
        }
    }
    /// <summary>
    /// Индексировать все 4 версии для 3-way merge.
    /// </summary>
    public async Task<MultiVersionIndexResult> IndexAllVersionsAsync(
        IndexingRequest request,
        CancellationToken ct = default
    ) {
        _logger.LogDebug("[INDEX] === Starting multi-version indexing for 3-way merge ===");
        _logger.LogDebug("[INDEX] BaseDirectory: {Dir}", request.BaseDirectory);
        _logger.LogDebug("[INDEX] BranchADirectory: {Dir}", request.BranchADirectory);
        _logger.LogDebug("[INDEX] BranchBDirectory: {Dir}", request.BranchBDirectory);

        var sw = Stopwatch.StartNew();

        // Индексировать base версию
        _logger.LogDebug("[INDEX] Indexing BASE version...");
        var baseIndex = await IndexVersionAsync(
            "base",
            request.BaseDirectory,
            request.FilePatterns,
            request.BaseCommitSha,
            request.BaseBranch,
            ct
        );
        _logger.LogDebug("[INDEX] BASE version indexed: {Count} units", baseIndex.Units.Count);

        // Индексировать branchA
        _logger.LogDebug("[INDEX] Indexing BRANCH-A (source) version...");
        var branchAIndex = await IndexVersionAsync(
            "branchA",
            request.BranchADirectory,
            request.FilePatterns,
            request.BranchACommitSha,
            request.BranchA,
            ct
        );
        _logger.LogDebug("[INDEX] BRANCH-A indexed: {Count} units", branchAIndex.Units.Count);

        // Индексировать branchB
        _logger.LogDebug("[INDEX] Indexing BRANCH-B (target) version...");
        var branchBIndex = await IndexVersionAsync(
            "branchB",
            request.BranchBDirectory,
            request.FilePatterns,
            request.BranchBCommitSha,
            request.BranchB,
            ct
        );
        _logger.LogDebug("[INDEX] BRANCH-B indexed: {Count} units", branchBIndex.Units.Count);

        // Merged версия будет создана позже
        VersionedIndex? mergedIndex = null;
        if (request.MergedDirectory != null) {
            mergedIndex = await IndexVersionAsync(
                "merged",
                request.MergedDirectory,
                request.FilePatterns,
                request.MergedCommitSha,
                request.MergedBranch,
                ct
            );
        }

        sw.Stop();

        _logger.LogDebug(
            "Multi-version indexing completed in {Time}ms",
            sw.ElapsedMilliseconds
        );

        return new MultiVersionIndexResult {
            BaseIndex = baseIndex,
            BranchAIndex = branchAIndex,
            BranchBIndex = branchBIndex,
            MergedIndex = mergedIndex,
            TotalIndexingTimeMs = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Вычислить статистику индекса.
    /// </summary>
    private IndexStatistics ComputeStatistics(List<CodeUnit> units, long indexingTimeMs) {
        var unitsByType = units.GroupBy(u => u.Type).ToDictionary(g => g.Key, g => g.Count());

        var fileCount = units.Count(u => u.Type == CodeUnitType.File);
        var unitsWithEmbeddings = units.Count(u => u.Embedding != null);

        return new IndexStatistics {
            TotalUnits = units.Count,
            UnitsByType = unitsByType,
            FileCount = fileCount,
            UnitsWithEmbeddings = unitsWithEmbeddings,
            IndexingTimeMs = indexingTimeMs,
        };
    }
}
/// <summary>
/// Запрос на индексацию.
/// </summary>
public sealed record IndexingRequest {
    public required string BaseDirectory { get; init; }
    public required string BranchADirectory { get; init; }
    public required string BranchBDirectory { get; init; }
    public string? MergedDirectory { get; init; }

    public string[]? FilePatterns { get; init; }

    public string? BaseCommitSha { get; init; }
    public string? BranchACommitSha { get; init; }
    public string? BranchBCommitSha { get; init; }
    public string? MergedCommitSha { get; init; }

    public string? BaseBranch { get; init; }
    public string? BranchA { get; init; }
    public string? BranchB { get; init; }
    public string? MergedBranch { get; init; }
}

/// <summary>
/// Результат индексации всех версий.
/// </summary>
public sealed record MultiVersionIndexResult {
    public required VersionedIndex BaseIndex { get; init; }
    public required VersionedIndex BranchAIndex { get; init; }
    public required VersionedIndex BranchBIndex { get; init; }
    public VersionedIndex? MergedIndex { get; init; }

    public required long TotalIndexingTimeMs { get; init; }

    public int TotalUnits =>
        BaseIndex.Statistics.TotalUnits
        + BranchAIndex.Statistics.TotalUnits
        + BranchBIndex.Statistics.TotalUnits
        + (MergedIndex?.Statistics.TotalUnits ?? 0);
}

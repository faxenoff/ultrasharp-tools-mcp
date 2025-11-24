using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Ipc;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Hybrid implementation of SemanticSearchService that delegates to IndexerClient via IPC.
/// Used in IPC mode (Comm → Droid → Indexer) instead of local semantic processing.
/// </summary>
public sealed class HybridSemanticSearchService : ISemanticSearchService
{
    private readonly IndexerClient _indexerClient;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<HybridSemanticSearchService> _logger;
    private readonly SemanticSearchServiceConfig _config;

    private bool _isIndexed;

    /// <summary>
    /// Indicates whether semantic search is available (has valid indexer client).
    /// </summary>
    public bool IsAvailable => _indexerClient != null;

    public HybridSemanticSearchService(
        IndexerClient indexerClient,
        ISolutionManager solutionManager,
        SemanticSearchServiceConfig? config = null,
        ILogger<HybridSemanticSearchService>? logger = null
    )
    {
        _indexerClient = indexerClient;
        _solutionManager = solutionManager;
        _config = config ?? SemanticSearchServiceConfig.Default;
        _logger = logger ?? NullLogger<HybridSemanticSearchService>.Instance;
    }

    /// <summary>
    /// Индексировать текущий solution (delegates to Indexer).
    /// </summary>
    public async Task IndexCurrentSolutionAsync(CancellationToken ct = default)
    {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null)
        {
            throw new InvalidOperationException(
                "No solution loaded. Call LoadSolutionAsync first."
            );
        }

        _logger.LogInformation(
            "[Hybrid] Starting semantic indexing of current solution via Indexer"
        );

        // В hybrid режиме индексация делается в фоне через Indexer
        // Здесь просто помечаем как indexed
        _isIndexed = true;

        _logger.LogInformation("[Hybrid] Indexing delegated to Indexer process");
    }

    /// <summary>
    /// Переиндексировать конкретный project.
    /// </summary>
    public async Task ReindexProjectAsync(string projectName, CancellationToken ct = default)
    {
        _logger.LogInformation("[Hybrid] Reindexing project {ProjectName} via Indexer", projectName);
        // В hybrid режиме индексация управляется Indexer
        await Task.CompletedTask;
    }

    /// <summary>
    /// Инкрементально переиндексировать изменённые файлы.
    /// </summary>
    public async Task ReindexChangedFilesAsync(string[] filePaths, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Hybrid] Incrementally reindexing {Count} changed files via Indexer",
            filePaths.Length
        );
        // В hybrid режиме индексация управляется Indexer
        await Task.CompletedTask;
    }

    /// <summary>
    /// Удалить индекс для конкретных файлов.
    /// </summary>
    public async Task DeleteFileIndexesAsync(string[] filePaths, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Hybrid] Deleting indexes for {Count} files via Indexer",
            filePaths.Length
        );
        // В hybrid режиме управляется Indexer
        await Task.CompletedTask;
    }

    /// <summary>
    /// Проверить индексирован ли solution.
    /// </summary>
    public bool IsIndexed() => _isIndexed;

    /// <summary>
    /// Найти семантически похожий код (delegates to Indexer).
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarCodeAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        _logger.LogDebug(
            "[Hybrid] Searching for similar code via Indexer: {Query}, topK={TopK}, minSimilarity={MinSimilarity}",
            query,
            topK,
            minSimilarity
        );

        // Делегируем в IndexerClient
        var matches = await _indexerClient.SearchSimilarAsync(query, topK, minSimilarity, ct);

        // Конвертируем SimilarityMatch → SemanticCodeMatch
        var results = matches
            .Select((m, index) => new SemanticCodeMatch
            {
                Id = m.Id,
                FullyQualifiedName = m.Id, // В Indexer ID = documentPath или FQN
                Code = m.Content,
                Similarity = m.Similarity,
                Rank = index + 1,
                FilePath = m.Id, // Assuming ID is file path
                LineNumber = 0, // Not available from Indexer
                Type = CodeMatchType.Unknown, // Not available from Indexer
            })
            .ToList();

        _logger.LogDebug("[Hybrid] Found {Count} similar matches via Indexer", results.Count);

        return results;
    }

    /// <summary>
    /// Найти семантически похожие методы.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        // В hybrid режиме делаем общий поиск
        // Фильтрация по типу (methods) пока не поддерживается
        _logger.LogDebug(
            "[Hybrid] Searching for similar methods via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Найти семантически похожие классы.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarClassesAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        // В hybrid режиме делаем общий поиск
        _logger.LogDebug(
            "[Hybrid] Searching for similar classes via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Найти семантически похожие методы в конкретном файле.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsInFileAsync(
        string query,
        string filePath,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        // В hybrid режиме делаем общий поиск
        _logger.LogDebug(
            "[Hybrid] Searching for similar methods in file via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Получить метрики индексатора.
    /// </summary>
    public CodeSemanticIndexerMetrics GetIndexerMetrics()
    {
        // Получаем метрики из Indexer
        // TODO: Использовать IndexerClient.GetStatusAsync()
        return new CodeSemanticIndexerMetrics
        {
            IndexedMethods = 0,
            IndexedClasses = 0,
            IndexedDocuments = 0,
            EmbeddingMetrics = new EmbeddingMetrics
            {
                TotalRequests = 0,
                CacheHits = 0,
                CacheMisses = 0,
                CacheHitRate = 0,
                CacheSize = 0,
                CacheCapacity = 0,
                TotalEmbedTimeMs = 0,
                AverageEmbedTimeMs = 0,
                ProviderInfo = new Models.ProviderInfo
                {
                    Name = "indexer-hybrid",
                    Model = "delegated-to-indexer",
                    Dimension = 384,
                    MaxTokens = 0,
                    IsLocal = false,
                    Version = "3.0.6",
                },
            },
        };
    }

    public async ValueTask DisposeAsync()
    {
        _indexerClient?.Dispose();
        await Task.CompletedTask;
    }
}

using Microsoft.Extensions.Logging;
using UltraSharpTools.VectorDB.Semantic.Models;

namespace UltraSharpTools.VectorDB.Semantic;

/// <summary>
/// Упрощённый semantic service для Indexer процесса
/// Предоставляет только core функциональность: индексация и поиск через VectorStore
/// </summary>
public sealed class VectorDBSemanticService : IAsyncDisposable
{
    private readonly VectorStore _vectorStore;
    private readonly EmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<VectorDBSemanticService> _logger;

    public VectorDBSemanticService(
        VectorStore vectorStore,
        EmbeddingGenerator embeddingGenerator,
        ILogger<VectorDBSemanticService> logger)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Индексировать код: генерировать embedding и сохранить в VectorStore
    /// </summary>
    public async Task IndexCodeAsync(
        string code,
        string documentPath,
        string? metadata = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Indexing code from {Path}", documentPath);

        // Генерируем embedding
        var embedding = await _embeddingGenerator.EmbedAsync(code, ct);

        if (embedding == null || embedding.Length == 0)
        {
            _logger.LogWarning("Failed to generate embedding for {Path}", documentPath);
            return;
        }

        // Создаем VectorEmbedding объект
        var vectorEmbedding = new VectorEmbedding
        {
            Id = documentPath,
            Content = code,
            Vector = embedding,
            Dimension = embedding.Length,
            Metadata = metadata
        };

        // Сохраняем в VectorStore
        await _vectorStore.InsertAsync(vectorEmbedding, ct);

        _logger.LogDebug("Successfully indexed {Path}", documentPath);
    }

    /// <summary>
    /// Поиск похожего кода по query
    /// </summary>
    public async Task<List<SimilarityResult>> SearchSimilarAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.0f,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Searching for similar code: {Query}", query);

        // Генерируем embedding для query
        var queryEmbedding = await _embeddingGenerator.EmbedAsync(query, ct);

        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
            _logger.LogWarning("Failed to generate embedding for query");
            return [];
        }

        // Ищем похожие в VectorStore
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            minSimilarity,
            ct);

        _logger.LogDebug("Found {Count} similar results", results.Count);
        return results;
    }

    /// <summary>
    /// Получить количество индексированных embeddings
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        return await _vectorStore.GetCountAsync(ct);
    }

    /// <summary>
    /// Очистить все embeddings
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Clearing all embeddings");
        await _vectorStore.ClearAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _vectorStore.DisposeAsync();
        await _embeddingGenerator.DisposeAsync();
    }
}

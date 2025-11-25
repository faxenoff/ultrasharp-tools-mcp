using Microsoft.Extensions.Logging;
using UltraSharpTools.VectorDB.Semantic.Models;

namespace UltraSharpTools.VectorDB.Semantic;

/// <summary>
/// Упрощённый semantic service для Indexer процесса
/// Предоставляет только core функциональность: индексация и поиск через VectorStore
/// </summary>
public sealed partial class VectorDBSemanticService : IAsyncDisposable {
    private readonly VectorStore _vectorStore;
    private readonly EmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<VectorDBSemanticService> _logger;

    public VectorDBSemanticService(
        VectorStore vectorStore,
        EmbeddingGenerator embeddingGenerator,
        ILogger<VectorDBSemanticService> logger) {
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
        CancellationToken ct = default) {
        LogIndexingCode(documentPath);

        // Генерируем embedding
        var embedding = await _embeddingGenerator.EmbedAsync(code, ct);

        if (embedding == null || embedding.Length == 0) {
            LogEmbeddingFailed(documentPath);
            return;
        }

        // Создаем VectorEmbedding объект
        var vectorEmbedding = new VectorEmbedding {
            Id = documentPath,
            Content = code,
            Vector = embedding,
            Dimension = embedding.Length,
            Metadata = metadata
        };

        // Сохраняем в VectorStore
        await _vectorStore.InsertAsync(vectorEmbedding, ct);

        LogIndexingSuccess(documentPath);
    }

    /// <summary>
    /// Поиск похожего кода по query
    /// </summary>
    public async Task<List<SimilarityResult>> SearchSimilarAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.0f,
        CancellationToken ct = default) {
        LogSearching(query);

        // Генерируем embedding для query
        var queryEmbedding = await _embeddingGenerator.EmbedAsync(query, ct);

        if (queryEmbedding == null || queryEmbedding.Length == 0) {
            LogQueryEmbeddingFailed();
            return [];
        }

        // Ищем похожие в VectorStore
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            minSimilarity,
            ct);

        LogSearchResults(results.Count);
        return results;
    }

    /// <summary>
    /// Получить количество индексированных embeddings
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken ct = default) {
        return await _vectorStore.GetCountAsync(ct);
    }

    /// <summary>
    /// Очистить все embeddings
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default) {
        LogClearing();
        await _vectorStore.ClearAsync(ct);
    }

    public async ValueTask DisposeAsync() {
        await _vectorStore.DisposeAsync();
        await _embeddingGenerator.DisposeAsync();
    }
    /// <summary>
    /// Batch-индексирование: генерирует embeddings для всех текстов за один вызов
    /// </summary>
    public async Task<int> IndexBatchAsync(
        IReadOnlyList<(string Code, string DocumentPath, string? Metadata)> items,
        CancellationToken ct = default) {
        if (items.Count == 0)
            return 0;

        LogIndexingBatch(items.Count);

        // Извлекаем тексты для batch embedding
        var texts = items.Select(i => i.Code).ToArray();

        // Генерируем все embeddings за один batch вызов
        var embeddings = await _embeddingGenerator.EmbedBatchAsync(texts, ct);

        // Создаём VectorEmbedding объекты
        var vectorEmbeddings = new List<VectorEmbedding>(items.Count);
        for (int i = 0; i < items.Count; i++) {
            var embedding = embeddings[i];
            if (embedding == null || embedding.Length == 0) {
                LogEmbeddingFailed(items[i].DocumentPath);
                continue;
            }

            vectorEmbeddings.Add(new VectorEmbedding {
                Id = items[i].DocumentPath,
                Content = items[i].Code,
                Vector = embedding,
                Dimension = embedding.Length,
                Metadata = items[i].Metadata
            });
        }

        // Batch insert в VectorStore
        await _vectorStore.InsertBatchAsync(vectorEmbeddings, ct);

        LogIndexingBatchSuccess(vectorEmbeddings.Count);
        return vectorEmbeddings.Count;
    }
}

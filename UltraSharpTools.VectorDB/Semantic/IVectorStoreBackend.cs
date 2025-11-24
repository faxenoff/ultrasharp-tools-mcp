using UltraSharpTools.VectorDB.Semantic.Models;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB.Semantic;

/// <summary>
/// Абстракция для vector store backend (SqliteVec или Vectorlite).
/// Позволяет автоматически переключаться между brute-force и HNSW ANN поиском
/// в зависимости от размера кодовой базы.
/// </summary>
public interface IVectorStoreBackend : IAsyncDisposable
{
    /// <summary>
    /// Инициализировать backend с указанной connection string и dimension.
    /// </summary>
    /// <param name="connectionString">SQLite connection string (например, "Data Source=:memory:")</param>
    /// <param name="dimension">Размерность векторов (384, 768, и т.д.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(
        string connectionString,
        int dimension,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Вставить один vector embedding.
    /// </summary>
    Task InsertAsync(VectorEmbedding embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Вставить batch векторов (оптимизировано для производительности).
    /// </summary>
    Task InsertBatchAsync(
        IEnumerable<VectorEmbedding> embeddings,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Поиск по similarity (cosine similarity).
    /// </summary>
    /// <param name="queryVector">Query vector для поиска похожих</param>
    /// <param name="limit">Максимальное количество результатов</param>
    /// <param name="minSimilarity">Минимальный порог similarity (0.0 - 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Список результатов, отсортированных по similarity (desc)</returns>
    Task<List<SimilarityResult>> SearchAsync(
        float[] queryVector,
        int limit,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Получить общее количество векторов в базе.
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Удалить embedding по ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очистить все embeddings.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Тип backend (SqliteVec или Vectorlite).
    /// </summary>
    VectorStoreBackendType BackendType { get; }

    /// <summary>
    /// Проверить здоровье backend (например, доступность vectorlite.dll).
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

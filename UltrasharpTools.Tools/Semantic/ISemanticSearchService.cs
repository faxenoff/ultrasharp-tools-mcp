namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Интерфейс для semantic code search сервисов.
/// Реализуется как SemanticSearchService (локальная индексация), так и HybridSemanticSearchService (IPC делегирование).
/// </summary>
public interface ISemanticSearchService : IAsyncDisposable
{
    /// <summary>
    /// Indicates whether semantic search is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Индексировать текущий solution.
    /// </summary>
    Task IndexCurrentSolutionAsync(CancellationToken ct = default);

    /// <summary>
    /// Переиндексировать конкретный project.
    /// </summary>
    Task ReindexProjectAsync(string projectName, CancellationToken ct = default);

    /// <summary>
    /// Инкрементально переиндексировать изменённые файлы.
    /// </summary>
    Task ReindexChangedFilesAsync(string[] filePaths, CancellationToken ct = default);

    /// <summary>
    /// Удалить индекс для конкретных файлов.
    /// </summary>
    Task DeleteFileIndexesAsync(string[] filePaths, CancellationToken ct = default);

    /// <summary>
    /// Проверить индексирован ли solution.
    /// </summary>
    bool IsIndexed();

    /// <summary>
    /// Найти семантически похожий код.
    /// </summary>
    Task<List<SemanticCodeMatch>> FindSimilarCodeAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    );

    /// <summary>
    /// Найти семантически похожие методы.
    /// </summary>
    Task<List<SemanticCodeMatch>> FindSimilarMethodsAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    );

    /// <summary>
    /// Найти семантически похожие классы.
    /// </summary>
    Task<List<SemanticCodeMatch>> FindSimilarClassesAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    );

    /// <summary>
    /// Найти семантически похожие методы в конкретном файле.
    /// </summary>
    Task<List<SemanticCodeMatch>> FindSimilarMethodsInFileAsync(
        string query,
        string filePath,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    );

    /// <summary>
    /// Получить метрики индексатора.
    /// </summary>
    CodeSemanticIndexerMetrics GetIndexerMetrics();
}

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Источник семантического режима
/// </summary>
public enum SemanticModeSource
{
    /// <summary>
    /// Семантический режим недоступен
    /// </summary>
    None = 0,

    /// <summary>
    /// Локальная модель embedding (Ollama/TEI)
    /// </summary>
    Local = 1,

    /// <summary>
    /// Overlord EmbeddingService
    /// </summary>
    Overlord = 2,

    /// <summary>
    /// Доступны оба источника (локальный + Overlord)
    /// </summary>
    Both = 3,
}

/// <summary>
/// Информация о доступности семантического режима
/// </summary>
public sealed class SemanticModeAvailability
{
    /// <summary>
    /// Доступен ли семантический режим
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Источник семантического режима
    /// </summary>
    public SemanticModeSource Source { get; init; }

    /// <summary>
    /// Название модели embedding (если доступно)
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Размерность векторов
    /// </summary>
    public int VectorDimension { get; init; }

    /// <summary>
    /// URL локального embedding сервиса (если используется Local)
    /// </summary>
    public string? LocalEmbeddingUrl { get; init; }

    /// <summary>
    /// URL Overlord сервера (если используется Overlord)
    /// </summary>
    public string? OverlordUrl { get; init; }
}

/// <summary>
/// Результат семантического поиска
/// </summary>
public sealed class SemanticMatch
{
    /// <summary>
    /// Идентификатор найденного элемента
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Текст найденного элемента
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0 - 1.0)
    /// </summary>
    public required double Similarity { get; init; }

    /// <summary>
    /// Метаданные элемента
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Провайдер семантического режима - динамическое определение доступности Local/Overlord embedding
/// </summary>
public interface ISemanticModeProvider
{
    /// <summary>
    /// Проверяет доступность семантического режима
    /// </summary>
    Task<SemanticModeAvailability> CheckAvailabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Получает embedding для текста
    /// </summary>
    /// <param name="text">Текст для векторизации</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Вектор embedding или null если недоступно</returns>
    Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Выполняет семантический поиск по векторной базе
    /// </summary>
    /// <param name="queryVector">Вектор запроса</param>
    /// <param name="topK">Количество результатов</param>
    /// <param name="threshold">Минимальный порог similarity (0.0 - 1.0)</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список найденных совпадений</returns>
    Task<IEnumerable<SemanticMatch>> SearchAsync(
        float[] queryVector,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default
    );

    /// <summary>
    /// Выполняет семантический поиск по естественному языку
    /// </summary>
    /// <param name="query">Запрос на естественном языке</param>
    /// <param name="topK">Количество результатов</param>
    /// <param name="threshold">Минимальный порог similarity (0.0 - 1.0)</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список найденных совпадений</returns>
    Task<IEnumerable<SemanticMatch>> SearchByTextAsync(
        string query,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default
    );
}

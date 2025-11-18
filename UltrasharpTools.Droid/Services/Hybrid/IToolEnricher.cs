namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Метаданные обогащения
/// </summary>
public sealed class EnrichmentMetadata
{
    /// <summary>
    /// Время выполнения обогащения (мс)
    /// </summary>
    public long EnrichmentTimeMs { get; init; }

    /// <summary>
    /// Количество найденных семантических совпадений
    /// </summary>
    public int SemanticMatchCount { get; init; }

    /// <summary>
    /// Источник семантических данных
    /// </summary>
    public SemanticModeSource Source { get; init; }

    /// <summary>
    /// Название применённой enrichment strategy
    /// </summary>
    public string? StrategyName { get; init; }

    /// <summary>
    /// Был ли выполнен timeout
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// Сообщение об ошибке (если была)
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Семантическое обогащение результата
/// </summary>
public sealed class SemanticEnrichment
{
    /// <summary>
    /// Семантически похожие определения
    /// </summary>
    public List<SemanticMatch>? SimilarDefinitions { get; init; }

    /// <summary>
    /// Семантически похожие использования
    /// </summary>
    public List<SemanticMatch>? SimilarUsages { get; init; }

    /// <summary>
    /// Похожие изменения из истории (для modify_code)
    /// </summary>
    public List<SemanticMatch>? SimilarChanges { get; init; }

    /// <summary>
    /// Классы с похожей структурой (для get_members)
    /// </summary>
    public List<SemanticMatch>? SimilarStructures { get; init; }

    /// <summary>
    /// Рекомендации на основе семантического анализа
    /// </summary>
    public List<string>? Recommendations { get; init; }

    /// <summary>
    /// Cross-project находки
    /// </summary>
    public Dictionary<string, List<SemanticMatch>>? CrossProjectMatches { get; init; }
}

/// <summary>
/// Результат работы инструмента с семантическим обогащением
/// </summary>
public sealed class EnrichedToolResult
{
    /// <summary>
    /// Оригинальный результат работы инструмента
    /// </summary>
    public required object OriginalResult { get; init; }

    /// <summary>
    /// Семантическое обогащение (если доступно)
    /// </summary>
    public SemanticEnrichment? Semantic { get; init; }

    /// <summary>
    /// Метаданные обогащения
    /// </summary>
    public EnrichmentMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Сервис для семантического обогащения результатов инструментов
/// </summary>
public interface IToolEnricher
{
    /// <summary>
    /// Обогащает результат работы инструмента семантическими данными
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <param name="originalResult">Оригинальный результат</param>
    /// <param name="toolArguments">Аргументы вызова инструмента (опционально)</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Обогащённый результат</returns>
    Task<EnrichedToolResult> EnrichAsync(
        string toolName,
        object originalResult,
        Dictionary<string, object>? toolArguments = null,
        CancellationToken ct = default);

    /// <summary>
    /// Проверяет, поддерживает ли инструмент семантическое обогащение
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <returns>True если поддерживает</returns>
    bool SupportsEnrichment(string toolName);
}

/// <summary>
/// Базовый интерфейс для enrichment strategy
/// </summary>
public interface IEnrichmentStrategy
{
    /// <summary>
    /// Название strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Инструменты, для которых применима эта strategy
    /// </summary>
    IReadOnlySet<string> SupportedTools { get; }

    /// <summary>
    /// Выполняет обогащение
    /// </summary>
    Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct);
}

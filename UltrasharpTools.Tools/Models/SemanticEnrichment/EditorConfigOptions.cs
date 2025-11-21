namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Опции генерации .editorconfig
/// </summary>
public class EditorConfigOptions
{
    /// <summary>
    /// Формат вывода
    /// </summary>
    public EditorConfigFormat Format { get; set; } = EditorConfigFormat.Detailed;

    /// <summary>
    /// Включать ли статистику в комментарии
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// Включать ли примеры кода в комментарии
    /// </summary>
    public bool IncludeExamples { get; set; } = false;

    /// <summary>
    /// Минимальный confidence для автоматического утверждения (default: 0.8)
    /// </summary>
    public double MinConfidenceForAutoApproval { get; set; } = 0.8;

    /// <summary>
    /// Группировать ли правила по категориям
    /// </summary>
    public bool GroupByCategory { get; set; } = true;
}

/// <summary>
/// Формат .editorconfig
/// </summary>
public enum EditorConfigFormat
{
    /// <summary>
    /// Стандартный формат без комментариев
    /// </summary>
    Standard,

    /// <summary>
    /// Детальный формат с обоснованиями и статистикой
    /// </summary>
    Detailed
}

namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Рекомендуемые severity для .editorconfig
/// </summary>
public enum EditorConfigSeverity
{
    /// <summary>
    /// Полностью игнорируем (severity = none)
    /// </summary>
    None,

    /// <summary>
    /// Применяем при автофиксе, но не показываем в IDE (severity = silent)
    /// </summary>
    Silent,

    /// <summary>
    /// Показываем как suggestion в IDE (severity = suggestion)
    /// </summary>
    Suggestion,

    /// <summary>
    /// Показываем как warning (severity = warning)
    /// </summary>
    Warning,

    /// <summary>
    /// Блокируем build (severity = error)
    /// </summary>
    Error
}

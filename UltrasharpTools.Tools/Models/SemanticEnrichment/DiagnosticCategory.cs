namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Категории диагностик для автоматической классификации
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>
    /// Ложное срабатывание (CA1873 - logging optimization в .NET 6+)
    /// </summary>
    FalsePositive,

    /// <summary>
    /// Критичные проблемы производительности (CA1829, CA1854, CA1845)
    /// </summary>
    PerformanceCritical,

    /// <summary>
    /// Инфраструктурный код (CA1822 в BenchmarkDotNet)
    /// </summary>
    Infrastructure,

    /// <summary>
    /// Проблемы безопасности (CA2***)
    /// </summary>
    Security,

    /// <summary>
    /// Проблемы надёжности (CA2***)
    /// </summary>
    Reliability,

    /// <summary>
    /// Проблемы поддерживаемости (CA15**)
    /// </summary>
    Maintainability,

    /// <summary>
    /// Стилистические правила (IDE****)
    /// </summary>
    Style,

    /// <summary>
    /// Требует ручной проверки (низкий confidence или uncertain)
    /// </summary>
    NeedsManualReview
}

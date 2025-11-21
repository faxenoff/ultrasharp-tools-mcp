using Microsoft.CodeAnalysis;

namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Кластер похожих диагностик
/// </summary>
public class DiagnosticCluster
{
    /// <summary>
    /// ID диагностики (CA1873, IDE0001, etc)
    /// </summary>
    public required string DiagnosticId { get; set; }

    /// <summary>
    /// Категория диагностики
    /// </summary>
    public DiagnosticCategory Category { get; set; }

    /// <summary>
    /// Обнаруженный паттерн (например, "Logging infrastructure", "Dictionary.Count() in hot paths")
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Количество срабатываний
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Confidence score категоризации (0.0-1.0)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Рекомендуемый severity для .editorconfig
    /// </summary>
    public EditorConfigSeverity RecommendedSeverity { get; set; }

    /// <summary>
    /// Обоснование решения
    /// </summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Severity из Roslyn
    /// </summary>
    public DiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// Репрезентативные примеры (обычно 3-5)
    /// </summary>
    public List<DiagnosticExample> RepresentativeExamples { get; set; } = new();

    /// <summary>
    /// Список затронутых файлов (может быть паттерн вида "Services/**/*.cs")
    /// </summary>
    public List<string> AffectedFiles { get; set; } = new();

    /// <summary>
    /// Список затронутых проектов
    /// </summary>
    public List<string> AffectedProjects { get; set; } = new();
}

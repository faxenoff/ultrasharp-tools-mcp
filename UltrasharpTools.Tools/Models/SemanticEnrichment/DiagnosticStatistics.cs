using Microsoft.CodeAnalysis;

namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Статистика по диагностикам (LEVEL 1: Statistical analysis)
/// </summary>
public class DiagnosticStatistics
{
    /// <summary>
    /// Количество срабатываний по каждому DiagnosticId
    /// </summary>
    public Dictionary<string, int> CountByDiagnosticId { get; set; } = new();

    /// <summary>
    /// Список файлов по каждому DiagnosticId
    /// </summary>
    public Dictionary<string, List<string>> FilesByDiagnosticId { get; set; } = new();

    /// <summary>
    /// Список проектов по каждому DiagnosticId
    /// </summary>
    public Dictionary<string, List<string>> ProjectsByDiagnosticId { get; set; } = new();

    /// <summary>
    /// Severity по каждому DiagnosticId
    /// </summary>
    public Dictionary<string, DiagnosticSeverity> SeverityByDiagnosticId { get; set; } = new();

    /// <summary>
    /// Общее количество уникальных DiagnosticId
    /// </summary>
    public int UniqueDiagnosticIds => CountByDiagnosticId.Count;

    /// <summary>
    /// Общее количество диагностик
    /// </summary>
    public int TotalDiagnostics => CountByDiagnosticId.Values.Sum();
}

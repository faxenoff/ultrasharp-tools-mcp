namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Результат semantic enrichment диагностик
/// </summary>
public class SemanticEnrichmentResult
{
    /// <summary>
    /// Список кластеров похожих диагностик
    /// </summary>
    public List<DiagnosticCluster> Clusters { get; set; } = new();

    /// <summary>
    /// Relevance scores по DiagnosticId (0.0-10.0)
    /// </summary>
    public Dictionary<string, double> RelevanceScores { get; set; } = new();

    /// <summary>
    /// Суммарная информация
    /// </summary>
    public EnrichmentSummary Summary { get; set; } = new();
}

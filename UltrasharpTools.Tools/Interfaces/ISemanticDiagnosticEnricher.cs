using UltrasharpTools.Tools.Models.SemanticEnrichment;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для semantic enrichment диагностик (Phase 1)
/// </summary>
public interface ISemanticDiagnosticEnricher
{
    /// <summary>
    /// Анализирует статистику диагностик (LEVEL 1: Statistical Analysis)
    /// </summary>
    /// <param name="diagnostics">Список диагностик с filepath и projectName</param>
    /// <returns>Статистические данные по диагностикам</returns>
    DiagnosticStatistics AnalyzeStatistics(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics
    );

    /// <summary>
    /// Обогащает диагностики семантической информацией (LEVEL 1 + LEVEL 2)
    /// </summary>
    /// <param name="diagnostics">Список диагностик с filepath и projectName</param>
    /// <param name="statistics">Статистические данные (из AnalyzeStatistics)</param>
    /// <param name="groupBySimilarity">Группировать похожие диагностики</param>
    /// <param name="similarityThreshold">Порог сходства для кластеризации (0.0-1.0)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат semantic enrichment</returns>
    Task<SemanticEnrichmentResult> EnrichAsync(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics,
        DiagnosticStatistics statistics,
        bool groupBySimilarity,
        double similarityThreshold,
        CancellationToken cancellationToken = default
    );
}

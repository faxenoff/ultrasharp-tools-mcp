namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Суммарная информация о результатах semantic enrichment
/// </summary>
public class EnrichmentSummary
{
    /// <summary>
    /// Общее количество диагностик
    /// </summary>
    public int TotalDiagnostics { get; set; }

    /// <summary>
    /// Количество созданных кластеров
    /// </summary>
    public int TotalClusters { get; set; }

    /// <summary>
    /// Распределение по категориям
    /// </summary>
    public Dictionary<DiagnosticCategory, int> CategoriesFound { get; set; } = new();

    /// <summary>
    /// Количество диагностик которые можно автоматически игнорировать
    /// </summary>
    public int AutoSuppressRecommendations { get; set; }

    /// <summary>
    /// Количество критичных проблем найдено
    /// </summary>
    public int CriticalIssuesFound { get; set; }

    /// <summary>
    /// Количество диагностик требующих ручной проверки
    /// </summary>
    public int ManualReviewRequired { get; set; }

    /// <summary>
    /// Статистика времени обработки
    /// </summary>
    public ProcessingStats ProcessingStats { get; set; } = new();
}

/// <summary>
/// Статистика времени обработки различных этапов
/// </summary>
public class ProcessingStats
{
    /// <summary>
    /// Время статистического анализа (ms)
    /// </summary>
    public long StatisticalAnalysisMs { get; set; }

    /// <summary>
    /// Время применения heuristic rules (ms)
    /// </summary>
    public long HeuristicRulesMs { get; set; }

    /// <summary>
    /// Время кластеризации (ms)
    /// </summary>
    public long ClusteringMs { get; set; }

    /// <summary>
    /// Время определения паттернов (ms)
    /// </summary>
    public long PatternDetectionMs { get; set; }

    /// <summary>
    /// Общее время (ms)
    /// </summary>
    public long TotalMs { get; set; }
}

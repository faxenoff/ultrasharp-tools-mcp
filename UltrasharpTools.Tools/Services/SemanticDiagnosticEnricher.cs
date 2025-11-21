using System.Diagnostics;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models.SemanticEnrichment;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для semantic enrichment диагностик (Phase 1)
/// Использует двухуровневый подход:
/// - LEVEL 1: Statistical Analysis + Heuristic Rules
/// - LEVEL 2: Semantic Clustering + Pattern Detection + Relevance Scoring
/// </summary>
public class SemanticDiagnosticEnricher(
    ISemanticModeProvider? semanticModeProvider,
    ILogger<SemanticDiagnosticEnricher> logger
) : ISemanticDiagnosticEnricher
{
    private readonly ISemanticModeProvider? _semanticModeProvider = semanticModeProvider;
    private readonly ILogger<SemanticDiagnosticEnricher> _logger = logger;

    /// <summary>
    /// LEVEL 1: Анализирует статистику диагностик
    /// </summary>
    public DiagnosticStatistics AnalyzeStatistics(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics
    )
    {
        var sw = Stopwatch.StartNew();

        var statistics = new DiagnosticStatistics
        {
            CountByDiagnosticId = new Dictionary<string, int>(),
            FilesByDiagnosticId = new Dictionary<string, List<string>>(),
            ProjectsByDiagnosticId = new Dictionary<string, List<string>>(),
            SeverityByDiagnosticId = new Dictionary<string, DiagnosticSeverity>(),
        };

        // Используем временные HashSet для избежания дубликатов
        var filesSetByDiagnostic = new Dictionary<string, HashSet<string>>();
        var projectsSetByDiagnostic = new Dictionary<string, HashSet<string>>();

        foreach (var (diagnostic, filePath, projectName) in diagnostics)
        {
            var diagnosticId = diagnostic.Id;

            // Count
            if (!statistics.CountByDiagnosticId.ContainsKey(diagnosticId))
            {
                statistics.CountByDiagnosticId[diagnosticId] = 0;
                filesSetByDiagnostic[diagnosticId] = new HashSet<string>();
                projectsSetByDiagnostic[diagnosticId] = new HashSet<string>();
                statistics.SeverityByDiagnosticId[diagnosticId] = diagnostic.Severity;
            }

            statistics.CountByDiagnosticId[diagnosticId]++;
            filesSetByDiagnostic[diagnosticId].Add(filePath);
            projectsSetByDiagnostic[diagnosticId].Add(projectName);
        }

        // Конвертируем HashSet в List
        foreach (var diagnosticId in statistics.CountByDiagnosticId.Keys)
        {
            statistics.FilesByDiagnosticId[diagnosticId] = filesSetByDiagnostic[
                diagnosticId
            ].ToList();
            statistics.ProjectsByDiagnosticId[diagnosticId] = projectsSetByDiagnostic[
                diagnosticId
            ].ToList();
        }

        sw.Stop();
        _logger.LogInformation(
            "Analyzed statistics for {Count} diagnostics in {ElapsedMs}ms",
            diagnostics.Count,
            sw.ElapsedMilliseconds
        );

        return statistics;
    }

    /// <summary>
    /// LEVEL 1 + LEVEL 2: Обогащает диагностики семантической информацией
    /// </summary>
    public async Task<SemanticEnrichmentResult> EnrichAsync(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics,
        DiagnosticStatistics statistics,
        bool groupBySimilarity,
        double similarityThreshold,
        CancellationToken cancellationToken = default
    )
    {
        var totalSw = Stopwatch.StartNew();
        var processingStats = new ProcessingStats();

        // LEVEL 1: Apply Heuristic Rules
        var sw = Stopwatch.StartNew();
        var clusters = ApplyHeuristicRules(diagnostics, statistics);
        processingStats.HeuristicRulesMs = sw.ElapsedMilliseconds;

        // LEVEL 2: Semantic Clustering (если включено и доступно)
        if (groupBySimilarity && _semanticModeProvider != null)
        {
            sw.Restart();
            await EnrichWithSemanticClusteringAsync(
                clusters,
                diagnostics,
                similarityThreshold,
                cancellationToken
            );
            processingStats.ClusteringMs = sw.ElapsedMilliseconds;
        }

        // LEVEL 2: Pattern Detection
        sw.Restart();
        foreach (var cluster in clusters)
        {
            DetectPattern(cluster, diagnostics);
        }
        processingStats.PatternDetectionMs = sw.ElapsedMilliseconds;

        // LEVEL 2: Calculate Relevance Scores
        var relevanceScores = CalculateRelevanceScores(clusters);

        totalSw.Stop();
        processingStats.TotalMs = totalSw.ElapsedMilliseconds;
        processingStats.StatisticalAnalysisMs = 0; // Выполнен снаружи

        // Build Summary
        var summary = BuildSummary(clusters, processingStats);

        return new SemanticEnrichmentResult
        {
            Clusters = clusters,
            RelevanceScores = relevanceScores,
            Summary = summary,
        };
    }

    // ========================================================================
    // LEVEL 1: HEURISTIC RULES
    // ========================================================================

    /// <summary>
    /// Применяет эвристические правила для классификации диагностик
    /// </summary>
    private List<DiagnosticCluster> ApplyHeuristicRules(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics,
        DiagnosticStatistics statistics
    )
    {
        var clusters = new List<DiagnosticCluster>();

        foreach (var (diagnosticId, count) in statistics.CountByDiagnosticId)
        {
            var severity = statistics.SeverityByDiagnosticId[diagnosticId];
            var files = statistics.FilesByDiagnosticId[diagnosticId];
            var projects = statistics.ProjectsByDiagnosticId[diagnosticId];

            // Получить примеры диагностик
            var examples = diagnostics
                .Where(d => d.Diagnostic.Id == diagnosticId)
                .Take(5)
                .Select(d => new DiagnosticExample
                {
                    FilePath = d.FilePath,
                    Line = d.Diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Snippet = GetSnippet(d.Diagnostic),
                    SimilarityToCentroid = 1.0, // Placeholder
                })
                .ToList();

            // Apply Heuristic Rules
            var (category, recommendedSeverity, confidence, justification) = ClassifyDiagnostic(
                diagnosticId,
                count,
                files.ToList(),
                projects.ToList(),
                severity
            );

            var cluster = new DiagnosticCluster
            {
                DiagnosticId = diagnosticId,
                Category = category,
                Pattern = "", // Will be detected later
                Occurrences = count,
                ConfidenceScore = confidence,
                RecommendedSeverity = recommendedSeverity,
                Justification = justification,
                Severity = severity,
                RepresentativeExamples = examples,
                AffectedFiles = files.ToList(),
                AffectedProjects = projects.ToList(),
            };

            clusters.Add(cluster);
        }

        _logger.LogInformation("Applied heuristic rules to {Count} clusters", clusters.Count);
        return clusters;
    }

    /// <summary>
    /// Классифицирует диагностику по эвристическим правилам
    /// </summary>
    private (
        DiagnosticCategory Category,
        EditorConfigSeverity RecommendedSeverity,
        double Confidence,
        string Justification
    ) ClassifyDiagnostic(
        string diagnosticId,
        int count,
        List<string> files,
        List<string> projects,
        DiagnosticSeverity severity
    )
    {
        // Rule 1: CA1873 Logging Optimization (False Positive in logging infrastructure)
        if (diagnosticId == "CA1873")
        {
            // Проверяем, связано ли с логированием
            var loggingRelated = files.Any(f =>
                f.Contains("Log", StringComparison.OrdinalIgnoreCase)
                || f.Contains("Logger", StringComparison.OrdinalIgnoreCase)
            );

            if (loggingRelated && count > 10)
            {
                return (
                    DiagnosticCategory.FalsePositive,
                    EditorConfigSeverity.None,
                    0.9,
                    "Массовые срабатывания CA1873 в логировании - ложное срабатывание. "
                        + "Предлагаем отключить через .editorconfig."
                );
            }
        }

        // Rule 2: CA1822 in BenchmarkDotNet (Infrastructure)
        if (diagnosticId == "CA1822")
        {
            var benchmarkRelated = files.Any(f =>
                f.Contains("Benchmark", StringComparison.OrdinalIgnoreCase)
            );

            if (benchmarkRelated)
            {
                return (
                    DiagnosticCategory.Infrastructure,
                    EditorConfigSeverity.None,
                    0.85,
                    "CA1822 в BenchmarkDotNet - методы должны быть instance. "
                        + "Отключаем для файлов бенчмарков."
                );
            }
        }

        // Rule 3: Performance Critical (CA1829, CA1854, CA1845)
        if (
            diagnosticId == "CA1829"
            || diagnosticId == "CA1854"
            || diagnosticId == "CA1845"
            || diagnosticId == "CA1860"
        )
        {
            return (
                DiagnosticCategory.PerformanceCritical,
                EditorConfigSeverity.Warning,
                0.8,
                "Критичная оптимизация производительности - рекомендуем исправить."
            );
        }

        // Rule 4: Security (CA2xxx)
        if (diagnosticId.StartsWith("CA2"))
        {
            return (
                DiagnosticCategory.Security,
                EditorConfigSeverity.Error,
                0.75,
                "Проблема безопасности - требует внимания."
            );
        }

        // Rule 5: Style Issues (IDExxxx)
        if (diagnosticId.StartsWith("IDE"))
        {
            return (
                DiagnosticCategory.Style,
                EditorConfigSeverity.Silent,
                0.7,
                "Стилистическая рекомендация - можно игнорировать или настроить."
            );
        }

        // Rule 6: Maintainability (CA15xx)
        if (diagnosticId.StartsWith("CA15"))
        {
            return (
                DiagnosticCategory.Maintainability,
                EditorConfigSeverity.Suggestion,
                0.7,
                "Рекомендация по поддерживаемости кода."
            );
        }

        // Default: Needs Manual Review
        return (
            DiagnosticCategory.NeedsManualReview,
            severity >= DiagnosticSeverity.Warning
                ? EditorConfigSeverity.Warning
                : EditorConfigSeverity.Suggestion,
            0.5,
            "Требует ручной проверки для определения категории."
        );
    }

    // ========================================================================
    // LEVEL 2: SEMANTIC CLUSTERING
    // ========================================================================

    /// <summary>
    /// Обогащает кластеры семантическим анализом с embeddings
    /// </summary>
    private async Task EnrichWithSemanticClusteringAsync(
        List<DiagnosticCluster> clusters,
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics,
        double similarityThreshold,
        CancellationToken cancellationToken
    )
    {
        if (_semanticModeProvider == null)
        {
            _logger.LogWarning("Semantic mode provider not available, skipping clustering");
            return;
        }

        try
        {
            // Проверяем доступность semantic mode
            var availability = await _semanticModeProvider.CheckAvailabilityAsync(
                cancellationToken
            );
            if (!availability.IsAvailable)
            {
                _logger.LogWarning(
                    "Semantic mode not available, skipping clustering. Source: {Source}",
                    availability.Source
                );
                return;
            }

            _logger.LogInformation(
                "Semantic mode available: {Source}, Model: {Model}",
                availability.Source,
                availability.ModelName
            );

            // TODO: Implement semantic clustering with embeddings
            // Это будет реализовано в следующих итерациях
            _logger.LogInformation("Semantic clustering not yet implemented");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich with semantic clustering");
        }
    }

    // ========================================================================
    // LEVEL 2: PATTERN DETECTION
    // ========================================================================

    /// <summary>
    /// Определяет паттерн для кластера диагностик
    /// </summary>
    private void DetectPattern(
        DiagnosticCluster cluster,
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics
    )
    {
        // Анализируем сообщения диагностик
        var messages = diagnostics
            .Where(d => d.Diagnostic.Id == cluster.DiagnosticId)
            .Select(d => d.Diagnostic.GetMessage())
            .Distinct()
            .ToList();

        // Простое определение паттерна по ключевым словам
        if (messages.Any(m => m.Contains("logging", StringComparison.OrdinalIgnoreCase)))
        {
            cluster.Pattern = "Logging infrastructure";
        }
        else if (
            messages.Any(m => m.Contains("performance", StringComparison.OrdinalIgnoreCase))
        )
        {
            cluster.Pattern = "Performance optimization";
        }
        else if (messages.Any(m => m.Contains("security", StringComparison.OrdinalIgnoreCase)))
        {
            cluster.Pattern = "Security concern";
        }
        else if (messages.Any(m => m.Contains("style", StringComparison.OrdinalIgnoreCase)))
        {
            cluster.Pattern = "Code style";
        }
        else
        {
            cluster.Pattern = cluster.Category.ToString();
        }
    }

    // ========================================================================
    // LEVEL 2: RELEVANCE SCORING
    // ========================================================================

    /// <summary>
    /// Вычисляет relevance scores для диагностик
    /// Формула: Score = CategoryWeight × SeverityWeight × ConfidenceWeight
    /// </summary>
    private Dictionary<string, double> CalculateRelevanceScores(
        List<DiagnosticCluster> clusters
    )
    {
        var scores = new Dictionary<string, double>();

        foreach (var cluster in clusters)
        {
            // Category Weight (0-10)
            var categoryWeight = cluster.Category switch
            {
                DiagnosticCategory.Security => 10.0,
                DiagnosticCategory.Reliability => 9.0,
                DiagnosticCategory.PerformanceCritical => 8.0,
                DiagnosticCategory.Maintainability => 6.0,
                DiagnosticCategory.Infrastructure => 5.0,
                DiagnosticCategory.Style => 3.0,
                DiagnosticCategory.FalsePositive => 1.0,
                DiagnosticCategory.NeedsManualReview => 5.0,
                _ => 5.0,
            };

            // Severity Weight (0-1)
            var severityWeight = cluster.Severity switch
            {
                DiagnosticSeverity.Error => 1.0,
                DiagnosticSeverity.Warning => 0.7,
                DiagnosticSeverity.Info => 0.4,
                DiagnosticSeverity.Hidden => 0.2,
                _ => 0.5,
            };

            // Confidence Weight (0-1)
            var confidenceWeight = cluster.ConfidenceScore;

            // Final Score (0-10)
            var score = categoryWeight * severityWeight * confidenceWeight;

            scores[cluster.DiagnosticId] = Math.Round(score, 2);
        }

        return scores;
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    /// <summary>
    /// Извлекает snippet из диагностики
    /// </summary>
    private static string GetSnippet(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        if (location == null || !location.IsInSource)
            return diagnostic.GetMessage();

        var sourceTree = location.SourceTree;
        if (sourceTree == null)
            return diagnostic.GetMessage();

        var span = location.SourceSpan;
        var text = sourceTree.GetText();
        var snippet = text.ToString(span);

        // Ограничиваем длину snippet
        const int maxLength = 100;
        if (snippet.Length > maxLength)
        {
            snippet = snippet.Substring(0, maxLength) + "...";
        }

        return snippet;
    }

    /// <summary>
    /// Строит summary для результата enrichment
    /// </summary>
    private EnrichmentSummary BuildSummary(
        List<DiagnosticCluster> clusters,
        ProcessingStats processingStats
    )
    {
        var categoriesFound = new Dictionary<DiagnosticCategory, int>();
        foreach (var cluster in clusters)
        {
            if (!categoriesFound.ContainsKey(cluster.Category))
            {
                categoriesFound[cluster.Category] = 0;
            }
            categoriesFound[cluster.Category]++;
        }

        var totalDiagnostics = clusters.Sum(c => c.Occurrences);
        var autoSuppressRecommendations = clusters.Count(c =>
            c.RecommendedSeverity == EditorConfigSeverity.None && c.ConfidenceScore >= 0.8
        );
        var criticalIssuesFound = clusters.Count(c =>
            c.Category == DiagnosticCategory.Security
            || c.Category == DiagnosticCategory.Reliability
        );
        var manualReviewRequired = clusters.Count(c =>
            c.Category == DiagnosticCategory.NeedsManualReview || c.ConfidenceScore < 0.8
        );

        return new EnrichmentSummary
        {
            TotalDiagnostics = totalDiagnostics,
            TotalClusters = clusters.Count,
            CategoriesFound = categoriesFound,
            AutoSuppressRecommendations = autoSuppressRecommendations,
            CriticalIssuesFound = criticalIssuesFound,
            ManualReviewRequired = manualReviewRequired,
            ProcessingStats = processingStats,
        };
    }
}

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
                "Semantic mode available: {Source}, Model: {Model}, Dimension: {Dimension}",
                availability.Source,
                availability.ModelName,
                availability.VectorDimension
            );

            // Шаг 1: Собираем все уникальные сообщения диагностик
            var messagesToEmbed = new Dictionary<string, List<string>>(); // DiagnosticId -> List<Message>
            foreach (var cluster in clusters)
            {
                var messages = diagnostics
                    .Where(d => d.Diagnostic.Id == cluster.DiagnosticId)
                    .Select(d => d.Diagnostic.GetMessage())
                    .Distinct()
                    .ToList();

                if (messages.Count > 0)
                {
                    messagesToEmbed[cluster.DiagnosticId] = messages;
                }
            }

            if (messagesToEmbed.Count == 0)
            {
                _logger.LogWarning("No messages to embed for clustering");
                return;
            }

            _logger.LogInformation(
                "Generating embeddings for {ClusterCount} diagnostic types with {MessageCount} unique messages",
                messagesToEmbed.Count,
                messagesToEmbed.Values.Sum(m => m.Count)
            );

            // Шаг 2: Генерируем embeddings для всех сообщений
            var embeddingCache = new Dictionary<string, float[]>(); // Message -> Embedding
            foreach (var kvp in messagesToEmbed)
            {
                var diagnosticId = kvp.Key;
                var messages = kvp.Value;

                foreach (var message in messages)
                {
                    if (embeddingCache.ContainsKey(message))
                        continue;

                    try
                    {
                        var embedding = await _semanticModeProvider.GetEmbeddingAsync(
                            message,
                            cancellationToken
                        );
                        if (embedding != null && embedding.Length > 0)
                        {
                            embeddingCache[message] = embedding;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to generate embedding for message: {Message}",
                            message.Length > 50 ? message.Substring(0, 50) + "..." : message
                        );
                    }
                }
            }

            _logger.LogInformation(
                "Generated {EmbeddingCount} embeddings successfully",
                embeddingCache.Count
            );

            if (embeddingCache.Count == 0)
            {
                _logger.LogWarning("No embeddings generated, skipping clustering");
                return;
            }

            // Шаг 3: Вычисляем центроиды для каждого кластера
            var clusterCentroids = new Dictionary<string, float[]>(); // DiagnosticId -> Centroid
            foreach (var cluster in clusters)
            {
                if (!messagesToEmbed.ContainsKey(cluster.DiagnosticId))
                    continue;

                var messages = messagesToEmbed[cluster.DiagnosticId];
                var embeddings = messages
                    .Select(m => embeddingCache.TryGetValue(m, out var emb) ? emb : null)
                    .Where(e => e != null)
                    .ToList();

                if (embeddings.Count > 0)
                {
                    var centroid = ComputeCentroid(embeddings!);
                    clusterCentroids[cluster.DiagnosticId] = centroid;
                }
            }

            _logger.LogInformation(
                "Computed {CentroidCount} cluster centroids",
                clusterCentroids.Count
            );

            // Шаг 4: Находим семантически похожие кластеры
            var similarClusters = FindSimilarClusters(
                clusterCentroids,
                similarityThreshold
            );

            if (similarClusters.Count > 0)
            {
                _logger.LogInformation(
                    "Found {PairCount} pairs of similar clusters (threshold: {Threshold:F2})",
                    similarClusters.Count,
                    similarityThreshold
                );

                // Обновляем confidence и pattern для похожих кластеров
                foreach (var (id1, id2, similarity) in similarClusters)
                {
                    var cluster1 = clusters.FirstOrDefault(c => c.DiagnosticId == id1);
                    var cluster2 = clusters.FirstOrDefault(c => c.DiagnosticId == id2);

                    if (cluster1 != null && cluster2 != null)
                    {
                        // Повышаем confidence если оба кластера в одной категории
                        if (cluster1.Category == cluster2.Category)
                        {
                            cluster1.ConfidenceScore = Math.Min(
                                1.0,
                                cluster1.ConfidenceScore + 0.05
                            );
                            cluster2.ConfidenceScore = Math.Min(
                                1.0,
                                cluster2.ConfidenceScore + 0.05
                            );

                            _logger.LogDebug(
                                "Increased confidence for similar clusters: {Id1} <-> {Id2} (similarity: {Similarity:F2})",
                                id1,
                                id2,
                                similarity
                            );
                        }
                    }
                }
            }

            // Шаг 5: Обновляем similarity scores в примерах
            foreach (var cluster in clusters)
            {
                if (!clusterCentroids.ContainsKey(cluster.DiagnosticId))
                    continue;

                var centroid = clusterCentroids[cluster.DiagnosticId];

                foreach (var example in cluster.RepresentativeExamples)
                {
                    // Находим embedding для snippet
                    if (embeddingCache.TryGetValue(example.Snippet, out var exampleEmbedding))
                    {
                        example.SimilarityToCentroid = CalculateCosineSimilarity(
                            centroid,
                            exampleEmbedding
                        );
                    }
                    else
                    {
                        // Fallback: ищем embedding для любого похожего сообщения
                        var diagnosticMessages = diagnostics
                            .Where(d =>
                                d.Diagnostic.Id == cluster.DiagnosticId
                                && d.Diagnostic.Location.SourceTree != null
                            )
                            .Select(d => d.Diagnostic.GetMessage())
                            .FirstOrDefault();

                        if (
                            diagnosticMessages != null
                            && embeddingCache.TryGetValue(diagnosticMessages, out var msgEmbedding)
                        )
                        {
                            example.SimilarityToCentroid = CalculateCosineSimilarity(
                                centroid,
                                msgEmbedding
                            );
                        }
                    }
                }
            }

            _logger.LogInformation("Semantic clustering complete");
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

    // ========================================================================
    // SEMANTIC CLUSTERING HELPERS
    // ========================================================================

    /// <summary>
    /// Вычисляет центроид (средний вектор) для набора embeddings
    /// </summary>
    private float[] ComputeCentroid(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            throw new ArgumentException("Cannot compute centroid for empty list");

        var dimension = embeddings[0].Length;
        var centroid = new float[dimension];

        // Суммируем все векторы
        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < dimension; i++)
            {
                centroid[i] += embedding[i];
            }
        }

        // Делим на количество векторов
        for (int i = 0; i < dimension; i++)
        {
            centroid[i] /= embeddings.Count;
        }

        return centroid;
    }

    /// <summary>
    /// Находит пары похожих кластеров на основе центроидов
    /// </summary>
    private List<(string Id1, string Id2, double Similarity)> FindSimilarClusters(
        Dictionary<string, float[]> centroids,
        double threshold
    )
    {
        var similarPairs = new List<(string, string, double)>();
        var ids = centroids.Keys.ToList();

        // Попарно сравниваем все центроиды
        for (int i = 0; i < ids.Count; i++)
        {
            for (int j = i + 1; j < ids.Count; j++)
            {
                var id1 = ids[i];
                var id2 = ids[j];

                var similarity = CalculateCosineSimilarity(centroids[id1], centroids[id2]);

                if (similarity >= threshold)
                {
                    similarPairs.Add((id1, id2, similarity));
                }
            }
        }

        return similarPairs.OrderByDescending(p => p.Item3).ToList();
    }

    /// <summary>
    /// Вычисляет cosine similarity между двумя векторами
    /// </summary>
    private double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension");

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        normA = Math.Sqrt(normA);
        normB = Math.Sqrt(normB);

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (normA * normB);
    }
}

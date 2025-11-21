using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Test.Common;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace UltrasharpTools.Test.SemanticAnalysis;

/// <summary>
/// Тест для Semantic Code Analysis Enrichment (Phase 1 + Phase 2)
/// </summary>
public class TestSemanticEnrichment
{
    public static async Task<int> Run(string[] args)
    {
        // Parse command line arguments
        var provider = args.Length > 0 ? args[0].ToLower() : "memory";
        var enableSemanticClustering = args.Length > 1 && args[1].ToLower() == "true";

        Console.WriteLine("=== UltrasharpTools Semantic Enrichment Test ===");
        Console.WriteLine();
        Console.WriteLine($"Provider: {provider.ToUpper()}");
        Console.WriteLine($"Semantic Clustering: {(enableSemanticClustering ? "ENABLED" : "DISABLED")}");
        Console.WriteLine();

        // Setup DI
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // Core services
        services.WithUltrasharpToolsServices(enableGit: false);

        // Semantic services (if enabled)
        if (enableSemanticClustering && provider != "memory")
        {
            // TODO: Add semantic RAG services
            Console.WriteLine("⚠️  Semantic clustering with embeddings requires semantic RAG setup");
            Console.WriteLine("    Running with semantic clustering disabled");
            enableSemanticClustering = false;
        }

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();
            var diagnosticService = serviceProvider.GetRequiredService<IDiagnosticService>();
            var logger = serviceProvider.GetRequiredService<ILogger<TestSemanticEnrichment>>();

            // Load UltrasharpTools solution (self-testing)
            var solutionPath = @"D:\github\ultrasharp-tools-mcp\UltrasharpTools.sln";

            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"ERROR: Solution not found: {solutionPath}");
                Console.WriteLine("Please update the path in TestSemanticEnrichment.cs");
                return 1;
            }

            Console.WriteLine("=== PHASE 1: Loading Solution ===");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();
            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);
            sw.Stop();

            Console.WriteLine($"✅ Solution loaded in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine();

            // Test 1: Baseline analysis (without enrichment)
            await TestBaselineAnalysis(diagnosticService, solutionPath, logger);

            // Test 2: Semantic enrichment (without clustering)
            await TestSemanticEnrichment_WithoutClustering(diagnosticService, solutionPath, logger);

            // Test 3: Semantic enrichment (with clustering, if enabled)
            if (enableSemanticClustering)
            {
                await TestSemanticEnrichment_WithClustering(
                    diagnosticService,
                    solutionPath,
                    logger
                );
            }

            // Test 4: EditorConfig generation
            await TestEditorConfigGeneration(diagnosticService, solutionPath, logger);

            Console.WriteLine();
            Console.WriteLine("=== ALL TESTS COMPLETED SUCCESSFULLY ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("=== ERROR ===");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);

            return 1;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    private static async Task TestBaselineAnalysis(
        IDiagnosticService diagnosticService,
        string solutionPath,
        ILogger logger
    )
    {
        Console.WriteLine("=== TEST 1: Baseline Analysis (No Enrichment) ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        var filterOptions = new DiagnosticFilterOptions
        {
            SeverityFilter = DiagnosticSeverity.Info,
            Skip = 0,
            Take = 100,
            EnrichWithSemantics = false,
            GenerateEditorConfigRecommendations = false,
        };

        var result = await diagnosticService.AnalyzeAsync(
            solutionPath,
            filterOptions,
            CancellationToken.None
        );

        sw.Stop();

        Console.WriteLine($"Total diagnostics: {result.TotalCount}");
        Console.WriteLine($"Returned: {result.Diagnostics.Count}");
        Console.WriteLine($"Has more: {result.HasMore}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();

        // Group by DiagnosticId
        var byId = result.Diagnostics.GroupBy(d => d.Diagnostic.Id).OrderByDescending(g => g.Count());
        Console.WriteLine("Top 10 diagnostic IDs:");
        foreach (var group in byId.Take(10))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} occurrences");
        }

        Console.WriteLine();
        Console.WriteLine($"✅ Baseline analysis completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }

    private static async Task TestSemanticEnrichment_WithoutClustering(
        IDiagnosticService diagnosticService,
        string solutionPath,
        ILogger logger
    )
    {
        Console.WriteLine("=== TEST 2: Semantic Enrichment (Without Clustering) ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        var filterOptions = new DiagnosticFilterOptions
        {
            SeverityFilter = DiagnosticSeverity.Info,
            Skip = 0,
            Take = 1000, // Увеличиваем чтобы захватить больше диагностик
            EnrichWithSemantics = true,
            GroupBySimilarity = false, // Без кластеризации
            SimilarityThreshold = 0.85,
            GenerateEditorConfigRecommendations = false,
        };

        var result = await diagnosticService.AnalyzeAsync(
            solutionPath,
            filterOptions,
            CancellationToken.None
        );

        sw.Stop();

        Console.WriteLine($"Total diagnostics: {result.TotalCount}");
        Console.WriteLine($"Enrichment available: {result.SemanticEnrichment != null}");
        Console.WriteLine();

        if (result.SemanticEnrichment != null)
        {
            var enrichment = result.SemanticEnrichment;
            Console.WriteLine("Enrichment Summary:");
            Console.WriteLine($"  Total clusters: {enrichment.Summary.TotalClusters}");
            Console.WriteLine($"  Total diagnostics: {enrichment.Summary.TotalDiagnostics}");
            Console.WriteLine(
                $"  Auto-suppress recommendations: {enrichment.Summary.AutoSuppressRecommendations}"
            );
            Console.WriteLine(
                $"  Critical issues: {enrichment.Summary.CriticalIssuesFound}"
            );
            Console.WriteLine(
                $"  Manual review required: {enrichment.Summary.ManualReviewRequired}"
            );
            Console.WriteLine();

            Console.WriteLine("Processing Stats:");
            var stats = enrichment.Summary.ProcessingStats;
            Console.WriteLine($"  Heuristic rules: {stats.HeuristicRulesMs}ms");
            Console.WriteLine($"  Pattern detection: {stats.PatternDetectionMs}ms");
            Console.WriteLine($"  Total: {stats.TotalMs}ms");
            Console.WriteLine();

            Console.WriteLine("Categories Found:");
            foreach (var cat in enrichment.Summary.CategoriesFound.OrderByDescending(c => c.Value))
            {
                Console.WriteLine($"  {cat.Key}: {cat.Value} clusters");
            }
            Console.WriteLine();

            Console.WriteLine("Top 10 Relevance Scores:");
            foreach (var score in enrichment.RelevanceScores.OrderByDescending(s => s.Value).Take(10))
            {
                Console.WriteLine($"  {score.Key}: {score.Value:F2}");
            }
            Console.WriteLine();

            Console.WriteLine("Sample Clusters:");
            foreach (var cluster in enrichment.Clusters.OrderByDescending(c => c.Occurrences).Take(5))
            {
                Console.WriteLine($"  {cluster.DiagnosticId}:");
                Console.WriteLine($"    Category: {cluster.Category}");
                Console.WriteLine($"    Pattern: {cluster.Pattern}");
                Console.WriteLine($"    Occurrences: {cluster.Occurrences}");
                Console.WriteLine($"    Confidence: {cluster.ConfidenceScore:F2}");
                Console.WriteLine($"    Recommended: {cluster.RecommendedSeverity}");
                Console.WriteLine($"    Justification: {cluster.Justification}");
                Console.WriteLine();
            }
        }

        Console.WriteLine($"✅ Semantic enrichment test completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }

    private static async Task TestSemanticEnrichment_WithClustering(
        IDiagnosticService diagnosticService,
        string solutionPath,
        ILogger logger
    )
    {
        Console.WriteLine("=== TEST 3: Semantic Enrichment (With Clustering) ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        var filterOptions = new DiagnosticFilterOptions
        {
            SeverityFilter = DiagnosticSeverity.Info,
            Skip = 0,
            Take = 1000,
            EnrichWithSemantics = true,
            GroupBySimilarity = true, // С кластеризацией
            SimilarityThreshold = 0.85,
            GenerateEditorConfigRecommendations = false,
        };

        var result = await diagnosticService.AnalyzeAsync(
            solutionPath,
            filterOptions,
            CancellationToken.None
        );

        sw.Stop();

        Console.WriteLine($"Total diagnostics: {result.TotalCount}");
        Console.WriteLine($"Enrichment available: {result.SemanticEnrichment != null}");
        Console.WriteLine();

        if (result.SemanticEnrichment != null)
        {
            var enrichment = result.SemanticEnrichment;
            var stats = enrichment.Summary.ProcessingStats;

            Console.WriteLine("Processing Stats (with clustering):");
            Console.WriteLine($"  Heuristic rules: {stats.HeuristicRulesMs}ms");
            Console.WriteLine($"  Clustering: {stats.ClusteringMs}ms");
            Console.WriteLine($"  Pattern detection: {stats.PatternDetectionMs}ms");
            Console.WriteLine($"  Total: {stats.TotalMs}ms");
            Console.WriteLine();

            Console.WriteLine("Clustering Results:");
            var clustersWithUpdatedConfidence = enrichment.Clusters.Count(c =>
                c.ConfidenceScore > 0.85
            );
            Console.WriteLine(
                $"  High-confidence clusters (>0.85): {clustersWithUpdatedConfidence}"
            );
            Console.WriteLine();

            Console.WriteLine("Sample Clusters with Similarity Scores:");
            foreach (var cluster in enrichment.Clusters.OrderByDescending(c => c.ConfidenceScore).Take(5))
            {
                Console.WriteLine($"  {cluster.DiagnosticId}:");
                Console.WriteLine($"    Confidence: {cluster.ConfidenceScore:F2}");
                Console.WriteLine($"    Examples:");
                foreach (var example in cluster.RepresentativeExamples.Take(2))
                {
                    Console.WriteLine(
                        $"      {example.FilePath}:{example.Line} (similarity: {example.SimilarityToCentroid:F2})"
                    );
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine(
            $"✅ Semantic enrichment with clustering completed in {sw.ElapsedMilliseconds}ms"
        );
        Console.WriteLine();
    }

    private static async Task TestEditorConfigGeneration(
        IDiagnosticService diagnosticService,
        string solutionPath,
        ILogger logger
    )
    {
        Console.WriteLine("=== TEST 4: EditorConfig Generation ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        var filterOptions = new DiagnosticFilterOptions
        {
            SeverityFilter = DiagnosticSeverity.Info,
            Skip = 0,
            Take = 1000,
            EnrichWithSemantics = true,
            GroupBySimilarity = false,
            GenerateEditorConfigRecommendations = true,
            EditorConfigFormat = "Detailed",
        };

        var result = await diagnosticService.AnalyzeAsync(
            solutionPath,
            filterOptions,
            CancellationToken.None
        );

        sw.Stop();

        Console.WriteLine($"EditorConfig available: {result.EditorConfigRecommendations != null}");
        Console.WriteLine();

        if (result.EditorConfigRecommendations != null)
        {
            var editorConfig = result.EditorConfigRecommendations;

            Console.WriteLine("EditorConfig Stats:");
            Console.WriteLine($"  Total rules: {editorConfig.Stats.TotalRulesGenerated}");
            Console.WriteLine($"  Auto-approved: {editorConfig.Stats.AutoApprovedRules}");
            Console.WriteLine($"  Needs review: {editorConfig.Stats.NeedsReviewRules}");
            Console.WriteLine();

            Console.WriteLine("Rules by Category:");
            foreach (var cat in editorConfig.Stats.ByCategory.OrderByDescending(c => c.Value))
            {
                Console.WriteLine($"  {cat.Key}: {cat.Value} rules");
            }
            Console.WriteLine();

            Console.WriteLine("Sample Rules (first 5):");
            foreach (var rule in editorConfig.Rules.Take(5))
            {
                Console.WriteLine($"  {rule.DiagnosticId} ({rule.Category}):");
                Console.WriteLine($"    {rule.EditorConfigLine}");
                Console.WriteLine($"    Justification: {rule.Justification}");
                Console.WriteLine(
                    $"    Stats: {rule.Statistics.Occurrences} occurrences in {rule.Statistics.AffectedFiles} files"
                );
                Console.WriteLine();
            }

            Console.WriteLine("Manual Review Cases (first 3):");
            foreach (var reviewCase in editorConfig.RequiresManualReview.Take(3))
            {
                Console.WriteLine($"  {reviewCase.DiagnosticId}:");
                Console.WriteLine($"    Reason: {reviewCase.Reason}");
                Console.WriteLine($"    Confidence: {reviewCase.ConfidenceScore:F2}");
                Console.WriteLine($"    Occurrences: {reviewCase.Occurrences}");
                Console.WriteLine($"    Suggested actions:");
                foreach (var action in reviewCase.SuggestedActions)
                {
                    Console.WriteLine($"      - {action}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Summary (first 500 chars):");
            Console.WriteLine(
                editorConfig.Summary.Length > 500
                    ? string.Concat(editorConfig.Summary.AsSpan(0, 500), "...")
                    : editorConfig.Summary
            );
            Console.WriteLine();

            // Сохраняем .editorconfig в файл для проверки
            var outputPath = Path.Combine(
                Path.GetTempPath(),
                $"test-editorconfig-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
            );
            await File.WriteAllTextAsync(outputPath, editorConfig.Content);
            Console.WriteLine($"📄 Full .editorconfig saved to: {outputPath}");
            Console.WriteLine();
        }

        Console.WriteLine($"✅ EditorConfig generation completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }
}

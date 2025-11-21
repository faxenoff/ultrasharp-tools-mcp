using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Models.SemanticEnrichment;
using UltrasharpTools.Tools.Services;

namespace UltrasharpTools.Test.SemanticAnalysis;

/// <summary>
/// Быстрый юнит-тест для Semantic Enrichment (без Roslyn, с мок-данными)
/// </summary>
public class TestSemanticEnrichmentUnit
{
    public static async Task<int> Run()
    {
        Console.WriteLine("=== UltrasharpTools Semantic Enrichment Unit Test ===");
        Console.WriteLine();

        try
        {
            // Test 1: SemanticDiagnosticEnricher - Heuristic Rules
            await TestHeuristicRules();

            // Test 2: SemanticDiagnosticEnricher - Statistics
            await TestStatistics();

            // Test 3: EditorConfigGenerator
            await TestEditorConfigGenerator();

            Console.WriteLine();
            Console.WriteLine("=== ALL UNIT TESTS PASSED ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("=== TEST FAILED ===");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task TestHeuristicRules()
    {
        Console.WriteLine("=== TEST 1: Heuristic Rules ===");
        Console.WriteLine();

        var enricher = new SemanticDiagnosticEnricher(null, NullLogger<SemanticDiagnosticEnricher>.Instance);

        // Mock diagnostics for different heuristic rules
        var diagnostics = new List<(Diagnostic Diagnostic, string FilePath, string ProjectName)>
        {
            // CA1873 - FalsePositive (0.9) - need >10 occurrences in Log* files
            CreateMockDiagnostic("CA1873", "Logger.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogWriter.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogProvider.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogService.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogManager.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogHandler.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LoggerFactory.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LoggingService.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogExtensions.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogHelper.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1873", "LogUtils.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),

            // CA1822 - Infrastructure (0.85)
            CreateMockDiagnostic("CA1822", "BenchmarkRunner.cs", "UltrasharpTools.Benchmarks", DiagnosticSeverity.Warning),
            CreateMockDiagnostic("CA1822", "PerformanceTests.cs", "UltrasharpTools.Benchmarks", DiagnosticSeverity.Warning),

            // CA1829 - PerformanceCritical (0.8)
            CreateMockDiagnostic("CA1829", "FastIndex.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1829", "VectorStore.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Info),

            // CA2000 - Security (0.75)
            CreateMockDiagnostic("CA2000", "FileHandler.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Warning),

            // IDE0005 - Style (0.7)
            CreateMockDiagnostic("IDE0005", "Program.cs", "UltrasharpTools.Test", DiagnosticSeverity.Info),

            // Unknown - NeedsManualReview (0.5)
            CreateMockDiagnostic("CS8600", "NullableContext.cs", "UltrasharpTools.Tools", DiagnosticSeverity.Warning),
        };

        // Analyze statistics
        var statistics = enricher.AnalyzeStatistics(diagnostics);

        Console.WriteLine($"Total diagnostics: {diagnostics.Count}");
        Console.WriteLine($"Unique diagnostic IDs: {statistics.CountByDiagnosticId.Count}");
        Console.WriteLine();

        // Enrich with heuristic rules (without clustering)
        var result = await enricher.EnrichAsync(
            diagnostics,
            statistics,
            groupBySimilarity: false,
            similarityThreshold: 0.85,
            CancellationToken.None
        );

        Console.WriteLine("Enrichment Results:");
        Console.WriteLine($"  Total clusters: {result.Summary.TotalClusters}");
        Console.WriteLine($"  Auto-suppress recommendations: {result.Summary.AutoSuppressRecommendations}");
        Console.WriteLine($"  Critical issues: {result.Summary.CriticalIssuesFound}");
        Console.WriteLine($"  Manual review required: {result.Summary.ManualReviewRequired}");
        Console.WriteLine();

        // Verify heuristic classifications
        var ca1873Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "CA1873");
        var ca1822Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "CA1822");
        var ca1829Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "CA1829");
        var ca2000Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "CA2000");
        var ide0005Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "IDE0005");
        var cs8600Cluster = result.Clusters.FirstOrDefault(c => c.DiagnosticId == "CS8600");

        Console.WriteLine("Cluster Verifications:");
        VerifyCluster("CA1873", ca1873Cluster, DiagnosticCategory.FalsePositive, 0.9, EditorConfigSeverity.None);
        VerifyCluster("CA1822", ca1822Cluster, DiagnosticCategory.Infrastructure, 0.85, EditorConfigSeverity.None);
        VerifyCluster("CA1829", ca1829Cluster, DiagnosticCategory.PerformanceCritical, 0.8, EditorConfigSeverity.Warning);
        VerifyCluster("CA2000", ca2000Cluster, DiagnosticCategory.Security, 0.75, EditorConfigSeverity.Error);
        VerifyCluster("IDE0005", ide0005Cluster, DiagnosticCategory.Style, 0.7, EditorConfigSeverity.Silent);
        VerifyCluster("CS8600", cs8600Cluster, DiagnosticCategory.NeedsManualReview, 0.5, EditorConfigSeverity.Warning);

        Console.WriteLine();
        Console.WriteLine("✅ Heuristic rules test passed");
        Console.WriteLine();
    }

    private static async Task TestStatistics()
    {
        Console.WriteLine("=== TEST 2: Statistics Analysis ===");
        Console.WriteLine();

        var enricher = new SemanticDiagnosticEnricher(null, NullLogger<SemanticDiagnosticEnricher>.Instance);

        // Mock diagnostics with duplicates
        var diagnostics = new List<(Diagnostic Diagnostic, string FilePath, string ProjectName)>
        {
            CreateMockDiagnostic("CA1829", "File1.cs", "Project1", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1829", "File2.cs", "Project1", DiagnosticSeverity.Info),
            CreateMockDiagnostic("CA1829", "File3.cs", "Project2", DiagnosticSeverity.Info),
            CreateMockDiagnostic("IDE0005", "File1.cs", "Project1", DiagnosticSeverity.Info),
            CreateMockDiagnostic("IDE0005", "File1.cs", "Project1", DiagnosticSeverity.Info), // Duplicate file
        };

        var statistics = enricher.AnalyzeStatistics(diagnostics);

        Console.WriteLine("Statistics Verification:");
        Console.WriteLine($"  Total diagnostics: {diagnostics.Count}");
        Console.WriteLine($"  CA1829 count: {statistics.CountByDiagnosticId["CA1829"]} (expected: 3)");
        Console.WriteLine($"  CA1829 files: {statistics.FilesByDiagnosticId["CA1829"].Count} (expected: 3)");
        Console.WriteLine($"  CA1829 projects: {statistics.ProjectsByDiagnosticId["CA1829"].Count} (expected: 2)");
        Console.WriteLine($"  IDE0005 count: {statistics.CountByDiagnosticId["IDE0005"]} (expected: 2)");
        Console.WriteLine($"  IDE0005 files: {statistics.FilesByDiagnosticId["IDE0005"].Count} (expected: 1, deduplicated)");
        Console.WriteLine();

        // Verify
        if (statistics.CountByDiagnosticId["CA1829"] != 3)
            throw new Exception("CA1829 count mismatch");
        if (statistics.FilesByDiagnosticId["CA1829"].Count != 3)
            throw new Exception("CA1829 files count mismatch");
        if (statistics.ProjectsByDiagnosticId["CA1829"].Count != 2)
            throw new Exception("CA1829 projects count mismatch");
        if (statistics.CountByDiagnosticId["IDE0005"] != 2)
            throw new Exception("IDE0005 count mismatch");
        if (statistics.FilesByDiagnosticId["IDE0005"].Count != 1)
            throw new Exception("IDE0005 files should be deduplicated");

        Console.WriteLine("✅ Statistics analysis test passed");
        Console.WriteLine();
    }

    private static async Task TestEditorConfigGenerator()
    {
        Console.WriteLine("=== TEST 3: EditorConfig Generation ===");
        Console.WriteLine();

        var generator = new EditorConfigGenerator(NullLogger<EditorConfigGenerator>.Instance);

        // Mock enrichment result
        var enrichmentResult = new SemanticEnrichmentResult
        {
            Clusters = new List<DiagnosticCluster>
            {
                // High confidence - auto-approve
                new DiagnosticCluster
                {
                    DiagnosticId = "CA1873",
                    Category = DiagnosticCategory.FalsePositive,
                    Pattern = "Logging optimization",
                    Occurrences = 10,
                    ConfidenceScore = 0.9,
                    RecommendedSeverity = EditorConfigSeverity.None,
                    Justification = "False positive for logging scenarios",
                    RepresentativeExamples = new List<DiagnosticExample>
                    {
                        new DiagnosticExample { FilePath = "Logger.cs", Line = 42, Snippet = "logger.LogInformation(...)" }
                    }
                },
                // Low confidence - needs review
                new DiagnosticCluster
                {
                    DiagnosticId = "CS8600",
                    Category = DiagnosticCategory.NeedsManualReview,
                    Pattern = "Nullable reference",
                    Occurrences = 5,
                    ConfidenceScore = 0.5,
                    RecommendedSeverity = EditorConfigSeverity.Warning,
                    Justification = "Unclear nullable context",
                    RepresentativeExamples = new List<DiagnosticExample>
                    {
                        new DiagnosticExample { FilePath = "NullableContext.cs", Line = 10, Snippet = "string? value = null;" }
                    }
                },
            },
            RelevanceScores = new Dictionary<string, double>
            {
                { "CA1873", 0.95 },
                { "CS8600", 0.3 },
            },
            Summary = new EnrichmentSummary
            {
                TotalClusters = 2,
                TotalDiagnostics = 15,
                AutoSuppressRecommendations = 1,
                CriticalIssuesFound = 0,
                ManualReviewRequired = 1,
                CategoriesFound = new Dictionary<DiagnosticCategory, int>
                {
                    { DiagnosticCategory.FalsePositive, 1 },
                    { DiagnosticCategory.NeedsManualReview, 1 },
                },
                ProcessingStats = new ProcessingStats
                {
                    HeuristicRulesMs = 10,
                    PatternDetectionMs = 5,
                    TotalMs = 15,
                }
            }
        };

        var options = new EditorConfigOptions
        {
            Format = EditorConfigFormat.Detailed,
            GroupByCategory = true,
            IncludeStatistics = true,
        };

        var result = await generator.GenerateAsync(enrichmentResult, options, CancellationToken.None);

        Console.WriteLine("EditorConfig Stats:");
        Console.WriteLine($"  Total rules: {result.Stats.TotalRulesGenerated}");
        Console.WriteLine($"  Auto-approved: {result.Stats.AutoApprovedRules}");
        Console.WriteLine($"  Needs review: {result.Stats.NeedsReviewRules}");
        Console.WriteLine();

        // Verify stats
        if (result.Stats.TotalRulesGenerated != 1)
            throw new Exception("Expected 1 auto-approved rule");
        if (result.Stats.AutoApprovedRules != 1)
            throw new Exception("Expected 1 auto-approved rule");
        if (result.Stats.NeedsReviewRules != 1)
            throw new Exception("Expected 1 needs-review rule");

        Console.WriteLine("Sample Auto-Approved Rule:");
        var autoRule = result.Rules.FirstOrDefault();
        if (autoRule != null)
        {
            Console.WriteLine($"  {autoRule.DiagnosticId}: {autoRule.EditorConfigLine}");
            Console.WriteLine($"  Justification: {autoRule.Justification}");
        }
        Console.WriteLine();

        Console.WriteLine("Sample Manual Review Case:");
        var reviewCase = result.RequiresManualReview.FirstOrDefault();
        if (reviewCase != null)
        {
            Console.WriteLine($"  {reviewCase.DiagnosticId}");
            Console.WriteLine($"  Reason: {reviewCase.Reason}");
            Console.WriteLine($"  Confidence: {reviewCase.ConfidenceScore:F2}");
        }
        Console.WriteLine();

        Console.WriteLine("Generated .editorconfig (first 500 chars):");
        Console.WriteLine(result.Content.Length > 500 ? string.Concat(result.Content.AsSpan(0, 500), "...") : result.Content);
        Console.WriteLine();

        Console.WriteLine("✅ EditorConfig generation test passed");
        Console.WriteLine();
    }

    // Helper methods
    private static (Diagnostic Diagnostic, string FilePath, string ProjectName) CreateMockDiagnostic(
        string id,
        string filePath,
        string projectName,
        DiagnosticSeverity severity
    )
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            $"Mock {id}",
            $"Mock message for {id}",
            "MockCategory",
            severity,
            isEnabledByDefault: true
        );

        var diagnostic = Diagnostic.Create(descriptor, Location.None);
        return (diagnostic, filePath, projectName);
    }

    private static void VerifyCluster(
        string id,
        DiagnosticCluster? cluster,
        DiagnosticCategory expectedCategory,
        double expectedConfidence,
        EditorConfigSeverity expectedSeverity
    )
    {
        if (cluster == null)
            throw new Exception($"  ❌ {id}: Cluster not found");

        var categoryMatch = cluster.Category == expectedCategory;
        var confidenceMatch = Math.Abs(cluster.ConfidenceScore - expectedConfidence) < 0.01;
        var severityMatch = cluster.RecommendedSeverity == expectedSeverity;

        Console.WriteLine($"  {(categoryMatch && confidenceMatch && severityMatch ? "✅" : "❌")} {id}:");
        Console.WriteLine($"      Category: {cluster.Category} (expected: {expectedCategory}) {(categoryMatch ? "✓" : "✗")}");
        Console.WriteLine($"      Confidence: {cluster.ConfidenceScore:F2} (expected: {expectedConfidence:F2}) {(confidenceMatch ? "✓" : "✗")}");
        Console.WriteLine($"      Severity: {cluster.RecommendedSeverity} (expected: {expectedSeverity}) {(severityMatch ? "✓" : "✗")}");

        if (!categoryMatch || !confidenceMatch || !severityMatch)
            throw new Exception($"{id} verification failed");
    }
}

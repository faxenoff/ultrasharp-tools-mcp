using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Test.Common;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Services;

namespace UltrasharpTools.Test.SemanticAnalysis;

/// <summary>
/// Интеграционные тесты для семантических инструментов анализа кода
/// (semantic_search, semantic_diff, detect_code_clones)
/// </summary>
public class SemanticAnalysisTest
{
    public static async Task Main(string[] args)
    {
        // Load configuration
        var config = TestConfiguration.Load();

        // Parse command line arguments
        var provider = args.Length > 0 ? args[0].ToLower() : "memory";
        var dimension = args.Length > 1 && int.TryParse(args[1], out var dim) ? dim : 384;

        Console.WriteLine("=== UltrasharpTools Semantic Analysis Tools Test ===");
        Console.WriteLine();
        Console.WriteLine($"Provider: {provider.ToUpper()}");
        Console.WriteLine($"Dimension: {dimension}");
        Console.WriteLine();

        // Setup DI
        var serviceProvider = TestServiceProvider.CreateForSemanticAnalysisTest(
            config,
            provider,
            dimension
        );

        try
        {
            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();
            var codeAnalysisService = serviceProvider.GetRequiredService<ICodeAnalysisService>();
            var semanticSearchService = serviceProvider.GetRequiredService<SemanticSearchService>();
            var logger = serviceProvider.GetRequiredService<
                ILogger<UltrasharpTools.Tools.Mcp.Tools.SemanticAnalysisToolsLogCategory>
            >();

            // Load UltrasharpTools solution (self-testing)
            var solutionPath = @"D:\github\ultrasharp-tools-mcp\UltrasharpTools.sln";

            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"ERROR: Solution not found: {solutionPath}");
                Console.WriteLine("Please update the path in Program.cs");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("=== PHASE 1: Loading Solution ===");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();
            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);
            sw.Stop();

            Console.WriteLine($"✅ Solution loaded in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine();

            // Test 1: semantic_search
            await TestSemanticSearch(semanticSearchService, solutionManager, logger);

            // Test 2: semantic_diff
            await TestSemanticDiff(
                semanticSearchService,
                solutionManager,
                codeAnalysisService,
                logger
            );

            // Test 3: detect_code_clones
            await TestDetectCodeClones(semanticSearchService, solutionManager, logger);

            Console.WriteLine();
            Console.WriteLine("=== ALL TESTS COMPLETED SUCCESSFULLY ===");
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

            Environment.ExitCode = 1;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    private static async Task TestSemanticSearch(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ILogger<UltrasharpTools.Tools.Mcp.Tools.SemanticAnalysisToolsLogCategory> logger
    )
    {
        Console.WriteLine("=== TEST 1: semantic_search ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        // Test query 1: Find error handling code
        Console.WriteLine("Query 1: 'error handling with try-catch'");
        var result1 = await SemanticAnalysisTools.SemanticSearch(
            searchService,
            solutionManager,
            logger,
            query: "error handling with try-catch",
            scope: "solution",
            topK: 5,
            minSimilarity: 0.7f,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(
            JsonSerializer.Serialize(result1, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine();

        // Test query 2: Find logging code
        Console.WriteLine("Query 2: 'logging information messages'");
        var result2 = await SemanticAnalysisTools.SemanticSearch(
            searchService,
            solutionManager,
            logger,
            query: "logging information messages",
            scope: "solution",
            topK: 5,
            minSimilarity: 0.7f,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(
            JsonSerializer.Serialize(result2, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine();

        // Test query 3: Specific scope (methods only)
        Console.WriteLine("Query 3: 'async task completion' (methods only)");
        var result3 = await SemanticAnalysisTools.SemanticSearch(
            searchService,
            solutionManager,
            logger,
            query: "async task completion",
            scope: "methods",
            topK: 5,
            minSimilarity: 0.7f,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(
            JsonSerializer.Serialize(result3, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine();

        sw.Stop();
        Console.WriteLine($"✅ semantic_search test completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }

    private static async Task TestSemanticDiff(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ICodeAnalysisService codeAnalysisService,
        ILogger<UltrasharpTools.Tools.Mcp.Tools.SemanticAnalysisToolsLogCategory> logger
    )
    {
        Console.WriteLine("=== TEST 2: semantic_diff ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        // For this test, we'll compare two similar methods from the codebase
        // First, find some methods using semantic search
        Console.WriteLine("Finding similar methods to compare...");

        // Find methods related to "load solution"
        var searchResult = await SemanticAnalysisTools.SemanticSearch(
            searchService,
            solutionManager,
            logger,
            query: "load solution from file path",
            scope: "methods",
            topK: 10,
            minSimilarity: 0.7f,
            cancellationToken: CancellationToken.None
        );

        // Extract FQNs from search results
        dynamic? searchData = JsonSerializer.Deserialize<dynamic>(
            JsonSerializer.Serialize(searchResult)
        );
        if (searchData == null)
        {
            Console.WriteLine("⚠️ No search results for semantic_diff test");
            return;
        }

        // Try to get the first two results
        var resultsProperty = searchData.GetProperty("results");
        if (resultsProperty.GetArrayLength() < 2)
        {
            Console.WriteLine("⚠️ Not enough results for semantic_diff test");
            return;
        }

        var fqn1 = resultsProperty[0].GetProperty("fqn").GetString();
        var fqn2 = resultsProperty[1].GetProperty("fqn").GetString();

        if (fqn1 == null || fqn2 == null)
        {
            Console.WriteLine("⚠️ Could not extract FQNs from search results");
            return;
        }

        Console.WriteLine($"Comparing:");
        Console.WriteLine($"  Before: {fqn1}");
        Console.WriteLine($"  After:  {fqn2}");
        Console.WriteLine();

        // Compare the two methods
        var diffResult = await SemanticAnalysisTools.SemanticDiff(
            searchService,
            solutionManager,
            codeAnalysisService,
            logger,
            beforeFqn: fqn1,
            afterFqn: fqn2,
            includeImplementationDetails: true,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(
            JsonSerializer.Serialize(diffResult, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine();

        sw.Stop();
        Console.WriteLine($"✅ semantic_diff test completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }

    private static async Task TestDetectCodeClones(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ILogger<UltrasharpTools.Tools.Mcp.Tools.SemanticAnalysisToolsLogCategory> logger
    )
    {
        Console.WriteLine("=== TEST 3: detect_code_clones ===");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();

        Console.WriteLine("Scanning for code clones with minSimilarity=0.85...");
        Console.WriteLine();

        var result = await SemanticAnalysisTools.DetectCodeClones(
            searchService,
            solutionManager,
            logger,
            minSimilarity: 0.85f,
            mode: "semantic",
            membersOnly: true,
            maxGroups: 10,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
        );
        Console.WriteLine();

        sw.Stop();
        Console.WriteLine($"✅ detect_code_clones test completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();
    }
}

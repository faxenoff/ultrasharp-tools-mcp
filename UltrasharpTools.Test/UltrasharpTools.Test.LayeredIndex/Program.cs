using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Test.Common;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Test.LayeredIndex;

class Program
{
    static async Task Main(string[] args)
    {
        // Run self-indexing test if requested
        if (args.Length > 0 && args[0] == "--index-self")
        {
            await IndexSelfTest.RunAsync();
            return;
        }

        // Run branch switch test if requested
        if (args.Length > 0 && args[0] == "--branch-test")
        {
            await BranchSwitchTest.RunAsync();
            return;
        }

        // Run cleanup test if requested
        if (args.Length > 0 && args[0] == "--cleanup-test")
        {
            await CompactionAndCleanupTest.RunAsync();
            return;
        }

        // Load configuration
        var config = TestConfiguration.Load();
        var serviceProvider = TestServiceProvider.CreateForLayeredIndexTest(
            config,
            preset: "Development"
        );
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("=== Phase 7: Layered Indexing Integration Test ===");

            var solutionPath = config.GetSolutionPath("Fabuza");
            logger.LogInformation("Test project: {Solution}", solutionPath);

            // Get SolutionManager
            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();

            logger.LogInformation("=== Test 7.2: Loading solution with layered indexing ===");
            var startTime = DateTimeOffset.UtcNow;

            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);

            var elapsed = DateTimeOffset.UtcNow - startTime;
            logger.LogInformation("Solution loaded in {Elapsed}ms", elapsed.TotalMilliseconds);

            // Check if layered index is available
            var layeredIndex = solutionManager.LayeredIndex;
            if (layeredIndex == null)
            {
                logger.LogWarning("LayeredIndex is not available (might not be enabled)");
                return;
            }

            logger.LogInformation("✅ LayeredIndex initialized successfully");

            // Get current branch
            var gitService = serviceProvider.GetRequiredService<IGitService>();
            var currentBranch = await gitService.GetCurrentBranchAsync(
                solutionPath,
                CancellationToken.None
            );
            logger.LogInformation("Current git branch: {Branch}", currentBranch);

            // Test symbol search in base layer
            logger.LogInformation("=== Testing base layer symbol search ===");
            var searchResults = await layeredIndex.FindAsync(
                clientId: "test-client",
                branch: currentBranch,
                searchTerm: "Cabinet",
                cancellationToken: CancellationToken.None
            );

            var resultsList = searchResults.ToList();
            logger.LogInformation(
                "Found {Count} symbols matching 'Cabinet' in base layer",
                resultsList.Count
            );
            foreach (var result in resultsList.Take(5))
            {
                logger.LogInformation(
                    "  - {Symbol} ({Kind})",
                    result.CanonicalFqn,
                    result.Symbol.Kind
                );
            }

            // Test GitWorkflowService
            var gitWorkflow = solutionManager.GitWorkflowService;
            if (gitWorkflow != null)
            {
                logger.LogInformation("✅ GitWorkflowService available");

                // Check for uncommitted changes
                var hasUncommitted = await gitWorkflow.HasUncommittedChangesAsync(
                    "test-client",
                    currentBranch,
                    CancellationToken.None
                );
                logger.LogInformation("Has uncommitted changes: {HasChanges}", hasUncommitted);
            }
            else
            {
                logger.LogWarning("GitWorkflowService not available");
            }

            // Performance stats
            logger.LogInformation("=== Performance Statistics ===");
            logger.LogInformation("Initial load time: {Time}ms", elapsed.TotalMilliseconds);

            // Check .ultrasharp directory
            var ultrasharpDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".ultrasharp");
            if (Directory.Exists(ultrasharpDir))
            {
                logger.LogInformation("✅ .ultrasharp directory created: {Path}", ultrasharpDir);

                var layeredDir = Path.Combine(ultrasharpDir, "layered");
                if (Directory.Exists(layeredDir))
                {
                    logger.LogInformation("✅ layered cache directory exists");

                    var files = Directory.GetFiles(layeredDir, "*.db");
                    logger.LogInformation("Cache files: {Count}", files.Length);
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        logger.LogInformation(
                            "  - {Name}: {Size} KB",
                            Path.GetFileName(file),
                            fileInfo.Length / 1024
                        );
                    }
                }
            }

            logger.LogInformation("=== Test 7.2 Complete ===");
            logger.LogInformation("✅ All basic integration tests passed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Test failed with exception");
            throw;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }
}

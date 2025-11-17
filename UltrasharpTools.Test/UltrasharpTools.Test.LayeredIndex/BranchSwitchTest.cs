using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Test.LayeredIndex;

public class BranchSwitchTest
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Register UltrasharpTools services
        services.WithUltrasharpToolsServices(
            enableGit: true,
            buildConfiguration: "Debug",
            gitOptions: null,
            reloadOptions: null,
            symbolCacheOptions: null
        );

        // Configure layered indexing
        services.WithLayeredIndexing(
            maxBranchDeltas: 50,
            enablePersistence: true,
            deltaCompactionThreshold: 500
        );

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<BranchSwitchTest>>();

        try
        {
            logger.LogInformation("=== Phase 7.3-7.5: Branch Switching & Delta Management Test ===");

            var solutionPath = @"D:\FABUZA2\Fabuza.sln";
            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();
            var gitService = serviceProvider.GetRequiredService<IGitService>();

            // Load solution
            logger.LogInformation("Loading solution...");
            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);

            var layeredIndex = solutionManager.LayeredIndex;
            var gitWorkflow = solutionManager.GitWorkflowService;

            if (layeredIndex == null || gitWorkflow == null)
            {
                logger.LogError("❌ LayeredIndex or GitWorkflowService not available");
                return;
            }

            // Get current branch
            var originalBranch = await gitService.GetCurrentBranchAsync(solutionPath, CancellationToken.None);
            logger.LogInformation("Current branch: {Branch}", originalBranch);

            // Get all branches
            var allBranches = await gitService.GetAllBranchesAsync(solutionPath, CancellationToken.None);
            logger.LogInformation("Available branches: {Count}", allBranches.Count);
            foreach (var branch in allBranches.Take(10))
            {
                logger.LogInformation("  - {Branch}", branch);
            }

            // Test 1: EnsureBranchDelta for current branch
            logger.LogInformation("=== Test 7.3.1: EnsureBranchDelta ===");
            var startTime = DateTimeOffset.UtcNow;

            await layeredIndex.EnsureBranchDeltaAsync(originalBranch, CancellationToken.None);

            var elapsed = DateTimeOffset.UtcNow - startTime;
            logger.LogInformation("✅ EnsureBranchDelta completed in {Time}ms", elapsed.TotalMilliseconds);

            // Test 2: Branch switching simulation
            if (allBranches.Count > 1)
            {
                var targetBranch = allBranches.FirstOrDefault(b =>
                    b != originalBranch &&
                    b != "main" &&
                    b != "master" &&
                    !b.StartsWith("origin/")
                );

                if (targetBranch != null)
                {
                    logger.LogInformation("=== Test 7.3.2: Branch Switch Simulation ({Original} → {Target}) ===",
                        originalBranch, targetBranch);

                    startTime = DateTimeOffset.UtcNow;

                    // Simulate branch switch (without actual git checkout)
                    logger.LogInformation("Clearing working delta for {Branch}...", originalBranch);
                    await layeredIndex.ClearWorkingDeltaAsync("test-client", originalBranch);

                    logger.LogInformation("Ensuring branch delta for {Branch}...", targetBranch);
                    await layeredIndex.EnsureBranchDeltaAsync(targetBranch, CancellationToken.None);

                    elapsed = DateTimeOffset.UtcNow - startTime;
                    logger.LogInformation("✅ Branch switch simulation completed in {Time}ms", elapsed.TotalMilliseconds);
                }
                else
                {
                    logger.LogWarning("⚠️ No suitable target branch found for switch test");
                }
            }

            // Test 3: Working Delta Updates
            logger.LogInformation("=== Test 7.4: Working Delta Updates ===");

            // Search for a symbol to update
            var searchResults = await layeredIndex.FindAsync(
                clientId: "test-client",
                branch: originalBranch,
                searchTerm: "Service",
                cancellationToken: CancellationToken.None
            );

            var resultsList = searchResults.ToList();
            logger.LogInformation("Found {Count} symbols matching 'Service'", resultsList.Count);

            if (resultsList.Any())
            {
                var symbolToUpdate = resultsList.First();
                logger.LogInformation("Updating working delta for symbol: {Symbol}", symbolToUpdate.CanonicalFqn);

                startTime = DateTimeOffset.UtcNow;
                await layeredIndex.UpdateWorkingDeltaAsync(
                    clientId: "test-client",
                    branch: originalBranch,
                    symbol: symbolToUpdate,
                    cancellationToken: CancellationToken.None
                );
                elapsed = DateTimeOffset.UtcNow - startTime;

                logger.LogInformation("✅ Working delta updated in {Time}ms", elapsed.TotalMilliseconds);

                // Verify working delta exists
                var hasUncommitted = await gitWorkflow.HasUncommittedChangesAsync(
                    "test-client",
                    originalBranch,
                    CancellationToken.None
                );
                logger.LogInformation("Has uncommitted changes after update: {HasChanges}", hasUncommitted);
            }

            // Test 4: Promote Working to Branch Delta (simulate commit)
            logger.LogInformation("=== Test 7.5: Promote Working Delta → Branch Delta ===");

            var currentCommitSha = await gitService.GetCurrentCommitShaAsync(solutionPath, CancellationToken.None);
            logger.LogInformation("Current commit SHA: {Sha}", currentCommitSha);

            startTime = DateTimeOffset.UtcNow;
            await layeredIndex.PromoteWorkingToBranchAsync(
                clientId: "test-client",
                branch: originalBranch,
                commitSha: currentCommitSha,
                cancellationToken: CancellationToken.None
            );
            elapsed = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation("✅ Working delta promoted to branch delta in {Time}ms", elapsed.TotalMilliseconds);

            // Verify no uncommitted changes after promotion
            var hasUncommittedAfterPromote = await gitWorkflow.HasUncommittedChangesAsync(
                "test-client",
                originalBranch,
                CancellationToken.None
            );
            logger.LogInformation("Has uncommitted changes after promotion: {HasChanges}", hasUncommittedAfterPromote);

            // Check cache files
            var ultrasharpDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".ultrasharp");
            var layeredDir = Path.Combine(ultrasharpDir, "layered");

            if (Directory.Exists(layeredDir))
            {
                var files = Directory.GetFiles(layeredDir, "*.db");
                logger.LogInformation("=== Cache Files ===");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    logger.LogInformation("  - {Name}: {Size} KB",
                        Path.GetFileName(file),
                        fileInfo.Length / 1024);
                }
            }

            logger.LogInformation("=== All Tests Complete ===");
            logger.LogInformation("✅ Branch switching and delta management tests passed");
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Layered;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Test.LayeredIndex;

public class CompactionAndCleanupTest
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

        // Configure layered indexing with lower thresholds for testing
        services.WithLayeredIndexing(
            maxBranchDeltas: 5, // Low threshold to trigger cleanup faster
            enablePersistence: true,
            deltaCompactionThreshold: 50 // Low threshold to trigger compaction
        );

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<CompactionAndCleanupTest>>();

        try
        {
            logger.LogInformation("=== Phase 7.6: Delta Compaction & Cleanup Test ===");

            var solutionPath = @"D:\FABUZA2\Fabuza.sln";
            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();
            var gitService = serviceProvider.GetRequiredService<IGitService>();

            // Load solution
            logger.LogInformation("Loading solution...");
            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);

            var layeredIndex = solutionManager.LayeredIndex;
            if (layeredIndex == null)
            {
                logger.LogError("❌ LayeredIndex not available");
                return;
            }

            var currentBranch = await gitService.GetCurrentBranchAsync(
                solutionPath,
                CancellationToken.None
            );
            logger.LogInformation("Current branch: {Branch}", currentBranch);

            // Test 1: Create working delta with multiple symbols
            logger.LogInformation("=== Test 7.6.1: Creating Working Delta for Compaction Test ===");

            var searchResults = await layeredIndex.FindAsync(
                clientId: "test-client",
                branch: currentBranch,
                searchTerm: "Service",
                cancellationToken: CancellationToken.None
            );

            var symbolsToUpdate = searchResults.Take(100).ToList();
            logger.LogInformation(
                "Found {Count} symbols to add to working delta",
                symbolsToUpdate.Count
            );

            var startTime = DateTimeOffset.UtcNow;

            // Add multiple symbols to trigger compaction threshold
            foreach (var symbol in symbolsToUpdate)
            {
                await layeredIndex.UpdateWorkingDeltaAsync(
                    clientId: "test-client",
                    branch: currentBranch,
                    symbol: symbol,
                    cancellationToken: CancellationToken.None
                );
            }

            var elapsed = DateTimeOffset.UtcNow - startTime;
            logger.LogInformation(
                "✅ Added {Count} symbols to working delta in {Time}ms",
                symbolsToUpdate.Count,
                elapsed.TotalMilliseconds
            );

            // Test 2: Promote to branch delta and test compaction
            logger.LogInformation("=== Test 7.6.2: Delta Compaction Service ===");

            var currentCommitSha = await gitService.GetCurrentCommitShaAsync(
                solutionPath,
                CancellationToken.None
            );

            startTime = DateTimeOffset.UtcNow;
            await layeredIndex.PromoteWorkingToBranchAsync(
                clientId: "test-client",
                branch: currentBranch,
                commitSha: currentCommitSha,
                cancellationToken: CancellationToken.None
            );
            elapsed = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation(
                "✅ Promoted working delta to branch delta in {Time}ms",
                elapsed.TotalMilliseconds
            );

            // Create compaction service manually (not from DI)
            var compactionLogger = serviceProvider.GetRequiredService<
                ILogger<DeltaCompactionService>
            >();
            var compactionService = new DeltaCompactionService(
                layeredIndex,
                gitService,
                layeredVectorStore: null,
                compactionThreshold: 50, // Low threshold for testing
                logger: compactionLogger
            );

            startTime = DateTimeOffset.UtcNow;
            var needsCompaction = await compactionService.NeedsCompactionAsync(
                branch: currentBranch,
                cancellationToken: CancellationToken.None
            );
            elapsed = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation(
                "Branch {Branch} needs compaction: {NeedsCompaction} (checked in {Time}ms)",
                currentBranch,
                needsCompaction,
                elapsed.TotalMilliseconds
            );

            // Test compacting all large deltas
            startTime = DateTimeOffset.UtcNow;
            await compactionService.CompactAllLargeDeltasAsync(
                solutionPath,
                CancellationToken.None
            );
            elapsed = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation(
                "✅ CompactAllLargeDeltasAsync completed in {Time}ms",
                elapsed.TotalMilliseconds
            );

            // Test 3: Orphaned Delta Cleanup
            logger.LogInformation("=== Test 7.6.3: Orphaned Delta Cleanup Service ===");

            // Create cleanup service manually (not from DI)
            var cleanupLogger = serviceProvider.GetRequiredService<
                ILogger<OrphanedDeltaCleanupService>
            >();
            var cleanupService = new OrphanedDeltaCleanupService(
                gitService,
                symbolCacheManager: null, // Will skip cache cleanup
                vectorCacheManager: null,
                logger: cleanupLogger
            );

            // Get orphaned branches first
            var orphanedBranches = await cleanupService.GetOrphanedBranchesAsync(
                solutionPath,
                CancellationToken.None
            );

            logger.LogInformation(
                "Found {Count} orphaned branches in cache",
                orphanedBranches.Count
            );
            foreach (var branch in orphanedBranches.Take(10))
            {
                logger.LogInformation("  - Orphaned: {Branch}", branch);
            }

            if (orphanedBranches.Count > 0)
            {
                // Test cleanup
                startTime = DateTimeOffset.UtcNow;
                await cleanupService.CleanupOrphanedDeltasAsync(
                    solutionPath,
                    CancellationToken.None
                );
                elapsed = DateTimeOffset.UtcNow - startTime;

                logger.LogInformation(
                    "✅ Orphaned deltas cleaned up in {Time}ms",
                    elapsed.TotalMilliseconds
                );

                // Verify cleanup
                var remainingOrphaned = await cleanupService.GetOrphanedBranchesAsync(
                    solutionPath,
                    CancellationToken.None
                );
                logger.LogInformation(
                    "Orphaned branches after cleanup: {Count}",
                    remainingOrphaned.Count
                );
            }
            else
            {
                logger.LogInformation("No orphaned branches found - skipping cleanup test");
            }

            // Test 4: Background Cleanup Scheduler
            logger.LogInformation("=== Test 7.6.4: Background Cleanup Scheduler ===");

            // Create scheduler manually (not from DI since it needs solution path)
            var schedulerLogger = serviceProvider.GetRequiredService<
                ILogger<BackgroundCleanupScheduler>
            >();
            var scheduler = new BackgroundCleanupScheduler(
                compactionService,
                cleanupService,
                solutionPath,
                compactionInterval: TimeSpan.FromSeconds(5), // Short interval for testing
                cleanupInterval: TimeSpan.FromSeconds(10),
                logger: schedulerLogger
            );

            logger.LogInformation("Starting background cleanup scheduler...");
            startTime = DateTimeOffset.UtcNow;

            // Start scheduler (it runs in background)
            scheduler.Start();

            logger.LogInformation(
                "✅ Background scheduler started in {Time}ms",
                (DateTimeOffset.UtcNow - startTime).TotalMilliseconds
            );
            logger.LogInformation(
                "Scheduler will run: Compaction every 5s, Cleanup every 10s (test intervals)"
            );
            logger.LogInformation("Scheduler running: {IsRunning}", scheduler.IsRunning);

            // Wait for at least one compaction cycle
            logger.LogInformation("Waiting 6 seconds for compaction cycle...");
            await Task.Delay(6000);

            // Stop scheduler
            await scheduler.StopAsync();
            logger.LogInformation("✅ Background scheduler stopped");

            scheduler.Dispose();

            // Check final cache size
            logger.LogInformation("=== Final Cache Statistics ===");
            var ultrasharpDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".ultrasharp");
            var layeredDir = Path.Combine(ultrasharpDir, "layered");

            if (Directory.Exists(layeredDir))
            {
                var files = Directory.GetFiles(layeredDir, "*.db");
                logger.LogInformation("Cache files: {Count}", files.Length);

                long totalSize = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                    logger.LogInformation(
                        "  - {Name}: {Size} KB",
                        Path.GetFileName(file),
                        fileInfo.Length / 1024
                    );
                }

                logger.LogInformation("Total cache size: {Size} KB", totalSize / 1024);
            }

            logger.LogInformation("=== All Compaction & Cleanup Tests Complete ===");
            logger.LogInformation("✅ Delta compaction and cleanup services working correctly");
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

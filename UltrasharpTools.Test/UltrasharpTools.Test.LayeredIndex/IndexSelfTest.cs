using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Test.LayeredIndex;

public class IndexSelfTest
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
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
        var logger = serviceProvider.GetRequiredService<ILogger<IndexSelfTest>>();

        try
        {
            logger.LogInformation("=== Индексация UltrasharpTools.sln ===");

            var solutionPath = @"D:\OneDrive\_mcp\ultrasharp-tools-mcp\UltrasharpTools.sln";

            if (!File.Exists(solutionPath))
            {
                logger.LogError("Solution file not found: {Path}", solutionPath);
                return;
            }

            var solutionManager = serviceProvider.GetRequiredService<ISolutionManager>();

            var startTime = DateTimeOffset.UtcNow;
            await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);
            var elapsed = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation("✅ Solution loaded in {Elapsed}ms", elapsed.TotalMilliseconds);

            // Check .ultrasharp directory
            var ultrasharpDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".ultrasharp");
            if (Directory.Exists(ultrasharpDir))
            {
                logger.LogInformation("✅ .ultrasharp directory created: {Path}", ultrasharpDir);

                var files = Directory.GetFiles(ultrasharpDir, "*.*", SearchOption.AllDirectories);
                logger.LogInformation("Files created: {Count}", files.Length);
                foreach (var file in files.OrderByDescending(f => new FileInfo(f).Length).Take(10))
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(ultrasharpDir, file);
                    logger.LogInformation(
                        "  - {File}: {Size} KB",
                        relativePath,
                        fileInfo.Length / 1024
                    );
                }
            }
            else
            {
                logger.LogWarning("❌ .ultrasharp directory NOT created");
            }

            // Test symbol search
            var layeredIndex = solutionManager.LayeredIndex;
            if (layeredIndex != null)
            {
                logger.LogInformation("=== Testing symbol search ===");

                var gitService = serviceProvider.GetRequiredService<IGitService>();
                var currentBranch = await gitService.GetCurrentBranchAsync(
                    solutionPath,
                    CancellationToken.None
                );
                logger.LogInformation("Current branch: {Branch}", currentBranch);

                var results = await layeredIndex.FindAsync(
                    "test-client",
                    currentBranch,
                    "SolutionManager",
                    CancellationToken.None
                );

                var resultsList = results.ToList();
                logger.LogInformation(
                    "Found {Count} symbols matching 'SolutionManager'",
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
            }
            else
            {
                logger.LogWarning("LayeredIndex not available");
            }

            logger.LogInformation("=== Индексация завершена ===");

            // Wait for background cache save to complete (non-blocking Task.Run in FastSymbolIndex)
            logger.LogInformation("Ждём сохранения кеша символов (5 сек)...");
            await Task.Delay(5000);

            // Check if cache was saved
            var symbolCachePath = Path.Combine(ultrasharpDir, "cache", "symbols");
            if (Directory.Exists(symbolCachePath))
            {
                var cacheFiles = Directory.GetFiles(symbolCachePath, "*.json");
                if (cacheFiles.Length > 0)
                {
                    foreach (var file in cacheFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        logger.LogInformation(
                            "✅ Symbol cache saved: {File} ({Size} MB)",
                            Path.GetFileName(file),
                            fileInfo.Length / (1024 * 1024)
                        );
                    }
                }
                else
                {
                    logger.LogWarning("❌ No symbol cache files found");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Ошибка индексации");
            throw;
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }
}

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Test.Common;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Merge.Engine;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Test.SemanticMerge;

/// <summary>
/// Тест 3-way merge на расходящихся ветках Umbraco CMS (v13/dev vs v14/dev)
/// </summary>
public class UmbracoMergeTest
{
    public static async Task Main(string[] args)
    {
        // Load configuration
        var config = TestConfiguration.Load();

        // Парсинг аргументов командной строки
        var provider = args.Length > 0 ? args[0].ToLower() : config.SemanticRag.Provider;
        var dimension =
            args.Length > 1 && int.TryParse(args[1], out var dim)
                ? dim
                : config.SemanticRag.Dimension;
        var ollamaModel = args.Length > 2 ? args[2] : "granite-embedding:latest";

        Console.WriteLine("=== Umbraco CMS 3-Way Merge Test (v13/dev vs v14/dev) ===");
        Console.WriteLine();
        Console.WriteLine("Scenario:");
        Console.WriteLine("  Base:    merge-base (db1d999) - common ancestor");
        Console.WriteLine("  BranchA: v13/dev (23b09b1) - +125 commits from base");
        Console.WriteLine(
            "  BranchB: v14/dev (9ab0abc) - +1,222 commits from base (major version!)"
        );
        Console.WriteLine();
        Console.WriteLine($"Provider: {provider.ToUpper()}");
        Console.WriteLine($"Dimension: {dimension}");
        if (provider == "ollama")
        {
            Console.WriteLine($"Model: {ollamaModel}");
        }
        Console.WriteLine();

        var umbracoRoot = config.GetSolutionPath("Umbraco");

        // Настройка DI
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Меньше шума
        });

        // Конфигурируем provider
        if (provider == "ollama")
        {
            services.WithSemanticRag(
                databasePath: null,
                dimension: dimension,
                configureEmbedding: opts =>
                {
                    opts.Provider = "ollama";
                    opts.Ollama.Model = ollamaModel;
                    opts.Ollama.BaseUrl = "http://localhost:11434";
                    opts.Ollama.Concurrency = 8;
                }
            );
        }
        else // memory (default)
        {
            services.WithSemanticRag(
                databasePath: null,
                dimension: dimension,
                configureEmbedding: opts =>
                {
                    opts.Provider = "memory";
                }
            );
        }

        services.WithSemanticMerge();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            var extractor = serviceProvider.GetRequiredService<CodeUnitExtractor>();
            var merger = serviceProvider.GetRequiredService<ThreeWayMerger>();

            Console.WriteLine("=== PHASE 1: Indexing Files ===");
            Console.WriteLine();

            var sw = Stopwatch.StartNew();

            // Helper для сбора C# файлов из Umbraco
            List<string> CollectFiles(string rootDir)
            {
                var files = new List<string>();

                // Собираем основные проекты Umbraco
                var srcDir = Path.Combine(rootDir, "src");
                if (Directory.Exists(srcDir))
                {
                    // Основные проекты
                    var coreProjects = new[]
                    {
                        "Umbraco.Core",
                        "Umbraco.Infrastructure",
                        "Umbraco.Web",
                        "Umbraco.Cms.Api.Common",
                        "Umbraco.Cms.Api.Delivery",
                    };

                    foreach (var project in coreProjects)
                    {
                        var projectDir = Path.Combine(srcDir, project);
                        if (Directory.Exists(projectDir))
                        {
                            files.AddRange(
                                Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                            );
                            files.AddRange(
                                Directory.GetFiles(
                                    projectDir,
                                    "*.csproj",
                                    SearchOption.AllDirectories
                                )
                            );
                        }
                    }
                }

                return files;
            }

            // Helper для создания VersionedIndex
            VersionedIndex CreateVersionedIndex(
                string version,
                List<CodeUnit> units,
                long indexingTimeMs
            )
            {
                var unitsByType = units
                    .GroupBy(u => u.Type)
                    .ToDictionary(g => g.Key, g => g.Count());

                var fileCount = units.Select(u => u.FilePath).Distinct().Count();

                var statistics = new IndexStatistics
                {
                    TotalUnits = units.Count,
                    UnitsByType = unitsByType,
                    FileCount = fileCount,
                    UnitsWithEmbeddings = 0,
                    IndexingTimeMs = indexingTimeMs,
                };

                return new VersionedIndex
                {
                    Version = version,
                    CommitSha = null,
                    BranchName = null,
                    Units = units.ToDictionary(u => u.Id),
                    VectorStore = new VectorStore(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    Statistics = statistics,
                };
            }

            // Индексация Base
            Console.Write("Indexing Base (merge-base)... ");
            var baseDir = Path.Combine(umbracoRoot, "base");
            var baseFiles = CollectFiles(baseDir);
            var baseUnits = await extractor.ExtractFromFilesAsync(
                baseFiles,
                CancellationToken.None
            );
            extractor.BuildHierarchy(baseUnits);
            var baseIndex = CreateVersionedIndex("base", baseUnits, sw.ElapsedMilliseconds);
            Console.WriteLine(
                $"{baseUnits.Count} units ({baseFiles.Count} files) in {sw.ElapsedMilliseconds}ms"
            );

            // Индексация v13/dev
            sw.Restart();
            Console.Write("Indexing v13/dev... ");
            var v13Dir = Path.Combine(umbracoRoot, "v13");
            var v13Files = CollectFiles(v13Dir);
            var v13Units = await extractor.ExtractFromFilesAsync(v13Files, CancellationToken.None);
            extractor.BuildHierarchy(v13Units);
            var v13Index = CreateVersionedIndex("v13", v13Units, sw.ElapsedMilliseconds);
            Console.WriteLine(
                $"{v13Units.Count} units ({v13Files.Count} files) in {sw.ElapsedMilliseconds}ms"
            );

            // Индексация v14/dev
            sw.Restart();
            Console.Write("Indexing v14/dev... ");
            var v14Dir = Path.Combine(umbracoRoot, "v14");
            var v14Files = CollectFiles(v14Dir);
            var v14Units = await extractor.ExtractFromFilesAsync(v14Files, CancellationToken.None);
            extractor.BuildHierarchy(v14Units);
            var v14Index = CreateVersionedIndex("v14", v14Units, sw.ElapsedMilliseconds);
            Console.WriteLine(
                $"{v14Units.Count} units ({v14Files.Count} files) in {sw.ElapsedMilliseconds}ms"
            );

            Console.WriteLine();
            Console.WriteLine("=== PHASE 2: Three-Way Merge ===");
            Console.WriteLine();

            sw.Restart();

            var mergeResult = await merger.MergeAsync(
                baseIndex,
                v13Index,
                v14Index,
                CancellationToken.None
            );

            sw.Stop();

            Console.WriteLine(
                $"Merge completed in {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalSeconds:F1}s)"
            );
            Console.WriteLine();

            // Анализ результатов
            Console.WriteLine("=== MERGE RESULTS ===");
            Console.WriteLine();

            var stats = mergeResult.Statistics;

            Console.WriteLine($"Total Changes:       {stats.TotalChanges}");
            Console.WriteLine($"Auto-Merged:         {stats.AutoMergedChanges}");
            Console.WriteLine($"Conflicts Detected:  {stats.ConflictCount}");
            Console.WriteLine($"Success:             {(mergeResult.IsSuccess ? "Yes" : "No")}");
            Console.WriteLine();

            // Группировка по типам действий
            if (mergeResult.Actions.Count > 0)
            {
                var actionsByType = mergeResult
                    .Actions.GroupBy(a => a.Type)
                    .OrderByDescending(g => g.Count());

                Console.WriteLine("=== ACTIONS BY TYPE ===");
                foreach (var group in actionsByType)
                {
                    var pct = 100.0 * group.Count() / mergeResult.Actions.Count;
                    Console.WriteLine($"{group.Key, -20}: {group.Count(), 6} ({pct:F1}%)");
                }
                Console.WriteLine();
            }

            // Статистика матчинга
            Console.WriteLine("=== MATCHING STATISTICS ===");
            var totalMatches = stats.FastPathMatches + stats.SlowPathMatches;
            if (totalMatches > 0)
            {
                Console.WriteLine(
                    $"Fast Path (hash):     {stats.FastPathMatches, 6} ({100.0 * stats.FastPathMatches / totalMatches:F1}%)"
                );
                Console.WriteLine(
                    $"Slow Path (semantic): {stats.SlowPathMatches, 6} ({100.0 * stats.SlowPathMatches / totalMatches:F1}%)"
                );
            }
            else
            {
                Console.WriteLine("No matching performed (no changes detected)");
            }
            Console.WriteLine();

            // Анализ конфликтов
            if (mergeResult.Conflicts.Count > 0)
            {
                Console.WriteLine("=== CONFLICTS ===");
                Console.WriteLine();

                var conflictsByType = mergeResult
                    .Conflicts.GroupBy(c => c.ConflictType)
                    .OrderByDescending(g => g.Count());

                foreach (var group in conflictsByType)
                {
                    Console.WriteLine($"{group.Key}: {group.Count()} conflicts");
                }
                Console.WriteLine();

                var conflictsBySeverity = mergeResult
                    .Conflicts.GroupBy(c => c.Severity)
                    .OrderByDescending(g => g.Count());

                Console.WriteLine("Severity Distribution:");
                foreach (var group in conflictsBySeverity)
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()}");
                }
                Console.WriteLine();

                // Показать первые 10 конфликтов
                Console.WriteLine("First 10 conflicts:");
                foreach (var conflict in mergeResult.Conflicts.Take(10))
                {
                    var filePath = conflict.BaseUnit.FilePath;
                    Console.WriteLine(
                        $"  [{conflict.Severity}] {conflict.ConflictType}: {Path.GetFileName(filePath)}"
                    );
                    Console.WriteLine($"    Base:     {conflict.BaseUnit.Id}");
                    Console.WriteLine($"    VersionA: {conflict.VersionA.Id}");
                    Console.WriteLine($"    VersionB: {conflict.VersionB.Id}");
                    Console.WriteLine(
                        $"    Suggested: {conflict.SuggestedResolutions.Count} resolution(s)"
                    );
                    Console.WriteLine();
                }
            }

            // Статистика по файлам
            if (mergeResult.Actions.Count > 0)
            {
                var filesChanged = mergeResult.Actions.Select(a => a.TargetPath).Distinct().Count();

                Console.WriteLine("=== FILE STATISTICS ===");
                Console.WriteLine($"Files affected by merge: {filesChanged}");
                Console.WriteLine();

                // Топ файлов по количеству действий
                var topFiles = mergeResult
                    .Actions.GroupBy(a => a.TargetPath)
                    .OrderByDescending(g => g.Count())
                    .Take(10);

                Console.WriteLine("=== TOP 10 FILES BY MERGE ACTIONS ===");
                foreach (var group in topFiles)
                {
                    var fileName = Path.GetFileName(group.Key);
                    Console.WriteLine($"{fileName, -50}: {group.Count(), 4} actions");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Test completed successfully!");
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
    }
}

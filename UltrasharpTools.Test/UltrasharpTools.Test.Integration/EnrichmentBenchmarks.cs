using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using Moq;
using UltrasharpTools.Droid.Models.Hybrid;
using UltrasharpTools.Droid.Services.Hybrid;

namespace UltrasharpTools.Test.Integration;

/// <summary>
/// Performance benchmarks для всех 15 enrichment стратегий
/// Измеряет overhead обогащения семантическими данными
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EnrichmentBenchmarks
{
    private ISemanticModeProvider _provider = null!;
    private IToolEnricher _enricher = null!;
    private Mock<IEmbeddingService> _embeddingMock = null!;

    [GlobalSetup]
    public void Setup()
    {
        var providerLogger = new Mock<ILogger<SemanticModeProvider>>();
        var enricherLogger = new Mock<ILogger<ToolEnricher>>();
        _embeddingMock = new Mock<IEmbeddingService>();

        // Setup быстрого mock embedding (минимальный overhead)
        _embeddingMock
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken ct) =>
            {
                // Минимальный вектор для benchmark
                return new float[768];
            });

        var config = SemanticModeConfig.CreateDefault();
        _provider = new SemanticModeProvider(
            providerLogger.Object,
            _embeddingMock.Object,
            null,
            null,
            config);

        _enricher = new ToolEnricher(
            enricherLogger.Object,
            _provider,
            config);
    }

    #region Phase 12.1 - Core Strategies Benchmarks

    [Benchmark]
    public async Task Benchmark_ViewDefinition()
    {
        var result = new { code = "public class Test { }" };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("view_definition", result, args);
    }

    [Benchmark]
    public async Task Benchmark_FindReferences()
    {
        var result = new { references = new[] { "File1.cs:10" }, count = 1 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test.Method" };
        await _enricher.EnrichAsync("find_references", result, args);
    }

    [Benchmark]
    public async Task Benchmark_OverwriteMember()
    {
        var result = new { success = true };
        var args = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Test.Method",
            ["newCode"] = "public void Method() { }"
        };
        await _enricher.EnrichAsync("overwrite_member", result, args);
    }

    [Benchmark]
    public async Task Benchmark_AddMember()
    {
        var result = new { success = true };
        var args = new Dictionary<string, object>
        {
            ["typeFqn"] = "MyApp.Test",
            ["newCode"] = "public void NewMethod() { }"
        };
        await _enricher.EnrichAsync("add_member", result, args);
    }

    [Benchmark]
    public async Task Benchmark_RenameSymbol()
    {
        var result = new { filesModified = 1 };
        var args = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Test.Method",
            ["newName"] = "RenamedMethod"
        };
        await _enricher.EnrichAsync("rename_symbol", result, args);
    }

    [Benchmark]
    public async Task Benchmark_GetMembers()
    {
        var result = new { members = new[] { "Method1", "Method2" }, count = 2 };
        var args = new Dictionary<string, object> { ["typeFqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("get_members", result, args);
    }

    [Benchmark]
    public async Task Benchmark_AnalyzeComplexity()
    {
        var result = new { cyclomaticComplexity = 10, cognitiveComplexity = 15 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test.Method" };
        await _enricher.EnrichAsync("analyze_complexity", result, args);
    }

    #endregion

    #region Phase 12.2 - Extended Strategies Benchmarks

    [Benchmark]
    public async Task Benchmark_FindAllReferences()
    {
        var result = new { references = new[] { "Project1:File1.cs:10" }, totalCount = 1 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("find_all_references", result, args);
    }

    [Benchmark]
    public async Task Benchmark_ListTypes()
    {
        var result = new { types = new[] { "Type1", "Type2" }, count = 2 };
        var args = new Dictionary<string, object> { ["namespace"] = "MyApp" };
        await _enricher.EnrichAsync("list_types", result, args);
    }

    [Benchmark]
    public async Task Benchmark_SearchSymbols()
    {
        var result = new { symbols = new[] { "Symbol1", "Symbol2" }, count = 2 };
        var args = new Dictionary<string, object> { ["query"] = "test" };
        await _enricher.EnrichAsync("search_symbols", result, args);
    }

    [Benchmark]
    public async Task Benchmark_TraceExecution()
    {
        var result = new { callChain = new[] { "Main", "Method" }, depth = 2 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test.Method" };
        await _enricher.EnrichAsync("trace_execution", result, args);
    }

    [Benchmark]
    public async Task Benchmark_AnalyzeCodeStyle()
    {
        var result = new { issues = new[] { "Issue1" }, score = 80 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("analyze_code_style", result, args);
    }

    [Benchmark]
    public async Task Benchmark_GetTypeHierarchy()
    {
        var result = new { baseTypes = new[] { "Object" }, interfaces = Array.Empty<string>() };
        var args = new Dictionary<string, object> { ["typeFqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("get_type_hierarchy", result, args);
    }

    [Benchmark]
    public async Task Benchmark_GetProjectStructure()
    {
        var result = new { namespaces = new[] { "MyApp.Services" }, count = 1 };
        var args = new Dictionary<string, object> { ["projectName"] = "MyApp" };
        await _enricher.EnrichAsync("get_project_structure", result, args);
    }

    [Benchmark]
    public async Task Benchmark_FindUsages()
    {
        var result = new { usages = new[] { "File1.cs:10" }, count = 1 };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };
        await _enricher.EnrichAsync("find_usages", result, args);
    }

    [Benchmark]
    public async Task Benchmark_GetDiagnostics()
    {
        var result = new { errors = 0, warnings = 1, info = 5 };
        var args = new Dictionary<string, object> { ["filePath"] = "Test.cs" };
        await _enricher.EnrichAsync("get_diagnostics", result, args);
    }

    [Benchmark]
    public async Task Benchmark_ApplyCodeFixes()
    {
        var result = new { fixesApplied = 1, filesModified = 1 };
        var args = new Dictionary<string, object>
        {
            ["filePath"] = "Test.cs",
            ["diagnosticIds"] = new[] { "CS0168" }
        };
        await _enricher.EnrichAsync("apply_code_fixes", result, args);
    }

    #endregion

    #region Comparative Benchmarks

    [Benchmark(Baseline = true)]
    public async Task Baseline_NoEnrichment()
    {
        // Baseline: просто await Task.CompletedTask для сравнения
        await Task.CompletedTask;
    }

    [Benchmark]
    public async Task Benchmark_CheckAvailability()
    {
        // Измеряем overhead проверки доступности
        await _provider.CheckAvailabilityAsync();
    }

    [Benchmark]
    public async Task Benchmark_GetEmbedding()
    {
        // Измеряем overhead получения вектора
        await _provider.GetEmbeddingAsync("test query");
    }

    [Benchmark]
    public async Task Benchmark_AvailabilityWithCache()
    {
        // Измеряем overhead с кешированием (второй вызов)
        await _provider.CheckAvailabilityAsync();
        await _provider.CheckAvailabilityAsync(); // Должен взяться из кеша
    }

    #endregion

    #region Stress Tests

    [Benchmark]
    public async Task Stress_Sequential_10_ViewDefinitions()
    {
        var result = new { code = "public class Test { }" };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };

        for (int i = 0; i < 10; i++)
        {
            await _enricher.EnrichAsync("view_definition", result, args);
        }
    }

    [Benchmark]
    public async Task Stress_Parallel_10_ViewDefinitions()
    {
        var result = new { code = "public class Test { }" };
        var args = new Dictionary<string, object> { ["fqn"] = "MyApp.Test" };

        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = _enricher.EnrichAsync("view_definition", result, args);
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Stress_MixedStrategies_Sequential()
    {
        var strategies = new[]
        {
            ("view_definition", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("find_references", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("get_members", new Dictionary<string, object> { ["typeFqn"] = "Test.Class" }),
            ("analyze_complexity", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("list_types", new Dictionary<string, object> { ["namespace"] = "Test" })
        };

        var result = new { test = "data" };
        foreach (var (tool, args) in strategies)
        {
            await _enricher.EnrichAsync(tool, result, args);
        }
    }

    [Benchmark]
    public async Task Stress_MixedStrategies_Parallel()
    {
        var strategies = new[]
        {
            ("view_definition", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("find_references", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("get_members", new Dictionary<string, object> { ["typeFqn"] = "Test.Class" }),
            ("analyze_complexity", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("list_types", new Dictionary<string, object> { ["namespace"] = "Test" })
        };

        var result = new { test = "data" };
        var tasks = strategies.Select(s => _enricher.EnrichAsync(s.Item1, result, s.Item2)).ToArray();

        await Task.WhenAll(tasks);
    }

    #endregion
}


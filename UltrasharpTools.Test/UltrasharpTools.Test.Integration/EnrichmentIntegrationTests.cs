using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UltrasharpTools.Droid.Models.Hybrid;
using UltrasharpTools.Droid.Services.Hybrid;
using Xunit;

namespace UltrasharpTools.Test.Integration;

/// <summary>
/// Интеграционные тесты для всех 15 enrichment стратегий
/// Проверяют реальную работу с mock embedding service (без Overlord)
/// </summary>
public class EnrichmentIntegrationTests
{
    private readonly Mock<ILogger<SemanticModeProvider>> _providerLoggerMock;
    private readonly Mock<ILogger<ToolEnricher>> _enricherLoggerMock;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly SemanticModeConfig _config;

    public EnrichmentIntegrationTests()
    {
        _providerLoggerMock = new Mock<ILogger<SemanticModeProvider>>();
        _enricherLoggerMock = new Mock<ILogger<ToolEnricher>>();
        _embeddingMock = new Mock<IEmbeddingService>();
        _config = SemanticModeConfig.CreateDefault();

        // Setup mock embedding service для всех тестов
        SetupMockEmbedding();
    }

    private void SetupMockEmbedding()
    {
        // Возвращаем mock векторы для любого текста
        _embeddingMock
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken ct) =>
            {
                // Генерируем mock вектор размерности 768 (как у nomic-embed-text)
                var random = new Random(text.GetHashCode());
                var vector = new float[768];
                for (int i = 0; i < 768; i++)
                {
                    vector[i] = (float)(random.NextDouble() * 2 - 1); // [-1, 1]
                }
                return vector;
            });
    }

    private (ISemanticModeProvider provider, IToolEnricher enricher) CreateServices()
    {
        var provider = new SemanticModeProvider(
            _providerLoggerMock.Object,
            _embeddingMock.Object,
            null, // No Overlord
            null,
            _config);

        var enricher = new ToolEnricher(
            _enricherLoggerMock.Object,
            provider,
            _config);

        return (provider, enricher);
    }

    #region Phase 12.1 - Core Strategies

    [Fact]
    public async Task ViewDefinition_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            code = "public class CustomerService { public void ProcessOrder() { } }",
            filePath = "Services/CustomerService.cs"
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService"
        };

        // Act
        var result = await enricher.EnrichAsync("view_definition", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.OriginalResult.Should().Be(originalResult);
        result.Metadata.StrategyName.Should().Be("ViewDefinition");
        result.Metadata.Source.Should().Be(SemanticModeSource.Local);

        // Semantic enrichment может быть null если нет vector store, но не должно быть ошибок
        result.Metadata.ErrorMessage.Should().BeNullOrEmpty();
        result.Metadata.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task FindReferences_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            references = new[] { "File1.cs:10", "File2.cs:25" },
            count = 2
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService.ProcessOrder"
        };

        // Act
        var result = await enricher.EnrichAsync("find_references", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("FindReferences");
        result.Metadata.Source.Should().Be(SemanticModeSource.Local);
        result.Metadata.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ModifyCode_OverwriteMember_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            success = true,
            modifiedFile = "Services/CustomerService.cs"
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService.ProcessOrder",
            ["newCode"] = "public void ProcessOrder() { /* new implementation */ }"
        };

        // Act
        var result = await enricher.EnrichAsync("overwrite_member", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("ModifyCode");
        result.Metadata.Source.Should().Be(SemanticModeSource.Local);
    }

    [Fact]
    public async Task ModifyCode_AddMember_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new { success = true };
        var arguments = new Dictionary<string, object>
        {
            ["typeFqn"] = "MyApp.Services.CustomerService",
            ["newCode"] = "public void NewMethod() { }"
        };

        // Act
        var result = await enricher.EnrichAsync("add_member", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("ModifyCode");
    }

    [Fact]
    public async Task ModifyCode_RenameSymbol_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new { filesModified = 5 };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService.ProcessOrder",
            ["newName"] = "HandleOrder"
        };

        // Act
        var result = await enricher.EnrichAsync("rename_symbol", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("ModifyCode");
    }

    [Fact]
    public async Task GetMembers_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            members = new[] { "ProcessOrder", "CancelOrder", "UpdateOrder" },
            count = 3
        };
        var arguments = new Dictionary<string, object>
        {
            ["typeFqn"] = "MyApp.Services.CustomerService"
        };

        // Act
        var result = await enricher.EnrichAsync("get_members", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("GetMembers");
    }

    [Fact]
    public async Task AnalyzeComplexity_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            cyclomaticComplexity = 15,
            cognitiveComplexity = 22,
            maintainabilityIndex = 65
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService.ProcessOrder"
        };

        // Act
        var result = await enricher.EnrichAsync("analyze_complexity", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("AnalyzeComplexity");
    }

    #endregion

    #region Phase 12.2 - Extended Strategies

    [Fact]
    public async Task FindAllReferences_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            references = new[] { "Project1:File1.cs:10", "Project2:File2.cs:25" },
            totalCount = 15
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService"
        };

        // Act
        var result = await enricher.EnrichAsync("find_all_references", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("FindAllReferences");
    }

    [Fact]
    public async Task ListTypes_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            types = new[] { "CustomerService", "OrderService", "PaymentService" },
            count = 3
        };
        var arguments = new Dictionary<string, object>
        {
            ["namespace"] = "MyApp.Services"
        };

        // Act
        var result = await enricher.EnrichAsync("list_types", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("ListTypes");
    }

    [Fact]
    public async Task SearchSymbols_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            symbols = new[] { "ProcessOrder", "HandleOrder", "ExecuteOrder" },
            count = 3
        };
        var arguments = new Dictionary<string, object>
        {
            ["query"] = "order"
        };

        // Act
        var result = await enricher.EnrichAsync("search_symbols", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("SearchSymbols");
    }

    [Fact]
    public async Task TraceExecution_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            callChain = new[] { "Main", "ProcessOrder", "ValidateOrder", "SaveOrder" },
            depth = 4
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService.ProcessOrder"
        };

        // Act
        var result = await enricher.EnrichAsync("trace_execution", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("TraceExecution");
    }

    [Fact]
    public async Task AnalyzeCodeStyle_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            issues = new[] { "Naming convention violation", "Missing XML documentation" },
            score = 75
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Services.CustomerService"
        };

        // Act
        var result = await enricher.EnrichAsync("analyze_code_style", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("AnalyzeCodeStyle");
    }

    [Fact]
    public async Task GetTypeHierarchy_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            baseTypes = new[] { "ServiceBase", "Object" },
            interfaces = new[] { "ICustomerService", "IService" }
        };
        var arguments = new Dictionary<string, object>
        {
            ["typeFqn"] = "MyApp.Services.CustomerService"
        };

        // Act
        var result = await enricher.EnrichAsync("get_type_hierarchy", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("GetTypeHierarchy");
    }

    [Fact]
    public async Task GetProjectStructure_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            namespaces = new[] { "MyApp.Services", "MyApp.Models", "MyApp.Controllers" },
            count = 3
        };
        var arguments = new Dictionary<string, object>
        {
            ["projectName"] = "MyApp"
        };

        // Act
        var result = await enricher.EnrichAsync("get_project_structure", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("GetProjectStructure");
    }

    [Fact]
    public async Task FindUsages_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            usages = new[] { "Controller.cs:45", "Service.cs:102" },
            count = 2
        };
        var arguments = new Dictionary<string, object>
        {
            ["fqn"] = "MyApp.Models.Customer"
        };

        // Act
        var result = await enricher.EnrichAsync("find_usages", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("FindUsages");
    }

    [Fact]
    public async Task GetDiagnostics_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            errors = 2,
            warnings = 5,
            info = 10
        };
        var arguments = new Dictionary<string, object>
        {
            ["filePath"] = "Services/CustomerService.cs"
        };

        // Act
        var result = await enricher.EnrichAsync("get_diagnostics", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("GetDiagnostics");
    }

    [Fact]
    public async Task ApplyCodeFixes_Integration_EnrichesWithSemanticMatches()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var originalResult = new
        {
            fixesApplied = 3,
            filesModified = 1
        };
        var arguments = new Dictionary<string, object>
        {
            ["filePath"] = "Services/CustomerService.cs",
            ["diagnosticIds"] = new[] { "CS0168", "CS8019" }
        };

        // Act
        var result = await enricher.EnrichAsync("apply_code_fixes", originalResult, arguments);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.StrategyName.Should().Be("ApplyCodeFixes");
    }

    #endregion

    #region Cross-Strategy Tests

    [Fact]
    public async Task AllStrategies_HaveConsistentMetadata()
    {
        // Arrange
        var (provider, enricher) = CreateServices();
        var testCases = new[]
        {
            ("view_definition", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("find_references", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("overwrite_member", new Dictionary<string, object> { ["fqn"] = "Test.Method", ["newCode"] = "code" }),
            ("get_members", new Dictionary<string, object> { ["typeFqn"] = "Test.Class" }),
            ("analyze_complexity", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("find_all_references", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("list_types", new Dictionary<string, object> { ["namespace"] = "Test" }),
            ("search_symbols", new Dictionary<string, object> { ["query"] = "test" }),
            ("trace_execution", new Dictionary<string, object> { ["fqn"] = "Test.Method" }),
            ("analyze_code_style", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("get_type_hierarchy", new Dictionary<string, object> { ["typeFqn"] = "Test.Class" }),
            ("get_project_structure", new Dictionary<string, object> { ["projectName"] = "Test" }),
            ("find_usages", new Dictionary<string, object> { ["fqn"] = "Test.Class" }),
            ("get_diagnostics", new Dictionary<string, object> { ["filePath"] = "Test.cs" }),
            ("apply_code_fixes", new Dictionary<string, object> { ["filePath"] = "Test.cs", ["diagnosticIds"] = Array.Empty<string>() })
        };

        // Act & Assert
        foreach (var (toolName, args) in testCases)
        {
            var result = await enricher.EnrichAsync(toolName, new { test = "data" }, args);

            result.Should().NotBeNull($"because {toolName} should return a result");
            result.Metadata.Should().NotBeNull($"because {toolName} should have metadata");
            result.Metadata.Source.Should().Be(SemanticModeSource.Local, $"because {toolName} uses local embedding");
            result.Metadata.EnrichmentTimeMs.Should().BeGreaterThanOrEqualTo(0, $"because {toolName} should track time");
            result.Metadata.TimedOut.Should().BeFalse($"because {toolName} should not timeout with mock service");
        }
    }

    [Fact]
    public async Task AllStrategies_RespectConfiguration_WhenDisabled()
    {
        // Arrange
        var disabledConfig = SemanticModeConfig.CreateDefault();
        disabledConfig.Enabled = false;

        var provider = new SemanticModeProvider(
            _providerLoggerMock.Object,
            _embeddingMock.Object,
            null,
            null,
            disabledConfig);

        var enricher = new ToolEnricher(
            _enricherLoggerMock.Object,
            provider,
            disabledConfig);

        // Act
        var result = await enricher.EnrichAsync("view_definition", new { test = "data" },
            new Dictionary<string, object> { ["fqn"] = "Test.Class" });

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Source.Should().Be(SemanticModeSource.None);
        result.Metadata.ErrorMessage.Should().Be("Semantic mode not available");
    }

    [Fact]
    public async Task AllStrategies_HandleMissingArguments_Gracefully()
    {
        // Arrange
        var (provider, enricher) = CreateServices();

        // Act - вызываем без обязательных аргументов
        var result = await enricher.EnrichAsync("view_definition", new { test = "data" }, null);

        // Assert
        result.Should().NotBeNull();
        result.Semantic.Should().BeNull("because strategy should return null for missing args");
        result.Metadata.ErrorMessage.Should().BeNullOrEmpty("because missing args is not an error");
    }

    #endregion
}

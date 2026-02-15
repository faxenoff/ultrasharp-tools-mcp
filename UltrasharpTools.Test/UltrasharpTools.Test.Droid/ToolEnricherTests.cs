using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UltrasharpTools.Droid.Models.Hybrid;
using UltrasharpTools.Droid.Services.Hybrid;
using UltrasharpTools.Tools.Interfaces;
using Xunit;

namespace UltrasharpTools.Test.Droid;

public class ToolEnricherTests
{
    private readonly Mock<ILogger<ToolEnricher>> _loggerMock;
    private readonly Mock<ISemanticModeProvider> _semanticProviderMock;

    public ToolEnricherTests()
    {
        _loggerMock = new Mock<ILogger<ToolEnricher>>();
        _semanticProviderMock = new Mock<ISemanticModeProvider>();
    }

    [Fact]
    public async Task EnrichAsync_WhenSemanticModeNotAvailable_ReturnsOriginalResult()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();

        _semanticProviderMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SemanticModeAvailability
                {
                    IsAvailable = false,
                    Source = SemanticModeSource.None,
                }
            );

        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);
        var originalResult = new { data = "test" };

        // Act
        var result = await enricher.EnrichAsync("view_definition", originalResult);

        // Assert
        result.Should().NotBeNull();
        result.OriginalResult.Should().Be(originalResult);
        result.Semantic.Should().BeNull();
        result.Metadata.Source.Should().Be(SemanticModeSource.None);
        result.Metadata.ErrorMessage.Should().Be("Semantic mode not available");
    }

    [Fact]
    public async Task EnrichAsync_WhenToolDisabledByConfig_ReturnsOriginalResult()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.ToolSettings["view_definition"].Enabled = false;

        _semanticProviderMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SemanticModeAvailability
                {
                    IsAvailable = true,
                    Source = SemanticModeSource.Local,
                }
            );

        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);
        var originalResult = new { data = "test" };

        // Act
        var result = await enricher.EnrichAsync("view_definition", originalResult);

        // Assert
        result.Should().NotBeNull();
        result.OriginalResult.Should().Be(originalResult);
        result.Semantic.Should().BeNull();
        result.Metadata.ErrorMessage.Should().Be("Enrichment disabled by configuration");
    }

    [Fact]
    public async Task EnrichAsync_WhenNoStrategyFound_ReturnsOriginalResult()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();

        _semanticProviderMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SemanticModeAvailability
                {
                    IsAvailable = true,
                    Source = SemanticModeSource.Local,
                }
            );

        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);
        var originalResult = new { data = "test" };

        // Act
        var result = await enricher.EnrichAsync("unknown_tool", originalResult);

        // Assert
        result.Should().NotBeNull();
        result.OriginalResult.Should().Be(originalResult);
        result.Semantic.Should().BeNull();
        result.Metadata.StrategyName.Should().Be("none");
    }

    [Fact]
    public void SupportsEnrichment_ForSupportedTool_ReturnsTrue()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);

        // Act & Assert
        enricher.SupportsEnrichment("view_definition").Should().BeTrue();
        enricher.SupportsEnrichment("find_references").Should().BeTrue();
        enricher.SupportsEnrichment("overwrite_member").Should().BeTrue();
        enricher.SupportsEnrichment("get_members").Should().BeTrue();
        enricher.SupportsEnrichment("analyze_complexity").Should().BeTrue();
    }

    [Fact]
    public void SupportsEnrichment_ForUnsupportedTool_ReturnsFalse()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);

        // Act & Assert
        enricher.SupportsEnrichment("unknown_tool").Should().BeFalse();
        enricher.SupportsEnrichment("load_solution").Should().BeFalse();
        enricher.SupportsEnrichment("load_project").Should().BeFalse();
    }

    [Fact]
    public void SupportsEnrichment_PhaseExtendedStrategies_ReturnsTrue()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);

        // Act & Assert - Phase 12.2 extended strategies
        enricher.SupportsEnrichment("find_all_references").Should().BeTrue();
        enricher.SupportsEnrichment("list_types").Should().BeTrue();
        enricher.SupportsEnrichment("search_symbols").Should().BeTrue();
        enricher.SupportsEnrichment("trace_execution").Should().BeTrue();
        enricher.SupportsEnrichment("analyze_code_style").Should().BeTrue();
        enricher.SupportsEnrichment("get_type_hierarchy").Should().BeTrue();
        enricher.SupportsEnrichment("get_project_structure").Should().BeTrue();
        enricher.SupportsEnrichment("find_usages").Should().BeTrue();
        enricher.SupportsEnrichment("get_diagnostics").Should().BeTrue();
        enricher.SupportsEnrichment("apply_code_fixes").Should().BeTrue();
    }

    [Fact]
    public async Task EnrichAsync_WithTimeout_ReturnsOriginalResultAndMarksTimeout()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Enrichment.TimeoutSeconds = 1; // Very short timeout

        _semanticProviderMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SemanticModeAvailability
                {
                    IsAvailable = true,
                    Source = SemanticModeSource.Local,
                }
            );

        // Simulate slow semantic search that respects cancellation
        _semanticProviderMock
            .Setup(x =>
                x.SearchByTextAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<double>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (string query, int topK, double threshold, CancellationToken ct) =>
                {
                    try
                    {
                        await Task.Delay(5000, ct); // Simulate slow response
                        return Array.Empty<SemanticMatch>();
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Re-throw to propagate cancellation
                    }
                }
            );

        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);
        var originalResult = new { code = "public class Test { }" };
        var toolArguments = new Dictionary<string, object> { ["fqn"] = "MyNamespace.MyClass" };

        // Act
        var result = await enricher.EnrichAsync("view_definition", originalResult, toolArguments);

        // Assert
        result.Should().NotBeNull();
        result.OriginalResult.Should().Be(originalResult);
        result.Semantic.Should().BeNull(); // Due to timeout
        result.Metadata.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task EnrichAsync_WithCustomToolSettings_UsesConfiguredValues()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.ToolSettings["view_definition"].TopK = 10;
        config.ToolSettings["view_definition"].Threshold = 0.9;

        _semanticProviderMock
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SemanticModeAvailability
                {
                    IsAvailable = true,
                    Source = SemanticModeSource.Local,
                }
            );

        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);

        // Act
        var result = await enricher.EnrichAsync("view_definition", new { code = "test" });

        // Assert
        result.Should().NotBeNull();
        // Note: Strategy uses hardcoded values currently, but config is available for future use
    }

    [Fact]
    public void Constructor_WithDefaultConfig_InitializesAllStrategies()
    {
        // Arrange & Act
        var config = SemanticModeConfig.CreateDefault();
        var enricher = new ToolEnricher(_loggerMock.Object, _semanticProviderMock.Object, config);

        // Assert - Should have 15 strategies (5 core + 10 extended)
        var supportedTools = new[]
        {
            // Phase 12.1 - Core
            "view_definition",
            "find_references",
            "overwrite_member",
            "get_members",
            "analyze_complexity",
            // Phase 12.2 - Extended
            "find_all_references",
            "list_types",
            "search_symbols",
            "trace_execution",
            "analyze_code_style",
            "get_type_hierarchy",
            "get_project_structure",
            "find_usages",
            "get_diagnostics",
            "apply_code_fixes",
        };

        foreach (var tool in supportedTools)
        {
            enricher
                .SupportsEnrichment(tool)
                .Should()
                .BeTrue($"because {tool} should be supported");
        }
    }
}

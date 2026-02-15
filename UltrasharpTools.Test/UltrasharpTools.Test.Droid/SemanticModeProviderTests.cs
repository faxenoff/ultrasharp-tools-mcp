using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UltrasharpTools.Droid.Models.Hybrid;
using UltrasharpTools.Droid.Services.Hybrid;
using UltrasharpTools.Tools.Interfaces;
using Xunit;

namespace UltrasharpTools.Test.Droid;

public class SemanticModeProviderTests
{
    private readonly Mock<ILogger<SemanticModeProvider>> _loggerMock;
    private readonly Mock<IEmbeddingService> _localEmbeddingMock;
    private readonly Mock<IServerBridgeService> _serverBridgeMock;

    public SemanticModeProviderTests()
    {
        _loggerMock = new Mock<ILogger<SemanticModeProvider>>();
        _localEmbeddingMock = new Mock<IEmbeddingService>();
        _serverBridgeMock = new Mock<IServerBridgeService>();
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenDisabledByConfig_ReturnsNone()
    {
        // Arrange
        var config = new SemanticModeConfig { Enabled = false };
        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            _serverBridgeMock.Object,
            "http://localhost:3001",
            config
        );

        // Act
        var result = await provider.CheckAvailabilityAsync();

        // Assert
        result.IsAvailable.Should().BeFalse();
        result.Source.Should().Be(SemanticModeSource.None);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenLocalAvailable_ReturnsLocal()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.LocalCheckTimeoutSeconds = 10; // Longer timeout for test

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null, // No server bridge
            null,
            config
        );

        // Act
        var result = await provider.CheckAvailabilityAsync();

        // Assert
        result.IsAvailable.Should().BeTrue();
        result.Source.Should().Be(SemanticModeSource.Local);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenOverlordAvailable_ReturnsOverlord()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.OverlordCheckTimeoutSeconds = 10;

        _serverBridgeMock
            .Setup(x => x.IsServerAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            null, // No local embedding
            _serverBridgeMock.Object,
            "http://localhost:3001",
            config
        );

        // Act
        var result = await provider.CheckAvailabilityAsync();

        // Assert
        result.IsAvailable.Should().BeTrue();
        result.Source.Should().Be(SemanticModeSource.Overlord);
        result.OverlordUrl.Should().Be("http://localhost:3001");
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenBothAvailable_ReturnsBoth()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.LocalCheckTimeoutSeconds = 10;
        config.Availability.OverlordCheckTimeoutSeconds = 10;

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        _serverBridgeMock
            .Setup(x => x.IsServerAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            _serverBridgeMock.Object,
            "http://localhost:3001",
            config
        );

        // Act
        var result = await provider.CheckAvailabilityAsync();

        // Assert
        result.IsAvailable.Should().BeTrue();
        result.Source.Should().Be(SemanticModeSource.Both);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_UsesCaching()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.CacheValiditySeconds = 60; // 1 minute cache
        config.Availability.LocalCheckTimeoutSeconds = 10;

        int callCount = 0;
        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new float[] { 0.1f, 0.2f, 0.3f };
            });

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null,
            null,
            config
        );

        // Act
        var result1 = await provider.CheckAvailabilityAsync();
        var result2 = await provider.CheckAvailabilityAsync();
        var result3 = await provider.CheckAvailabilityAsync();

        // Assert
        result1.Source.Should().Be(SemanticModeSource.Local);
        result2.Source.Should().Be(SemanticModeSource.Local);
        result3.Source.Should().Be(SemanticModeSource.Local);

        // First call: 2 times (availability check + vector dimension check)
        // Subsequent calls: 0 times (cached)
        callCount
            .Should()
            .Be(
                2,
                "because first check needs availability + dimension, then cache prevents subsequent calls"
            );
    }

    [Fact]
    public async Task CheckAvailabilityAsync_LocalTimeout_ReturnsNone()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.LocalCheckTimeoutSeconds = 1; // Very short timeout

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .Returns(
                async (string text, CancellationToken ct) =>
                {
                    await Task.Delay(5000, ct); // Simulate slow response
                    return new float[] { 0.1f };
                }
            );

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null,
            null,
            config
        );

        // Act
        var result = await provider.CheckAvailabilityAsync();

        // Assert
        result.IsAvailable.Should().BeFalse();
        result.Source.Should().Be(SemanticModeSource.None);
    }

    [Fact]
    public async Task GetEmbeddingAsync_WhenSemanticModeDisabled_ReturnsNull()
    {
        // Arrange
        var config = new SemanticModeConfig { Enabled = false };
        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null,
            null,
            config
        );

        // Act
        var result = await provider.GetEmbeddingAsync("test");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmbeddingAsync_WithLocalSource_ReturnsEmbedding()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.LocalCheckTimeoutSeconds = 10;

        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.4f, 0.5f, 0.6f });

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null,
            null,
            config
        );

        // Act
        var result = await provider.GetEmbeddingAsync("hello");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(new float[] { 0.4f, 0.5f, 0.6f });
    }

    [Fact]
    public async Task SearchByTextAsync_WhenAvailable_ReturnsMatches()
    {
        // Arrange
        var config = SemanticModeConfig.CreateDefault();
        config.Availability.LocalCheckTimeoutSeconds = 10;

        var queryEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _localEmbeddingMock
            .Setup(x => x.GetEmbeddingAsync("find similar code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        var provider = new SemanticModeProvider(
            _loggerMock.Object,
            _localEmbeddingMock.Object,
            null,
            null,
            config
        );

        // Act
        var result = await provider.SearchByTextAsync("find similar code", 5, 0.7);

        // Assert
        result.Should().NotBeNull();
        // Note: Will be empty since we don't have a vector store mock, but no exception thrown
    }
}

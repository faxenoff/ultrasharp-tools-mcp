using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Droid.Services.Hybrid;
using Xunit;

namespace UltrasharpTools.Test.Droid;

public class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var policy = new RetryPolicy(NullLogger.Instance, maxRetries: 3);
        var expectedResult = "success";

        // Act
        var result = await policy.ExecuteAsync(
            _ => Task.FromResult(expectedResult),
            "test-operation");

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ExecuteAsync_FailsOnce_RetriesAndSucceeds()
    {
        // Arrange
        var policy = new RetryPolicy(
            NullLogger.Instance,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10));

        var attemptCount = 0;
        var expectedResult = "success";

        // Act
        var result = await policy.ExecuteAsync(
            _ =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new HttpRequestException("Temporary failure");
                }
                return Task.FromResult(expectedResult);
            },
            "test-operation");

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Equal(2, attemptCount); // Failed once, succeeded on second attempt
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetries_ThrowsException()
    {
        // Arrange
        var policy = new RetryPolicy(
            NullLogger.Instance,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10));

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(
                _ =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Persistent failure");
                },
                "test-operation");
        });

        Assert.Equal(3, attemptCount); // All 3 attempts should have been made
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_IncreasesDelay()
    {
        // Arrange
        var policy = new RetryPolicy(
            NullLogger.Instance,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(50),
            backoffMultiplier: 2.0);

        var attemptTimes = new List<DateTime>();

        // Act
        try
        {
            await policy.ExecuteAsync(
                _ =>
                {
                    attemptTimes.Add(DateTime.UtcNow);
                    throw new HttpRequestException("Test failure");
                },
                "test-operation");
        }
        catch
        {
            // Expected to fail
        }

        // Assert
        Assert.Equal(3, attemptTimes.Count);

        // Check delays are increasing (approximately)
        var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
        var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;

        Assert.InRange(delay1, 40, 70); // ~50ms ± tolerance
        Assert.InRange(delay2, 90, 120); // ~100ms ± tolerance (2x backoff)
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ThrowsImmediately()
    {
        // Arrange
        var policy = new RetryPolicy(NullLogger.Instance, maxRetries: 3);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(
                _ =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Non-retryable");
                },
                "test-operation");
        });

        Assert.Equal(1, attemptCount); // Should not retry
    }

    [Fact]
    public async Task ExecuteAsync_CustomRetryCheck_UsesCustomLogic()
    {
        // Arrange
        var policy = new RetryPolicy(
            NullLogger.Instance,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(10));

        var attemptCount = 0;

        // Custom check: retry only for InvalidOperationException
        bool CustomRetryCheck(Exception ex) => ex is InvalidOperationException;

        // Act
        var result = await policy.ExecuteAsync(
            _ =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new InvalidOperationException("Retryable with custom check");
                }
                return Task.FromResult("success");
            },
            "test-operation",
            CancellationToken.None,
            CustomRetryCheck);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, attemptCount); // Retried once
    }
}

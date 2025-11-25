using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Retry policy с exponential backoff для Overlord calls
/// </summary>
public sealed partial class RetryPolicy
{
    private readonly ILogger _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _backoffMultiplier;

    public RetryPolicy(
        ILogger logger,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        double backoffMultiplier = 2.0
    )
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(10);
        _backoffMultiplier = backoffMultiplier;
    }

    /// <summary>
    /// Выполнить операцию с retry logic и exponential backoff
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default,
        Func<Exception, bool>? shouldRetry = null
    )
    {
        var attempt = 0;
        var delay = _initialDelay;

        while (true)
        {
            attempt++;

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt <= _maxRetries && IsRetryable(ex, shouldRetry))
            {
                if (attempt == _maxRetries)
                {
                    LogOperationFailed(ex, operationName, attempt);
                    throw;
                }

                LogRetrying(ex, operationName, attempt, _maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);

                // Exponential backoff
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(
                        delay.TotalMilliseconds * _backoffMultiplier,
                        _maxDelay.TotalMilliseconds
                    )
                );
            }
        }
    }

    /// <summary>
    /// Выполнить операцию без возвращаемого значения
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default,
        Func<Exception, bool>? shouldRetry = null
    )
    {
        await ExecuteAsync<object?>(
            async ct =>
            {
                await operation(ct);
                return null;
            },
            operationName,
            cancellationToken,
            shouldRetry
        );
    }

    /// <summary>
    /// Проверяет, стоит ли повторять операцию после исключения
    /// </summary>
    private static bool IsRetryable(Exception ex, Func<Exception, bool>? customCheck)
    {
        // Если есть custom check, используем его
        if (customCheck != null)
        {
            return customCheck(ex);
        }

        // По умолчанию retry для:
        // - HttpRequestException (network issues)
        // - TaskCanceledException (timeouts, но не если CancellationToken был отменен явно)
        // - OperationCanceledException (timeouts)
        return ex is HttpRequestException
            || (ex is TaskCanceledException tce && !tce.CancellationToken.IsCancellationRequested)
            || (
                ex is OperationCanceledException oce
                && !oce.CancellationToken.IsCancellationRequested
            );
    }
}

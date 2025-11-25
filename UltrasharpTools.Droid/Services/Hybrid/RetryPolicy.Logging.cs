using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class RetryPolicy
{
    // Retry operations (6200-6201)
    [LoggerMessage(EventId = 6200, Level = LogLevel.Error,
        Message = "Operation {OperationName} failed after {Attempts} attempts")]
    private partial void LogOperationFailed(Exception exception, string operationName, int attempts);

    [LoggerMessage(EventId = 6201, Level = LogLevel.Warning,
        Message = "Operation {OperationName} failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...")]
    private partial void LogRetrying(Exception exception, string operationName, int attempt, int maxRetries, double delay);
}

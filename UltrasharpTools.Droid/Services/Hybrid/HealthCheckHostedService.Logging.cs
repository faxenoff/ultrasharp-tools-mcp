using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class HealthCheckHostedService
{
    // Service lifecycle (5700-5702)
    [LoggerMessage(EventId = 5700, Level = LogLevel.Information,
        Message = "Health check service started. Interval: {Interval}s")]
    private partial void LogServiceStarted(int interval);

    [LoggerMessage(EventId = 5701, Level = LogLevel.Error,
        Message = "Error in health check loop")]
    private partial void LogHealthCheckLoopError(Exception exception);

    [LoggerMessage(EventId = 5702, Level = LogLevel.Information,
        Message = "Health check service stopped")]
    private partial void LogServiceStopped();

    // Overlord status (5703-5707)
    [LoggerMessage(EventId = 5703, Level = LogLevel.Information,
        Message = "Overlord is now AVAILABLE")]
    private partial void LogOverlordAvailable();

    [LoggerMessage(EventId = 5704, Level = LogLevel.Warning,
        Message = "Overlord is now UNAVAILABLE - routing will fallback to LOCAL")]
    private partial void LogOverlordUnavailable();

    [LoggerMessage(EventId = 5705, Level = LogLevel.Trace,
        Message = "Overlord status: {Status}")]
    private partial void LogOverlordStatus(string status);

    [LoggerMessage(EventId = 5706, Level = LogLevel.Error,
        Message = "Health check failed")]
    private partial void LogHealthCheckFailed(Exception exception);

    [LoggerMessage(EventId = 5707, Level = LogLevel.Warning,
        Message = "Overlord status changed to UNAVAILABLE due to error")]
    private partial void LogOverlordUnavailableDueToError();
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class NotificationClientService
{
    // Connection (6000-6002)
    [LoggerMessage(EventId = 6000, Level = LogLevel.Information,
        Message = "Connecting to SSE: {Url}")]
    private partial void LogConnecting(string url);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Debug,
        Message = "SSE connection cancelled")]
    private partial void LogConnectionCancelled();

    [LoggerMessage(EventId = 6002, Level = LogLevel.Error,
        Message = "SSE connection error")]
    private partial void LogConnectionError(Exception exception);

    // Events (6003-6004)
    [LoggerMessage(EventId = 6003, Level = LogLevel.Debug,
        Message = "Received SSE event: {Type}")]
    private partial void LogReceivedEvent(string type);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Error,
        Message = "Failed to process SSE event")]
    private partial void LogProcessEventFailed(Exception exception);
}

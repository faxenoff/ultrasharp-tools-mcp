using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class NotificationService
{
    // Client registration (7100-7103)
    [LoggerMessage(EventId = 7100, Level = LogLevel.Information,
        Message = "Client {ClientId} registered for project {Project}")]
    private partial void LogClientRegistered(string clientId, string project);

    [LoggerMessage(EventId = 7101, Level = LogLevel.Debug,
        Message = "Client {ClientId} connection cancelled")]
    private partial void LogClientConnectionCancelled(string clientId);

    [LoggerMessage(EventId = 7102, Level = LogLevel.Warning,
        Message = "Client {ClientId} already registered")]
    private partial void LogClientAlreadyRegistered(string clientId);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Information,
        Message = "Client {ClientId} unregistered")]
    private partial void LogClientUnregistered(string clientId);

    // Broadcasting (7104-7107)
    [LoggerMessage(EventId = 7104, Level = LogLevel.Information,
        Message = "Broadcasting notification type {Type} to {Count} clients")]
    private partial void LogBroadcasting(string type, int count);

    [LoggerMessage(EventId = 7105, Level = LogLevel.Information,
        Message = "Sending notification type {Type} to project {Project}")]
    private partial void LogSendingToProject(string type, string project);

    [LoggerMessage(EventId = 7106, Level = LogLevel.Debug,
        Message = "No clients found for project {Project}")]
    private partial void LogNoClientsForProject(string project);

    [LoggerMessage(EventId = 7107, Level = LogLevel.Error,
        Message = "Failed to send notification to client {ClientId}")]
    private partial void LogSendNotificationFailed(Exception exception, string clientId);
}

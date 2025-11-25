using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class ServerBridgeService
{
    // File events (5600-5601)
    [LoggerMessage(EventId = 5600, Level = LogLevel.Debug,
        Message = "Sent file changed event: {Project}/{File}")]
    private partial void LogFileSent(string project, string file);

    [LoggerMessage(EventId = 5601, Level = LogLevel.Error,
        Message = "Failed to send file changed event: {Project}/{File}")]
    private partial void LogFileEventFailed(Exception exception, string project, string file);

    // Branch switch events (5602-5603)
    [LoggerMessage(EventId = 5602, Level = LogLevel.Information,
        Message = "Sent branch switch event: {Project} {From} -> {To}")]
    private partial void LogBranchSwitchSent(string project, string from, string to);

    [LoggerMessage(EventId = 5603, Level = LogLevel.Error,
        Message = "Failed to send branch switch event: {Project}")]
    private partial void LogBranchEventFailed(Exception exception, string project);

    // Git commit events (5604-5605)
    [LoggerMessage(EventId = 5604, Level = LogLevel.Information,
        Message = "Sent git commit event: {Project}/{Branch} {Sha}")]
    private partial void LogCommitEventSent(string project, string branch, string sha);

    [LoggerMessage(EventId = 5605, Level = LogLevel.Error,
        Message = "Failed to send git commit event: {Project}")]
    private partial void LogCommitEventFailed(Exception exception, string project);

    // MCP Proxy (5606-5607)
    [LoggerMessage(EventId = 5606, Level = LogLevel.Debug,
        Message = "Calling MCP proxy: tool={ToolName}, project={Project}")]
    private partial void LogMcpProxyCall(string toolName, string? project);

    [LoggerMessage(EventId = 5607, Level = LogLevel.Debug,
        Message = "MCP proxy call succeeded: tool={ToolName}")]
    private partial void LogMcpProxySuccess(string toolName);
}

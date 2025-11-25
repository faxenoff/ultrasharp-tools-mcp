using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Controllers;

public partial class AgentController
{
    // File events (7500-7501)
    [LoggerMessage(EventId = 7500, Level = LogLevel.Information,
        Message = "File changed: {Project}/{Branch}/{File} ({Action})")]
    private partial void LogFileChanged(string project, string branch, string file, string action);

    [LoggerMessage(EventId = 7501, Level = LogLevel.Error,
        Message = "Failed to process file changed event")]
    private partial void LogFileChangedFailed(Exception exception);

    // Branch events (7502-7503)
    [LoggerMessage(EventId = 7502, Level = LogLevel.Information,
        Message = "Branch switched: {Project} {From} → {To}")]
    private partial void LogBranchSwitched(string project, string from, string to);

    [LoggerMessage(EventId = 7503, Level = LogLevel.Error,
        Message = "Failed to process branch switch event")]
    private partial void LogBranchSwitchFailed(Exception exception);

    // Git events (7504-7505)
    [LoggerMessage(EventId = 7504, Level = LogLevel.Information,
        Message = "Git commit: {Project}/{Branch} {Sha} ({Files} files)")]
    private partial void LogGitCommit(string project, string branch, string sha, int files);

    [LoggerMessage(EventId = 7505, Level = LogLevel.Error,
        Message = "Failed to process git commit event")]
    private partial void LogGitCommitFailed(Exception exception);

    // MCP proxy (7506-7509)
    [LoggerMessage(EventId = 7506, Level = LogLevel.Information,
        Message = "MCP proxy request: {Tool}")]
    private partial void LogMcpProxyRequest(string tool);

    [LoggerMessage(EventId = 7507, Level = LogLevel.Error,
        Message = "Failed to proxy MCP request")]
    private partial void LogMcpProxyFailed(Exception exception);

    [LoggerMessage(EventId = 7508, Level = LogLevel.Error,
        Message = "Failed to get MCP tools")]
    private partial void LogGetMcpToolsFailed(Exception exception);

    // SSE (7510-7512)
    [LoggerMessage(EventId = 7510, Level = LogLevel.Information,
        Message = "SSE connection established: {ClientId}, project: {Project}")]
    private partial void LogSseConnectionEstablished(string clientId, string project);

    [LoggerMessage(EventId = 7511, Level = LogLevel.Debug,
        Message = "SSE connection closed for client {ClientId}")]
    private partial void LogSseConnectionClosed(string clientId);

    [LoggerMessage(EventId = 7512, Level = LogLevel.Error,
        Message = "SSE connection error for client {ClientId}")]
    private partial void LogSseConnectionError(Exception exception, string clientId);
}

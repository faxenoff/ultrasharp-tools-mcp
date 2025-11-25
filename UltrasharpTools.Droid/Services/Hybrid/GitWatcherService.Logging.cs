using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class GitWatcherService
{
    // Service lifecycle (5300-5303)
    [LoggerMessage(EventId = 5300, Level = LogLevel.Information,
        Message = "GitWatcherService starting for repository: {Path}")]
    private partial void LogStarting(string path);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Information,
        Message = "Initial Git state: branch={Branch}, commit={Commit}")]
    private partial void LogInitialState(string branch, string commit);

    [LoggerMessage(EventId = 5302, Level = LogLevel.Information,
        Message = "GitWatcherService stopping")]
    private partial void LogStopping();

    [LoggerMessage(EventId = 5303, Level = LogLevel.Error,
        Message = "GitWatcherService error")]
    private partial void LogServiceError(Exception exception);

    // Git changes detection (5304-5306)
    [LoggerMessage(EventId = 5304, Level = LogLevel.Information,
        Message = "Branch switched: {OldBranch} -> {NewBranch}")]
    private partial void LogBranchSwitched(string oldBranch, string newBranch);

    [LoggerMessage(EventId = 5305, Level = LogLevel.Information,
        Message = "New commit detected: {OldSha} -> {NewSha}")]
    private partial void LogNewCommit(string oldSha, string newSha);

    [LoggerMessage(EventId = 5306, Level = LogLevel.Error,
        Message = "Failed to check Git changes")]
    private partial void LogCheckFailed(Exception exception);

    // Branch/commit operations (5307-5308)
    [LoggerMessage(EventId = 5307, Level = LogLevel.Warning,
        Message = "Failed to get current branch")]
    private partial void LogGetBranchFailed(Exception exception);

    [LoggerMessage(EventId = 5308, Level = LogLevel.Warning,
        Message = "Failed to get last commit SHA")]
    private partial void LogGetCommitFailed(Exception exception);

    // Git process (5309-5312)
    [LoggerMessage(EventId = 5309, Level = LogLevel.Warning,
        Message = "Failed to start git process")]
    private partial void LogGitProcessFailed();

    [LoggerMessage(EventId = 5310, Level = LogLevel.Warning,
        Message = "git diff-tree failed: {Error}")]
    private partial void LogDiffTreeFailed(string error);

    [LoggerMessage(EventId = 5311, Level = LogLevel.Debug,
        Message = "Found {FileCount} changed files in commit {CommitSha}")]
    private partial void LogFoundChangedFiles(int fileCount, string commitSha);

    [LoggerMessage(EventId = 5312, Level = LogLevel.Warning,
        Message = "Failed to get changed files for commit {CommitSha}")]
    private partial void LogGetChangedFilesFailed(Exception exception, string commitSha);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class GitCliService
{
    [LoggerMessage(EventId = 3980, Level = LogLevel.Debug,
        Message = "Error checking if path is a Git repository: {Error}")]
    private partial void LogRepositoryCheckError(string error);

    [LoggerMessage(EventId = 3981, Level = LogLevel.Debug,
        Message = "Current branch: {BranchName}, IsSharpToolsBranch: {IsSharpToolsBranch}")]
    private partial void LogCurrentBranchCheck(string branchName, bool isSharpToolsBranch);

    [LoggerMessage(EventId = 3982, Level = LogLevel.Warning,
        Message = "Error checking current branch: {Error}")]
    private partial void LogBranchCheckError(string error);

    [LoggerMessage(EventId = 3983, Level = LogLevel.Warning,
        Message = "No Git repository found for solution at {SolutionPath}")]
    private partial void LogNoRepository(string solutionPath);

    [LoggerMessage(EventId = 3984, Level = LogLevel.Debug,
        Message = "Already on SharpTools branch: {BranchName}")]
    private partial void LogAlreadyOnSharpToolsBranch(string branchName);

    [LoggerMessage(EventId = 3985, Level = LogLevel.Error,
        Message = "Failed to create and checkout branch {BranchName}: {Error}")]
    private partial void LogBranchCreateFailed(string branchName, string error);

    [LoggerMessage(EventId = 3986, Level = LogLevel.Information,
        Message = "Created and switched to SharpTools branch: {BranchName}")]
    private partial void LogBranchCreated(string branchName);

    [LoggerMessage(EventId = 3987, Level = LogLevel.Error,
        Message = "Error ensuring SharpTools branch for solution at {SolutionPath}")]
    private partial void LogEnsureBranchError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 3988, Level = LogLevel.Warning,
        Message = "Auto cleanup of old branches failed, but continuing")]
    private partial void LogAutoCleanupFailed(Exception exception);

    [LoggerMessage(EventId = 3989, Level = LogLevel.Debug,
        Message = "Staged file: {FilePath}")]
    private partial void LogStagedFile(string filePath);

    [LoggerMessage(EventId = 3990, Level = LogLevel.Warning,
        Message = "Failed to stage file {FilePath}: {Error}")]
    private partial void LogStageFileFailed(string filePath, string error);

    [LoggerMessage(EventId = 3991, Level = LogLevel.Warning,
        Message = "No files were staged for commit")]
    private partial void LogNoFilesStaged();

    [LoggerMessage(EventId = 3992, Level = LogLevel.Error,
        Message = "Failed to create commit: {Error}")]
    private partial void LogCommitFailed(string error);

    [LoggerMessage(EventId = 3993, Level = LogLevel.Information,
        Message = "Created commit {CommitSha} with {FileCount} files: {CommitMessage}")]
    private partial void LogCommitCreated(string commitSha, int fileCount, string commitMessage);

    [LoggerMessage(EventId = 3994, Level = LogLevel.Error,
        Message = "Error committing changes for solution at {SolutionPath}")]
    private partial void LogCommitError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 3995, Level = LogLevel.Error,
        Message = "Failed to create undo branch {BranchName}: {Error}")]
    private partial void LogUndoBranchFailed(string branchName, string error);

    [LoggerMessage(EventId = 3996, Level = LogLevel.Information,
        Message = "Created undo branch: {BranchName} at commit {CommitSha}")]
    private partial void LogUndoBranchCreated(string branchName, string commitSha);

    [LoggerMessage(EventId = 3997, Level = LogLevel.Error,
        Message = "Error creating undo branch for solution at {SolutionPath}")]
    private partial void LogUndoBranchError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 3998, Level = LogLevel.Warning,
        Message = "Could not get diff between {OldSha} and {NewSha}: {Error}")]
    private partial void LogDiffFailed(string oldSha, string newSha, string error);

    [LoggerMessage(EventId = 3999, Level = LogLevel.Error,
        Message = "Error getting diff for solution at {SolutionPath}")]
    private partial void LogGetDiffError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning,
        Message = "Not on a SharpTools branch, cannot revert. Current branch: {BranchName}")]
    private partial void LogNotOnSharpToolsBranch(string branchName);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning,
        Message = "Could not get commit SHAs for revert")]
    private partial void LogRevertShasFailed();

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information,
        Message = "Reverting from commit {CurrentSha} to parent {ParentSha}")]
    private partial void LogReverting(string currentSha, string parentSha);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning,
        Message = "Failed to create undo branch")]
    private partial void LogUndoBranchCreateFailed();

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error,
        Message = "Failed to reset to parent commit: {Error}")]
    private partial void LogResetFailed(string error);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Information,
        Message = "Successfully reverted to commit {CommitSha}")]
    private partial void LogRevertSuccess(string commitSha);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Error,
        Message = "Error reverting last commit for solution at {SolutionPath}")]
    private partial void LogRevertError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Debug,
        Message = "Not on a SharpTools branch: {BranchName}")]
    private partial void LogNotOnSharpToolsBranchDebug(string branchName);

    [LoggerMessage(EventId = 4008, Level = LogLevel.Debug,
        Message = "Origin commit for branch {BranchName}: {CommitSha}")]
    private partial void LogBranchOriginCommit(string branchName, string commitSha);

    [LoggerMessage(EventId = 4009, Level = LogLevel.Error,
        Message = "Error getting branch origin commit for solution at {SolutionPath}")]
    private partial void LogBranchOriginError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Error,
        Message = "Error getting current branch for solution at {SolutionPath}")]
    private partial void LogGetCurrentBranchError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Error,
        Message = "Error getting current commit SHA for solution at {SolutionPath}")]
    private partial void LogGetCommitShaError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4012, Level = LogLevel.Error,
        Message = "Error getting merge base for branches {Branch1} and {Branch2}")]
    private partial void LogMergeBaseError(Exception exception, string branch1, string branch2);

    [LoggerMessage(EventId = 4013, Level = LogLevel.Error,
        Message = "Error getting changed files between {From} and {To}")]
    private partial void LogGetChangedFilesError(Exception exception, string from, string to);

    [LoggerMessage(EventId = 4014, Level = LogLevel.Error,
        Message = "Error getting all branches for solution at {SolutionPath}")]
    private partial void LogGetBranchesError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4015, Level = LogLevel.Debug,
        Message = "No Git repository found for solution at {SolutionPath}, skipping cleanup")]
    private partial void LogNoRepositoryForCleanup(string solutionPath);

    [LoggerMessage(EventId = 4016, Level = LogLevel.Debug,
        Message = "Found {Count} SharpTools branches")]
    private partial void LogFoundBranches(int count);

    [LoggerMessage(EventId = 4017, Level = LogLevel.Information,
        Message = "Deleted old SharpTools branch: {BranchName} (age: {AgeDays:F1} days)")]
    private partial void LogBranchDeleted(string branchName, double ageDays);

    [LoggerMessage(EventId = 4018, Level = LogLevel.Warning,
        Message = "Failed to delete branch {BranchName}: {Error}")]
    private partial void LogBranchDeleteFailed(string branchName, string error);

    [LoggerMessage(EventId = 4019, Level = LogLevel.Information,
        Message = "Cleanup complete: deleted {Count} old branches")]
    private partial void LogCleanupComplete(int count);

    [LoggerMessage(EventId = 4020, Level = LogLevel.Error,
        Message = "Error cleaning up old branches for solution at {SolutionPath}")]
    private partial void LogCleanupError(Exception exception, string solutionPath);

    [LoggerMessage(EventId = 4021, Level = LogLevel.Warning,
        Message = "Git command timed out after 10 seconds: {Arguments}")]
    private partial void LogGitTimeout(string arguments);

    [LoggerMessage(EventId = 4022, Level = LogLevel.Error,
        Message = "Error running git command: {Arguments}")]
    private partial void LogGitCommandError(Exception exception, string arguments);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class DocumentOperationsService
{
    // File operations (3300-3319)
    [LoggerMessage(EventId = 3300, Level = LogLevel.Warning,
        Message = "Path is not writable: {FilePath}. Reason: {Reason}")]
    private partial void LogPathNotWritable(string filePath, string? reason);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Warning,
        Message = "File already exists and overwrite not allowed: {FilePath}")]
    private partial void LogFileExistsNoOverwrite(string filePath);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information,
        Message = "File {Operation} at {FilePath}")]
    private partial void LogFileOperation(string operation, string filePath);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Warning,
        Message = "Added non-code file: {FilePath}")]
    private partial void LogNonCodeFileAdded(string filePath);

    [LoggerMessage(EventId = 3304, Level = LogLevel.Information,
        Message = "File added to SDK-style project: {ProjectPath}. Reloading Solution to pick up changes.")]
    private partial void LogFileAddedToSdkProject(string? projectPath);

    [LoggerMessage(EventId = 3305, Level = LogLevel.Warning,
        Message = "Mystery file was not added to any project: {FilePath}")]
    private partial void LogMysteryFileNotAdded(string filePath);

    [LoggerMessage(EventId = 3306, Level = LogLevel.Warning,
        Message = "Document not found in solution: {FilePath}")]
    private partial void LogDocumentNotFound(string filePath);

    [LoggerMessage(EventId = 3307, Level = LogLevel.Information,
        Message = "File formatted and committed: {FilePath}")]
    private partial void LogFileFormattedAndCommitted(string filePath);

    [LoggerMessage(EventId = 3308, Level = LogLevel.Warning,
        Message = "Failed to format file: {FilePath}")]
    private partial void LogFormatFailed(string filePath);

    [LoggerMessage(EventId = 3309, Level = LogLevel.Information,
        Message = "File is already part of project: {FilePath}")]
    private partial void LogFileAlreadyInProject(string filePath);

    // Project operations (3320-3329)
    [LoggerMessage(EventId = 3320, Level = LogLevel.Information,
        Message = "Adding file to {ProjectName}: {FilePath}")]
    private partial void LogAddingFileToProject(string projectName, string filePath);

    [LoggerMessage(EventId = 3321, Level = LogLevel.Error,
        Message = "Failed to add file {FilePath} to project")]
    private partial void LogAddFileFailed(Exception exception, string filePath);

    [LoggerMessage(EventId = 3322, Level = LogLevel.Debug,
        Message = "Project {ProjectPath} is SDK-style (has Sdk attribute)")]
    private partial void LogProjectSdkStyleAttribute(string projectPath);

    [LoggerMessage(EventId = 3323, Level = LogLevel.Debug,
        Message = "Project {ProjectPath} is SDK-style (uses TargetFramework)")]
    private partial void LogProjectSdkStyleFramework(string projectPath);

    [LoggerMessage(EventId = 3324, Level = LogLevel.Debug,
        Message = "Project {ProjectPath} is classic-style (no SDK indicators found)")]
    private partial void LogProjectClassicStyle(string projectPath);

    [LoggerMessage(EventId = 3325, Level = LogLevel.Warning,
        Message = "Error determining project style for {ProjectPath}, assuming classic format")]
    private partial void LogProjectStyleError(Exception exception, string projectPath);

    // Format operations (3330-3339)
    [LoggerMessage(EventId = 3330, Level = LogLevel.Information,
        Message = "Document {FilePath} formatted successfully")]
    private partial void LogDocumentFormatted(string? filePath);

    [LoggerMessage(EventId = 3331, Level = LogLevel.Warning,
        Message = "Failed to format file {FilePath}")]
    private partial void LogDocumentFormatFailed(Exception exception, string? filePath);

    // Git operations (3340-3349)
    [LoggerMessage(EventId = 3340, Level = LogLevel.Debug,
        Message = "Solution path is not available, skipping Git operations")]
    private partial void LogSolutionPathNotAvailable();

    [LoggerMessage(EventId = 3341, Level = LogLevel.Debug,
        Message = "Solution is not in a Git repository, skipping Git operations")]
    private partial void LogNotInGitRepo();

    [LoggerMessage(EventId = 3342, Level = LogLevel.Debug,
        Message = "Solution is in a Git repository, processing Git operations for {Count} files")]
    private partial void LogProcessingGitOperations(int count);

    [LoggerMessage(EventId = 3343, Level = LogLevel.Information,
        Message = "Not on a SharpTools branch, creating one")]
    private partial void LogCreatingSharpToolsBranch();

    [LoggerMessage(EventId = 3344, Level = LogLevel.Information,
        Message = "Git operations completed successfully for {Count} files with commit message: {CommitMessage}")]
    private partial void LogGitOperationsCompleted(int count, string commitMessage);

    [LoggerMessage(EventId = 3345, Level = LogLevel.Warning,
        Message = "Git operations failed for {Count} files but file operations were still applied")]
    private partial void LogGitOperationsFailed(Exception exception, int count);
}

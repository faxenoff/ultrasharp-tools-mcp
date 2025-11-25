using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class CallGraphIndexer
{
    // Call graph indexer (3450-3469)
    [LoggerMessage(EventId = 3450, Level = LogLevel.Information,
        Message = "Call graph indexing is already in progress")]
    private partial void LogIndexingInProgress();

    [LoggerMessage(EventId = 3451, Level = LogLevel.Warning,
        Message = "Cannot start call graph indexing: no solution loaded")]
    private partial void LogNoSolutionForIndexing();

    [LoggerMessage(EventId = 3452, Level = LogLevel.Information,
        Message = "Starting background call graph indexing...")]
    private partial void LogStartingIndexing();

    [LoggerMessage(EventId = 3453, Level = LogLevel.Information,
        Message = "Stopping call graph indexing...")]
    private partial void LogStoppingIndexing();

    [LoggerMessage(EventId = 3454, Level = LogLevel.Information,
        Message = "Call graph indexing stopped")]
    private partial void LogIndexingStopped();

    [LoggerMessage(EventId = 3455, Level = LogLevel.Warning,
        Message = "Cannot index call graph: solution is not loaded")]
    private partial void LogSolutionNotLoadedForIndex();

    [LoggerMessage(EventId = 3456, Level = LogLevel.Information,
        Message = "Collecting methods from solution...")]
    private partial void LogCollectingMethods();

    [LoggerMessage(EventId = 3457, Level = LogLevel.Warning,
        Message = "Failed to get compilation for project {ProjectName}")]
    private partial void LogCompilationFailed(string projectName);

    [LoggerMessage(EventId = 3458, Level = LogLevel.Information,
        Message = "Found {MethodCount} methods to index")]
    private partial void LogMethodsFound(int methodCount);

    [LoggerMessage(EventId = 3459, Level = LogLevel.Information,
        Message = "Call graph indexing progress: {Indexed}/{Total} methods ({Percentage:F1}%)")]
    private partial void LogIndexingProgress(int indexed, int total, double percentage);

    [LoggerMessage(EventId = 3460, Level = LogLevel.Warning,
        Message = "Error indexing method {MethodName}")]
    private partial void LogMethodIndexError(Exception exception, string methodName);

    [LoggerMessage(EventId = 3461, Level = LogLevel.Information,
        Message = "Call graph indexing completed: {MethodCount} methods in {Elapsed:F1} seconds")]
    private partial void LogIndexingCompleted(int methodCount, double elapsed);

    [LoggerMessage(EventId = 3462, Level = LogLevel.Information,
        Message = "Call graph indexing was cancelled")]
    private partial void LogIndexingCancelled();

    [LoggerMessage(EventId = 3463, Level = LogLevel.Error,
        Message = "Error during call graph indexing")]
    private partial void LogIndexingError(Exception exception);

    [LoggerMessage(EventId = 3464, Level = LogLevel.Warning,
        Message = "Timeout indexing method {MethodName} (>30s)")]
    private partial void LogMethodIndexTimeout(string methodName);

    [LoggerMessage(EventId = 3465, Level = LogLevel.Warning,
        Message = "Skipping project {ProjectName} due to {Count} analyzer issues (CS8032/CS8033/CS8034)")]
    private partial void LogSkippingProjectWithAnalyzerIssues(string projectName, int count);

    [LoggerMessage(EventId = 3466, Level = LogLevel.Warning,
        Message = "Skipped {Count} projects due to analyzer issues: {ProjectNames}")]
    private partial void LogProjectsSkippedDueToAnalyzerIssues(int count, string projectNames);
}

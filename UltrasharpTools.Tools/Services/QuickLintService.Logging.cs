using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class QuickLintService
{
    // Quick lint (3440-3449)
    [LoggerMessage(EventId = 3440, Level = LogLevel.Debug,
        Message = "Starting quick lint for {Count} files")]
    private partial void LogStartingLint(int count);

    [LoggerMessage(EventId = 3441, Level = LogLevel.Warning,
        Message = "Solution not loaded, cannot perform quick lint")]
    private partial void LogSolutionNotLoaded();

    [LoggerMessage(EventId = 3442, Level = LogLevel.Warning,
        Message = "Failed to lint project: {ProjectName}")]
    private partial void LogLintProjectFailed(Exception exception, string projectName);

    [LoggerMessage(EventId = 3443, Level = LogLevel.Debug,
        Message = "Quick lint completed: {Errors} errors, {Warnings} warnings")]
    private partial void LogLintCompleted(int errors, int warnings);

    [LoggerMessage(EventId = 3444, Level = LogLevel.Error,
        Message = "Quick lint failed")]
    private partial void LogLintFailed(Exception exception);
}

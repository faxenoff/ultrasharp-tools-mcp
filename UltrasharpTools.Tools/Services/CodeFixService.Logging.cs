using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class CodeFixService
{
    // Code fix operations (3400-3409)
    [LoggerMessage(EventId = 3400, Level = LogLevel.Information,
        Message = "Starting code fix application for solution: {SolutionPath}, DiagnosticId: {DiagnosticId}, Preview: {Preview}")]
    private partial void LogStartingCodeFix(string solutionPath, string diagnosticId, bool preview);

    [LoggerMessage(EventId = 3401, Level = LogLevel.Information,
        Message = "Would apply fix: {ActionTitle}")]
    private partial void LogWouldApplyFix(string actionTitle);

    [LoggerMessage(EventId = 3402, Level = LogLevel.Warning,
        Message = "Failed to apply fix for diagnostic: {DiagnosticId}")]
    private partial void LogApplyFixFailed(Exception exception, string diagnosticId);

    [LoggerMessage(EventId = 3403, Level = LogLevel.Warning,
        Message = "Failed to process project: {ProjectName}")]
    private partial void LogProcessProjectFailed(Exception exception, string projectName);

    [LoggerMessage(EventId = 3404, Level = LogLevel.Information,
        Message = "Code fix application complete. Total fixes found: {Count}, Preview: {Preview}")]
    private partial void LogCodeFixComplete(int count, bool preview);
}

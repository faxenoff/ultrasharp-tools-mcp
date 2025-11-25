using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class ConflictDetectionService
{
    // Duplicate detection (7400-7403)
    [LoggerMessage(EventId = 7400, Level = LogLevel.Debug,
        Message = "Detecting duplicates for {Project}/{Branch}/{File}")]
    private partial void LogDetectingDuplicates(string project, string branch, string file);

    [LoggerMessage(EventId = 7401, Level = LogLevel.Information,
        Message = "Found {Count} duplicates for {Project}/{File}")]
    private partial void LogDuplicatesFound(int count, string project, string file);

    [LoggerMessage(EventId = 7402, Level = LogLevel.Error,
        Message = "Failed to detect duplicates for {Project}/{File}")]
    private partial void LogDetectDuplicatesFailed(Exception exception, string project, string file);

    // Notifications (7403)
    [LoggerMessage(EventId = 7403, Level = LogLevel.Information,
        Message = "Sent duplicate notification: {Project} -> {DuplicateProject} (similarity: {Similarity:P0})")]
    private partial void LogDuplicateNotificationSent(string project, string duplicateProject, double similarity);
}

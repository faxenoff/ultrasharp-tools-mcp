using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class ImportUpdateService
{
    // Import updates (3490-3509)
    [LoggerMessage(EventId = 3490, Level = LogLevel.Debug,
        Message = "Analyzing required usings for: {FilePath}")]
    private partial void LogAnalyzingUsings(string filePath);

    [LoggerMessage(EventId = 3491, Level = LogLevel.Warning,
        Message = "Document not found in solution: {FilePath}")]
    private partial void LogDocumentNotFound(string filePath);

    [LoggerMessage(EventId = 3492, Level = LogLevel.Debug,
        Message = "Found {Count} required namespaces for {FilePath}")]
    private partial void LogNamespacesFound(int count, string filePath);

    [LoggerMessage(EventId = 3493, Level = LogLevel.Information,
        Message = "Updating usings for: {FilePath} (removeUnused={Remove}, addMissing={Add})")]
    private partial void LogUpdatingUsings(string filePath, bool remove, bool add);

    [LoggerMessage(EventId = 3494, Level = LogLevel.Debug,
        Message = "No using changes needed for {FilePath}")]
    private partial void LogNoChangesNeeded(string filePath);

    [LoggerMessage(EventId = 3495, Level = LogLevel.Information,
        Message = "Updated usings for {FilePath}: +{Added} -{Removed}")]
    private partial void LogUsingsUpdated(string filePath, int added, int removed);

    [LoggerMessage(EventId = 3496, Level = LogLevel.Information,
        Message = "Batch updating usings for {Count} files")]
    private partial void LogBatchUpdating(int count);

    [LoggerMessage(EventId = 3497, Level = LogLevel.Error,
        Message = "Failed to update usings for {FilePath}")]
    private partial void LogUpdateFailed(Exception exception, string filePath);

    [LoggerMessage(EventId = 3498, Level = LogLevel.Information,
        Message = "Batch update complete: {Success}/{Total} files updated")]
    private partial void LogBatchComplete(int success, int total);

    [LoggerMessage(EventId = 3499, Level = LogLevel.Debug,
        Message = "Analyzing import changes: {Original} -> {Count} new files")]
    private partial void LogAnalyzingImportChanges(string original, int count);
}

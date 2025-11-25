using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class FileWatcherService
{
    // Service lifecycle (5400-5403)
    [LoggerMessage(EventId = 5400, Level = LogLevel.Information,
        Message = "FileWatcherService starting for path: {Path}")]
    private partial void LogStarting(string path);

    [LoggerMessage(EventId = 5401, Level = LogLevel.Information,
        Message = "FileWatcher started. Monitoring patterns: {Patterns}")]
    private partial void LogStarted(string patterns);

    [LoggerMessage(EventId = 5402, Level = LogLevel.Information,
        Message = "FileWatcherService stopping")]
    private partial void LogStopping();

    [LoggerMessage(EventId = 5403, Level = LogLevel.Error,
        Message = "FileWatcherService error")]
    private partial void LogServiceError(Exception exception);

    // File events (5404-5405)
    [LoggerMessage(EventId = 5404, Level = LogLevel.Debug,
        Message = "File {ChangeType}: {Path}")]
    private partial void LogFileChange(WatcherChangeTypes changeType, string path);

    [LoggerMessage(EventId = 5405, Level = LogLevel.Debug,
        Message = "File renamed: {OldPath} -> {NewPath}")]
    private partial void LogFileRenamed(string oldPath, string newPath);

    // Embedding (5406-5407)
    [LoggerMessage(EventId = 5406, Level = LogLevel.Debug,
        Message = "Generated embedding: {Dimensions} dimensions")]
    private partial void LogGeneratedEmbedding(int dimensions);

    [LoggerMessage(EventId = 5407, Level = LogLevel.Warning,
        Message = "Failed to generate embedding, continuing without vectors")]
    private partial void LogEmbeddingFailed(Exception exception);

    // Symbol extraction (5408-5409)
    [LoggerMessage(EventId = 5408, Level = LogLevel.Debug,
        Message = "Extracted {SymbolCount} symbols from {File}")]
    private partial void LogExtractedSymbols(int symbolCount, string file);

    [LoggerMessage(EventId = 5409, Level = LogLevel.Warning,
        Message = "Failed to extract symbols from {File}, continuing without symbols")]
    private partial void LogSymbolExtractionFailed(Exception exception, string file);

    // Process file change (5410-5411)
    [LoggerMessage(EventId = 5410, Level = LogLevel.Information,
        Message = "Processed file change: {Project}/{Branch}/{File} ({Action})")]
    private partial void LogProcessedChange(string project, string branch, string file, string action);

    [LoggerMessage(EventId = 5411, Level = LogLevel.Error,
        Message = "Failed to process file change: {Path}")]
    private partial void LogProcessFileFailed(Exception exception, string path);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SqliteReflectionTypeIndex
{
    [LoggerMessage(EventId = 3570, Level = LogLevel.Information,
        Message = "SQLite reflection type index initialized at {DbPath}")]
    private partial void LogInitialized(string dbPath);

    [LoggerMessage(EventId = 3571, Level = LogLevel.Information,
        Message = "Populating SQLite reflection type index...")]
    private partial void LogPopulating();

    [LoggerMessage(EventId = 3572, Level = LogLevel.Information,
        Message = "SQLite reflection type index is up-to-date with {Count} types")]
    private partial void LogUpToDate(int count);

    [LoggerMessage(EventId = 3573, Level = LogLevel.Trace,
        Message = "Error loading types from {Path}: {ErrorMessage}")]
    private partial void LogTypeLoadError(string path, string errorMessage);

    [LoggerMessage(EventId = 3574, Level = LogLevel.Information,
        Message = "SQLite reflection type index populated: {Count} types in {ElapsedMs}ms. DB size: {Size}")]
    private partial void LogPopulated(int count, long elapsedMs, string size);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SymbolCacheManager
{
    [LoggerMessage(EventId = 3800, Level = LogLevel.Information,
        Message = "Created symbol cache directory: {Directory}")]
    private partial void LogCacheDirectoryCreated(string directory);

    [LoggerMessage(EventId = 3801, Level = LogLevel.Debug,
        Message = "Solution has no file path, cannot load cache")]
    private partial void LogNoSolutionPathForLoad();

    [LoggerMessage(EventId = 3802, Level = LogLevel.Debug,
        Message = "No cache file found at {Path}")]
    private partial void LogNoCacheFile(string path);

    [LoggerMessage(EventId = 3803, Level = LogLevel.Information,
        Message = "Loading symbol cache from {Path}...")]
    private partial void LogLoadingCache(string path);

    [LoggerMessage(EventId = 3804, Level = LogLevel.Warning,
        Message = "Failed to deserialize cache file")]
    private partial void LogDeserializationFailed();

    [LoggerMessage(EventId = 3805, Level = LogLevel.Warning,
        Message = "Cache version mismatch: expected {Expected}, got {Actual}. Cache invalidated.")]
    private partial void LogVersionMismatch(int expected, int actual);

    [LoggerMessage(EventId = 3806, Level = LogLevel.Information,
        Message = "Solution has changed, cache invalidated")]
    private partial void LogSolutionChanged();

    [LoggerMessage(EventId = 3807, Level = LogLevel.Information,
        Message = "Successfully loaded {Count} symbols from cache")]
    private partial void LogSymbolsLoaded(int count);

    [LoggerMessage(EventId = 3808, Level = LogLevel.Error,
        Message = "Error loading symbol cache")]
    private partial void LogLoadError(Exception exception);

    [LoggerMessage(EventId = 3809, Level = LogLevel.Warning,
        Message = "Solution has no file path, cannot save cache")]
    private partial void LogNoSolutionPathForSave();

    [LoggerMessage(EventId = 3810, Level = LogLevel.Information,
        Message = "Saving {Count} symbols to cache at {Path}...")]
    private partial void LogSavingCache(int count, string path);

    [LoggerMessage(EventId = 3811, Level = LogLevel.Information,
        Message = "Successfully saved symbol cache ({Size} bytes)")]
    private partial void LogCacheSaved(int size);

    [LoggerMessage(EventId = 3812, Level = LogLevel.Error,
        Message = "Error saving symbol cache")]
    private partial void LogSaveError(Exception exception);

    [LoggerMessage(EventId = 3813, Level = LogLevel.Debug,
        Message = "Solution file hash mismatch")]
    private partial void LogSolutionHashMismatch();

    [LoggerMessage(EventId = 3814, Level = LogLevel.Debug,
        Message = "Project count mismatch: expected {Expected}, got {Actual}")]
    private partial void LogProjectCountMismatch(int expected, int actual);

    [LoggerMessage(EventId = 3815, Level = LogLevel.Debug,
        Message = "Project {ProjectName} not found in cache metadata")]
    private partial void LogProjectNotInCache(string projectName);

    [LoggerMessage(EventId = 3816, Level = LogLevel.Debug,
        Message = "Project {ProjectName} file modified (timestamp changed)")]
    private partial void LogProjectModified(string projectName);

    [LoggerMessage(EventId = 3817, Level = LogLevel.Debug,
        Message = "Project {ProjectName} file hash mismatch")]
    private partial void LogProjectHashMismatch(string projectName);

    [LoggerMessage(EventId = 3818, Level = LogLevel.Warning,
        Message = "Failed to delete cache file {File}")]
    private partial void LogDeleteFailed(Exception exception, string file);

    [LoggerMessage(EventId = 3819, Level = LogLevel.Information,
        Message = "Cleared {Count} cache files (parallel)")]
    private partial void LogCacheCleared(int count);

    [LoggerMessage(EventId = 3820, Level = LogLevel.Error,
        Message = "Error clearing cache files")]
    private partial void LogClearError(Exception exception);
}

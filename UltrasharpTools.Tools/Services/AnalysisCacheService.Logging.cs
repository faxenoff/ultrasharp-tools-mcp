using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class AnalysisCacheService
{
    [LoggerMessage(EventId = 3780, Level = LogLevel.Information,
        Message = "Created analysis cache directory: {Directory}")]
    private partial void LogCacheDirectoryCreated(string directory);

    [LoggerMessage(EventId = 3781, Level = LogLevel.Information,
        Message = "AnalysisCacheService initialized with database: {DbPath}")]
    private partial void LogInitialized(string dbPath);

    [LoggerMessage(EventId = 3782, Level = LogLevel.Debug,
        Message = "Database schema initialized")]
    private partial void LogSchemaInitialized();

    [LoggerMessage(EventId = 3783, Level = LogLevel.Trace,
        Message = "Cache miss for {Operation} (key: {Key})")]
    private partial void LogCacheMiss(string operation, string key);

    [LoggerMessage(EventId = 3784, Level = LogLevel.Debug,
        Message = "Cache version mismatch for {Operation}: expected {Expected}, got {Actual}")]
    private partial void LogVersionMismatch(string operation, int expected, int actual);

    [LoggerMessage(EventId = 3785, Level = LogLevel.Debug,
        Message = "Cache entry expired for {Operation} (age: {Age}s, TTL: {Ttl}s)")]
    private partial void LogCacheExpired(string operation, long age, int ttl);

    [LoggerMessage(EventId = 3786, Level = LogLevel.Debug,
        Message = "Cache hit for {Operation} (key: {Key})")]
    private partial void LogCacheHit(string operation, string key);

    [LoggerMessage(EventId = 3787, Level = LogLevel.Warning,
        Message = "Error reading from cache for {Operation}")]
    private partial void LogCacheReadError(Exception exception, string operation);

    [LoggerMessage(EventId = 3788, Level = LogLevel.Trace,
        Message = "Skipping cache for {Operation}: result type {Type} not registered in JsonContext")]
    private partial void LogSkippingCache(string operation, string type);

    [LoggerMessage(EventId = 3789, Level = LogLevel.Debug,
        Message = "Cached result for {Operation} (key: {Key}, size: {Size} bytes)")]
    private partial void LogResultCached(string operation, string key, int size);

    [LoggerMessage(EventId = 3790, Level = LogLevel.Warning,
        Message = "Error writing to cache for {Operation}")]
    private partial void LogCacheWriteError(Exception exception, string operation);

    [LoggerMessage(EventId = 3791, Level = LogLevel.Information,
        Message = "Invalidated {Count} cache entries for solution {Hash}")]
    private partial void LogSolutionInvalidated(int count, string hash);

    [LoggerMessage(EventId = 3792, Level = LogLevel.Error,
        Message = "Error invalidating cache for solution {Hash}")]
    private partial void LogInvalidationError(Exception exception, string hash);

    [LoggerMessage(EventId = 3793, Level = LogLevel.Information,
        Message = "Cleaned up {Count} expired cache entries")]
    private partial void LogExpiredCleaned(int count);

    [LoggerMessage(EventId = 3794, Level = LogLevel.Error,
        Message = "Error cleaning up expired cache entries")]
    private partial void LogCleanupError(Exception exception);

    [LoggerMessage(EventId = 3795, Level = LogLevel.Error,
        Message = "Error getting cache statistics")]
    private partial void LogStatisticsError(Exception exception);

    [LoggerMessage(EventId = 3796, Level = LogLevel.Information,
        Message = "Cleared all cache entries ({Count} deleted)")]
    private partial void LogCacheCleared(int count);

    [LoggerMessage(EventId = 3797, Level = LogLevel.Error,
        Message = "Error clearing cache")]
    private partial void LogClearError(Exception exception);

    [LoggerMessage(EventId = 3798, Level = LogLevel.Trace,
        Message = "Error updating access time for cache key {Key}")]
    private partial void LogAccessTimeError(Exception exception, string key);
}

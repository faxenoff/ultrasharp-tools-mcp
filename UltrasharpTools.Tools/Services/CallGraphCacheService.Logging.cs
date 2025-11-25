using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class CallGraphCacheService
{
    // CallGraph cache (3420-3439)
    [LoggerMessage(EventId = 3420, Level = LogLevel.Information,
        Message = "CallGraphCache database path: {DbPath}")]
    private partial void LogCacheDbPath(string dbPath);

    [LoggerMessage(EventId = 3421, Level = LogLevel.Information,
        Message = "CallGraphCache database initialized successfully")]
    private partial void LogCacheDbInitialized();

    [LoggerMessage(EventId = 3422, Level = LogLevel.Debug,
        Message = "Cache HIT for method: {Method}")]
    private partial void LogCacheHit(string method);

    [LoggerMessage(EventId = 3423, Level = LogLevel.Debug,
        Message = "Cache MISS for method: {Method}")]
    private partial void LogCacheMiss(string method);

    [LoggerMessage(EventId = 3424, Level = LogLevel.Debug,
        Message = "Cached callers for method: {Method}, count: {Count}")]
    private partial void LogCachedCallers(string method, int count);

    [LoggerMessage(EventId = 3425, Level = LogLevel.Information,
        Message = "Invalidated {Count} cache entries for modified files")]
    private partial void LogInvalidatedByFiles(int count);

    [LoggerMessage(EventId = 3426, Level = LogLevel.Information,
        Message = "Invalidated entire call graph cache ({Count} entries)")]
    private partial void LogInvalidatedAll(int count);

    [LoggerMessage(EventId = 3427, Level = LogLevel.Information,
        Message = "Compacted call graph cache database")]
    private partial void LogCompacted();

    // Full caller cache (3428-3439)
    [LoggerMessage(EventId = 3428, Level = LogLevel.Debug,
        Message = "Full cache HIT for method: {Method}")]
    private partial void LogFullCacheHit(string method);

    [LoggerMessage(EventId = 3429, Level = LogLevel.Debug,
        Message = "Full cache MISS for method: {Method}")]
    private partial void LogFullCacheMiss(string method);

    [LoggerMessage(EventId = 3430, Level = LogLevel.Debug,
        Message = "Cached full callers for method: {Method}, count: {Count}")]
    private partial void LogCachedFullCallers(string method, int count);

    [LoggerMessage(EventId = 3431, Level = LogLevel.Information,
        Message = "Created CallGraphFull table for full caller caching")]
    private partial void LogFullTableCreated();
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SyntaxTreeCacheService
{
    [LoggerMessage(EventId = 3610, Level = LogLevel.Information,
        Message = "SyntaxTreeCache initialized: capacity={Capacity}, TTL={TtlMinutes}m")]
    private partial void LogInitialized(int capacity, double ttlMinutes);

    [LoggerMessage(EventId = 3611, Level = LogLevel.Debug,
        Message = "SyntaxTree cache HIT: {FilePath}")]
    private partial void LogCacheHit(string filePath);

    [LoggerMessage(EventId = 3612, Level = LogLevel.Debug,
        Message = "SyntaxTree cache MISS: {FilePath}")]
    private partial void LogCacheMiss(string filePath);

    [LoggerMessage(EventId = 3613, Level = LogLevel.Debug,
        Message = "SyntaxTree parsed and cached: {FilePath} ({ElapsedMs}ms)")]
    private partial void LogParsedAndCached(string filePath, long elapsedMs);

    [LoggerMessage(EventId = 3614, Level = LogLevel.Debug,
        Message = "Invalidated SyntaxTree cache for file: {FilePath}")]
    private partial void LogInvalidatedFile(string filePath);

    [LoggerMessage(EventId = 3615, Level = LogLevel.Information,
        Message = "Invalidated entire SyntaxTree cache")]
    private partial void LogInvalidatedAll();

    [LoggerMessage(EventId = 3616, Level = LogLevel.Information,
        Message = "SyntaxTreeCache Stats: Hit={HitCount}, Miss={MissCount}, HitRate={HitRate:F1}%, Entries={CachedEntries}, ParseTime={ParseTimeMs}ms")]
    private partial void LogStats(long hitCount, long missCount, double hitRate, int cachedEntries, long parseTimeMs);
}

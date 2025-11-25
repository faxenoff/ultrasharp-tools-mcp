using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SemanticModelCacheService
{
    [LoggerMessage(EventId = 3620, Level = LogLevel.Information,
        Message = "SemanticModelCache initialized: capacity={Capacity}, TTL={TtlMinutes}m")]
    private partial void LogInitialized(int capacity, double ttlMinutes);

    [LoggerMessage(EventId = 3621, Level = LogLevel.Debug,
        Message = "SemanticModel cache HIT: {FilePath}")]
    private partial void LogCacheHit(string filePath);

    [LoggerMessage(EventId = 3622, Level = LogLevel.Debug,
        Message = "SemanticModel cache MISS: {FilePath}")]
    private partial void LogCacheMiss(string filePath);

    [LoggerMessage(EventId = 3623, Level = LogLevel.Debug,
        Message = "SemanticModel computed and cached: {FilePath} ({ElapsedMs}ms)")]
    private partial void LogComputedAndCached(string filePath, long elapsedMs);

    [LoggerMessage(EventId = 3624, Level = LogLevel.Debug,
        Message = "Invalidated SemanticModel cache for file: {FilePath}")]
    private partial void LogInvalidatedFile(string filePath);

    [LoggerMessage(EventId = 3625, Level = LogLevel.Information,
        Message = "Invalidated SemanticModel cache for project: {ProjectName} ({Count} entries)")]
    private partial void LogInvalidatedProject(string projectName, int count);

    [LoggerMessage(EventId = 3626, Level = LogLevel.Information,
        Message = "Invalidated entire SemanticModel cache")]
    private partial void LogInvalidatedAll();

    [LoggerMessage(EventId = 3627, Level = LogLevel.Information,
        Message = "SemanticModelCache Stats: Hit={HitCount}, Miss={MissCount}, HitRate={HitRate:F1}%, Entries={CachedEntries}, ComputeTime={ComputeTimeMs}ms, AvgCompute={AvgComputeMs:F1}ms")]
    private partial void LogStats(long hitCount, long missCount, double hitRate, int cachedEntries, long computeTimeMs, double avgComputeMs);
}

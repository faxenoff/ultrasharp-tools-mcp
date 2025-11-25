using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Cache для SemanticModel instances
/// SemanticModel is expensive to compute (~50-200ms per file)
/// Caching provides 10-50x speedup для repeated symbol lookups
/// </summary>
public sealed partial class SemanticModelCacheService
{
    private readonly ILogger<SemanticModelCacheService> _logger;
    private readonly LruCache<string, CachedSemanticModel> _cache;
    private readonly TimeSpan _ttl;

    // Metrics
    private long _hitCount;
    private long _missCount;
    private long _computeTimeMs;

    public SemanticModelCacheService(
        ILogger<SemanticModelCacheService> logger,
        int capacity = 200,
        TimeSpan? ttl = null
    )
    {
        _logger = logger;
        _cache = new LruCache<string, CachedSemanticModel>(capacity);
        _ttl = ttl ?? TimeSpan.FromMinutes(5);

        LogInitialized(capacity, _ttl.TotalMinutes);
    }

    /// <summary>
    /// Get или compute SemanticModel для document
    /// </summary>
    public async Task<SemanticModel?> GetOrComputeAsync(
        Document document,
        CancellationToken cancellationToken = default
    )
    {
        var filePath = document.FilePath ?? document.Name;
        var documentVersion = await document
            .GetTextVersionAsync(cancellationToken)
            .ConfigureAwait(false);
        var cacheKey = $"{filePath}|{documentVersion}";

        // Check cache
        if (_cache.TryGet(cacheKey, out var cached))
        {
            // Check TTL
            if (DateTimeOffset.UtcNow - cached.CreatedAt < _ttl)
            {
                Interlocked.Increment(ref _hitCount);
                LogCacheHit(filePath);
                return cached.SemanticModel;
            }

            // Expired - remove from cache
            _cache.Remove(cacheKey);
        }

        Interlocked.Increment(ref _missCount);
        LogCacheMiss(filePath);

        // Compute SemanticModel
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var semanticModel = await document
            .GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        sw.Stop();

        Interlocked.Add(ref _computeTimeMs, sw.ElapsedMilliseconds);

        if (semanticModel != null)
        {
            // Add to cache
            _cache.Add(
                cacheKey,
                new CachedSemanticModel
                {
                    SemanticModel = semanticModel,
                    CreatedAt = DateTimeOffset.UtcNow,
                    FilePath = filePath,
                }
            );

            LogComputedAndCached(filePath, sw.ElapsedMilliseconds);
        }

        return semanticModel;
    }

    /// <summary>
    /// Invalidate cache для specific file
    /// </summary>
    public void InvalidateFile(string filePath)
    {
        // Remove all entries for this file (different versions)
        var keysToRemove = _cache
            .Keys.Where(k => k.StartsWith(filePath + "|", StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        LogInvalidatedFile(filePath);
    }

    /// <summary>
    /// Invalidate cache для specific project (когда project rebuilds)
    /// </summary>
    public void InvalidateProject(string projectName)
    {
        var keysToRemove = _cache
            .Keys.Where(k => k.Contains(projectName, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        LogInvalidatedProject(projectName, keysToRemove.Count);
    }

    /// <summary>
    /// Invalidate all cache entries
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        LogInvalidatedAll();
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public SemanticModelCacheStats GetStats()
    {
        var hitCount = Interlocked.Read(ref _hitCount);
        var missCount = Interlocked.Read(ref _missCount);
        var totalRequests = hitCount + missCount;

        return new SemanticModelCacheStats
        {
            HitCount = hitCount,
            MissCount = missCount,
            TotalRequests = totalRequests,
            HitRate = totalRequests > 0 ? (double)hitCount / totalRequests * 100.0 : 0.0,
            CachedEntries = _cache.Count,
            TotalComputeTimeMs = Interlocked.Read(ref _computeTimeMs),
            AverageComputeTimeMs =
                missCount > 0 ? (double)Interlocked.Read(ref _computeTimeMs) / missCount : 0.0,
        };
    }

    /// <summary>
    /// Log cache statistics
    /// </summary>
    public void LogStats()
    {
        var stats = GetStats();
        LogStats(stats.HitCount, stats.MissCount, stats.HitRate, stats.CachedEntries, stats.TotalComputeTimeMs, stats.AverageComputeTimeMs);
    }
}

/// <summary>
/// Cached SemanticModel entry
/// </summary>
internal sealed class CachedSemanticModel
{
    public required SemanticModel SemanticModel { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string FilePath { get; init; }
}

/// <summary>
/// SemanticModel cache statistics
/// </summary>
public sealed class SemanticModelCacheStats
{
    public long HitCount { get; init; }
    public long MissCount { get; init; }
    public long TotalRequests { get; init; }
    public double HitRate { get; init; }
    public int CachedEntries { get; init; }
    public long TotalComputeTimeMs { get; init; }
    public double AverageComputeTimeMs { get; init; }
}

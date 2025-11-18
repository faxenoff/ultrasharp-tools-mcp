using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Cache для parsed SyntaxTree instances
/// Снижает overhead re-parsing для frequently accessed files
/// </summary>
public sealed class SyntaxTreeCacheService
{
    private readonly ILogger<SyntaxTreeCacheService> _logger;
    private readonly LruCache<string, CachedSyntaxTree> _cache;
    private readonly TimeSpan _ttl;

    // Metrics
    private long _hitCount;
    private long _missCount;
    private long _parseTimeMs;

    public SyntaxTreeCacheService(
        ILogger<SyntaxTreeCacheService> logger,
        int capacity = 500,
        TimeSpan? ttl = null)
    {
        _logger = logger;
        _cache = new LruCache<string, CachedSyntaxTree>(capacity);
        _ttl = ttl ?? TimeSpan.FromMinutes(10);

        _logger.LogInformation(
            "SyntaxTreeCache initialized: capacity={Capacity}, TTL={TtlMinutes}m",
            capacity,
            _ttl.TotalMinutes);
    }

    /// <summary>
    /// Get или parse SyntaxTree для document
    /// </summary>
    public async Task<SyntaxTree?> GetOrParseAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        var filePath = document.FilePath ?? document.Name;
        var documentVersion = await document.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{filePath}|{documentVersion}";

        // Check cache
        if (_cache.TryGet(cacheKey, out var cached))
        {
            // Check TTL
            if (DateTimeOffset.UtcNow - cached.CreatedAt < _ttl)
            {
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("SyntaxTree cache HIT: {FilePath}", filePath);
                return cached.SyntaxTree;
            }

            // Expired - remove from cache
            _cache.Remove(cacheKey);
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogDebug("SyntaxTree cache MISS: {FilePath}", filePath);

        // Parse SyntaxTree
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        sw.Stop();

        Interlocked.Add(ref _parseTimeMs, sw.ElapsedMilliseconds);

        if (syntaxTree != null)
        {
            // Add to cache
            _cache.Add(cacheKey, new CachedSyntaxTree
            {
                SyntaxTree = syntaxTree,
                CreatedAt = DateTimeOffset.UtcNow,
                FilePath = filePath
            });

            _logger.LogDebug(
                "SyntaxTree parsed and cached: {FilePath} ({ElapsedMs}ms)",
                filePath,
                sw.ElapsedMilliseconds);
        }

        return syntaxTree;
    }

    /// <summary>
    /// Invalidate cache для specific file
    /// </summary>
    public void InvalidateFile(string filePath)
    {
        // Remove all entries for this file (different versions)
        var keysToRemove = _cache.Keys
            .Where(k => k.StartsWith(filePath + "|", StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }

        _logger.LogDebug("Invalidated SyntaxTree cache for file: {FilePath}", filePath);
    }

    /// <summary>
    /// Invalidate all cache entries
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.LogInformation("Invalidated entire SyntaxTree cache");
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public SyntaxTreeCacheStats GetStats()
    {
        var hitCount = Interlocked.Read(ref _hitCount);
        var missCount = Interlocked.Read(ref _missCount);
        var totalRequests = hitCount + missCount;

        return new SyntaxTreeCacheStats
        {
            HitCount = hitCount,
            MissCount = missCount,
            TotalRequests = totalRequests,
            HitRate = totalRequests > 0 ? (double)hitCount / totalRequests * 100.0 : 0.0,
            CachedEntries = _cache.Count,
            TotalParseTimeMs = Interlocked.Read(ref _parseTimeMs)
        };
    }

    /// <summary>
    /// Log cache statistics
    /// </summary>
    public void LogStats()
    {
        var stats = GetStats();
        _logger.LogInformation(
            "SyntaxTreeCache Stats: Hit={HitCount}, Miss={MissCount}, HitRate={HitRate:F1}%, Entries={CachedEntries}, ParseTime={ParseTimeMs}ms",
            stats.HitCount,
            stats.MissCount,
            stats.HitRate,
            stats.CachedEntries,
            stats.TotalParseTimeMs);
    }
}

/// <summary>
/// Cached SyntaxTree entry
/// </summary>
internal sealed class CachedSyntaxTree
{
    public required SyntaxTree SyntaxTree { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string FilePath { get; init; }
}

/// <summary>
/// SyntaxTree cache statistics
/// </summary>
public sealed class SyntaxTreeCacheStats
{
    public long HitCount { get; init; }
    public long MissCount { get; init; }
    public long TotalRequests { get; init; }
    public double HitRate { get; init; }
    public int CachedEntries { get; init; }
    public long TotalParseTimeMs { get; init; }
}

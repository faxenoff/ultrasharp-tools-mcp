namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Cache entry with file-based invalidation metadata
/// </summary>
/// <typeparam name="T">Cached value type</typeparam>
public sealed class CacheEntry<T>
    where T : class
{
    public required T Value { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public required string FilePath { get; init; }
    public DateTime CachedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Check if cache entry is still valid based on file modification time
    /// </summary>
    public bool IsValid()
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        try
        {
            var currentWriteTime = File.GetLastWriteTimeUtc(FilePath);
            return currentWriteTime <= LastWriteTimeUtc;
        }
        catch
        {
            // If we can't check - assume invalid
            return false;
        }
    }
}

/// <summary>
/// Cache statistics for monitoring
/// </summary>
public sealed class CacheStatistics
{
    public int CompilationCacheSize { get; init; }
    public int SemanticModelCacheSize { get; init; }
    public int ReflectionTypesCacheSize { get; init; }
    public long CompilationCacheHits { get; init; }
    public long CompilationCacheMisses { get; init; }
    public long SemanticModelCacheHits { get; init; }
    public long SemanticModelCacheMisses { get; init; }
    public double CompilationHitRate =>
        CompilationCacheHits + CompilationCacheMisses > 0
            ? CompilationCacheHits / (double)(CompilationCacheHits + CompilationCacheMisses)
            : 0.0;
    public double SemanticModelHitRate =>
        SemanticModelCacheHits + SemanticModelCacheMisses > 0
            ? SemanticModelCacheHits / (double)(SemanticModelCacheHits + SemanticModelCacheMisses)
            : 0.0;
    public long TotalMemoryBytes { get; init; }
}

/// <summary>
/// Project-level cache entry (tracks all document IDs for granular invalidation)
/// </summary>
public sealed class ProjectCacheEntry
{
    public required Microsoft.CodeAnalysis.Compilation Value { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public required string ProjectFilePath { get; init; }
    public required HashSet<Microsoft.CodeAnalysis.DocumentId> DocumentIds { get; init; }
    public DateTime CachedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Check if project file has been modified
    /// </summary>
    public bool IsValid()
    {
        if (!File.Exists(ProjectFilePath))
        {
            return false;
        }

        try
        {
            var currentWriteTime = File.GetLastWriteTimeUtc(ProjectFilePath);
            return currentWriteTime <= LastWriteTimeUtc;
        }
        catch
        {
            return false;
        }
    }
}

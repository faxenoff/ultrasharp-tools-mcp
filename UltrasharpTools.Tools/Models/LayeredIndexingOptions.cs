namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Configuration options for layered indexing.
/// Controls memory usage, persistence, and optimization thresholds.
/// </summary>
public sealed record LayeredIndexingOptions
{
    /// <summary>
    /// Maximum number of branch deltas to keep in memory.
    /// Least Recently Used (LRU) eviction policy applied when exceeded.
    /// Default: 20 branches.
    /// </summary>
    public int MaxBranchDeltas { get; init; } = 20;

    /// <summary>
    /// Enable persistent SQLite cache for branch deltas.
    /// When enabled, deltas survive server restart.
    /// Default: true.
    /// </summary>
    public bool EnablePersistence { get; init; } = true;

    /// <summary>
    /// Compact (rebase) branch deltas larger than this threshold.
    /// Large deltas are recomputed from current git diff to reduce memory.
    /// Default: 1000 changes.
    /// </summary>
    public int DeltaCompactionThreshold { get; init; } = 1000;

    /// <summary>
    /// Directory for storing branch delta caches.
    /// If null, uses ProjectPathHelper.GetProjectUltrasharpDir() + "/cache/branches".
    /// </summary>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// Enable automatic cleanup of orphaned branch deltas.
    /// Removes deltas for branches that no longer exist in git.
    /// Default: true.
    /// </summary>
    public bool AutoCleanupOrphanedDeltas { get; init; } = true;

    /// <summary>
    /// Interval for automatic cleanup task (in hours).
    /// Default: 24 hours.
    /// </summary>
    public int CleanupIntervalHours { get; init; } = 24;

    /// <summary>
    /// Enable background persistence (write-behind) for deltas.
    /// When enabled, delta saves are debounced and run asynchronously.
    /// Default: true.
    /// </summary>
    public bool EnableBackgroundPersistence { get; init; } = true;

    /// <summary>
    /// Debounce delay for background persistence (in milliseconds).
    /// Default: 1000ms (1 second).
    /// </summary>
    public int PersistenceDebounceMs { get; init; } = 1000;

    /// <summary>
    /// Enable background scheduler for delta compaction and orphaned deltas cleanup.
    /// When enabled, scheduler runs periodic maintenance tasks.
    /// Default: true.
    /// </summary>
    public bool EnableBackgroundScheduler { get; init; } = true;

    /// <summary>
    /// Interval for delta compaction task (in minutes).
    /// Default: 30 minutes.
    /// </summary>
    public int CompactionIntervalMinutes { get; init; } = 30;

    /// <summary>
    /// Interval for orphaned delta cleanup task (in minutes).
    /// Default: 60 minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; init; } = 60;

    /// <summary>
    /// Default options (all features enabled with reasonable defaults).
    /// </summary>
    public static LayeredIndexingOptions Default => new();

    /// <summary>
    /// Options for development (more aggressive caching, faster updates).
    /// </summary>
    public static LayeredIndexingOptions Development => new()
    {
        MaxBranchDeltas = 50, // More branches in memory
        DeltaCompactionThreshold = 500, // More aggressive compaction
        PersistenceDebounceMs = 500, // Faster persistence
        CleanupIntervalHours = 1, // More frequent cleanup
        CompactionIntervalMinutes = 15, // More frequent compaction
        CleanupIntervalMinutes = 30 // More frequent orphaned cleanup
    };

    /// <summary>
    /// Options for production (conservative, stable).
    /// </summary>
    public static LayeredIndexingOptions Production => new()
    {
        MaxBranchDeltas = 20,
        DeltaCompactionThreshold = 1000,
        PersistenceDebounceMs = 2000,
        CleanupIntervalHours = 24,
        CompactionIntervalMinutes = 30,
        CleanupIntervalMinutes = 60
    };
}

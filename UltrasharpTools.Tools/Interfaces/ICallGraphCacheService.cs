using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for caching call graph relationships between methods.
/// Provides persistent storage to avoid expensive SymbolFinder.FindCallersAsync calls.
/// </summary>
public interface ICallGraphCacheService
{
    /// <summary>
    /// Gets cached callers for a method.
    /// </summary>
    /// <param name="methodFqn">Fully qualified name of the method</param>
    /// <param name="solutionHash">Hash of the current solution state for cache validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of caller FQNs, or null if not cached or stale</returns>
    Task<List<string>?> GetCallersAsync(
        string methodFqn,
        string solutionHash,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Stores callers for a method in cache.
    /// </summary>
    /// <param name="methodFqn">Fully qualified name of the method</param>
    /// <param name="callerFqns">List of caller FQNs</param>
    /// <param name="solutionHash">Hash of the current solution state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetCallersAsync(
        string methodFqn,
        List<string> callerFqns,
        string solutionHash,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidates cache entries for methods in modified files.
    /// </summary>
    /// <param name="modifiedFilePaths">Paths of modified files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateByFilesAsync(
        List<string> modifiedFilePaths,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidates entire cache (e.g., after solution reload).
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    Task<CallGraphCacheStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Compacts and optimizes the cache database.
    /// </summary>
    Task CompactAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached callers WITH FULL LOCATION DATA (5-10x faster than SymbolFinder).
    /// Returns null on cache miss, List<SerializableCallerInfo> on cache hit.
    /// </summary>
    /// <param name="methodFqn">Fully qualified name of the method</param>
    /// <param name="solutionHash">Hash of the current solution state for cache validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of caller info with locations, or null if not cached</returns>
    Task<List<SerializableCallerInfo>?> GetCallersFullAsync(
        string methodFqn,
        string solutionHash,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Stores callers WITH FULL LOCATION DATA in cache.
    /// </summary>
    /// <param name="methodFqn">Fully qualified name of the method</param>
    /// <param name="callers">List of caller info with locations</param>
    /// <param name="solutionHash">Hash of the current solution state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetCallersFullAsync(
        string methodFqn,
        List<SerializableCallerInfo> callers,
        string solutionHash,
        CancellationToken cancellationToken = default
    );
}

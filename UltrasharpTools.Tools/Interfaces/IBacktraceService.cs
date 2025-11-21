using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for backtracing from a crash/failure point to potential entry points.
/// </summary>
public interface IBacktraceService
{
    /// <summary>
    /// Traces backwards from a crash point to find all possible call paths.
    /// </summary>
    /// <param name="crashPointFqn">Fully qualified name of the crash/failure point method</param>
    /// <param name="startPointFqn">Optional fully qualified name of the expected start point</param>
    /// <param name="stackTraceHints">Optional stack trace lines to guide path selection</param>
    /// <param name="maxDepth">Maximum depth to search backwards (default: 15)</param>
    /// <param name="maxPaths">Maximum number of paths to return (default: 5)</param>
    /// <param name="includeExternalCallers">Whether to include external library callers (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backtrace result with possible call paths</returns>
    Task<BacktraceResult> BacktraceFromCrashAsync(
        string crashPointFqn,
        string? startPointFqn = null,
        List<string>? stackTraceHints = null,
        int maxDepth = 15,
        int maxPaths = 5,
        bool includeExternalCallers = false,
        CancellationToken cancellationToken = default
    );
}

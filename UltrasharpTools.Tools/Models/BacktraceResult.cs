namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Represents a backtrace from a crash/failure point to potential entry points.
/// </summary>
public sealed class BacktraceResult
{
    public required string CrashPointFqn { get; init; }
    public string? StartPointFqn { get; init; }
    public List<string>? StackTraceHints { get; init; }
    public required List<CallPath> CallPaths { get; init; }
    public int TotalPaths => CallPaths.Count;
    public int MaxDepthReached { get; init; }
    public bool StartPointReached { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents one possible call path from crash point to entry point.
/// </summary>
public sealed class CallPath
{
    public required int PathId { get; init; }
    public required List<CallFrame> Frames { get; init; }
    public int Depth => Frames.Count;
    public bool ReachedEntryPoint { get; init; }
    public double Confidence { get; init; } // 0.0-1.0, higher = more likely based on stacktrace
}

/// <summary>
/// Represents a single frame in the call path (one method call).
/// </summary>
public sealed record CallFrame
{
    public required int FrameNumber { get; init; }
    public required string MethodFqn { get; init; }
    public required string Description { get; init; }
    public string? SourceLocation { get; init; }
    public List<VariableInfo>? Parameters { get; init; }
    public CallSiteInfo? CallSite { get; init; }
    public bool MatchesStackTrace { get; init; }
    public double StackTraceConfidence { get; init; } // 0.0-1.0, fuzzy match confidence
}

/// <summary>
/// Information about where this method was called from.
/// </summary>
public sealed record CallSiteInfo
{
    public required string CallingMethodFqn { get; init; }
    public string? SourceLocation { get; init; }
    public string? CallExpression { get; init; }
}

/// <summary>
/// Statistics for the call graph cache.
/// </summary>
public sealed class CallGraphCacheStats
{
    public int TotalEntries { get; init; }
    public int HitCount { get; init; }
    public int MissCount { get; init; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0.0;
    public int TotalRequests => HitCount + MissCount;
    public long CacheSizeBytes { get; init; }
    public DateTime? OldestEntryTimestamp { get; init; }
    public DateTime? NewestEntryTimestamp { get; init; }
}

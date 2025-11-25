namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Interface for tracking activity to manage power/efficiency modes.
/// Implementations should call RecordActivity() on each MCP request
/// to prevent the process from staying in idle/efficiency mode during work.
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Records activity to reset idle timer.
    /// Call this on each incoming request.
    /// </summary>
    void RecordActivity();
}

/// <summary>
/// Static provider for IActivityTracker.
/// Set Current at application startup to enable activity tracking.
/// </summary>
public static class ActivityTrackerProvider
{
    /// <summary>
    /// Current activity tracker instance.
    /// Set this at application startup.
    /// </summary>
    public static IActivityTracker? Current { get; set; }

    /// <summary>
    /// Records activity if tracker is configured.
    /// Safe to call even if no tracker is set.
    /// </summary>
    public static void RecordActivity()
    {
        Current?.RecordActivity();
    }
}

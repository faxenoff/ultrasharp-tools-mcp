namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Configuration options for automatic solution reload
/// </summary>
public class SolutionReloadOptions
{
    /// <summary>
    /// Enable automatic reload when .csproj or .sln files change
    /// </summary>
    public bool AutoReloadEnabled { get; set; } = false;

    /// <summary>
    /// Debounce delay in milliseconds before triggering reload
    /// Prevents multiple reloads when many files change at once
    /// </summary>
    public int DebounceDelayMs { get; set; } = 2000;

    /// <summary>
    /// Files to watch for automatic reload (.csproj, .sln by default)
    /// </summary>
    public string[] WatchedExtensions { get; set; } =
        new[] { ".csproj", ".sln", ".props", ".targets" };
}

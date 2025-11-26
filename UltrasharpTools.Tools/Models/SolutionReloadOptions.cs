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
    /// Files to watch for automatic reload (.csproj, .sln, .slnx by default)
    /// </summary>
    public string[] WatchedExtensions { get; set; } =
        new[] { ".csproj", ".sln", ".slnx", ".props", ".targets" };

    /// <summary>
    /// Enable automatic reload when git branch changes.
    /// Watches .git/HEAD file for changes and reloads solution when branch switches.
    /// Useful when project structure or references differ between branches.
    /// </summary>
    public bool WatchGitBranch { get; set; } = true;

    /// <summary>
    /// Run 'dotnet clean' before reloading solution after branch switch.
    /// Helps avoid stale obj/ cache issues when project references changed between branches.
    /// </summary>
    public bool CleanOnBranchSwitch { get; set; } = false;
}

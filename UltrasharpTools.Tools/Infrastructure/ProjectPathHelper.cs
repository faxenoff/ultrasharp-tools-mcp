namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Helper for resolving project-specific paths for cache and logs.
/// </summary>
public static class ProjectPathHelper
{
    private const string UltrasharpDirName = ".ultrasharp";

    /// <summary>
    /// Gets the .ultrasharp directory for the current solution.
    /// Creates it if it doesn't exist.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file, or null to use current directory</param>
    /// <returns>Path to .ultrasharp directory</returns>
    public static string GetProjectUltrasharpDir(string? solutionPath = null)
    {
        string projectRoot;

        if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
        {
            // Use solution directory
            projectRoot = Path.GetDirectoryName(solutionPath)!;
        }
        else
        {
            // Try to find solution or git root from current directory
            projectRoot =
                FindProjectRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
        }

        var ultrasharpDir = Path.Combine(projectRoot, UltrasharpDirName);
        Directory.CreateDirectory(ultrasharpDir);
        return ultrasharpDir;
    }

    /// <summary>
    /// Gets path for analysis cache within .ultrasharp directory.
    /// </summary>
    public static string GetAnalysisCachePath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var cachePath = Path.Combine(ultrasharpDir, "cache", "analysis");
        Directory.CreateDirectory(cachePath);
        return cachePath;
    }

    /// <summary>
    /// Gets path for call graph cache within .ultrasharp directory.
    /// </summary>
    public static string GetCallGraphCachePath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var cachePath = Path.Combine(ultrasharpDir, "cache", "callgraph");
        Directory.CreateDirectory(cachePath);
        return cachePath;
    }

    /// <summary>
    /// Gets path for symbol cache within .ultrasharp directory.
    /// </summary>
    public static string GetSymbolCachePath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var cachePath = Path.Combine(ultrasharpDir, "cache", "symbols");
        Directory.CreateDirectory(cachePath);
        return cachePath;
    }

    /// <summary>
    /// Gets path for logs within .ultrasharp directory.
    /// </summary>
    public static string GetLogsPath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var logsPath = Path.Combine(ultrasharpDir, "logs");
        Directory.CreateDirectory(logsPath);
        return logsPath;
    }

    /// <summary>
    /// Finds project root by looking for .sln, .git, or other markers.
    /// Prioritizes .sln and .git over project files to find the real root.
    /// </summary>
    private static string? FindProjectRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        string? fallbackProjectDir = null;

        while (current != null)
        {
            // Check for solution file (highest priority)
            if (current.GetFiles("*.sln").Length > 0)
            {
                return current.FullName;
            }

            // Check for .git directory (second priority)
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            // Remember first project directory as fallback, but keep searching up
            if (
                fallbackProjectDir == null
                && (
                    current.GetFiles("*.csproj").Length > 0
                    || current.GetFiles("package.json").Length > 0
                )
            )
            {
                fallbackProjectDir = current.FullName;
            }

            current = current.Parent;
        }

        // Return fallback if we found a project but no .sln or .git
        return fallbackProjectDir;
    }
}

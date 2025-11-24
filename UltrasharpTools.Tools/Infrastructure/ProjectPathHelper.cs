using System.Runtime.InteropServices;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Helper for resolving project-specific paths for cache and logs.
/// </summary>
public static class ProjectPathHelper
{
    /// <summary>
    /// Gets the central UltraSharpTools data directory.
    /// Windows: %LOCALAPPDATA%\UltraSharpTools
    /// Linux/macOS: ~/.ultrasharp
    /// </summary>
    /// <returns>Path to central data directory</returns>
    public static string GetProjectUltrasharpDir(string? solutionPath = null)
    {
        string baseDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: C:\Users\{User}\AppData\Local\UltraSharpTools
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            baseDir = Path.Combine(localAppData, "UltraSharpTools");
        }
        else
        {
            // Linux/macOS: ~/.ultrasharp
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, ".ultrasharp");
        }

        Directory.CreateDirectory(baseDir);
        return baseDir;
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
    /// Gets path for vector database within .ultrasharp directory.
    /// </summary>
    public static string GetVectorDatabasePath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var vectorDbPath = Path.Combine(ultrasharpDir, "vector-db");
        Directory.CreateDirectory(vectorDbPath);
        return vectorDbPath;
    }

    /// <summary>
    /// Gets path for database within .ultrasharp directory.
    /// </summary>
    public static string GetDatabasePath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var dbPath = Path.Combine(ultrasharpDir, "db");
        Directory.CreateDirectory(dbPath);
        return dbPath;
    }

    /// <summary>
    /// Gets path for configuration files within .ultrasharp directory.
    /// </summary>
    public static string GetConfigPath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var configPath = Path.Combine(ultrasharpDir, "config");
        Directory.CreateDirectory(configPath);
        return configPath;
    }

    /// <summary>
    /// Gets path for setup scripts within .ultrasharp directory.
    /// </summary>
    public static string GetScriptsPath(string? solutionPath = null)
    {
        var ultrasharpDir = GetProjectUltrasharpDir(solutionPath);
        var scriptsPath = Path.Combine(ultrasharpDir, "Scripts");
        Directory.CreateDirectory(scriptsPath);
        return scriptsPath;
    }
}

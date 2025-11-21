using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Metadata for FastSymbolIndex persistent cache
/// </summary>
public class SymbolCacheMetadata
{
    /// <summary>
    /// Version of the cache format (for breaking changes detection)
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Timestamp when cache was created
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// Full path to the solution file
    /// </summary>
    [JsonPropertyName("solutionPath")]
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of solution file content
    /// </summary>
    [JsonPropertyName("solutionHash")]
    public string SolutionHash { get; set; } = string.Empty;

    /// <summary>
    /// Project metadata for incremental rebuild
    /// </summary>
    [JsonPropertyName("projects")]
    public List<ProjectCacheInfo> Projects { get; set; } = new();

    /// <summary>
    /// Total number of symbols in cache
    /// </summary>
    [JsonPropertyName("symbolCount")]
    public int SymbolCount { get; set; }
}

/// <summary>
/// Cache information for a single project
/// </summary>
public class ProjectCacheInfo
{
    /// <summary>
    /// Project name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to .csproj file
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of .csproj file content
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Assembly name
    /// </summary>
    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Last write time of .csproj file (for quick staleness check)
    /// </summary>
    [JsonPropertyName("lastWriteTime")]
    public DateTimeOffset LastWriteTime { get; set; }
}

using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Serializable representation of SymbolCallerInfo for persistent cache
/// Contains calling symbol FQN and call site locations
/// </summary>
public sealed class SerializableCallerInfo
{
    /// <summary>
    /// Fully qualified name of the calling symbol (method/property)
    /// </summary>
    [JsonPropertyName("caller")]
    public string CallingSymbolFqn { get; set; } = string.Empty;

    /// <summary>
    /// Call site locations (where this symbol is called)
    /// Multiple locations for overloads or multiple call sites
    /// </summary>
    [JsonPropertyName("locations")]
    public List<SerializableLocation> CallSiteLocations { get; set; } = new();

    /// <summary>
    /// Whether calling symbol is direct call (false = indirect via delegate/lambda)
    /// </summary>
    [JsonPropertyName("isDirect")]
    public bool IsDirect { get; set; } = true;
}

/// <summary>
/// Serializable source location
/// </summary>
public sealed class SerializableLocation
{
    /// <summary>
    /// File path (relative to solution or absolute)
    /// </summary>
    [JsonPropertyName("file")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Start line number (0-indexed)
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// Start character position (0-indexed)
    /// </summary>
    [JsonPropertyName("startChar")]
    public int StartCharacter { get; set; }

    /// <summary>
    /// End line number (0-indexed)
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    /// <summary>
    /// End character position (0-indexed)
    /// </summary>
    [JsonPropertyName("endChar")]
    public int EndCharacter { get; set; }

    /// <summary>
    /// Source snippet (optional, for debugging)
    /// </summary>
    [JsonPropertyName("snippet")]
    public string? SourceSnippet { get; set; }
}

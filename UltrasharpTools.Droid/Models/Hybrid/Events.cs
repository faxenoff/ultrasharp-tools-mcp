using System.Text.Json.Serialization;

namespace UltrasharpTools.Droid.Models.Hybrid;

/// <summary>
/// Событие изменения файла
/// </summary>
public sealed class FileChangedEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "file_changed";

    [JsonPropertyName("project")]
    public required string Project { get; init; }

    [JsonPropertyName("branch")]
    public required string Branch { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; } // "modified", "created", "deleted"

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("vectors")]
    public float[]? Vectors { get; init; }

    [JsonPropertyName("symbols")]
    public SymbolInfo[]? Symbols { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Событие переключения ветки
/// </summary>
public sealed class BranchSwitchEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "branch_switched";

    [JsonPropertyName("project")]
    public required string Project { get; init; }

    [JsonPropertyName("fromBranch")]
    public required string FromBranch { get; init; }

    [JsonPropertyName("toBranch")]
    public required string ToBranch { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Событие Git коммита
/// </summary>
public sealed class GitCommitEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "git_commit";

    [JsonPropertyName("project")]
    public required string Project { get; init; }

    [JsonPropertyName("branch")]
    public required string Branch { get; init; }

    [JsonPropertyName("commitSha")]
    public required string CommitSha { get; init; }

    [JsonPropertyName("filesChanged")]
    public required string[] FilesChanged { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Информация о символе кода
/// </summary>
public sealed class SymbolInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; } // "class", "method", "property", etc.

    [JsonPropertyName("line")]
    public int Line { get; init; }
}

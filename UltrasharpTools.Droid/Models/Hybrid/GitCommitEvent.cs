namespace UltrasharpTools.Agent.Models;

/// <summary>
/// Событие коммита в Git
/// </summary>
public sealed class GitCommitEvent
{
    public string Type { get; init; } = "git_commit";
    public required string Project { get; init; }
    public required string Branch { get; init; }
    public required string CommitSha { get; init; }
    public string[] FilesChanged { get; init; } = Array.Empty<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

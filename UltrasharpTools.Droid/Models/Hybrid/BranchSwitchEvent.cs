namespace UltrasharpTools.Agent.Models;

/// <summary>
/// Событие переключения ветки Git
/// </summary>
public sealed class BranchSwitchEvent
{
    public string Type { get; init; } = "branch_switched";
    public required string Project { get; init; }
    public required string FromBranch { get; init; }
    public required string ToBranch { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

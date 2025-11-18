namespace UltrasharpTools.Overlord.Models.Agent;

/// <summary>
/// DTO для события переключения ветки от Agent
/// </summary>
public sealed class BranchSwitchEventDto
{
    public string Type { get; init; } = "branch_switched";
    public required string Project { get; init; }
    public required string FromBranch { get; init; }
    public required string ToBranch { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

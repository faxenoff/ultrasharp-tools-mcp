namespace UltrasharpTools.Tools.Models;

public class GitOptions
{
    /// <summary>
    /// Keep only the N most recent sharptools/* branches (null = no limit)
    /// </summary>
    public int? RetentionCount { get; set; } = 10;

    /// <summary>
    /// Keep branches created within the last N days (null = no limit)
    /// </summary>
    public int? RetentionDays { get; set; } = null;

    /// <summary>
    /// Automatically cleanup old branches after each modification
    /// </summary>
    public bool AutoCleanup { get; set; } = true;
}

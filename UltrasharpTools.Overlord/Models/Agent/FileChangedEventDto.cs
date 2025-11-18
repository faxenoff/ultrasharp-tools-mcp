namespace UltrasharpTools.Overlord.Models.Agent;

/// <summary>
/// DTO для события изменения файла от Agent
/// </summary>
public sealed class FileChangedEventDto
{
    public string Type { get; init; } = "file_changed";
    public required string Project { get; init; }
    public required string Branch { get; init; }
    public required string File { get; init; }
    public required string Action { get; init; } // "created", "modified", "deleted"
    public string? Content { get; init; }
    public float[]? Vectors { get; init; }
    public SymbolInfoDto[]? Symbols { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class SymbolInfoDto
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public int Line { get; init; }
}

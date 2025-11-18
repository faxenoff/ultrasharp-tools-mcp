namespace UltrasharpTools.Agent.Models;

/// <summary>
/// Событие изменения файла
/// </summary>
public sealed class FileChangedEvent
{
    public string Type { get; init; } = "file_changed";
    public required string Project { get; init; }
    public required string Branch { get; init; }
    public required string File { get; init; }
    public required string Action { get; init; } // "created", "modified", "deleted"
    public string? Content { get; init; }
    public float[]? Vectors { get; init; }
    public SymbolInfo[]? Symbols { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Информация о символе (метод, класс, и т.д.)
/// </summary>
public sealed class SymbolInfo
{
    public required string Name { get; init; }
    public required string Kind { get; init; } // "method", "class", "interface", etc.
    public int Line { get; init; }
}

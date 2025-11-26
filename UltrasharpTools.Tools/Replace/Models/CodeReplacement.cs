namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Изменение для apply операции.
/// </summary>
public sealed record CodeReplacement
{
    /// <summary>ID из CodeMatch</summary>
    public required string MatchId { get; init; }

    /// <summary>Новый код (полный код контейнера)</summary>
    public required string NewCode { get; init; }

    /// <summary>Опциональное описание изменения</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Успешно применённое изменение.
/// </summary>
public sealed record AppliedChange
{
    /// <summary>ID match</summary>
    public required string MatchId { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Старый код</summary>
    public required string OldCode { get; init; }

    /// <summary>Новый код</summary>
    public required string NewCode { get; init; }

    /// <summary>Описание изменения</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Неудачное изменение.
/// </summary>
public sealed record FailedChange
{
    /// <summary>ID match</summary>
    public required string MatchId { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Причина ошибки</summary>
    public required string Error { get; init; }
}

/// <summary>
/// Конфликт изменений (несколько replacements на один контейнер).
/// </summary>
public sealed record ReplaceConflict
{
    /// <summary>ID конфликтующих matches</summary>
    public required IReadOnlyList<string> MatchIds { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>FQN контейнера</summary>
    public required string ContainerFqn { get; init; }

    /// <summary>Описание конфликта</summary>
    public required string Message { get; init; }
}

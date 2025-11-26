namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Результат preview операции.
/// </summary>
public sealed record SemanticReplacePreviewResult
{
    /// <summary>Найденные matches с контекстом</summary>
    public required IReadOnlyList<CodeMatch> Matches { get; init; }

    /// <summary>Общее количество matches (может быть больше чем Matches.Count при pagination)</summary>
    public required int TotalMatches { get; init; }

    /// <summary>Есть ещё результаты (pagination)</summary>
    public bool HasMore => Matches.Count < TotalMatches;

    /// <summary>Offset для следующей страницы</summary>
    public int NextOffset { get; init; }
}

/// <summary>
/// Результат apply операции.
/// </summary>
public sealed record ReplaceResult
{
    /// <summary>Успешность операции</summary>
    public required bool Success { get; init; }

    /// <summary>Сообщение об ошибке (если !Success)</summary>
    public string? Error { get; init; }

    /// <summary>Количество применённых изменений</summary>
    public required int Applied { get; init; }

    /// <summary>Количество неудачных изменений</summary>
    public required int Failed { get; init; }

    /// <summary>Количество пропущенных изменений</summary>
    public int Skipped { get; init; }

    /// <summary>Применённые изменения</summary>
    public required IReadOnlyList<AppliedChange> Changes { get; init; }

    /// <summary>Неудачные изменения</summary>
    public required IReadOnlyList<FailedChange> Failures { get; init; }

    /// <summary>Конфликты (если есть)</summary>
    public IReadOnlyList<ReplaceConflict>? Conflicts { get; init; }

    /// <summary>Ошибки компиляции (если есть)</summary>
    public IReadOnlyList<string>? CompilationErrors { get; init; }

    // Factory methods

    public static ReplaceResult SuccessResult(
        int applied,
        IReadOnlyList<AppliedChange> changes,
        IReadOnlyList<FailedChange>? failures = null) => new()
    {
        Success = true,
        Applied = applied,
        Failed = failures?.Count ?? 0,
        Changes = changes,
        Failures = failures ?? []
    };

    public static ReplaceResult FailedResult(
        string error,
        IReadOnlyList<AppliedChange>? appliedBeforeFailure = null,
        IReadOnlyList<FailedChange>? failures = null) => new()
    {
        Success = false,
        Error = error,
        Applied = appliedBeforeFailure?.Count ?? 0,
        Failed = failures?.Count ?? 0,
        Changes = appliedBeforeFailure ?? [],
        Failures = failures ?? []
    };

    public static ReplaceResult ValidationFailed(IReadOnlyList<string> errors) => new()
    {
        Success = false,
        Error = $"Validation failed: {string.Join("; ", errors)}",
        Applied = 0,
        Failed = 0,
        Changes = [],
        Failures = []
    };

    public static ReplaceResult ConflictDetected(IReadOnlyList<ReplaceConflict> conflicts) => new()
    {
        Success = false,
        Error = "Conflicting replacements detected",
        Applied = 0,
        Failed = 0,
        Changes = [],
        Failures = [],
        Conflicts = conflicts
    };

    public static ReplaceResult CompilationFailed(
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<AppliedChange>? appliedBeforeRollback = null) => new()
    {
        Success = false,
        Error = "Compilation errors after changes (rolled back)",
        Applied = 0,
        Failed = appliedBeforeRollback?.Count ?? 0,
        Changes = [],
        Failures = [],
        CompilationErrors = diagnostics
    };
}

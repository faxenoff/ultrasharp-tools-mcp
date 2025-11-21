namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Результат семантического мерджа.
/// </summary>
public sealed record MergeResult
{
    /// <summary>Список действий для применения мерджа</summary>
    public required List<MergeAction> Actions { get; init; }

    /// <summary>Список конфликтов (требуют ручного разрешения)</summary>
    public required List<SemanticConflict> Conflicts { get; init; }

    /// <summary>Статистика мерджа</summary>
    public required MergeStatistics Statistics { get; init; }

    /// <summary>Успешность мерджа</summary>
    public bool IsSuccess => Conflicts.Count == 0;
}

/// <summary>
/// Действие для применения в мердже.
/// </summary>
public sealed record MergeAction
{
    /// <summary>Целевой путь файла</summary>
    public required string TargetPath { get; init; }

    /// <summary>Тип действия</summary>
    public required MergeActionType Type { get; init; }

    /// <summary>Контент для записи</summary>
    public required string Content { get; init; }

    /// <summary>Намерение изменения</summary>
    public required ChangeIntent Intent { get; init; }

    /// <summary>Уверенность в корректности (0.0-1.0)</summary>
    public required float Confidence { get; init; }

    /// <summary>Источник изменения (branchA, branchB, merged)</summary>
    public required string Source { get; init; }
}

/// <summary>
/// Тип действия мерджа.
/// </summary>
public enum MergeActionType
{
    Create,         // Создать новый файл
    Update,         // Обновить существующий
    Delete,         // Удалить файл
    Move,           // Переместить файл
    Rename          // Переименовать файл
}

/// <summary>
/// Статистика мерджа.
/// </summary>
public sealed record MergeStatistics
{
    /// <summary>Общее количество changes</summary>
    public required int TotalChanges { get; init; }

    /// <summary>Auto-merged changes</summary>
    public required int AutoMergedChanges { get; init; }

    /// <summary>Конфликты</summary>
    public required int ConflictCount { get; init; }

    /// <summary>Fast Path matches</summary>
    public required int FastPathMatches { get; init; }

    /// <summary>Slow Path matches (semantic)</summary>
    public required int SlowPathMatches { get; init; }

    /// <summary>Время выполнения мерджа (мс)</summary>
    public required long MergeTimeMs { get; init; }
}

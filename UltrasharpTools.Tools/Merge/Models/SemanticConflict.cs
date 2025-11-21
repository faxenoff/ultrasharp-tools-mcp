namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Семантический конфликт при мердже.
/// </summary>
public sealed record SemanticConflict
{
    /// <summary>Уникальный ID конфликта</summary>
    public required string Id { get; init; }

    /// <summary>Базовая версия unit (common ancestor)</summary>
    public required CodeUnit BaseUnit { get; init; }

    /// <summary>Версия из ветки A</summary>
    public required CodeUnit VersionA { get; init; }

    /// <summary>Версия из ветки B</summary>
    public required CodeUnit VersionB { get; init; }

    /// <summary>Тип конфликта</summary>
    public required ConflictType ConflictType { get; init; }

    /// <summary>Описание конфликта</summary>
    public required string Description { get; init; }

    /// <summary>Предложенные варианты разрешения</summary>
    public required List<ConflictResolution> SuggestedResolutions { get; init; }

    /// <summary>Severity (насколько критичен)</summary>
    public required ConflictSeverity Severity { get; init; }
}

/// <summary>
/// Тип семантического конфликта.
/// </summary>
public enum ConflictType
{
    LogicConflict,      // Разная логика в одном методе
    APIConflict,        // Несовместимые изменения сигнатуры
    NamingConflict,     // Разные переименования
    MovementConflict,   // Перемещение в разные места
    StructuralConflict, // Несовместимые структурные изменения
    ContentConflict     // Общий content conflict
}

/// <summary>
/// Severity конфликта.
/// </summary>
public enum ConflictSeverity
{
    Low,        // Легко разрешить
    Medium,     // Требует внимания
    High,       // Критический конфликт
    Critical    // Невозможно auto-resolve
}

/// <summary>
/// Предложенное разрешение конфликта.
/// </summary>
public sealed record ConflictResolution
{
    /// <summary>Тип разрешения</summary>
    public required ResolutionType Type { get; init; }

    /// <summary>Описание</summary>
    public required string Description { get; init; }

    /// <summary>Результирующий контент</summary>
    public required string ResolvedContent { get; init; }

    /// <summary>Приоритет (1 = лучший вариант)</summary>
    public required int Priority { get; init; }

    /// <summary>Уверенность AI (0.0-1.0)</summary>
    public required float Confidence { get; init; }
}

/// <summary>
/// Тип разрешения конфликта.
/// </summary>
public enum ResolutionType
{
    UseA,               // Использовать версию A
    UseB,               // Использовать версию B
    CombineBoth,        // Объединить обе версии
    ManualMerge,        // Ручной мердж
    UseBase,            // Вернуться к base версии
    CreateBoth          // Создать обе версии (если возможно)
}

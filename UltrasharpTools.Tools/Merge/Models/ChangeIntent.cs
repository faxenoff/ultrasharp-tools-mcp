namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Намерение изменения кода (что и зачем изменилось).
/// </summary>
public sealed record ChangeIntent
{
    /// <summary>Тип намерения</summary>
    public required IntentType Type { get; init; }

    /// <summary>Описание намерения</summary>
    public required string Description { get; init; }

    /// <summary>Затронутые символы (FQN)</summary>
    public required List<string> AffectedSymbols { get; init; }

    /// <summary>Уверенность в классификации (0.0-1.0)</summary>
    public required float Confidence { get; init; }
}

/// <summary>
/// Тип намерения изменения.
/// </summary>
public enum IntentType
{
    Unknown,            // Неизвестно
    BugFix,             // Исправление бага
    Refactoring,        // Рефакторинг (не меняет поведение)
    FeatureAddition,    // Новая функциональность
    PerformanceOpt,     // Оптимизация производительности
    CodeCleanup,        // Форматирование, комментарии
    APIChange,          // Изменение API (signatures)
    Modification        // Общая модификация
}

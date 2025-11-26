using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Interfaces;

/// <summary>
/// Сервис для атомарного применения batch изменений.
/// </summary>
public interface IBatchReplacerService
{
    /// <summary>
    /// Зарегистрировать matches для tracking между preview и apply.
    /// </summary>
    /// <param name="matches">Matches из preview</param>
    void RegisterMatches(IEnumerable<CodeMatch> matches);

    /// <summary>
    /// Получить зарегистрированный match по ID.
    /// </summary>
    /// <param name="matchId">ID match</param>
    /// <returns>CodeMatch или null</returns>
    CodeMatch? GetMatch(string matchId);

    /// <summary>
    /// Применить batch изменений.
    /// </summary>
    /// <param name="replacements">Список изменений</param>
    /// <param name="mode">Режим применения (AllOrNothing или BestEffort)</param>
    /// <param name="commitMessage">Сообщение для Git commit (опционально)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Результат применения</returns>
    Task<ReplaceResult> ApplyReplacementsAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode = ApplyMode.AllOrNothing,
        string? commitMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Валидировать replacements перед применением.
    /// </summary>
    /// <param name="replacements">Список изменений</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Результат валидации</returns>
    Task<ValidationResult> ValidateReplacementsAsync(
        IReadOnlyList<CodeReplacement> replacements,
        CancellationToken ct = default);

    /// <summary>
    /// Очистить registry (после сессии).
    /// </summary>
    void ClearRegistry();
}

/// <summary>
/// Результат валидации replacements.
/// </summary>
public sealed record ValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }

    public static ValidationResult Valid() => new()
    {
        IsValid = true,
        Errors = []
    };

    public static ValidationResult Invalid(IReadOnlyList<string> errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

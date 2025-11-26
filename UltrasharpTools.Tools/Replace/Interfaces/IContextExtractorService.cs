using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Interfaces;

/// <summary>
/// Сервис для извлечения полного контекста кода из PatternMatch.
/// </summary>
public interface IContextExtractorService
{
    /// <summary>
    /// Извлечь контекст для одного match.
    /// </summary>
    /// <param name="match">Результат поиска паттерна</param>
    /// <param name="scope">Уровень контекста</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>CodeMatch с полным контекстом</returns>
    Task<CodeMatch> ExtractContextAsync(
        PatternMatch match,
        ReplaceScope scope,
        CancellationToken ct = default);

    /// <summary>
    /// Извлечь контекст для нескольких matches (batch).
    /// </summary>
    /// <param name="matches">Список matches</param>
    /// <param name="scope">Уровень контекста</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Список CodeMatch с полным контекстом</returns>
    Task<IReadOnlyList<CodeMatch>> ExtractContextBatchAsync(
        IReadOnlyList<PatternMatch> matches,
        ReplaceScope scope,
        CancellationToken ct = default);
}

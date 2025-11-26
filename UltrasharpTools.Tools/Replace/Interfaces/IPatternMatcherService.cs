using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Interfaces;

/// <summary>
/// Сервис для поиска паттернов в коде.
/// Поддерживает три режима: Regex, Roslyn (FQN), Semantic (embeddings).
/// </summary>
public interface IPatternMatcherService
{
    /// <summary>
    /// Найти все вхождения паттерна.
    /// </summary>
    /// <param name="pattern">Паттерн для поиска (regex, FQN, или natural language)</param>
    /// <param name="mode">Режим поиска</param>
    /// <param name="filePattern">Glob фильтр файлов (опционально)</param>
    /// <param name="namespaceFilter">Фильтр по namespace (опционально)</param>
    /// <param name="limit">Максимум результатов</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Список найденных matches</returns>
    Task<IReadOnlyList<PatternMatch>> FindMatchesAsync(
        string pattern,
        SearchMode mode,
        string? filePattern = null,
        string? namespaceFilter = null,
        int limit = 100,
        CancellationToken ct = default);
}

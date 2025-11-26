using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Interfaces;

/// <summary>
/// Главный сервис для semantic replace операций.
/// Объединяет поиск, извлечение контекста и применение изменений.
/// </summary>
public interface ISemanticReplaceService
{
    /// <summary>
    /// Preview: Найти все вхождения паттерна с полным контекстом.
    /// </summary>
    /// <param name="pattern">Паттерн для поиска</param>
    /// <param name="searchMode">Режим поиска</param>
    /// <param name="scope">Уровень контекста</param>
    /// <param name="filePattern">Glob фильтр файлов</param>
    /// <param name="namespaceFilter">Фильтр по namespace</param>
    /// <param name="limit">Максимум результатов</param>
    /// <param name="offset">Offset для pagination</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Preview результат с matches</returns>
    Task<SemanticReplacePreviewResult> PreviewAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string? filePattern = null,
        string? namespaceFilter = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Apply: Применить batch изменений.
    /// </summary>
    /// <param name="replacements">Список изменений</param>
    /// <param name="mode">Режим применения</param>
    /// <param name="commitMessage">Сообщение для Git commit</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Результат применения</returns>
    Task<ReplaceResult> ApplyAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode = ApplyMode.AllOrNothing,
        string? commitMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transform: Применить семантическую трансформацию через LLM.
    /// </summary>
    /// <param name="pattern">Паттерн для поиска</param>
    /// <param name="searchMode">Режим поиска</param>
    /// <param name="scope">Уровень контекста</param>
    /// <param name="transformation">Описание трансформации</param>
    /// <param name="filePattern">Glob фильтр файлов</param>
    /// <param name="namespaceFilter">Фильтр по namespace</param>
    /// <param name="commitMessage">Сообщение для Git commit</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Результат применения</returns>
    Task<ReplaceResult> TransformAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string transformation,
        string? filePattern = null,
        string? namespaceFilter = null,
        string? commitMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Проверить доступность semantic transform (требует LLM).
    /// </summary>
    bool IsSemanticTransformAvailable { get; }
}

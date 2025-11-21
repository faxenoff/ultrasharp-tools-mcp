namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для применения автоматических исправлений кода
/// </summary>
public interface ICodeFixService
{
    /// <summary>
    /// Применяет автоматические исправления для указанных диагностик
    /// </summary>
    /// <param name="solutionPath">Путь к .sln файлу</param>
    /// <param name="diagnosticId">ID диагностики для исправления или 'all'</param>
    /// <param name="preview">Только предпросмотр без применения изменений</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат с информацией о примененных исправлениях</returns>
    Task<CodeFixResult> ApplyFixesAsync(
        string solutionPath,
        string diagnosticId,
        bool preview,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Результат применения code fixes
/// </summary>
public record CodeFixResult
{
    /// <summary>
    /// Описания примененных исправлений
    /// </summary>
    public required List<string> AppliedFixes { get; init; }

    /// <summary>
    /// Всего найдено исправляемых проблем
    /// </summary>
    public required int TotalFixableIssues { get; init; }

    /// <summary>
    /// Ошибки при применении исправлений
    /// </summary>
    public List<(string Location, string Error)> Errors { get; init; } = [];

    /// <summary>
    /// Был ли это preview режим
    /// </summary>
    public required bool WasPreview { get; init; }
}

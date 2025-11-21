

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для быстрого линтинга измененных файлов
/// </summary>
public interface IQuickLintService
{
    /// <summary>
    /// Быстрый линтинг указанных файлов (только Errors и Warnings)
    /// </summary>
    /// <param name="solutionPath">Путь к solution</param>
    /// <param name="filePaths">Пути к файлам для проверки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат быстрого линтинга</returns>
    Task<QuickLintResult> LintFilesAsync(
        string solutionPath,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Результат быстрого линтинга
/// </summary>
public record QuickLintResult
{
    /// <summary>
    /// Количество ошибок
    /// </summary>
    public required int ErrorCount { get; init; }

    /// <summary>
    /// Количество предупреждений
    /// </summary>
    public required int WarningCount { get; init; }

    /// <summary>
    /// Всего проблем
    /// </summary>
    public int TotalIssues => ErrorCount + WarningCount;

    /// <summary>
    /// Детали проблем (первые 10)
    /// </summary>
    public List<QuickLintIssue> TopIssues { get; init; } = [];
}

/// <summary>
/// Одна проблема линтинга
/// </summary>
public record QuickLintIssue
{
    public required string Id { get; init; }
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public required int Line { get; init; }
}

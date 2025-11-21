namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для анализа кода через Roslyn analyzers
/// </summary>
public interface IDiagnosticService
{
    /// <summary>
    /// Анализирует код решения и возвращает диагностики
    /// </summary>
    /// <param name="solutionPath">Путь к .sln файлу</param>
    /// <param name="severityFilter">Минимальный уровень серьезности</param>
    /// <param name="skip">Количество пропускаемых результатов для пагинации</param>
    /// <param name="take">Количество возвращаемых результатов</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат анализа с диагностиками</returns>
    Task<DiagnosticAnalysisResult> AnalyzeAsync(
        string solutionPath,
        DiagnosticSeverity severityFilter,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Результат анализа диагностик
/// </summary>
public record DiagnosticAnalysisResult
{
    /// <summary>
    /// Диагностики с путями к файлам
    /// </summary>
    public required List<(Diagnostic Diagnostic, string FilePath)> Diagnostics { get; init; }

    /// <summary>
    /// Всего найдено диагностик (до пагинации)
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Есть ли еще результаты
    /// </summary>
    public required bool HasMore { get; init; }
}

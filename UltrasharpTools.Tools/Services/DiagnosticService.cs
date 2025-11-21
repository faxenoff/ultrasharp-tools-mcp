using System.Collections.Concurrent;
using System.Collections.Immutable;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для анализа кода через Roslyn analyzers
/// </summary>
public class DiagnosticService(ILogger<DiagnosticService> logger, ISolutionManager solutionManager)
    : IDiagnosticService
{
    private readonly ILogger<DiagnosticService> _logger = logger;
    private readonly ISolutionManager _solutionManager = solutionManager;

    // Кеш результатов анализа: ключ = solution path, значение = (timestamp, diagnostics)
    private readonly ConcurrentDictionary<
        string,
        (DateTime Timestamp, List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> Diagnostics)
    > _cache = new();

    // Время жизни кеша - 5 минут
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public async Task<DiagnosticAnalysisResult> AnalyzeAsync(
        string solutionPath,
        DiagnosticFilterOptions filterOptions,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Starting diagnostic analysis for solution: {SolutionPath}, Preset: {Preset}, Ids: {Ids}",
            solutionPath,
            filterOptions.PresetName ?? "none",
            filterOptions.DiagnosticIds != null
                ? string.Join(", ", filterOptions.DiagnosticIds.Take(5))
                : "all"
        );

        // Получаем все диагностики (с кешированием)
        var allDiagnostics = await GetAllDiagnosticsAsync(solutionPath, cancellationToken);

        // Применяем фильтры
        var filteredDiagnostics = allDiagnostics
            .Where(item =>
                filterOptions.ShouldIncludeDiagnostic(
                    item.Diagnostic,
                    item.FilePath,
                    item.ProjectName
                )
            )
            .OrderByDescending(d => d.Diagnostic.Severity)
            .ThenBy(d => d.FilePath)
            .ThenBy(d => d.Diagnostic.Id)
            .ToList();

        var totalCount = filteredDiagnostics.Count;

        // Применяем пагинацию
        var paginatedDiagnostics = filteredDiagnostics
            .Skip(filterOptions.Skip)
            .Take(filterOptions.Take)
            .Select(item => (item.Diagnostic, item.FilePath))
            .ToList();

        _logger.LogInformation(
            "Diagnostic analysis complete. Total: {Total}, Filtered: {Filtered}, Returned: {Returned}",
            allDiagnostics.Count,
            totalCount,
            paginatedDiagnostics.Count
        );

        return new DiagnosticAnalysisResult
        {
            Diagnostics = paginatedDiagnostics,
            TotalCount = totalCount,
            HasMore = filterOptions.Skip + filterOptions.Take < totalCount,
        };
    }

    // Legacy метод для обратной совместимости
    public Task<DiagnosticAnalysisResult> AnalyzeAsync(
        string solutionPath,
        DiagnosticSeverity severityFilter,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        var filterOptions = new DiagnosticFilterOptions
        {
            SeverityFilter = severityFilter,
            Skip = skip,
            Take = take,
        };

        return AnalyzeAsync(solutionPath, filterOptions, cancellationToken);
    }

    public void ClearCache()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger.LogInformation("Diagnostic cache cleared. Removed {Count} entries", count);
    }

    /// <summary>
    /// Получает все диагностики с кешированием
    /// </summary>
    private async Task<
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)>
    > GetAllDiagnosticsAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(solutionPath);

        // Проверяем кеш
        if (_cache.TryGetValue(normalizedPath, out var cached))
        {
            var age = DateTime.UtcNow - cached.Timestamp;

            if (age < CacheExpiration)
            {
                _logger.LogInformation(
                    "Using cached diagnostics for {SolutionPath} (age: {Age:F1}s)",
                    solutionPath,
                    age.TotalSeconds
                );
                return cached.Diagnostics;
            }

            _logger.LogInformation(
                "Cache expired for {SolutionPath} (age: {Age:F1}s)",
                solutionPath,
                age.TotalSeconds
            );
        }

        // Загружаем solution
        await _solutionManager.LoadSolutionAsync(solutionPath, cancellationToken);
        var solution = _solutionManager.CurrentSolution!;

        // Анализируем все проекты параллельно
        var diagnosticTasks = solution
            .Projects.Where(p => p.SupportsCompilation)
            .Select(async project => await AnalyzeProjectAsync(project, cancellationToken));

        var projectDiagnostics = await Task.WhenAll(diagnosticTasks);

        // Объединяем результаты
        var allDiagnostics = projectDiagnostics.SelectMany(x => x).ToList();

        // Сохраняем в кеш
        _cache[normalizedPath] = (DateTime.UtcNow, allDiagnostics);

        _logger.LogInformation(
            "Cached {Count} diagnostics for {SolutionPath}",
            allDiagnostics.Count,
            solutionPath
        );

        return allDiagnostics;
    }

    /// <summary>
    /// Анализирует один проект
    /// </summary>
    private async Task<
        IEnumerable<(Diagnostic Diagnostic, string FilePath, string ProjectName)>
    > AnalyzeProjectAsync(Project project, CancellationToken cancellationToken)
    {
        try
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                return [];

            IEnumerable<Diagnostic> diagnostics;

            // Пытаемся запустить анализаторы
            var analyzers = project
                .AnalyzerReferences.SelectMany(r => r.GetAnalyzers(project.Language))
                .ToImmutableArray();

            if (analyzers.Length > 0)
            {
                try
                {
                    var compilationWithAnalyzers = compilation.WithAnalyzers(
                        analyzers,
                        options: null
                    );

                    diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(
                        cancellationToken
                    );
                }
                catch
                {
                    // Fallback к базовым диагностикам компиляции
                    diagnostics = compilation.GetDiagnostics();
                }
            }
            else
            {
                // Нет анализаторов, используем базовые диагностики компиляции
                diagnostics = compilation.GetDiagnostics();
            }

            // Возвращаем диагностики с метаданными
            return diagnostics
                .Where(d => !d.IsSuppressed)
                .Select(d =>
                {
                    var filePath = d.Location.SourceTree?.FilePath ?? "Unknown";
                    return (d, filePath, project.Name);
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze project: {ProjectName}", project.Name);
            return [];
        }
    }
}

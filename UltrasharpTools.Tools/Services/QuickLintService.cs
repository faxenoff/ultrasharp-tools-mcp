using System.Collections.Immutable;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для быстрого линтинга измененных файлов
/// </summary>
public class QuickLintService(ILogger<QuickLintService> logger, ISolutionManager solutionManager)
    : IQuickLintService
{
    private readonly ILogger<QuickLintService> _logger = logger;
    private readonly ISolutionManager _solutionManager = solutionManager;

    public async Task<QuickLintResult> LintFilesAsync(
        string solutionPath,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Starting quick lint for {Count} files", filePaths.Count());

        if (!filePaths.Any())
        {
            return new QuickLintResult
            {
                ErrorCount = 0,
                WarningCount = 0,
                TopIssues = [],
            };
        }

        try
        {
            // Нормализуем пути для сравнения
            var normalizedPaths = filePaths
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Получаем current solution (должна быть уже загружена)
            var solution = _solutionManager.CurrentSolution;
            if (solution == null)
            {
                _logger.LogWarning("Solution not loaded, cannot perform quick lint");
                return new QuickLintResult
                {
                    ErrorCount = 0,
                    WarningCount = 0,
                    TopIssues = [],
                };
            }

            // ✅ OPTIMIZATION: Parallel processing файлов
            var diagnosticsBag = new ConcurrentBag<Diagnostic>();

            var tasks = solution
                .Projects.Where(p => p.SupportsCompilation)
                .Select(async project =>
                {
                    try
                    {
                        // Фильтруем только измененные документы этого проекта
                        var relevantDocuments = project
                            .Documents.Where(d =>
                                d.FilePath != null
                                && normalizedPaths.Contains(Path.GetFullPath(d.FilePath))
                            )
                            .ToList();

                        if (!relevantDocuments.Any())
                            return;

                        var compilation = await project.GetCompilationAsync(cancellationToken);
                        if (compilation == null)
                            return;

                        // Получаем analyzers
                        var analyzers = project
                            .AnalyzerReferences.SelectMany(r => r.GetAnalyzers(project.Language))
                            .ToImmutableArray();

                        IEnumerable<Diagnostic> projectDiagnostics;

                        if (analyzers.Length > 0)
                        {
                            try
                            {
                                var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

                                projectDiagnostics =
                                    await compilationWithAnalyzers.GetAllDiagnosticsAsync(
                                        cancellationToken
                                    );
                            }
                            catch
                            {
                                // Fallback на базовую компиляцию
                                projectDiagnostics = compilation.GetDiagnostics();
                            }
                        }
                        else
                        {
                            projectDiagnostics = compilation.GetDiagnostics();
                        }

                        // Фильтруем только Error и Warning в измененных файлах
                        var filteredDiagnostics = projectDiagnostics.Where(d =>
                            !d.IsSuppressed
                            && (
                                d.Severity == DiagnosticSeverity.Error
                                || d.Severity == DiagnosticSeverity.Warning
                            )
                            && d.Location.SourceTree?.FilePath != null
                            && normalizedPaths.Contains(
                                Path.GetFullPath(d.Location.SourceTree.FilePath)
                            )
                        );

                        foreach (var diagnostic in filteredDiagnostics)
                        {
                            diagnosticsBag.Add(diagnostic);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to lint project: {ProjectName}",
                            project.Name
                        );
                    }
                });

            await Task.WhenAll(tasks);

            var allDiagnostics = diagnosticsBag.ToList();

            var errorCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warningCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

            // Берем топ-10 проблем (сначала errors, потом warnings)
            var topIssues = allDiagnostics
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Location.SourceTree?.FilePath)
                .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
                .Take(10)
                .Select(d => new QuickLintIssue
                {
                    Id = d.Id,
                    Severity = d.Severity,
                    Message = d.GetMessage(),
                    FilePath = d.Location.SourceTree?.FilePath ?? "Unknown",
                    Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                })
                .ToList();

            _logger.LogDebug(
                "Quick lint completed: {Errors} errors, {Warnings} warnings",
                errorCount,
                warningCount
            );

            return new QuickLintResult
            {
                ErrorCount = errorCount,
                WarningCount = warningCount,
                TopIssues = topIssues,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick lint failed");
            return new QuickLintResult
            {
                ErrorCount = 0,
                WarningCount = 0,
                TopIssues = [],
            };
        }
    }
}

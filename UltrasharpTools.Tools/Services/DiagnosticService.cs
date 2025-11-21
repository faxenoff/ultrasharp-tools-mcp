

using System.Collections.Immutable;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для анализа кода через Roslyn analyzers
/// </summary>
public class DiagnosticService(
ILogger<DiagnosticService> logger,
ISolutionManager solutionManager
) : IDiagnosticService
{
    private readonly ILogger<DiagnosticService> _logger = logger;
    private readonly ISolutionManager _solutionManager = solutionManager;

    public async Task<DiagnosticAnalysisResult> AnalyzeAsync(
    string solutionPath,
    DiagnosticSeverity severityFilter,
    int skip,
    int take,
    CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
        "Starting diagnostic analysis for solution: {SolutionPath}, SeverityFilter: {SeverityFilter}",
        solutionPath,
        severityFilter
        );

        await _solutionManager.LoadSolutionAsync(solutionPath, cancellationToken);
        var solution = _solutionManager.CurrentSolution!;

        // ✅ OPTIMIZATION: Parallel project processing with Task.WhenAll
        var diagnosticTasks = solution
        .Projects.Where(p => p.SupportsCompilation)
        .Select(async project =>
        {
            try
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                    return Enumerable.Empty<(Diagnostic, string)>();

                IEnumerable<Diagnostic> diagnostics;

                // Try to get analyzers and run them
                var analyzers = project.AnalyzerReferences
        .SelectMany(r => r.GetAnalyzers(project.Language))
        .ToImmutableArray();

                if (analyzers.Length > 0)
                {
                    try
                    {
                        var compilationWithAnalyzers = compilation.WithAnalyzers(
                analyzers,
                options: null
                );

                        diagnostics = (await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken))
                .Where(d => d.Severity >= severityFilter && !d.IsSuppressed);
                    }
                    catch
                    {
                        // Fallback to basic compilation diagnostics
                        diagnostics = compilation
                .GetDiagnostics()
                .Where(d => d.Severity >= severityFilter && !d.IsSuppressed);
                    }
                }
                else
                {
                    // No analyzers, use basic compilation diagnostics
                    diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity >= severityFilter && !d.IsSuppressed);
                }

                return diagnostics.Select(d =>
        {
            var filePath = d.Location.SourceTree?.FilePath ?? "Unknown";
            return (d, filePath);
        });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze project: {ProjectName}", project.Name);
                return Enumerable.Empty<(Diagnostic, string)>();
            }
        });

        // Wait for all projects to be analyzed in parallel
        var projectDiagnostics = await Task.WhenAll(diagnosticTasks);

        // Combine and get total count before pagination
        var allDiagnostics = projectDiagnostics
        .SelectMany(x => x)
        .OrderByDescending(d => d.Item1.Severity)
        .ThenBy(d => d.Item2)
        .ToList();

        var totalCount = allDiagnostics.Count;

        // ✅ PAGINATION: Apply skip/take for large diagnostic sets
        var paginatedDiagnostics = allDiagnostics
        .Skip(skip)
        .Take(take)
        .ToList();

        _logger.LogInformation(
        "Diagnostic analysis complete. Total: {Total}, Returned: {Returned}",
        totalCount,
        paginatedDiagnostics.Count
        );

        return new DiagnosticAnalysisResult
        {
            Diagnostics = paginatedDiagnostics,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }
}

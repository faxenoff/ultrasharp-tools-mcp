using System.Text.Json;
using Ultrasharp.Addon.Models;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles "validate" request — runs Roslyn diagnostics on a file or solution.
/// Requires Phase 2 (loaded solution).
/// </summary>
public sealed class ValidationHandler
{
    private readonly IDiagnosticService _diagnosticService;
    private readonly AddonLifecycleService _lifecycle;
    private readonly ILogger<ValidationHandler> _logger;

    public ValidationHandler(
        IDiagnosticService diagnosticService,
        AddonLifecycleService lifecycle,
        ILogger<ValidationHandler> logger)
    {
        _diagnosticService = diagnosticService;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>
    /// Validate a file or solution.
    /// Params: { "filePath"?: string, "severity"?: "error"|"warning"|"info", "take"?: number }
    /// </summary>
    public async Task<object?> HandleValidateAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var slnPath = _lifecycle.LoadedSolutionPath;
        if (string.IsNullOrEmpty(slnPath))
            return new { error = "No solution path" };

        // Parse severity filter
        var severityStr = "warning";
        if (request.Params?.TryGetProperty("severity", out var sev) == true)
            severityStr = sev.GetString() ?? "warning";

        var severity = severityStr.ToLowerInvariant() switch
        {
            "error" => Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
            "warning" => Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            "info" => Microsoft.CodeAnalysis.DiagnosticSeverity.Info,
            "hidden" => Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden,
            _ => Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
        };

        int take = 50;
        if (request.Params?.TryGetProperty("take", out var takeEl) == true)
            take = takeEl.GetInt32();

        var result = await _diagnosticService.AnalyzeAsync(slnPath, severity, 0, take, ct);

        // Filter by filePath if specified
        string? filterFilePath = null;
        if (request.Params?.TryGetProperty("filePath", out var fp) == true)
            filterFilePath = fp.GetString();

        var diagnostics = result.Diagnostics
            .Where(d => string.IsNullOrEmpty(filterFilePath) ||
                        string.Equals(d.FilePath, filterFilePath, StringComparison.OrdinalIgnoreCase))
            .Select(d =>
            {
                var lineSpan = d.Diagnostic.Location.GetLineSpan();
                return new
                {
                    id = d.Diagnostic.Id,
                    message = d.Diagnostic.GetMessage(),
                    severity = d.Diagnostic.Severity.ToString().ToLowerInvariant(),
                    filePath = d.FilePath,
                    line = lineSpan.StartLinePosition.Line + 1,
                    column = lineSpan.StartLinePosition.Character + 1,
                };
            })
            .ToList();

        return new
        {
            totalCount = result.TotalCount,
            hasMore = result.HasMore,
            diagnostics,
        };
    }
}

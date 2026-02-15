using System.Text.Json;
using Microsoft.CodeAnalysis;
using Ultrasharp.Addon.Models;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles code modification requests: modifyCode, renameSymbol, applyCodeFix, formatCode.
/// Requires Phase 2 (loaded solution).
/// Returns modified file content — TS applies changes via fs.writeFile.
/// </summary>
public sealed class ModificationHandler
{
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeModificationService _modificationService;
    private readonly ICodeFixService _codeFixService;
    private readonly IFormattingService _formattingService;
    private readonly AddonLifecycleService _lifecycle;
    private readonly ILogger<ModificationHandler> _logger;

    public ModificationHandler(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        ICodeFixService codeFixService,
        IFormattingService formattingService,
        AddonLifecycleService lifecycle,
        ILogger<ModificationHandler> logger)
    {
        _solutionManager = solutionManager;
        _modificationService = modificationService;
        _codeFixService = codeFixService;
        _formattingService = formattingService;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>
    /// Modify code using find-and-replace in the workspace.
    /// Params: { "targetString": string, "pattern": string, "replacement": string }
    /// Returns modified files with new content.
    /// </summary>
    public async Task<object?> HandleModifyCodeAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var targetString = request.Params?.GetProperty("targetString").GetString() ?? "";
        var pattern = request.Params?.GetProperty("pattern").GetString() ?? "";
        var replacement = request.Params?.GetProperty("replacement").GetString() ?? "";

        var newSolution = await _modificationService.FindAndReplaceAsync(
            targetString, pattern, replacement, ct);

        // Collect changed documents
        var changes = await CollectChangedDocumentsAsync(newSolution, ct);

        // Apply changes
        var result = await _modificationService.ApplyChangesAsync(
            newSolution, ct, "Addon: modify code");

        return new
        {
            success = true,
            changedFiles = changes,
            lintChangedFiles = result.ChangedFiles,
            hasLintResults = result.HasLintResults,
        };
    }

    /// <summary>
    /// Rename a symbol across the solution.
    /// Params: { "fqn": string, "newName": string }
    /// </summary>
    public async Task<object?> HandleRenameSymbolAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var fqn = request.Params?.GetProperty("fqn").GetString() ?? "";
        var newName = request.Params?.GetProperty("newName").GetString() ?? "";

        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, ct);
        if (symbol == null)
            return new { error = $"Symbol not found: {fqn}" };

        var newSolution = await _modificationService.RenameSymbolAsync(symbol, newName, ct);
        var changes = await CollectChangedDocumentsAsync(newSolution, ct);

        var result = await _modificationService.ApplyChangesAsync(
            newSolution, ct, $"Addon: rename {fqn} -> {newName}");

        return new
        {
            success = true,
            changedFiles = changes,
            lintChangedFiles = result.ChangedFiles,
            hasLintResults = result.HasLintResults,
        };
    }

    /// <summary>
    /// Apply code fix for a diagnostic.
    /// Params: { "diagnosticId": string, "preview"?: bool }
    /// </summary>
    public async Task<object?> HandleApplyCodeFixAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var slnPath = _lifecycle.LoadedSolutionPath;
        if (string.IsNullOrEmpty(slnPath))
            return new { error = "No solution path" };

        var diagnosticId = request.Params?.GetProperty("diagnosticId").GetString() ?? "";
        bool preview = false;
        if (request.Params?.TryGetProperty("preview", out var p) == true)
            preview = p.GetBoolean();

        var result = await _codeFixService.ApplyFixesAsync(slnPath, diagnosticId, preview, ct);

        return new
        {
            appliedFixes = result.AppliedFixes,
            totalFixableIssues = result.TotalFixableIssues,
            wasPreview = result.WasPreview,
            errors = result.Errors.Select(e => new { location = e.Location, error = e.Error }).ToList(),
        };
    }

    /// <summary>
    /// Format code in a file or directory.
    /// Params: { "path": string, "checkOnly"?: bool }
    /// </summary>
    public async Task<object?> HandleFormatCodeAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var path = request.Params?.GetProperty("path").GetString() ?? "";
        bool checkOnly = false;
        if (request.Params?.TryGetProperty("checkOnly", out var co) == true)
            checkOnly = co.GetBoolean();

        var result = await _formattingService.FormatAsync(path, checkOnly, ct);

        return new
        {
            filesNeedingFormatting = result.FilesNeedingFormatting,
            filesFormatted = result.FilesFormatted,
            totalFilesChecked = result.TotalFilesChecked,
            errors = result.Errors.Select(e => new { filePath = e.FilePath, error = e.Error }).ToList(),
        };
    }

    private async Task<List<object>> CollectChangedDocumentsAsync(Solution newSolution, CancellationToken ct)
    {
        var currentSolution = _solutionManager.CurrentSolution;
        if (currentSolution == null)
            return [];

        var changes = new List<object>();
        var solutionChanges = newSolution.GetChanges(currentSolution);

        foreach (var projectChanges in solutionChanges.GetProjectChanges())
        {
            foreach (var docId in projectChanges.GetChangedDocuments())
            {
                var doc = newSolution.GetDocument(docId);
                if (doc == null) continue;

                var text = await doc.GetTextAsync(ct);
                changes.Add(new
                {
                    filePath = doc.FilePath ?? "",
                    content = text?.ToString() ?? "",
                });
            }
        }

        return changes;
    }
}

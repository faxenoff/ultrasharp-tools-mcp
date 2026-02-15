using System.Text.Json;
using Microsoft.CodeAnalysis;
using Ultrasharp.Addon.Models;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles incremental document updates: updateDocument, addDocument, removeDocument.
/// Called by TS when file changes are detected (from GitWatcher).
/// </summary>
public sealed class DocumentHandler
{
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<DocumentHandler> _logger;

    public DocumentHandler(ISolutionManager solutionManager, ILogger<DocumentHandler> logger)
    {
        _solutionManager = solutionManager;
        _logger = logger;
    }

    /// <summary>
    /// Update document text in workspace.
    /// Params: { "filePath": string, "content": string }
    /// </summary>
    public async Task<object?> HandleUpdateDocumentAsync(AddonRequest request, CancellationToken ct)
    {
        var filePath = request.Params?.GetProperty("filePath").GetString() ?? "";
        var content = request.Params?.GetProperty("content").GetString() ?? "";

        if (!_solutionManager.IsSolutionLoaded)
            return new { success = false, error = "No solution loaded" };

        var solution = _solutionManager.CurrentSolution;
        if (solution == null)
            return new { success = false, error = "Solution is null" };

        // Find document by file path
        var document = solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (document == null)
            return new { success = false, error = $"Document not found: {filePath}" };

        // Update text
        var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(content);
        var newSolution = solution.WithDocumentText(document.Id, sourceText);

        if (_solutionManager.CurrentWorkspace != null)
        {
            _solutionManager.CurrentWorkspace.TryApplyChanges(newSolution);
            _solutionManager.RefreshCurrentSolution();
        }

        _logger.LogDebug("[Document] Updated: {Path}", filePath);
        return new { success = true, filePath };
    }

    /// <summary>
    /// Add a new document to a project.
    /// Params: { "filePath": string, "content": string, "projectName"?: string }
    /// </summary>
    public async Task<object?> HandleAddDocumentAsync(AddonRequest request, CancellationToken ct)
    {
        var filePath = request.Params?.GetProperty("filePath").GetString() ?? "";
        var content = request.Params?.GetProperty("content").GetString() ?? "";
        string? projectName = null;
        if (request.Params?.TryGetProperty("projectName", out var pn) == true)
            projectName = pn.GetString();

        if (!_solutionManager.IsSolutionLoaded)
            return new { success = false, error = "No solution loaded" };

        var solution = _solutionManager.CurrentSolution;
        if (solution == null)
            return new { success = false, error = "Solution is null" };

        // Find target project
        Project? project = null;
        if (!string.IsNullOrEmpty(projectName))
            project = _solutionManager.GetProjectByName(projectName);

        project ??= solution.Projects.FirstOrDefault(p =>
            filePath.StartsWith(Path.GetDirectoryName(p.FilePath) ?? "", StringComparison.OrdinalIgnoreCase));

        if (project == null)
            return new { success = false, error = "Could not determine target project" };

        var fileName = Path.GetFileName(filePath);
        var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(content);
        var newDocument = project.AddDocument(fileName, sourceText, filePath: filePath);

        if (_solutionManager.CurrentWorkspace != null)
        {
            _solutionManager.CurrentWorkspace.TryApplyChanges(newDocument.Project.Solution);
            _solutionManager.RefreshCurrentSolution();
        }

        _logger.LogDebug("[Document] Added: {Path} to {Project}", filePath, project.Name);
        return new { success = true, filePath, projectName = project.Name };
    }

    /// <summary>
    /// Remove a document from workspace.
    /// Params: { "filePath": string }
    /// </summary>
    public async Task<object?> HandleRemoveDocumentAsync(AddonRequest request, CancellationToken ct)
    {
        var filePath = request.Params?.GetProperty("filePath").GetString() ?? "";

        if (!_solutionManager.IsSolutionLoaded)
            return new { success = false, error = "No solution loaded" };

        var solution = _solutionManager.CurrentSolution;
        if (solution == null)
            return new { success = false, error = "Solution is null" };

        var document = solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (document == null)
            return new { success = false, error = $"Document not found: {filePath}" };

        var newSolution = solution.RemoveDocument(document.Id);

        if (_solutionManager.CurrentWorkspace != null)
        {
            _solutionManager.CurrentWorkspace.TryApplyChanges(newSolution);
            _solutionManager.RefreshCurrentSolution();
        }

        _logger.LogDebug("[Document] Removed: {Path}", filePath);
        return new { success = true, filePath };
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Service for automatic import/using statement updates after file operations.
/// Analyzes code dependencies and updates using directives intelligently.
/// </summary>
public class ImportUpdateService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<ImportUpdateService> _logger;

    public ImportUpdateService(
        ISolutionManager solutionManager,
        ILogger<ImportUpdateService> logger)
    {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyze file and determine required usings based on actual symbol usage.
    /// </summary>
    public async Task<List<string>> AnalyzeRequiredUsingsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Analyzing required usings for: {FilePath}", filePath);

        var document = await GetDocumentByPathAsync(filePath, cancellationToken);
        if (document == null)
        {
            _logger.LogWarning("Document not found in solution: {FilePath}", filePath);
            return new List<string>();
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (semanticModel == null || root == null)
        {
            return new List<string>();
        }

        var requiredNamespaces = new HashSet<string>();

        // Find all identifiers in the code
        var identifiers = root.DescendantNodes()
            .Where(n => n is IdentifierNameSyntax || n is GenericNameSyntax)
            .ToList();

        foreach (var identifier in identifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancellationToken);
            var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

            if (symbol != null)
            {
                var containingNamespace = GetContainingNamespace(symbol);
                if (!string.IsNullOrEmpty(containingNamespace))
                {
                    requiredNamespaces.Add(containingNamespace);
                }
            }
        }

        // Remove current file's namespace
        var currentNamespace = GetFileNamespace(root);
        if (!string.IsNullOrEmpty(currentNamespace))
        {
            requiredNamespaces.Remove(currentNamespace);
        }

        // Remove System namespace if no System types used
        if (!requiredNamespaces.Any(ns => ns.StartsWith("System")))
        {
            requiredNamespaces.RemoveWhere(ns => ns == "System");
        }

        _logger.LogDebug("Found {Count} required namespaces for {FilePath}",
            requiredNamespaces.Count, filePath);

        return requiredNamespaces.OrderBy(ns => ns).ToList();
    }

    /// <summary>
    /// Update using statements in file to match actual dependencies.
    /// </summary>
    public async Task<ImportUpdateResult> UpdateUsingsAsync(
        string filePath,
        bool removeUnused = true,
        bool addMissing = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating usings for: {FilePath} (removeUnused={Remove}, addMissing={Add})",
            filePath, removeUnused, addMissing);

        var document = await GetDocumentByPathAsync(filePath, cancellationToken);
        if (document == null)
        {
            return new ImportUpdateResult
            {
                Success = false,
                ErrorMessage = $"File not found in solution: {filePath}"
            };
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken) as CompilationUnitSyntax;
        if (root == null)
        {
            return new ImportUpdateResult
            {
                Success = false,
                ErrorMessage = "Failed to parse file"
            };
        }

        var originalUsings = root.Usings.Select(u => u.Name?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        var requiredUsings = await AnalyzeRequiredUsingsAsync(filePath, cancellationToken);

        var usingsToAdd = new List<string>();
        var usingsToRemove = new List<string>();

        // Find usings to add
        if (addMissing)
        {
            usingsToAdd = requiredUsings.Except(originalUsings).ToList();
        }

        // Find usings to remove
        if (removeUnused)
        {
            usingsToRemove = originalUsings.Except(requiredUsings).ToList();
        }

        // Nothing to do
        if (usingsToAdd.Count == 0 && usingsToRemove.Count == 0)
        {
            _logger.LogDebug("No using changes needed for {FilePath}", filePath);
            return new ImportUpdateResult
            {
                Success = true,
                FilePath = filePath,
                OriginalUsings = originalUsings,
                UpdatedUsings = originalUsings,
                UsingsAdded = new List<string>(),
                UsingsRemoved = new List<string>(),
                Message = "No changes needed"
            };
        }

        // Create new using directives
        var newUsings = new List<string>();
        newUsings.AddRange(originalUsings.Except(usingsToRemove));
        newUsings.AddRange(usingsToAdd);
        newUsings = newUsings.Distinct().OrderBy(u => u).ToList();

        // Update the file
        var newRoot = root.WithUsings(
            SyntaxFactory.List(
                newUsings.Select(ns =>
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                )
            )
        );

        var newCode = newRoot.NormalizeWhitespace().ToFullString();
        await File.WriteAllTextAsync(filePath, newCode, cancellationToken);

        _logger.LogInformation("Updated usings for {FilePath}: +{Added} -{Removed}",
            filePath, usingsToAdd.Count, usingsToRemove.Count);

        return new ImportUpdateResult
        {
            Success = true,
            FilePath = filePath,
            OriginalUsings = originalUsings,
            UpdatedUsings = newUsings,
            UsingsAdded = usingsToAdd,
            UsingsRemoved = usingsToRemove,
            Message = $"Added {usingsToAdd.Count} usings, removed {usingsToRemove.Count} usings"
        };
    }

    /// <summary>
    /// Update usings in multiple files (batch operation).
    /// </summary>
    public async Task<List<ImportUpdateResult>> UpdateUsingsInFilesAsync(
        string[] filePaths,
        bool removeUnused = true,
        bool addMissing = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Batch updating usings for {Count} files", filePaths.Length);

        var results = new List<ImportUpdateResult>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var result = await UpdateUsingsAsync(filePath, removeUnused, addMissing, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update usings for {FilePath}", filePath);
                results.Add(new ImportUpdateResult
                {
                    Success = false,
                    FilePath = filePath,
                    ErrorMessage = ex.Message
                });
            }
        }

        var successCount = results.Count(r => r.Success);
        _logger.LogInformation("Batch update complete: {Success}/{Total} files updated",
            successCount, results.Count);

        return results;
    }

    /// <summary>
    /// Analyze import changes between files (for split/synthesize operations).
    /// </summary>
    public async Task<ImportAnalysisResult> AnalyzeImportChangesAsync(
        string originalFile,
        string[] newFiles,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Analyzing import changes: {Original} -> {Count} new files",
            originalFile, newFiles.Length);

        var originalUsings = await AnalyzeRequiredUsingsAsync(originalFile, cancellationToken);

        var newFileUsings = new Dictionary<string, List<string>>();
        foreach (var newFile in newFiles)
        {
            var usings = await AnalyzeRequiredUsingsAsync(newFile, cancellationToken);
            newFileUsings[newFile] = usings;
        }

        var allNewUsings = newFileUsings.Values.SelectMany(u => u).Distinct().ToList();
        var commonUsings = newFileUsings.Values
            .Aggregate((IEnumerable<string>)originalUsings, (acc, usings) => acc.Intersect(usings))
            .ToList();

        return new ImportAnalysisResult
        {
            OriginalFile = originalFile,
            OriginalUsings = originalUsings,
            NewFiles = newFileUsings,
            CommonUsings = commonUsings,
            AllNewUsings = allNewUsings,
            UsingsPerFile = newFileUsings.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)new
                {
                    Total = kvp.Value.Count,
                    Unique = kvp.Value.Except(commonUsings).Count()
                }
            )
        };
    }

    // ==================== Helper Methods ====================

    private async Task<Document?> GetDocumentByPathAsync(string filePath, CancellationToken cancellationToken)
    {
        var solution = _solutionManager.CurrentWorkspace?.CurrentSolution;
        if (solution == null)
        {
            return null;
        }

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }

        return null;
    }

    private static string? GetContainingNamespace(ISymbol symbol)
    {
        var containingNamespace = symbol.ContainingNamespace;
        if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
        {
            return null;
        }

        return containingNamespace.ToDisplayString();
    }

    private static string? GetFileNamespace(SyntaxNode root)
    {
        var namespaceDecl = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDecl?.Name?.ToString();
    }
}

/// <summary>
/// Result of import update operation.
/// </summary>
public class ImportUpdateResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = "";
    public List<string> OriginalUsings { get; set; } = new();
    public List<string> UpdatedUsings { get; set; } = new();
    public List<string> UsingsAdded { get; set; } = new();
    public List<string> UsingsRemoved { get; set; } = new();
    public string Message { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of import analysis for multiple files.
/// </summary>
public class ImportAnalysisResult
{
    public string OriginalFile { get; set; } = "";
    public List<string> OriginalUsings { get; set; } = new();
    public Dictionary<string, List<string>> NewFiles { get; set; } = new();
    public List<string> CommonUsings { get; set; } = new();
    public List<string> AllNewUsings { get; set; } = new();
    public Dictionary<string, object> UsingsPerFile { get; set; } = new();
}

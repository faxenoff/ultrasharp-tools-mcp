using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;
using System.Text;

namespace UltrasharpTools.Tools.Preview;

/// <summary>
/// Manages preview generation for code modifications before applying changes.
/// Generates unified diff and estimates impact on codebase.
/// </summary>
public sealed class PreviewManager
{
private readonly ISolutionManager _solutionManager;
private readonly ICodeAnalysisService _codeAnalysisService;
private readonly ILogger<PreviewManager> _logger;

public PreviewManager(
ISolutionManager solutionManager,
ICodeAnalysisService codeAnalysisService,
ILogger<PreviewManager>? logger = null)
{
_solutionManager = solutionManager;
_codeAnalysisService = codeAnalysisService;
_logger = logger ?? NullLogger<PreviewManager>.Instance;
}

/// <summary>
/// Preview code modification for a member (method/class/property).
/// </summary>
public async Task<DiffPreview> PreviewCodeModificationAsync(
string fullyQualifiedName,
string newCode,
CancellationToken cancellationToken = default)
{
_logger.LogInformation("Generating preview for code modification: {FQN}", fullyQualifiedName);

// 1. Resolve symbol
var symbol = await _solutionManager.FindRoslynSymbolAsync(fullyQualifiedName, cancellationToken);
if (symbol == null)
{
throw new InvalidOperationException($"Symbol not found: {fullyQualifiedName}");
}

// 2. Get current code
var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
if (syntaxRef == null)
{
throw new InvalidOperationException($"Symbol has no syntax reference: {fullyQualifiedName}");
}

var syntaxNode = await syntaxRef.GetSyntaxAsync(cancellationToken);
var oldCode = syntaxNode.ToFullString();

// 3. Get file path
var filePath = syntaxRef.SyntaxTree.FilePath;

// 4. Generate diff
var diff = GenerateUnifiedDiff(oldCode, newCode, filePath, symbol.Name);

// 5. Estimate impact
var impact = await EstimateImpactAsync(symbol, cancellationToken);

return new DiffPreview
{
Operation = "modify_code",
TargetFqn = fullyQualifiedName,
FilesAffected = new[] { filePath },
Diff = diff,
OldCode = oldCode,
NewCode = newCode,
EstimatedImpact = impact
};
}

/// <summary>
/// Preview adding a new member to a type.
/// </summary>
public async Task<DiffPreview> PreviewAddMemberAsync(
string parentTypeFqn,
string newMemberCode,
CancellationToken cancellationToken = default)
{
_logger.LogInformation("Generating preview for add member to: {ParentType}", parentTypeFqn);

// 1. Resolve parent type
var symbol = await _solutionManager.FindRoslynSymbolAsync(parentTypeFqn, cancellationToken);
if (symbol is not INamedTypeSymbol typeSymbol)
{
throw new InvalidOperationException($"Symbol is not a type: {parentTypeFqn}");
}

// 2. Get file path
var syntaxRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
if (syntaxRef == null)
{
throw new InvalidOperationException($"Type has no syntax reference: {parentTypeFqn}");
}

var filePath = syntaxRef.SyntaxTree.FilePath;

// 3. Generate diff (insertion)
var diff = GenerateAddMemberDiff(newMemberCode, filePath, typeSymbol.Name);

return new DiffPreview
{
Operation = "add_member",
TargetFqn = parentTypeFqn,
FilesAffected = new[] { filePath },
Diff = diff,
OldCode = "",
NewCode = newMemberCode,
EstimatedImpact = new ImpactEstimation
{
EntitiesAffected = 1, // Parent type
ReferencesAffected = 0, // New member has no references yet
BreakingChange = false
}
};
}

/// <summary>
/// Preview renaming a symbol.
/// </summary>
public async Task<DiffPreview> PreviewRenameSymbolAsync(
string fullyQualifiedName,
string newName,
CancellationToken cancellationToken = default)
{
_logger.LogInformation("Generating preview for rename: {FQN} -> {NewName}", fullyQualifiedName, newName);

// 1. Resolve symbol
var symbol = await _solutionManager.FindRoslynSymbolAsync(fullyQualifiedName, cancellationToken);
if (symbol == null)
{
throw new InvalidOperationException($"Symbol not found: {fullyQualifiedName}");
}

// 2. Find all references
var solution = _solutionManager.CurrentWorkspace?.CurrentSolution;
if (solution == null)
{
throw new InvalidOperationException("No solution loaded");
}

var references = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync(
symbol,
solution,
cancellationToken);

var referencesCount = references.Sum(r => r.Locations.Count());

// 3. Get affected files
var affectedFiles = references
.SelectMany(r => r.Locations)
.Select(l => l.Document.FilePath ?? "")
.Distinct()
.Where(p => !string.IsNullOrEmpty(p))
.ToArray();

// 4. Generate summary diff
var diff = GenerateRenameDiffSummary(symbol.Name, newName, affectedFiles.Length, referencesCount);

return new DiffPreview
{
Operation = "rename_symbol",
TargetFqn = fullyQualifiedName,
FilesAffected = affectedFiles,
Diff = diff,
OldCode = symbol.Name,
NewCode = newName,
EstimatedImpact = new ImpactEstimation
{
EntitiesAffected = 1,
ReferencesAffected = referencesCount,
BreakingChange = IsPublicSymbol(symbol) // Public renames are breaking changes
}
};
}

// ==================== Private Helper Methods ====================

private string GenerateUnifiedDiff(string oldCode, string newCode, string filePath, string symbolName)
{
var sb = new StringBuilder();

// Unified diff header
sb.AppendLine($"--- a/{Path.GetFileName(filePath)}");
sb.AppendLine($"+++ b/{Path.GetFileName(filePath)}");

// Split into lines
var oldLines = oldCode.Split('\n');
var newLines = newCode.Split('\n');

// Simple line-by-line diff (could use more sophisticated algorithm like Myers diff)
var maxLines = Math.Max(oldLines.Length, newLines.Length);

sb.AppendLine($"@@ -{symbolName} +{symbolName} @@");

for (int i = 0; i < maxLines; i++)
{
if (i < oldLines.Length && i < newLines.Length)
{
if (oldLines[i] != newLines[i])
{
sb.AppendLine($"-{oldLines[i]}");
sb.AppendLine($"+{newLines[i]}");
}
else
{
sb.AppendLine($" {oldLines[i]}");
}
}
else if (i < oldLines.Length)
{
sb.AppendLine($"-{oldLines[i]}");
}
else if (i < newLines.Length)
{
sb.AppendLine($"+{newLines[i]}");
}
}

return sb.ToString();
}

private string GenerateAddMemberDiff(string newMemberCode, string filePath, string parentTypeName)
{
var sb = new StringBuilder();

sb.AppendLine($"--- a/{Path.GetFileName(filePath)}");
sb.AppendLine($"+++ b/{Path.GetFileName(filePath)}");
sb.AppendLine($"@@ +{parentTypeName} (new member) @@");

foreach (var line in newMemberCode.Split('\n'))
{
sb.AppendLine($"+{line}");
}

return sb.ToString();
}

private string GenerateRenameDiffSummary(string oldName, string newName, int filesAffected, int referencesAffected)
{
var sb = new StringBuilder();

sb.AppendLine($"Rename: {oldName} -> {newName}");
sb.AppendLine($"Files affected: {filesAffected}");
sb.AppendLine($"References affected: {referencesAffected}");
sb.AppendLine();
sb.AppendLine($"- {oldName}");
sb.AppendLine($"+ {newName}");

return sb.ToString();
}

private async Task<ImpactEstimation> EstimateImpactAsync(ISymbol symbol, CancellationToken cancellationToken)
{
var solution = _solutionManager.CurrentWorkspace?.CurrentSolution;
if (solution == null)
{
return new ImpactEstimation { EntitiesAffected = 1, ReferencesAffected = 0, BreakingChange = false };
}

try
{
// Find all references to this symbol
var references = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync(
symbol,
solution,
cancellationToken);

var referencesCount = references.Sum(r => r.Locations.Count());

// Check if this is a public API (breaking change potential)
var isBreaking = IsPublicSymbol(symbol);

return new ImpactEstimation
{
EntitiesAffected = 1,
ReferencesAffected = referencesCount,
BreakingChange = isBreaking
};
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to estimate impact for {Symbol}", symbol.ToDisplayString());

return new ImpactEstimation
{
EntitiesAffected = 1,
ReferencesAffected = 0,
BreakingChange = false
};
}
}

private bool IsPublicSymbol(ISymbol symbol)
{
return symbol.DeclaredAccessibility == Accessibility.Public ||
symbol.DeclaredAccessibility == Accessibility.Protected ||
symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
}
}

/// <summary>
/// Result of preview operation containing diff and impact analysis.
/// </summary>
public sealed record DiffPreview
{
/// <summary>
/// Operation type: "modify_code", "add_member", "rename_symbol", "move_member"
/// </summary>
public required string Operation { get; init; }

/// <summary>
/// Fully qualified name of target entity.
/// </summary>
public required string TargetFqn { get; init; }

/// <summary>
/// Files that will be affected by this change.
/// </summary>
public required string[] FilesAffected { get; init; }

/// <summary>
/// Unified diff format showing changes.
/// </summary>
public required string Diff { get; init; }

/// <summary>
/// Original code (before changes).
/// </summary>
public required string OldCode { get; init; }

/// <summary>
/// New code (after changes).
/// </summary>
public required string NewCode { get; init; }

/// <summary>
/// Estimated impact on codebase.
/// </summary>
public required ImpactEstimation EstimatedImpact { get; init; }
}

/// <summary>
/// Impact estimation for a code change.
/// </summary>
public sealed record ImpactEstimation
{
/// <summary>
/// Number of entities (classes/methods) directly affected.
/// </summary>
public required int EntitiesAffected { get; init; }

/// <summary>
/// Number of references to this entity that will be affected.
/// </summary>
public required int ReferencesAffected { get; init; }

/// <summary>
/// Whether this change is potentially breaking (public API change).
/// </summary>
public required bool BreakingChange { get; init; }
}

using Microsoft.CodeAnalysis;
using ModelContextProtocol;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Services;
using System.Text;

namespace UltrasharpTools.Tools.Mcp.Tools;

// Marker class for ILogger<T> category specific to QualityTools
public class QualityToolsLogCategory { }

/// <summary>
/// MCP инструменты для анализа и улучшения качества кода
/// </summary>
[McpServerToolType]
public static partial class QualityTools
{
[McpServerTool(Name = "format_code", Idempotent = false, ReadOnly = false, Destructive = false, OpenWorld = false)]
[Description("Formats C# code files using Roslyn Formatter. Supports .cs, .csproj, and .xml files. Can check formatting without applying changes.")]
public static async Task<object> FormatCode(
IFormattingService formattingService,
ILogger<QualityToolsLogCategory> logger,
[Description("Path to a file or directory to format")] string path,
[Description("If true, only check formatting without applying changes (default: true)")] bool checkOnly = true,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(path, nameof(path), logger);

logger.LogInformation("Executing {ToolName} for path: {Path}, CheckOnly: {CheckOnly}",
nameof(FormatCode), path, checkOnly);

if (!Path.IsPathFullyQualified(path))
{
throw new McpException($"Path must be absolute: {path}");
}

if (!File.Exists(path) && !Directory.Exists(path))
{
throw new McpException($"Path does not exist: {path}");
}

var result = await formattingService.FormatAsync(path, checkOnly, cancellationToken);

var output = new StringBuilder();
output.AppendLine("## Code Formatting Results\n");
output.AppendLine($"**Total files checked:** {result.TotalFilesChecked}");
output.AppendLine($"**Files needing formatting:** {result.FilesNeedingFormatting.Count}");

if (!checkOnly)
{
output.AppendLine($"**Files formatted:** {result.FilesFormatted.Count}");
}

output.AppendLine();

if (result.FilesNeedingFormatting.Count > 0)
{
output.AppendLine("### Files needing formatting:");
foreach (var file in result.FilesNeedingFormatting.Take(50))
{
var relativePath = Path.GetRelativePath(path, file);
output.AppendLine($"  • `{relativePath}`");
}

if (result.FilesNeedingFormatting.Count > 50)
{
output.AppendLine($"\n  ... and {result.FilesNeedingFormatting.Count - 50} more files");
}
}
else
{
output.AppendLine("✅ All files are properly formatted!");
}

if (result.Errors.Count > 0)
{
output.AppendLine("\n### Errors:");
foreach (var (filePath, error) in result.Errors.Take(10))
{
var relativePath = Path.GetRelativePath(path, filePath);
output.AppendLine($"  • `{relativePath}`: {error}");
}

if (result.Errors.Count > 10)
{
output.AppendLine($"\n  ... and {result.Errors.Count - 10} more errors");
}
}

if (checkOnly && result.FilesNeedingFormatting.Count > 0)
{
output.AppendLine("\n**💡 Tip:** Set `checkOnly=false` to apply formatting changes.");
}

return ToolHelpers.ToJson(new
{
summary = output.ToString(),
totalFilesChecked = result.TotalFilesChecked,
filesNeedingFormatting = result.FilesNeedingFormatting.Count,
filesFormatted = result.FilesFormatted.Count,
errors = result.Errors.Count
});

}, logger, nameof(FormatCode), cancellationToken);
}

[McpServerTool(Name = "analyze_code_style", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
[Description("Analyzes C# code using Roslyn analyzers to find code style issues, warnings, and errors. Returns diagnostics grouped by severity.")]
public static async Task<object> AnalyzeCodeStyle(
IDiagnosticService diagnosticService,
ISolutionManager solutionManager,
ILogger<QualityToolsLogCategory> logger,
[Description("Path to the solution file (.sln)")] string solutionPath,
[Description("Minimum diagnostic severity to return: 'Hidden', 'Info', 'Warning', 'Error' (default: 'Warning')")] string severityFilter = "Warning",
[Description("Number of results to skip for pagination (default: 0)")] int skip = 0,
[Description("Number of results to return (default: 100)")] int take = 100,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(solutionPath, nameof(solutionPath), logger);
await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(AnalyzeCodeStyle), cancellationToken);

logger.LogInformation("Executing {ToolName} for solution: {SolutionPath}",
nameof(AnalyzeCodeStyle), solutionPath);

if (!Enum.TryParse<DiagnosticSeverity>(severityFilter, true, out var severity))
{
throw new McpException(
$"Invalid severity filter: {severityFilter}. Valid values: Hidden, Info, Warning, Error");
}

var result = await diagnosticService.AnalyzeAsync(solutionPath, severity, skip, take, cancellationToken);

var output = new StringBuilder();
output.AppendLine("## Code Style Analysis Results\n");
output.AppendLine($"**Total diagnostics found:** {result.TotalCount}");
output.AppendLine($"**Showing:** {result.Diagnostics.Count} (skip: {skip}, take: {take})");
output.AppendLine($"**Has more results:** {result.HasMore}");
output.AppendLine();

// Group by severity
var grouped = result.Diagnostics.GroupBy(d => d.Diagnostic.Severity);

foreach (var group in grouped.OrderByDescending(g => g.Key))
{
output.AppendLine($"### {GetSeverityEmoji(group.Key)} {group.Key} ({group.Count()})");
output.AppendLine();

foreach (var (diagnostic, filePath) in group.Take(30))
{
var lineSpan = diagnostic.Location.GetLineSpan();
var fileName = Path.GetFileName(filePath);
var line = lineSpan.StartLinePosition.Line + 1;

output.AppendLine($"**{diagnostic.Id}**: {diagnostic.GetMessage()}");
output.AppendLine($"  📄 `{fileName}:{line}`");
output.AppendLine();
}

if (group.Count() > 30)
{
output.AppendLine($"  ... and {group.Count() - 30} more {group.Key} diagnostics");
output.AppendLine();
}
}

if (result.HasMore)
{
output.AppendLine($"\n**💡 Tip:** Use `skip={skip + take}` to see more results.");
}

return ToolHelpers.ToJson(new
{
summary = output.ToString(),
totalCount = result.TotalCount,
returnedCount = result.Diagnostics.Count,
hasMore = result.HasMore,
diagnostics = result.Diagnostics.Select(d => new
{
id = d.Diagnostic.Id,
severity = d.Diagnostic.Severity.ToString(),
message = d.Diagnostic.GetMessage(),
filePath = d.FilePath,
line = d.Diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
column = d.Diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1
}).ToList()
});

}, logger, nameof(AnalyzeCodeStyle), cancellationToken);
}

[McpServerTool(Name = "apply_code_fixes", Idempotent = false, ReadOnly = false, Destructive = false, OpenWorld = false)]
[Description("Automatically applies code fixes for Roslyn diagnostics. Supports common issues like unused usings (IDE0005, CS8019) and more. Creates a git commit if not in preview mode.")]
public static async Task<object> ApplyCodeFixes(
ICodeFixService codeFixService,
ISolutionManager solutionManager,
ILogger<QualityToolsLogCategory> logger,
[Description("Path to the solution file (.sln)")] string solutionPath,
[Description("Diagnostic ID to fix (e.g., 'IDE0005', 'CS8019'), or 'all' for all fixable issues (default: 'all')")] string diagnosticId = "all",
[Description("If true, only preview changes without applying them (default: true)")] bool preview = true,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(solutionPath, nameof(solutionPath), logger);
await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(ApplyCodeFixes), cancellationToken);

logger.LogInformation("Executing {ToolName} for solution: {SolutionPath}, DiagnosticId: {DiagnosticId}, Preview: {Preview}",
nameof(ApplyCodeFixes), solutionPath, diagnosticId, preview);

var result = await codeFixService.ApplyFixesAsync(solutionPath, diagnosticId, preview, cancellationToken);

var output = new StringBuilder();
output.AppendLine("## Code Fix Results\n");
output.AppendLine($"**Total fixable issues found:** {result.TotalFixableIssues}");

if (result.TotalFixableIssues == 0)
{
output.AppendLine("\n✅ No fixable issues found!");
return ToolHelpers.ToJson(new
{
summary = output.ToString(),
totalFixableIssues = 0,
appliedFixes = 0,
wasPreview = preview
});
}

output.AppendLine();
output.AppendLine("### Fixes:");

foreach (var fix in result.AppliedFixes.Take(50))
{
output.AppendLine($"  • {fix}");
}

if (result.AppliedFixes.Count > 50)
{
output.AppendLine($"\n  ... and {result.AppliedFixes.Count - 50} more fixes");
}

if (result.Errors.Count > 0)
{
output.AppendLine("\n### Errors:");
foreach (var (location, error) in result.Errors.Take(10))
{
output.AppendLine($"  • {location}: {error}");
}

if (result.Errors.Count > 10)
{
output.AppendLine($"\n  ... and {result.Errors.Count - 10} more errors");
}
}

if (preview)
{
output.AppendLine("\n**⚠️ Preview mode:** No changes were applied.");
output.AppendLine("**💡 Tip:** Set `preview=false` to apply the fixes and create a git commit.");
}
else
{
output.AppendLine("\n**✅ Changes applied successfully!** Git commit created.");
}

return ToolHelpers.ToJson(new
{
summary = output.ToString(),
totalFixableIssues = result.TotalFixableIssues,
appliedFixes = result.AppliedFixes.Count,
errors = result.Errors.Count,
wasPreview = preview
});

}, logger, nameof(ApplyCodeFixes), cancellationToken);
}

private static string GetSeverityEmoji(DiagnosticSeverity severity) => severity switch
{
DiagnosticSeverity.Error => "❌",
DiagnosticSeverity.Warning => "⚠️",
DiagnosticSeverity.Info => "ℹ️",
DiagnosticSeverity.Hidden => "🔹",
_ => "•"
};
}

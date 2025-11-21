using ModelContextProtocol;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Mcp.Tools;

// Marker class for ILogger<T> category specific to QualityTools
public class QualityToolsLogCategory { }

/// <summary>
/// MCP инструменты для анализа и улучшения качества кода
/// </summary>
[McpServerToolType]
public static partial class QualityTools
{
    [McpServerTool(
        Name = "format_code",
        Idempotent = false,
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Formats C# code files using Roslyn Formatter. Supports .cs, .csproj, and .xml files. Can check formatting without applying changes."
    )]
    public static async Task<object> FormatCode(
        IFormattingService formattingService,
        ILogger<QualityToolsLogCategory> logger,
        [Description("Path to a file or directory to format")] string path,
        [Description("If true, only check formatting without applying changes (default: true)")]
            bool checkOnly = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                ErrorHandlingHelpers.ValidateStringParameter(path, nameof(path), logger);

                logger.LogInformation(
                    "Executing {ToolName} for path: {Path}, CheckOnly: {CheckOnly}",
                    nameof(FormatCode),
                    path,
                    checkOnly
                );

                if (!Path.IsPathFullyQualified(path))
                {
                    throw new McpException($"Path must be absolute: {path}");
                }

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    throw new McpException($"Path does not exist: {path}");
                }

                var result = await formattingService.FormatAsync(
                    path,
                    checkOnly,
                    cancellationToken
                );

                var output = ObjectPoolProvider.Instance.GetStringBuilder();
                try
                {
                    output.AppendLine("## Code Formatting Results\n");
                    output.AppendLine($"**Total files checked:** {result.TotalFilesChecked}");
                    output.AppendLine(
                        $"**Files needing formatting:** {result.FilesNeedingFormatting.Count}"
                    );

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
                            output.AppendLine(
                                $"\n  ... and {result.FilesNeedingFormatting.Count - 50} more files"
                            );
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
                            output.AppendLine(
                                $"\n  ... and {result.Errors.Count - 10} more errors"
                            );
                        }
                    }

                    if (checkOnly && result.FilesNeedingFormatting.Count > 0)
                    {
                        output.AppendLine(
                            "\n**💡 Tip:** Set `checkOnly=false` to apply formatting changes."
                        );
                    }

                    var outputStr = output.ToString();

                    return ToolHelpers.ToJson(
                        new
                        {
                            summary = outputStr,
                            totalFilesChecked = result.TotalFilesChecked,
                            filesNeedingFormatting = result.FilesNeedingFormatting.Count,
                            filesFormatted = result.FilesFormatted.Count,
                            errors = result.Errors.Count,
                        }
                    );
                }
                finally
                {
                    ObjectPoolProvider.Instance.ReturnStringBuilder(output);
                }
            },
            logger,
            nameof(FormatCode),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "analyze_code_style",
        Idempotent = true,
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Analyzes C# code using Roslyn analyzers with advanced filtering. Supports presets (performance, security, critical, etc.), specific diagnostic IDs, file patterns, and project filters."
    )]
    public static async Task<object> AnalyzeCodeStyle(
        IDiagnosticService diagnosticService,
        ISolutionManager solutionManager,
        ILogger<QualityToolsLogCategory> logger,
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description(
            "Minimum diagnostic severity: 'Hidden', 'Info', 'Warning', 'Error' (default: 'Warning')"
        )]
            string? severityFilter = null,
        [Description(
            "Preset name for quick rule selection: 'performance', 'security', 'reliability', 'maintainability', 'critical', 'high', 'medium', 'low', etc. See DiagnosticPresets for full list."
        )]
            string? preset = null,
        [Description(
            "Specific diagnostic IDs to filter (e.g., 'CA1822,CA1860,CS8019'). Comma-separated list."
        )]
            string? diagnosticIds = null,
        [Description(
            "File patterns to filter (glob syntax, e.g., '**/Services/*.cs,**/Controllers/*.cs'). Comma-separated list."
        )]
            string? filePatterns = null,
        [Description(
            "Project names to filter (e.g., 'UltrasharpTools.Tools,UltrasharpTools.Droid'). Comma-separated list."
        )]
            string? projectNames = null,
        [Description("Number of results to skip for pagination (default: 0)")] int skip = 0,
        [Description("Number of results to return (default: 100)")] int take = 100,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                ErrorHandlingHelpers.ValidateStringParameter(
                    solutionPath,
                    nameof(solutionPath),
                    logger
                );
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(AnalyzeCodeStyle),
                    cancellationToken
                );

                logger.LogInformation(
                    "Executing {ToolName} with preset={Preset}, diagnosticIds={DiagnosticIds}",
                    nameof(AnalyzeCodeStyle),
                    preset ?? "none",
                    diagnosticIds ?? "all"
                );

                // Parse severity filter
                var severity = DiagnosticSeverity.Warning;
                if (severityFilter != null)
                {
                    if (!Enum.TryParse<DiagnosticSeverity>(severityFilter, true, out severity))
                    {
                        throw new McpException(
                            $"Invalid severity filter: {severityFilter}. Valid: Hidden, Info, Warning, Error"
                        );
                    }
                }

                // Build filter options
                var filterOptions = new DiagnosticFilterOptions
                {
                    SeverityFilter = severity,
                    Skip = skip,
                    Take = take,
                };

                // Apply preset if specified
                if (!string.IsNullOrEmpty(preset))
                {
                    filterOptions = DiagnosticFilterOptions.WithPreset(preset, severity);
                    filterOptions = filterOptions with
                    {
                        Skip = skip,
                        Take = take,
                    };
                }

                // Override with specific diagnostic IDs if provided
                if (!string.IsNullOrEmpty(diagnosticIds))
                {
                    var ids = diagnosticIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToArray();
                    filterOptions = filterOptions with { DiagnosticIds = ids };
                }

                // Apply file patterns if provided
                if (!string.IsNullOrEmpty(filePatterns))
                {
                    var patterns = filePatterns
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToArray();
                    filterOptions = filterOptions with { FilePatterns = patterns };
                }

                // Apply project names if provided
                if (!string.IsNullOrEmpty(projectNames))
                {
                    var projects = projectNames
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToArray();
                    filterOptions = filterOptions with { ProjectNames = projects };
                }

                var result = await diagnosticService.AnalyzeAsync(
                    solutionPath,
                    filterOptions,
                    cancellationToken
                );

                var output = ObjectPoolProvider.Instance.GetStringBuilder();
                try
                {
                    output.AppendLine("## Code Style Analysis Results\n");

                    // Show filters
                    if (!string.IsNullOrEmpty(preset))
                    {
                        output.AppendLine($"**Preset:** {preset}");
                    }
                    if (filterOptions.DiagnosticIds?.Count() > 0)
                    {
                        output.AppendLine(
                            $"**Filtered IDs:** {string.Join(", ", filterOptions.DiagnosticIds.Take(10))}{(filterOptions.DiagnosticIds.Count() > 10 ? $" (+{filterOptions.DiagnosticIds.Count() - 10} more)" : "")}"
                        );
                    }
                    if (filterOptions.FilePatterns?.Count() > 0)
                    {
                        output.AppendLine($"**File patterns:** {string.Join(", ", filterOptions.FilePatterns)}");
                    }
                    if (filterOptions.ProjectNames?.Count() > 0)
                    {
                        output.AppendLine($"**Projects:** {string.Join(", ", filterOptions.ProjectNames)}");
                    }
                    output.AppendLine($"**Severity filter:** {filterOptions.SeverityFilter}");
                    output.AppendLine();

                    output.AppendLine($"**Total diagnostics found:** {result.TotalCount}");
                    output.AppendLine(
                        $"**Showing:** {result.Diagnostics.Count} (skip: {skip}, take: {take})"
                    );
                    output.AppendLine($"**Has more results:** {result.HasMore}");
                    output.AppendLine();

                    // Group by severity
                    var grouped = result.Diagnostics.GroupBy(d => d.Diagnostic.Severity);

                    foreach (var group in grouped.OrderByDescending(g => g.Key))
                    {
                        output.AppendLine(
                            $"### {GetSeverityEmoji(group.Key)} {group.Key} ({group.Count()})"
                        );
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
                            output.AppendLine(
                                $"  ... and {group.Count() - 30} more {group.Key} diagnostics"
                            );
                            output.AppendLine();
                        }
                    }

                    if (result.HasMore)
                    {
                        output.AppendLine(
                            $"\n**💡 Tip:** Use `skip={skip + take}` to see more results."
                        );
                    }

                    var outputStr = output.ToString();

                    return ToolHelpers.ToJson(
                        new
                        {
                            summary = outputStr,
                            totalCount = result.TotalCount,
                            returnedCount = result.Diagnostics.Count,
                            hasMore = result.HasMore,
                            diagnostics = result
                                .Diagnostics.Select(d => new
                                {
                                    id = d.Diagnostic.Id,
                                    severity = d.Diagnostic.Severity.ToString(),
                                    message = d.Diagnostic.GetMessage(),
                                    filePath = d.FilePath,
                                    line = d.Diagnostic.Location.GetLineSpan().StartLinePosition.Line
                                        + 1,
                                    column = d.Diagnostic.Location.GetLineSpan().StartLinePosition.Character
                                        + 1,
                                })
                                .ToList(),
                        }
                    );
                }
                finally
                {
                    ObjectPoolProvider.Instance.ReturnStringBuilder(output);
                }
            },
            logger,
            nameof(AnalyzeCodeStyle),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "apply_code_fixes",
        Idempotent = false,
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Automatically applies code fixes for Roslyn diagnostics. Supports common issues like unused usings (IDE0005, CS8019) and more. Creates a git commit if not in preview mode."
    )]
    public static async Task<object> ApplyCodeFixes(
        ICodeFixService codeFixService,
        ISolutionManager solutionManager,
        ILogger<QualityToolsLogCategory> logger,
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description(
            "Diagnostic ID to fix (e.g., 'IDE0005', 'CS8019'), or 'all' for all fixable issues (default: 'all')"
        )]
            string diagnosticId = "all",
        [Description("If true, only preview changes without applying them (default: true)")]
            bool preview = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                ErrorHandlingHelpers.ValidateStringParameter(
                    solutionPath,
                    nameof(solutionPath),
                    logger
                );
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(ApplyCodeFixes),
                    cancellationToken
                );

                logger.LogInformation(
                    "Executing {ToolName} for solution: {SolutionPath}, DiagnosticId: {DiagnosticId}, Preview: {Preview}",
                    nameof(ApplyCodeFixes),
                    solutionPath,
                    diagnosticId,
                    preview
                );

                var result = await codeFixService.ApplyFixesAsync(
                    solutionPath,
                    diagnosticId,
                    preview,
                    cancellationToken
                );

                var output = ObjectPoolProvider.Instance.GetStringBuilder();
                try
                {
                    output.AppendLine("## Code Fix Results\n");
                    output.AppendLine(
                        $"**Total fixable issues found:** {result.TotalFixableIssues}"
                    );

                    if (result.TotalFixableIssues == 0)
                    {
                        output.AppendLine("\n✅ No fixable issues found!");
                        var outputStr = output.ToString();
                        return ToolHelpers.ToJson(
                            new
                            {
                                summary = outputStr,
                                totalFixableIssues = 0,
                                appliedFixes = 0,
                                wasPreview = preview,
                            }
                        );
                    }

                    output.AppendLine();
                    output.AppendLine("### Fixes:");

                    foreach (var fix in result.AppliedFixes.Take(50))
                    {
                        output.AppendLine($"  • {fix}");
                    }

                    if (result.AppliedFixes.Count > 50)
                    {
                        output.AppendLine(
                            $"\n  ... and {result.AppliedFixes.Count - 50} more fixes"
                        );
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
                            output.AppendLine(
                                $"\n  ... and {result.Errors.Count - 10} more errors"
                            );
                        }
                    }

                    if (preview)
                    {
                        output.AppendLine("\n**⚠️ Preview mode:** No changes were applied.");
                        output.AppendLine(
                            "**💡 Tip:** Set `preview=false` to apply the fixes and create a git commit."
                        );
                    }
                    else
                    {
                        output.AppendLine(
                            "\n**✅ Changes applied successfully!** Git commit created."
                        );
                    }

                    return ToolHelpers.ToJson(
                        new
                        {
                            summary = output.ToString(),
                            totalFixableIssues = result.TotalFixableIssues,
                            appliedFixes = result.AppliedFixes.Count,
                            errors = result.Errors.Count,
                            wasPreview = preview,
                        }
                    );
                }
                finally
                {
                    ObjectPoolProvider.Instance.ReturnStringBuilder(output);
                }
            },
            logger,
            nameof(ApplyCodeFixes),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "cleanup_usings",
        Idempotent = false,
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Removes redundant using directives that duplicate global usings declared in GlobalUsings.cs files. Scans all projects and removes usings that are already declared globally."
    )]
    public static async Task<object> CleanupUsings(
        ISolutionManager solutionManager,
        ILogger<QualityToolsLogCategory> logger,
        [Description("Path to solution directory")] string path,
        [Description("If true, only preview changes without applying them (default: true)")]
            bool preview = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                ErrorHandlingHelpers.ValidateStringParameter(path, nameof(path), logger);
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(CleanupUsings),
                    cancellationToken
                );

                logger.LogInformation(
                    "Executing {ToolName} for path: {Path}, Preview: {Preview}",
                    nameof(CleanupUsings),
                    path,
                    preview
                );

                if (!Path.IsPathFullyQualified(path))
                {
                    throw new McpException($"Path must be absolute: {path}");
                }

                if (!Directory.Exists(path))
                {
                    throw new McpException($"Directory does not exist: {path}");
                }

                if (!solutionManager.IsSolutionLoaded)
                {
                    throw new McpException("No solution loaded");
                }

                var output = ObjectPoolProvider.Instance.GetStringBuilder();
                try
                {
                    output.AppendLine("=== Redundant Using Directives Cleanup ===");
                    output.AppendLine($"Mode: {(preview ? "PREVIEW (dry run)" : "APPLY CHANGES")}");
                    output.AppendLine();

                    var solution = solutionManager.CurrentSolution;
                    var totalFilesChanged = 0;
                    var totalUsingsRemoved = 0;

                    // Find all GlobalUsings.cs files across projects
                    var globalUsingsMap = new Dictionary<string, HashSet<string>>();

                    foreach (var project in solution.Projects)
                    {
                        var globalUsingsDoc = project.Documents.FirstOrDefault(d =>
                            Path.GetFileName(d.FilePath ?? "") == "GlobalUsings.cs"
                        );

                        if (globalUsingsDoc != null)
                        {
                            var root = await globalUsingsDoc.GetSyntaxRootAsync(cancellationToken);
                            if (root != null)
                            {
                                var globalUsings = root.DescendantNodes()
                                    .OfType<UsingDirectiveSyntax>()
                                    .Where(u => u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                                    .Select(u => u.Name?.ToString())
                                    .Where(n => n != null)
                                    .OfType<string>() // Filter out nulls with correct type
                                    .ToHashSet(StringComparer.Ordinal);

                                globalUsingsMap[project.Name] = globalUsings;

                                output.AppendLine($"📁 Project: {project.Name}");
                                output.AppendLine(
                                    $"   Found {globalUsings.Count} global using directive(s)"
                                );
                            }
                        }
                    }

                    if (globalUsingsMap.Count == 0)
                    {
                        output.AppendLine();
                        output.AppendLine("⚠️  No GlobalUsings.cs files found in solution");
                        return ToolHelpers.ToJson(
                            new
                            {
                                summary = output.ToString(),
                                filesChanged = 0,
                                usingsRemoved = 0,
                            }
                        );
                    }

                    output.AppendLine();
                    output.AppendLine("=== Scanning for redundant usings ===");
                    output.AppendLine();

                    // Scan all documents in projects that have global usings
                    foreach (var project in solution.Projects)
                    {
                        if (!globalUsingsMap.TryGetValue(project.Name, out var globalUsings))
                            continue;

                        var projectFilesChanged = 0;
                        var projectUsingsRemoved = 0;

                        foreach (var document in project.Documents)
                        {
                            if (Path.GetFileName(document.FilePath ?? "") == "GlobalUsings.cs")
                                continue;

                            var root = await document.GetSyntaxRootAsync(cancellationToken);
                            if (root == null)
                                continue;

                            var usingDirectives = root.DescendantNodes()
                                .OfType<UsingDirectiveSyntax>()
                                .Where(u => !u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                                .ToList();

                            var redundantUsings = usingDirectives
                                .Where(u => globalUsings.Contains(u.Name?.ToString() ?? ""))
                                .ToList();

                            if (redundantUsings.Count > 0)
                            {
                                var relativePath = Path.GetRelativePath(
                                    path,
                                    document.FilePath ?? ""
                                );
                                output.AppendLine($"📄 {relativePath}");

                                foreach (var redundant in redundantUsings)
                                {
                                    output.AppendLine($"   - Remove: using {redundant.Name};");
                                }

                                if (!preview)
                                {
                                    // Remove redundant usings
                                    var newRoot = root.RemoveNodes(
                                        redundantUsings,
                                        SyntaxRemoveOptions.KeepNoTrivia
                                    )!;

                                    // Write back to file
                                    var text = newRoot.ToFullString();
                                    await File.WriteAllTextAsync(
                                        document.FilePath!,
                                        text,
                                        cancellationToken
                                    );

                                    output.AppendLine($"   ✓ Applied changes");
                                }

                                projectFilesChanged++;
                                projectUsingsRemoved += redundantUsings.Count;
                            }
                        }

                        if (projectFilesChanged > 0)
                        {
                            totalFilesChanged += projectFilesChanged;
                            totalUsingsRemoved += projectUsingsRemoved;
                        }
                    }

                    output.AppendLine();
                    output.AppendLine("=== Summary ===");
                    output.AppendLine($"Files changed: {totalFilesChanged}");
                    output.AppendLine($"Usings removed: {totalUsingsRemoved}");

                    if (preview)
                    {
                        output.AppendLine();
                        output.AppendLine("⚠️  This was a PREVIEW. No files were modified.");
                    }

                    return ToolHelpers.ToJson(
                        new
                        {
                            summary = output.ToString(),
                            filesChanged = totalFilesChanged,
                            usingsRemoved = totalUsingsRemoved,
                            wasPreview = preview,
                        }
                    );
                }
                finally
                {
                    ObjectPoolProvider.Instance.ReturnStringBuilder(output);
                }
            },
            logger,
            nameof(CleanupUsings),
            cancellationToken
        );
    }

    private static string GetSeverityEmoji(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => "❌",
            DiagnosticSeverity.Warning => "⚠️",
            DiagnosticSeverity.Info => "ℹ️",
            DiagnosticSeverity.Hidden => "🔹",
            _ => "•",
        };
}

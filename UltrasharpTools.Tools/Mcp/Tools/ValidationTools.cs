

using ModelContextProtocol;

using UltrasharpTools.Tools.Mcp;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for code validation and diagnostics.
/// Provides file and directory validation with Roslyn analyzers.
/// </summary>
public class ValidationToolsLogCategory { }

[McpServerToolType]
public static partial class ValidationTools
{
    /// <summary>
    /// Validate a single C# file with Roslyn analyzers.
    /// </summary>
    [McpServerTool(Name = "validate_file", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Validate C# file with Roslyn analyzers. Returns diagnostics (errors, warnings, info) with locations. " +
        "Useful for checking code quality before/after modifications.")]
    public static async Task<object> ValidateFile(
        ISolutionManager solutionManager,
        ILogger<ValidationToolsLogCategory> logger,

        [Description("Absolute path to .cs file to validate")]
        string filePath,

        [Description("Minimum severity to report (Error, Warning, Info, Hidden)")]
        string minSeverity = "Warning",

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            ErrorHandlingHelpers.ValidateStringParameter(filePath, nameof(filePath), logger);

            if (!File.Exists(filePath))
            {
                throw new McpException($"File not found: {filePath}");
            }

            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new McpException("Only .cs files are supported");
            }

            logger.LogInformation("Validating file: {FilePath}", filePath);

            // Parse severity
            if (!Enum.TryParse<DiagnosticSeverity>(minSeverity, ignoreCase: true, out var minSeverityEnum))
            {
                throw new McpException($"Invalid severity: {minSeverity}. Valid values: Error, Warning, Info, Hidden");
            }

            // Get document
            var document = await GetDocumentByPathAsync(solutionManager, filePath, cancellationToken);
            if (document == null)
            {
                throw new McpException($"File not found in loaded solution: {filePath}");
            }

            // Get diagnostics
            var diagnostics = await GetDiagnosticsAsync(document, minSeverityEnum, cancellationToken);

            // Format results
            var problems = diagnostics.Select(d => new
            {
                severity = d.Severity.ToString(),
                message = d.GetMessage(),
                ruleId = d.Id,
                location = d.Location.IsInSource ? new
                {
                    line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    column = d.Location.GetLineSpan().StartLinePosition.Character + 1,
                    endLine = d.Location.GetLineSpan().EndLinePosition.Line + 1,
                    endColumn = d.Location.GetLineSpan().EndLinePosition.Character + 1
                } : null,
                descriptor = d.Descriptor?.Id ?? d.Id
            }).ToList();

            var summary = new
            {
                errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning),
                info = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info),
                hidden = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Hidden)
            };

            return ToolHelpers.ToJson(new
            {
                filePath,
                totalProblems = problems.Count,
                summary,
                problems = problems.Take(100), // Limit to 100 problems
                truncated = problems.Count > 100
            });

        }, logger, nameof(ValidateFile), cancellationToken);
    }

    /// <summary>
    /// Batch validate all C# files in a directory.
    /// </summary>
    [McpServerTool(Name = "validate_directory", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Batch validate all .cs files in directory with Roslyn analyzers. Processes files in parallel for performance. " +
        "Returns aggregated statistics and per-file reports.")]
    public static async Task<object> ValidateDirectory(
        ISolutionManager solutionManager,
        ILogger<ValidationToolsLogCategory> logger,

        [Description("Directory path to validate (absolute)")]
        string directoryPath,

        [Description("Search subdirectories (default: true)")]
        bool recursive = true,

        [Description("Minimum severity to report (Error, Warning, Info, Hidden)")]
        string minSeverity = "Warning",

        [Description("Maximum files to process (1-1000, default: 100)")]
        int maxFiles = 100,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            ErrorHandlingHelpers.ValidateStringParameter(directoryPath, nameof(directoryPath), logger);

            if (!Directory.Exists(directoryPath))
            {
                throw new McpException($"Directory not found: {directoryPath}");
            }

            if (maxFiles < 1 || maxFiles > 1000)
            {
                throw new McpException("maxFiles must be between 1 and 1000");
            }

            // Parse severity
            if (!Enum.TryParse<DiagnosticSeverity>(minSeverity, ignoreCase: true, out var minSeverityEnum))
            {
                throw new McpException($"Invalid severity: {minSeverity}");
            }

            logger.LogInformation("Validating directory: {Directory} (recursive={Recursive}, maxFiles={MaxFiles})",
                directoryPath, recursive, maxFiles);

            // Find C# files
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, "*.cs", searchOption)
                .Take(maxFiles)
                .ToList();

            if (files.Count == 0)
            {
                return ToolHelpers.ToJson(new
                {
                    directoryPath,
                    totalFiles = 0,
                    message = "No .cs files found in directory"
                });
            }

            // Validate files (parallel processing with limited concurrency)
            var batchSize = 10;
            var reports = new List<object>();
            var totalErrors = 0;
            var totalWarnings = 0;
            var totalInfo = 0;

            for (int i = 0; i < files.Count; i += batchSize)
            {
                var batch = files.Skip(i).Take(batchSize).ToList();
                var batchTasks = batch.Select<string, Task<object>>(async file =>
                {
                    try
                    {
                        var document = await GetDocumentByPathAsync(solutionManager, file, cancellationToken);
                        if (document == null)
                        {
                            return new
                            {
                                filePath = file,
                                status = "not_in_solution",
                                errors = 0,
                                warnings = 0,
                                info = 0
                            };
                        }

                        var diagnostics = await GetDiagnosticsAsync(document, minSeverityEnum, cancellationToken);

                        return new
                        {
                            filePath = file,
                            status = "validated",
                            errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                            warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning),
                            info = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info)
                        };
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to validate file: {File}", file);
                        return new
                        {
                            filePath = file,
                            status = "error",
                            errors = 0,
                            warnings = 0,
                            info = 0,
                            error = ex.Message
                        };
                    }
                });

                var batchResults = await Task.WhenAll(batchTasks);
                reports.AddRange(batchResults);

                // Aggregate stats
                foreach (dynamic result in batchResults)
                {
                    totalErrors += result.errors;
                    totalWarnings += result.warnings;
                    totalInfo += result.info;
                }
            }

            // Find most problematic files
            var problemFiles = reports
                .Cast<dynamic>()
                .Where(r => r.errors > 0 || r.warnings > 0)
                .OrderByDescending(r => r.errors + r.warnings)
                .Take(20)
                .ToList();

            return ToolHelpers.ToJson(new
            {
                directoryPath,
                totalFiles = files.Count,
                aggregated = new
                {
                    totalErrors,
                    totalWarnings,
                    totalInfo,
                    filesWithProblems = problemFiles.Count
                },
                topProblematicFiles = problemFiles,
                allFiles = reports
            });

        }, logger, nameof(ValidateDirectory), cancellationToken);
    }

    /// <summary>
    /// Compare validation results before/after modification.
    /// </summary>
    [McpServerTool(Name = "compare_validation", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Compare validation results before and after code changes. Useful for measuring code quality improvement. " +
        "Pass results from validate_file tool to track errors fixed/introduced.")]
    public static Task<object> CompareValidation(
        ILogger<ValidationToolsLogCategory> logger,

        [Description("Errors count before changes")]
        int errorsBefore,

        [Description("Warnings count before changes")]
        int warningsBefore,

        [Description("Errors count after changes")]
        int errorsAfter,

        [Description("Warnings count after changes")]
        int warningsAfter,

        CancellationToken cancellationToken = default)
    {
        return ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(() =>
        {
            logger.LogInformation("Comparing validation results: before({Eb}/{Wb}) vs after({Ea}/{Wa})",
                errorsBefore, warningsBefore, errorsAfter, warningsAfter);

            var errorsFixed = errorsBefore - errorsAfter;
            var warningsFixed = warningsBefore - warningsAfter;
            var errorsIntroduced = errorsAfter - errorsBefore;
            var warningsIntroduced = warningsAfter - warningsBefore;

            var netErrorChange = errorsAfter - errorsBefore;
            var netWarningChange = warningsAfter - warningsBefore;
            var totalProblemsBefore = errorsBefore + warningsBefore;
            var totalProblemsAfter = errorsAfter + warningsAfter;
            var netChange = totalProblemsAfter - totalProblemsBefore;

            // Determine overall status
            string status;
            if (netChange < 0)
                status = "improved";
            else if (netChange > 0)
                status = "degraded";
            else
                status = "unchanged";

            var result = new
            {
                before = new { errors = errorsBefore, warnings = warningsBefore, total = totalProblemsBefore },
                after = new { errors = errorsAfter, warnings = warningsAfter, total = totalProblemsAfter },
                changes = new
                {
                    errorsFixed = errorsFixed > 0 ? errorsFixed : 0,
                    warningsFixed = warningsFixed > 0 ? warningsFixed : 0,
                    errorsIntroduced = errorsIntroduced > 0 ? errorsIntroduced : 0,
                    warningsIntroduced = warningsIntroduced > 0 ? warningsIntroduced : 0,
                    netErrorChange,
                    netWarningChange,
                    netTotalChange = netChange
                },
                status,
                message = GenerateComparisonMessage(status, netChange, errorsFixed, warningsFixed)
            };

            return Task.FromResult((object)ToolHelpers.ToJson(result));

        }, logger, nameof(CompareValidation), cancellationToken);
    }

    // ==================== Helper Methods ====================

    private static async Task<Document?> GetDocumentByPathAsync(
        ISolutionManager solutionManager,
        string filePath,
        CancellationToken cancellationToken)
    {
        var solution = solutionManager.CurrentWorkspace?.CurrentSolution;
        if (solution == null)
        {
            throw new InvalidOperationException("No solution loaded");
        }

        // Find document by file path
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

    private static async Task<List<Diagnostic>> GetDiagnosticsAsync(
        Document document,
        DiagnosticSeverity minSeverity,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
        {
            return new List<Diagnostic>();
        }

        // Get all diagnostics
        var allDiagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken);

        // Filter by severity
        var filtered = allDiagnostics
            .Where(d => d.Severity >= minSeverity)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToList();

        return filtered;
    }

    private static string GenerateComparisonMessage(string status, int netChange, int errorsFixed, int warningsFixed)
    {
        return status switch
        {
            "improved" => $"✅ Code quality improved! Fixed {errorsFixed} errors and {warningsFixed} warnings (net: {-netChange} problems resolved)",
            "degraded" => $"⚠️ Code quality degraded. {Math.Abs(netChange)} new problems introduced",
            _ => "Code quality unchanged"
        };
    }
}

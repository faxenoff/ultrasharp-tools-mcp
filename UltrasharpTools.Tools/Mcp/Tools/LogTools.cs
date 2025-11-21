

using ModelContextProtocol;
using UltrasharpTools.Tools.Mcp;

using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Infrastructure;
using LogLevel = UltrasharpTools.Tools.Models.LogLevel;

namespace UltrasharpTools.Tools.Mcp.Tools;

// Marker class for ILogger<T> category
public class LogToolsLogCategory { }

[McpServerToolType]
public static partial class LogTools
{
    /// <summary>
    /// Analyzes log files with automatic format detection and efficient searching.
    /// </summary>
    [McpServerTool(
    Name = "analyze_logs",
    Idempotent = true,
    ReadOnly = true,
    Destructive = false,
    OpenWorld = false
    )]
    [Description(
    "Analyzes log files with automatic format detection (ECS/JSON, PlainText, Logcat, WebServer, XML). " +
    "Efficiently searches for keywords, log levels, and status codes without loading entire file into memory. " +
    "Returns brief summary by default (timestamp, level, message, stacktrace, url/path) with context lines. " +
    "Supports pagination to handle large log files."
    )]
    public static async Task<object> AnalyzeLogs(
    ILogAnalysisService logAnalysisService,
    ILogger<LogToolsLogCategory> logger,
    [Description("Path to the log file to analyze")]
string filePath,
    [Description("Optional: Keywords to search for (case-insensitive). Searches in message and stacktrace.")]
List<string>? keywords = null,
    [Description("Optional: Log levels to filter (Verbose, Debug, Info, Warning, Error, Fatal)")]
List<string>? levels = null,
    [Description("Optional: HTTP status codes to filter (e.g., 404, 500)")]
List<int>? statusCodes = null,
    [Description("Number of context lines before each match. Default: 5")]
int contextBefore = 5,
    [Description("Number of context lines after each match. Default: 5")]
int contextAfter = 5,
    [Description("Detail level: Brief (default) or Full. Brief includes only time, level, message, stacktrace, url/path.")]
string detailLevel = "Brief",
    [Description("Number of results to skip for pagination. Default: 0")]
int skip = 0,
    [Description("Number of results to return. Default: 100")]
int take = 100,
    CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
        async () =>
        {
            ErrorHandlingHelpers.ValidateStringParameter(filePath, nameof(filePath), logger);

            logger.LogInformation(
    "Analyzing log file: {FilePath}, Keywords: {Keywords}, Levels: {Levels}",
    filePath,
    keywords != null ? string.Join(", ", keywords) : "none",
    levels != null ? string.Join(", ", levels) : "all"
    );

            // Parse detail level
            var parsedDetailLevel = detailLevel.Equals("Full", StringComparison.OrdinalIgnoreCase)
    ? LogDetailLevel.Full
    : LogDetailLevel.Brief;

            // Parse log levels
            List<LogLevel>? parsedLevels = null;
            if (levels?.Count > 0)
            {
                parsedLevels = levels.Select(ParseLogLevel).Where(l => l.HasValue).Select(l => l!.Value).ToList();
            }

            // Create search criteria
            var criteria = new LogSearchCriteria
            {
                Keywords = keywords,
                Levels = parsedLevels,
                StatusCodes = statusCodes,
                ContextLinesBefore = contextBefore,
                ContextLinesAfter = contextAfter,
                Skip = skip,
                Take = take
            };

            // Analyze log file
            var result = await logAnalysisService.AnalyzeLogFileAsync(
    filePath,
    criteria,
    parsedDetailLevel,
    cancellationToken
    );

            // Format result
            var formatted = FormatLogAnalysisResult(result);

            logger.LogInformation(
    "Log analysis completed: {Format}, {Matches} matches, {Returned} returned",
    result.DetectedFormat,
    result.TotalMatches,
    result.Entries.Count
    );

            return ToolHelpers.ToJson(formatted);
        },
        logger,
        nameof(AnalyzeLogs),
        cancellationToken
        );
    }

    private static object FormatLogAnalysisResult(LogAnalysisResult result)
    {
        var formatted = new Dictionary<string, object>
        {
            ["filePath"] = result.FilePath,
            ["format"] = result.DetectedFormat.ToString(),
            ["totalMatches"] = result.TotalMatches,
            ["returned"] = result.Entries.Count,
            ["skip"] = result.Skip,
            ["take"] = result.Take,
            ["hasMore"] = result.HasMore
        };

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            formatted["error"] = result.ErrorMessage;
            return formatted;
        }

        // Format as text
        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        try
        {
            sb.AppendLine();
            sb.AppendLine($"Log Analysis: {Path.GetFileName(result.FilePath)}");
            sb.AppendLine(new string('═', 80));
            sb.AppendLine($"Format: {result.DetectedFormat}");
            sb.AppendLine($"Matches: {result.TotalMatches} (showing {result.Entries.Count})");

            if (result.HasMore)
            {
                sb.AppendLine($"⚠ More results available. Use skip={result.Skip + result.Take} to see next page.");
            }

            sb.AppendLine();

            foreach (var entry in result.Entries)
            {
                sb.AppendLine(new string('─', 80));
                sb.AppendLine($"Line {entry.LineNumber}:");

                if (entry.Timestamp.HasValue)
                {
                    sb.AppendLine($"  🕐 {entry.Timestamp.Value:yyyy-MM-dd HH:mm:ss.fff}");
                }

                if (entry.Level.HasValue)
                {
                    var levelIcon = entry.Level.Value switch
                    {
                        LogLevel.Error or LogLevel.Fatal => "🔴",
                        LogLevel.Warning => "🟡",
                        LogLevel.Info => "🔵",
                        LogLevel.Debug => "🟢",
                        _ => "⚪"
                    };
                    sb.AppendLine($"  {levelIcon} [{entry.Level.Value}]");
                }

                if (entry.StatusCode.HasValue)
                {
                    var statusIcon = entry.StatusCode.Value >= 500 ? "🔴" :
                    entry.StatusCode.Value >= 400 ? "🟡" : "🟢";
                    sb.AppendLine($"  {statusIcon} HTTP {entry.StatusCode.Value}");
                }

                if (!string.IsNullOrEmpty(entry.Path) || !string.IsNullOrEmpty(entry.Url))
                {
                    sb.AppendLine($"  🔗 {entry.Path ?? entry.Url}");
                }

                if (!string.IsNullOrEmpty(entry.Source))
                {
                    sb.AppendLine($"  📦 Source: {entry.Source}");
                }

                sb.AppendLine($"  📝 {entry.Message}");

                if (!string.IsNullOrEmpty(entry.StackTrace))
                {
                    sb.AppendLine("  ⚠️ Stack Trace:");
                    var stackLines = entry.StackTrace.Split('\n');
                    foreach (var line in stackLines.Take(5)) // Limit stacktrace lines
                    {
                        sb.AppendLine($"     {line.Trim()}");
                    }
                    if (stackLines.Length > 5)
                    {
                        sb.AppendLine($"     ... ({stackLines.Length - 5} more lines)");
                    }
                }

                // Context before
                if (entry.ContextBefore?.Count > 0)
                {
                    sb.AppendLine("  ⬆️ Context before:");
                    foreach (var line in entry.ContextBefore)
                    {
                        sb.AppendLine($"     {line}");
                    }
                }

                // Context after
                if (entry.ContextAfter?.Count > 0)
                {
                    sb.AppendLine("  ⬇️ Context after:");
                    foreach (var line in entry.ContextAfter)
                    {
                        sb.AppendLine($"     {line}");
                    }
                }

                sb.AppendLine();
            }

            formatted["analysis"] = sb.ToString();
        }
        finally
        {
            ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        }

        // Include raw entries for programmatic access
        formatted["entries"] = result.Entries.Select(e => new
        {
            lineNumber = e.LineNumber,
            timestamp = e.Timestamp?.ToString("O"),
            level = e.Level?.ToString(),
            message = e.Message,
            source = e.Source,
            stackTrace = e.StackTrace,
            url = e.Url,
            path = e.Path,
            statusCode = e.StatusCode,
            contextBefore = e.ContextBefore,
            contextAfter = e.ContextAfter,
            additionalFields = e.AdditionalFields
        }).ToList();

        return formatted;
    }

    private static LogLevel? ParseLogLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "VERBOSE" or "V" => LogLevel.Verbose,
            "DEBUG" or "D" => LogLevel.Debug,
            "INFO" or "I" or "INFORMATION" => LogLevel.Info,
            "WARNING" or "WARN" or "W" => LogLevel.Warning,
            "ERROR" or "ERR" or "E" => LogLevel.Error,
            "FATAL" or "F" or "CRITICAL" => LogLevel.Fatal,
            _ => null
        };
    }
}

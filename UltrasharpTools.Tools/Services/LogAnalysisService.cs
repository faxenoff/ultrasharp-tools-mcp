using System.Xml.Linq;
using UltrasharpTools.Tools.Models;
using LogLevel = UltrasharpTools.Tools.Models.LogLevel;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Service for analyzing log files with automatic format detection.
/// Supports ECS (JSON), PlainText, Logcat, WebServer, and XML formats.
/// </summary>
public sealed partial class LogAnalysisService : ILogAnalysisService
{
    private const int FormatDetectionLines = 10;
    private const int MaxLineLengthForRead = 100000; // 100KB per line max

    // Regex patterns for log parsing (compiled for performance)
    [GeneratedRegex(@"^\{.*\}$")]
    private static partial Regex JsonLineRegex();

    [GeneratedRegex(
        @"^(\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3})\s+(\d+)\s+(\d+)\s+([VDIWEF])\s+(.+?)\s*:\s*(.*)$"
    )]
    private static partial Regex LogcatRegex();

    [GeneratedRegex(
        @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\s+-\s+-\s+\[([^\]]+)\]\s+""([A-Z]+)\s+([^\s]+)\s+HTTP/[\d.]+""\s+(\d{3})\s+(\d+)"
    )]
    private static partial Regex WebServerRegex();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+\[([^\]]+)\]\s*(.*)$")]
    private static partial Regex PlainTextRegex();

    [GeneratedRegex(@"\b(ERROR|EXCEPTION|FAIL|FATAL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorKeywordRegex();

    public async Task<LogAnalysisResult> AnalyzeLogFileAsync(
        string filePath,
        LogSearchCriteria criteria,
        LogDetailLevel detailLevel = LogDetailLevel.Brief,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(filePath))
        {
            return new LogAnalysisResult
            {
                FilePath = filePath,
                DetectedFormat = LogFormat.Unknown,
                Entries = new List<LogEntry>(),
                TotalMatches = 0,
                Skip = criteria.Skip,
                Take = criteria.Take,
                HasMore = false,
                ErrorMessage = $"File not found: {filePath}",
            };
        }

        try
        {
            // Detect format
            var format = await DetectLogFormatAsync(filePath, cancellationToken);

            // Parse log file
            var allMatches = await ParseLogFileAsync(filePath, format, criteria, cancellationToken);

            // Apply pagination
            var skip = criteria.Skip;
            var take = criteria.Take;
            var entries = allMatches.Skip(skip).Take(take).ToList();

            // Filter fields based on detail level
            if (detailLevel == LogDetailLevel.Brief)
            {
                entries = entries.Select(e => CreateBriefEntry(e)).ToList();
            }

            return new LogAnalysisResult
            {
                FilePath = filePath,
                DetectedFormat = format,
                Entries = entries,
                TotalMatches = allMatches.Count,
                Skip = skip,
                Take = take,
                HasMore = allMatches.Count > skip + take,
            };
        }
        catch (Exception ex)
        {
            return new LogAnalysisResult
            {
                FilePath = filePath,
                DetectedFormat = LogFormat.Unknown,
                Entries = new List<LogEntry>(),
                TotalMatches = 0,
                Skip = criteria.Skip,
                Take = criteria.Take,
                HasMore = false,
                ErrorMessage = $"Error analyzing log: {ex.Message}",
            };
        }
    }

    public async Task<LogFormat> DetectLogFormatAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var lines = new List<string>();
            using var reader = new StreamReader(filePath, Encoding.UTF8);

            // Read first N lines for format detection
            for (int i = 0; i < FormatDetectionLines; i++)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break; // End of stream
                }
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line.Trim());
                }
            }

            if (lines.Count == 0)
            {
                return LogFormat.Unknown;
            }

            // Check for JSON (ECS format)
            if (lines.Any(l => JsonLineRegex().IsMatch(l)))
            {
                try
                {
                    var firstJson = lines.First(l => JsonLineRegex().IsMatch(l));
                    var doc = JsonDocument.Parse(firstJson);
                    if (
                        doc.RootElement.TryGetProperty("@timestamp", out _)
                        || doc.RootElement.TryGetProperty("log.level", out _)
                    )
                    {
                        return LogFormat.ECS;
                    }
                }
                catch
                {
                    // Not valid JSON or not ECS format
                }
            }

            // Check for XML
            if (lines.Any(l => l.StartsWith("<") && l.Contains("<log") || l.Contains("<entry")))
            {
                return LogFormat.XML;
            }

            // Check for Logcat format (MM-DD HH:MM:SS.mmm PID TID LEVEL TAG : message)
            if (lines.Any(l => LogcatRegex().IsMatch(l)))
            {
                return LogFormat.Logcat;
            }

            // Check for Web Server format (IP - - [timestamp] "METHOD url" status size)
            if (lines.Any(l => WebServerRegex().IsMatch(l)))
            {
                return LogFormat.WebServer;
            }

            // Check for PlainText format (YYYY-MM-DD HH:MM:SS [LEVEL] message)
            if (lines.Any(l => PlainTextRegex().IsMatch(l)))
            {
                return LogFormat.PlainText;
            }

            // Default to PlainText for unrecognized formats
            return LogFormat.PlainText;
        }
        catch
        {
            return LogFormat.Unknown;
        }
    }

    private async Task<List<LogEntry>> ParseLogFileAsync(
        string filePath,
        LogFormat format,
        LogSearchCriteria criteria,
        CancellationToken cancellationToken
    )
    {
        var matches = new List<LogEntry>();
        int requiredMatches = criteria.Skip + criteria.Take;
        bool needsContextLines = criteria.ContextLinesBefore > 0 || criteria.ContextLinesAfter > 0;

        // If context lines are needed, we still need to buffer some lines, but not the entire file
        if (needsContextLines)
        {
            // Use sliding window approach with limited buffer
            return await ParseWithContextAsync(
                filePath,
                format,
                criteria,
                requiredMatches,
                cancellationToken
            );
        }

        // Streaming mode: constant memory usage, early exit when we have enough matches
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920
        );
        using var reader = new StreamReader(stream, Encoding.UTF8);

        int lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLengthForRead)
            {
                continue;
            }

            LogEntry? entry = format switch
            {
                LogFormat.ECS => ParseEcsLine(line, lineNumber),
                LogFormat.PlainText => ParsePlainTextLine(line, lineNumber),
                LogFormat.Logcat => ParseLogcatLine(line, lineNumber),
                LogFormat.WebServer => ParseWebServerLine(line, lineNumber),
                LogFormat.XML => null, // XML parsing requires full document context
                _ => null,
            };

            if (entry == null)
            {
                continue;
            }

            // Apply search criteria
            if (!MatchesCriteria(entry, criteria))
            {
                continue;
            }

            matches.Add(entry);

            // Early exit: stop reading once we have enough matches for pagination
            if (matches.Count >= requiredMatches)
            {
                break;
            }
        }

        return matches;
    }

    /// <summary>
    /// Parse log file with context lines support using sliding window approach.
    /// More memory intensive than streaming mode, but still better than loading entire file.
    /// </summary>
    private async Task<List<LogEntry>> ParseWithContextAsync(
        string filePath,
        LogFormat format,
        LogSearchCriteria criteria,
        int requiredMatches,
        CancellationToken cancellationToken
    )
    {
        var matches = new List<LogEntry>();
        var matchedLineIndices = new List<(int LineIndex, LogEntry Entry)>();

        // Use circular buffer for context lines (limited memory)
        int maxContextWindow = Math.Max(criteria.ContextLinesBefore, criteria.ContextLinesAfter);
        var lineBuffer = new Queue<(int LineNumber, string Line)>(maxContextWindow * 2 + 100);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920
        );
        using var reader = new StreamReader(stream, Encoding.UTF8);

        int lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            // Add to circular buffer
            lineBuffer.Enqueue((lineNumber, line));

            // Keep buffer size limited
            if (lineBuffer.Count > maxContextWindow * 2 + 100)
            {
                lineBuffer.Dequeue();
            }

            if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLengthForRead)
            {
                continue;
            }

            LogEntry? entry = format switch
            {
                LogFormat.ECS => ParseEcsLine(line, lineNumber),
                LogFormat.PlainText => ParsePlainTextLine(line, lineNumber),
                LogFormat.Logcat => ParseLogcatLine(line, lineNumber),
                LogFormat.WebServer => ParseWebServerLine(line, lineNumber),
                LogFormat.XML => null,
                _ => null,
            };

            if (entry == null)
            {
                continue;
            }

            if (!MatchesCriteria(entry, criteria))
            {
                continue;
            }

            matchedLineIndices.Add((lineNumber, entry));

            // Early exit for performance
            if (matchedLineIndices.Count >= requiredMatches)
            {
                // Read a few more lines for afterContext of the last match
                int additionalLines = criteria.ContextLinesAfter;
                for (
                    int i = 0;
                    i < additionalLines
                        && await reader.ReadLineAsync(cancellationToken) is { } extraLine;
                    i++
                )
                {
                    lineBuffer.Enqueue((++lineNumber, extraLine));
                }
                break;
            }
        }

        // Add context lines to matched entries
        foreach (var (matchLineNumber, entry) in matchedLineIndices)
        {
            var bufferedLines = lineBuffer
                .Where(x =>
                    Math.Abs(x.LineNumber - matchLineNumber)
                    <= Math.Max(criteria.ContextLinesBefore, criteria.ContextLinesAfter)
                )
                .OrderBy(x => x.LineNumber)
                .ToList();

            var contextBefore = bufferedLines
                .Where(x =>
                    x.LineNumber < matchLineNumber
                    && x.LineNumber >= matchLineNumber - criteria.ContextLinesBefore
                )
                .Select(x => x.Line)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var contextAfter = bufferedLines
                .Where(x =>
                    x.LineNumber > matchLineNumber
                    && x.LineNumber <= matchLineNumber + criteria.ContextLinesAfter
                )
                .Select(x => x.Line)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            matches.Add(
                entry with
                {
                    ContextBefore = contextBefore.Count > 0 ? contextBefore : null,
                    ContextAfter = contextAfter.Count > 0 ? contextAfter : null,
                }
            );
        }

        return matches;
    }

    private bool MatchesCriteria(LogEntry entry, LogSearchCriteria criteria)
    {
        // Check keywords
        if (criteria.Keywords?.Count > 0)
        {
            var hasKeyword = criteria.Keywords.Any(k =>
                entry.Message.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (entry.StackTrace?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
            );

            if (!hasKeyword)
            {
                return false;
            }
        }

        // Check log levels
        if (criteria.Levels?.Count > 0 && entry.Level.HasValue)
        {
            if (!criteria.Levels.Contains(entry.Level.Value))
            {
                return false;
            }
        }

        // Check status codes
        if (criteria.StatusCodes?.Count > 0 && entry.StatusCode.HasValue)
        {
            if (!criteria.StatusCodes.Contains(entry.StatusCode.Value))
            {
                return false;
            }
        }

        // Check time range
        if (entry.Timestamp.HasValue)
        {
            if (criteria.FromTime.HasValue && entry.Timestamp.Value < criteria.FromTime.Value)
            {
                return false;
            }

            if (criteria.ToTime.HasValue && entry.Timestamp.Value > criteria.ToTime.Value)
            {
                return false;
            }
        }

        return true;
    }

    private LogEntry AddContextLines(
        LogEntry entry,
        string[] allLines,
        int currentIndex,
        int beforeLines,
        int afterLines
    )
    {
        var contextBefore = new List<string>();
        var contextAfter = new List<string>();

        // Get lines before
        for (int i = Math.Max(0, currentIndex - beforeLines); i < currentIndex; i++)
        {
            if (!string.IsNullOrWhiteSpace(allLines[i]))
            {
                contextBefore.Add(allLines[i]);
            }
        }

        // Get lines after
        for (
            int i = currentIndex + 1;
            i < Math.Min(allLines.Length, currentIndex + afterLines + 1);
            i++
        )
        {
            if (!string.IsNullOrWhiteSpace(allLines[i]))
            {
                contextAfter.Add(allLines[i]);
            }
        }

        return entry with
        {
            ContextBefore = contextBefore.Count > 0 ? contextBefore : null,
            ContextAfter = contextAfter.Count > 0 ? contextAfter : null,
        };
    }

    private LogEntry CreateBriefEntry(LogEntry entry)
    {
        return new LogEntry
        {
            LineNumber = entry.LineNumber,
            Timestamp = entry.Timestamp,
            Level = entry.Level,
            Message = entry.Message,
            StackTrace = entry.StackTrace,
            Url = entry.Url,
            Path = entry.Path,
            StatusCode = entry.StatusCode,
            Source = null, // Exclude in brief mode
            AdditionalFields = null, // Exclude in brief mode
            ContextBefore = entry.ContextBefore,
            ContextAfter = entry.ContextAfter,
            RawLine = entry.RawLine,
        };
    }

    // Parser methods for different formats (see next file part)
    private LogEntry? ParseEcsLine(string line, int lineNumber)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var entry = new LogEntry
            {
                LineNumber = lineNumber,
                Timestamp = root.TryGetProperty("@timestamp", out var ts)
                    ? DateTime.Parse(ts.GetString() ?? "")
                    : null,
                Level = root.TryGetProperty("log.level", out var lvl)
                    ? ParseLogLevel(lvl.GetString() ?? "")
                    : null,
                Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
                Source = root.TryGetProperty("service.name", out var svc) ? svc.GetString() : null,
                StackTrace = root.TryGetProperty("error.stack_trace", out var st)
                    ? st.GetString()
                    : null,
                Url = root.TryGetProperty("url.full", out var url) ? url.GetString() : null,
                Path = root.TryGetProperty("url.path", out var path) ? path.GetString() : null,
                StatusCode = root.TryGetProperty("http.response.status_code", out var status)
                    ? status.GetInt32()
                    : null,
                RawLine = line,
            };

            // Capture additional fields
            var additionalFields = new Dictionary<string, object>();
            foreach (var prop in root.EnumerateObject())
            {
                if (!IsStandardEcsField(prop.Name))
                {
                    additionalFields[prop.Name] = prop.Value.ToString();
                }
            }

            if (additionalFields.Count > 0)
            {
                entry = entry with { AdditionalFields = additionalFields };
            }

            return entry;
        }
        catch
        {
            return null;
        }
    }

    private LogEntry? ParsePlainTextLine(string line, int lineNumber)
    {
        var match = PlainTextRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var timestamp = DateTime.Parse(match.Groups[1].Value);
        var level = ParseLogLevel(match.Groups[2].Value);
        var message = match.Groups[3].Value;

        // Check if next lines are stack trace
        string? stackTrace = null;
        if (ErrorKeywordRegex().IsMatch(message))
        {
            stackTrace = message;
        }

        return new LogEntry
        {
            LineNumber = lineNumber,
            Timestamp = timestamp,
            Level = level,
            Message = message,
            StackTrace = stackTrace,
            RawLine = line,
        };
    }

    private LogEntry? ParseLogcatLine(string line, int lineNumber)
    {
        var match = LogcatRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var timestamp = ParseLogcatTimestamp(match.Groups[1].Value);
        var level = ParseLogcatLevel(match.Groups[4].Value);
        var tag = match.Groups[5].Value;
        var message = match.Groups[6].Value;

        return new LogEntry
        {
            LineNumber = lineNumber,
            Timestamp = timestamp,
            Level = level,
            Message = message,
            Source = tag,
            RawLine = line,
        };
    }

    private LogEntry? ParseWebServerLine(string line, int lineNumber)
    {
        var match = WebServerRegex().Match(line);
        if (!match.Success)
        {
            return null;
        }

        var timestamp = ParseWebServerTimestamp(match.Groups[2].Value);
        var method = match.Groups[3].Value;
        var path = match.Groups[4].Value;
        var statusCode = int.Parse(match.Groups[5].Value);

        var message = $"{method} {path}";
        var level =
            statusCode >= 500 ? LogLevel.Error
            : statusCode >= 400 ? LogLevel.Warning
            : LogLevel.Info;

        return new LogEntry
        {
            LineNumber = lineNumber,
            Timestamp = timestamp,
            Level = level,
            Message = message,
            Path = path,
            Url = path,
            StatusCode = statusCode,
            RawLine = line,
        };
    }

    // Helper methods
    private bool IsStandardEcsField(string fieldName)
    {
        return fieldName.StartsWith("@")
            || fieldName.StartsWith("log.")
            || fieldName.StartsWith("error.")
            || fieldName.StartsWith("http.")
            || fieldName.StartsWith("url.")
            || fieldName == "message"
            || fieldName == "service";
    }

    private LogLevel? ParseLogLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "VERBOSE" or "V" or "TRACE" => LogLevel.Verbose,
            "DEBUG" or "D" => LogLevel.Debug,
            "INFO" or "I" or "INFORMATION" => LogLevel.Info,
            "WARN" or "WARNING" or "W" => LogLevel.Warning,
            "ERROR" or "E" or "ERR" => LogLevel.Error,
            "FATAL" or "F" or "CRITICAL" => LogLevel.Fatal,
            _ => LogLevel.Unknown,
        };
    }

    private LogLevel? ParseLogcatLevel(string level)
    {
        return level switch
        {
            "V" => LogLevel.Verbose,
            "D" => LogLevel.Debug,
            "I" => LogLevel.Info,
            "W" => LogLevel.Warning,
            "E" => LogLevel.Error,
            "F" => LogLevel.Fatal,
            _ => LogLevel.Unknown,
        };
    }

    private DateTime? ParseLogcatTimestamp(string timestamp)
    {
        // Format: MM-DD HH:MM:SS.mmm
        try
        {
            var parts = timestamp.Split(' ');
            var dateParts = parts[0].Split('-');
            var timeParts = parts[1].Split(':');

            var now = DateTime.Now;
            var month = int.Parse(dateParts[0]);
            var day = int.Parse(dateParts[1]);
            var hour = int.Parse(timeParts[0]);
            var minute = int.Parse(timeParts[1]);
            var secondMs = timeParts[2].Split('.');
            var second = int.Parse(secondMs[0]);
            var millisecond = int.Parse(secondMs[1]);

            return new DateTime(now.Year, month, day, hour, minute, second, millisecond);
        }
        catch
        {
            return null;
        }
    }

    private DateTime? ParseWebServerTimestamp(string timestamp)
    {
        // Format: DD/Mon/YYYY:HH:MM:SS +ZZZZ
        try
        {
            // Replace first colon with space (between date and time)
            var colonIndex = timestamp.IndexOf(':');
            if (colonIndex >= 0)
            {
                timestamp =
                    string.Concat(timestamp.AsSpan(0, colonIndex), " ", timestamp.AsSpan(colonIndex + 1));
            }
            return DateTime.Parse(timestamp);
        }
        catch
        {
            return null;
        }
    }
}

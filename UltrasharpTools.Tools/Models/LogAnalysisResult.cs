namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Result of log file analysis.
/// </summary>
public sealed class LogAnalysisResult
{
    public required string FilePath { get; init; }
    public required LogFormat DetectedFormat { get; init; }
    public required List<LogEntry> Entries { get; init; }
    public int TotalMatches { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
    public bool HasMore { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Supported log formats.
/// </summary>
public enum LogFormat
{
    Unknown,
    ECS, // Elastic Common Schema (JSON)
    PlainText, // Plain text logs
    Logcat, // Android Logcat
    WebServer, // Apache/Nginx access logs
    XML, // XML format logs
}

/// <summary>
/// Single log entry.
/// </summary>
public sealed record LogEntry
{
    public required int LineNumber { get; init; }
    public DateTime? Timestamp { get; init; }
    public LogLevel? Level { get; init; }
    public required string Message { get; init; }
    public string? Source { get; init; }
    public string? StackTrace { get; init; }
    public string? Url { get; init; }
    public string? Path { get; init; }
    public int? StatusCode { get; init; }
    public Dictionary<string, object>? AdditionalFields { get; init; }
    public List<string>? ContextBefore { get; init; }
    public List<string>? ContextAfter { get; init; }
    public required string RawLine { get; init; }
}

/// <summary>
/// Log level/severity.
/// </summary>
public enum LogLevel
{
    Verbose,
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
    Unknown,
}

/// <summary>
/// Log search criteria.
/// </summary>
public sealed class LogSearchCriteria
{
    public List<string>? Keywords { get; init; }
    public List<LogLevel>? Levels { get; init; }
    public List<int>? StatusCodes { get; init; }
    public DateTime? FromTime { get; init; }
    public DateTime? ToTime { get; init; }
    public bool IncludeStackTrace { get; init; } = true;
    public int ContextLinesBefore { get; init; } = 5;
    public int ContextLinesAfter { get; init; } = 5;
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 100;
}

/// <summary>
/// Detail level for log output.
/// </summary>
public enum LogDetailLevel
{
    Brief, // Time, level, message, stacktrace, url/path only
    Full, // All fields
}

namespace UltrasharpTools.Tools.Logging;

/// <summary>
/// Configuration options for file logging.
/// </summary>
public class FileLoggerOptions
{
    /// <summary>
    /// Path to the log file. Can contain date format specifiers like {Date:yyyy-MM-dd}.
    /// </summary>
    public string FilePath { get; set; } = "logs/app.log";

    /// <summary>
    /// Minimum log level to write to file.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Maximum file size in bytes before rotation. Default is 10 MB.
    /// Set to 0 to disable rotation.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum number of rotated files to keep. Default is 5.
    /// </summary>
    public int MaxRetainedFiles { get; set; } = 5;

    /// <summary>
    /// Enable timestamps in log entries. Default is true.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Enable log level in log entries. Default is true.
    /// </summary>
    public bool IncludeLogLevel { get; set; } = true;

    /// <summary>
    /// Enable category name in log entries. Default is true.
    /// </summary>
    public bool IncludeCategory { get; set; } = true;

    /// <summary>
    /// Timestamp format. Default is ISO 8601.
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Flush logs to disk immediately. Default is true (immediate).
    /// </summary>
    public bool AutoFlush { get; set; } = true;

    /// <summary>
    /// Buffer size for async writes in entries. Default is 100.
    /// When buffer is full, it will be flushed to disk.
    /// </summary>
    public int BufferSize { get; set; } = 100;
}

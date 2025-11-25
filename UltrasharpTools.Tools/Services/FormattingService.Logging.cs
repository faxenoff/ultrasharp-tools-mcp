using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class FormattingService
{
    // Formatting (3470-3489)
    [LoggerMessage(EventId = 3470, Level = LogLevel.Information,
        Message = "Starting formatting for path: {Path}, CheckOnly: {CheckOnly}")]
    private partial void LogStartingFormat(string path, bool checkOnly);

    [LoggerMessage(EventId = 3471, Level = LogLevel.Warning,
        Message = "No files found to format at path: {Path}")]
    private partial void LogNoFilesFound(string path);

    [LoggerMessage(EventId = 3472, Level = LogLevel.Information,
        Message = "Found {Count} files to check")]
    private partial void LogFilesFound(int count);

    [LoggerMessage(EventId = 3473, Level = LogLevel.Warning,
        Message = "Failed to format file: {FilePath}")]
    private partial void LogFormatFileFailed(Exception exception, string filePath);

    [LoggerMessage(EventId = 3474, Level = LogLevel.Information,
        Message = "Formatted file: {FilePath}")]
    private partial void LogFileFormatted(string filePath);

    [LoggerMessage(EventId = 3475, Level = LogLevel.Error,
        Message = "Failed to write formatted file: {FilePath}")]
    private partial void LogWriteFormatFailed(Exception exception, string filePath);

    [LoggerMessage(EventId = 3476, Level = LogLevel.Information,
        Message = "Formatting complete. Total checked: {Total}, Need formatting: {NeedFormatting}, Formatted: {Formatted}")]
    private partial void LogFormatComplete(int total, int needFormatting, int formatted);

    [LoggerMessage(EventId = 3477, Level = LogLevel.Warning,
        Message = "Failed to format C# code with Roslyn, returning original")]
    private partial void LogCSharpFormatFailed(Exception exception);

    [LoggerMessage(EventId = 3478, Level = LogLevel.Warning,
        Message = "Failed to format XML code, returning original")]
    private partial void LogXmlFormatFailed(Exception exception);

    [LoggerMessage(EventId = 3479, Level = LogLevel.Warning,
        Message = "Unsupported file extension: {Ext}")]
    private partial void LogUnsupportedExtension(string ext);

    [LoggerMessage(EventId = 3480, Level = LogLevel.Warning,
        Message = "Failed to enumerate files with extension {Ext}")]
    private partial void LogEnumerateFilesFailed(Exception exception, string ext);

    [LoggerMessage(EventId = 3481, Level = LogLevel.Warning,
        Message = "Path does not exist: {Path}")]
    private partial void LogPathNotExists(string path);
}

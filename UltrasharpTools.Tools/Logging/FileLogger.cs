using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Logging;

/// <summary>
/// Lightweight file logger with structured logging support.
/// </summary>
internal sealed partial class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerOptions _options;
    private readonly FileLoggerProcessor _processor;

    public FileLogger(string categoryName, FileLoggerOptions options, FileLoggerProcessor processor)
    {
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _options.MinimumLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        string message = formatter(state, exception);

        // Format structured log message
        if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            message = FormatStructuredMessage(message, properties);
        }

        // Build log entry
        var logEntry = BuildLogEntry(logLevel, _categoryName, message, exception);

        // Queue for async write
        _processor.EnqueueMessage(logEntry);
    }

    private string BuildLogEntry(LogLevel logLevel, string category, string message, Exception? exception)
    {
        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        try
        {
        if (_options.IncludeTimestamp)
        {
            sb.Append(DateTime.Now.ToString(_options.TimestampFormat));
            sb.Append(' ');
        }

        if (_options.IncludeLogLevel)
        {
            sb.Append('[');
            sb.Append(GetLogLevelString(logLevel));
            sb.Append(']');
            sb.Append(' ');
        }

        if (_options.IncludeCategory)
        {
            sb.Append(category);
            sb.Append(": ");
        }

        sb.Append(message);

        if (exception != null)
        {
            sb.AppendLine();
            sb.Append(exception);
        }

        return sb.ToString();
        }
        finally
        {
            ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        }
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => "UNKNOWN"
        };
    }

    private static string FormatStructuredMessage(string template, IReadOnlyList<KeyValuePair<string, object?>> properties)
    {
        // Simple structured logging: replace {ParameterName} with actual values
        var message = template;

        foreach (var kvp in properties)
        {
            if (kvp.Key == "{OriginalFormat}")
            {
                continue; // Skip metadata
            }

            var placeholder = "{" + kvp.Key + "}";
            var value = kvp.Value?.ToString() ?? "null";

            message = message.Replace(placeholder, value);
        }

        return message;
    }
}

/// <summary>
/// Processes log messages asynchronously and writes them to file.
/// </summary>
internal sealed class FileLoggerProcessor : IDisposable
{
    private const int MaxQueueSize = 10000;
    private readonly BlockingCollection<string> _messageQueue = new(MaxQueueSize);
    private readonly Thread _outputThread;
    private readonly FileLoggerOptions _options;
    private readonly object _fileLock = new();
    private StreamWriter? _streamWriter;
    private long _currentFileSize;
    private int _bufferCount;

    public FileLoggerProcessor(FileLoggerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Start background thread for writing
        _outputThread = new Thread(ProcessLogQueue)
        {
            IsBackground = true,
            Name = "FileLogger Processor"
        };
        _outputThread.Start();
    }

    public void EnqueueMessage(string message)
    {
        if (!_messageQueue.IsAddingCompleted)
        {
            try
            {
                _messageQueue.Add(message);
            }
            catch (InvalidOperationException)
            {
                // Queue was completed - ignore
            }
        }
    }

    private void ProcessLogQueue()
    {
        try
        {
            foreach (var message in _messageQueue.GetConsumingEnumerable())
            {
                WriteMessage(message);
            }
        }
        catch (Exception)
        {
            // Suppress exceptions in background thread
        }
    }

    private void WriteMessage(string message)
    {
        lock (_fileLock)
        {
            try
            {
                EnsureStreamWriterCreated();

                if (_streamWriter != null)
                {
                    _streamWriter.WriteLine(message);
                    _bufferCount++;

                    // Estimate size increase
                    _currentFileSize += Encoding.UTF8.GetByteCount(message) + Environment.NewLine.Length;

                    // Check if we need to flush
                    if (_options.AutoFlush || _bufferCount >= _options.BufferSize)
                    {
                        _streamWriter.Flush();
                        _bufferCount = 0;
                    }

                    // Check if we need to rotate
                    if (_options.MaxFileSizeBytes > 0 && _currentFileSize >= _options.MaxFileSizeBytes)
                    {
                        RotateLogFile();
                    }
                }
            }
            catch (Exception)
            {
                // Suppress file write errors to prevent cascading failures
            }
        }
    }

    private void EnsureStreamWriterCreated()
    {
        if (_streamWriter != null)
        {
            return;
        }

        var filePath = GetCurrentLogFilePath();
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open file in append mode with UTF-8 encoding (no BOM)
        var fileStream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: false);

        _streamWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false // We'll flush manually
        };

        // Get current file size
        _currentFileSize = new FileInfo(filePath).Length;
    }

    private void RotateLogFile()
    {
        try
        {
            // Close current writer
            _streamWriter?.Flush();
            _streamWriter?.Dispose();
            _streamWriter = null;

            var currentPath = GetCurrentLogFilePath();

            // Rotate existing files
            for (int i = _options.MaxRetainedFiles - 1; i >= 1; i--)
            {
                var sourcePath = GetRotatedFilePath(i);
                var destPath = GetRotatedFilePath(i + 1);

                if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, destPath, overwrite: true);
                }
            }

            // Move current log to .1
            if (File.Exists(currentPath))
            {
                File.Move(currentPath, GetRotatedFilePath(1), overwrite: true);
            }

            // Reset file size
            _currentFileSize = 0;
            _bufferCount = 0;
        }
        catch (Exception)
        {
            // If rotation fails, just continue - better to lose rotation than logs
        }
    }

    private string GetCurrentLogFilePath()
    {
        // Support {Date:format} placeholder
        var filePath = _options.FilePath;

        var dateMatch = Regex.Match(filePath, @"\{Date:([^\}]+)\}");
        if (dateMatch.Success)
        {
            var format = dateMatch.Groups[1].Value;
            var dateString = DateTime.Now.ToString(format);
            filePath = filePath.Replace(dateMatch.Value, dateString);
        }

        return filePath;
    }

    private string GetRotatedFilePath(int index)
    {
        var currentPath = GetCurrentLogFilePath();
        return currentPath + "." + index;
    }

    public void Dispose()
    {
        _messageQueue.CompleteAdding();

        try
        {
            // Wait for queue to drain (max 5 seconds)
            _outputThread.Join(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Ignore join errors
        }

        lock (_fileLock)
        {
            _streamWriter?.Flush();
            _streamWriter?.Dispose();
            _streamWriter = null;
        }

        _messageQueue.Dispose();
    }
}

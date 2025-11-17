using System.Collections.Concurrent;

namespace UltrasharpTools.Tools.Logging;

/// <summary>
/// Provider for creating file loggers.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly FileLoggerProcessor _processor;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private bool _disposed;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _processor = new FileLoggerProcessor(_options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileLoggerProvider));
        }

        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _options, _processor));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _processor.Dispose();
            _loggers.Clear();
        }
    }
}

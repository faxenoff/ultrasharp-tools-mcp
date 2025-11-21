namespace UltrasharpTools.Tools.Logging;

/// <summary>
/// Extension methods for adding file logging.
/// </summary>
public static class FileLoggerExtensions
{
    /// <summary>
    /// Adds a file logger to the logging builder.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="filePath">Path to the log file. Can contain {Date:format} placeholder.</param>
    /// <param name="minLevel">Minimum log level. Defaults to Information.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddFile(
        this ILoggingBuilder builder,
        string filePath,
        LogLevel minLevel = LogLevel.Information
    )
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var options = new FileLoggerOptions { FilePath = filePath, MinimumLevel = minLevel };

        return builder.AddFile(options);
    }

    /// <summary>
    /// Adds a file logger to the logging builder with custom options.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="options">File logger options.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, FileLoggerOptions options)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        builder.AddProvider(new FileLoggerProvider(options));

        return builder;
    }

    /// <summary>
    /// Adds a file logger to the logging builder with configuration action.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">Action to configure file logger options.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddFile(
        this ILoggingBuilder builder,
        Action<FileLoggerOptions> configure
    )
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new FileLoggerOptions();
        configure(options);

        return builder.AddFile(options);
    }
}

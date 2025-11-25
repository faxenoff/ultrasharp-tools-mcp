using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class ConfigurationService
{
    // Load configuration (5900-5903)
    [LoggerMessage(EventId = 5900, Level = LogLevel.Information,
        Message = "Loaded routing configuration from {ConfigPath}")]
    private partial void LogConfigLoaded(string configPath);

    [LoggerMessage(EventId = 5901, Level = LogLevel.Warning,
        Message = "Invalid configuration in {ConfigPath}: {Error}. Using default.")]
    private partial void LogInvalidConfig(string configPath, string error);

    [LoggerMessage(EventId = 5902, Level = LogLevel.Error,
        Message = "Failed to load configuration from {ConfigPath}. Using default.")]
    private partial void LogLoadConfigFailed(Exception exception, string configPath);

    [LoggerMessage(EventId = 5903, Level = LogLevel.Information,
        Message = "Configuration file not found at {ConfigPath}. Using default configuration.")]
    private partial void LogConfigNotFound(string configPath);

    // Save configuration (5904-5906)
    [LoggerMessage(EventId = 5904, Level = LogLevel.Information,
        Message = "Created default configuration at {ConfigPath}")]
    private partial void LogDefaultConfigCreated(string configPath);

    [LoggerMessage(EventId = 5905, Level = LogLevel.Warning,
        Message = "Failed to save default configuration to {ConfigPath}")]
    private partial void LogSaveDefaultFailed(Exception exception, string configPath);

    [LoggerMessage(EventId = 5906, Level = LogLevel.Information,
        Message = "Saved configuration to {ConfigPath}")]
    private partial void LogConfigSaved(string configPath);
}

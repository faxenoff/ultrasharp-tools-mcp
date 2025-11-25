using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class SemanticModeConfigurationLoader
{
    // Loading from explicit path (5100-5101)
    [LoggerMessage(EventId = 5100, Level = LogLevel.Information,
        Message = "Loading semantic mode config from explicit path: {Path}")]
    private partial void LogLoadingFromExplicitPath(string path);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Warning,
        Message = "Explicit config path not found: {Path}, using default")]
    private partial void LogExplicitPathNotFound(string path);

    // Config search (5102-5104)
    [LoggerMessage(EventId = 5102, Level = LogLevel.Information,
        Message = "Found semantic mode config: {Path}")]
    private partial void LogFoundConfig(string path);

    [LoggerMessage(EventId = 5103, Level = LogLevel.Warning,
        Message = "Failed to load config from {Path}, trying next location")]
    private partial void LogLoadConfigFailed(Exception exception, string path);

    [LoggerMessage(EventId = 5104, Level = LogLevel.Information,
        Message = "No semantic mode config found, using default configuration")]
    private partial void LogUsingDefaultConfig();

    // Default config save (5105-5106)
    [LoggerMessage(EventId = 5105, Level = LogLevel.Information,
        Message = "Saved default semantic mode config to {Path}")]
    private partial void LogSavedDefaultConfig(string path);

    [LoggerMessage(EventId = 5106, Level = LogLevel.Warning,
        Message = "Failed to save default config to {Path}")]
    private partial void LogSaveDefaultFailed(Exception exception, string path);

    // Load from file (5107-5108)
    [LoggerMessage(EventId = 5107, Level = LogLevel.Debug,
        Message = "Loaded semantic mode config: enabled={Enabled}, enrichmentTimeout={Timeout}s, tools={ToolCount}")]
    private partial void LogLoadedConfig(bool enabled, int timeout, int toolCount);

    [LoggerMessage(EventId = 5108, Level = LogLevel.Error,
        Message = "Failed to load semantic mode config from {Path}")]
    private partial void LogLoadConfigError(Exception exception, string path);

    // Save to file (5109-5110)
    [LoggerMessage(EventId = 5109, Level = LogLevel.Debug,
        Message = "Created directory: {Directory}")]
    private partial void LogCreatedDirectory(string directory);

    [LoggerMessage(EventId = 5110, Level = LogLevel.Information,
        Message = "Saved semantic mode config to {Path}")]
    private partial void LogSavedConfig(string path);

    // Example config (5111)
    [LoggerMessage(EventId = 5111, Level = LogLevel.Information,
        Message = "Created example semantic mode config at {Path}")]
    private partial void LogCreatedExampleConfig(string path);

    // Reload (5112-5113)
    [LoggerMessage(EventId = 5112, Level = LogLevel.Information,
        Message = "Reloading semantic mode config from {Path}")]
    private partial void LogReloading(string path);

    [LoggerMessage(EventId = 5113, Level = LogLevel.Warning,
        Message = "No config path found for reload, returning default")]
    private partial void LogNoPathForReload();

    // Merge (5114-5115)
    [LoggerMessage(EventId = 5114, Level = LogLevel.Warning,
        Message = "Merged config is invalid: {Error}, using base config")]
    private partial void LogMergedConfigInvalid(string error);

    [LoggerMessage(EventId = 5115, Level = LogLevel.Debug,
        Message = "Merged semantic mode config with overrides")]
    private partial void LogMergedConfig();
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SemanticConfigManager
{
    [LoggerMessage(EventId = 3860, Level = LogLevel.Warning,
        Message = "═══════════════════════════════════════════════════════════")]
    private partial void LogSeparator();

    [LoggerMessage(EventId = 3861, Level = LogLevel.Warning,
        Message = "No configuration found - running first-time setup")]
    private partial void LogFirstTimeSetup();

    [LoggerMessage(EventId = 3862, Level = LogLevel.Information,
        Message = "")]
    private partial void LogEmptyLine();

    [LoggerMessage(EventId = 3863, Level = LogLevel.Information,
        Message = "Configuration saved to: {Path}")]
    private partial void LogConfigSaved(string path);

    [LoggerMessage(EventId = 3864, Level = LogLevel.Warning,
        Message = "⚠ SETUP REQUIRED")]
    private partial void LogSetupRequired();

    [LoggerMessage(EventId = 3865, Level = LogLevel.Warning,
        Message = "{Instructions}")]
    private partial void LogSetupInstructions(string instructions);

    [LoggerMessage(EventId = 3866, Level = LogLevel.Information,
        Message = "✓ Ready to use!")]
    private partial void LogReadyToUse();

    [LoggerMessage(EventId = 3867, Level = LogLevel.Information,
        Message = "Loading global config from: {Path}")]
    private partial void LogLoadingGlobalConfig(string path);

    [LoggerMessage(EventId = 3868, Level = LogLevel.Information,
        Message = "Validating configuration...")]
    private partial void LogValidating();

    [LoggerMessage(EventId = 3869, Level = LogLevel.Warning,
        Message = "")]
    private partial void LogWarningEmptyLine();

    [LoggerMessage(EventId = 3870, Level = LogLevel.Error,
        Message = "Configuration is invalid and cannot be used!")]
    private partial void LogConfigInvalid();

    [LoggerMessage(EventId = 3871, Level = LogLevel.Error,
        Message = "Please fix the issues above or run: ./setup-semantic-embedding.ps1")]
    private partial void LogFixInstructions();

    [LoggerMessage(EventId = 3872, Level = LogLevel.Information,
        Message = "✓ Configuration is valid")]
    private partial void LogConfigValid();

    [LoggerMessage(EventId = 3873, Level = LogLevel.Information,
        Message = "Project config not found or force auto-detect, analyzing codebase...")]
    private partial void LogAnalyzingCodebase();

    [LoggerMessage(EventId = 3874, Level = LogLevel.Information,
        Message = "Loading project config from: {Path}")]
    private partial void LogLoadingProjectConfig(string path);

    [LoggerMessage(EventId = 3875, Level = LogLevel.Information,
        Message = "Config has 'auto' values, performing detection...")]
    private partial void LogAutoDetecting();

    [LoggerMessage(EventId = 3876, Level = LogLevel.Information,
        Message = "Auto-detection complete: Size={Size}, Files={Files}, Language={Lang} ({NonEnglish:F1}% non-English)")]
    private partial void LogAutoDetectionComplete(string size, int files, string lang, double nonEnglish);

    [LoggerMessage(EventId = 3877, Level = LogLevel.Warning,
        Message = "Auto-detection failed, using defaults")]
    private partial void LogAutoDetectionFailed(Exception exception);

    [LoggerMessage(EventId = 3878, Level = LogLevel.Information,
        Message = "ENV override: Platform = {Platform}")]
    private partial void LogEnvPlatformOverride(string platform);

    [LoggerMessage(EventId = 3879, Level = LogLevel.Information,
        Message = "ENV override: Architecture = {Arch}")]
    private partial void LogEnvArchOverride(string arch);

    [LoggerMessage(EventId = 3880, Level = LogLevel.Information,
        Message = "ENV override: TEI Endpoint = {Endpoint}")]
    private partial void LogEnvTeiEndpointOverride(string endpoint);

    [LoggerMessage(EventId = 3881, Level = LogLevel.Information,
        Message = "ENV override: Ollama Endpoint = {Endpoint}")]
    private partial void LogEnvOllamaEndpointOverride(string endpoint);

    [LoggerMessage(EventId = 3882, Level = LogLevel.Information,
        Message = "Global config saved to: {Path}")]
    private partial void LogGlobalConfigSaved(string path);

    [LoggerMessage(EventId = 3883, Level = LogLevel.Information,
        Message = "Project config saved to: {Path}")]
    private partial void LogProjectConfigSaved(string path);

    [LoggerMessage(EventId = 3884, Level = LogLevel.Information,
        Message = "═══════════════════════════════════════════════════════════")]
    private partial void LogInfoSeparator();
}

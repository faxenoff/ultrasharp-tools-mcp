using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class EmbeddingConfigValidator
{
    // Embedding config validation (3510-3519)
    [LoggerMessage(EventId = 3510, Level = LogLevel.Information,
        Message = "Validating global embedding configuration...")]
    private partial void LogValidatingConfig();

    [LoggerMessage(EventId = 3511, Level = LogLevel.Error,
        Message = "✗ Configuration validation FAILED with {Count} critical issue(s)")]
    private partial void LogValidationFailed(int count);

    [LoggerMessage(EventId = 3512, Level = LogLevel.Warning,
        Message = "⚠ Configuration is valid but has {Count} warning(s)")]
    private partial void LogValidationWarnings(int count);

    [LoggerMessage(EventId = 3513, Level = LogLevel.Information,
        Message = "✓ Configuration is valid")]
    private partial void LogValidationPassed();

    [LoggerMessage(EventId = 3514, Level = LogLevel.Information,
        Message = "✓ {Details}")]
    private partial void LogHealthDetails(string? details);
}

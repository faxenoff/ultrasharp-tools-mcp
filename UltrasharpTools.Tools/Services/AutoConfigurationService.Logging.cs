using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class AutoConfigurationService
{
    // Auto-config header (3200-3209)
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information,
        Message = "=== Auto-Configuring Embedding Platform ===")]
    private partial void LogAutoConfigHeader();

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information,
        Message = "Detecting best available platform...")]
    private partial void LogDetectingPlatform();

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information,
        Message = "")]
    private partial void LogEmptyLine();

    [LoggerMessage(EventId = 3203, Level = LogLevel.Information,
        Message = "✓ GPU Architecture: {Arch}")]
    private partial void LogGpuArchitecture(string arch);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Information,
        Message = "Checking available platforms...")]
    private partial void LogCheckingPlatforms();

    // Ollama status (3210-3219)
    [LoggerMessage(EventId = 3210, Level = LogLevel.Information,
        Message = "✓ Ollama: Available with granite-embedding model")]
    private partial void LogOllamaAvailableWithModel();

    [LoggerMessage(EventId = 3211, Level = LogLevel.Information,
        Message = "  Selected: Ollama (recommended for ease of use)")]
    private partial void LogOllamaSelectedRecommended();

    [LoggerMessage(EventId = 3212, Level = LogLevel.Warning,
        Message = "⚠ Ollama: Available but model not installed")]
    private partial void LogOllamaNoModel();

    [LoggerMessage(EventId = 3213, Level = LogLevel.Information,
        Message = "  Selected: Ollama (requires model installation)")]
    private partial void LogOllamaSelectedRequiresModel();

    [LoggerMessage(EventId = 3214, Level = LogLevel.Warning,
        Message = "✗ Ollama: Not available")]
    private partial void LogOllamaNotAvailable();

    [LoggerMessage(EventId = 3215, Level = LogLevel.Debug,
        Message = "  {Details}")]
    private partial void LogDetails(string? details);

    // TEI status (3220-3229)
    [LoggerMessage(EventId = 3220, Level = LogLevel.Information,
        Message = "✓ TEI: Available and ready")]
    private partial void LogTeiAvailable();

    [LoggerMessage(EventId = 3221, Level = LogLevel.Information,
        Message = "  Selected: TEI (high performance)")]
    private partial void LogTeiSelected();

    [LoggerMessage(EventId = 3222, Level = LogLevel.Warning,
        Message = "✗ TEI: Not available")]
    private partial void LogTeiNotAvailable();

    // No platform (3230-3239)
    [LoggerMessage(EventId = 3230, Level = LogLevel.Warning,
        Message = "✗ No embedding platform available")]
    private partial void LogNoPlatformAvailable();

    [LoggerMessage(EventId = 3231, Level = LogLevel.Information,
        Message = "Recommendation: Install Ollama for easy setup")]
    private partial void LogRecommendOllama();

    [LoggerMessage(EventId = 3232, Level = LogLevel.Information,
        Message = "  Visit: https://ollama.ai")]
    private partial void LogOllamaUrl();

    // GPU detection (3240-3249)
    [LoggerMessage(EventId = 3240, Level = LogLevel.Warning,
        Message = "GPU detection script not found, defaulting to CPU")]
    private partial void LogGpuScriptNotFound();

    [LoggerMessage(EventId = 3241, Level = LogLevel.Warning,
        Message = "Failed to start GPU detection script")]
    private partial void LogGpuScriptStartFailed();

    [LoggerMessage(EventId = 3242, Level = LogLevel.Warning,
        Message = "GPU detection script failed, defaulting to CPU")]
    private partial void LogGpuScriptFailed();

    [LoggerMessage(EventId = 3243, Level = LogLevel.Warning,
        Message = "Error detecting GPU architecture, defaulting to CPU")]
    private partial void LogGpuDetectionError(Exception exception);

    [LoggerMessage(EventId = 3244, Level = LogLevel.Debug,
        Message = "Found GPU detection script: {Path}")]
    private partial void LogGpuScriptFound(string path);
}

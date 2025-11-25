using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class EmbeddingServiceHealthChecker
{
    // TEI health (3520-3529)
    [LoggerMessage(EventId = 3520, Level = LogLevel.Debug,
        Message = "Checking TEI health: {Url}")]
    private partial void LogCheckingTeiHealth(string url);

    [LoggerMessage(EventId = 3521, Level = LogLevel.Information,
        Message = "✓ TEI is healthy at {Endpoint}")]
    private partial void LogTeiHealthy(string endpoint);

    [LoggerMessage(EventId = 3522, Level = LogLevel.Warning,
        Message = "✗ TEI health check failed: {Status}")]
    private partial void LogTeiHealthFailed(System.Net.HttpStatusCode status);

    [LoggerMessage(EventId = 3523, Level = LogLevel.Warning,
        Message = "✗ TEI connection failed: {Error}")]
    private partial void LogTeiConnectionFailed(string error);

    [LoggerMessage(EventId = 3524, Level = LogLevel.Warning,
        Message = "✗ TEI health check timed out")]
    private partial void LogTeiTimeout();

    [LoggerMessage(EventId = 3525, Level = LogLevel.Error,
        Message = "✗ Unexpected error checking TEI health")]
    private partial void LogTeiUnexpectedError(Exception exception);

    // Ollama health (3530-3539)
    [LoggerMessage(EventId = 3530, Level = LogLevel.Debug,
        Message = "Checking Ollama health: {Url}")]
    private partial void LogCheckingOllamaHealth(string url);

    [LoggerMessage(EventId = 3531, Level = LogLevel.Information,
        Message = "✓ Ollama is healthy at {Endpoint} ({Count} models)")]
    private partial void LogOllamaHealthy(string endpoint, int count);

    [LoggerMessage(EventId = 3532, Level = LogLevel.Warning,
        Message = "✗ Ollama health check failed: {Status}")]
    private partial void LogOllamaHealthFailed(System.Net.HttpStatusCode status);

    [LoggerMessage(EventId = 3533, Level = LogLevel.Warning,
        Message = "✗ Ollama connection failed: {Error}")]
    private partial void LogOllamaConnectionFailed(string error);

    [LoggerMessage(EventId = 3534, Level = LogLevel.Warning,
        Message = "✗ Ollama health check timed out")]
    private partial void LogOllamaTimeout();

    [LoggerMessage(EventId = 3535, Level = LogLevel.Error,
        Message = "✗ Unexpected error checking Ollama health")]
    private partial void LogOllamaUnexpectedError(Exception exception);

    [LoggerMessage(EventId = 3536, Level = LogLevel.Debug,
        Message = "✓ Ollama model found: {Model}")]
    private partial void LogOllamaModelFound(string model);

    [LoggerMessage(EventId = 3537, Level = LogLevel.Warning,
        Message = "✗ Ollama model not found: {Model}")]
    private partial void LogOllamaModelNotFound(string model);

    [LoggerMessage(EventId = 3538, Level = LogLevel.Warning,
        Message = "Failed to check Ollama model: {Model}")]
    private partial void LogOllamaModelCheckFailed(Exception exception, string model);
}

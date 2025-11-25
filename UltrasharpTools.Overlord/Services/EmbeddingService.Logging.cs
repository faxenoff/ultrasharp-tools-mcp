using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class EmbeddingService
{
    // General (7300)
    [LoggerMessage(EventId = 7300, Level = LogLevel.Error,
        Message = "Failed to get embedding for text (length: {Length})")]
    private partial void LogEmbeddingError(Exception exception, int length);

    // Health check (7301-7302)
    [LoggerMessage(EventId = 7301, Level = LogLevel.Debug,
        Message = "Embedding service availability: {IsAvailable}")]
    private partial void LogAvailability(bool isAvailable);

    [LoggerMessage(EventId = 7302, Level = LogLevel.Warning,
        Message = "Embedding service health check failed")]
    private partial void LogHealthCheckFailed(Exception exception);

    // Ollama (7303-7304)
    [LoggerMessage(EventId = 7303, Level = LogLevel.Warning,
        Message = "Ollama returned empty embedding")]
    private partial void LogOllamaEmptyEmbedding();

    [LoggerMessage(EventId = 7304, Level = LogLevel.Debug,
        Message = "Generated embedding via Ollama (model: {Model}): {Dimensions} dimensions")]
    private partial void LogOllamaEmbedding(string model, int dimensions);

    // TEI (7305-7306)
    [LoggerMessage(EventId = 7305, Level = LogLevel.Warning,
        Message = "TEI returned empty embedding")]
    private partial void LogTeiEmptyEmbedding();

    [LoggerMessage(EventId = 7306, Level = LogLevel.Debug,
        Message = "Generated embedding via TEI: {Dimensions} dimensions")]
    private partial void LogTeiEmbedding(int dimensions);
}

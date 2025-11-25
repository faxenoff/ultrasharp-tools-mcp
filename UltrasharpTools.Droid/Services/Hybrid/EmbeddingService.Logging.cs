using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class EmbeddingService
{
    // General (6100)
    [LoggerMessage(EventId = 6100, Level = LogLevel.Error,
        Message = "Failed to get embedding")]
    private partial void LogEmbeddingError(Exception exception);

    // Ollama (6101-6102)
    [LoggerMessage(EventId = 6101, Level = LogLevel.Warning,
        Message = "Ollama returned empty embedding")]
    private partial void LogOllamaEmptyEmbedding();

    [LoggerMessage(EventId = 6102, Level = LogLevel.Debug,
        Message = "Generated embedding via Ollama: {Dimensions} dimensions")]
    private partial void LogOllamaEmbedding(int dimensions);

    // TEI (6103-6104)
    [LoggerMessage(EventId = 6103, Level = LogLevel.Warning,
        Message = "TEI returned empty embedding")]
    private partial void LogTeiEmptyEmbedding();

    [LoggerMessage(EventId = 6104, Level = LogLevel.Debug,
        Message = "Generated embedding via TEI: {Dimensions} dimensions")]
    private partial void LogTeiEmbedding(int dimensions);
}

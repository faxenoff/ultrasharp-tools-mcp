using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class MultiProjectVectorStoreService
{
    // Initialization (7200)
    [LoggerMessage(EventId = 7200, Level = LogLevel.Information,
        Message = "MultiProjectVectorStore initialized: BasePath={BasePath}, Dimension={Dimension}")]
    private partial void LogInitialized(string basePath, int dimension);

    // Store operations (7201-7203)
    [LoggerMessage(EventId = 7201, Level = LogLevel.Debug,
        Message = "Stored vectors: {Project}/{Branch}/{File}")]
    private partial void LogStoredVectors(string project, string branch, string file);

    [LoggerMessage(EventId = 7202, Level = LogLevel.Warning,
        Message = "Delete not implemented yet: {Project}/{Branch}/{File}")]
    private partial void LogDeleteNotImplemented(string project, string branch, string file);

    [LoggerMessage(EventId = 7203, Level = LogLevel.Error,
        Message = "Failed to search in vector store")]
    private partial void LogSearchFailed(Exception exception);

    // Store creation (7204)
    [LoggerMessage(EventId = 7204, Level = LogLevel.Information,
        Message = "Created new VectorStore: {Project}/{Branch}")]
    private partial void LogStoreCreated(string project, string branch);
}

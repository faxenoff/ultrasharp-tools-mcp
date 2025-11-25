using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB;

/// <summary>
/// Source-generated logging methods for VectorDBService
/// </summary>
public sealed partial class VectorDBService {
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[VectorDBService] Handling request: {Method}")]
    private partial void LogHandlingRequest(string method);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error handling request")]
    private partial void LogErrorHandlingRequest(Exception ex);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error indexing code")]
    private partial void LogErrorIndexingCode(Exception ex);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error searching")]
    private partial void LogErrorSearching(Exception ex);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error getting status")]
    private partial void LogErrorGettingStatus(Exception ex);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error clearing")]
    private partial void LogErrorClearing(Exception ex);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Error,
        Message = "[VectorDBService] Error batch indexing")]
    private partial void LogErrorIndexingBatch(Exception ex);
}

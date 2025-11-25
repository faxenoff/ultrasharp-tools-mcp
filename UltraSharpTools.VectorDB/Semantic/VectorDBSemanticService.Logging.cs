using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB.Semantic;

public sealed partial class VectorDBSemanticService
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Indexing code from {Path}")]
    private partial void LogIndexingCode(string path);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Failed to generate embedding for {Path}")]
    private partial void LogEmbeddingFailed(string path);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Successfully indexed {Path}")]
    private partial void LogIndexingSuccess(string path);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Searching for similar code: {Query}")]
    private partial void LogSearching(string query);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Failed to generate embedding for query")]
    private partial void LogQueryEmbeddingFailed();

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Debug,
        Message = "Found {Count} similar results")]
    private partial void LogSearchResults(int count);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Clearing all embeddings")]
    private partial void LogClearing();

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Debug,
        Message = "Batch indexing {Count} items")]
    private partial void LogIndexingBatch(int count);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Debug,
        Message = "Successfully batch indexed {Count} items")]
    private partial void LogIndexingBatchSuccess(int count);
}

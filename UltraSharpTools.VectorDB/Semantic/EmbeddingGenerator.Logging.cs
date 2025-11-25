using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB.Semantic;

public sealed partial class EmbeddingGenerator
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Debug,
        Message = "Cache hit for text (length={Length})")]
    private partial void LogCacheHit(int length);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Debug,
        Message = "Generated embedding for text (length={Length}) in {ElapsedMs}ms")]
    private partial void LogEmbeddingGenerated(int length, long elapsedMs);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Debug,
        Message = "All {Count} texts found in cache")]
    private partial void LogAllCacheHits(int count);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Debug,
        Message = "Generating embeddings for {UncachedCount}/{TotalCount} texts (cache hit rate: {HitRate})")]
    private partial void LogBatchProcessing(int uncachedCount, int totalCount, string hitRate);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Debug,
        Message = "Generated {Count} embeddings in {ElapsedMs}ms ({AvgMs}ms/embedding)")]
    private partial void LogBatchGenerated(int count, long elapsedMs, string avgMs);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Information,
        Message = "Embedding cache cleared")]
    private partial void LogCacheCleared();

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Information,
        Message = "Embedding metrics reset")]
    private partial void LogMetricsReset();
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SemanticDiagnosticEnricher
{
    [LoggerMessage(EventId = 3730, Level = LogLevel.Information,
        Message = "Analyzed statistics for {Count} diagnostics in {ElapsedMs}ms")]
    private partial void LogStatisticsAnalyzed(int count, long elapsedMs);

    [LoggerMessage(EventId = 3731, Level = LogLevel.Information,
        Message = "Applied heuristic rules to {Count} clusters")]
    private partial void LogHeuristicRulesApplied(int count);

    [LoggerMessage(EventId = 3732, Level = LogLevel.Warning,
        Message = "Semantic mode provider not available, skipping clustering")]
    private partial void LogSemanticProviderNotAvailable();

    [LoggerMessage(EventId = 3733, Level = LogLevel.Warning,
        Message = "Semantic mode not available, skipping clustering. Source: {Source}")]
    private partial void LogSemanticModeNotAvailable(string source);

    [LoggerMessage(EventId = 3734, Level = LogLevel.Information,
        Message = "Semantic mode available: {Source}, Model: {Model}, Dimension: {Dimension}")]
    private partial void LogSemanticModeAvailable(string source, string? model, int dimension);

    [LoggerMessage(EventId = 3735, Level = LogLevel.Warning,
        Message = "No messages to embed for clustering")]
    private partial void LogNoMessagesToEmbed();

    [LoggerMessage(EventId = 3736, Level = LogLevel.Information,
        Message = "Generating embeddings for {ClusterCount} diagnostic types with {MessageCount} unique messages")]
    private partial void LogGeneratingEmbeddings(int clusterCount, int messageCount);

    [LoggerMessage(EventId = 3737, Level = LogLevel.Warning,
        Message = "Failed to generate embedding for message: {Message}")]
    private partial void LogEmbeddingGenerationFailed(Exception exception, string message);

    [LoggerMessage(EventId = 3738, Level = LogLevel.Information,
        Message = "Generated {EmbeddingCount} embeddings successfully")]
    private partial void LogEmbeddingsGenerated(int embeddingCount);

    [LoggerMessage(EventId = 3739, Level = LogLevel.Warning,
        Message = "No embeddings generated, skipping clustering")]
    private partial void LogNoEmbeddingsGenerated();

    [LoggerMessage(EventId = 3740, Level = LogLevel.Information,
        Message = "Computed {CentroidCount} cluster centroids")]
    private partial void LogCentroidsComputed(int centroidCount);

    [LoggerMessage(EventId = 3741, Level = LogLevel.Information,
        Message = "Found {PairCount} pairs of similar clusters (threshold: {Threshold:F2})")]
    private partial void LogSimilarClustersFound(int pairCount, double threshold);

    [LoggerMessage(EventId = 3742, Level = LogLevel.Debug,
        Message = "Increased confidence for similar clusters: {Id1} <-> {Id2} (similarity: {Similarity:F2})")]
    private partial void LogConfidenceIncreased(string id1, string id2, double similarity);

    [LoggerMessage(EventId = 3743, Level = LogLevel.Information,
        Message = "Semantic clustering complete")]
    private partial void LogSemanticClusteringComplete();

    [LoggerMessage(EventId = 3744, Level = LogLevel.Warning,
        Message = "Failed to enrich with semantic clustering")]
    private partial void LogSemanticEnrichmentFailed(Exception exception);
}

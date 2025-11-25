using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class EditorConfigGenerator
{
    [LoggerMessage(EventId = 3545, Level = LogLevel.Information,
        Message = "Starting EditorConfig generation for {ClusterCount} clusters")]
    private partial void LogStartingGeneration(int clusterCount);

    [LoggerMessage(EventId = 3546, Level = LogLevel.Information,
        Message = "Clusters analysis: {AutoApproved} auto-approved, {NeedsReview} needs review")]
    private partial void LogClustersAnalysis(int autoApproved, int needsReview);

    [LoggerMessage(EventId = 3547, Level = LogLevel.Information,
        Message = "EditorConfig generation complete. Rules: {RulesCount}, Manual review: {ReviewCount}")]
    private partial void LogGenerationComplete(int rulesCount, int reviewCount);
}

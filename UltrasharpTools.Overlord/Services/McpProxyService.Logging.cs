using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class McpProxyService
{
    // Tool execution (7600-7602)
    [LoggerMessage(EventId = 7600, Level = LogLevel.Information,
        Message = "Executing proxied tool: {ToolName}")]
    private partial void LogExecutingTool(string toolName);

    [LoggerMessage(EventId = 7601, Level = LogLevel.Debug,
        Message = "Tool {ToolName} executed successfully")]
    private partial void LogToolExecuted(string toolName);

    [LoggerMessage(EventId = 7602, Level = LogLevel.Error,
        Message = "Failed to execute tool {ToolName}")]
    private partial void LogToolExecutionFailed(Exception exception, string toolName);

    // find_duplicates (7603-7605)
    [LoggerMessage(EventId = 7603, Level = LogLevel.Debug,
        Message = "Using pre-computed vector (length: {Length})")]
    private partial void LogUsingPrecomputedVector(int length);

    [LoggerMessage(EventId = 7604, Level = LogLevel.Information,
        Message = "Computing embedding for TargetCode (length: {Length})")]
    private partial void LogComputingEmbeddingForTargetCode(int length);

    [LoggerMessage(EventId = 7605, Level = LogLevel.Debug,
        Message = "Computed embedding via EmbeddingService (dimensions: {Dim})")]
    private partial void LogComputedEmbedding(int dim);

    // view_definition (7606)
    [LoggerMessage(EventId = 7606, Level = LogLevel.Error,
        Message = "Failed to execute view_definition for {Fqn}")]
    private partial void LogViewDefinitionFailed(Exception exception, string fqn);

    // find_references (7607)
    [LoggerMessage(EventId = 7607, Level = LogLevel.Error,
        Message = "Failed to execute find_references for {Fqn}")]
    private partial void LogFindReferencesFailed(Exception exception, string fqn);

    // modify_code (7608)
    [LoggerMessage(EventId = 7608, Level = LogLevel.Error,
        Message = "Failed to execute modify_code for {Fqn}")]
    private partial void LogModifyCodeFailed(Exception exception, string fqn);

    // analyze_complexity (7609)
    [LoggerMessage(EventId = 7609, Level = LogLevel.Error,
        Message = "Failed to execute analyze_complexity for {Target}")]
    private partial void LogAnalyzeComplexityFailed(Exception exception, string target);

    // format_code (7610)
    [LoggerMessage(EventId = 7610, Level = LogLevel.Error,
        Message = "Failed to execute format_code for {Path}")]
    private partial void LogFormatCodeFailed(Exception exception, string path);

    // reindex_changed_files (7611-7613)
    [LoggerMessage(EventId = 7611, Level = LogLevel.Information,
        Message = "Reindexing {FileCount} files for {Project}/{Branch}")]
    private partial void LogReindexing(int fileCount, string project, string branch);

    [LoggerMessage(EventId = 7612, Level = LogLevel.Error,
        Message = "Failed to reindex {File}")]
    private partial void LogReindexFileFailed(Exception exception, string file);

    [LoggerMessage(EventId = 7613, Level = LogLevel.Error,
        Message = "Failed to execute reindex_changed_files")]
    private partial void LogReindexFailed(Exception exception);

    // semantic_search (7614-7615)
    [LoggerMessage(EventId = 7614, Level = LogLevel.Information,
        Message = "Computing embedding for query: {Query}")]
    private partial void LogComputingEmbeddingForQuery(string query);

    [LoggerMessage(EventId = 7615, Level = LogLevel.Error,
        Message = "Failed to execute semantic_search")]
    private partial void LogSemanticSearchFailed(Exception exception);

    // semantic_diff (7616-7617)
    [LoggerMessage(EventId = 7616, Level = LogLevel.Information,
        Message = "Computing embeddings for semantic diff")]
    private partial void LogComputingEmbeddingsForDiff();

    [LoggerMessage(EventId = 7617, Level = LogLevel.Error,
        Message = "Failed to execute semantic_diff")]
    private partial void LogSemanticDiffFailed(Exception exception);

    // detect_code_clones (7618-7619)
    [LoggerMessage(EventId = 7618, Level = LogLevel.Information,
        Message = "detect_code_clones called on Overlord - this should be routed to LOCAL. minSimilarity={MinSimilarity}, mode={Mode}, membersOnly={MembersOnly}, maxGroups={MaxGroups}")]
    private partial void LogDetectCodeClonesRedirect(double minSimilarity, string? mode, bool membersOnly, int maxGroups);

    [LoggerMessage(EventId = 7619, Level = LogLevel.Error,
        Message = "Failed to execute detect_code_clones")]
    private partial void LogDetectCodeClonesFailed(Exception exception);

    // pattern_search (7620)
    [LoggerMessage(EventId = 7620, Level = LogLevel.Error,
        Message = "Failed to execute pattern_search")]
    private partial void LogPatternSearchFailed(Exception exception);
}

namespace UltrasharpTools.Overlord.Models.Responses;

// ============================================================================
// Base Response Types
// ============================================================================

/// <summary>
/// Error response for MCP tool calls.
/// </summary>
public sealed record ErrorResponse(string Error, string? ToolName = null);

/// <summary>
/// Error response with hint for configuration issues.
/// </summary>
public sealed record ErrorWithHintResponse(string Error, string? Hint = null);

// ============================================================================
// Tool-Specific Responses
// ============================================================================

/// <summary>
/// Response for load_solution tool.
/// </summary>
public sealed record LoadSolutionResponse(
    bool Success,
    string SolutionPath,
    string Message
);

/// <summary>
/// Match result for duplicate/semantic search.
/// </summary>
public sealed record CodeMatchResult(
    string? Project,
    string? Branch,
    string? File,
    int Line,
    double Similarity,
    string? Code
);

/// <summary>
/// Response for find_duplicates tool.
/// </summary>
public sealed record FindDuplicatesResponse(
    int TargetVectorDimension,
    string? Scope,
    double Threshold,
    int MatchCount,
    IEnumerable<CodeMatchResult> Matches
);

/// <summary>
/// Response for view_definition tool.
/// </summary>
public sealed record ViewDefinitionResponse(
    string Fqn,
    string? FilePath,
    string? Source,
    int? Line = null,
    string? ResolutionMethod = null
);

/// <summary>
/// Reference location info for find_references tool.
/// Named to avoid conflict with Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation.
/// </summary>
public sealed record SymbolReferenceLocation(
    string? FilePath,
    int Line,
    int Column
);

/// <summary>
/// Response for find_references tool.
/// </summary>
public sealed record FindReferencesResponse(
    string Fqn,
    int ReferenceCount,
    IEnumerable<SymbolReferenceLocation> References
);

/// <summary>
/// Response for modify_code tool.
/// </summary>
public sealed record ModifyCodeResponse(
    bool Success,
    string Fqn,
    string Message,
    IEnumerable<string>? ChangedFiles = null,
    int Errors = 0,
    int Warnings = 0
);

/// <summary>
/// Response for analyze_complexity tool.
/// </summary>
public sealed record AnalyzeComplexityResponse(
    string Scope,
    string Target,
    Dictionary<string, object> Metrics,
    List<string> Recommendations
);

/// <summary>
/// Response for format_code tool.
/// </summary>
public sealed record FormatCodeResponse(
    int TotalFilesChecked,
    int FilesNeedingFormatting,
    int FilesFormatted,
    bool CheckOnly,
    List<string> FilesNeedingFormattingList,
    string Message
);

/// <summary>
/// Response for reindex_changed_files tool.
/// </summary>
public sealed record ReindexChangedFilesResponse(
    string Project,
    string Branch,
    int TotalFiles,
    int SuccessCount,
    int ErrorCount,
    List<string> Errors,
    string Message
);

/// <summary>
/// Response for semantic_search tool.
/// </summary>
public sealed record SemanticSearchResponse(
    string Query,
    string Scope,
    int TopK,
    double MinSimilarity,
    int ResultsCount,
    IEnumerable<CodeMatchResult> Results
);

/// <summary>
/// Response for semantic_diff tool.
/// </summary>
public sealed record SemanticDiffResponse(
    int Code1Length,
    int Code2Length,
    double SemanticSimilarity,
    string Interpretation
);

/// <summary>
/// Explanation for detect_code_clones error.
/// </summary>
public sealed record DetectCodeClonesExplanation(
    string ToolPurpose,
    string RequiresLocal,
    string Alternative
);

/// <summary>
/// Response for detect_code_clones tool (when called on wrong server).
/// </summary>
public sealed record DetectCodeClonesResponse(
    string Error,
    string ToolType,
    string Hint,
    DetectCodeClonesExplanation Explanation,
    string Recommendation
);

/// <summary>
/// Response for pattern_search tool (partial support).
/// </summary>
public sealed record PatternSearchResponse(
    string Message,
    string? Hint = null,
    string? SupportedMode = null
);

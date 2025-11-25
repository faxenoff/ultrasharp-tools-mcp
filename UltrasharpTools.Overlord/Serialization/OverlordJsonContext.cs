using System.Text.Json.Serialization;
using UltrasharpTools.Overlord.Models.Responses;

namespace UltrasharpTools.Overlord.Serialization;

/// <summary>
/// JSON Source Generator context for Overlord MCP responses.
/// Eliminates reflection-based serialization for AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
// Base response types
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ErrorWithHintResponse))]
// Tool-specific responses
[JsonSerializable(typeof(LoadSolutionResponse))]
[JsonSerializable(typeof(CodeMatchResult))]
[JsonSerializable(typeof(FindDuplicatesResponse))]
[JsonSerializable(typeof(ViewDefinitionResponse))]
[JsonSerializable(typeof(SymbolReferenceLocation))]
[JsonSerializable(typeof(FindReferencesResponse))]
[JsonSerializable(typeof(ModifyCodeResponse))]
[JsonSerializable(typeof(AnalyzeComplexityResponse))]
[JsonSerializable(typeof(FormatCodeResponse))]
[JsonSerializable(typeof(ReindexChangedFilesResponse))]
[JsonSerializable(typeof(SemanticSearchResponse))]
[JsonSerializable(typeof(SemanticDiffResponse))]
[JsonSerializable(typeof(DetectCodeClonesExplanation))]
[JsonSerializable(typeof(DetectCodeClonesResponse))]
[JsonSerializable(typeof(PatternSearchResponse))]
// Collections
[JsonSerializable(typeof(IEnumerable<CodeMatchResult>))]
[JsonSerializable(typeof(IEnumerable<SymbolReferenceLocation>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
// Input DTOs (for deserialization) - moved from private classes
[JsonSerializable(typeof(LoadSolutionArgs))]
[JsonSerializable(typeof(FindDuplicatesArgs))]
[JsonSerializable(typeof(ViewDefinitionArgs))]
[JsonSerializable(typeof(FindReferencesArgs))]
[JsonSerializable(typeof(ModifyCodeArgs))]
[JsonSerializable(typeof(AnalyzeComplexityArgs))]
[JsonSerializable(typeof(FormatCodeArgs))]
[JsonSerializable(typeof(ReindexChangedFilesArgs))]
[JsonSerializable(typeof(FileVectorData))]
[JsonSerializable(typeof(FileVectorData[]))]
[JsonSerializable(typeof(SemanticSearchArgs))]
[JsonSerializable(typeof(SemanticDiffArgs))]
[JsonSerializable(typeof(DetectCodeClonesArgs))]
[JsonSerializable(typeof(PatternSearchArgs))]
public partial class OverlordJsonContext : JsonSerializerContext { }

// ============================================================================
// Input DTOs (for deserialization)
// ============================================================================

public sealed class LoadSolutionArgs
{
    public string? SolutionPath { get; set; }
    public string? BuildConfiguration { get; set; }
}

public sealed class FindDuplicatesArgs
{
    public string? TargetCode { get; set; }
    public float[]? TargetVector { get; set; }
    public double Threshold { get; set; } = 0.7;
    public string Scope { get; set; } = "current_project";
    public int Limit { get; set; } = 10;
}

public sealed class ViewDefinitionArgs
{
    public string? Fqn { get; set; }
}

public sealed class FindReferencesArgs
{
    public string? Fqn { get; set; }
}

public sealed class ModifyCodeArgs
{
    public string? Fqn { get; set; }
    public string? NewCode { get; set; }
}

public sealed class AnalyzeComplexityArgs
{
    public string? Scope { get; set; }
    public string? Target { get; set; }
}

public sealed class FormatCodeArgs
{
    public string? Path { get; set; }
    public bool CheckOnly { get; set; } = true;
}

public sealed class ReindexChangedFilesArgs
{
    public string? Project { get; set; }
    public string? Branch { get; set; }
    public FileVectorData[]? Files { get; set; }
}

public sealed class FileVectorData
{
    public required string FilePath { get; set; }
    public float[]? Vector { get; set; }
    public string? Content { get; set; }
    public UltrasharpTools.Overlord.Models.Agent.SymbolInfoDto[]? Symbols { get; set; }
}

public sealed class SemanticSearchArgs
{
    public string? Query { get; set; }
    public string Scope { get; set; } = "solution";
    public int TopK { get; set; } = 10;
    public double MinSimilarity { get; set; } = 0.7;
}

public sealed class SemanticDiffArgs
{
    public string? Code1 { get; set; }
    public string? Code2 { get; set; }
}

public sealed class DetectCodeClonesArgs
{
    public float MinSimilarity { get; set; } = 0.85f;
    public string Mode { get; set; } = "semantic";
    public bool MembersOnly { get; set; } = true;
    public int MaxGroups { get; set; } = 20;
}

public sealed class PatternSearchArgs
{
    public string? Pattern { get; set; }
    public string? Mode { get; set; } = "semantic";
    public string? Scope { get; set; }
    public int Limit { get; set; } = 10;
}

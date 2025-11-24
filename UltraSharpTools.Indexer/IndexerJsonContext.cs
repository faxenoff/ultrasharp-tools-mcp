using System.Text.Json.Serialization;

namespace UltraSharpTools.Indexer;

/// <summary>
/// JSON source generation context for AOT compatibility
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(IndexCodeResponse))]
[JsonSerializable(typeof(SearchSimilarResponse))]
[JsonSerializable(typeof(SearchMatch))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(ClearResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class IndexerJsonContext : JsonSerializerContext
{
}

// Response types for JSON serialization
public sealed record IndexCodeResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("documentPath")] string DocumentPath
);

public sealed record SearchSimilarResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("matches")] SearchMatch[] Matches
);

public sealed record SearchMatch(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("similarity")] float Similarity,
    [property: JsonPropertyName("metadata")] string? Metadata
);

public sealed record GetStatusResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("indexed_count")] int IndexedCount,
    [property: JsonPropertyName("version")] string Version
);

public sealed record ClearResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message
);

public sealed record ErrorResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string Error
);

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace UltraSharpTools.Indexer.Semantic.Embedding.Providers;

/// <summary>
/// JSON source generation context for embedding provider types (AOT compatibility)
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
// Ollama types
[JsonSerializable(typeof(OllamaRequest))]
[JsonSerializable(typeof(OllamaResponse))]
[JsonSerializable(typeof(OllamaTagsResponse))]
[JsonSerializable(typeof(OllamaModel))]
[JsonSerializable(typeof(OllamaPullRequest))]
// TEI types
[JsonSerializable(typeof(TEIRequest))]
[JsonSerializable(typeof(TEIBatchRequest))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(float[][]))]
public partial class EmbeddingJsonContext : JsonSerializerContext
{
    // Helper methods to get JsonTypeInfo for array types
    public static JsonTypeInfo<float[]> GetSingleArrayTypeInfo()
        => (JsonTypeInfo<float[]>)Default.GetTypeInfo(typeof(float[]))!;

    public static JsonTypeInfo<float[][]> GetDoubleArrayTypeInfo()
        => (JsonTypeInfo<float[][]>)Default.GetTypeInfo(typeof(float[][]))!;
}

// Ollama types
public sealed record OllamaRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
}

public sealed record OllamaResponse
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = Array.Empty<float>();
}

public sealed record OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public OllamaModel[]? Models { get; init; }
}

public sealed record OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record OllamaPullRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

// TEI types
public sealed record TEIRequest
{
    [JsonPropertyName("inputs")]
    public string Inputs { get; init; } = string.Empty;
}

public sealed record TEIBatchRequest
{
    [JsonPropertyName("inputs")]
    public string[] Inputs { get; init; } = Array.Empty<string>();
}

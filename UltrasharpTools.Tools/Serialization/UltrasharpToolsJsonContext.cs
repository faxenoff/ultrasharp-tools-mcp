

using System.Text.Json.Serialization;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Serialization;

/// <summary>
/// JSON Source Generator context for UltrasharpTools.
/// Provides compile-time optimized serialization for hot-path types.
/// Performance: 2-5x faster than reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default
)]
// CallGraphCacheService - CRITICAL HOT PATH
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<SerializableCallerInfo>))]
[JsonSerializable(typeof(SerializableCallerInfo))]
[JsonSerializable(typeof(SerializableLocation))]
[JsonSerializable(typeof(List<SerializableLocation>))]

// SymbolCacheManager - Large data serialization
[JsonSerializable(typeof(SymbolCacheData))]
[JsonSerializable(typeof(SymbolCacheMetadata))]
[JsonSerializable(typeof(SerializableSymbolEntry))]
[JsonSerializable(typeof(List<SerializableSymbolEntry>))]
[JsonSerializable(typeof(ProjectCacheInfo))]
[JsonSerializable(typeof(List<ProjectCacheInfo>))]

// Generic cache results - common types
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]

// Primitive types for MCP responses
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(object))]
internal partial class UltrasharpToolsJsonContext : JsonSerializerContext
{
}

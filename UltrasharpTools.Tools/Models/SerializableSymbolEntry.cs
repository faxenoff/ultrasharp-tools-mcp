using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Serializable representation of SymbolIndexEntry for persistent cache
/// Excludes ISymbol reference which cannot be serialized
/// </summary>
public class SerializableSymbolEntry
{
    /// <summary>
    /// Canonical fully qualified name
    /// </summary>
    [JsonPropertyName("fqn")]
    public string CanonicalFqn { get; set; } = string.Empty;

    /// <summary>
    /// Simple name without namespace
    /// </summary>
    [JsonPropertyName("name")]
    public string SimpleName { get; set; } = string.Empty;

    /// <summary>
    /// Namespace
    /// </summary>
    [JsonPropertyName("ns")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Bitwise metadata flags (serialized as long)
    /// </summary>
    [JsonPropertyName("flags")]
    public long Flags { get; set; }

    /// <summary>
    /// Namespace depth
    /// </summary>
    [JsonPropertyName("nsDepth")]
    public byte NamespaceDepth { get; set; }

    /// <summary>
    /// Length of simple name
    /// </summary>
    [JsonPropertyName("nameLen")]
    public ushort NameLength { get; set; }

    /// <summary>
    /// Length of FQN
    /// </summary>
    [JsonPropertyName("fqnLen")]
    public ushort FqnLength { get; set; }

    /// <summary>
    /// First character (lowercase)
    /// </summary>
    [JsonPropertyName("firstChar")]
    public char FirstChar { get; set; }

    /// <summary>
    /// Hash code of simple name
    /// </summary>
    [JsonPropertyName("nameHash")]
    public int SimpleNameHashCode { get; set; }

    /// <summary>
    /// Assembly name where symbol is defined
    /// </summary>
    [JsonPropertyName("assembly")]
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Project name where symbol is defined
    /// </summary>
    [JsonPropertyName("project")]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Convert from SymbolIndexEntry
    /// </summary>
    public static SerializableSymbolEntry FromIndexEntry(SymbolIndexEntry entry, string projectName, string assemblyName)
    {
        return new SerializableSymbolEntry
        {
            CanonicalFqn = entry.CanonicalFqn,
            SimpleName = entry.SimpleName,
            Namespace = entry.Namespace,
            Flags = (long)entry.Flags,
            NamespaceDepth = entry.NamespaceDepth,
            NameLength = entry.NameLength,
            FqnLength = entry.FqnLength,
            FirstChar = entry.FirstChar,
            SimpleNameHashCode = entry.SimpleNameHashCode,
            AssemblyName = assemblyName,
            ProjectName = projectName
        };
    }
}

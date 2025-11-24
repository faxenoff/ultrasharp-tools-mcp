namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Serializable representation of FindReferences results
/// </summary>
public class CachedReferencesResult
{
    public List<CachedReferenceLocation> Locations { get; set; } = new();
}

public class CachedReferenceLocation
{
    public string FilePath { get; set; } = "";
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string ContainingMemberName { get; set; } = "";
}

/// <summary>
/// Serializable representation of GetMembers results
/// </summary>
public class CachedMembersResult
{
    public List<CachedMemberInfo> Members { get; set; } = new();
}

public class CachedMemberInfo
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public string Accessibility { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Documentation { get; set; } = "";
}

/// <summary>
/// Serializable representation of symbol search results
/// </summary>
public class CachedSymbolSearchResult
{
    public List<CachedSymbolInfo> Symbols { get; set; } = new();
}

public class CachedSymbolInfo
{
    public string FullyQualifiedName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
}

/// <summary>
/// Cache parameters for symbol-based operations (FindCallers, FindOutgoingCalls, etc.)
/// Replaces anonymous types to support AOT-compatible JSON serialization.
/// </summary>
public sealed record SymbolFqnCacheParameter
{
    public required string SymbolFqn { get; init; }
}

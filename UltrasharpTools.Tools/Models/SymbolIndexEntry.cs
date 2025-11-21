

namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Indexed entry for fast symbol lookup with bitwise metadata.
/// Optimized for minimal memory footprint and maximum lookup speed.
/// </summary>
public sealed class SymbolIndexEntry
{
    /// <summary>
    /// The actual Roslyn symbol. Kept for full symbol operations after filtering.
    /// </summary>
    public required ISymbol Symbol { get; init; }

    /// <summary>
    /// Canonical fully qualified name (without global:: prefix, normalized)
    /// </summary>
    public required string CanonicalFqn { get; init; }

    /// <summary>
    /// Bitwise flags for ultra-fast filtering (64 bits)
    /// </summary>
    public required SymbolMetadataFlags Flags { get; init; }

    /// <summary>
    /// Simple name without namespace (e.g., "MyMethod", "MyClass")
    /// Used for O(1) dictionary lookup by simple name.
    /// </summary>
    public required string SimpleName { get; init; }

    /// <summary>
    /// Containing namespace (e.g., "MyApp.Services")
    /// Empty string for global namespace.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Namespace depth for hierarchical filtering.
    /// Examples: "" = 0, "MyApp" = 1, "MyApp.Services" = 2
    /// Allows quick filtering by namespace depth without string operations.
    /// </summary>
    public required byte NamespaceDepth { get; init; }

    /// <summary>
    /// Length of SimpleName for quick length-based filtering.
    /// Stored as ushort to save memory (65535 max - more than enough for any identifier).
    /// </summary>
    public required ushort NameLength { get; init; }

    /// <summary>
    /// Length of CanonicalFqn for quick FQN length-based filtering.
    /// Useful for Levenshtein distance pre-filtering.
    /// </summary>
    public required ushort FqnLength { get; init; }

    /// <summary>
    /// First character of SimpleName (lowercase) for quick char-based bloom filtering.
    /// Enables instant "starts with" checks without string allocation.
    /// </summary>
    public required char FirstChar { get; init; }

    /// <summary>
    /// Hash code of SimpleName for quick equality checks.
    /// Pre-computed to avoid repeated hash calculations during lookup.
    /// </summary>
    public required int SimpleNameHashCode { get; init; }

    /// <summary>
    /// Roslyn DocumentId for incremental updates.
    /// Used to track which document this symbol belongs to for fast invalidation.
    /// Nullable for cached entries loaded from disk.
    /// </summary>
    public DocumentId? DocumentId { get; init; }

    /// <summary>
    /// File path where this symbol is defined.
    /// Used for navigation and incremental updates.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Line number where this symbol is defined (1-based).
    /// Used for navigation and debugging.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Unique identifier for this symbol.
    /// Format: {DocumentId}:{LineNumber}:{SimpleName} or fallback to CanonicalFqn hash.
    /// Used for tracking symbols across deltas.
    /// </summary>
    public string SymbolId => DocumentId != null
        ? $"{DocumentId.Id}:{LineNumber}:{SimpleName}"
        : $"cached:{CanonicalFqn.GetHashCode():X8}";

    public override string ToString()
    {
        return $"{CanonicalFqn} [{Flags.GetAccessibility()}]";
    }
}

/// <summary>
/// Builder for creating SymbolIndexEntry from ISymbol.
/// Extracts all metadata and computes bitwise flags.
/// </summary>
public static class SymbolIndexEntryBuilder
{
    /// <summary>
    /// Creates an indexed entry from a Roslyn symbol with all metadata extracted.
    /// </summary>
    /// <param name="symbol">The Roslyn symbol</param>
    /// <param name="canonicalFqn">Canonical fully qualified name</param>
    /// <param name="documentId">Optional DocumentId for incremental updates</param>
    /// <param name="filePath">Optional file path where symbol is defined</param>
    /// <param name="lineNumber">Optional line number (1-based)</param>
    public static SymbolIndexEntry Build(
        ISymbol symbol,
        string canonicalFqn,
        DocumentId? documentId = null,
        string? filePath = null,
        int lineNumber = 0)
    {
        var flags = ExtractFlags(symbol);
        var simpleName = symbol.Name;
        var ns = GetNamespace(symbol);
        var nsDepth = CountNamespaceDepth(ns);

        // Extract location info if not provided
        if (filePath == null || lineNumber == 0)
        {
            var location = symbol.Locations.FirstOrDefault(loc => loc.IsInSource);
            if (location != null)
            {
                filePath ??= location.SourceTree?.FilePath;
                if (lineNumber == 0)
                {
                    lineNumber = location.GetLineSpan().StartLinePosition.Line + 1; // Convert to 1-based
                }
            }
        }

        return new SymbolIndexEntry
        {
            Symbol = symbol,
            CanonicalFqn = canonicalFqn,
            Flags = flags,
            SimpleName = simpleName,
            Namespace = ns,
            NamespaceDepth = nsDepth,
            NameLength = (ushort)Math.Min(simpleName.Length, ushort.MaxValue),
            FqnLength = (ushort)Math.Min(canonicalFqn.Length, ushort.MaxValue),
            FirstChar = simpleName.Length > 0 ? char.ToLowerInvariant(simpleName[0]) : '\0',
            SimpleNameHashCode = simpleName.GetHashCode(),
            DocumentId = documentId,
            FilePath = filePath,
            LineNumber = lineNumber
        };
    }

    /// <summary>
    /// Extracts bitwise flags from ISymbol.
    /// This is the core optimization: all metadata in 64 bits.
    /// </summary>
    public static SymbolMetadataFlags ExtractFlags(ISymbol symbol)
    {
        var flags = SymbolMetadataFlags.None;

        // Accessibility
        flags |= symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => SymbolMetadataFlags.Public,
            Accessibility.Private => SymbolMetadataFlags.Private,
            Accessibility.Internal => SymbolMetadataFlags.Internal,
            Accessibility.Protected => SymbolMetadataFlags.Protected,
            Accessibility.ProtectedOrInternal => SymbolMetadataFlags.Protected | SymbolMetadataFlags.Internal,
            _ => SymbolMetadataFlags.None
        };

        // Symbol kind
        flags |= symbol.Kind switch
        {
            SymbolKind.Method => SymbolMetadataFlags.IsMethod,
            SymbolKind.Property => SymbolMetadataFlags.IsProperty,
            SymbolKind.Field => SymbolMetadataFlags.IsField,
            SymbolKind.Event => SymbolMetadataFlags.IsEvent,
            SymbolKind.Namespace => SymbolMetadataFlags.IsNamespace,
            SymbolKind.Parameter => SymbolMetadataFlags.IsParameter,
            SymbolKind.Local => SymbolMetadataFlags.IsLocal,
            _ => SymbolMetadataFlags.None
        };

        // Type kind (if it's a type)
        if (symbol is INamedTypeSymbol namedType)
        {
            flags |= namedType.TypeKind switch
            {
                TypeKind.Class => SymbolMetadataFlags.IsClass,
                TypeKind.Interface => SymbolMetadataFlags.IsInterface,
                TypeKind.Struct => SymbolMetadataFlags.IsStruct,
                TypeKind.Enum => SymbolMetadataFlags.IsEnum,
                TypeKind.Delegate => SymbolMetadataFlags.IsDelegate,
                _ => SymbolMetadataFlags.None
            };

            if (namedType.IsGenericType)
                flags |= SymbolMetadataFlags.IsGeneric | SymbolMetadataFlags.HasGenericArgs;
        }

        // Modifiers
        if (symbol.IsStatic) flags |= SymbolMetadataFlags.IsStatic;
        if (symbol.IsAbstract) flags |= SymbolMetadataFlags.IsAbstract;
        if (symbol.IsSealed) flags |= SymbolMetadataFlags.IsSealed;
        if (symbol.IsVirtual) flags |= SymbolMetadataFlags.IsVirtual;
        if (symbol.IsOverride) flags |= SymbolMetadataFlags.IsOverride;
        if (symbol.IsExtern) flags |= SymbolMetadataFlags.IsExtern;

        // Special attributes
        if (symbol is IMethodSymbol methodSymbol)
        {
            if (methodSymbol.IsAsync)
                flags |= SymbolMetadataFlags.IsAsync;
            if (methodSymbol.IsExtensionMethod)
                flags |= SymbolMetadataFlags.IsExtension;
            if (methodSymbol.MethodKind == MethodKind.Constructor)
                flags |= SymbolMetadataFlags.IsConstructor;
            if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator || methodSymbol.MethodKind == MethodKind.Conversion)
                flags |= SymbolMetadataFlags.IsOperator;
            if (methodSymbol.Parameters.Length > 0)
                flags |= SymbolMetadataFlags.HasParameters;
        }

        // Partial types
        if (symbol is INamedTypeSymbol typeSymbol && typeSymbol.DeclaringSyntaxReferences.Length > 1)
            flags |= SymbolMetadataFlags.IsPartial;

        // Scope
        if (symbol.ContainingType == null)
            flags |= SymbolMetadataFlags.IsTopLevel;
        else
            flags |= SymbolMetadataFlags.IsNested;

        // Attributes
        if (symbol.GetAttributes().Length > 0)
            flags |= SymbolMetadataFlags.HasAttributes;

        return flags;
    }

    private static string GetNamespace(ISymbol symbol)
    {
        var ns = symbol.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace)
            return string.Empty;

        return ns.ToDisplayString();
    }

    private static byte CountNamespaceDepth(string ns)
    {
        if (string.IsNullOrEmpty(ns))
            return 0;

        byte depth = 1;
        foreach (var ch in ns)
        {
            if (ch == '.')
                depth++;
        }

        return depth;
    }
}

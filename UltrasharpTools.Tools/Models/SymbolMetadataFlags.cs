namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Bitwise flags representing symbol metadata for ultra-fast filtering.
/// Uses single ulong (64 bits) instead of 30+ bool properties.
/// Bitwise operations are the fastest CPU instructions available.
/// </summary>
[Flags]
public enum SymbolMetadataFlags : ulong {
    None = 0,

    // === Accessibility (4 bits: 0-3) ===
    Public = 1UL << 0,
    Private = 1UL << 1,
    Internal = 1UL << 2,
    Protected = 1UL << 3,

    // === Type kind (5 bits: 4-8) ===
    IsClass = 1UL << 4,
    IsInterface = 1UL << 5,
    IsStruct = 1UL << 6,
    IsEnum = 1UL << 7,
    IsDelegate = 1UL << 8,

    // === Modifiers (6 bits: 9-14) ===
    IsStatic = 1UL << 9,
    IsAbstract = 1UL << 10,
    IsSealed = 1UL << 11,
    IsVirtual = 1UL << 12,
    IsOverride = 1UL << 13,
    IsReadOnly = 1UL << 14,

    // === Member kind (6 bits: 15-20) ===
    IsMethod = 1UL << 15,
    IsProperty = 1UL << 16,
    IsField = 1UL << 17,
    IsEvent = 1UL << 18,
    IsConstructor = 1UL << 19,
    IsOperator = 1UL << 20,

    // === Special attributes (6 bits: 21-26) ===
    IsGeneric = 1UL << 21,
    IsAsync = 1UL << 22,
    IsExtension = 1UL << 23,
    IsPartial = 1UL << 24,
    IsNullable = 1UL << 25,
    IsExtern = 1UL << 26,

    // === Scope (2 bits: 27-28) ===
    IsTopLevel = 1UL << 27,
    IsNested = 1UL << 28,

    // === Name characteristics (3 bits: 29-31) ===
    HasGenericArgs = 1UL << 29,
    HasParameters = 1UL << 30,
    HasAttributes = 1UL << 31,

    // === Additional type info (3 bits: 32-34) ===
    IsNamespace = 1UL << 32,
    IsParameter = 1UL << 33,
    IsLocal = 1UL << 34,

    // === Nullability (2 bits: 35-36) ===
    IsNullableReference = 1UL << 35,
    IsNullableValue = 1UL << 36,

    // === Reserved for future use (27 bits: 37-63) ===
    // Can add up to 27 more flags without changing storage size!
}

/// <summary>
/// Extension methods for fast bitwise operations on SymbolMetadataFlags
/// </summary>
public static class SymbolMetadataFlagsExtensions {
    /// <summary>
    /// Check if all required flags are present
    /// </summary>
    public static bool HasAllFlags(this SymbolMetadataFlags flags, SymbolMetadataFlags required) {
        return (flags & required) == required;
    }

    /// <summary>
    /// Check if any of the specified flags are present
    /// </summary>
    public static bool HasAnyFlag(this SymbolMetadataFlags flags, SymbolMetadataFlags check) {
        return (flags & check) != 0;
    }

    /// <summary>
    /// Check if none of the excluded flags are present
    /// </summary>
    public static bool HasNoFlags(this SymbolMetadataFlags flags, SymbolMetadataFlags excluded) {
        return (flags & excluded) == 0;
    }

    /// <summary>
    /// Get accessibility flags only
    /// </summary>
    public static SymbolMetadataFlags GetAccessibility(this SymbolMetadataFlags flags) {
        return flags & (SymbolMetadataFlags.Public | SymbolMetadataFlags.Private |
                       SymbolMetadataFlags.Internal | SymbolMetadataFlags.Protected);
    }

    /// <summary>
    /// Get type kind flags only
    /// </summary>
    public static SymbolMetadataFlags GetTypeKind(this SymbolMetadataFlags flags) {
        return flags & (SymbolMetadataFlags.IsClass | SymbolMetadataFlags.IsInterface |
                       SymbolMetadataFlags.IsStruct | SymbolMetadataFlags.IsEnum |
                       SymbolMetadataFlags.IsDelegate);
    }

    /// <summary>
    /// Get member kind flags only
    /// </summary>
    public static SymbolMetadataFlags GetMemberKind(this SymbolMetadataFlags flags) {
        return flags & (SymbolMetadataFlags.IsMethod | SymbolMetadataFlags.IsProperty |
                       SymbolMetadataFlags.IsField | SymbolMetadataFlags.IsEvent |
                       SymbolMetadataFlags.IsConstructor | SymbolMetadataFlags.IsOperator);
    }
}



using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Resolves ISymbol instances from FQNs and cached metadata
/// Used for restoring symbols from persistent cache
/// </summary>
public class SymbolResolver
{
private readonly ILogger _logger;

public SymbolResolver(ILogger logger)
{
_logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

/// <summary>
/// Find type by FQN in compilation
/// </summary>
private INamedTypeSymbol? FindType(Compilation compilation, string typeFqn)
{
// Remove generic arity markers (e.g., MyType`1 -> MyType)
var cleanTypeName = typeFqn;
var arityIndex = typeFqn.IndexOf('`');
if (arityIndex > 0)
{
cleanTypeName = typeFqn.Substring(0, arityIndex);
}

// Try exact match first
var symbol = compilation.GetTypeByMetadataName(cleanTypeName);
if (symbol != null)
return symbol;

// Fallback: search in all types
// This handles nested types and edge cases
return FindTypeInNamespaces(compilation.GlobalNamespace, typeFqn);
}

/// <summary>
/// Recursively search for type in namespace hierarchy
/// </summary>
private INamedTypeSymbol? FindTypeInNamespaces(INamespaceSymbol namespaceSymbol, string typeFqn)
{
// Check types in this namespace
foreach (var type in namespaceSymbol.GetTypeMembers())
{
var typeFqnCandidate = type.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat)
.Replace("global::", "");

if (typeFqnCandidate == typeFqn)
return type;

// Check nested types
var nestedType = FindNestedType(type, typeFqn);
if (nestedType != null)
return nestedType;
}

// Recurse into child namespaces
foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
{
var found = FindTypeInNamespaces(childNamespace, typeFqn);
if (found != null)
return found;
}

return null;
}

/// <summary>
/// Search for nested type
/// </summary>
private INamedTypeSymbol? FindNestedType(INamedTypeSymbol type, string typeFqn)
{
foreach (var nestedType in type.GetTypeMembers())
{
var nestedFqn = nestedType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat)
.Replace("global::", "");

if (nestedFqn == typeFqn)
return nestedType;

// Recurse deeper
var deeperNested = FindNestedType(nestedType, typeFqn);
if (deeperNested != null)
return deeperNested;
}

return null;
}

/// <summary>
/// Find member in type by name and flags
/// </summary>
private ISymbol? FindMember(INamedTypeSymbol type, string memberName, long flags)
{
var symbolFlags = (SymbolMetadataFlags)flags;

// Get all members with matching name
var candidates = type.GetMembers(memberName);

if (candidates.Length == 0)
return null;

if (candidates.Length == 1)
return candidates[0];

// Multiple candidates - use flags to disambiguate
foreach (var candidate in candidates)
{
var candidateFlags = SymbolIndexEntryBuilder.ExtractFlags(candidate);
if (candidateFlags == symbolFlags)
return candidate;
}

// Fallback: return first candidate
_logger.LogTrace("Multiple candidates for member {MemberName}, using first match", memberName);
return candidates[0];
}

/// <summary>
/// Parse FQN to extract type and member information
/// </summary>
private (string TypeFqn, string? MemberName) ParseFqn(string fqn)
{
// FQN formats:
// - Type: "Namespace.TypeName"
// - Method: "Namespace.TypeName.MethodName(params)"
// - Property: "Namespace.TypeName.PropertyName"
// - Field: "Namespace.TypeName.FieldName"

// Find last dot before parenthesis (if any)
var parenIndex = fqn.IndexOf('(');
var searchLength = parenIndex > 0 ? parenIndex : fqn.Length;

var lastDotIndex = fqn.LastIndexOf('.', searchLength - 1, searchLength);

if (lastDotIndex < 0)
{
// No dots - must be a simple type
return (fqn, null);
}

// Check if this looks like a member
// Heuristic: if there's a '(' after the last dot, it's a method
// Otherwise, we need to check if it's a nested type or a member

// For now, simple approach: last segment is member if it looks like one
var potentialMember = fqn.Substring(lastDotIndex + 1);

// If it contains '(' or '<', it's definitely a method
if (potentialMember.Contains('(') || potentialMember.Contains('<'))
{
// Extract member name (without parameters/generics)
var memberNameEnd = Math.Min(
potentialMember.IndexOf('(') >= 0 ? potentialMember.IndexOf('(') : int.MaxValue,
potentialMember.IndexOf('<') >= 0 ? potentialMember.IndexOf('<') : int.MaxValue
);

var memberName = memberNameEnd < int.MaxValue
? potentialMember.Substring(0, memberNameEnd)
: potentialMember;

return (fqn.Substring(0, lastDotIndex), memberName);
}

// Ambiguous case - could be nested type or member
// Try both: first assume it's a member, if that fails, caller will try as type
return (fqn.Substring(0, lastDotIndex), potentialMember);
}

/// <summary>
/// Batch resolve symbols for better performance
/// Uses parallel processing with compilation caching per project
/// </summary>
public async Task<List<(SerializableSymbolEntry Entry, ISymbol? Symbol)>> ResolveSymbolsAsync(
List<SerializableSymbolEntry> entries,
Solution solution,
CancellationToken cancellationToken,
Action<int, int>? progressCallback = null)
{
// Pre-load all compilations in parallel (much faster than loading one-by-one)
var compilationCache = new Dictionary<string, Compilation?>();
var projects = solution.Projects.ToList();

_logger.LogDebug("Pre-loading {Count} project compilations in parallel...", projects.Count);
var compilationTasks = projects
.Select(async p => {
var compilation = await p.GetCompilationAsync(cancellationToken);
return (p.Name, compilation);
})
.ToList();

var compilations = await Task.WhenAll(compilationTasks);
foreach (var (name, compilation) in compilations)
{
compilationCache[name] = compilation;
}
_logger.LogDebug("Loaded {Count} compilations", compilationCache.Count(c => c.Value != null));

// Process symbols in parallel batches (256 at a time to avoid overwhelming the system)
var results = new List<(SerializableSymbolEntry Entry, ISymbol? Symbol)>(entries.Count);
var resultsLock = new object();
var processedCount = 0;
var totalCount = entries.Count;
var batchSize = 256;

for (int i = 0; i < entries.Count; i += batchSize)
{
cancellationToken.ThrowIfCancellationRequested();

var batch = entries.Skip(i).Take(batchSize).ToList();

var batchResults = await Task.WhenAll(
batch.Select(async entry => {
var symbol = await TryResolveSymbolAsync(entry, solution, compilationCache, cancellationToken);

// Thread-safe progress reporting
var current = Interlocked.Increment(ref processedCount);
if (progressCallback != null && current % 1000 == 0) // Report every 1000 symbols
{
progressCallback(current, totalCount);
}

return (entry, symbol);
})
);

lock (resultsLock)
{
results.AddRange(batchResults);
}
}

// Final progress report
progressCallback?.Invoke(processedCount, totalCount);

return results;
}

/// <summary>
/// Try to resolve ISymbol from SerializableSymbolEntry using cached compilations
/// </summary>
private async Task<ISymbol?> TryResolveSymbolAsync(
SerializableSymbolEntry entry,
Solution solution,
Dictionary<string, Compilation?> compilationCache,
CancellationToken cancellationToken)
{
try
{
// Use cached compilation instead of loading each time
if (!compilationCache.TryGetValue(entry.ProjectName, out var compilation) || compilation == null)
{
_logger.LogTrace("Compilation not available for project {ProjectName}", entry.ProjectName);
return null;
}

// Parse FQN to extract type and member information
var symbolInfo = ParseFqn(entry.CanonicalFqn);

// Try to find the symbol
ISymbol? symbol = null;

if (symbolInfo.MemberName == null)
{
// It's a type
symbol = FindType(compilation, symbolInfo.TypeFqn);
}
else
{
// It's a member (method, property, field, etc.)
var containingType = FindType(compilation, symbolInfo.TypeFqn);
if (containingType != null)
{
symbol = FindMember(containingType, symbolInfo.MemberName, entry.Flags);
}
}

return symbol;
}
catch (Exception ex)
{
_logger.LogTrace(ex, "Error resolving symbol {FQN}", entry.CanonicalFqn);
return null;
}
}
}

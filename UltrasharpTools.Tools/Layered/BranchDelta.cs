
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Represents symbol changes (delta) for a specific branch relative to base (main).
/// Layer 1 in the three-layer architecture.
/// Shared between all clients working in the same branch.
/// </summary>
public sealed class BranchDelta
{
    /// <summary>
    /// Branch name (e.g., "feature/auth").
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Base commit SHA from which this delta was computed.
    /// Used for rebase detection.
    /// </summary>
    public string BaseCommitSha { get; set; } = string.Empty;

    /// <summary>
    /// Symbols added in this branch (not present in base).
    /// Key: SymbolId
    /// </summary>
    public ConcurrentDictionary<string, SymbolIndexEntry> AddedSymbols { get; } = new();

    /// <summary>
    /// Symbols modified in this branch (present in base but changed).
    /// Key: SymbolId
    /// </summary>
    public ConcurrentDictionary<string, SymbolIndexEntry> ModifiedSymbols { get; } = new();

    /// <summary>
    /// Symbol IDs deleted in this branch (present in base but removed).
    /// </summary>
    public ConcurrentBag<string> DeletedSymbolIds { get; } = new();

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of changes in this delta.
    /// </summary>
    public int TotalChanges => AddedSymbols.Count + ModifiedSymbols.Count + DeletedSymbolIds.Count;

    /// <summary>
    /// Apply this delta to base results.
    /// Filters deleted symbols, replaces modified, adds new.
    /// </summary>
    public IEnumerable<SymbolIndexEntry> Apply(IEnumerable<SymbolIndexEntry> baseResults)
    {
        foreach (var symbol in baseResults)
        {
            // Skip deleted symbols
            if (DeletedSymbolIds.Contains(symbol.SymbolId))
            {
                continue;
            }

            // Replace with modified version if exists
            if (ModifiedSymbols.TryGetValue(symbol.SymbolId, out var modified))
            {
                yield return modified;
            }
            else
            {
                yield return symbol;
            }
        }

        // Add new symbols
        foreach (var added in AddedSymbols.Values)
        {
            yield return added;
        }
    }

    /// <summary>
    /// Merge another delta into this one.
    /// Used when promoting working delta to branch delta.
    /// </summary>
    public void MergeWith(BranchDelta other, string? newCommitSha = null)
    {
        // Merge added symbols
        foreach (var (id, symbol) in other.AddedSymbols)
        {
            AddedSymbols[id] = symbol;
        }

        // Merge modified symbols
        foreach (var (id, symbol) in other.ModifiedSymbols)
        {
            ModifiedSymbols[id] = symbol;
        }

        // Merge deleted symbols
        foreach (var id in other.DeletedSymbolIds)
        {
            DeletedSymbolIds.Add(id);
        }

        LastModified = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(newCommitSha))
        {
            BaseCommitSha = newCommitSha;
        }
    }

    /// <summary>
    /// Clear all changes.
    /// </summary>
    public void Clear()
    {
        AddedSymbols.Clear();
        ModifiedSymbols.Clear();
        DeletedSymbolIds.Clear();
        LastModified = DateTime.UtcNow;
    }
}

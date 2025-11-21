using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Represents uncommitted symbol changes for a specific client.
/// Layer 2 in the three-layer architecture.
/// Per-client, mutable, not persisted (except for crash recovery).
/// </summary>
public sealed class WorkingDelta
{
    /// <summary>
    /// Client identifier (e.g., process ID, session ID, connection ID).
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Branch name this working delta is for.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Symbols added in working directory (uncommitted).
    /// Key: SymbolId
    /// </summary>
    public ConcurrentDictionary<string, SymbolIndexEntry> AddedSymbols { get; } = new();

    /// <summary>
    /// Symbols modified in working directory (uncommitted).
    /// Key: SymbolId
    /// </summary>
    public ConcurrentDictionary<string, SymbolIndexEntry> ModifiedSymbols { get; } = new();

    /// <summary>
    /// Symbol IDs deleted in working directory (uncommitted).
    /// </summary>
    public ConcurrentBag<string> DeletedSymbolIds { get; } = new();

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of uncommitted changes.
    /// </summary>
    public int TotalChanges => AddedSymbols.Count + ModifiedSymbols.Count + DeletedSymbolIds.Count;

    /// <summary>
    /// Apply this working delta to layer results (Base + Branch).
    /// </summary>
    public IEnumerable<SymbolIndexEntry> Apply(IEnumerable<SymbolIndexEntry> layerResults)
    {
        foreach (var symbol in layerResults)
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
    /// Clear all uncommitted changes.
    /// Called after git commit.
    /// </summary>
    public void Clear()
    {
        AddedSymbols.Clear();
        ModifiedSymbols.Clear();
        DeletedSymbolIds.Clear();
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Promote this working delta to a branch delta.
    /// Used during git commit to move changes from Layer 2 to Layer 1.
    /// </summary>
    public BranchDelta PromoteToBranchDelta(string? commitSha = null)
    {
        var branchDelta = new BranchDelta
        {
            BranchName = BranchName,
            BaseCommitSha = commitSha ?? string.Empty,
            LastModified = DateTime.UtcNow,
        };

        // Copy all changes
        foreach (var (id, symbol) in AddedSymbols)
        {
            branchDelta.AddedSymbols[id] = symbol;
        }

        foreach (var (id, symbol) in ModifiedSymbols)
        {
            branchDelta.ModifiedSymbols[id] = symbol;
        }

        foreach (var id in DeletedSymbolIds)
        {
            branchDelta.DeletedSymbolIds.Add(id);
        }

        return branchDelta;
    }

    /// <summary>
    /// Clone this working delta (for backup before git operations).
    /// </summary>
    public WorkingDelta Clone()
    {
        var clone = new WorkingDelta
        {
            ClientId = ClientId,
            BranchName = BranchName,
            LastModified = LastModified,
        };

        foreach (var (id, symbol) in AddedSymbols)
        {
            clone.AddedSymbols[id] = symbol;
        }

        foreach (var (id, symbol) in ModifiedSymbols)
        {
            clone.ModifiedSymbols[id] = symbol;
        }

        foreach (var id in DeletedSymbolIds)
        {
            clone.DeletedSymbolIds.Add(id);
        }

        return clone;
    }
}

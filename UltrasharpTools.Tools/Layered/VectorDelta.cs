using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Represents a delta of vector embeddings for a specific layer (branch or working directory).
/// Stores added, modified, and deleted embeddings to be applied on top of base vectors.
/// Phase 4.1: Vector Integration
/// </summary>
public class VectorDelta
{
    public string BranchName { get; set; } = string.Empty;
    public string BaseCommitSha { get; set; } = string.Empty;

    /// <summary>
    /// Embeddings for newly added symbols (SymbolId → embedding vector).
    /// Uses FastStringComparer (xxHash32) for 2-3x faster dictionary lookups.
    /// </summary>
    public ConcurrentDictionary<string, float[]> AddedEmbeddings { get; } =
        new(Infrastructure.FastStringComparer.Ordinal);

    /// <summary>
    /// Embeddings for modified symbols (SymbolId → new embedding vector).
    /// Uses FastStringComparer (xxHash32) for 2-3x faster dictionary lookups.
    /// </summary>
    public ConcurrentDictionary<string, float[]> ModifiedEmbeddings { get; } =
        new(Infrastructure.FastStringComparer.Ordinal);

    /// <summary>
    /// Symbol IDs that were deleted (no longer have embeddings).
    /// </summary>
    public ConcurrentHashSet<string> DeletedSymbolIds { get; } = new();

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total number of changes in this delta.
    /// </summary>
    public int TotalChanges =>
        AddedEmbeddings.Count + ModifiedEmbeddings.Count + DeletedSymbolIds.Count;

    /// <summary>
    /// Applies this delta to a list of vector search results.
    /// Removes deleted symbols, updates modified embeddings, and adds new embeddings.
    /// </summary>
    /// <param name="baseResults">Base search results from Layer 0</param>
    /// <param name="topK">Maximum number of results to return</param>
    /// <returns>Merged results with delta applied</returns>
    public List<VectorSearchResult> Apply(List<VectorSearchResult> baseResults, int topK)
    {
        // Start with base results, filtered by deletions
        var merged = baseResults.Where(r => !DeletedSymbolIds.Contains(r.SymbolId)).ToList();

        // Update modified embeddings (recalculate scores if needed)
        foreach (var result in merged)
        {
            if (ModifiedEmbeddings.TryGetValue(result.SymbolId, out var newEmbedding))
            {
                // Update embedding (score recalculation happens in LayeredVectorStore)
                result.Embedding = newEmbedding;
            }
        }

        // Add new embeddings from this delta
        foreach (var (symbolId, embedding) in AddedEmbeddings)
        {
            // Score calculation happens in LayeredVectorStore
            merged.Add(
                new VectorSearchResult
                {
                    SymbolId = symbolId,
                    Embedding = embedding,
                    Score = 0f, // Will be recalculated
                }
            );
        }

        // Re-sort by score and take topK
        return [.. merged.OrderByDescending(r => r.Score).Take(topK)];
    }

    /// <summary>
    /// Merges another delta into this one (for compaction or combining layers).
    /// </summary>
    /// <param name="other">Delta to merge</param>
    public void MergeWith(VectorDelta other)
    {
        // Merge added embeddings
        foreach (var (symbolId, embedding) in other.AddedEmbeddings)
        {
            AddedEmbeddings[symbolId] = embedding;
            ModifiedEmbeddings.TryRemove(symbolId, out _); // If it was modified, it's now added
        }

        // Merge modified embeddings
        foreach (var (symbolId, embedding) in other.ModifiedEmbeddings)
        {
            if (!AddedEmbeddings.ContainsKey(symbolId))
                ModifiedEmbeddings[symbolId] = embedding;
        }

        // Merge deleted IDs
        foreach (var symbolId in other.DeletedSymbolIds)
        {
            DeletedSymbolIds.Add(symbolId);
            AddedEmbeddings.TryRemove(symbolId, out _); // Can't be added if deleted
            ModifiedEmbeddings.TryRemove(symbolId, out _); // Can't be modified if deleted
        }

        LastModified = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Clears all changes in this delta.
    /// </summary>
    public void Clear()
    {
        AddedEmbeddings.Clear();
        ModifiedEmbeddings.Clear();
        DeletedSymbolIds.Clear();
        LastModified = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a shallow clone of this delta.
    /// </summary>
    public VectorDelta Clone()
    {
        var clone = new VectorDelta
        {
            BranchName = BranchName,
            BaseCommitSha = BaseCommitSha,
            LastModified = LastModified,
        };

        foreach (var kvp in AddedEmbeddings)
            clone.AddedEmbeddings[kvp.Key] = kvp.Value;

        foreach (var kvp in ModifiedEmbeddings)
            clone.ModifiedEmbeddings[kvp.Key] = kvp.Value;

        foreach (var id in DeletedSymbolIds)
            clone.DeletedSymbolIds.Add(id);

        return clone;
    }
}

/// <summary>
/// Represents a single vector search result.
/// </summary>
public class VectorSearchResult
{
    public required string SymbolId { get; set; }
    public required float[] Embedding { get; set; }
    public float Score { get; set; }
    public SymbolIndexEntry? SymbolEntry { get; set; } // Optional, populated by LayeredVectorStore
}

/// <summary>
/// Concurrent HashSet implementation (not provided by BCL).
/// </summary>
public class ConcurrentHashSet<T> : IEnumerable<T>
    where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = new();

    public bool Add(T item) => _dict.TryAdd(item, 0);

    public bool Contains(T item) => _dict.ContainsKey(item);

    public bool Remove(T item) => _dict.TryRemove(item, out _);

    public void Clear() => _dict.Clear();

    public int Count => _dict.Count;
    public IEnumerable<T> Items => _dict.Keys;

    public IEnumerator<T> GetEnumerator() => _dict.Keys.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}


using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Interface for layered symbol indexing with branch and client awareness.
/// Supports three layers: Base (main branch), Branch Deltas, and Working Directory (uncommitted changes).
/// </summary>
public interface ILayeredIndex
{
    /// <summary>
    /// Check if the index is built and ready for queries.
    /// </summary>
    bool IsBuilt { get; }

    /// <summary>
    /// Total number of symbols across all layers.
    /// </summary>
    int TotalSymbols { get; }

    /// <summary>
    /// Build base index from solution (Layer 0).
    /// </summary>
    Task BuildFromSolutionAsync(Solution solution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find symbols with layered merging (Base + Branch + Working).
    /// </summary>
    /// <param name="clientId">Client identifier for working directory isolation</param>
    /// <param name="branch">Branch name for delta layer</param>
    /// <param name="searchTerm">Search term</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged results from all applicable layers</returns>
    Task<IEnumerable<SymbolIndexEntry>> FindAsync(
        string? clientId,
        string? branch,
        string searchTerm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create or load branch delta (Layer 1).
    /// </summary>
    Task EnsureBranchDeltaAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update working directory delta when code is modified (Layer 2).
    /// </summary>
    Task UpdateWorkingDeltaAsync(
        string clientId,
        string branch,
        SymbolIndexEntry symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear working directory delta (e.g., after commit).
    /// </summary>
    Task ClearWorkingDeltaAsync(string clientId, string branch);

    /// <summary>
    /// Promote working directory delta to branch delta (git commit).
    /// </summary>
    Task PromoteWorkingToBranchAsync(
        string clientId,
        string branch,
        string commitSha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incremental update for document change.
    /// </summary>
    Task UpdateDocumentAsync(
        Solution solution,
        DocumentId documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incremental add for new document.
    /// </summary>
    Task AddDocumentAsync(
        Solution solution,
        DocumentId documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incremental remove for deleted document.
    /// </summary>
    Task RemoveDocumentAsync(
        DocumentId documentId,
        CancellationToken cancellationToken = default);
}

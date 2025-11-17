using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Services;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Three-layer symbol index with branch awareness and multi-client isolation.
///
/// Architecture:
/// - Layer 0 (Base): Main branch symbols, shared, immutable
/// - Layer 1 (Branch Deltas): Per-branch changes, shared, mostly immutable
/// - Layer 2 (Working Deltas): Per-client uncommitted changes, mutable
///
/// Query composition: Result = Layer0 ∪ Layer1 ∪ Layer2 - Deleted
/// </summary>
public sealed class LayeredSymbolIndex : ILayeredIndex
{
    private readonly FastSymbolIndex _baseIndex;
    private readonly LayeredIndexingOptions _options;
    private readonly ILogger<LayeredSymbolIndex> _logger;
    private readonly IGitService? _gitService;

    // Layer 1: Branch deltas (shared)
    private readonly ConcurrentDictionary<string, BranchDelta> _branchDeltas = new();
    private readonly LruCache<string, BranchDelta> _branchDeltaCache;

    // Layer 2: Working deltas (per-client)
    private readonly ConcurrentDictionary<string, WorkingDelta> _workingDeltas = new();

    // Synchronization
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // Git delta computation
    private GitDeltaComputer? _deltaComputer;

    // Current solution (needed for delta computation)
    private Solution? _currentSolution;
    private string? _currentSolutionPath;

    // Persistence (Phase 1.3)
    private LayeredCacheManager? _cacheManager;

    public LayeredSymbolIndex(
        FastSymbolIndex baseIndex,
        LayeredIndexingOptions? options = null,
        IGitService? gitService = null,
        ILogger<LayeredSymbolIndex>? logger = null)
    {
        _baseIndex = baseIndex ?? throw new ArgumentNullException(nameof(baseIndex));
        _options = options ?? LayeredIndexingOptions.Default;
        _gitService = gitService;
        _logger = logger ?? NullLogger<LayeredSymbolIndex>.Instance;

        // Initialize LRU cache for branch deltas
        _branchDeltaCache = new LruCache<string, BranchDelta>(_options.MaxBranchDeltas);
        _branchDeltaCache.OnEvict += OnBranchDeltaEvicted;

        _logger.LogInformation(
            "LayeredSymbolIndex initialized with max {MaxBranches} branch deltas, persistence: {Persistence}, git integration: {GitEnabled}",
            _options.MaxBranchDeltas,
            _options.EnablePersistence,
            _gitService != null);
    }

    /// <summary>
    /// Is the base index built and ready?
    /// </summary>
    public bool IsBuilt => _baseIndex.IsBuilt;

    /// <summary>
    /// Total number of symbols in base index.
    /// Note: Does not include delta symbols for performance reasons.
    /// </summary>
    public int TotalSymbols => _baseIndex.TotalSymbols;

    #region Layer 0: Base Index Operations

    /// <summary>
    /// Build base index from solution (Layer 0).
    /// This is the foundation for all layered operations.
    /// </summary>
    public async Task BuildFromSolutionAsync(Solution solution, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building base index (Layer 0) from solution...");
        await _baseIndex.BuildFromSolutionAsync(solution, cancellationToken);
        _logger.LogInformation("Base index built with {SymbolCount} symbols", _baseIndex.TotalSymbols);

        // Store solution for delta computation
        _currentSolution = solution;
        _currentSolutionPath = solution.FilePath;

        // Initialize delta computer if git service available
        if (_gitService != null && _currentSolution != null)
        {
            _deltaComputer = new GitDeltaComputer(_gitService, _baseIndex, null);
            _logger.LogDebug("GitDeltaComputer initialized for automatic delta computation");
        }

        // Initialize persistent cache manager if enabled
        if (_options.EnablePersistence && !string.IsNullOrEmpty(_currentSolutionPath))
        {
            try
            {
                _cacheManager = new LayeredCacheManager(_currentSolutionPath, null);
                _logger.LogInformation("LayeredCacheManager initialized for persistent storage");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize LayeredCacheManager, persistence disabled");
            }
        }
    }

    #endregion

    #region Layered Find Operations

    /// <summary>
    /// Find symbols with three-layer merging.
    /// Applies: Base → Branch Delta → Working Delta
    /// </summary>
    public async Task<IEnumerable<SymbolIndexEntry>> FindAsync(
        string? clientId,
        string? branch,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        // Layer 0: Query base index
        var layer0Results = _baseIndex.Find(searchTerm);

        // Layer 1: Apply branch delta (if not main)
        IEnumerable<SymbolIndexEntry> layer1Results = layer0Results;
        if (!string.IsNullOrEmpty(branch) && !IsMainBranch(branch))
        {
            var branchDelta = await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
            if (branchDelta != null)
            {
                layer1Results = branchDelta.Apply(layer0Results);
            }
        }

        // Layer 2: Apply working delta (if client provided)
        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(branch))
        {
            var workingDelta = GetWorkingDelta(clientId, branch);
            if (workingDelta != null)
            {
                return workingDelta.Apply(layer1Results);
            }
        }

        return layer1Results;
    }

    #endregion

    #region Layer 1: Branch Delta Operations

    /// <summary>
    /// Ensure branch delta exists (load from cache or create empty).
    /// </summary>
    public async Task EnsureBranchDeltaAsync(string branch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(branch) || IsMainBranch(branch))
        {
            return; // Main branch doesn't need delta
        }

        await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
    }

    /// <summary>
    /// Get or load branch delta from cache/storage.
    /// If not found and GitService available, computes from git diff.
    /// </summary>
    private async Task<BranchDelta?> GetOrLoadBranchDeltaAsync(string branch, CancellationToken cancellationToken)
    {
        // Check in-memory cache
        if (_branchDeltaCache.TryGet(branch, out var cached))
        {
            _logger.LogDebug("Branch delta cache hit: {Branch}", branch);
            return cached;
        }

        // Check if already loading/loaded in dictionary
        if (_branchDeltas.TryGetValue(branch, out var existing))
        {
            _branchDeltaCache.Add(branch, existing); // Refresh LRU
            return existing;
        }

        // Try to load from persistent storage (Phase 1.3)
        BranchDelta? delta = null;
        if (_cacheManager != null)
        {
            delta = await _cacheManager.LoadBranchDeltaAsync(branch, cancellationToken);
            if (delta != null)
            {
                _logger.LogInformation("Loaded branch delta from persistent storage: {Branch}", branch);
            }
        }

        // If not in storage, try to compute from git diff
        if (delta == null && _deltaComputer != null && _currentSolution != null && !string.IsNullOrEmpty(_currentSolutionPath))
        {
            _logger.LogInformation("Computing branch delta from git diff: {Branch}", branch);
            delta = await _deltaComputer.ComputeDeltaAsync(_currentSolution, _currentSolutionPath, "main", cancellationToken);

            // Save newly computed delta to storage
            if (delta != null && _cacheManager != null)
            {
                await _cacheManager.SaveBranchDeltaAsync(delta, cancellationToken);
            }
        }

        // Fallback to empty delta
        if (delta == null)
        {
            _logger.LogInformation("Creating empty branch delta: {Branch}", branch);
            delta = new BranchDelta { BranchName = branch };
        }

        _branchDeltas[branch] = delta;
        _branchDeltaCache.Add(branch, delta);

        _logger.LogInformation(
            "Branch delta loaded: {Branch} ({Added} added, {Modified} modified, {Deleted} deleted)",
            branch, delta.AddedSymbols.Count, delta.ModifiedSymbols.Count, delta.DeletedSymbolIds.Count);

        return delta;
    }

    /// <summary>
    /// Called when branch delta is evicted from LRU cache.
    /// Saves to persistent storage before removal from memory.
    /// </summary>
    private void OnBranchDeltaEvicted(string branch, BranchDelta delta)
    {
        _logger.LogInformation(
            "Branch delta evicted from cache: {Branch} ({Changes} changes)",
            branch,
            delta.TotalChanges);

        // Save to persistent storage before eviction (Phase 1.3)
        if (_cacheManager != null)
        {
            // Fire and forget - we're in a synchronous callback
            _ = Task.Run(async () =>
            {
                try
                {
                    await _cacheManager.SaveBranchDeltaAsync(delta, CancellationToken.None);
                    _logger.LogDebug("Saved evicted delta to storage: {Branch}", branch);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save evicted delta: {Branch}", branch);
                }
            });
        }

        // Remove from dictionary
        _branchDeltas.TryRemove(branch, out _);
    }

    #endregion

    #region Layer 2: Working Delta Operations

    /// <summary>
    /// Update working directory delta when code is modified.
    /// </summary>
    public async Task UpdateWorkingDeltaAsync(
        string clientId,
        string branch,
        SymbolIndexEntry symbol,
        CancellationToken cancellationToken = default)
    {
        var key = GetWorkingDeltaKey(clientId, branch);
        var workingDelta = GetOrCreateWorkingDelta(clientId, branch);

        // Add to appropriate collection based on whether symbol exists in base/branch
        // For now, simple implementation: add to AddedSymbols
        workingDelta.AddedSymbols[symbol.SymbolId] = symbol;
        workingDelta.LastModified = DateTime.UtcNow;

        _logger.LogDebug(
            "Updated working delta: client={ClientId}, branch={Branch}, symbol={SymbolId}",
            clientId, branch, symbol.SymbolId);

        await Task.CompletedTask; // For async signature compatibility
    }

    /// <summary>
    /// Clear working directory delta (e.g., after git commit).
    /// </summary>
    public Task ClearWorkingDeltaAsync(string clientId, string branch)
    {
        var key = GetWorkingDeltaKey(clientId, branch);
        if (_workingDeltas.TryGetValue(key, out var delta))
        {
            delta.Clear();
            _logger.LogInformation(
                "Cleared working delta: client={ClientId}, branch={Branch}",
                clientId, branch);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Promote working delta to branch delta (git commit operation).
    /// </summary>
    public async Task PromoteWorkingToBranchAsync(
        string clientId,
        string branch,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        var key = GetWorkingDeltaKey(clientId, branch);
        if (!_workingDeltas.TryGetValue(key, out var workingDelta))
        {
            _logger.LogWarning("No working delta to promote: client={ClientId}, branch={Branch}",
                clientId, branch);
            return;
        }

        if (workingDelta.TotalChanges == 0)
        {
            _logger.LogDebug("Working delta is empty, nothing to promote");
            return;
        }

        // Get or create branch delta
        var branchDelta = await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
        if (branchDelta == null)
        {
            branchDelta = new BranchDelta { BranchName = branch };
            _branchDeltas[branch] = branchDelta;
        }

        // Merge working delta into branch delta
        var promotedDelta = workingDelta.PromoteToBranchDelta(commitSha);
        branchDelta.MergeWith(promotedDelta, commitSha);

        // Clear working delta
        workingDelta.Clear();

        _logger.LogInformation(
            "Promoted working delta to branch: client={ClientId}, branch={Branch}, commit={CommitSha}, changes={Changes}",
            clientId, branch, commitSha, promotedDelta.TotalChanges);

        // TODO Phase 1.3: Persist branch delta to storage
    }

    /// <summary>
    /// Get working delta for client+branch.
    /// </summary>
    private WorkingDelta? GetWorkingDelta(string clientId, string branch)
    {
        var key = GetWorkingDeltaKey(clientId, branch);
        return _workingDeltas.TryGetValue(key, out var delta) ? delta : null;
    }

    /// <summary>
    /// Get or create working delta for client+branch.
    /// </summary>
    private WorkingDelta GetOrCreateWorkingDelta(string clientId, string branch)
    {
        var key = GetWorkingDeltaKey(clientId, branch);
        return _workingDeltas.GetOrAdd(key, _ => new WorkingDelta
        {
            ClientId = clientId,
            BranchName = branch
        });
    }

    /// <summary>
    /// Generate unique key for working delta storage.
    /// </summary>
    private static string GetWorkingDeltaKey(string clientId, string branch) => $"{clientId}:{branch}";

    #endregion

    #region Incremental Update Delegation

    /// <summary>
    /// Incremental update for document change (delegates to base index).
    /// </summary>
    public async Task UpdateDocumentAsync(
        Solution solution,
        DocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            await _baseIndex.UpdateDocumentAsync(solution, documentId, cancellationToken);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Incremental add for new document (delegates to base index).
    /// </summary>
    public async Task AddDocumentAsync(
        Solution solution,
        DocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            await _baseIndex.AddDocumentAsync(solution, documentId, cancellationToken);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    /// Incremental remove for deleted document (delegates to base index).
    /// </summary>
    public async Task RemoveDocumentAsync(
        DocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            await _baseIndex.RemoveDocumentAsync(documentId, cancellationToken);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check if branch is main/master.
    /// </summary>
    private static bool IsMainBranch(string branch)
    {
        return string.IsNullOrEmpty(branch) ||
               branch.Equals("main", StringComparison.OrdinalIgnoreCase) ||
               branch.Equals("master", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

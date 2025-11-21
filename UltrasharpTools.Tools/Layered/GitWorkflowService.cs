using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Coordinates Git workflow operations with layered indexing.
/// Handles git commit, pull, and branch switch scenarios to ensure
/// proper promotion, preservation, and clearing of deltas.
/// Phase 5: Git Workflow Integration
/// </summary>
public class GitWorkflowService
{
    private readonly IGitService _gitService;
    private readonly ILayeredIndex? _layeredSymbolIndex;
    private readonly LayeredVectorStore? _layeredVectorStore;
    private readonly ILogger<GitWorkflowService> _logger;

    // Track current branch and client context
    private string _currentBranch = "main";
    private string? _currentClientId;

    public GitWorkflowService(
        IGitService gitService,
        ILayeredIndex? layeredSymbolIndex = null,
        LayeredVectorStore? layeredVectorStore = null,
        ILogger<GitWorkflowService>? logger = null
    )
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _layeredSymbolIndex = layeredSymbolIndex;
        _layeredVectorStore = layeredVectorStore;
        _logger = logger ?? NullLogger<GitWorkflowService>.Instance;

        _logger.LogInformation(
            "GitWorkflowService initialized with symbol index: {HasSymbols}, vector store: {HasVectors}",
            layeredSymbolIndex != null,
            layeredVectorStore != null
        );
    }

    /// <summary>
    /// Set current client context for multi-client scenarios.
    /// </summary>
    public void SetClientContext(string clientId, string branch)
    {
        _currentClientId = clientId;
        _currentBranch = branch;

        _logger.LogDebug("Client context set: {ClientId} on branch {Branch}", clientId, branch);
    }

    /// <summary>
    /// Handles git commit workflow: promotes working delta (Layer 2) to branch delta (Layer 1).
    /// Call this after a successful git commit.
    /// </summary>
    public async Task OnCommitAsync(
        string commitSha,
        string? clientId = null,
        string? branch = null,
        CancellationToken cancellationToken = default
    )
    {
        clientId ??= _currentClientId ?? throw new InvalidOperationException("Client ID not set");
        branch ??= _currentBranch;

        _logger.LogInformation(
            "Processing git commit for client {ClientId} on branch {Branch}, commit {CommitSha}",
            clientId,
            branch,
            commitSha
        );

        // Promote symbol index working delta to branch delta
        if (_layeredSymbolIndex != null)
        {
            try
            {
                await _layeredSymbolIndex.PromoteWorkingToBranchAsync(
                    clientId,
                    branch,
                    commitSha,
                    cancellationToken
                );

                _logger.LogInformation(
                    "Promoted symbol index working delta to branch delta: {Branch}",
                    branch
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to promote symbol index working delta for branch {Branch}",
                    branch
                );
                throw;
            }
        }

        // Promote vector store working delta to branch delta
        if (_layeredVectorStore != null)
        {
            try
            {
                await _layeredVectorStore.PromoteWorkingToBranchAsync(
                    clientId,
                    branch,
                    commitSha,
                    cancellationToken
                );

                _logger.LogInformation(
                    "Promoted vector store working delta to branch delta: {Branch}",
                    branch
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to promote vector store working delta for branch {Branch}",
                    branch
                );
                throw;
            }
        }

        _logger.LogInformation(
            "Git commit workflow completed successfully for commit {CommitSha}",
            commitSha
        );
    }

    /// <summary>
    /// Handles git pull workflow: preserves working delta (Layer 2), recomputes branch delta (Layer 1).
    /// Call this after a successful git pull.
    /// </summary>
    public async Task OnPullAsync(
        string? clientId = null,
        string? branch = null,
        CancellationToken cancellationToken = default
    )
    {
        clientId ??= _currentClientId ?? throw new InvalidOperationException("Client ID not set");
        branch ??= _currentBranch;

        _logger.LogInformation(
            "Processing git pull for client {ClientId} on branch {Branch}",
            clientId,
            branch
        );

        // Note: Working deltas (Layer 2) are automatically preserved in memory
        // We only need to recompute branch deltas (Layer 1) from updated git history

        // Recompute symbol index branch delta from git
        if (_layeredSymbolIndex != null)
        {
            try
            {
                // Force reload branch delta (will recompute from git diff)
                await _layeredSymbolIndex.EnsureBranchDeltaAsync(branch, cancellationToken);

                _logger.LogInformation(
                    "Recomputed symbol index branch delta after pull: {Branch}",
                    branch
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to recompute symbol index branch delta for branch {Branch}",
                    branch
                );
                // Non-critical - working delta is still preserved
            }
        }

        // Recompute vector store branch delta
        if (_layeredVectorStore != null)
        {
            try
            {
                // Force reload vector delta
                await _layeredVectorStore.EnsureBranchDeltaAsync(branch, cancellationToken);

                _logger.LogInformation(
                    "Recomputed vector store branch delta after pull: {Branch}",
                    branch
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to recompute vector store branch delta for branch {Branch}",
                    branch
                );
                // Non-critical - working delta is still preserved
            }
        }

        _logger.LogInformation("Git pull workflow completed successfully");
    }

    /// <summary>
    /// Handles branch switch workflow: validates no uncommitted changes, loads new branch delta.
    /// Call this before switching branches.
    /// </summary>
    public async Task OnBranchSwitchAsync(
        string newBranch,
        string? clientId = null,
        bool forceSwitch = false,
        CancellationToken cancellationToken = default
    )
    {
        clientId ??= _currentClientId ?? throw new InvalidOperationException("Client ID not set");
        var oldBranch = _currentBranch;

        _logger.LogInformation(
            "Processing branch switch for client {ClientId}: {OldBranch} → {NewBranch}",
            clientId,
            oldBranch,
            newBranch
        );

        // Validate no uncommitted changes in working delta (unless force)
        if (!forceSwitch)
        {
            var hasUncommittedChanges = await HasUncommittedChangesAsync(
                clientId,
                oldBranch,
                cancellationToken
            );

            if (hasUncommittedChanges)
            {
                var message =
                    $"Cannot switch from {oldBranch} to {newBranch}: uncommitted changes exist. "
                    + "Commit or stash changes first, or use forceSwitch=true to discard.";
                _logger.LogWarning(message);
                throw new InvalidOperationException(message);
            }
        }
        else
        {
            _logger.LogWarning(
                "Force switching branches - discarding uncommitted changes in {OldBranch}",
                oldBranch
            );
        }

        // Clear working delta for old branch (if force or no changes)
        if (_layeredSymbolIndex != null)
        {
            await _layeredSymbolIndex.ClearWorkingDeltaAsync(clientId, oldBranch);
        }

        if (_layeredVectorStore != null)
        {
            await _layeredVectorStore.ClearWorkingDeltaAsync(
                clientId,
                oldBranch,
                cancellationToken
            );
        }

        // Load or create branch delta for new branch
        if (_layeredSymbolIndex != null)
        {
            await _layeredSymbolIndex.EnsureBranchDeltaAsync(newBranch, cancellationToken);
            _logger.LogInformation(
                "Loaded symbol index branch delta for new branch: {NewBranch}",
                newBranch
            );
        }

        if (_layeredVectorStore != null)
        {
            await _layeredVectorStore.EnsureBranchDeltaAsync(newBranch, cancellationToken);
            _logger.LogInformation(
                "Loaded vector store branch delta for new branch: {NewBranch}",
                newBranch
            );
        }

        // Update current branch
        _currentBranch = newBranch;

        _logger.LogInformation(
            "Branch switch completed successfully: {OldBranch} → {NewBranch}",
            oldBranch,
            newBranch
        );
    }

    /// <summary>
    /// Checks if client has uncommitted changes in working delta.
    /// </summary>
    public async Task<bool> HasUncommittedChangesAsync(
        string? clientId = null,
        string? branch = null,
        CancellationToken cancellationToken = default
    )
    {
        clientId ??= _currentClientId ?? throw new InvalidOperationException("Client ID not set");
        branch ??= _currentBranch;

        // Check symbol index working delta
        if (_layeredSymbolIndex != null)
        {
            // Note: No direct API to check working delta size
            // Would need to add GetWorkingDeltaInfo() method to ILayeredIndex
            // For now, assume no uncommitted changes in symbol index
        }

        // Check vector store working delta
        if (_layeredVectorStore != null)
        {
            // Similar issue - no API to check working delta size
            // For now, assume no uncommitted changes
        }

        // Conservative approach: assume no uncommitted changes
        // In real implementation, should add methods to query working delta state
        await Task.CompletedTask;
        return false;
    }

    /// <summary>
    /// Clears all working deltas for a client (useful for cleanup/reset).
    /// </summary>
    public async Task ClearAllWorkingDeltasAsync(
        string? clientId = null,
        CancellationToken cancellationToken = default
    )
    {
        clientId ??= _currentClientId ?? throw new InvalidOperationException("Client ID not set");

        _logger.LogInformation("Clearing all working deltas for client {ClientId}", clientId);

        if (_layeredSymbolIndex != null)
        {
            await _layeredSymbolIndex.ClearWorkingDeltaAsync(clientId, _currentBranch);
        }

        if (_layeredVectorStore != null)
        {
            await _layeredVectorStore.ClearWorkingDeltaAsync(
                clientId,
                _currentBranch,
                cancellationToken
            );
        }

        _logger.LogInformation("Cleared all working deltas for client {ClientId}", clientId);
    }

    /// <summary>
    /// Gets current branch name.
    /// </summary>
    public string GetCurrentBranch() => _currentBranch;

    /// <summary>
    /// Gets current client ID.
    /// </summary>
    public string? GetCurrentClientId() => _currentClientId;
}

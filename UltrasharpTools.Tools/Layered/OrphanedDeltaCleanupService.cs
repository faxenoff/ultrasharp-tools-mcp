using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Service for cleaning up orphaned branch deltas (deltas for branches that no longer exist in git).
/// Runs periodically to reclaim storage and memory.
/// Phase 6.3: Orphaned Deltas Cleanup
/// </summary>
public class OrphanedDeltaCleanupService
{
private readonly IGitService _gitService;
private readonly LayeredCacheManager? _symbolCacheManager;
private readonly VectorCacheManager? _vectorCacheManager;
private readonly ILogger<OrphanedDeltaCleanupService> _logger;

public OrphanedDeltaCleanupService(
IGitService gitService,
LayeredCacheManager? symbolCacheManager = null,
VectorCacheManager? vectorCacheManager = null,
ILogger<OrphanedDeltaCleanupService>? logger = null)
{
_gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
_symbolCacheManager = symbolCacheManager;
_vectorCacheManager = vectorCacheManager;
_logger = logger ?? NullLogger<OrphanedDeltaCleanupService>.Instance;

_logger.LogInformation(
"OrphanedDeltaCleanupService initialized. Symbol cache: {HasSymbols}, Vector cache: {HasVectors}",
symbolCacheManager != null, vectorCacheManager != null);
}

/// <summary>
/// Finds and removes orphaned branch deltas.
/// An orphaned delta is a delta for a branch that no longer exists in git.
/// </summary>
public async Task CleanupOrphanedDeltasAsync(string solutionPath, CancellationToken cancellationToken = default)
{
_logger.LogInformation("Starting orphaned delta cleanup...");

var startTime = DateTimeOffset.UtcNow;
var totalCleaned = 0;

try
{
// Get all branches from git
var gitBranches = await _gitService.GetAllBranchesAsync(solutionPath, cancellationToken);
var gitBranchSet = new HashSet<string>(gitBranches, Infrastructure.FastStringComparer.OrdinalIgnoreCase);

_logger.LogInformation("Found {Count} branches in git repository", gitBranches.Count);

// Cleanup symbol cache deltas
if (_symbolCacheManager != null)
{
var symbolCleaned = await CleanupSymbolDeltasAsync(gitBranchSet, cancellationToken);
totalCleaned += symbolCleaned;
}

// Cleanup vector cache deltas
if (_vectorCacheManager != null)
{
var vectorCleaned = await CleanupVectorDeltasAsync(gitBranchSet, cancellationToken);
totalCleaned += vectorCleaned;
}

var elapsed = DateTimeOffset.UtcNow - startTime;

_logger.LogInformation(
"Orphaned delta cleanup completed. Removed {Count} orphaned deltas in {Elapsed}ms",
totalCleaned, elapsed.TotalMilliseconds);
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to cleanup orphaned deltas");
throw;
}
}

/// <summary>
/// Cleanup orphaned symbol deltas.
/// </summary>
private async Task<int> CleanupSymbolDeltasAsync(
HashSet<string> gitBranches,
CancellationToken cancellationToken)
{
if (_symbolCacheManager == null)
return 0;

try
{
// Get all cached branch names
var cachedBranches = await _symbolCacheManager.GetAllBranchNamesAsync(cancellationToken);
var orphaned = cachedBranches.Where(b => !gitBranches.Contains(b)).ToList();

_logger.LogInformation(
"Found {Count} orphaned symbol deltas out of {Total} cached",
orphaned.Count, cachedBranches.Count);

// Delete orphaned deltas
foreach (var branch in orphaned)
{
cancellationToken.ThrowIfCancellationRequested();

try
{
await _symbolCacheManager.DeleteBranchDeltaAsync(branch, cancellationToken);
_logger.LogDebug("Deleted orphaned symbol delta: {Branch}", branch);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to delete orphaned symbol delta: {Branch}", branch);
}
}

return orphaned.Count;
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to cleanup symbol deltas");
return 0;
}
}

/// <summary>
/// Cleanup orphaned vector deltas.
/// </summary>
private async Task<int> CleanupVectorDeltasAsync(
HashSet<string> gitBranches,
CancellationToken cancellationToken)
{
if (_vectorCacheManager == null)
return 0;

try
{
// Get all cached branch names
var cachedBranches = await _vectorCacheManager.GetAllBranchNamesAsync(cancellationToken);
var orphaned = cachedBranches.Where(b => !gitBranches.Contains(b)).ToList();

_logger.LogInformation(
"Found {Count} orphaned vector deltas out of {Total} cached",
orphaned.Count, cachedBranches.Count);

// Delete orphaned deltas
foreach (var branch in orphaned)
{
cancellationToken.ThrowIfCancellationRequested();

try
{
await _vectorCacheManager.DeleteVectorDeltaAsync(branch, cancellationToken);
_logger.LogDebug("Deleted orphaned vector delta: {Branch}", branch);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to delete orphaned vector delta: {Branch}", branch);
}
}

return orphaned.Count;
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to cleanup vector deltas");
return 0;
}
}

/// <summary>
/// Gets list of orphaned branches (cached but not in git).
/// </summary>
public async Task<List<string>> GetOrphanedBranchesAsync(string solutionPath, CancellationToken cancellationToken = default)
{
var orphaned = new HashSet<string>(Infrastructure.FastStringComparer.OrdinalIgnoreCase);

try
{
// Get all branches from git
var gitBranches = await _gitService.GetAllBranchesAsync(solutionPath, cancellationToken);
var gitBranchSet = new HashSet<string>(gitBranches, Infrastructure.FastStringComparer.OrdinalIgnoreCase);

// Check symbol cache
if (_symbolCacheManager != null)
{
var cachedBranches = await _symbolCacheManager.GetAllBranchNamesAsync(cancellationToken);
foreach (var branch in cachedBranches)
{
if (!gitBranchSet.Contains(branch))
{
orphaned.Add(branch);
}
}
}

// Check vector cache
if (_vectorCacheManager != null)
{
var cachedBranches = await _vectorCacheManager.GetAllBranchNamesAsync(cancellationToken);
foreach (var branch in cachedBranches)
{
if (!gitBranchSet.Contains(branch))
{
orphaned.Add(branch);
}
}
}

return orphaned.ToList();
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to get orphaned branches");
return new List<string>();
}
}
}

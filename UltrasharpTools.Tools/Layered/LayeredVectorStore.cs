
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers;

using System.Numerics;

using System.Runtime.InteropServices;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Layered vector store with three-layer architecture:
/// - Layer 0: Base vectors (main branch, shared)
/// - Layer 1: Branch deltas (per-branch committed changes, cached)
/// - Layer 2: Working deltas (per-client uncommitted changes, in-memory)
/// Phase 4.1: Vector Integration
/// </summary>
public class LayeredVectorStore : IAsyncDisposable
{
private readonly VectorStore _baseVectors;
private readonly EmbeddingGenerator _embeddingGenerator;
private readonly LayeredIndexingOptions _options;
private readonly ILogger<LayeredVectorStore> _logger;
private readonly SemaphoreSlim _updateLock = new(1, 1);

// Layer 1: Branch deltas (LRU cache)
private readonly Infrastructure.LruCache<string, VectorDelta> _branchDeltaCache;
private readonly ConcurrentDictionary<string, VectorDelta> _branchDeltas = new();

// Layer 2: Working deltas per client
private readonly ConcurrentDictionary<string, VectorDelta> _workingDeltas = new();

// Optional: Cache manager for persistent storage
private readonly VectorCacheManager? _cacheManager;

// Track current solution for delta computation
private Solution? _currentSolution;
private string _currentSolutionPath = string.Empty;

public LayeredVectorStore(
VectorStore baseVectors,
EmbeddingGenerator embeddingGenerator,
LayeredIndexingOptions options,
IGitService? gitService = null,
VectorCacheManager? cacheManager = null,
ILogger<LayeredVectorStore>? logger = null)
{
_baseVectors = baseVectors ?? throw new ArgumentNullException(nameof(baseVectors));
_embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
_options = options ?? throw new ArgumentNullException(nameof(options));
_logger = logger ?? NullLogger<LayeredVectorStore>.Instance;
_cacheManager = cacheManager;

// Initialize LRU cache with eviction callback
_branchDeltaCache = new Infrastructure.LruCache<string, VectorDelta>(maxSize: options.MaxBranchDeltas);
_branchDeltaCache.OnEvict += OnBranchDeltaEvicted;

_logger.LogInformation(
"LayeredVectorStore initialized with max {MaxBranches} branch deltas, persistence: {Persistence}, git integration: {GitEnabled}",
options.MaxBranchDeltas, cacheManager != null, gitService != null);
}

/// <summary>
/// Build base vectors from solution (Layer 0).
/// </summary>
public async Task BuildFromSolutionAsync(
Solution solution,
string solutionPath,
CancellationToken cancellationToken = default)
{
_currentSolution = solution;
_currentSolutionPath = solutionPath;

_logger.LogInformation("Building base vectors from solution: {SolutionPath}", solutionPath);

// Note: Base vectors should already be populated by existing symbol indexing
// This method just tracks the current solution for delta computation
// Actual embedding generation happens via LazyEmbeddingGenerator

_logger.LogInformation("Base vectors ready");
}

/// <summary>
/// Search for similar code across all three layers.
/// </summary>
public async Task<List<SimilarityResult>> SearchAsync(
string clientId,
string branch,
string query,
int topK,
float minSimilarity = 0.0f,
CancellationToken cancellationToken = default)
{
// Generate query embedding
var queryEmbedding = await _embeddingGenerator.EmbedAsync(query, cancellationToken);
if (queryEmbedding == null || queryEmbedding.Length == 0)
{
_logger.LogWarning("Failed to generate query embedding for: {Query}", query);
return new List<SimilarityResult>();
}

// Layer 0: Search base vectors
var baseResults = await _baseVectors.SearchAsync(queryEmbedding, topK, minSimilarity, cancellationToken);
_logger.LogDebug("Layer 0 (base) returned {Count} results", baseResults.Count);

// Convert to internal format for delta application (avoid LINQ allocation)
var vectorResults = new List<VectorSearchResult>(baseResults.Count);
foreach (var r in baseResults)
{
vectorResults.Add(new VectorSearchResult
{
SymbolId = r.Id,
Embedding = Array.Empty<float>(), // Not needed for scoring, use singleton
Score = r.Similarity
});
}

// Layer 1: Apply branch delta (if not main branch)
if (branch != "main" && branch != "master")
{
var branchDelta = await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
if (branchDelta != null)
{
vectorResults = ApplyVectorDelta(vectorResults, branchDelta, queryEmbedding, topK);
_logger.LogDebug("Layer 1 (branch) applied {Changes} changes", branchDelta.TotalChanges);
}
}

// Layer 2: Apply working delta
var workingKey = GetWorkingDeltaKey(clientId, branch);
if (_workingDeltas.TryGetValue(workingKey, out var workingDelta))
{
vectorResults = ApplyVectorDelta(vectorResults, workingDelta, queryEmbedding, topK);
_logger.LogDebug("Layer 2 (working) applied {Changes} changes", workingDelta.TotalChanges);
}

// Convert back to SimilarityResult format (avoid LINQ allocation)
var results = new List<SimilarityResult>(vectorResults.Count);
for (int i = 0; i < vectorResults.Count; i++)
{
var r = vectorResults[i];
results.Add(new SimilarityResult
{
Id = r.SymbolId,
Content = r.SymbolEntry?.CanonicalFqn ?? string.Empty,
Similarity = r.Score,
Rank = i + 1,
Metadata = r.SymbolEntry != null ? System.Text.Json.JsonSerializer.Serialize(r.SymbolEntry) : null
});
}

return results;
}

/// <summary>
/// Update working delta with a new or modified symbol embedding.
/// </summary>
public async Task UpdateWorkingDeltaAsync(
string clientId,
string branch,
string symbolId,
string code,
CancellationToken cancellationToken = default)
{
await _updateLock.WaitAsync(cancellationToken);
try
{
var workingKey = GetWorkingDeltaKey(clientId, branch);
var delta = _workingDeltas.GetOrAdd(workingKey, _ => new VectorDelta
{
BranchName = branch
});

// Generate embedding for the code
var embedding = await _embeddingGenerator.EmbedAsync(code, cancellationToken);
if (embedding == null || embedding.Length == 0)
{
_logger.LogWarning("Failed to generate embedding for symbol {SymbolId}", symbolId);
return;
}

// Check if symbol exists in base or was previously added
bool isNewSymbol = !delta.ModifiedEmbeddings.ContainsKey(symbolId);

if (isNewSymbol)
{
delta.AddedEmbeddings[symbolId] = embedding;
_logger.LogDebug("Added embedding for new symbol {SymbolId} in working delta", symbolId);
}
else
{
delta.ModifiedEmbeddings[symbolId] = embedding;
_logger.LogDebug("Updated embedding for modified symbol {SymbolId} in working delta", symbolId);
}

delta.LastModified = DateTimeOffset.UtcNow;
}
finally
{
_updateLock.Release();
}
}

/// <summary>
/// Clear working delta for a client.
/// </summary>
public async Task ClearWorkingDeltaAsync(
string clientId,
string branch,
CancellationToken cancellationToken = default)
{
await _updateLock.WaitAsync(cancellationToken);
try
{
var workingKey = GetWorkingDeltaKey(clientId, branch);
if (_workingDeltas.TryRemove(workingKey, out var removed))
{
_logger.LogInformation(
"Cleared working delta for client {ClientId}, branch {Branch} ({Changes} changes)",
clientId, branch, removed.TotalChanges);
}
}
finally
{
_updateLock.Release();
}
}

/// <summary>
/// Promote working delta to branch delta (after git commit).
/// </summary>
public async Task PromoteWorkingToBranchAsync(
string clientId,
string branch,
string newCommitSha,
CancellationToken cancellationToken = default)
{
await _updateLock.WaitAsync(cancellationToken);
try
{
var workingKey = GetWorkingDeltaKey(clientId, branch);
if (!_workingDeltas.TryRemove(workingKey, out var workingDelta))
{
_logger.LogWarning("No working delta to promote for client {ClientId}, branch {Branch}",
clientId, branch);
return;
}

// Get or create branch delta
var branchDelta = await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
if (branchDelta == null)
{
branchDelta = new VectorDelta
{
BranchName = branch,
BaseCommitSha = newCommitSha
};
}

// Merge working delta into branch delta
branchDelta.MergeWith(workingDelta);
branchDelta.BaseCommitSha = newCommitSha;

// Update cache
_branchDeltaCache.Add(branch, branchDelta);
_branchDeltas[branch] = branchDelta;

// Save to persistent storage
if (_cacheManager != null)
{
await _cacheManager.SaveVectorDeltaAsync(branchDelta, cancellationToken);
}

_logger.LogInformation(
"Promoted working delta to branch delta: {Branch} ({Changes} changes)",
branch, workingDelta.TotalChanges);
}
finally
{
_updateLock.Release();
}
}

/// <summary>
/// Ensure branch delta is loaded (from storage or git diff).
/// </summary>
public async Task EnsureBranchDeltaAsync(
string branch,
CancellationToken cancellationToken = default)
{
if (branch == "main" || branch == "master")
return; // Main branch doesn't have delta

await GetOrLoadBranchDeltaAsync(branch, cancellationToken);
}

// Private helpers

private async Task<VectorDelta?> GetOrLoadBranchDeltaAsync(
string branch,
CancellationToken cancellationToken)
{
// Check in-memory cache
if (_branchDeltaCache.TryGet(branch, out var cached))
{
_logger.LogDebug("Vector delta cache hit: {Branch}", branch);
return cached;
}

// Try to load from persistent storage
VectorDelta? delta = null;
if (_cacheManager != null)
{
delta = await _cacheManager.LoadVectorDeltaAsync(branch, cancellationToken);
if (delta != null)
{
_logger.LogInformation("Loaded vector delta from persistent storage: {Branch}", branch);
}
}

// If not in storage, create empty delta
if (delta == null)
{
// Create empty delta - will be populated by LazyEmbeddingGenerator
delta = new VectorDelta
{
BranchName = branch,
BaseCommitSha = string.Empty // Will be updated by git integration
};

if (_cacheManager != null)
{
await _cacheManager.SaveVectorDeltaAsync(delta, cancellationToken);
}

_logger.LogInformation("Created new vector delta: {Branch}", branch);
}

// Fallback to empty delta
if (delta == null)
{
delta = new VectorDelta { BranchName = branch };
}

// Add to cache
_branchDeltaCache.Add(branch, delta);
_branchDeltas[branch] = delta;

return delta;
}

private void OnBranchDeltaEvicted(string branch, VectorDelta delta)
{
// Save to persistent storage before eviction
if (_cacheManager != null)
{
_ = Task.Run(async () =>
{
try
{
await _cacheManager.SaveVectorDeltaAsync(delta, CancellationToken.None);
_logger.LogDebug("Saved evicted vector delta to storage: {Branch}", branch);
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to save evicted vector delta: {Branch}", branch);
}
});
}

_branchDeltas.TryRemove(branch, out _);
_logger.LogDebug("Evicted vector delta from cache: {Branch}", branch);
}

/// <summary>
/// Apply vector delta to base results with minimal allocations.
/// Modifies results in-place and uses List.Sort instead of LINQ for better performance.
/// </summary>
private List<VectorSearchResult> ApplyVectorDelta(
List<VectorSearchResult> baseResults,
VectorDelta delta,
float[] queryEmbedding,
int topK)
{
// Pre-allocate list with estimated capacity to avoid resizing
int estimatedCapacity = baseResults.Count + delta.AddedEmbeddings.Count;
var filtered = new List<VectorSearchResult>(estimatedCapacity);

// Filter out deleted symbols and update modified in single pass
foreach (var result in baseResults)
{
if (delta.DeletedSymbolIds.Contains(result.SymbolId))
continue;

if (delta.ModifiedEmbeddings.TryGetValue(result.SymbolId, out var newEmbedding))
{
result.Embedding = newEmbedding;
result.Score = CalculateCosineSimilarity(queryEmbedding, newEmbedding);
}

filtered.Add(result);
}

// Add new embeddings from delta
foreach (var (symbolId, embedding) in delta.AddedEmbeddings)
{
var score = CalculateCosineSimilarity(queryEmbedding, embedding);
filtered.Add(new VectorSearchResult
{
SymbolId = symbolId,
Embedding = embedding,
Score = score
});
}

// Sort in-place (descending by score) - faster than LINQ OrderByDescending
filtered.Sort((a, b) => b.Score.CompareTo(a.Score));

// Take topK manually to avoid extra allocation
if (filtered.Count > topK)
{
filtered.RemoveRange(topK, filtered.Count - topK);
}

return filtered;
}

/// <summary>
/// SIMD-optimized cosine similarity calculation.
/// Uses System.Numerics.Vector for parallel float operations.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float CalculateCosineSimilarity(float[] a, float[] b)
{
if (a.Length != b.Length || a.Length == 0)
return 0f;

return CalculateCosineSimilaritySimd(a, b);
}

/// <summary>
/// SIMD-accelerated implementation using Vector&lt;float&gt;.
/// Processes multiple floats in parallel (4-8 at a time depending on CPU).
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float CalculateCosineSimilaritySimd(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
var vectorSize = Vector<float>.Count;
var length = a.Length;
var vectorLength = length - (length % vectorSize);

var dotProductVec = Vector<float>.Zero;
var normAVec = Vector<float>.Zero;
var normBVec = Vector<float>.Zero;

// SIMD loop: process vectorSize elements at once
int i = 0;
for (; i < vectorLength; i += vectorSize)
{
var vecA = new Vector<float>(a.Slice(i, vectorSize));
var vecB = new Vector<float>(b.Slice(i, vectorSize));

dotProductVec += vecA * vecB;
normAVec += vecA * vecA;
normBVec += vecB * vecB;
}

// Horizontal sum of SIMD vectors
float dotProduct = Vector.Dot(dotProductVec, Vector<float>.One);
float normA = Vector.Dot(normAVec, Vector<float>.One);
float normB = Vector.Dot(normBVec, Vector<float>.One);

// Scalar remainder loop for remaining elements
for (; i < length; i++)
{
var valA = a[i];
var valB = b[i];
dotProduct += valA * valB;
normA += valA * valA;
normB += valB * valB;
}

// Avoid division by zero
if (normA == 0f || normB == 0f)
return 0f;

// Use MathF for single-precision sqrt (faster than Math.Sqrt)
return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
}

private static string GetWorkingDeltaKey(string clientId, string branch) =>
$"{clientId}::{branch}";

public async ValueTask DisposeAsync()
{
// Save all branch deltas before disposal
if (_cacheManager != null)
{
var saveTasks = _branchDeltas.Values
.Select(delta => _cacheManager.SaveVectorDeltaAsync(delta, CancellationToken.None));
await Task.WhenAll(saveTasks);
}

_updateLock.Dispose();

_logger.LogInformation("LayeredVectorStore disposed");
}
}

# Three-Layer Architecture Extension

## 🔄 Extended Design: Multi-Layer Architecture

### Problem Extension: Uncommitted Changes

**Original problem:** Different branches need isolation ✅ (solved in LAYERED_INDEXING_DESIGN.md)

**New problem:** Each developer has uncommitted local changes!

```
Developer A (feature/auth branch):
  - Committed: AuthService.Login() ✅ (in git)
  - Uncommitted: AuthService.Logout() ⚠️ (working directory)
  - Should see BOTH in search results

Developer B (same feature/auth branch):
  - Committed: AuthService.Login() ✅ (same as A)
  - Uncommitted: AuthService.Register() ⚠️ (different from A!)
  - Should NOT see Developer A's uncommitted changes!
```

**This applies to:**
- ✅ **RemoteServer (SSE):** Different clients in same branch with different uncommitted changes
- ✅ **MCPServer (stdio):** Single client, but uncommitted changes need separate layer

---

## 🏗️ Three-Layer Architecture (Like Docker Layers)

```
┌─────────────────────────────────────────────────────────────────┐
│  Layer 0: Base Index (main branch, committed, read-only)       │
│  - Shared by ALL clients                                        │
│  - Rebuilt only when main branch updates                        │
│  - ~355K symbols, ~600 MB memory                                │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  Layer 1: Branch Delta (committed to branch, read-only)         │
│  - Per-branch: feature/auth, feature/payments, etc.             │
│  - Shared by clients in SAME branch                             │
│  - Rebuilt when branch commits change                           │
│  - ~10-50 MB per branch                                         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│  Layer 2: Working Directory (uncommitted, read-write)           │
│  - Per-client/session: Client A, Client B, etc.                 │
│  - NOT shared between clients (even in same branch!)            │
│  - Updated on EVERY code modification (AddMember, etc.)         │
│  - ~1-10 MB per client                                          │
└─────────────────────────────────────────────────────────────────┘
```

### Query Composition

```
Query for Client A in feature/auth with uncommitted changes:

Result = Layer 0 (base symbols)
       ∪ Layer 1 (feature/auth committed delta)
       ∪ Layer 2 (Client A uncommitted delta)
       - Deleted symbols from all layers
```

---

## 📊 Three-Layer Data Model

### Layer 2: Working Directory Delta (NEW!)
```csharp
public class WorkingDirectoryDelta {
    public string ClientId { get; init; }       // Session ID or client identifier
    public string BranchName { get; init; }     // Which branch this is for

    // Uncommitted changes (NOT in git)
    // Mutable - updated on every AddMember/OverwriteMember
    public ConcurrentDictionary<string, SymbolIndexEntry> AddedSymbols { get; }
    public ConcurrentDictionary<string, SymbolIndexEntry> ModifiedSymbols { get; }
    public ConcurrentBag<string> DeletedSymbolIds { get; }

    public DateTime LastModified { get; set; }

    public IEnumerable<SymbolIndexEntry> Apply(IEnumerable<SymbolIndexEntry> layerResults) {
        foreach (var symbol in layerResults) {
            if (DeletedSymbolIds.Contains(symbol.SymbolId)) {
                continue;
            }

            if (ModifiedSymbols.TryGetValue(symbol.SymbolId, out var modified)) {
                yield return modified;
            } else {
                yield return symbol;
            }
        }

        foreach (var added in AddedSymbols.Values) {
            yield return added;
        }
    }

    // Clear when git commit happens
    public void Clear() {
        AddedSymbols.Clear();
        ModifiedSymbols.Clear();
        DeletedSymbolIds.Clear();
    }

    // Promote to BranchDelta when committed
    public BranchDelta PromoteToBranchDelta() {
        return new BranchDelta {
            BranchName = BranchName,
            AddedSymbols = AddedSymbols.ToImmutableDictionary(),
            ModifiedSymbols = ModifiedSymbols.ToImmutableDictionary(),
            DeletedSymbolIds = DeletedSymbolIds.ToImmutableHashSet()
        };
    }
}
```

---

## 🎯 Three-Layer Query Architecture

```csharp
public class ThreeLayerSymbolIndex {
    // Layer 0: Base (main, shared, immutable)
    private readonly BaseSymbolIndex _base;

    // Layer 1: Branch deltas (per-branch, shared, immutable)
    private readonly ConcurrentDictionary<string, BranchDelta> _branchDeltas;

    // Layer 2: Working directory deltas (per-client, mutable)
    private readonly ConcurrentDictionary<string, WorkingDirectoryDelta> _workingDeltas;

    public async Task<IEnumerable<SymbolIndexEntry>> FindAsync(
        string clientId,
        string branch,
        string searchPattern,
        CancellationToken ct)
    {
        // Layer 0: Query base index
        var layer0Results = _base.Find(searchPattern);

        // Layer 1: Apply branch delta (if not main)
        IEnumerable<SymbolIndexEntry> layer1Results = layer0Results;
        if (branch != "main") {
            var branchDelta = await GetOrLoadBranchDeltaAsync(branch, ct);
            layer1Results = branchDelta.Apply(layer0Results);
        }

        // Layer 2: Apply working directory delta (uncommitted changes)
        var workingDelta = GetOrCreateWorkingDelta(clientId, branch);
        var finalResults = workingDelta.Apply(layer1Results);

        return finalResults;
    }

    public async Task UpdateWorkingDeltaAsync(
        string clientId,
        string branch,
        SymbolIndexEntry symbol,
        CancellationToken ct)
    {
        var workingDelta = GetOrCreateWorkingDelta(clientId, branch);
        workingDelta.AddedSymbols.AddOrUpdate(symbol.SymbolId, symbol, (k, old) => symbol);
        workingDelta.LastModified = DateTime.UtcNow;

        // Optional: Persist for crash recovery
        await _sqliteCache.SaveWorkingDeltaAsync(clientId, workingDelta, ct);
    }
}
```

---

## 🔄 Git Operations Integration

### Git Commit (Promote Layer 2 → Layer 1)
```csharp
public async Task CommitChangesAsync(string clientId, string branch, string message) {
    // Get working delta (uncommitted)
    if (!_workingDeltas.TryGetValue(clientId, out var workingDelta)) {
        return;
    }

    // Git commit
    await GitCommitAsync(branch, message);
    var newCommitSha = await GetGitCommitShaAsync(branch);

    // Merge working delta into branch delta
    var branchDelta = await GetOrLoadBranchDeltaAsync(branch);
    var updatedBranchDelta = MergeDelta(branchDelta, workingDelta, newCommitSha);

    // Save and clear
    _branchDeltas[branch] = updatedBranchDelta;
    await _sqliteCache.SaveBranchDeltaAsync(updatedBranchDelta);
    workingDelta.Clear();
}
```

### Git Pull (Preserve Layer 2)
```csharp
public async Task PullChangesAsync(string clientId, string branch) {
    // Save uncommitted changes
    WorkingDirectoryDelta? savedDelta = null;
    if (_workingDeltas.TryGetValue(clientId, out var wd)) {
        savedDelta = wd;
    }

    // Git pull
    await GitPullAsync(branch);

    // Rebuild Layer 1 from new commits
    var branchDelta = await ComputeDeltaFromGitDiffAsync(branch, "main");
    _branchDeltas[branch] = branchDelta;

    // Restore Layer 2
    if (savedDelta != null) {
        _workingDeltas[clientId] = savedDelta;
    }
}
```

---

## 📊 Memory Usage: 10 Clients Example

```
Scenario:
  - 3 branches: main, feature/auth, feature/payments
  - 10 clients:
    - 4 in main (no uncommitted)
    - 4 in feature/auth (2 with uncommitted)
    - 2 in feature/payments (1 with uncommitted)

Memory:
  Layer 0 (Base):                      600 MB  (shared)
  Layer 1:
    - feature/auth delta:               50 MB  (shared by 4)
    - feature/payments delta:           30 MB  (shared by 2)
  Layer 2:
    - Client 5 working delta:            5 MB  (private)
    - Client 6 working delta:            8 MB  (private)
    - Client 10 working delta:           3 MB  (private)

Total: 600 + 80 + 16 = 696 MB

vs Isolated Solutions: 10 × 600 MB = 6 GB
Savings: 88% memory reduction
```

---

## 🚀 Client ID Strategies

### MCPServer (stdio)
```csharp
// Process ID unique per Claude window
var clientId = $"stdio_{Process.GetCurrentProcess().Id}";
```

### RemoteServer (HTTP SSE)
```csharp
// HTTP header
[FromHeader(Name = "X-Client-Id")] string? clientId

// Session ID
var clientId = HttpContext.Session.Id;

// Connection ID
var clientId = HttpContext.Connection.Id;
```

---

## 📋 Implementation Plan Extension

### Phase 0: Three-Layer Foundation (3-4 days) - NEW!
1. Implement WorkingDirectoryDelta class
2. Implement ThreeLayerSymbolIndex
3. Add client ID tracking
4. Update SQLite schema for working deltas
5. Add git integration (commit/pull/discard)

### Phases 1-7: Continue from LAYERED_INDEXING_DESIGN.md (15-21 days)

**Total: 18-25 days**

---

## ✅ Success Criteria (Extended)

- [ ] 10+ concurrent clients with uncommitted changes
- [ ] <10 MB memory per working delta
- [ ] Uncommitted changes immediately visible
- [ ] Uncommitted changes isolated per client
- [ ] Git commit promotes Layer 2 → Layer 1
- [ ] Git pull preserves uncommitted changes

---

## 🎯 Benefits Summary

**Memory:** 10 clients: 6 GB → 700 MB (88% reduction)

**Performance:**
- Uncommitted change visible: Instant (vs 33s reload)
- Query with uncommitted: <5ms (vs 5-10s Roslyn)

**User Experience:**
```
Before:
  AddMember → 33s reload → SearchDefinitions

After:
  AddMember → Layer 2 update (instant) → SearchDefinitions (5ms)
```

---

## 🚀 Architecture Summary

```
Layer 0 (Base):        Main branch, committed, shared, immutable
Layer 1 (Branch):      Feature branch, committed, shared, immutable
Layer 2 (Working):     Uncommitted changes, per-client, mutable

Query = Layer 0 ∪ Layer 1 ∪ Layer 2 - Deleted
```

This design solves:
- ✅ Branch isolation (Layer 1)
- ✅ Uncommitted isolation (Layer 2)
- ✅ Memory efficiency (shared base + deltas)
- ✅ Fast queries (in-memory merge)
- ✅ Git workflow (commit, pull, discard)

# Layered Branch-Aware Indexing для RemoteServer

## 🎯 Проблема: Multi-Client Branch Isolation

### Текущая архитектура RemoteServer (SSE)

```
Multiple Claude Clients
    ↓
Single HTTP Server (sseserver.exe)
    ↓
Single ISolutionManager (DI Singleton)
    ↓
Single Solution + Single FastSymbolIndex
    ↓
❌ ПРОБЛЕМА: Все клиенты видят одно и то же состояние!
```

### Реальный сценарий

```
Client A (User Alice):
  - Working in branch: feature/auth
  - Adds method: AuthService.Login()
  - Expects to find: AuthService.Login() ✅

Client B (User Bob):
  - Working in branch: main
  - Should NOT see: AuthService.Login() ❌
  - But sees it because index is shared! 💥
```

### Текущие ограничения

| Проблема | Impact | Severity |
|----------|--------|----------|
| **Shared Solution** | Client A changes affect Client B | 🔴 Critical |
| **No branch isolation** | Can't work in different branches | 🔴 Critical |
| **Race conditions** | Concurrent modifications conflict | 🟡 High |
| **Stale index** | Index doesn't reflect git checkout | 🟡 High |

---

## 🏗️ Solution Approaches: Comparison

### Option 1: Isolated Solutions (Separate SolutionManager per client)

```
Client A → SolutionManager A → Solution (feature/auth) → Index A
Client B → SolutionManager B → Solution (main) → Index B
Client C → SolutionManager C → Solution (bugfix/crash) → Index C
```

**Architecture:**
```csharp
// DI Scoped instead of Singleton
services.AddScoped<ISolutionManager, SolutionManager>();
services.AddScoped<FastSymbolIndex>();

// Each HTTP request gets own instance
public class AnalysisTools {
    private readonly ISolutionManager _solutionManager; // Scoped per request
}
```

**Pros:**
- ✅ Complete isolation between clients
- ✅ Simple conceptual model
- ✅ No shared state conflicts

**Cons:**
- ❌ **Memory explosion:** 3 clients × 600 MB = 1.8 GB RAM
- ❌ **Git conflicts:** Multiple checkouts to different branches (working directory shared)
- ❌ **Slow branch switches:** Full rebuild on every checkout (33s)
- ❌ **Duplicate symbols:** 80-90% symbols are identical across branches

**Verdict:** ❌ Not scalable for production

---

### Option 2: Layered In-Memory Index (Base + Deltas)

```
Base Index (main branch, read-only, shared in memory)
    ↓
├─ Delta Layer (feature/auth) → Added/Modified/Deleted symbols for Client A
├─ Delta Layer (feature/payments) → Added/Modified/Deleted symbols for Client B
└─ Delta Layer (bugfix/crash) → Added/Modified/Deleted symbols for Client C
```

**Architecture:**
```csharp
public class LayeredSymbolIndex {
    // Shared base (main branch) - ONE copy in memory
    private FastSymbolIndex _baseIndex;

    // Per-branch deltas - lightweight overlay
    private ConcurrentDictionary<string, BranchDelta> _deltas;

    public IEnumerable<SymbolIndexEntry> Find(string branch, string searchTerm) {
        var baseResults = _baseIndex.Find(searchTerm);

        if (_deltas.TryGetValue(branch, out var delta)) {
            return delta.Apply(baseResults); // Merge base + delta
        }

        return baseResults; // main branch → no delta
    }
}

public class BranchDelta {
    public HashSet<string> AddedSymbols { get; set; }
    public HashSet<string> ModifiedSymbols { get; set; }
    public HashSet<string> DeletedSymbols { get; set; }

    public IEnumerable<SymbolIndexEntry> Apply(IEnumerable<SymbolIndexEntry> baseResults) {
        return baseResults
            .Where(s => !DeletedSymbols.Contains(s.SymbolId))  // Remove deleted
            .Concat(AddedSymbols.Select(id => GetSymbol(id)))  // Add new
            .Select(s => ModifiedSymbols.Contains(s.SymbolId) ? GetModified(s) : s); // Update modified
    }
}
```

**Pros:**
- ✅ **Memory efficient:** Base shared (~500 MB), deltas tiny (~10-50 MB each)
- ✅ **Fast queries:** O(1) Bloom filter on base, O(delta_size) merge
- ✅ **No git checkouts:** Virtual branches, no disk I/O
- ✅ **Fast branch creation:** Build delta from git diff (1-3 seconds)

**Cons:**
- ⚠️ **Complex merge logic:** Every query needs base + delta merge
- ⚠️ **Delta rebase:** If base changes (main updated), deltas need rebuild
- ⚠️ **Not persistent:** Deltas lost on server restart
- ⚠️ **Memory growth:** Many branches × many deltas

**Verdict:** ✅ Good for short-lived branches, needs persistence

---

### Option 3: SQLite Layered Storage (ATTACH DATABASE)

```
base.db (main branch, read-only, shared on disk)
    ↓
├─ feature_auth.db (delta for Client A)
├─ feature_payments.db (delta for Client B)
└─ bugfix_crash.db (delta for Client C)
```

**Architecture:**
```sql
-- Attach delta database
ATTACH DATABASE 'branches/feature_auth.db' AS delta;

-- Query combines base + delta
SELECT * FROM base.symbols WHERE name LIKE '%Login%'
UNION ALL
SELECT * FROM delta.symbols WHERE name LIKE '%Login%'
WHERE id NOT IN (SELECT symbol_id FROM delta.deleted_symbols);
```

**Code:**
```csharp
public class LayeredSymbolCache {
    private readonly string _baseDbPath = "cache/base.db";
    private readonly ConcurrentDictionary<string, string> _branchDbPaths;

    public async Task<List<SymbolIndexEntry>> QueryAsync(string branch, string searchPattern) {
        var deltaDbPath = _branchDbPaths.GetOrAdd(branch, CreateDeltaDb);

        using var conn = new SQLiteConnection(_baseDbPath);
        await conn.OpenAsync();

        // Attach delta database
        var attachCmd = new SQLiteCommand($"ATTACH DATABASE '{deltaDbPath}' AS delta", conn);
        await attachCmd.ExecuteNonQueryAsync();

        // Query with UNION
        var queryCmd = new SQLiteCommand(@"
            SELECT id, name, fqn, kind, flags FROM (
                -- Base symbols
                SELECT * FROM base.symbols
                WHERE name LIKE @pattern
                AND id NOT IN (SELECT symbol_id FROM delta.deleted_symbols)

                UNION ALL

                -- Delta symbols
                SELECT * FROM delta.symbols
                WHERE name LIKE @pattern
            )
        ", conn);
        queryCmd.Parameters.AddWithValue("@pattern", searchPattern);

        return await ExecuteReaderAsync(queryCmd);
    }

    private string CreateDeltaDb(string branch) {
        var deltaPath = $"cache/branches/{branch}.db";

        // Create empty delta schema
        using var conn = new SQLiteConnection(deltaPath);
        conn.Open();

        var createCmd = new SQLiteCommand(@"
            CREATE TABLE IF NOT EXISTS symbols (
                id TEXT PRIMARY KEY,
                name TEXT,
                fqn TEXT,
                kind TEXT,
                flags INTEGER
            );
            CREATE TABLE IF NOT EXISTS deleted_symbols (
                symbol_id TEXT PRIMARY KEY
            );
            CREATE INDEX idx_name ON symbols(name);
        ", conn);
        createCmd.ExecuteNonQuery();

        return deltaPath;
    }
}
```

**Pros:**
- ✅ **Persistent deltas:** Survives server restart
- ✅ **ACID guarantees:** SQLite transactions
- ✅ **Efficient storage:** SQLite compression
- ✅ **SQL power:** Complex queries, joins, indexes
- ✅ **No memory overhead:** Query on-demand from disk

**Cons:**
- ⚠️ **Slower than in-memory:** Disk I/O for every query (~10-50ms)
- ⚠️ **Complex schema:** Need tables for symbols, deleted, modified
- ⚠️ **ATTACH limit:** SQLite limit ~125 attached databases
- ⚠️ **Lock contention:** Multiple clients querying same delta.db

**Verdict:** ✅ Good for persistent branches, acceptable latency

---

### Option 4: Hybrid (In-Memory Base + SQLite Deltas)

```
FastSymbolIndex (in-memory, main branch, shared)
    ↓ Fast O(1) Bloom filter
Deltas in SQLite (per-branch, persistent)
    ↓ Load on-demand to memory
ConcurrentDictionary<string, BranchDelta> (in-memory cache)
```

**Architecture:**
```csharp
public class HybridLayeredIndex {
    // Base index in memory (fast queries)
    private readonly FastSymbolIndex _baseIndex;

    // Delta cache (loaded from SQLite on-demand)
    private readonly ConcurrentDictionary<string, BranchDelta> _deltaCache;

    // Persistent storage
    private readonly LayeredSymbolCache _sqliteCache;

    public async Task<IEnumerable<SymbolIndexEntry>> FindAsync(
        string branch,
        string searchPattern,
        CancellationToken ct)
    {
        // Fast path: Query base index in-memory
        var baseResults = _baseIndex.Find(searchPattern);

        if (branch == "main" || string.IsNullOrEmpty(branch)) {
            return baseResults; // No delta needed
        }

        // Load delta from cache or SQLite
        var delta = await GetOrLoadDeltaAsync(branch, ct);

        // Merge base + delta in-memory
        return delta.Apply(baseResults);
    }

    private async Task<BranchDelta> GetOrLoadDeltaAsync(string branch, CancellationToken ct) {
        // Check in-memory cache
        if (_deltaCache.TryGetValue(branch, out var cached)) {
            return cached;
        }

        // Load from SQLite
        var delta = await _sqliteCache.LoadDeltaAsync(branch, ct);

        // Cache in memory
        _deltaCache.TryAdd(branch, delta);

        return delta;
    }

    public async Task UpdateDeltaAsync(string branch, SymbolIndexEntry symbol, CancellationToken ct) {
        // Update in-memory cache
        var delta = await GetOrLoadDeltaAsync(branch, ct);
        delta.AddOrUpdate(symbol);

        // Persist to SQLite asynchronously
        await _sqliteCache.SaveDeltaAsync(branch, delta, ct);
    }
}
```

**Pros:**
- ✅ **Best of both worlds:** Memory speed + SQLite persistence
- ✅ **Fast queries:** O(1) Bloom on base, O(delta_size) merge in-memory
- ✅ **Persistent deltas:** Survives restarts
- ✅ **Memory efficient:** Deltas loaded on-demand, can be evicted (LRU)
- ✅ **Scalable:** 100+ branches, only active ones in memory

**Cons:**
- ⚠️ **Complex implementation:** Two storage layers to manage
- ⚠️ **Cache coherence:** In-memory vs SQLite sync
- ⚠️ **First query slow:** Delta load from SQLite (~10-50ms)

**Verdict:** ✅ **BEST SOLUTION** - Production-ready

---

## 🎯 Recommended Architecture: Hybrid Layered Index

### High-Level Design

```
┌─────────────────────────────────────────────────────────────┐
│  RemoteServer (HTTP SSE)                                    │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  HybridLayeredIndex (Singleton)                     │  │
│  │                                                       │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │  FastSymbolIndex (main branch, in-memory)    │  │  │
│  │  │  - 355K symbols (~600 MB)                    │  │  │
│  │  │  - Bloom filter + FrozenDictionary           │  │  │
│  │  │  - O(1) queries                              │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  │                                                       │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │  Delta Cache (ConcurrentDictionary)          │  │  │
│  │  │  - feature/auth → BranchDelta (~10-50 MB)    │  │  │
│  │  │  - feature/payments → BranchDelta            │  │  │
│  │  │  - LRU eviction (max 20 branches in memory) │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  │                                                       │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │  SQLite Storage (persistent)                 │  │  │
│  │  │  - base.db (main, read-only)                 │  │  │
│  │  │  - branches/feature_auth.db                  │  │  │
│  │  │  - branches/feature_payments.db              │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  Client A (branch: feature/auth) ───────────────────────► │
│  Client B (branch: main) ────────────────────────────────► │
│  Client C (branch: feature/payments) ────────────────────► │
└─────────────────────────────────────────────────────────────┘
```

### Data Structures

#### BranchDelta (In-Memory)
```csharp
public class BranchDelta {
    public string BranchName { get; init; }
    public string BaseCommitSha { get; set; } // For rebase detection

    // Symbol changes
    public ConcurrentDictionary<string, SymbolIndexEntry> AddedSymbols { get; }
    public ConcurrentDictionary<string, SymbolIndexEntry> ModifiedSymbols { get; }
    public ConcurrentBag<string> DeletedSymbolIds { get; }

    // Metadata
    public DateTime LastModified { get; set; }
    public long SymbolCount => AddedSymbols.Count + ModifiedSymbols.Count;

    public IEnumerable<SymbolIndexEntry> Apply(IEnumerable<SymbolIndexEntry> baseResults) {
        foreach (var symbol in baseResults) {
            // Skip deleted symbols
            if (DeletedSymbolIds.Contains(symbol.SymbolId)) {
                continue;
            }

            // Replace with modified version
            if (ModifiedSymbols.TryGetValue(symbol.SymbolId, out var modified)) {
                yield return modified;
            } else {
                yield return symbol;
            }
        }

        // Add new symbols
        foreach (var added in AddedSymbols.Values) {
            yield return added;
        }
    }
}
```

#### SQLite Schema (Persistent)
```sql
-- base.db (main branch, read-only)
CREATE TABLE symbols (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    simple_name TEXT NOT NULL,
    namespace TEXT,
    canonical_fqn TEXT NOT NULL,
    kind TEXT NOT NULL,  -- Class, Method, Property, etc.
    flags INTEGER NOT NULL,  -- SymbolMetadataFlags bitwise
    file_path TEXT,
    line_number INTEGER,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_name ON symbols(name);
CREATE INDEX idx_simple_name ON symbols(simple_name);
CREATE INDEX idx_namespace ON symbols(namespace);

-- branches/{branch_name}.db (delta per branch)
CREATE TABLE delta_metadata (
    branch_name TEXT PRIMARY KEY,
    base_commit_sha TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    modified_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE added_symbols (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    simple_name TEXT NOT NULL,
    namespace TEXT,
    canonical_fqn TEXT NOT NULL,
    kind TEXT NOT NULL,
    flags INTEGER NOT NULL,
    file_path TEXT,
    line_number INTEGER,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE modified_symbols (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    simple_name TEXT NOT NULL,
    namespace TEXT,
    canonical_fqn TEXT NOT NULL,
    kind TEXT NOT NULL,
    flags INTEGER NOT NULL,
    file_path TEXT,
    line_number INTEGER,
    modified_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (id) REFERENCES base.symbols(id)
);

CREATE TABLE deleted_symbols (
    symbol_id TEXT PRIMARY KEY,
    deleted_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (symbol_id) REFERENCES base.symbols(id)
);

CREATE INDEX idx_added_name ON added_symbols(name);
CREATE INDEX idx_modified_name ON modified_symbols(name);
```

### Query Flow

#### Scenario: Client A searches for "Login" in feature/auth

```
1. Client HTTP Request
   GET /api/search?pattern=Login&branch=feature/auth

2. HybridLayeredIndex.FindAsync()
   ↓
3. FastSymbolIndex.Find("Login")  // Base index
   - Bloom filter check: MightContain("Login") → true
   - FrozenDictionary lookup: _bySimpleName["Login"] → [...]
   - Returns: 15 results from main branch

4. GetOrLoadDeltaAsync("feature/auth")
   - Check _deltaCache: Not found
   - Load from SQLite: branches/feature_auth.db
   - Parse to BranchDelta object
   - Cache in _deltaCache
   - Returns: BranchDelta with 3 added, 1 modified, 0 deleted

5. delta.Apply(baseResults)
   - Filter deleted: None
   - Replace modified: UserService.Login (updated signature)
   - Append added: AuthService.Login, AuthService.LoginAsync, ...
   - Returns: 18 results (15 base + 3 added)

6. HTTP Response
   JSON: [{ name: "Login", fqn: "MyApp.AuthService.Login", ... }, ...]

Performance: ~50-100ms (first query), ~5-10ms (cached delta)
```

### Delta Creation Flow

#### Scenario: Client creates feature/auth branch

```
1. Client calls CreateBranch
   POST /api/branch/create
   Body: { "name": "feature/auth", "from": "main" }

2. HybridLayeredIndex.CreateBranchAsync()
   ↓
3. Get current main commit SHA
   git rev-parse HEAD → "abc123def456"

4. Compute diff between main and feature/auth
   git diff --name-only main..feature/auth → [file1.cs, file2.cs]

5. If branch already exists on disk:
   - Load existing solution (feature/auth)
   - Extract symbols from changed files only
   - Compare with base symbols
   - Build delta: added, modified, deleted

   If branch doesn't exist yet (new branch):
   - Delta = empty
   - Will be populated on first modification

6. Save to SQLite
   - Create branches/feature_auth.db
   - INSERT INTO delta_metadata (branch_name, base_commit_sha)
   - (No symbols yet for new branch)

7. Return success

Performance: ~1-3 seconds (for existing branch with changes)
            ~100ms (for new branch)
```

### Delta Update Flow

#### Scenario: Client A adds method in feature/auth

```
1. Client calls AddMember
   POST /api/modification/add-member
   Headers: { "X-Branch": "feature/auth" }
   Body: { "typeName": "AuthService", "memberCode": "public void Login() {}" }

2. CodeModificationService.ApplyChangesAsync()
   - workspace.TryApplyChanges(newSolution)
   - Save to disk
   - Extract new symbol: AuthService.Login

3. HybridLayeredIndex.UpdateDeltaAsync("feature/auth", newSymbol)
   ↓
4. Get or create delta
   var delta = await GetOrLoadDeltaAsync("feature/auth");

5. Add to delta
   delta.AddedSymbols.TryAdd(newSymbol.SymbolId, newSymbol);
   delta.LastModified = DateTime.UtcNow;

6. Persist to SQLite asynchronously
   await _sqliteCache.SaveDeltaAsync("feature/auth", delta);
   - INSERT INTO added_symbols (id, name, ...) VALUES (...)
   - UPDATE delta_metadata SET modified_at = CURRENT_TIMESTAMP

7. Update in-memory cache
   _deltaCache["feature/auth"] = delta;

8. Return success with linting

Performance: ~100-300ms (incremental update)
```

---

## 📊 Performance Comparison

### Memory Usage

| Architecture | Base Index | Per-Branch Delta | 10 Branches Total |
|--------------|------------|------------------|-------------------|
| **Isolated Solutions** | N/A | 600 MB | **6 GB** ❌ |
| **Layered In-Memory** | 600 MB | 50 MB | **1.1 GB** ✅ |
| **SQLite Only** | 0 MB (disk) | 0 MB (disk) | **~50 MB** (cache) ✅ |
| **Hybrid (Recommended)** | 600 MB | 10 MB (cached) | **700-800 MB** ✅ |

### Query Performance

| Architecture | First Query | Cached Query | Branch Switch |
|--------------|-------------|--------------|---------------|
| **Isolated Solutions** | 5-10s (Roslyn) | <1s (index) | **33s rebuild** ❌ |
| **Layered In-Memory** | 5-10ms | **1-3ms** ✅ | **1-3s** ✅ |
| **SQLite Only** | 50-100ms | 10-50ms | **1-3s** ✅ |
| **Hybrid (Recommended)** | 50-100ms (load delta) | **1-5ms** ✅ | **1-3s** ✅ |

### Branch Operations

| Operation | Isolated | Layered | SQLite | Hybrid |
|-----------|----------|---------|--------|--------|
| **Create branch** | 33s rebuild | 1-3s diff | 1-3s diff | **1-3s** ✅ |
| **Switch branch** | 33s rebuild | Instant (memory) | 10-50ms (load) | **10-50ms** ✅ |
| **Modify code** | 100-500ms | 100-300ms | 100-300ms | **100-300ms** ✅ |
| **Merge to main** | 33s rebuild | 5-10s (promote) | 5-10s (promote) | **5-10s** ✅ |

---

## 🚧 Implementation Challenges

### Challenge 1: Base Rebase (Main Branch Updates)

**Problem:** Main branch gets new commits, feature branches need rebase

```
Initial state:
  main: commit abc123
  feature/auth: based on abc123 + delta

Main updated:
  main: commit def456 (new symbols added)
  feature/auth: still based on abc123 ⚠️ STALE BASE!
```

**Solution: Automatic Rebase Detection**
```csharp
public async Task<bool> IsDeltaStaleAsync(string branch) {
    var delta = await GetOrLoadDeltaAsync(branch);
    var currentMainCommit = await GetGitCommitShaAsync("main");

    return delta.BaseCommitSha != currentMainCommit;
}

public async Task RebaseDeltaAsync(string branch) {
    var oldDelta = await GetOrLoadDeltaAsync(branch);

    // 1. Load NEW base index (updated main)
    await _baseIndex.BuildFromSolutionAsync(mainSolution);

    // 2. Compute NEW delta from git diff
    var newDelta = await ComputeDeltaFromGitDiff(branch, mainCommit);

    // 3. Merge old delta changes (preserve user modifications)
    newDelta.MergeWith(oldDelta);

    // 4. Save updated delta
    await _sqliteCache.SaveDeltaAsync(branch, newDelta);

    _deltaCache[branch] = newDelta;
}
```

**Trigger:** Periodic background task or explicit rebase command

### Challenge 2: Concurrent Delta Updates

**Problem:** Multiple clients modifying same branch simultaneously

```
Client A: AddMember("Foo") → delta.AddedSymbols["Foo"] = ...
Client B: AddMember("Bar") → delta.AddedSymbols["Bar"] = ...

Risk: Lost updates if not synchronized
```

**Solution: Lock-Free ConcurrentDictionary**
```csharp
public class BranchDelta {
    // Thread-safe collections
    private readonly ConcurrentDictionary<string, SymbolIndexEntry> _addedSymbols;
    private readonly ConcurrentDictionary<string, SymbolIndexEntry> _modifiedSymbols;
    private readonly ConcurrentBag<string> _deletedSymbolIds;

    // Atomic add/update
    public void AddOrUpdate(SymbolIndexEntry symbol) {
        _addedSymbols.AddOrUpdate(
            symbol.SymbolId,
            symbol,
            (key, old) => symbol); // Replace with newer
    }
}
```

**Persistence:** Async write-behind with batching
```csharp
private async Task PersistDeltaAsync(BranchDelta delta) {
    // Batch multiple updates within 1 second
    await Task.Delay(1000);

    // Write all changes to SQLite in one transaction
    using var transaction = _sqliteConn.BeginTransaction();

    foreach (var added in delta.AddedSymbols.Values) {
        await InsertSymbolAsync(added);
    }

    transaction.Commit();
}
```

### Challenge 3: Delta Size Growth

**Problem:** Long-lived feature branches accumulate many changes

```
feature/auth (3 months old):
  - 500 added symbols
  - 200 modified symbols
  - 100 deleted symbols

Delta size: ~100 MB in memory
Query overhead: O(800) merge operations
```

**Solution: Delta Compaction**
```csharp
public async Task CompactDeltaAsync(string branch) {
    var delta = await GetOrLoadDeltaAsync(branch);

    if (delta.SymbolCount > 1000) {
        // Option 1: Promote to dedicated base
        var branchBase = await BuildBaseFromBranch(branch);
        _branchBases[branch] = branchBase;

        // Clear delta (now everything is in branchBase)
        delta.Clear();

        // Option 2: Periodic squash with main
        await RebaseDeltaAsync(branch);
    }
}
```

### Challenge 4: Branch Deletion Cleanup

**Problem:** Merged branches leave orphaned delta files

```
feature/auth merged to main → branches/feature_auth.db orphaned
```

**Solution: Automatic Cleanup**
```csharp
public async Task DeleteBranchAsync(string branch) {
    // 1. Remove from in-memory cache
    _deltaCache.TryRemove(branch, out _);

    // 2. Delete SQLite file
    var deltaPath = $"cache/branches/{branch}.db";
    if (File.Exists(deltaPath)) {
        File.Delete(deltaPath);
    }

    // 3. Log cleanup
    _logger.LogInformation("Deleted delta for branch: {Branch}", branch);
}

// Background task: Clean orphaned deltas
private async Task CleanupOrphanedDeltasAsync() {
    var gitBranches = await GetGitBranchesAsync();
    var deltaFiles = Directory.GetFiles("cache/branches", "*.db");

    foreach (var deltaFile in deltaFiles) {
        var branchName = Path.GetFileNameWithoutExtension(deltaFile);

        if (!gitBranches.Contains(branchName)) {
            await DeleteBranchAsync(branchName);
        }
    }
}
```

---

## 📋 Implementation Plan

### Phase 1: Core Data Structures (2-3 days)
1. ✅ Implement BranchDelta class (in-memory)
2. ✅ Implement HybridLayeredIndex skeleton
3. ✅ Add SQLite schema for deltas
4. ✅ Implement delta persistence (SaveDeltaAsync, LoadDeltaAsync)

### Phase 2: Git Integration (2-3 days)
1. ✅ Implement ComputeDeltaFromGitDiff()
2. ✅ Implement GetGitCommitShaAsync()
3. ✅ Implement CreateBranchAsync()
4. ✅ Add git diff parsing (libgit2sharp)

### Phase 3: Query Layer (2-3 days)
1. ✅ Implement FindAsync() with base + delta merge
2. ✅ Implement delta.Apply() logic
3. ✅ Add LRU cache for deltas (MemoryCache)
4. ✅ Add metrics logging (cache hit rate, query time)

### Phase 4: Update Layer (2-3 days)
1. ✅ Implement UpdateDeltaAsync()
2. ✅ Hook into CodeModificationService events
3. ✅ Add concurrent update protection (ConcurrentDictionary)
4. ✅ Implement async write-behind persistence

### Phase 5: Branch Operations (2-3 days)
1. ✅ Implement SwitchBranchAsync()
2. ✅ Implement RebaseDeltaAsync()
3. ✅ Implement DeleteBranchAsync()
4. ✅ Add background cleanup task

### Phase 6: Testing & Optimization (3-4 days)
1. ✅ Benchmark: Query performance (base + delta)
2. ✅ Benchmark: Memory usage (10+ branches)
3. ✅ Benchmark: Concurrent updates (multiple clients)
4. ✅ Test: Branch switch, rebase, merge scenarios
5. ✅ Test: Delta compaction under load

### Phase 7: Integration & Documentation (2-3 days)
1. ✅ Integrate with RemoteServer
2. ✅ Add MCP tools: CreateBranch, SwitchBranch, DeleteBranch
3. ✅ Update ULTRA-SHARPED.md
4. ✅ Create user guide for branch-aware workflows

**Total: 15-21 days**

---

## 🎯 Expected Benefits

### Memory Savings
```
Before (Isolated Solutions):
  3 clients × 600 MB = 1.8 GB RAM ❌

After (Hybrid Layered):
  Base: 600 MB
  3 deltas × 10 MB = 30 MB
  Total: 630 MB ✅

Savings: 65% memory reduction
```

### Performance Improvements
```
Before:
  Branch switch: 33 seconds (full rebuild) ❌
  Query in feature branch: 5-10s (Roslyn fallback) ❌

After:
  Branch switch: 10-50ms (load delta from SQLite) ✅
  Query in feature branch: 1-5ms (cached delta merge) ✅

Speedup: 660-3300x for branch switch
         1000-10000x for queries
```

### User Experience
```
Before:
  User: "Switch to feature/auth"
  AI: ⏳ Rebuilding index... (33 seconds)
  User: "Search for Login"
  AI: ⏳ Searching... (5-10 seconds)

After:
  User: "Switch to feature/auth"
  AI: ✅ Switched (50ms)
  User: "Search for Login"
  AI: ✅ Found 18 results (5ms)
```

---

## ✅ Success Criteria

- [ ] 10+ concurrent clients in different branches
- [ ] <100 MB memory per branch delta
- [ ] <10ms query latency (cached delta)
- [ ] <100ms query latency (cold delta load)
- [ ] <3s branch creation from git diff
- [ ] No stale symbols after branch operations
- [ ] Graceful handling of main branch updates
- [ ] Automatic cleanup of orphaned deltas

---

## 🚀 Ready for Implementation?

**Recommended approach:** Hybrid Layered Index (Option 4)

**Next steps:**
1. Review this design document
2. Approve architecture
3. Start Phase 1 (Core Data Structures)
4. Iterate based on Phase 1 results

**Key decision points:**
- ✅ Use hybrid approach (in-memory base + SQLite deltas)
- ✅ LRU cache for deltas (max 20 branches)
- ✅ Automatic rebase detection (via base_commit_sha)
- ✅ Background cleanup for orphaned deltas
- ✅ ConcurrentDictionary for thread safety

Начинаем реализацию? 🎯

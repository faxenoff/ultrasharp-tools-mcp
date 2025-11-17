# Incremental Indexing: Architecture Analysis & Implementation Plan

## 🏗️ Architecture Analysis

### Process Model

#### Droid (Stdio Transport)
```
Claude Code Instance
    ↓
Separate Process (stserver.exe)
    ↓
Single ISolutionManager (DI Singleton)
    ↓
Single Solution loaded per process
```

**Характеристики:**
- ✅ **One process per Claude Code window**
- ✅ **One solution per process**
- ✅ **Process lifetime = Claude Code session lifetime**
- ✅ **Isolated state** (разные Claude окна = разные процессы)

**Команда запуска:**
```bash
stserver.exe \
  --log-directory ./logs \
  --auto-reload true \
  --reload-debounce-ms 2000 \
  --symbol-cache true \
  --load-solution ./MyApp.sln
```

#### Overlord (HTTP SSE Transport)
```
Multiple Claude Clients
    ↓
Single HTTP Server Process
    ↓
Single ISolutionManager (DI Singleton)
    ↓
Single Solution loaded (shared between all clients)
```

**Характеристики:**
- ✅ **One long-running server process**
- ❌ **One solution for ALL clients** (limitation!)
- ⚠️ **Concurrent access** to same solution
- ⚠️ **No client isolation**

**Команда запуска:**
```bash
sseserver.exe \
  --port 3001 \
  --load-solution ./MyApp.sln \
  --symbol-cache true
```

### Current State Management

#### ISolutionManager (Singleton)
```csharp
public class SolutionManager : ISolutionManager {
    // State:
    private MSBuildWorkspace? _workspace;
    private Solution? _currentSolution;       // ONE solution only
    private string? _currentSolutionPath;

    // Indexing:
    private readonly FastSymbolIndex _symbolIndex;  // ONE index

    // Caching:
    private readonly MemoryCache _semanticModelCache;
    private readonly Dictionary<DocumentId, string> _documentIdToFilePath;
    private readonly Dictionary<string, DocumentId> _filePathToDocumentId;

    // File Watching:
    private FileSystemWatcher? _fileWatcher;          // For *.cs changes
    private FileSystemWatcher? _projectFileWatcher;   // For .csproj/.sln changes
}
```

### Existing FileSystemWatcher Infrastructure

#### 1. Cache Invalidation Watcher (`_fileWatcher`)
**Purpose:** Invalidate SemanticModel cache when .cs files change on disk

```csharp
// Configuration:
Filter = "*.cs"
IncludeSubdirectories = true
NotifyFilter = LastWrite | FileName

// Events:
Changed → InvalidateSemanticModel(documentId)
Deleted → InvalidateSemanticModel(documentId)
Renamed → Update _filePathToDocumentId mapping
```

**What it does:**
- ✅ Detects external file changes (e.g., from VS, Rider, external tools)
- ✅ Invalidates MemoryCache for SemanticModel
- ❌ Does NOT update FastSymbolIndex
- ❌ Does NOT reload Solution

#### 2. Auto-Reload Watcher (`_projectFileWatcher`)
**Purpose:** Trigger full solution reload when project files change

```csharp
// Configuration:
Filter = "*.*"  // Filters in event handler
IncludeSubdirectories = true
WatchedExtensions = [".csproj", ".sln", ".props", ".targets"]
DebounceDelayMs = 2000  // Default

// Events:
Changed → Schedule ReloadSolutionFromDiskAsync() with debounce
```

**What it does:**
- ✅ Detects project structure changes
- ✅ Triggers full solution reload (LoadSolutionAsync)
- ✅ Debouncing prevents multiple reloads
- ✅ Full FastSymbolIndex rebuild (~33 seconds)
- ⚠️ Enabled via `--auto-reload true` flag (disabled by default)

### Current Behavior After Code Modifications

#### Scenario 1: Internal Modification (AddMember, OverwriteMember)
```csharp
// User calls MCP tool:
add_member(typeName: "MyClass", memberCode: "public void Foo() {}")
  ↓
CodeModificationService.ApplyChangesAsync()
  ↓
workspace.TryApplyChanges(newSolution)  // Roslyn applies changes
  ↓
_currentSolution = workspace.CurrentSolution  // Solution updated
  ↓
Save files to disk
  ↓
FileSystemWatcher detects *.cs change
  ↓
InvalidateSemanticModel(documentId)  // Cache invalidated
  ↓
❌ FastSymbolIndex NOT updated (still has old symbols!)
```

**Result:**
- ✅ Roslyn Solution is up-to-date
- ✅ SemanticModel cache invalidated
- ❌ FastSymbolIndex is STALE
- ❌ SearchDefinitions("Foo") will NOT find new method
- ⚠️ Fallback to slow Roslyn API (5-15 seconds)

#### Scenario 2: External Modification (VS, Rider, other tools)
```csharp
// User edits file in external editor and saves
  ↓
FileSystemWatcher.Changed event fires
  ↓
InvalidateSemanticModel(documentId)  // Cache invalidated
  ↓
❌ Roslyn Solution NOT reloaded (stale AST!)
  ↓
❌ FastSymbolIndex NOT updated
```

**Result:**
- ⚠️ Roslyn Solution is OUT OF SYNC with disk
- ⚠️ SemanticModel will be reloaded on next access (from disk)
- ❌ FastSymbolIndex is STALE

#### Scenario 3: Project File Change (if --auto-reload enabled)
```csharp
// .csproj file changes (add/remove files, NuGet packages)
  ↓
_projectFileWatcher.Changed event fires
  ↓
Debounce timer starts (2000ms)
  ↓
TriggerAutoReloadAsync()
  ↓
ReloadSolutionFromDiskAsync()
  ↓
LoadSolutionAsync(_currentSolutionPath)  // FULL reload
  ↓
BuildFromSolutionAsync()  // FULL index rebuild (~33 seconds)
```

**Result:**
- ✅ Roslyn Solution fully reloaded from disk
- ✅ FastSymbolIndex fully rebuilt
- ⚠️ EXPENSIVE operation (33 seconds for 355K symbols)
- ⚠️ Overkill for small changes

---

## 🎯 Problem Statement

### Current Limitations

| Issue | Impact | Frequency |
|-------|--------|-----------|
| **Stale Index after modifications** | SearchDefinitions fails, fallback to slow Roslyn | Every AddMember/OverwriteMember call |
| **No incremental updates** | Full rebuild required (33s) | Every auto-reload trigger |
| **External changes not synced** | Roslyn Solution out of sync with disk | When using external editors |
| **Overlord shared state** | Multiple clients conflict | Multi-user scenarios |

### User Impact

**Workflow:**
```
1. User: "Add method Foo to MyClass"
   AI: ✅ Adds method successfully (200ms)

2. User: "Find all references to Foo"
   AI: ❌ "Method not found" (FastSymbolIndex is stale)
   AI: ⚠️ Falls back to Roslyn FindReferencesAsync (5-10 seconds)

3. User: "Reload solution"  # Manual workaround
   AI: ⏳ Full rebuild (33 seconds)

4. User: "Find all references to Foo"
   AI: ✅ Found (instant from index)
```

**Expected workflow:**
```
1. User: "Add method Foo to MyClass"
   AI: ✅ Adds method + incremental index update (300ms)

2. User: "Find all references to Foo"
   AI: ✅ Found instantly (index already updated)
```

---

## 🚀 Solution: Incremental Indexing

### Design Principles

1. **Lazy Synchronization**
   - Update index only when Roslyn Solution changes
   - Don't rebuild entire index for 1 file change

2. **Event-Driven Updates**
   - Subscribe to workspace.WorkspaceChanged
   - Detect DocumentChanged, DocumentAdded, DocumentRemoved

3. **Granular Rebuilds**
   - Per-document symbol extraction
   - Rebuild lookup structures (Bloom, FrozenDict) only when needed

4. **Batch Optimizations**
   - Group multiple changes together
   - Debounce rapid successive updates

5. **Graceful Degradation**
   - Fallback to full rebuild if incremental fails
   - Fallback to Roslyn API if index temporarily unavailable

### Architecture Overview

```
Roslyn Workspace
    ↓ workspace.WorkspaceChanged event
SolutionManager (event handler)
    ↓ Detect change type
FastSymbolIndex (incremental methods)
    ↓ Extract/Remove/Update symbols
Bloom Filter + FrozenDictionary
    ↓ Rebuild lookup structures
Index Ready (typically 100-500ms for 1 file)
```

### Components to Modify

#### 1. SolutionManager
```csharp
// Add event subscription:
private void SubscribeToWorkspaceChanges() {
    _workspace.WorkspaceChanged += OnWorkspaceChanged;
}

private async void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs e) {
    switch (e.Kind) {
        case WorkspaceChangeKind.DocumentChanged:
            await _symbolIndex.UpdateDocumentAsync(e.NewSolution, e.DocumentId, cancellationToken);
            break;
        case WorkspaceChangeKind.DocumentAdded:
            await _symbolIndex.AddDocumentAsync(e.NewSolution, e.DocumentId, cancellationToken);
            break;
        case WorkspaceChangeKind.DocumentRemoved:
            await _symbolIndex.RemoveDocumentAsync(e.DocumentId, cancellationToken);
            break;
        case WorkspaceChangeKind.ProjectAdded:
        case WorkspaceChangeKind.ProjectRemoved:
        case WorkspaceChangeKind.ProjectChanged:
            // Fallback to full rebuild for project-level changes
            await _symbolIndex.BuildFromSolutionAsync(e.NewSolution, cancellationToken);
            break;
    }
}
```

#### 2. FastSymbolIndex (new incremental methods)
```csharp
public async Task UpdateDocumentAsync(Solution solution, DocumentId documentId, CancellationToken ct) {
    var document = solution.GetDocument(documentId);
    if (document == null) return;

    // 1. Extract new symbols from document
    var semanticModel = await document.GetSemanticModelAsync(ct);
    var newSymbols = ExtractSymbolsFromSemanticModel(semanticModel);

    // 2. Find and remove old symbols from this document
    var oldSymbols = _allSymbols.Where(e => e.DocumentId == documentId).ToList();
    foreach (var old in oldSymbols) {
        RemoveSymbolFromLookups(old);
    }

    // 3. Add new symbols to lookups
    foreach (var newSym in newSymbols) {
        AddSymbolToLookups(newSym);
    }

    // 4. Rebuild immutable structures (Bloom, FrozenDict)
    RebuildLookupStructures();
}

public async Task AddDocumentAsync(Solution solution, DocumentId documentId, CancellationToken ct) {
    // Similar to UpdateDocumentAsync, but no removal step
}

public async Task RemoveDocumentAsync(DocumentId documentId, CancellationToken ct) {
    var oldSymbols = _allSymbols.Where(e => e.DocumentId == documentId).ToList();
    foreach (var old in oldSymbols) {
        RemoveSymbolFromLookups(old);
    }
    RebuildLookupStructures();
}

private void RebuildLookupStructures() {
    // Rebuild FrozenDictionary
    _bySimpleName = BuildSimpleNameIndex(_allSymbols);
    _byNamespace = BuildNamespaceIndex(_allSymbols);

    // Rebuild Bloom filter
    _nameBloomFilter = new BloomFilter(expectedItems: _allSymbols.Length);
    foreach (var entry in _allSymbols) {
        _nameBloomFilter.Add(entry.SimpleName);
    }
}
```

### Challenges & Solutions

#### Challenge 1: Bloom Filter Immutability
**Problem:** Bloom filters don't support deletion (probabilistic structure)

**Solutions:**
- **Option A (Simple):** Rebuild Bloom filter on every update (30-200ms)
- **Option B (Complex):** Use Counting Bloom Filter (supports deletion, but 4x memory overhead)
- **Option C (Hybrid):** Track deleted count, rebuild when >10% deleted

**Recommendation:** Option A (Simple) - 30-200ms rebuild is acceptable

#### Challenge 2: FrozenDictionary Immutability
**Problem:** FrozenDictionary is immutable, can't add/remove entries

**Solutions:**
- **Option A (Simple):** Rebuild FrozenDictionary from updated _allSymbols (20-100ms)
- **Option B (Hybrid):** Keep mutable Dictionary for pending changes, merge on threshold
- **Option C (Complex):** Implement copy-on-write with structural sharing

**Recommendation:** Option A (Simple) - 20-100ms rebuild is acceptable

#### Challenge 3: Memory Management
**Problem:** Deleted symbols remain in _allSymbols array until compaction

**Solutions:**
- **Option A (Tombstones):** Mark deleted with flag, compact when >10% deleted
- **Option B (Immediate):** Remove from array immediately (expensive: Array.Copy)
- **Option C (Deferred):** Collect in "deleted" list, rebuild array on threshold

**Recommendation:** Option A (Tombstones) - lazy cleanup

#### Challenge 4: Concurrent Updates
**Problem:** Multiple rapid changes can trigger overlapping updates

**Solutions:**
- **Option A (Lock):** Use SemaphoreSlim to serialize updates
- **Option B (Debounce):** Batch changes within 100-500ms window
- **Option C (Version):** Track update version, discard stale updates

**Recommendation:** Option A (Lock) + Option B (Debounce) combined

#### Challenge 5: Cache Invalidation
**Problem:** SQLite SymbolCache becomes invalid after incremental updates

**Solutions:**
- **Option A (Disable):** Don't save incremental state to cache
- **Option B (Incremental):** Save incremental updates to SQLite (complex)
- **Option C (Periodic):** Save full snapshot every N updates

**Recommendation:** Option C (Periodic) - save every 10 updates or on shutdown

---

## 📋 Implementation Plan

### Phase 1: Foundation (1-2 days)
**Goal:** Add workspace event subscription and logging

**Tasks:**
1. ✅ Subscribe to `workspace.WorkspaceChanged` in SolutionManager
2. ✅ Add logging for all workspace change events
3. ✅ Test event firing for AddMember/OverwriteMember
4. ✅ Test event firing for external file changes

**Files to modify:**
- `SolutionManager.cs` - Add SubscribeToWorkspaceChanges()
- Test with Droid + load_solution + AddMember

**Success criteria:**
- Log entries show WorkspaceChangeKind.DocumentChanged after AddMember
- No exceptions, no performance degradation

### Phase 2: Incremental Methods (2-3 days)
**Goal:** Implement UpdateDocumentAsync, AddDocumentAsync, RemoveDocumentAsync

**Tasks:**
1. ✅ Add SymbolIndexEntry.DocumentId field (required for tracking)
2. ✅ Implement UpdateDocumentAsync() in FastSymbolIndex
3. ✅ Implement AddDocumentAsync() in FastSymbolIndex
4. ✅ Implement RemoveDocumentAsync() in FastSymbolIndex
5. ✅ Implement RebuildLookupStructures() (Bloom + FrozenDict rebuild)
6. ✅ Add SemaphoreSlim for concurrent update protection

**Files to modify:**
- `FastSymbolIndex.cs` - Add incremental methods
- `SymbolIndexEntry.cs` - Add DocumentId property
- `SolutionManager.cs` - Call incremental methods from event handler

**Success criteria:**
- AddMember + instant SearchDefinitions finds new symbol
- UpdateMember + instant SearchDefinitions reflects changes
- Performance: 100-500ms per single file update

### Phase 3: Batch Optimization (1-2 days)
**Goal:** Optimize for multiple rapid changes

**Tasks:**
1. ✅ Implement BeginBatchUpdate() / EndBatchUpdate() in FastSymbolIndex
2. ✅ Add debouncing for workspace events (100-500ms window)
3. ✅ Batch multiple document changes into single rebuild
4. ✅ Add metrics logging (symbols added/removed, rebuild time)

**Files to modify:**
- `FastSymbolIndex.cs` - Add batch mode
- `SolutionManager.cs` - Add debounce timer for events

**Success criteria:**
- 10 files changed → single rebuild (1-3 seconds)
- Metrics show correct symbol counts
- No duplicate rebuilds

### Phase 4: Fallback Strategy (1 day)
**Goal:** Handle edge cases and errors gracefully

**Tasks:**
1. ✅ Add fallback to full rebuild if incremental fails
2. ✅ Add threshold: if >100 files changed → full rebuild
3. ✅ Add error handling with detailed logging
4. ✅ Add health check: verify index integrity after updates

**Files to modify:**
- `FastSymbolIndex.cs` - Add error handling
- `SolutionManager.cs` - Add fallback logic

**Success criteria:**
- Incremental fails → full rebuild triggered automatically
- >100 files → full rebuild (no incremental attempts)
- Detailed error logs for debugging

### Phase 5: Cache Strategy (1-2 days)
**Goal:** Handle persistent cache with incremental updates

**Tasks:**
1. ✅ Disable cache save during incremental updates
2. ✅ Add periodic full cache save (every 10 updates)
3. ✅ Add full cache save on graceful shutdown
4. ✅ Add cache metadata: version, lastModified, isIncremental flag

**Files to modify:**
- `SymbolCacheManager.cs` - Add incremental metadata
- `FastSymbolIndex.cs` - Track update count, trigger periodic saves

**Success criteria:**
- Cache saved after 10 incremental updates
- Cache saved on server shutdown
- Cache loading respects incremental metadata

### Phase 6: Testing & Benchmarking (2-3 days)
**Goal:** Validate performance and correctness

**Tasks:**
1. ✅ Benchmark: 1 file → UpdateDocument → search_definitions
2. ✅ Benchmark: 10 files → Batch update → SearchDefinitions
3. ✅ Benchmark: 100 files → Fallback to full rebuild
4. ✅ Test: add_member → instant search
5. ✅ Test: External edit → auto-reload → index updated
6. ✅ Test: Project file change → full rebuild
7. ✅ Test: Concurrent updates (multiple AddMember calls)
8. ✅ Test: Overlord with multiple clients

**Success criteria:**
- 1 file: 100-500ms (vs 33s full rebuild) ✅ 66-330x faster
- 10 files: 1-3s (vs 33s) ✅ 11-33x faster
- 100 files: 10-15s (vs 33s) ✅ 2-3x faster
- No stale index after modifications
- No crashes under concurrent load

### Phase 7: Documentation (1 day)
**Goal:** Update all documentation

**Tasks:**
1. ✅ Update ULTRA-SHARPED.md with incremental indexing section
2. ✅ Add Dev.Docs/Features/IncrementalIndexing/ with full docs
3. ✅ Update Run.Docs with --auto-reload flag explanation
4. ✅ Add troubleshooting guide for stale index issues

---

## 📊 Expected Performance Impact

### Before (Current)
```
Scenario: Add 1 method to MyClass

AddMember                     200ms   ✅
FastSymbolIndex               STALE   ❌search_definitionss("NewMethod") 5-10s  ⚠️ (Roslyn fallback)
Manual ReloadSolution         33s     ⚠️
SearchDefinitions("NewMethod") <1s    ✅ (from index)

Total: 38-43 seconds
```

### After (With Incremental)
```
Scenario: Add 1 method to MyClass

AddMember                     200ms   ✅
Incremental index update      100-500ms ✅
SearchDefinitions("NewMethod") <1s    ✅ (from index)

Total: 0.3-0.7 seconds

Speedup: 54-143x faster
```

### Benchmark Table

| Scenario | Before | After (Incremental) | Speedup |
|----------|--------|---------------------|---------|
| 1 file modified | 33s (full rebuild) | 0.1-0.5s | **66-330x** |
| 5 files modified | 33s | 0.5-2s | **16-66x** |
| 10 files modified | 33s | 1-3s | **11-33x** |
| 50 files modified | 33s | 5-10s | **3-6x** |
| 100+ files modified | 33s | 33s (fallback) | 1x |
| External edit (with --auto-reload) | 33s | 0.1-0.5s | **66-330x** |

---

## 🎯 Recommendations

### For Droid (Stdio) - Recommended Flags
```bash
stserver.exe \
  --load-solution ./MyApp.sln \
  --auto-reload true \              # Enable auto-reload for external changes
  --reload-debounce-ms 500 \        # Fast debounce for responsive updates
  --symbol-cache true \             # Persistent cache for restart speed
  --log-level Information
```

**Rationale:**
- Droid is single-user, isolated process
- Auto-reload is safe and beneficial
- Incremental indexing works perfectly
- 500ms debounce balances responsiveness vs rebuild frequency

### For Overlord (HTTP) - Recommended Flags
```bash
sseserver.exe \
  --port 3001 \
  --load-solution ./MyApp.sln \
  --symbol-cache true \
  --log-level Information
```

**Rationale:**
- Overlord is multi-client, shared state
- Auto-reload could cause conflicts (Client A modifies, Client B index rebuilds)
- Incremental indexing still beneficial for internal modifications
- Consider disabling auto-reload for stability

### Development vs Production

**Development (frequent code changes):**
```bash
--auto-reload true
--reload-debounce-ms 500
--symbol-cache false  # Avoid stale cache during rapid iteration
```

**Production (stable codebase):**
```bash
--auto-reload false
--symbol-cache true
--symbol-cache-directory /persistent/storage/cache
```

---

## 🔍 Edge Cases & Considerations

### 1. Race Conditions
**Problem:** User calls AddMember while auto-reload is in progress

**Solution:**
- Lock _symbolIndex updates with SemaphoreSlim
- Queue pending updates during full rebuild
- Process queued updates after rebuild completes

### 2. Out-of-Order Events
**Problem:** workspace.WorkspaceChanged fires before TryApplyChanges completes

**Solution:**
- Use e.NewSolution (not _currentSolution) in event handler
- Verify DocumentId exists in NewSolution before processing

### 3. Large Batch Updates
**Problem:** Renaming a namespace affects 1000+ files

**Solution:**
- Detect large batch (>100 files)
- Fallback to full rebuild
- Log decision for transparency

### 4. External Tool Conflicts
**Problem:** Visual Studio and UltrasharpTools both modify same file

**Solution:**
- FileSystemWatcher detects external changes
- Trigger incremental update (not full reload)
- Log source of change for debugging

### 5. Memory Leaks
**Problem:** Deleted symbols accumulate in tombstoned array

**Solution:**
- Track deleted percentage
- Compact array when >10% deleted
- Log compaction events

---

## ✅ Success Metrics

### Performance Targets
- [ ] 1 file update: <500ms (current: 33s)
- [ ] 10 file batch: <3s (current: 33s)
- [ search_definitionsns after AddMember: <1s (current: 5-10s fallback)
- [ ] Memory overhead: <10% increase (tombstones + pending queue)

### Correctness Targets
- [ ] No stale symbols after modifications
- [ ] No duplicate symbols in index
- [ ] No crashes under concurrent load
- [ ] Fallback to full rebuild on errors

### User Experience Targets
- [ ] No manual ReloadSolution required
- [ ] Instant search after code modifications
- [ ] Transparent background updates (< 1s blocking time)
- [ ] Clear logging for debugging

---

## 📅 Timeline Estimate

| Phase | Duration | Dependency |
|-------|----------|------------|
| Phase 1: Foundation | 1-2 days | None |
| Phase 2: Incremental Methods | 2-3 days | Phase 1 |
| Phase 3: Batch Optimization | 1-2 days | Phase 2 |
| Phase 4: Fallback Strategy | 1 day | Phase 3 |
| Phase 5: Cache Strategy | 1-2 days | Phase 4 |
| Phase 6: Testing & Benchmarking | 2-3 days | Phase 5 |
| Phase 7: Documentation | 1 day | Phase 6 |

**Total: 9-14 days** (with buffer for unexpected issues)

**Critical Path:** Phase 1 → Phase 2 → Phase 6
**Minimum Viable:** Phase 1 + Phase 2 = 3-5 days (basic functionality)

---

## 🚦 Next Steps

1. **Review this document** with team
2. **Approve architecture** and plan
3. **Start Phase 1** (Foundation) - low risk, high visibility
4. **Iterate** based on feedback from Phase 1 results
5. **Proceed to Phase 2** once Phase 1 validates approach

**Ready to start implementation?** 🚀

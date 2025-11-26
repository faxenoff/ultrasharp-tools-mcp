# UltrasharpTools - System Tools

**Server capabilities, runtime information, and snapshot management.**

---

## 📋 Quick Reference

| Tool | Purpose | Destructive |
|------|---------|-------------|
| **get_capabilities** | Server capabilities and semantic mode status | ❌ No |
| **create_snapshot** | Create restore point before changes | ❌ No |
| **list_snapshots** | View available snapshots | ❌ No |
| **rollback_snapshot** | Restore code to previous snapshot | ⚠️ Yes |
| **cleanup_snapshots** | Delete old snapshots to save space | ⚠️ Yes |

---

## get_capabilities

**Get server capabilities and semantic mode status**

Returns comprehensive information about server features and semantic mode availability.

```typescript
get_capabilities() → ServerCapabilities
```

#### Response Structure

```json
{
  "serverInfo": {
    "name": "UltrasharpTools MCP Droid",
    "version": "3.0.0",
    "protocol": "MCP 1.0"
  },
  "capabilities": {
    "semanticMode": {
      "enabled": true,
      "source": "Both",           // None | Local | Overlord | Both
      "modelName": "nomic-embed-text",
      "vectorDimension": 768,
      "localEmbeddingUrl": "http://localhost:11434",
      "overlordUrl": "http://localhost:8080",
      "dynamic": true,
      "cacheValiditySeconds": 60,
      "description": "Semantic code search and similarity analysis using vector embeddings"
    },
    "features": {
      "gitIntegration": true,
      "editorConfigSupport": true,
      "universalSemanticMode": true,
      "hybridMode": true,
      "tracing": true,
      "codeModification": true,
      "projectAnalysis": true,
      "symbolCaching": true
    }
  },
  "timestamp": "2025-11-18T22:30:00Z"
}
```

#### Semantic Mode Sources

- **None**: Semantic mode disabled or unavailable
- **Local**: Local embedding service (Ollama/TEI) available
- **Overlord**: Remote Overlord server available
- **Both**: Both local and remote sources available (hybrid mode)

#### Use Cases

**1. Detect semantic capabilities at startup**
```
get_capabilities()
→ Check if semanticMode.enabled = true
→ Optimize query strategy based on availability
```

**2. Choose semantic search strategy**
```
get_capabilities()
→ If source = "Both": prefer local for speed
→ If source = "Overlord": use remote search
→ If source = "None": fallback to non-semantic tools
```

**3. Display server status to user**
```
get_capabilities()
→ Show: "Semantic search: ✓ Available (Local + Overlord)"
→ Show: "Model: nomic-embed-text (768 dimensions)"
→ Show: "Git integration: ✓ Enabled"
```

**4. Runtime health check**
```
// Check if semantic mode recovered after failure
get_capabilities()
→ semanticMode.enabled changed from false → true
→ Resume using semantic-enhanced tools
```

#### Features Explained

**gitIntegration** - Automatic git branching and commits for all modifications
**editorConfigSupport** - Uses .editorconfig for code formatting
**universalSemanticMode** - All analysis tools can be enriched with semantic data
**hybridMode** - Can route tools between local and remote execution
**tracing** - Advanced execution tracing capabilities available
**codeModification** - Code modification and refactoring tools enabled
**projectAnalysis** - Full project analysis and navigation capabilities
**symbolCaching** - Symbol cache enabled for faster initialization

#### When to Call

**Recommended:**
- ✅ At startup - check available capabilities
- ✅ Before using semantic search - verify availability
- ✅ After network issues - check if Overlord recovered
- ✅ When showing status to user

**Not needed:**
- ❌ Before every tool call (capabilities cached for 60s)
- ❌ For non-semantic operations (git, analysis, modification)

#### Performance

- **Cache**: 60 seconds validity (configurable)
- **Response time**: < 100ms (cached), < 3s (fresh check)
- **Network**: Checks local embedding + Overlord in parallel

#### Example Workflow

```typescript
// 1. Check capabilities at startup
const caps = await get_capabilities();

// 2. Decide strategy based on semantic mode
if (caps.capabilities.semanticMode.enabled) {
  console.log(`Semantic search available via ${caps.capabilities.semanticMode.source}`);

  // Use semantic-enhanced tools
  const result = await view_definition("MyClass");
  // result.semantic contains similar symbols, related implementations

} else {
  console.log("Semantic mode unavailable - using standard analysis");

  // Use standard tools without semantic enrichment
  const result = await view_definition("MyClass");
  // result.semantic will be empty or null
}

// 3. Show features to user
console.log("Available features:");
for (const [feature, enabled] of Object.entries(caps.capabilities.features)) {
  console.log(`  ${feature}: ${enabled ? '✓' : '✗'}`);
}
```

#### Notes

- **Dynamic capability**: Semantic mode can change at runtime (service restart, network issues)
- **Cache invalidation**: Cached for 60s to avoid overhead on every request
- **Graceful degradation**: Tools work without semantic mode, just without enrichment
- **MCP Initialize**: Same info available via MCP Initialize Response (static snapshot at startup)

#### Related

- **MCP Initialize Response**: Static capabilities at connection time
- **Semantic tools**: [ULTRA_SHARP_SEMANTIC.md](./ULTRA_SHARP_SEMANTIC.md)
- **Hybrid mode**: [../../Dev.Docs/Architecture/HYBRID_MODE.md](../../Dev.Docs/Architecture/HYBRID_MODE.md)

---

## create_snapshot

**Create restore point** — creates a snapshot of current code state for safe experimentation and rollback. Snapshots are stored in `.ultrasharp/snapshots/` directory.

### Usage

```javascript
// Create snapshot with description
create_snapshot(
    description: "before refactoring UserService"
)

// Create snapshot of specific files
create_snapshot(
    description: "backup authentication files",
    files: ["src/Auth/AuthService.cs", "src/Auth/TokenProvider.cs"]
)
```

### Parameters

- **description** (required): Human-readable description (e.g., "before refactoring UserService")
- **files** (optional): Array of specific files to snapshot. If null, creates empty snapshot marker.

### What It Returns

```json
{
    "snapshotId": "backup-20251126-143052",
    "description": "before refactoring UserService",
    "backend": "file",
    "filesCount": 5,
    "createdAt": "2025-11-26T14:30:52Z",
    "message": "Snapshot created: backup-20251126-143052. Use rollback_snapshot to restore this state."
}
```

### When to Use

✅ **Before risky changes:**
- Before major refactoring
- Before experimental modifications
- Before applying auto-fixes

✅ **As safety net:**
- Before using destructive tools
- When testing new approaches
- When making changes you might want to undo

### Related Tools

- ➡️ [**rollback_snapshot**](#rollback_snapshot) — restore to this snapshot
- ➡️ [**list_snapshots**](#list_snapshots) — view all snapshots
- ➡️ [**undo**](./ULTRA_SHARP_MODIFICATION.md#undo) — undo last single change

---

## list_snapshots

**View available snapshots** — lists all snapshots with details including ID, description, creation time, and file count.

### Usage

```javascript
// List recent snapshots (default: 20)
list_snapshots()

// List more snapshots
list_snapshots(
    limit: 50
)
```

### Parameters

- **limit** (default: 20): Maximum number of snapshots to return (1-100)

### What It Returns

```json
{
    "totalCount": 5,
    "snapshots": [
        {
            "snapshotId": "backup-20251126-143052",
            "description": "before refactoring UserService",
            "createdAt": "2025-11-26 14:30:52",
            "fileCount": 5,
            "backend": "file",
            "sizeBytes": 15234,
            "age": "2 hours ago"
        },
        {
            "snapshotId": "backup-20251126-120000",
            "description": "before auth changes",
            "createdAt": "2025-11-26 12:00:00",
            "fileCount": 3,
            "backend": "file",
            "sizeBytes": 8456,
            "age": "5 hours ago"
        }
    ]
}
```

### When to Use

✅ **Before rollback:**
- Find the right snapshot to restore
- Check snapshot descriptions
- Verify file counts

✅ **For tracking:**
- See history of major changes
- Find specific restore points
- Monitor snapshot disk usage

---

## rollback_snapshot

**Restore to previous state** — rolls back code to a specific snapshot, overwriting current changes.

⚠️ **DESTRUCTIVE**: Current changes will be lost. Consider creating a new snapshot first!

### Usage

```javascript
// Rollback to specific snapshot
rollback_snapshot(
    snapshotId: "backup-20251126-143052"
)
```

### Parameters

- **snapshotId** (required): Snapshot ID from `list_snapshots` output

### What It Returns

```json
{
    "success": true,
    "snapshotId": "backup-20251126-143052",
    "message": "Successfully rolled back to snapshot: backup-20251126-143052"
}
```

### When to Use

✅ **To recover from mistakes:**
- After failed refactoring
- After breaking changes
- After experimental changes didn't work

### Best Practices

1. **Always create snapshot of current state first:**
   ```javascript
   // ✅ Safe - can undo the rollback
   create_snapshot(description: "current state before rollback")
   rollback_snapshot(snapshotId: "backup-20251126-143052")
   ```

2. **Verify the snapshot first:**
   ```javascript
   list_snapshots()
   // Find the right snapshot ID
   rollback_snapshot(snapshotId: "backup-20251126-143052")
   ```

---

## cleanup_snapshots

**Delete old snapshots** — removes old snapshots to save disk space. Keeps the most recent snapshots.

⚠️ **DESTRUCTIVE**: Deleted snapshots cannot be recovered!

### Usage

```javascript
// Keep only 10 most recent snapshots
cleanup_snapshots(
    keepCount: 10
)

// Delete snapshots older than 30 days, keep at least 5
cleanup_snapshots(
    keepCount: 5,
    olderThanDays: 30
)
```

### Parameters

- **keepCount** (default: 20): Number of most recent snapshots to keep (minimum: 1)
- **olderThanDays** (optional): Delete snapshots older than this many days

### What It Returns

```json
{
    "success": true,
    "deletedCount": 15,
    "remainingCount": 10,
    "message": "Deleted 15 old snapshots. 10 snapshots remaining."
}
```

### When to Use

✅ **For maintenance:**
- When disk space is low
- After completing a major project phase
- Regular cleanup (weekly/monthly)

### Notes

- Git stashes are NOT auto-deleted (only file-based snapshots)
- Snapshots are stored in `.ultrasharp/snapshots/` directory

---

## Tool Comparison

| Action | Tool | Response Time |
|--------|------|---------------|
| Check semantic availability | `get_capabilities()` | < 100ms (cached) |
| Full capability report | `get_capabilities()` | < 3s (fresh) |
| Create snapshot | `create_snapshot()` | 1-5s |
| List snapshots | `list_snapshots()` | < 100ms |
| Rollback | `rollback_snapshot()` | 1-10s |
| Cleanup | `cleanup_snapshots()` | 1-5s |

---

## 📚 See Also

- [Semantic Mode Discovery](../../Dev.Docs/Features/Semantic/PHASE_1_SEMANTIC_DISCOVERY.md) - Architecture
- [Semantic Setup Guide](../Deployment/SEMANTIC_SETUP_GUIDE.md) - How to enable semantic mode
- [ROADMAP.md](../../ROADMAP.md) - Phase 13.5 (Semantic Discovery)

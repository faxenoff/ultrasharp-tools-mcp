# System & Capabilities

**Server capabilities** — status, snapshots, version management.

---

## Tools

| Tool | Purpose |
|------|---------|
| **get_capabilities** | Server status and features |
| **create_snapshot** | Create restore point |
| **list_snapshots** | List available snapshots |
| **rollback_snapshot** | Rollback to snapshot |
| **cleanup_snapshots** | Delete old snapshots |
| **undo** | Rollback last change |

---

## get_capabilities

**Server status** — available features, semantic mode status.

```javascript
get_capabilities()
```

**Returns:**
- Server info (name, version)
- **Semantic mode status**: Local | Overlord | Both | None
- Hybrid mode availability
- Enabled features

**When to use:**
- ✅ At startup — check capabilities
- ✅ Before semantic search — verify availability
- ✅ For diagnostics — understand configuration

---

## create_snapshot

**Create restore point** — safe experimentation.

```javascript
create_snapshot(
    description: "before refactoring UserService"
)
```

**Returns:** snapshot ID for rollback.

**When to use:**
- Before risky changes
- Before large refactoring
- For code experiments

---

## list_snapshots

**List snapshots** — ID, description, time, size.

```javascript
list_snapshots(
    limit: 20  // default: 20
)
```

---

## rollback_snapshot

**Rollback to snapshot** — restore all files.

```javascript
rollback_snapshot(
    snapshotId: "snap-20251113-143022"
)
```

⚠️ **DESTRUCTIVE:** Current changes will be lost!

---

## cleanup_snapshots

**Delete old snapshots** — free disk space.

```javascript
cleanup_snapshots(
    keepCount: 20,           // keep N most recent
    olderThanDays: null      // or delete older than N days
)
```

⚠️ **DESTRUCTIVE:** Deleted snapshots cannot be recovered!

---

## undo

**Rollback last change** — via git reset.

```javascript
undo()
```

**Can call multiple times:**
```javascript
undo()  // Rollback last
undo()  // Rollback previous
undo()  // And another
```

---

## Workflow: Safe Experiment

```javascript
// 1. Create snapshot before experiment
create_snapshot(description: "before experiment")
// Output: snapshot ID: snap-xxx

// 2. Make changes
modify_code(...)
add_member(...)
rename_symbol(...)

// 3a. If all good — continue
// (snapshot remains as backup)

// 3b. If something wrong — rollback
rollback_snapshot(snapshotId: "snap-xxx")
```

---

## Workflow: Series of Changes with Rollback

```javascript
// 1. First change
add_member(...)

// 2. Second change
modify_code(...)

// 3. Third change
rename_symbol(...)

// If need to rollback only last:
undo()

// If need to rollback all three:
undo()
undo()
undo()
```

---

## Git Integration

Each modification creates:
- Branch: `sharptools/YYYYMMDD-HHMMSS`
- Commit with change description

**Branch cleanup:**
```bash
# Option at startup
--git-branch-retention-count 10
--git-auto-cleanup
```

**Disable Git:**
```bash
--disable-git
```

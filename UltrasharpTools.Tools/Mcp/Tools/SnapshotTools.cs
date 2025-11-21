
using ModelContextProtocol;
using UltrasharpTools.Tools.Versioning;
using UltrasharpTools.Tools.Mcp;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for managing code snapshots (version control points).
/// Supports safe experimentation with multiple restore points.
/// </summary>
public class SnapshotToolsLogCategory { }

[McpServerToolType]
public static partial class SnapshotTools
{
/// <summary>
/// Create a snapshot of current code state for safe rollback.
/// </summary>
[McpServerTool(Name = "create_snapshot", Idempotent = false, ReadOnly = false, Destructive = false, OpenWorld = false)]
[Description("Create a snapshot of current code state. Snapshots allow safe experimentation with multiple restore points. " +
"Uses file-based backup in .ultrasharp/snapshots/ directory. Snapshots include metadata and can be listed/rolled back.")]
public static async Task<object> CreateSnapshot(
VersionManager versionManager,
ILogger<SnapshotToolsLogCategory> logger,

[Description("Snapshot description (e.g., 'before refactoring UserService')")]
string description,

[Description("Specific files to snapshot. If null, creates empty snapshot marker.")]
string[]? files = null,

CancellationToken cancellationToken = default)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(description, nameof(description), logger);

logger.LogInformation("Creating snapshot: {Description}", description);

var snapshotId = await versionManager.CreateSnapshotAsync(description, files, cancellationToken);

return ToolHelpers.ToJson(new
{
snapshotId,
description,
backend = snapshotId.StartsWith("backup-") ? "file" : "git",
filesCount = files?.Length ?? 0,
createdAt = DateTimeOffset.UtcNow,
message = $"Snapshot created: {snapshotId}. Use rollback_snapshot to restore this state."
});

}, logger, nameof(CreateSnapshot), cancellationToken);
}

/// <summary>
/// Rollback to a previous snapshot.
/// </summary>
[McpServerTool(Name = "rollback_snapshot", Idempotent = false, ReadOnly = false, Destructive = true, OpenWorld = false)]
[Description("Rollback code to a previous snapshot. This will restore files from the snapshot, overwriting current changes. " +
"⚠️ DESTRUCTIVE: Current changes will be lost. Consider creating a snapshot of current state first.")]
public static async Task<object> RollbackSnapshot(
VersionManager versionManager,
ILogger<SnapshotToolsLogCategory> logger,

[Description("Snapshot ID to rollback to (from list_snapshots)")]
string snapshotId,

CancellationToken cancellationToken = default)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(snapshotId, nameof(snapshotId), logger);

logger.LogInformation("Rolling back to snapshot: {SnapshotId}", snapshotId);

await versionManager.RollbackAsync(snapshotId, cancellationToken);

return ToolHelpers.ToJson(new
{
success = true,
snapshotId,
message = $"Successfully rolled back to snapshot: {snapshotId}"
});

}, logger, nameof(RollbackSnapshot), cancellationToken);
}

/// <summary>
/// List available snapshots.
/// </summary>
[McpServerTool(Name = "list_snapshots", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
[Description("List available code snapshots. Shows snapshot ID, description, creation time, file count, and backend type. " +
"Snapshots are ordered by creation time (newest first). Use snapshot ID with rollback_snapshot to restore.")]
public static async Task<object> ListSnapshots(
VersionManager versionManager,
ILogger<SnapshotToolsLogCategory> logger,

[Description("Maximum number of snapshots to return (default: 20)")]
int limit = 20,

CancellationToken cancellationToken = default)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
if (limit < 1 || limit > 100)
{
throw new McpException("Limit must be between 1 and 100");
}

logger.LogInformation("Listing snapshots (limit={Limit})", limit);

var snapshots = await versionManager.ListSnapshotsAsync(limit, cancellationToken);

return ToolHelpers.ToJson(new
{
totalCount = snapshots.Count,
snapshots = snapshots.Select(s => new
{
snapshotId = s.SnapshotId,
description = s.Description,
createdAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
fileCount = s.Files.Length,
backend = s.Backend,
sizeBytes = s.SizeBytes,
age = GetAge(s.CreatedAt)
}).ToList()
});

}, logger, nameof(ListSnapshots), cancellationToken);
}

/// <summary>
/// Cleanup old snapshots to save disk space.
/// </summary>
[McpServerTool(Name = "cleanup_snapshots", Idempotent = false, ReadOnly = false, Destructive = true, OpenWorld = false)]
[Description("Cleanup old snapshots to save disk space. Keeps most recent snapshots and deletes older ones. " +
"⚠️ DESTRUCTIVE: Deleted snapshots cannot be recovered. Git stashes are not auto-deleted.")]
public static async Task<object> CleanupSnapshots(
VersionManager versionManager,
ILogger<SnapshotToolsLogCategory> logger,

[Description("Number of most recent snapshots to keep (default: 20)")]
int keepCount = 20,

[Description("Delete snapshots older than this many days (default: null = no age limit)")]
int? olderThanDays = null,

CancellationToken cancellationToken = default)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
{
if (keepCount < 1)
{
throw new McpException("keepCount must be at least 1");
}

if (olderThanDays.HasValue && olderThanDays.Value < 1)
{
throw new McpException("olderThanDays must be at least 1");
}

logger.LogInformation("Cleaning up snapshots (keepCount={KeepCount}, olderThanDays={OlderThanDays})",
keepCount, olderThanDays);

// Get snapshots before cleanup
var beforeCount = (await versionManager.ListSnapshotsAsync(int.MaxValue, cancellationToken)).Count;

// Cleanup
var olderThan = olderThanDays.HasValue ? TimeSpan.FromDays(olderThanDays.Value) : (TimeSpan?)null;
await versionManager.CleanupOldSnapshotsAsync(keepCount, olderThan, cancellationToken);

// Get snapshots after cleanup
var afterCount = (await versionManager.ListSnapshotsAsync(int.MaxValue, cancellationToken)).Count;
var deletedCount = beforeCount - afterCount;

return ToolHelpers.ToJson(new
{
success = true,
deletedCount,
remainingCount = afterCount,
message = $"Deleted {deletedCount} old snapshots. {afterCount} snapshots remaining."
});

}, logger, nameof(CleanupSnapshots), cancellationToken);
}

// ==================== Helper Methods ====================

private static string GetAge(DateTimeOffset createdAt)
{
var age = DateTimeOffset.UtcNow - createdAt;

if (age.TotalMinutes < 1)
return "just now";
if (age.TotalHours < 1)
return $"{(int)age.TotalMinutes} min ago";
if (age.TotalDays < 1)
return $"{(int)age.TotalHours} hours ago";
if (age.TotalDays < 30)
return $"{(int)age.TotalDays} days ago";

return $"{(int)(age.TotalDays / 30)} months ago";
}
}

using Microsoft.Data.Sqlite;

using UltrasharpTools.Tools.Infrastructure;

using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Serialization;
using System.Security.Cryptography;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// SQLite-based persistent cache for call graph relationships.
/// Dramatically speeds up backtrace operations by caching SymbolFinder.FindCallersAsync results.
/// </summary>
public sealed partial class CallGraphCacheService : ICallGraphCacheService, IDisposable
{
private readonly ILogger<CallGraphCacheService> _logger;
private readonly string _cacheDbPath;
private readonly SemaphoreSlim _dbLock = new(1, 1);
private SqliteConnection? _connection;
private bool _disposed;

// In-memory stats for performance
private int _hitCount;
private int _missCount;

public CallGraphCacheService(ILogger<CallGraphCacheService> logger)
{
_logger = logger;

// Store cache in .ultrasharp/cache/callgraph in project root
var cacheDir = ProjectPathHelper.GetCallGraphCachePath();

_cacheDbPath = Path.Combine(cacheDir, "callgraph.db");
_logger.LogInformation("CallGraphCache database path: {DbPath}", _cacheDbPath);

InitializeDatabaseAsync().GetAwaiter().GetResult();
}

private async Task InitializeDatabaseAsync()
{
await _dbLock.WaitAsync();
try
{
_connection = new SqliteConnection($"Data Source={_cacheDbPath}");
await _connection.OpenAsync();

// Create schema
var createTableSql = @"
CREATE TABLE IF NOT EXISTS CallGraph (
MethodFqn TEXT NOT NULL,
SolutionHash TEXT NOT NULL,
CallersFqnJson TEXT NOT NULL,
Timestamp INTEGER NOT NULL,
FilePath TEXT,
PRIMARY KEY (MethodFqn, SolutionHash)
);

CREATE INDEX IF NOT EXISTS idx_method ON CallGraph(MethodFqn);
CREATE INDEX IF NOT EXISTS idx_filepath ON CallGraph(FilePath);
CREATE INDEX IF NOT EXISTS idx_timestamp ON CallGraph(Timestamp);

CREATE TABLE IF NOT EXISTS CacheMetadata (
Key TEXT PRIMARY KEY,
Value TEXT NOT NULL
);
";

await using var cmd = _connection.CreateCommand();
cmd.CommandText = createTableSql;
await cmd.ExecuteNonQueryAsync();

// Set version
await SetMetadataAsync("Version", "1");

_logger.LogInformation("CallGraphCache database initialized successfully");
}
finally
{
_dbLock.Release();
}
}

public async Task<List<string>?> GetCallersAsync(
string methodFqn,
string solutionHash,
CancellationToken cancellationToken = default
)
{
await _dbLock.WaitAsync(cancellationToken);
try
{
var sql = @"
SELECT CallersFqnJson
FROM CallGraph
WHERE MethodFqn = @methodFqn AND SolutionHash = @solutionHash
LIMIT 1
";

await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;
cmd.Parameters.AddWithValue("@methodFqn", methodFqn);
cmd.Parameters.AddWithValue("@solutionHash", solutionHash);

await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
if (await reader.ReadAsync(cancellationToken))
{
var json = reader.GetString(0);
// Use source-generated JSON context for 2-3x faster deserialization
var callers = JsonSerializer.Deserialize(json, UltrasharpToolsJsonContext.Default.ListString);

Interlocked.Increment(ref _hitCount);
_logger.LogDebug("Cache HIT for method: {Method}", methodFqn);

return callers;
}

Interlocked.Increment(ref _missCount);
_logger.LogDebug("Cache MISS for method: {Method}", methodFqn);

return null;
}
finally
{
_dbLock.Release();
}
}

public async Task SetCallersAsync(
string methodFqn,
List<string> callerFqns,
string solutionHash,
CancellationToken cancellationToken = default
)
{
await _dbLock.WaitAsync(cancellationToken);
try
{
// Use source-generated JSON context for 2-5x faster serialization
var json = JsonSerializer.Serialize(callerFqns, UltrasharpToolsJsonContext.Default.ListString);
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

var sql = @"
INSERT OR REPLACE INTO CallGraph (MethodFqn, SolutionHash, CallersFqnJson, Timestamp, FilePath)
VALUES (@methodFqn, @solutionHash, @json, @timestamp, @filePath)
";

await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;
cmd.Parameters.AddWithValue("@methodFqn", methodFqn);
cmd.Parameters.AddWithValue("@solutionHash", solutionHash);
cmd.Parameters.AddWithValue("@json", json);
cmd.Parameters.AddWithValue("@timestamp", timestamp);
cmd.Parameters.AddWithValue("@filePath", DBNull.Value); // Can enhance later with source file tracking

await cmd.ExecuteNonQueryAsync(cancellationToken);

_logger.LogDebug("Cached callers for method: {Method}, count: {Count}", methodFqn, callerFqns.Count);
}
finally
{
_dbLock.Release();
}
}

public async Task InvalidateByFilesAsync(
List<string> modifiedFilePaths,
CancellationToken cancellationToken = default
)
{
if (modifiedFilePaths == null || modifiedFilePaths.Count == 0)
{
return;
}

await _dbLock.WaitAsync(cancellationToken);
try
{
// For now, invalidate by file path
// In future, could be smarter by tracking method -> file mapping
var sql = "DELETE FROM CallGraph WHERE FilePath IN (" +
string.Join(",", modifiedFilePaths.Select((_, i) => $"@path{i}")) + ")";

await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;
for (int i = 0; i < modifiedFilePaths.Count; i++)
{
cmd.Parameters.AddWithValue($"@path{i}", modifiedFilePaths[i]);
}

var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
_logger.LogInformation("Invalidated {Count} cache entries for modified files", deleted);
}
finally
{
_dbLock.Release();
}
}

public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
{
await _dbLock.WaitAsync(cancellationToken);
try
{
var sql = "DELETE FROM CallGraph";
await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;

var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
_logger.LogInformation("Invalidated entire call graph cache ({Count} entries)", deleted);

// Reset stats
_hitCount = 0;
_missCount = 0;
}
finally
{
_dbLock.Release();
}
}

public async Task<CallGraphCacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
{
await _dbLock.WaitAsync(cancellationToken);
try
{
var sql = @"
SELECT
COUNT(*) as TotalEntries,
MIN(Timestamp) as OldestTimestamp,
MAX(Timestamp) as NewestTimestamp
FROM CallGraph
";

await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;

await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
if (await reader.ReadAsync(cancellationToken))
{
var totalEntries = reader.GetInt32(0);
var oldestTimestamp = reader.IsDBNull(1) ? (DateTime?)null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)).DateTime;
var newestTimestamp = reader.IsDBNull(2) ? (DateTime?)null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).DateTime;

// Get file size
var fileInfo = new FileInfo(_cacheDbPath);
var cacheSize = fileInfo.Exists ? fileInfo.Length : 0;

return new CallGraphCacheStats
{
TotalEntries = totalEntries,
HitCount = _hitCount,
MissCount = _missCount,
CacheSizeBytes = cacheSize,
OldestEntryTimestamp = oldestTimestamp,
NewestEntryTimestamp = newestTimestamp
};
}

return new CallGraphCacheStats();
}
finally
{
_dbLock.Release();
}
}

public async Task CompactAsync(CancellationToken cancellationToken = default)
{
await _dbLock.WaitAsync(cancellationToken);
try
{
var sql = "VACUUM";
await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;
await cmd.ExecuteNonQueryAsync(cancellationToken);

_logger.LogInformation("Compacted call graph cache database");
}
finally
{
_dbLock.Release();
}
}

private async Task SetMetadataAsync(string key, string value)
{
var sql = "INSERT OR REPLACE INTO CacheMetadata (Key, Value) VALUES (@key, @value)";
await using var cmd = _connection!.CreateCommand();
cmd.CommandText = sql;
cmd.Parameters.AddWithValue("@key", key);
cmd.Parameters.AddWithValue("@value", value);
await cmd.ExecuteNonQueryAsync();
}

public void Dispose()
{
if (_disposed) return;

_dbLock.Wait();
try
{
_connection?.Dispose();
_disposed = true;
}
finally
{
_dbLock.Release();
_dbLock.Dispose();
}
}
}

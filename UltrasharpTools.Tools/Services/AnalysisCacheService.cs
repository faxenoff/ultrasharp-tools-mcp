
using Microsoft.Data.Sqlite;

using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Serialization;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Persistent cache for analysis results using SQLite.
/// Provides 2-3x speedup for repeated analysis operations.
/// </summary>
public class AnalysisCacheService : IDisposable
{
private readonly ILogger _logger;
private readonly string _dbPath;
private readonly SqliteConnection _connection;
private const int CacheVersion = 1;
private const int DefaultTtlSeconds = 3600; // 1 hour

public AnalysisCacheService(ILogger logger, string? cacheDirectory = null)
{
_logger = logger ?? throw new ArgumentNullException(nameof(logger));

// Cache directory: .ultrasharp/cache/analysis in project root, or custom directory
var baseDir = cacheDirectory ?? ProjectPathHelper.GetAnalysisCachePath();
if (!Directory.Exists(baseDir))
{
Directory.CreateDirectory(baseDir);
_logger.LogInformation("Created analysis cache directory: {Directory}", baseDir);
}

_dbPath = Path.Combine(baseDir, "analysis_cache.db");

// Initialize SQLite connection
_connection = new SqliteConnection($"Data Source={_dbPath}");
_connection.Open();

InitializeDatabase();

_logger.LogInformation("AnalysisCacheService initialized with database: {DbPath}", _dbPath);
}

private void InitializeDatabase()
{
using var command = _connection.CreateCommand();
command.CommandText = @"
CREATE TABLE IF NOT EXISTS cache_entries (
cache_key TEXT PRIMARY KEY,
operation_name TEXT NOT NULL,
solution_hash TEXT NOT NULL,
parameters_hash TEXT NOT NULL,
result_data TEXT NOT NULL,
created_at INTEGER NOT NULL,
accessed_at INTEGER NOT NULL,
ttl_seconds INTEGER NOT NULL,
version INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_solution_hash ON cache_entries(solution_hash);
CREATE INDEX IF NOT EXISTS idx_operation_name ON cache_entries(operation_name);
CREATE INDEX IF NOT EXISTS idx_accessed_at ON cache_entries(accessed_at);
";
command.ExecuteNonQuery();

_logger.LogDebug("Database schema initialized");
}

/// <summary>
/// Try to get cached result for an operation
/// </summary>
public bool TryGetCached<T>(string solutionHash, string operationName, object parameters, out T? result)
{
result = default;

try
{
var cacheKey = ComputeCacheKey(solutionHash, operationName, parameters);

using var command = _connection.CreateCommand();
command.CommandText = @"
SELECT result_data, created_at, ttl_seconds, version
FROM cache_entries
WHERE cache_key = @cacheKey
LIMIT 1;
";
command.Parameters.AddWithValue("@cacheKey", cacheKey);

using var reader = command.ExecuteReader();
if (!reader.Read())
{
_logger.LogTrace("Cache miss for {Operation} (key: {Key})", operationName, cacheKey);
return false;
}

var resultData = reader.GetString(0);
var createdAt = reader.GetInt64(1);
var ttlSeconds = reader.GetInt32(2);
var version = reader.GetInt32(3);

// Check version
if (version != CacheVersion)
{
_logger.LogDebug("Cache version mismatch for {Operation}: expected {Expected}, got {Actual}",
operationName, CacheVersion, version);
return false;
}

// Check TTL
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
if (now - createdAt > ttlSeconds)
{
_logger.LogDebug("Cache entry expired for {Operation} (age: {Age}s, TTL: {Ttl}s)",
operationName, now - createdAt, ttlSeconds);
return false;
}

// Deserialize result - use source-generated context for known types
result = JsonSerializer.Deserialize<T>(resultData, new JsonSerializerOptions
{
TypeInfoResolver = UltrasharpToolsJsonContext.Default
});

// Update accessed_at
UpdateAccessTime(cacheKey);

_logger.LogDebug("Cache hit for {Operation} (key: {Key})", operationName, cacheKey);
return result != null;
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Error reading from cache for {Operation}", operationName);
return false;
}
}

/// <summary>
/// Store result in cache
/// </summary>
public void SetCached<T>(string solutionHash, string operationName, object parameters, T result, int ttlSeconds = DefaultTtlSeconds)
{
try
{
var cacheKey = ComputeCacheKey(solutionHash, operationName, parameters);
// Use source-generated context for faster serialization
var serializerOptions = new JsonSerializerOptions { TypeInfoResolver = UltrasharpToolsJsonContext.Default };
var parametersHash = ComputeHash(JsonSerializer.Serialize(parameters, serializerOptions));
var resultData = JsonSerializer.Serialize(result, serializerOptions);
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

using var command = _connection.CreateCommand();
command.CommandText = @"
INSERT OR REPLACE INTO cache_entries
(cache_key, operation_name, solution_hash, parameters_hash, result_data, created_at, accessed_at, ttl_seconds, version)
VALUES
(@cacheKey, @operationName, @solutionHash, @parametersHash, @resultData, @createdAt, @accessedAt, @ttlSeconds, @version);
";

command.Parameters.AddWithValue("@cacheKey", cacheKey);
command.Parameters.AddWithValue("@operationName", operationName);
command.Parameters.AddWithValue("@solutionHash", solutionHash);
command.Parameters.AddWithValue("@parametersHash", parametersHash);
command.Parameters.AddWithValue("@resultData", resultData);
command.Parameters.AddWithValue("@createdAt", now);
command.Parameters.AddWithValue("@accessedAt", now);
command.Parameters.AddWithValue("@ttlSeconds", ttlSeconds);
command.Parameters.AddWithValue("@version", CacheVersion);

command.ExecuteNonQuery();

_logger.LogDebug("Cached result for {Operation} (key: {Key}, size: {Size} bytes)",
operationName, cacheKey, resultData.Length);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Error writing to cache for {Operation}", operationName);
}
}

/// <summary>
/// Invalidate all cache entries for a specific solution
/// </summary>
public void InvalidateSolution(string solutionHash)
{
try
{
using var command = _connection.CreateCommand();
command.CommandText = "DELETE FROM cache_entries WHERE solution_hash = @solutionHash;";
command.Parameters.AddWithValue("@solutionHash", solutionHash);

var deletedCount = command.ExecuteNonQuery();
_logger.LogInformation("Invalidated {Count} cache entries for solution {Hash}", deletedCount, solutionHash);
}
catch (Exception ex)
{
_logger.LogError(ex, "Error invalidating cache for solution {Hash}", solutionHash);
}
}

/// <summary>
/// Clear expired entries from cache
/// </summary>
public void CleanupExpired()
{
try
{
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

using var command = _connection.CreateCommand();
command.CommandText = @"
DELETE FROM cache_entries
WHERE (created_at + ttl_seconds) < @now;
";
command.Parameters.AddWithValue("@now", now);

var deletedCount = command.ExecuteNonQuery();
if (deletedCount > 0)
{
_logger.LogInformation("Cleaned up {Count} expired cache entries", deletedCount);
}
}
catch (Exception ex)
{
_logger.LogError(ex, "Error cleaning up expired cache entries");
}
}

/// <summary>
/// Get cache statistics
/// </summary>
public AnalysisCacheStatistics GetStatistics()
{
try
{
using var command = _connection.CreateCommand();
command.CommandText = @"
SELECT
COUNT(*) as total_entries,
SUM(LENGTH(result_data)) as total_size,
COUNT(DISTINCT solution_hash) as unique_solutions,
COUNT(DISTINCT operation_name) as unique_operations
FROM cache_entries;
";

using var reader = command.ExecuteReader();
if (reader.Read())
{
return new AnalysisCacheStatistics
{
TotalEntries = reader.GetInt32(0),
TotalSizeBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
UniqueSolutions = reader.GetInt32(2),
UniqueOperations = reader.GetInt32(3)
};
}
}
catch (Exception ex)
{
_logger.LogError(ex, "Error getting cache statistics");
}

return new AnalysisCacheStatistics();
}

/// <summary>
/// Clear all cache entries
/// </summary>
public void ClearAll()
{
try
{
using var command = _connection.CreateCommand();
command.CommandText = "DELETE FROM cache_entries;";
var deletedCount = command.ExecuteNonQuery();

_logger.LogInformation("Cleared all cache entries ({Count} deleted)", deletedCount);
}
catch (Exception ex)
{
_logger.LogError(ex, "Error clearing cache");
}
}

private void UpdateAccessTime(string cacheKey)
{
try
{
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

using var command = _connection.CreateCommand();
command.CommandText = "UPDATE cache_entries SET accessed_at = @now WHERE cache_key = @cacheKey;";
command.Parameters.AddWithValue("@now", now);
command.Parameters.AddWithValue("@cacheKey", cacheKey);
command.ExecuteNonQuery();
}
catch (Exception ex)
{
_logger.LogTrace(ex, "Error updating access time for cache key {Key}", cacheKey);
}
}

private static string ComputeCacheKey(string solutionHash, string operationName, object parameters)
{
// Use source-generated context for faster serialization
var parametersJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
{
TypeInfoResolver = UltrasharpToolsJsonContext.Default
});
var combined = $"{solutionHash}|{operationName}|{parametersJson}";
return ComputeHash(combined);
}

private static string ComputeHash(string input)
{
// Use xxHash3 for 10x faster hashing (non-cryptographic is fine for cache keys)
return FastHash.ComputeHash(input);
}

public void Dispose()
{
_connection?.Dispose();
}
}

/// <summary>
/// Analysis cache statistics
/// </summary>
public class AnalysisCacheStatistics
{
public int TotalEntries { get; set; }
public long TotalSizeBytes { get; set; }
public int UniqueSolutions { get; set; }
public int UniqueOperations { get; set; }

public string TotalSizeMB => $"{TotalSizeBytes / 1024.0 / 1024.0:F2} MB";
}

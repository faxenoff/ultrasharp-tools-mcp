using Microsoft.Data.Sqlite;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Serialization;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Persistent cache for analysis results using SQLite.
/// Provides 2-3x speedup for repeated analysis operations.
/// </summary>
public partial class AnalysisCacheService : IDisposable
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
            LogCacheDirectoryCreated(baseDir);
        }

        _dbPath = Path.Combine(baseDir, "analysis_cache.db");

        // Initialize SQLite connection
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        InitializeDatabase();

        LogInitialized(_dbPath);
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            @"
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

        LogSchemaInitialized();
    }

    /// <summary>
    /// Try to get cached result for an operation
    /// </summary>
    public bool TryGetCached<T>(
        string solutionHash,
        string operationName,
        object parameters,
        out T? result
    )
    {
        result = default;

        try
        {
            var cacheKey = ComputeCacheKey(solutionHash, operationName, parameters);

            using var command = _connection.CreateCommand();
            command.CommandText =
                @"
SELECT result_data, created_at, ttl_seconds, version
FROM cache_entries
WHERE cache_key = @cacheKey
LIMIT 1;
";
            command.Parameters.AddWithValue("@cacheKey", cacheKey);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                LogCacheMiss(operationName, cacheKey);
                return false;
            }

            var resultData = reader.GetString(0);
            var createdAt = reader.GetInt64(1);
            var ttlSeconds = reader.GetInt32(2);
            var version = reader.GetInt32(3);

            // Check version
            if (version != CacheVersion)
            {
                LogVersionMismatch(operationName, CacheVersion, version);
                return false;
            }

            // Check TTL
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - createdAt > ttlSeconds)
            {
                LogCacheExpired(operationName, now - createdAt, ttlSeconds);
                return false;
            }

            // Deserialize result - use source-generated context for known types
            result = JsonSerializer.Deserialize<T>(
                resultData,
                new JsonSerializerOptions { TypeInfoResolver = UltrasharpToolsJsonContext.Default }
            );

            // Update accessed_at
            UpdateAccessTime(cacheKey);

            LogCacheHit(operationName, cacheKey);
            return result != null;
        }
        catch (Exception ex)
        {
            LogCacheReadError(ex, operationName);
            return false;
        }
    }

    /// <summary>
    /// Store result in cache
    /// </summary>
    public void SetCached<T>(
        string solutionHash,
        string operationName,
        object parameters,
        T result,
        int ttlSeconds = DefaultTtlSeconds
    )
    {
        try
        {
            var cacheKey = ComputeCacheKey(solutionHash, operationName, parameters);
            // Use source-generated context for faster serialization
            var serializerOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = UltrasharpToolsJsonContext.Default,
            };
            var parametersHash = ComputeHash(
                JsonSerializer.Serialize(parameters, serializerOptions)
            );

            // Try to serialize result - skip caching if not supported
            string resultData;
            try
            {
                resultData = JsonSerializer.Serialize(result, serializerOptions);
            }
            catch (NotSupportedException)
            {
                // Type not registered in JsonContext - skip caching
                LogSkippingCache(operationName, typeof(T).Name);
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var command = _connection.CreateCommand();
            command.CommandText =
                @"
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

            LogResultCached(operationName, cacheKey, resultData.Length);
        }
        catch (Exception ex)
        {
            LogCacheWriteError(ex, operationName);
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
            LogSolutionInvalidated(deletedCount, solutionHash);
        }
        catch (Exception ex)
        {
            LogInvalidationError(ex, solutionHash);
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
            command.CommandText =
                @"
DELETE FROM cache_entries
WHERE (created_at + ttl_seconds) < @now;
";
            command.Parameters.AddWithValue("@now", now);

            var deletedCount = command.ExecuteNonQuery();
            if (deletedCount > 0)
            {
                LogExpiredCleaned(deletedCount);
            }
        }
        catch (Exception ex)
        {
            LogCleanupError(ex);
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
            command.CommandText =
                @"
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
                    UniqueOperations = reader.GetInt32(3),
                };
            }
        }
        catch (Exception ex)
        {
            LogStatisticsError(ex);
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

            LogCacheCleared(deletedCount);
        }
        catch (Exception ex)
        {
            LogClearError(ex);
        }
    }

    private void UpdateAccessTime(string cacheKey)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var command = _connection.CreateCommand();
            command.CommandText =
                "UPDATE cache_entries SET accessed_at = @now WHERE cache_key = @cacheKey;";
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue("@cacheKey", cacheKey);
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            LogAccessTimeError(ex, cacheKey);
        }
    }

    private static string ComputeCacheKey(
        string solutionHash,
        string operationName,
        object parameters
    )
    {
        string parametersJson;
        try
        {
            // Use source-generated context for faster serialization
            parametersJson = JsonSerializer.Serialize(
                parameters,
                new JsonSerializerOptions { TypeInfoResolver = UltrasharpToolsJsonContext.Default }
            );
        }
        catch (NotSupportedException)
        {
            // Fallback for types not registered in JsonContext (e.g. anonymous types)
            // This should not happen in normal operation, but provides graceful degradation
            parametersJson = parameters?.ToString() ?? "null";
        }
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

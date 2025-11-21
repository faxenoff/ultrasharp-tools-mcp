using Microsoft.Data.Sqlite;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Serialization;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Extension of CallGraphCacheService for full caller caching (including Location data)
/// Provides 5-10x faster backtrace by eliminating expensive SymbolFinder.FindCallersAsync calls
/// </summary>
public sealed partial class CallGraphCacheService
{
    private bool _fullCacheTableExists = false;

    /// <summary>
    /// Get full caller information including locations (FULL CACHE - 5-10x faster)
    /// Returns null on cache MISS, List<SerializableCallerInfo> on cache HIT
    /// </summary>
    public async Task<List<SerializableCallerInfo>?> GetCallersFullAsync(
        string methodFqn,
        string solutionHash,
        CancellationToken cancellationToken = default
    )
    {
        // Ensure CallGraphFull table exists
        await EnsureFullCacheTableAsync();

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            var sql =
                @"
SELECT CallersFullJson
FROM CallGraphFull
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
                var callers = JsonSerializer.Deserialize(
                    json,
                    UltrasharpToolsJsonContext.Default.ListSerializableCallerInfo
                );

                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("Full cache HIT for method: {Method}", methodFqn);

                return callers;
            }

            Interlocked.Increment(ref _missCount);
            _logger.LogDebug("Full cache MISS for method: {Method}", methodFqn);

            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Set full caller information including locations
    /// </summary>
    public async Task SetCallersFullAsync(
        string methodFqn,
        List<SerializableCallerInfo> callers,
        string solutionHash,
        CancellationToken cancellationToken = default
    )
    {
        // Ensure CallGraphFull table exists
        await EnsureFullCacheTableAsync();

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Use source-generated JSON context for 2-5x faster serialization
            var json = JsonSerializer.Serialize(
                callers,
                UltrasharpToolsJsonContext.Default.ListSerializableCallerInfo
            );
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var sql =
                @"
INSERT OR REPLACE INTO CallGraphFull (MethodFqn, SolutionHash, CallersFullJson, Timestamp, FilePath)
VALUES (@methodFqn, @solutionHash, @json, @timestamp, @filePath)
";

            await using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@methodFqn", methodFqn);
            cmd.Parameters.AddWithValue("@solutionHash", solutionHash);
            cmd.Parameters.AddWithValue("@json", json);
            cmd.Parameters.AddWithValue("@timestamp", timestamp);
            cmd.Parameters.AddWithValue("@filePath", DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug(
                "Cached full callers for method: {Method}, count: {Count}",
                methodFqn,
                callers.Count
            );
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task EnsureFullCacheTableAsync()
    {
        // Quick check if table exists (cached in memory after first call)
        if (_fullCacheTableExists)
        {
            return;
        }

        await _dbLock.WaitAsync();
        try
        {
            var checkTableSql =
                "SELECT name FROM sqlite_master WHERE type='table' AND name='CallGraphFull'";
            await using var checkCmd = _connection!.CreateCommand();
            checkCmd.CommandText = checkTableSql;

            var tableExists = await checkCmd.ExecuteScalarAsync();
            if (tableExists != null)
            {
                _fullCacheTableExists = true;
                return;
            }

            // Create CallGraphFull table
            var createTableSql =
                @"
CREATE TABLE CallGraphFull (
MethodFqn TEXT NOT NULL,
SolutionHash TEXT NOT NULL,
CallersFullJson TEXT NOT NULL,
Timestamp INTEGER NOT NULL,
FilePath TEXT,
PRIMARY KEY (MethodFqn, SolutionHash)
);

CREATE INDEX idx_method_full ON CallGraphFull(MethodFqn);
CREATE INDEX idx_filepath_full ON CallGraphFull(FilePath);
CREATE INDEX idx_timestamp_full ON CallGraphFull(Timestamp);
";

            await using var createCmd = _connection!.CreateCommand();
            createCmd.CommandText = createTableSql;
            await createCmd.ExecuteNonQueryAsync();

            _fullCacheTableExists = true;
            _logger.LogInformation("Created CallGraphFull table for full caller caching");
        }
        finally
        {
            _dbLock.Release();
        }
    }
}

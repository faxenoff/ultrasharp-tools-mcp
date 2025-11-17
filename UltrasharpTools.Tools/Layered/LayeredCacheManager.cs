using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Manages persistent SQLite storage for branch deltas.
/// Stores BranchDelta objects to survive process restarts.
/// Database location: .ultrasharp/layered/deltas.db
/// </summary>
public sealed class LayeredCacheManager : IDisposable
{
    private readonly string _dbPath;
    private readonly ILogger<LayeredCacheManager> _logger;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public LayeredCacheManager(string solutionPath, ILogger<LayeredCacheManager>? logger = null)
    {
        _logger = logger ?? NullLogger<LayeredCacheManager>.Instance;

        // Determine database path: .ultrasharp/layered/deltas.db
        var ultrasharpDir = ProjectPathHelper.GetProjectUltrasharpDir(solutionPath);
        var layeredDir = Path.Combine(ultrasharpDir, "layered");
        Directory.CreateDirectory(layeredDir);

        _dbPath = Path.Combine(layeredDir, "deltas.db");
        _logger.LogInformation("LayeredCacheManager initialized with database: {DbPath}", _dbPath);

        InitializeDatabase();
    }

    /// <summary>
    /// Initialize SQLite database and create schema if needed.
    /// </summary>
    private void InitializeDatabase()
    {
        try
        {
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            var createTableCmd = _connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS BranchDeltas (
                    BranchName TEXT PRIMARY KEY,
                    BaseCommitSha TEXT NOT NULL,
                    LastModified INTEGER NOT NULL,
                    AddedSymbols TEXT NOT NULL,
                    ModifiedSymbols TEXT NOT NULL,
                    DeletedSymbolIds TEXT NOT NULL
                )";
            createTableCmd.ExecuteNonQuery();

            _logger.LogDebug("SQLite schema initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite database at {DbPath}", _dbPath);
            throw;
        }
    }

    /// <summary>
    /// Save branch delta to persistent storage.
    /// </summary>
    public async Task SaveBranchDeltaAsync(BranchDelta delta, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            _logger.LogWarning("Cannot save delta - database not initialized");
            return;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Serialize collections to JSON
            var addedJson = JsonSerializer.Serialize(delta.AddedSymbols, JsonOptions);
            var modifiedJson = JsonSerializer.Serialize(delta.ModifiedSymbols, JsonOptions);
            var deletedJson = JsonSerializer.Serialize(delta.DeletedSymbolIds, JsonOptions);

            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO BranchDeltas
                (BranchName, BaseCommitSha, LastModified, AddedSymbols, ModifiedSymbols, DeletedSymbolIds)
                VALUES (@branchName, @baseCommit, @lastModified, @added, @modified, @deleted)";

            cmd.Parameters.AddWithValue("@branchName", delta.BranchName ?? "");
            cmd.Parameters.AddWithValue("@baseCommit", delta.BaseCommitSha ?? "");
            cmd.Parameters.AddWithValue("@lastModified", delta.LastModified.Ticks);
            cmd.Parameters.AddWithValue("@added", addedJson);
            cmd.Parameters.AddWithValue("@modified", modifiedJson);
            cmd.Parameters.AddWithValue("@deleted", deletedJson);

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Saved branch delta: {Branch} ({Added} added, {Modified} modified, {Deleted} deleted)",
                delta.BranchName, delta.AddedSymbols.Count, delta.ModifiedSymbols.Count, delta.DeletedSymbolIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save branch delta: {Branch}", delta.BranchName);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Load branch delta from persistent storage.
    /// </summary>
    public async Task<BranchDelta?> LoadBranchDeltaAsync(string branchName, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            _logger.LogWarning("Cannot load delta - database not initialized");
            return null;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT BaseCommitSha, LastModified, AddedSymbols, ModifiedSymbols, DeletedSymbolIds
                FROM BranchDeltas
                WHERE BranchName = @branchName";
            cmd.Parameters.AddWithValue("@branchName", branchName);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                _logger.LogDebug("Branch delta not found in storage: {Branch}", branchName);
                return null;
            }

            var baseCommitSha = reader.GetString(0);
            var lastModifiedTicks = reader.GetInt64(1);
            var addedJson = reader.GetString(2);
            var modifiedJson = reader.GetString(3);
            var deletedJson = reader.GetString(4);

            // Deserialize collections from JSON
            var addedSymbols = JsonSerializer.Deserialize<Dictionary<string, SymbolIndexEntry>>(addedJson, JsonOptions)
                ?? new Dictionary<string, SymbolIndexEntry>();
            var modifiedSymbols = JsonSerializer.Deserialize<Dictionary<string, SymbolIndexEntry>>(modifiedJson, JsonOptions)
                ?? new Dictionary<string, SymbolIndexEntry>();
            var deletedSymbolIds = JsonSerializer.Deserialize<HashSet<string>>(deletedJson, JsonOptions)
                ?? new HashSet<string>();

            var delta = new BranchDelta
            {
                BranchName = branchName,
                BaseCommitSha = baseCommitSha,
                LastModified = new DateTime(lastModifiedTicks)
            };

            // Populate collections
            foreach (var kvp in addedSymbols)
            {
                delta.AddedSymbols[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in modifiedSymbols)
            {
                delta.ModifiedSymbols[kvp.Key] = kvp.Value;
            }
            foreach (var id in deletedSymbolIds)
            {
                delta.DeletedSymbolIds.Add(id);
            }

            _logger.LogInformation("Loaded branch delta from storage: {Branch} ({Added} added, {Modified} modified, {Deleted} deleted)",
                branchName, delta.AddedSymbols.Count, delta.ModifiedSymbols.Count, delta.DeletedSymbolIds.Count);

            return delta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load branch delta: {Branch}", branchName);
            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Delete branch delta from persistent storage.
    /// </summary>
    public async Task DeleteBranchDeltaAsync(string branchName, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            return;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM BranchDeltas WHERE BranchName = @branchName";
            cmd.Parameters.AddWithValue("@branchName", branchName);

            var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected > 0)
            {
                _logger.LogDebug("Deleted branch delta from storage: {Branch}", branchName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete branch delta: {Branch}", branchName);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Get all branch names stored in cache.
    /// </summary>
    public async Task<List<string>> GetAllBranchNamesAsync(CancellationToken cancellationToken = default)
    {
        var branches = new List<string>();

        if (_connection == null)
        {
            return branches;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT BranchName FROM BranchDeltas ORDER BY LastModified DESC";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                branches.Add(reader.GetString(0));
            }

            _logger.LogDebug("Found {Count} branch deltas in storage", branches.Count);
            return branches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get branch names from storage");
            return branches;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Clear all branch deltas from storage.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            return;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM BranchDeltas";
            var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Cleared all branch deltas from storage ({Count} rows deleted)", rowsAffected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear branch deltas from storage");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void Dispose()
    {
        _dbLock.Dispose();
        _connection?.Dispose();
    }
}

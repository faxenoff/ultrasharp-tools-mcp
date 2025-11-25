using System.Buffers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Manages persistent storage of vector deltas in SQLite.
/// Database location: .ultrasharp/layered/vector_deltas.db
/// Phase 4.3: Vector Cache Persistence
/// </summary>
public class VectorCacheManager : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly ILogger<VectorCacheManager> _logger;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public VectorCacheManager(string solutionPath, ILogger<VectorCacheManager>? logger = null)
    {
        var ultrasharpDir = ProjectPathHelper.GetProjectUltrasharpDir(solutionPath);
        var layeredDir = Path.Combine(ultrasharpDir, "layered");
        Directory.CreateDirectory(layeredDir);

        _databasePath = Path.Combine(layeredDir, "vector_deltas.db");
        _logger = logger ?? NullLogger<VectorCacheManager>.Instance;

        _logger.LogInformation(
            "VectorCacheManager initialized with database: {Path}",
            _databasePath
        );
    }

    /// <summary>
    /// Initialize database schema.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            var createTableSql =
                @"
CREATE TABLE IF NOT EXISTS VectorDeltas (
BranchName TEXT PRIMARY KEY,
BaseCommitSha TEXT NOT NULL,
LastModified INTEGER NOT NULL,
AddedEmbeddings TEXT NOT NULL,
ModifiedEmbeddings TEXT NOT NULL,
DeletedSymbolIds TEXT NOT NULL
)";

            await using var command = connection.CreateCommand();
            command.CommandText = createTableSql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            _initialized = true;
            _logger.LogInformation("VectorCacheManager database initialized");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Save vector delta to SQLite.
    /// </summary>
    public async Task SaveVectorDeltaAsync(
        VectorDelta delta,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureInitializedAsync(cancellationToken);
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Serialize embeddings dictionaries
            var addedJson = SerializeEmbeddings(delta.AddedEmbeddings);
            var modifiedJson = SerializeEmbeddings(delta.ModifiedEmbeddings);
            var deletedJson = JsonSerializer.Serialize(
                delta.DeletedSymbolIds.Items.ToList(),
                JsonOptions
            );

            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            var sql =
                @"
INSERT OR REPLACE INTO VectorDeltas
(BranchName, BaseCommitSha, LastModified, AddedEmbeddings, ModifiedEmbeddings, DeletedSymbolIds)
VALUES (@branchName, @baseCommitSha, @lastModified, @addedEmbeddings, @modifiedEmbeddings, @deletedSymbolIds)";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@branchName", delta.BranchName);
            command.Parameters.AddWithValue("@baseCommitSha", delta.BaseCommitSha);
            command.Parameters.AddWithValue(
                "@lastModified",
                delta.LastModified.ToUnixTimeSeconds()
            );
            command.Parameters.AddWithValue("@addedEmbeddings", addedJson);
            command.Parameters.AddWithValue("@modifiedEmbeddings", modifiedJson);
            command.Parameters.AddWithValue("@deletedSymbolIds", deletedJson);

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug(
                "Saved vector delta to database: {Branch} ({AddedCount} added, {ModifiedCount} modified, {DeletedCount} deleted)",
                delta.BranchName,
                delta.AddedEmbeddings.Count,
                delta.ModifiedEmbeddings.Count,
                delta.DeletedSymbolIds.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save vector delta: {Branch}", delta.BranchName);
            throw;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Load vector delta from SQLite.
    /// </summary>
    public async Task<VectorDelta?> LoadVectorDeltaAsync(
        string branchName,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureInitializedAsync(cancellationToken);
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            var sql =
                @"
SELECT BranchName, BaseCommitSha, LastModified, AddedEmbeddings, ModifiedEmbeddings, DeletedSymbolIds
FROM VectorDeltas
WHERE BranchName = @branchName";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@branchName", branchName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                _logger.LogDebug("No vector delta found in database: {Branch}", branchName);
                return null;
            }

            var delta = new VectorDelta
            {
                BranchName = reader.GetString(0),
                BaseCommitSha = reader.GetString(1),
                LastModified = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)),
            };

            // Deserialize embeddings
            var addedJson = reader.GetString(3);
            var modifiedJson = reader.GetString(4);
            var deletedJson = reader.GetString(5);

            DeserializeEmbeddings(addedJson, delta.AddedEmbeddings);
            DeserializeEmbeddings(modifiedJson, delta.ModifiedEmbeddings);

            var deletedIds = JsonSerializer.Deserialize<List<string>>(deletedJson, JsonOptions);
            if (deletedIds != null)
            {
                foreach (var id in deletedIds)
                {
                    delta.DeletedSymbolIds.Add(id);
                }
            }

            _logger.LogDebug(
                "Loaded vector delta from database: {Branch} ({AddedCount} added, {ModifiedCount} modified, {DeletedCount} deleted)",
                delta.BranchName,
                delta.AddedEmbeddings.Count,
                delta.ModifiedEmbeddings.Count,
                delta.DeletedSymbolIds.Count
            );

            return delta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load vector delta: {Branch}", branchName);
            return null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Delete vector delta from database.
    /// </summary>
    public async Task DeleteVectorDeltaAsync(
        string branchName,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureInitializedAsync(cancellationToken);
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "DELETE FROM VectorDeltas WHERE BranchName = @branchName";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@branchName", branchName);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Deleted vector delta from database: {Branch}", branchName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete vector delta: {Branch}", branchName);
            throw;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Get all branch names with stored deltas.
    /// </summary>
    public async Task<List<string>> GetAllBranchNamesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await EnsureInitializedAsync(cancellationToken);
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT BranchName FROM VectorDeltas ORDER BY LastModified DESC";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var branches = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                branches.Add(reader.GetString(0));
            }

            return branches;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // Private helpers

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Streaming serialization of embeddings to reduce memory allocations.
    /// Uses Utf8JsonWriter with ArrayPool-backed buffer.
    /// </summary>
    private static string SerializeEmbeddings(
        System.Collections.Concurrent.ConcurrentDictionary<string, float[]> embeddings
    )
    {
        if (embeddings.IsEmpty)
            return "{}";

        // Use ArrayPool-backed buffer for reduced allocations
        var bufferWriter = new ArrayBufferWriter<byte>(initialCapacity: 4096);
        using (
            var writer = new Utf8JsonWriter(
                bufferWriter,
                new JsonWriterOptions { Indented = false, SkipValidation = false }
            )
        )
        {
            writer.WriteStartObject();

            foreach (var kvp in embeddings)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteStartArray();
                foreach (var value in kvp.Value)
                {
                    writer.WriteNumberValue(value);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Streaming deserialization of embeddings to reduce memory allocations.
    /// Uses Utf8JsonReader with ReadOnlySpan.
    /// </summary>
    private static void DeserializeEmbeddings(
        string json,
        System.Collections.Concurrent.ConcurrentDictionary<string, float[]> target
    )
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(jsonBytes);

        string? currentKey = null;
        var currentValues = new List<float>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    currentKey = reader.GetString();
                    currentValues.Clear();
                    break;

                case JsonTokenType.Number:
                    currentValues.Add(reader.GetSingle());
                    break;

                case JsonTokenType.EndArray:
                    if (currentKey != null && currentValues.Count > 0)
                    {
                        target[currentKey] = currentValues.ToArray();
                    }
                    break;

                default:
                    // Ignore other token types (StartArray, StartObject, EndObject, etc.)
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbLock.Dispose();
        _logger.LogInformation("VectorCacheManager disposed");
        await Task.CompletedTask;
    }
}

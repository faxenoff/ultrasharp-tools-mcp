using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Data.Sqlite;
using UltraSharpTools.VectorDB.Semantic.Models;

namespace UltraSharpTools.VectorDB.Semantic.Backends;

/// <summary>
/// SqliteVec backend - brute-force SIMD search для малых кодовых баз (&lt;10K векторов).
/// Использует Microsoft.Data.Sqlite + кастомный SIMD cosine similarity (из Performance Phase 5).
/// Преимущества: 100% точность, быстрая индексация, простая интеграция.
/// </summary>
public sealed class SqliteVecBackend : IVectorStoreBackend {
    private SqliteConnection? _connection;
    private int _dimension;
    private bool _initialized;

    public VectorStoreBackendType BackendType => VectorStoreBackendType.SqliteVec;

    public async Task InitializeAsync(
        string connectionString,
        int dimension,
        CancellationToken cancellationToken = default
    ) {
        _dimension = dimension;
        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync(cancellationToken);

        // Создать таблицу для embeddings
        var createTableSql =
            @"
CREATE TABLE IF NOT EXISTS doc_embeddings (
id TEXT PRIMARY KEY,
content TEXT NOT NULL,
vector BLOB NOT NULL,
metadata TEXT,
created_at INTEGER NOT NULL,
dimension INTEGER NOT NULL,
provider TEXT
);

CREATE INDEX IF NOT EXISTS idx_embeddings_created ON doc_embeddings(created_at);
CREATE INDEX IF NOT EXISTS idx_embeddings_provider ON doc_embeddings(provider);
";

        using var command = _connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
    }

    public async Task InsertAsync(
        VectorEmbedding embedding,
        CancellationToken cancellationToken = default
    ) {
        ThrowIfNotInitialized();

        using var command = _connection!.CreateCommand();
        command.CommandText =
            @"
INSERT OR REPLACE INTO doc_embeddings (id, content, vector, metadata, created_at, dimension, provider)
VALUES (@id, @content, @vector, @metadata, @created_at, @dimension, @provider)
";

        command.Parameters.AddWithValue("@id", embedding.Id);
        command.Parameters.AddWithValue("@content", embedding.Content);
        command.Parameters.AddWithValue("@vector", SerializeVector(embedding.Vector));
        command.Parameters.AddWithValue("@metadata", embedding.Metadata ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@created_at", embedding.CreatedAt);
        command.Parameters.AddWithValue("@dimension", embedding.Dimension);
        command.Parameters.AddWithValue("@provider", embedding.Provider ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertBatchAsync(
        IEnumerable<VectorEmbedding> embeddings,
        CancellationToken cancellationToken = default
    ) {
        ThrowIfNotInitialized();

        using var transaction = _connection!.BeginTransaction();
        try {
            foreach (var embedding in embeddings) {
                using var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    @"
INSERT OR REPLACE INTO doc_embeddings (id, content, vector, metadata, created_at, dimension, provider)
VALUES (@id, @content, @vector, @metadata, @created_at, @dimension, @provider)
";

                command.Parameters.AddWithValue("@id", embedding.Id);
                command.Parameters.AddWithValue("@content", embedding.Content);
                command.Parameters.AddWithValue("@vector", SerializeVector(embedding.Vector));
                command.Parameters.AddWithValue(
                    "@metadata",
                    embedding.Metadata ?? (object)DBNull.Value
                );
                command.Parameters.AddWithValue("@created_at", embedding.CreatedAt);
                command.Parameters.AddWithValue("@dimension", embedding.Dimension);
                command.Parameters.AddWithValue(
                    "@provider",
                    embedding.Provider ?? (object)DBNull.Value
                );

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        } catch {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    public async Task<List<SimilarityResult>> SearchAsync(
        float[] queryVector,
        int limit,
        float minSimilarity = 0.0f,
        CancellationToken cancellationToken = default
    ) {
        ThrowIfNotInitialized();

        // Шаг 1: Загрузить все данные из БД в память (I/O bound)
        var documents = new List<(string Id, string Content, byte[] VectorBlob, string? Metadata)>();

        using var command = _connection!.CreateCommand();
        command.CommandText =
            "SELECT id, content, vector, metadata FROM doc_embeddings WHERE dimension = @dimension";
        command.Parameters.AddWithValue("@dimension", _dimension);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) {
            documents.Add((
                reader.GetString(0),
                reader.GetString(1),
                (byte[])reader.GetValue(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }

        if (documents.Count == 0) {
            return [];
        }

        // Шаг 2: Параллельное вычисление similarity (CPU bound - отлично параллелится)
        var results = new ConcurrentBag<SimilarityResult>();
        var parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(documents, parallelOptions, (doc, ct) => {
            var storedVector = DeserializeVector(doc.VectorBlob);
            var similarity = CalculateCosineSimilarity(queryVector, storedVector);

            if (similarity >= minSimilarity) {
                results.Add(new SimilarityResult {
                    Id = doc.Id,
                    Content = doc.Content,
                    Similarity = similarity,
                    Metadata = doc.Metadata,
                    Rank = 0
                });
            }

            return ValueTask.CompletedTask;
        });

        // Шаг 3: Сортировка и установка ranks
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(limit)
            .Select((r, index) => r with { Rank = index + 1 })
            .ToList();
    }
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default) {
        ThrowIfNotInitialized();

        using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM doc_embeddings";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default) {
        ThrowIfNotInitialized();

        using var command = _connection!.CreateCommand();
        command.CommandText = "DELETE FROM doc_embeddings WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default) {
        ThrowIfNotInitialized();

        using var command = _connection!.CreateCommand();
        command.CommandText = "DELETE FROM doc_embeddings";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default) {
        return Task.FromResult(_connection?.State == System.Data.ConnectionState.Open);
    }

    public async ValueTask DisposeAsync() {
        if (_connection != null) {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    // Helper methods

    private void ThrowIfNotInitialized() {
        if (!_initialized || _connection == null) {
            throw new InvalidOperationException(
                "Backend not initialized. Call InitializeAsync first."
            );
        }
    }

    private static byte[] SerializeVector(float[] vector) {
        // Сериализация как float32 array (4 bytes per float)
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeVector(byte[] bytes) {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    /// <summary>
    /// SIMD-оптимизированный cosine similarity (из Performance Phase 5).
    /// Использует System.Numerics.Vector для AVX2 инструкций (8 float за раз).
    /// </summary>
    private static float CalculateCosineSimilarity(
        ReadOnlySpan<float> vec1,
        ReadOnlySpan<float> vec2
    ) {
        if (vec1.Length != vec2.Length) {
            throw new ArgumentException("Vectors must have the same dimension");
        }

        int length = vec1.Length;
        int vectorSize = Vector<float>.Count; // 8 на AVX2

        float dotProduct = 0f;
        float magnitude1 = 0f;
        float magnitude2 = 0f;

        // SIMD processing
        int i = 0;
        for (; i <= length - vectorSize; i += vectorSize) {
            var v1 = new Vector<float>(vec1.Slice(i, vectorSize));
            var v2 = new Vector<float>(vec2.Slice(i, vectorSize));

            dotProduct += Vector.Dot(v1, v2);
            magnitude1 += Vector.Dot(v1, v1);
            magnitude2 += Vector.Dot(v2, v2);
        }

        // Scalar remainder
        for (; i < length; i++) {
            dotProduct += vec1[i] * vec2[i];
            magnitude1 += vec1[i] * vec1[i];
            magnitude2 += vec2[i] * vec2[i];
        }

        // Cosine similarity = dot(A, B) / (||A|| * ||B||)
        var denominator = MathF.Sqrt(magnitude1) * MathF.Sqrt(magnitude2);
        return denominator > 0 ? dotProduct / denominator : 0f;
    }
}

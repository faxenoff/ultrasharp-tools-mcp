using System.IO.Hashing;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic.Embedding.Providers;

/// <summary>
/// Memory provider (fallback)
/// Uses deterministic hash instead of ML embeddings
/// No external dependencies, always available
/// </summary>
public sealed class MemoryProvider : IEmbeddingProvider
{
    private readonly MemoryOptions _options;
    private readonly ILogger<MemoryProvider> _logger;
    private const int EmbeddingDimension = 384; // Match granite-embedding:30m

    public string Name => "memory";
    public int MaxContextTokens => 0; // No ML, unlimited "context"
    public int? Dimension => EmbeddingDimension;

    public ProviderInfo Info =>
        new()
        {
            Name = "memory",
            Model = _options.UseDeterministicHash ? "sha256-hash" : "random-seeded",
            Dimension = EmbeddingDimension,
            MaxTokens = 0,
            IsLocal = true,
            Version = "1.0",
        };

    public MemoryProvider(MemoryOptions options, ILogger<MemoryProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Memory] Initialized (deterministic hash, no ML embeddings)");
        return Task.CompletedTask;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_options.UseDeterministicHash)
        {
            return Task.FromResult(GenerateDeterministicEmbedding(text));
        }
        else
        {
            return Task.FromResult(GenerateRandomEmbedding(text));
        }
    }

    public Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        var embeddings = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
        {
            embeddings[i] = _options.UseDeterministicHash
                ? GenerateDeterministicEmbedding(texts[i])
                : GenerateRandomEmbedding(texts[i]);
        }
        return Task.FromResult(embeddings);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true); // Always available
    }

    /// <summary>
    /// Generate deterministic embedding from text hash
    /// Same text always produces same embedding
    /// </summary>
    private float[] GenerateDeterministicEmbedding(string text)
    {
        var embedding = new float[EmbeddingDimension];

        // Use xxHash128 to generate deterministic bytes (7x faster than SHA256)
        // Не требует криптостойкости, только детерминированность
        var textBytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = XxHash128.Hash(textBytes);

        // Expand hash to fill embedding dimension
        for (int i = 0; i < EmbeddingDimension; i++)
        {
            // Use hash bytes cyclically
            var byteIndex = i % hashBytes.Length;
            var value = hashBytes[byteIndex];

            // Map 0-255 to -1.0 to 1.0
            embedding[i] = (value / 128.0f) - 1.0f;
        }

        // Normalize to unit vector (cosine similarity compatible)
        Normalize(embedding);

        return embedding;
    }

    /// <summary>
    /// Generate pseudo-random embedding seeded by text hash
    /// </summary>
    private float[] GenerateRandomEmbedding(string text)
    {
        var embedding = new float[EmbeddingDimension];

        // Seed RNG with text hash for determinism
        var seed = text.GetHashCode();
        var rng = new Random(seed);

        for (int i = 0; i < EmbeddingDimension; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        Normalize(embedding);

        return embedding;
    }

    private void Normalize(float[] vector)
    {
        double sum = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            sum += vector[i] * vector[i];
        }

        var magnitude = Math.Sqrt(sum);
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= (float)magnitude;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        // Nothing to dispose
        return ValueTask.CompletedTask;
    }
}

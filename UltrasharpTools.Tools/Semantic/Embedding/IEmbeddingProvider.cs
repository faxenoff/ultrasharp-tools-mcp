using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic.Embedding;

/// <summary>
/// Interface for embedding providers (TEI, Ollama, Memory, etc.)
/// </summary>
public interface IEmbeddingProvider : IAsyncDisposable
{
    /// <summary>
    /// Provider name (e.g., "tei", "ollama", "memory")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Maximum context tokens supported
    /// </summary>
    int MaxContextTokens { get; }

    /// <summary>
    /// Embedding dimension
    /// </summary>
    int? Dimension { get; }

    /// <summary>
    /// Provider metadata information
    /// </summary>
    ProviderInfo Info { get; }

    /// <summary>
    /// Initialize the provider
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embedding for text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Float array of embedding vector</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts (batch)
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of embedding vectors</returns>
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if provider is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

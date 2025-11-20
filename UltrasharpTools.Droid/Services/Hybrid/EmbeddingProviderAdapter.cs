using UltrasharpTools.Tools.Semantic.Embedding;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Adapter to use IEmbeddingProvider (from Tools) as IEmbeddingService (for Droid Hybrid mode)
/// </summary>
public sealed class EmbeddingProviderAdapter : IEmbeddingService
{
    private readonly IEmbeddingProvider _provider;

    public EmbeddingProviderAdapter(IEmbeddingProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _provider.EmbedAsync(text, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Пробуем получить embedding для простого теста
            var result = await _provider.EmbedAsync("test", cancellationToken);
            return result != null && result.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}

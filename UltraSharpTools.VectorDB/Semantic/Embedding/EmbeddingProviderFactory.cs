using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using UltraSharpTools.VectorDB.Semantic.Embedding.Providers;
using UltraSharpTools.VectorDB.Semantic.GPU;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB.Semantic.Embedding;

/// <summary>
/// Factory for creating embedding providers with auto-selection
/// </summary>
public sealed class EmbeddingProviderFactory
{
    private readonly EmbeddingOptions _options;
    private readonly ILogger<EmbeddingProviderFactory> _logger;
    private readonly IGPUDetectionService _gpuDetection;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public EmbeddingProviderFactory(
        IOptions<EmbeddingOptions> options,
        ILogger<EmbeddingProviderFactory> logger,
        IGPUDetectionService gpuDetection,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory
    )
    {
        _options = options.Value;
        _logger = logger;
        _gpuDetection = gpuDetection;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create embedding provider based on configuration and GPU capabilities
    /// </summary>
    public async Task<IEmbeddingProvider> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[EmbeddingFactory] Embeddings disabled, using Memory provider");
            return CreateMemoryProvider();
        }

        var providerType = _options.Provider.ToLowerInvariant();

        // Auto-detect mode: simplified for Indexer (no GPU detection)
        if (providerType == "auto" && _options.AutoDetectGPU)
        {
            _logger.LogInformation("[EmbeddingFactory] Auto-detect disabled in Indexer, using default");
            // Indexer always uses configured provider, no GPU auto-detection
            providerType = "ollama"; // Default fallback
            _logger.LogInformation("[EmbeddingFactory] Using fallback provider: {Provider}", providerType);
        }

        // Create provider
        var provider = providerType switch
        {
            "tei" => CreateTEIProvider(),
            "ollama" => CreateOllamaProvider(),
            "memory" => CreateMemoryProvider(),
            _ => throw new InvalidOperationException($"Unknown embedding provider: {providerType}"),
        };

        // Initialize provider
        try
        {
            await provider.InitializeAsync(cancellationToken);
            _logger.LogInformation(
                "[EmbeddingFactory] Provider initialized: {Name} ({Tokens} tokens, dimension: {Dim})",
                provider.Name,
                provider.MaxContextTokens,
                provider.Dimension
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[EmbeddingFactory] Provider initialization failed: {Provider}",
                providerType
            );

            // Fallback to memory provider
            if (providerType != "memory")
            {
                _logger.LogWarning("[EmbeddingFactory] Falling back to Memory provider");
                var fallbackProvider = CreateMemoryProvider();
                await fallbackProvider.InitializeAsync(cancellationToken);
                return fallbackProvider;
            }

            throw;
        }

        return provider;
    }

    private IEmbeddingProvider CreateTEIProvider()
    {
        return new TEIProvider(
            _options.TEI,
            _loggerFactory.CreateLogger<TEIProvider>(),
            _httpClientFactory
        );
    }

    private IEmbeddingProvider CreateOllamaProvider()
    {
        return new OllamaProvider(
            _options.Ollama,
            _loggerFactory.CreateLogger<OllamaProvider>(),
            _httpClientFactory
        );
    }

    private IEmbeddingProvider CreateMemoryProvider()
    {
        return new MemoryProvider(_options.Memory, _loggerFactory.CreateLogger<MemoryProvider>());
    }
}

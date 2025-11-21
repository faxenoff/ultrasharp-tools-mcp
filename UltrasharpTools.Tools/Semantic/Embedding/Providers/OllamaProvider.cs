using System.Net.Http.Json;
using Microsoft.Extensions.Http;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic.Embedding.Providers;

/// <summary>
/// Ollama provider
/// Supports 512 tokens context with granite-embedding model
/// Lightweight alternative to TEI
/// </summary>
public sealed class OllamaProvider : IEmbeddingProvider
{
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private int? _dimension;

    public string Name => "ollama";
    public int MaxContextTokens => 512;
    public int? Dimension => _dimension;

    public ProviderInfo Info =>
        new()
        {
            Name = "ollama",
            Model = _options.Model,
            Dimension = _dimension ?? 0,
            MaxTokens = 512,
            IsLocal = true,
            Version = "1.0",
        };

    public OllamaProvider(
        OllamaOptions options,
        ILogger<OllamaProvider> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _options = options;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);
        _concurrencyLimiter = new SemaphoreSlim(_options.Concurrency, _options.Concurrency);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Ollama] Initializing with model: {Model}", _options.Model);

        if (_options.CheckServer)
        {
            var available = await CheckServerAsync(cancellationToken);
            if (!available)
            {
                throw new InvalidOperationException(
                    $"Ollama server is not available at {_options.BaseUrl}"
                );
            }
        }

        // Check if model exists
        var modelExists = await CheckModelExistsAsync(cancellationToken);
        if (!modelExists)
        {
            if (_options.AutoPull)
            {
                _logger.LogInformation(
                    "[Ollama] Model not found, pulling: {Model}",
                    _options.Model
                );
                await PullModelAsync(cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Ollama model not found: {_options.Model}");
            }
        }

        // Warmup
        try
        {
            var testEmbedding = await EmbedAsync("test", cancellationToken);
            _dimension = testEmbedding.Length;
            _logger.LogInformation(
                "[Ollama] Initialized successfully (dimension: {Dim})",
                _dimension
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ollama] Initialization failed");
            throw;
        }
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var request = new OllamaRequest { Model = _options.Model, Prompt = text };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/embeddings",
                request,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
                cancellationToken
            );
            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Ollama returned empty embedding");
            }

            return result.Embedding;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Ollama] HTTP request failed");
            throw new InvalidOperationException($"Ollama request failed: {ex.Message}", ex);
        }
    }

    public async Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        // Ollama doesn't support native batch - process with concurrency limit
        var tasks = texts.Select(async text =>
        {
            await _concurrencyLimiter.WaitAsync(cancellationToken);
            try
            {
                return await EmbedAsync(text, cancellationToken);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await CheckServerAsync(cancellationToken);
    }

    private async Task<bool> CheckServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/version", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback: try /api/tags
                response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Ollama] Server check failed");
            return false;
        }
    }

    private async Task<bool> CheckModelExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                cancellationToken
            );
            return result?.Models?.Any(m => m.Name == _options.Model) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Ollama] Failed to check model existence");
            return false;
        }
    }

    private async Task PullModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new OllamaPullRequest { Name = _options.Model };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 min timeout for pull

            var response = await _httpClient.PostAsJsonAsync("/api/pull", request, cts.Token);
            response.EnsureSuccessStatusCode();

            // Read streaming response
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new System.IO.StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cts.Token)) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logger.LogDebug("[Ollama] Pull: {Line}", line);
                }
            }

            _logger.LogInformation("[Ollama] Model pulled successfully: {Model}", _options.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ollama] Failed to pull model");
            throw new InvalidOperationException(
                $"Failed to pull Ollama model: {_options.Model}",
                ex
            );
        }
    }

    private record OllamaRequest
    {
        public string Model { get; init; } = string.Empty;
        public string Prompt { get; init; } = string.Empty;
    }

    private record OllamaResponse
    {
        public float[] Embedding { get; init; } = Array.Empty<float>();
    }

    private record OllamaTagsResponse
    {
        public OllamaModel[]? Models { get; init; }
    }

    private record OllamaModel
    {
        public string Name { get; init; } = string.Empty;
    }

    private record OllamaPullRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public ValueTask DisposeAsync()
    {
        _concurrencyLimiter?.Dispose();
        _httpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}

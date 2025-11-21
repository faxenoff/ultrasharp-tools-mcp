using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация embedding сервиса через Ollama или TEI
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _embeddingUrl;
    private readonly string _embeddingModel;
    private readonly JsonSerializerOptions _jsonOptions;

    public EmbeddingService(
        HttpClient httpClient,
        ILogger<EmbeddingService> logger,
        string embeddingUrl,
        string embeddingModel = "nomic-embed-text"
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _embeddingUrl = embeddingUrl;
        _embeddingModel = embeddingModel;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    public async Task<float[]?> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            // Определяем тип сервиса по URL (простая эвристика)
            var isOllama = _embeddingUrl.Contains("11434") || _embeddingUrl.Contains("ollama");

            if (isOllama)
            {
                return await GetEmbeddingFromOllamaAsync(text, cancellationToken);
            }
            else
            {
                return await GetEmbeddingFromTeiAsync(text, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get embedding for text (length: {Length})",
                text.Length
            );
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isOllama = _embeddingUrl.Contains("11434") || _embeddingUrl.Contains("ollama");
            var healthUrl = isOllama ? $"{_embeddingUrl}/api/tags" : $"{_embeddingUrl}/health";

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            var isAvailable = response.IsSuccessStatusCode;

            _logger.LogDebug("Embedding service availability: {IsAvailable}", isAvailable);

            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding service health check failed");
            return false;
        }
    }

    private async Task<float[]?> GetEmbeddingFromOllamaAsync(
        string text,
        CancellationToken cancellationToken
    )
    {
        var url = $"{_embeddingUrl}/api/embeddings";

        var request = new { model = _embeddingModel, prompt = text };

        var response = await _httpClient.PostAsJsonAsync(
            url,
            request,
            _jsonOptions,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            _jsonOptions,
            cancellationToken
        );

        if (result?.Embedding == null || result.Embedding.Length == 0)
        {
            _logger.LogWarning("Ollama returned empty embedding");
            return null;
        }

        _logger.LogDebug(
            "Generated embedding via Ollama (model: {Model}): {Dimensions} dimensions",
            _embeddingModel,
            result.Embedding.Length
        );

        return result.Embedding;
    }

    private async Task<float[]?> GetEmbeddingFromTeiAsync(
        string text,
        CancellationToken cancellationToken
    )
    {
        var url = $"{_embeddingUrl}/embed";

        var request = new { inputs = text };

        var response = await _httpClient.PostAsJsonAsync(
            url,
            request,
            _jsonOptions,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<float[][]>(
            _jsonOptions,
            cancellationToken
        );

        if (result == null || result.Length == 0 || result[0].Length == 0)
        {
            _logger.LogWarning("TEI returned empty embedding");
            return null;
        }

        _logger.LogDebug("Generated embedding via TEI: {Dimensions} dimensions", result[0].Length);

        return result[0];
    }

    // DTOs
    private sealed class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация embedding сервиса через Ollama или TEI
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly AgentConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public EmbeddingService(
        HttpClient httpClient,
        ILogger<EmbeddingService> logger,
        AgentConfig config
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
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
            // Определяем тип сервиса по URL
            var isOllama = _config.EmbeddingUrl.Contains("11434");

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
            _logger.LogError(ex, "Failed to get embedding");
            return null;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isOllama = _config.EmbeddingUrl.Contains("11434");
            var healthUrl = isOllama
                ? $"{_config.EmbeddingUrl}/api/tags"
                : $"{_config.EmbeddingUrl}/health";

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<float[]?> GetEmbeddingFromOllamaAsync(
        string text,
        CancellationToken cancellationToken
    )
    {
        var url = $"{_config.EmbeddingUrl}/api/embeddings";

        var request = new { model = _config.EmbeddingModel, prompt = text };

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
            "Generated embedding via Ollama: {Dimensions} dimensions",
            result.Embedding.Length
        );

        return result.Embedding;
    }

    private async Task<float[]?> GetEmbeddingFromTeiAsync(
        string text,
        CancellationToken cancellationToken
    )
    {
        var url = $"{_config.EmbeddingUrl}/embed";

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

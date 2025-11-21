using System.Net.Http;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Checks health of embedding services (TEI, Ollama)
/// </summary>
public class EmbeddingServiceHealthChecker
{
    private readonly ILogger<EmbeddingServiceHealthChecker> _logger;
    private readonly HttpClient _httpClient;

    public EmbeddingServiceHealthChecker(
        ILogger<EmbeddingServiceHealthChecker> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public class HealthResult
    {
        public bool IsHealthy { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
        public TimeSpan ResponseTime { get; set; }
    }

    /// <summary>
    /// Check TEI health endpoint
    /// </summary>
    public async Task<HealthResult> CheckTeiHealthAsync(
        string endpoint,
        CancellationToken cancellationToken = default
    )
    {
        var startTime = DateTime.UtcNow;
        var result = new HealthResult();

        try
        {
            var healthUrl = $"{endpoint.TrimEnd('/')}/health";
            _logger.LogDebug("Checking TEI health: {Url}", healthUrl);

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            result.ResponseTime = DateTime.UtcNow - startTime;

            if (response.IsSuccessStatusCode)
            {
                result.IsHealthy = true;
                result.Details = $"TEI is available ({result.ResponseTime.TotalMilliseconds:F0}ms)";
                _logger.LogInformation("✓ TEI is healthy at {Endpoint}", endpoint);
            }
            else
            {
                result.IsHealthy = false;
                result.ErrorMessage = $"TEI returned status {response.StatusCode}";
                result.Details = $"Endpoint: {endpoint}";
                _logger.LogWarning("✗ TEI health check failed: {Status}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Cannot connect to TEI server";
            result.Details = $"Endpoint: {endpoint}\nError: {ex.Message}";
            _logger.LogWarning("✗ TEI connection failed: {Error}", ex.Message);
        }
        catch (TaskCanceledException)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "TEI health check timed out";
            result.Details = $"Endpoint: {endpoint}\nTimeout: 5 seconds";
            _logger.LogWarning("✗ TEI health check timed out");
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Unexpected error checking TEI";
            result.Details = ex.Message;
            _logger.LogError(ex, "✗ Unexpected error checking TEI health");
        }

        return result;
    }

    /// <summary>
    /// Check Ollama availability
    /// </summary>
    public async Task<HealthResult> CheckOllamaHealthAsync(
        string endpoint,
        CancellationToken cancellationToken = default
    )
    {
        var startTime = DateTime.UtcNow;
        var result = new HealthResult();

        try
        {
            var tagsUrl = $"{endpoint.TrimEnd('/')}/api/tags";
            _logger.LogDebug("Checking Ollama health: {Url}", tagsUrl);

            var response = await _httpClient.GetAsync(tagsUrl, cancellationToken);
            result.ResponseTime = DateTime.UtcNow - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);

                var models = doc.RootElement.GetProperty("models");
                var modelCount = models.GetArrayLength();

                result.IsHealthy = true;
                result.Details =
                    $"Ollama is available with {modelCount} model(s) ({result.ResponseTime.TotalMilliseconds:F0}ms)";
                _logger.LogInformation(
                    "✓ Ollama is healthy at {Endpoint} ({Count} models)",
                    endpoint,
                    modelCount
                );
            }
            else
            {
                result.IsHealthy = false;
                result.ErrorMessage = $"Ollama returned status {response.StatusCode}";
                result.Details = $"Endpoint: {endpoint}";
                _logger.LogWarning("✗ Ollama health check failed: {Status}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Cannot connect to Ollama server";
            result.Details =
                $"Endpoint: {endpoint}\nError: {ex.Message}\n\nIs Ollama installed? Visit: https://ollama.ai";
            _logger.LogWarning("✗ Ollama connection failed: {Error}", ex.Message);
        }
        catch (TaskCanceledException)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Ollama health check timed out";
            result.Details = $"Endpoint: {endpoint}\nTimeout: 5 seconds";
            _logger.LogWarning("✗ Ollama health check timed out");
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Unexpected error checking Ollama";
            result.Details = ex.Message;
            _logger.LogError(ex, "✗ Unexpected error checking Ollama health");
        }

        return result;
    }

    /// <summary>
    /// Check if a specific Ollama model is available
    /// </summary>
    public async Task<bool> CheckOllamaModelAsync(
        string endpoint,
        string modelName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var tagsUrl = $"{endpoint.TrimEnd('/')}/api/tags";
            var response = await _httpClient.GetAsync(tagsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            var models = doc.RootElement.GetProperty("models");
            foreach (var model in models.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name) && name.GetString() == modelName)
                {
                    _logger.LogDebug("✓ Ollama model found: {Model}", modelName);
                    return true;
                }
            }

            _logger.LogWarning("✗ Ollama model not found: {Model}", modelName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Ollama model: {Model}", modelName);
            return false;
        }
    }
}

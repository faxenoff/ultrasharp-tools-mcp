using System.Net.Http;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Checks health of embedding services (TEI, Ollama)
/// </summary>
public partial class EmbeddingServiceHealthChecker
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
            LogCheckingTeiHealth(healthUrl);

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            result.ResponseTime = DateTime.UtcNow - startTime;

            if (response.IsSuccessStatusCode)
            {
                result.IsHealthy = true;
                result.Details = $"TEI is available ({result.ResponseTime.TotalMilliseconds:F0}ms)";
                LogTeiHealthy(endpoint);
            }
            else
            {
                result.IsHealthy = false;
                result.ErrorMessage = $"TEI returned status {response.StatusCode}";
                result.Details = $"Endpoint: {endpoint}";
                LogTeiHealthFailed(response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Cannot connect to TEI server";
            result.Details = $"Endpoint: {endpoint}\nError: {ex.Message}";
            LogTeiConnectionFailed(ex.Message);
        }
        catch (TaskCanceledException)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "TEI health check timed out";
            result.Details = $"Endpoint: {endpoint}\nTimeout: 5 seconds";
            LogTeiTimeout();
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Unexpected error checking TEI";
            result.Details = ex.Message;
            LogTeiUnexpectedError(ex);
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
            LogCheckingOllamaHealth(tagsUrl);

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
                LogOllamaHealthy(endpoint, modelCount);
            }
            else
            {
                result.IsHealthy = false;
                result.ErrorMessage = $"Ollama returned status {response.StatusCode}";
                result.Details = $"Endpoint: {endpoint}";
                LogOllamaHealthFailed(response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Cannot connect to Ollama server";
            result.Details =
                $"Endpoint: {endpoint}\nError: {ex.Message}\n\nIs Ollama installed? Visit: https://ollama.ai";
            LogOllamaConnectionFailed(ex.Message);
        }
        catch (TaskCanceledException)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Ollama health check timed out";
            result.Details = $"Endpoint: {endpoint}\nTimeout: 5 seconds";
            LogOllamaTimeout();
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = "Unexpected error checking Ollama";
            result.Details = ex.Message;
            LogOllamaUnexpectedError(ex);
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
                    LogOllamaModelFound(modelName);
                    return true;
                }
            }

            LogOllamaModelNotFound(modelName);
            return false;
        }
        catch (Exception ex)
        {
            LogOllamaModelCheckFailed(ex, modelName);
            return false;
        }
    }
}

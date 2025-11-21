using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Простая проверка semantic mode availability при bootstrap (без DI)
/// </summary>
public static class SemanticModeBootstrapCheck
{
    /// <summary>
    /// Быстрая проверка доступности semantic mode для MCP Initialize
    /// </summary>
    public static async Task<SemanticModeAvailability> CheckAvailabilityAsync(
        string? embeddingUrl = null,
        string? overlordUrl = null,
        int timeoutMs = 3000,
        CancellationToken ct = default
    )
    {
        var hasLocal = await CheckLocalAsync(embeddingUrl, timeoutMs, ct);
        var hasOverlord = await CheckOverlordAsync(overlordUrl, timeoutMs, ct);

        SemanticModeSource source;
        if (hasLocal && hasOverlord)
            source = SemanticModeSource.Both;
        else if (hasLocal)
            source = SemanticModeSource.Local;
        else if (hasOverlord)
            source = SemanticModeSource.Overlord;
        else
            source = SemanticModeSource.None;

        return new SemanticModeAvailability
        {
            IsAvailable = source != SemanticModeSource.None,
            Source = source,
            ModelName = source != SemanticModeSource.None ? "nomic-embed-text" : null,
            VectorDimension = source != SemanticModeSource.None ? 768 : 0,
            LocalEmbeddingUrl = hasLocal ? embeddingUrl : null,
            OverlordUrl = hasOverlord ? overlordUrl : null,
        };
    }

    private static async Task<bool> CheckLocalAsync(
        string? url,
        int timeoutMs,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(url))
            return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            };
            var response = await httpClient.GetAsync($"{url}/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckOverlordAsync(
        string? url,
        int timeoutMs,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(url))
            return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            };
            var response = await httpClient.GetAsync($"{url}/api/server/status", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

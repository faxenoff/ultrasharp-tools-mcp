using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация провайдера семантического режима с автоматическим определением доступности
/// </summary>
public sealed class SemanticModeProvider : ISemanticModeProvider
{
    private readonly ILogger<SemanticModeProvider> _logger;
    private readonly IEmbeddingService? _localEmbedding;
    private readonly IServerBridgeService? _serverBridge;
    private readonly string? _overlordUrl;
    private readonly SemanticModeConfig _config;

    private SemanticModeAvailability? _cachedAvailability;
    private DateTime _lastCheck = DateTime.MinValue;

    public SemanticModeProvider(
        ILogger<SemanticModeProvider> logger,
        IEmbeddingService? localEmbedding = null,
        IServerBridgeService? serverBridge = null,
        string? overlordUrl = null,
        SemanticModeConfig? config = null)
    {
        _logger = logger;
        _localEmbedding = localEmbedding;
        _serverBridge = serverBridge;
        _overlordUrl = overlordUrl;
        _config = config ?? SemanticModeConfig.CreateDefault();

        _logger.LogDebug(
            "SemanticModeProvider initialized: enabled={Enabled}, cacheValidity={CacheSeconds}s, localTimeout={LocalTimeout}s, overlordTimeout={OverlordTimeout}s",
            _config.Enabled,
            _config.Availability.CacheValiditySeconds,
            _config.Availability.LocalCheckTimeoutSeconds,
            _config.Availability.OverlordCheckTimeoutSeconds);
    }

    /// <inheritdoc/>
    public async Task<SemanticModeAvailability> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        // Проверяем, включён ли semantic mode
        if (!_config.Enabled)
        {
            _logger.LogDebug("Semantic mode is DISABLED by configuration");
            return new SemanticModeAvailability
            {
                IsAvailable = false,
                Source = SemanticModeSource.None,
                ModelName = null,
                VectorDimension = 0,
                LocalEmbeddingUrl = null,
                OverlordUrl = null
            };
        }

        // Проверяем кэш
        var cacheValidity = TimeSpan.FromSeconds(_config.Availability.CacheValiditySeconds);
        if (_cachedAvailability != null && DateTime.UtcNow - _lastCheck < cacheValidity)
        {
            _logger.LogTrace("Returning cached semantic mode availability: {Source}", _cachedAvailability.Source);
            return _cachedAvailability;
        }

        _logger.LogDebug("Checking semantic mode availability...");

        var hasLocal = await CheckLocalAvailabilityAsync(ct);
        var hasOverlord = await CheckOverlordAvailabilityAsync(ct);

        SemanticModeSource source;
        if (hasLocal && hasOverlord)
        {
            source = SemanticModeSource.Both;
            _logger.LogInformation("Semantic mode available from BOTH sources (Local + Overlord)");
        }
        else if (hasLocal)
        {
            source = SemanticModeSource.Local;
            _logger.LogInformation("Semantic mode available from LOCAL embedding service");
        }
        else if (hasOverlord)
        {
            source = SemanticModeSource.Overlord;
            _logger.LogInformation("Semantic mode available from OVERLORD");
        }
        else
        {
            source = SemanticModeSource.None;
            _logger.LogWarning("Semantic mode is NOT available - no embedding services detected");
        }

        _cachedAvailability = new SemanticModeAvailability
        {
            IsAvailable = source != SemanticModeSource.None,
            Source = source,
            ModelName = await GetModelNameAsync(source, ct),
            VectorDimension = await GetVectorDimensionAsync(source, ct),
            LocalEmbeddingUrl = hasLocal ? GetLocalEmbeddingUrl() : null,
            OverlordUrl = hasOverlord ? _overlordUrl : null
        };

        _lastCheck = DateTime.UtcNow;
        return _cachedAvailability;
    }

    /// <inheritdoc/>
    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot get embedding for empty text");
            return null;
        }

        var availability = await CheckAvailabilityAsync(ct);
        if (!availability.IsAvailable)
        {
            _logger.LogWarning("Semantic mode not available - cannot generate embedding");
            return null;
        }

        try
        {
            // Prefer Local для меньшей latency
            if (availability.Source == SemanticModeSource.Local || availability.Source == SemanticModeSource.Both)
            {
                if (_localEmbedding != null)
                {
                    _logger.LogTrace("Getting embedding from LOCAL service");
                    return await _localEmbedding.GetEmbeddingAsync(text, ct);
                }
            }

            // Fallback на Overlord
            if (availability.Source == SemanticModeSource.Overlord || availability.Source == SemanticModeSource.Both)
            {
                if (_serverBridge != null)
                {
                    _logger.LogTrace("Getting embedding from OVERLORD");
                    // Вызываем MCP proxy tool на Overlord
                    var result = await _serverBridge.CallMcpProxyAsync(
                        "get_embedding",
                        new Dictionary<string, object> { ["text"] = text },
                        null,
                        ct);

                    if (result is float[] vector)
                    {
                        return vector;
                    }

                    _logger.LogWarning("Unexpected result type from Overlord embedding: {Type}", result?.GetType());
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedding for text (length: {Length})", text.Length);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SemanticMatch>> SearchAsync(
        float[] queryVector,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default)
    {
        if (queryVector == null || queryVector.Length == 0)
        {
            _logger.LogWarning("Cannot search with empty query vector");
            return Array.Empty<SemanticMatch>();
        }

        var availability = await CheckAvailabilityAsync(ct);
        if (!availability.IsAvailable)
        {
            _logger.LogWarning("Semantic mode not available - cannot perform search");
            return Array.Empty<SemanticMatch>();
        }

        try
        {
            // Prefer Overlord для cross-project search
            if (availability.Source == SemanticModeSource.Overlord || availability.Source == SemanticModeSource.Both)
            {
                if (_serverBridge != null)
                {
                    _logger.LogTrace("Performing semantic search via OVERLORD (topK: {TopK}, threshold: {Threshold})", topK, threshold);
                    var result = await _serverBridge.CallMcpProxyAsync(
                        "semantic_search_by_vector",
                        new Dictionary<string, object>
                        {
                            ["queryVector"] = queryVector,
                            ["topK"] = topK,
                            ["threshold"] = threshold
                        },
                        null,
                        ct);

                    if (result is IEnumerable<SemanticMatch> matches)
                    {
                        return matches;
                    }

                    _logger.LogWarning("Unexpected result type from Overlord search: {Type}", result?.GetType());
                }
            }

            // Fallback на Local (если доступен)
            if (availability.Source == SemanticModeSource.Local)
            {
                _logger.LogDebug("Local-only semantic search not implemented yet - returning empty results");
                // TODO: Implement local vector store search
            }

            return Array.Empty<SemanticMatch>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic search failed (vector dim: {Dim}, topK: {TopK})", queryVector.Length, topK);
            return Array.Empty<SemanticMatch>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SemanticMatch>> SearchByTextAsync(
        string query,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Cannot search with empty query text");
            return Array.Empty<SemanticMatch>();
        }

        _logger.LogDebug("Converting query to vector: {Query}", query.Length > 50 ? query[..50] + "..." : query);

        // Получаем вектор для запроса
        var queryVector = await GetEmbeddingAsync(query, ct);
        if (queryVector == null)
        {
            _logger.LogWarning("Failed to vectorize query - cannot perform search");
            return Array.Empty<SemanticMatch>();
        }

        // Выполняем поиск по вектору
        return await SearchAsync(queryVector, topK, threshold, ct);
    }

    // === Private Helper Methods ===

    private async Task<bool> CheckLocalAvailabilityAsync(CancellationToken ct)
    {
        if (_localEmbedding == null)
        {
            _logger.LogTrace("Local embedding service not registered");
            return false;
        }

        // Retry logic: 3 attempts with exponential backoff (1s, 2s, 4s)
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Проверяем доступность через тестовый запрос с timeout из конфига
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_config.Availability.LocalCheckTimeoutSeconds));

                var testVector = await _localEmbedding.GetEmbeddingAsync("test", cts.Token);
                var isAvailable = testVector != null && testVector.Length > 0;

                if (isAvailable)
                {
                    _logger.LogTrace("Local embedding available (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                    return true;
                }

                // Not available but no exception - don't retry
                _logger.LogTrace("Local embedding not available (no exception, attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                return false;
            }
            catch (OperationCanceledException)
            {
                if (attempt < maxRetries)
                {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    _logger.LogTrace("Local embedding check timed out (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                else
                {
                    _logger.LogTrace("Local embedding service check timed out after {Attempts} attempts", maxRetries);
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    _logger.LogTrace(ex, "Local embedding check failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                else
                {
                    _logger.LogTrace(ex, "Local embedding service check failed after {Attempts} attempts", maxRetries);
                    return false;
                }
            }
        }

        return false;
    }

    private async Task<bool> CheckOverlordAvailabilityAsync(CancellationToken ct)
    {
        if (_serverBridge == null || string.IsNullOrEmpty(_overlordUrl))
        {
            _logger.LogTrace("Overlord not configured");
            return false;
        }

        // Retry logic: 3 attempts with exponential backoff (1s, 2s, 4s)
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Проверяем доступность с timeout из конфига
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(_config.Availability.OverlordCheckTimeoutSeconds));

                var isAvailable = await _serverBridge.IsServerAvailableAsync(cts.Token);

                if (isAvailable)
                {
                    _logger.LogTrace("Overlord available (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                    return true;
                }

                // Not available but no exception - don't retry
                _logger.LogTrace("Overlord not available (no exception, attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                return false;
            }
            catch (OperationCanceledException)
            {
                if (attempt < maxRetries)
                {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    _logger.LogTrace("Overlord check timed out (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                else
                {
                    _logger.LogTrace("Overlord availability check timed out after {Attempts} attempts", maxRetries);
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    _logger.LogTrace(ex, "Overlord check failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
                else
                {
                    _logger.LogTrace(ex, "Overlord availability check failed after {Attempts} attempts", maxRetries);
                    return false;
                }
            }
        }

        return false;
    }

    private async Task<string?> GetModelNameAsync(SemanticModeSource source, CancellationToken ct)
    {
        try
        {
            if (source == SemanticModeSource.Local || source == SemanticModeSource.Both)
            {
                // TODO: Get model name from local embedding service
                return "nomic-embed-text"; // Default для Ollama/TEI
            }

            if (source == SemanticModeSource.Overlord)
            {
                // TODO: Get model name from Overlord
                return "nomic-embed-text";
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<int> GetVectorDimensionAsync(SemanticModeSource source, CancellationToken ct)
    {
        try
        {
            if (source == SemanticModeSource.Local || source == SemanticModeSource.Both)
            {
                // Test embedding для определения размерности
                if (_localEmbedding != null)
                {
                    var testVector = await _localEmbedding.GetEmbeddingAsync("test", ct);
                    if (testVector != null)
                    {
                        return testVector.Length;
                    }
                }
            }

            // Default для nomic-embed-text
            return 768;
        }
        catch
        {
            return 768; // Default dimension
        }
    }

    private string? GetLocalEmbeddingUrl()
    {
        // TODO: Get from AgentConfig или configuration
        return "http://localhost:11434"; // Default Ollama URL
    }
}

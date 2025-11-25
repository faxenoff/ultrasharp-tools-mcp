using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация провайдера семантического режима с автоматическим определением доступности
/// </summary>
public sealed partial class SemanticModeProvider : ISemanticModeProvider {
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
        SemanticModeConfig? config = null
    ) {
        _logger = logger;
        _localEmbedding = localEmbedding;
        _serverBridge = serverBridge;
        _overlordUrl = overlordUrl;
        _config = config ?? SemanticModeConfig.CreateDefault();

        LogProviderInitialized(
            _config.Enabled,
            _config.Availability.CacheValiditySeconds,
            _config.Availability.LocalCheckTimeoutSeconds,
            _config.Availability.OverlordCheckTimeoutSeconds
        );
    }

    /// <inheritdoc/>
    public async Task<SemanticModeAvailability> CheckAvailabilityAsync(
        CancellationToken ct = default
    ) {
        // Проверяем, включён ли semantic mode
        if (!_config.Enabled) {
            LogSemanticModeDisabled();
            return new SemanticModeAvailability {
                IsAvailable = false,
                Source = SemanticModeSource.None,
                ModelName = null,
                VectorDimension = 0,
                LocalEmbeddingUrl = null,
                OverlordUrl = null,
            };
        }

        // Проверяем кэш
        var cacheValidity = TimeSpan.FromSeconds(_config.Availability.CacheValiditySeconds);
        if (_cachedAvailability != null && DateTime.UtcNow - _lastCheck < cacheValidity) {
            LogCachedAvailability(_cachedAvailability.Source);
            return _cachedAvailability;
        }

        LogCheckingAvailability();

        var hasLocal = await CheckLocalAvailabilityAsync(ct);
        var hasOverlord = await CheckOverlordAvailabilityAsync(ct);

        SemanticModeSource source;
        if (hasLocal && hasOverlord) {
            source = SemanticModeSource.Both;
            LogBothSourcesAvailable();
        } else if (hasLocal) {
            source = SemanticModeSource.Local;
            LogLocalSourceAvailable();
        } else if (hasOverlord) {
            source = SemanticModeSource.Overlord;
            LogOverlordSourceAvailable();
        } else {
            source = SemanticModeSource.None;
            LogNoSourcesAvailable();
        }

        _cachedAvailability = new SemanticModeAvailability {
            IsAvailable = source != SemanticModeSource.None,
            Source = source,
            ModelName = await GetModelNameAsync(source, ct),
            VectorDimension = await GetVectorDimensionAsync(source, ct),
            LocalEmbeddingUrl = hasLocal ? GetLocalEmbeddingUrl() : null,
            OverlordUrl = hasOverlord ? _overlordUrl : null,
        };

        _lastCheck = DateTime.UtcNow;
        return _cachedAvailability;
    }

    /// <inheritdoc/>
    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(text)) {
            LogEmptyTextEmbedding();
            return null;
        }

        var availability = await CheckAvailabilityAsync(ct);
        if (!availability.IsAvailable) {
            LogEmbeddingNotAvailable();
            return null;
        }

        try {
            // Prefer Local для меньшей latency
            if (
                availability.Source == SemanticModeSource.Local
                || availability.Source == SemanticModeSource.Both
            ) {
                if (_localEmbedding != null) {
                    LogGettingLocalEmbedding();
                    return await _localEmbedding.GetEmbeddingAsync(text, ct);
                }
            }

            // Fallback на Overlord
            if (
                availability.Source == SemanticModeSource.Overlord
                || availability.Source == SemanticModeSource.Both
            ) {
                if (_serverBridge != null) {
                    LogGettingOverlordEmbedding();
                    // Вызываем MCP proxy tool на Overlord
                    var result = await _serverBridge.CallMcpProxyAsync(
                        "get_embedding",
                        new Dictionary<string, object> { ["text"] = text },
                        null,
                        ct
                    );

                    if (result is float[] vector) {
                        return vector;
                    }

                    LogUnexpectedEmbeddingType(result?.GetType()?.ToString());
                }
            }

            return null;
        } catch (Exception ex) {
            LogEmbeddingFailed(ex, text.Length);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SemanticMatch>> SearchAsync(
        float[] queryVector,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default
    ) {
        if (queryVector == null || queryVector.Length == 0) {
            LogEmptyQueryVector();
            return Array.Empty<SemanticMatch>();
        }

        var availability = await CheckAvailabilityAsync(ct);
        if (!availability.IsAvailable) {
            LogSearchNotAvailable();
            return Array.Empty<SemanticMatch>();
        }

        try {
            // Prefer Overlord для cross-project search
            if (
                availability.Source == SemanticModeSource.Overlord
                || availability.Source == SemanticModeSource.Both
            ) {
                if (_serverBridge != null) {
                    LogOverlordSearch(topK, threshold);
                    var result = await _serverBridge.CallMcpProxyAsync(
                        "semantic_search_by_vector",
                        new Dictionary<string, object> {
                            ["queryVector"] = queryVector,
                            ["topK"] = topK,
                            ["threshold"] = threshold,
                        },
                        null,
                        ct
                    );

                    if (result is IEnumerable<SemanticMatch> matches) {
                        return matches;
                    }

                    LogUnexpectedSearchType(result?.GetType()?.ToString());
                }
            }

            // Fallback на Local (если доступен)
            if (availability.Source == SemanticModeSource.Local) {
                LogLocalSearchNotImplemented();
                // TODO: Implement local vector store search
            }

            return Array.Empty<SemanticMatch>();
        } catch (Exception ex) {
            LogSearchFailed(ex, queryVector.Length, topK);
            return Array.Empty<SemanticMatch>();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SemanticMatch>> SearchByTextAsync(
        string query,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default
    ) {
        if (string.IsNullOrWhiteSpace(query)) {
            LogEmptyQueryText();
            return Array.Empty<SemanticMatch>();
        }

        LogConvertingQuery(query.Length > 50 ? query[..50] + "..." : query);

        // Получаем вектор для запроса
        var queryVector = await GetEmbeddingAsync(query, ct);
        if (queryVector == null) {
            LogVectorizeFailed();
            return Array.Empty<SemanticMatch>();
        }

        // Выполняем поиск по вектору
        return await SearchAsync(queryVector, topK, threshold, ct);
    }

    // === Private Helper Methods ===

    private async Task<bool> CheckLocalAvailabilityAsync(CancellationToken ct) {
        if (_localEmbedding == null) {
            LogLocalNotRegistered();
            return false;
        }

        // Retry logic: 3 attempts with exponential backoff (1s, 2s, 4s)
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++) {
            try {
                // Проверяем доступность через тестовый запрос с timeout из конфига
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(
                    TimeSpan.FromSeconds(_config.Availability.LocalCheckTimeoutSeconds)
                );

                var testVector = await _localEmbedding.GetEmbeddingAsync("test", cts.Token);
                var isAvailable = testVector != null && testVector.Length > 0;

                if (isAvailable) {
                    LogLocalAvailable(attempt, maxRetries);
                    return true;
                }

                // Not available but no exception - don't retry
                LogLocalNotAvailableNoException(attempt, maxRetries);
                return false;
            } catch (OperationCanceledException) {
                if (attempt < maxRetries) {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    LogLocalTimeout(attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                } else {
                    LogLocalTimeoutFinal(maxRetries);
                    return false;
                }
            } catch (Exception ex) {
                if (attempt < maxRetries) {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    LogLocalCheckFailed(ex, attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                } else {
                    LogLocalCheckFailedFinal(ex, maxRetries);
                    return false;
                }
            }
        }

        return false;
    }

    private async Task<bool> CheckOverlordAvailabilityAsync(CancellationToken ct) {
        if (_serverBridge == null || string.IsNullOrEmpty(_overlordUrl)) {
            LogOverlordNotConfigured();
            return false;
        }

        // Retry logic: 3 attempts with exponential backoff (1s, 2s, 4s)
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(1);

        for (int attempt = 1; attempt <= maxRetries; attempt++) {
            try {
                // Проверяем доступность с timeout из конфига
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(
                    TimeSpan.FromSeconds(_config.Availability.OverlordCheckTimeoutSeconds)
                );

                var isAvailable = await _serverBridge.IsServerAvailableAsync(cts.Token);

                if (isAvailable) {
                    LogOverlordAvailable(attempt, maxRetries);
                    return true;
                }

                // Not available but no exception - don't retry
                LogOverlordNotAvailableNoException(attempt, maxRetries);
                return false;
            } catch (OperationCanceledException) {
                if (attempt < maxRetries) {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    LogOverlordTimeout(attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                } else {
                    LogOverlordTimeoutFinal(maxRetries);
                    return false;
                }
            } catch (Exception ex) {
                if (attempt < maxRetries) {
                    var delay = baseDelay * Math.Pow(2, attempt - 1);
                    LogOverlordCheckFailed(ex, attempt, maxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                } else {
                    LogOverlordCheckFailedFinal(ex, maxRetries);
                    return false;
                }
            }
        }

        return false;
    }
    private async Task<string?> GetModelNameAsync(SemanticModeSource source, CancellationToken ct) {
        try {
            if (source == SemanticModeSource.Local || source == SemanticModeSource.Both) {
                // Get model name from config
                if (_config.Embedding != null) {
                    if (_config.Embedding.Platform == "tei" && _config.Embedding.Tei != null) {
                        return _config.Embedding.Tei.SelectedModel ?? "unknown-tei-model";
                    }
                    if (_config.Embedding.Platform == "ollama" && _config.Embedding.Ollama != null) {
                        return _config.Embedding.Ollama.SelectedModel ?? "nomic-embed-text";
                    }
                }
                return "nomic-embed-text"; // Default fallback
            }

            if (source == SemanticModeSource.Overlord) {
                return "nomic-embed-text";
            }

            return null;
        } catch {
            return null;
        }
    }
    private async Task<int> GetVectorDimensionAsync(SemanticModeSource source, CancellationToken ct) {
        try {
            if (source == SemanticModeSource.Local || source == SemanticModeSource.Both) {
                // Test embedding для определения размерности
                if (_localEmbedding != null) {
                    var testVector = await _localEmbedding.GetEmbeddingAsync("test", ct);
                    if (testVector != null) {
                        return testVector.Length;
                    }
                }
            }

            // Fallback: get from config
            if (_config.Embedding != null) {
                if (_config.Embedding.Platform == "tei" && _config.Embedding.Tei?.Models != null) {
                    var selectedModel = _config.Embedding.Tei.SelectedModel;
                    var modelInfo = _config.Embedding.Tei.Models.Find(m => m.Id == selectedModel);
                    if (modelInfo != null && modelInfo.VectorSize > 0) {
                        return modelInfo.VectorSize;
                    }
                }
            }

            return 768; // Default dimension
        } catch {
            return 768;
        }
    }
    private string? GetLocalEmbeddingUrl() {
        if (_config.Embedding != null) {
            if (_config.Embedding.Platform == "tei" && _config.Embedding.Tei != null) {
                return _config.Embedding.Tei.Endpoint;
            }
            if (_config.Embedding.Platform == "ollama" && _config.Embedding.Ollama != null) {
                return _config.Embedding.Ollama.Endpoint;
            }
        }
        return "http://localhost:11434"; // Default Ollama URL
    }
}

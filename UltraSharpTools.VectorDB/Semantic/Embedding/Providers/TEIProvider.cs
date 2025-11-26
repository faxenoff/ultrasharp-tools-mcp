using System.Net.Http.Json;
using Microsoft.Extensions.Http;
using UltraSharpTools.VectorDB.Semantic.Models;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB.Semantic.Embedding.Providers;

/// <summary>
/// TEI (Text Embeddings Inference) provider
/// Supports 8192 tokens context with ibm-granite models
/// Requires Docker container running
/// </summary>
public sealed class TEIProvider : IEmbeddingProvider {
    private readonly TEIOptions _options;
    private readonly ILogger<TEIProvider> _logger;
    private readonly HttpClient _httpClient;
    private int? _dimension;

    public string Name => "tei";
    public int MaxContextTokens => 8192;
    public int? Dimension => _dimension;

    public ProviderInfo Info =>
        new() {
            Name = "tei",
            Model = _options.Model,
            Dimension = _dimension ?? 0,
            MaxTokens = 8192,
            IsLocal = true,
            Version = "1.0",
        };

    public TEIProvider(
        TEIOptions options,
        ILogger<TEIProvider> logger,
        IHttpClientFactory httpClientFactory
    ) {
        _options = options;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("TEI");
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        _logger.LogInformation("[TEI] Initializing with model: {Model}", _options.Model);

        if (_options.CheckServer) {
            var available = await CheckServerAsync(cancellationToken);
            if (!available && _options.AutoStart) {
                _logger.LogInformation("[TEI] Server not available, attempting auto-start...");
                await TryStartDockerContainerAsync(cancellationToken);
            }
        }

        // Warmup: generate test embedding to determine dimension
        try {
            var testEmbedding = await EmbedAsync("test", cancellationToken);
            _dimension = testEmbedding.Length;
            _logger.LogInformation("[TEI] Initialized successfully (dimension: {Dim})", _dimension);
        } catch (Exception ex) {
            _logger.LogError(ex, "[TEI] Initialization failed");
            throw new InvalidOperationException(
                "TEI provider initialization failed. Is Docker container running?",
                ex
            );
        }
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default
    ) {
        try {
            var request = new TEIRequest { Inputs = text };
            var response = await _httpClient.PostAsJsonAsync(
                "/embed",
                request,
                EmbeddingJsonContext.Default.TEIRequest,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            // TEI returns [[0.1, 0.2, ...]] even for single input
            var embeddings = await response.Content.ReadFromJsonAsync(
                EmbeddingJsonContext.GetDoubleArrayTypeInfo(),
                cancellationToken
            );
            if (embeddings == null || embeddings.Length == 0 || embeddings[0].Length == 0) {
                throw new InvalidOperationException("TEI returned empty embedding");
            }

            return embeddings[0];
        } catch (HttpRequestException ex) {
            _logger.LogError(ex, "[TEI] HTTP request failed");
            throw new InvalidOperationException($"TEI request failed: {ex.Message}", ex);
        }
    }
    public async Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default
    ) {
        // TEI supports batch embedding
        try {
            var request = new TEIBatchRequest { Inputs = texts.ToArray() };
            var response = await _httpClient.PostAsJsonAsync(
                "/embed",
                request,
                EmbeddingJsonContext.Default.TEIBatchRequest,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var embeddings = await response.Content.ReadFromJsonAsync(
                EmbeddingJsonContext.GetDoubleArrayTypeInfo(),
                cancellationToken
            );
            if (embeddings == null || embeddings.Length != texts.Count) {
                throw new InvalidOperationException(
                    $"TEI batch response mismatch: expected {texts.Count}, got {embeddings?.Length ?? 0}"
                );
            }

            return embeddings;
        } catch (HttpRequestException ex) {
            _logger.LogError(ex, "[TEI] Batch request failed");

            // Fallback: параллельная обработка с ограничением concurrency
            _logger.LogWarning("[TEI] Falling back to parallel processing (concurrency: {Concurrency})", _options.Concurrency);

            using var semaphore = new SemaphoreSlim(_options.Concurrency);
            var tasks = texts.Select(async text => {
                await semaphore.WaitAsync(cancellationToken);
                try {
                    return await EmbedAsync(text, cancellationToken);
                } finally {
                    semaphore.Release();
                }
            });

            return await Task.WhenAll(tasks);
        }
    }
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
        return await CheckServerAsync(cancellationToken);
    }

    private async Task<bool> CheckServerAsync(CancellationToken cancellationToken) {
        try {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;

            if (isHealthy) {
                _logger.LogDebug("[TEI] Server is healthy");
            } else {
                _logger.LogWarning("[TEI] Server returned {StatusCode}", response.StatusCode);
            }

            return isHealthy;
        } catch (Exception ex) {
            _logger.LogDebug(ex, "[TEI] Server check failed");
            return false;
        }
    }

    private async Task TryStartDockerContainerAsync(CancellationToken cancellationToken) {
        try {
            _logger.LogInformation(
                "[TEI] Starting Docker container: {Container}",
                _options.ContainerName
            );

            using var process = new System.Diagnostics.Process {
                StartInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = "docker",
                    Arguments = $"start {_options.ContainerName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0) {
                _logger.LogInformation("[TEI] Docker container started successfully");

                // Wait for server to become ready
                for (int i = 0; i < 30; i++) {
                    await Task.Delay(1000, cancellationToken);
                    if (await CheckServerAsync(cancellationToken)) {
                        _logger.LogInformation("[TEI] Server is ready");
                        return;
                    }
                }

                _logger.LogWarning("[TEI] Server did not become ready after 30 seconds");
            } else {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("[TEI] Docker start failed: {Error}", stderr);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "[TEI] Failed to start Docker container");
        }
    }

    public ValueTask DisposeAsync() {
        _httpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}

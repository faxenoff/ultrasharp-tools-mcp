using System.Diagnostics;
using System.Net.Http;
using UltrasharpTools.Tools.Config;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Health check and auto-start service for semantic embedding providers (TEI, Ollama)
/// </summary>
public class SemanticServiceHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SemanticServiceHealthCheck>? _logger;

    public SemanticServiceHealthCheck(
        IHttpClientFactory? httpClientFactory = null,
        ILogger<SemanticServiceHealthCheck>? logger = null
    )
    {
        _httpClient =
            httpClientFactory?.CreateClient()
            ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = logger;
    }

    /// <summary>
    /// Quick check if semantic service is available (fast, no auto-start)
    /// </summary>
    public async Task<bool> QuickCheckAsync(
        SemanticEmbeddingConfig config,
        CancellationToken cancellationToken = default
    )
    {
        // CRITICAL DEBUG: Hardcoded path logging FIRST
        try
        {
            File.AppendAllText(
                @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - QuickCheckAsync ENTRY\n"
            );
        }
        catch { }

        var platform = config.Embedding.Platform.ToLowerInvariant();

        // Debug logging with details
        try
        {
            File.AppendAllText(
                @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - QuickCheckAsync: platform={platform}, endpoint={config.Embedding.Tei?.Endpoint ?? "null"}\n"
            );
        }
        catch { }

        var result = platform switch
        {
            "tei" => await CheckTeiHealthAsync(
                config.Embedding.Tei?.Endpoint ?? "http://127.0.0.1:8080",
                cancellationToken
            ),
            "ollama" => await CheckOllamaHealthAsync(
                config.Embedding.Ollama?.Endpoint ?? "http://127.0.0.1:11434",
                cancellationToken
            ),
            "memory" => true,
            _ => false,
        };

        // Log result
        try
        {
            File.AppendAllText(
                @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - QuickCheckAsync EXIT: result={result}\n"
            );
        }
        catch { }

        return result;
    }

    /// <summary>
    /// Check if semantic service is available and try to start if not
    /// </summary>
    public async Task<HealthCheckResult> CheckAndStartAsync(
        SemanticEmbeddingConfig config,
        CancellationToken cancellationToken = default
    )
    {
        var platform = config.Embedding.Platform.ToLowerInvariant();

        return platform switch
        {
            "tei" => await CheckAndStartTeiAsync(config, cancellationToken),
            "ollama" => await CheckAndStartOllamaAsync(config, cancellationToken),
            "memory" => new HealthCheckResult
            {
                IsAvailable = true,
                Platform = "memory",
                Message = "In-memory embeddings enabled",
            },
            _ => new HealthCheckResult
            {
                IsAvailable = false,
                Platform = platform,
                Message = $"Unknown platform: {platform}",
            },
        };
    }

    private async Task<HealthCheckResult> CheckAndStartTeiAsync(
        SemanticEmbeddingConfig config,
        CancellationToken cancellationToken
    )
    {
        var endpoint = config.Embedding.Tei.Endpoint;
        var architecture = config.Embedding.Architecture ?? "cpu";
        var model = config.Embedding.Tei.SelectedModel;

        _logger?.LogInformation("[Semantic] Checking TEI availability at {Endpoint}", endpoint);

        // Check if TEI is already running
        if (await CheckTeiHealthAsync(endpoint, cancellationToken))
        {
            _logger?.LogInformation("[Semantic] ✓ TEI is running");
            return new HealthCheckResult
            {
                IsAvailable = true,
                Platform = "tei",
                Endpoint = endpoint,
                Message = "TEI server is running",
            };
        }

        _logger?.LogWarning("[Semantic] TEI not responding, attempting auto-start...");

        // Try to start TEI
        var started = await StartTeiAsync(endpoint, architecture, model, cancellationToken);

        if (started)
        {
            _logger?.LogInformation("[Semantic] ✓ TEI started successfully");
            return new HealthCheckResult
            {
                IsAvailable = true,
                Platform = "tei",
                Endpoint = endpoint,
                Message = "TEI server auto-started",
            };
        }

        _logger?.LogError("[Semantic] ✗ Failed to start TEI");
        return new HealthCheckResult
        {
            IsAvailable = false,
            Platform = "tei",
            Endpoint = endpoint,
            Message =
                "TEI server not available and auto-start failed. Please run: .\\Config\\Scripts\\setup-tei.ps1",
        };
    }

    private async Task<HealthCheckResult> CheckAndStartOllamaAsync(
        SemanticEmbeddingConfig config,
        CancellationToken cancellationToken
    )
    {
        var endpoint = config.Embedding.Ollama.Endpoint;
        var model = config.Embedding.Ollama.SelectedModel;

        _logger?.LogInformation("[Semantic] Checking Ollama availability at {Endpoint}", endpoint);

        // Check if Ollama is already running
        if (await CheckOllamaHealthAsync(endpoint, cancellationToken))
        {
            _logger?.LogInformation("[Semantic] ✓ Ollama is running");

            // Check if model is available
            var hasModel = await CheckOllamaModelAsync(endpoint, model, cancellationToken);
            if (!hasModel)
            {
                _logger?.LogWarning(
                    "[Semantic] Model {Model} not found, attempting pull...",
                    model
                );
                var pulled = await PullOllamaModelAsync(model, cancellationToken);
                if (!pulled)
                {
                    return new HealthCheckResult
                    {
                        IsAvailable = false,
                        Platform = "ollama",
                        Endpoint = endpoint,
                        Message =
                            $"Ollama running but model '{model}' not available. Run: ollama pull {model}",
                    };
                }
            }

            return new HealthCheckResult
            {
                IsAvailable = true,
                Platform = "ollama",
                Endpoint = endpoint,
                Message = "Ollama server is running with model loaded",
            };
        }

        _logger?.LogWarning("[Semantic] Ollama not responding, attempting auto-start...");

        // Try to start Ollama
        var started = await StartOllamaAsync(cancellationToken);

        if (started)
        {
            _logger?.LogInformation("[Semantic] ✓ Ollama started successfully");

            // Try to pull model
            await PullOllamaModelAsync(model, cancellationToken);

            return new HealthCheckResult
            {
                IsAvailable = true,
                Platform = "ollama",
                Endpoint = endpoint,
                Message = "Ollama server auto-started",
            };
        }

        _logger?.LogError("[Semantic] ✗ Failed to start Ollama");
        return new HealthCheckResult
        {
            IsAvailable = false,
            Platform = "ollama",
            Endpoint = endpoint,
            Message = "Ollama not available and auto-start failed. Install from: https://ollama.ai",
        };
    }

    private async Task<bool> CheckTeiHealthAsync(
        string endpoint,
        CancellationToken cancellationToken
    )
    {
        const string LOG = @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log";

        try
        {
            File.AppendAllText(
                LOG,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CheckTeiHealthAsync ENTRY\n"
            );
        }
        catch { }

        try
        {
            // TEI uses /info endpoint for health checks, not /health
            var healthUrl = $"{endpoint.TrimEnd('/')}/info";

            try
            {
                File.AppendAllText(
                    LOG,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CheckTeiHealthAsync START: URL={healthUrl}, Timeout={_httpClient.Timeout.TotalSeconds}s\n"
                );
            }
            catch { }

            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);

            try
            {
                File.AppendAllText(
                    LOG,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CheckTeiHealthAsync RESPONSE: StatusCode={response.StatusCode}, Success={response.IsSuccessStatusCode}\n"
                );
            }
            catch { }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(
                    LOG,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CheckTeiHealthAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}\n"
                );
            }
            catch { }
            return false;
        }
    }

    private async Task<bool> StartTeiAsync(
        string endpoint,
        string architecture,
        string model,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Extract port from endpoint
            var uri = new Uri(endpoint);
            var port = uri.Port;

            // Check if docker is available
            var dockerCheck = await RunProcessAsync("docker", "--version", cancellationToken);
            if (!dockerCheck.success)
            {
                _logger?.LogWarning("[Semantic] Docker not found, cannot auto-start TEI");
                return false;
            }

            // Stop existing container if running
            await RunProcessAsync("docker", $"stop ultrasharp-tei", cancellationToken);
            await RunProcessAsync("docker", $"rm ultrasharp-tei", cancellationToken);

            // Determine image based on architecture
            var image = architecture.ToLowerInvariant() switch
            {
                "cpu" => "ghcr.io/huggingface/text-embeddings-inference:cpu-1.5",
                _ => $"ghcr.io/huggingface/text-embeddings-inference:{architecture}-1.5",
            };

            // Start TEI container
            var dockerArgs =
                $"run -d --name ultrasharp-tei -p {port}:80 -v $HOME/.cache/huggingface:/data {image} --model-id {model}";

            _logger?.LogInformation("[Semantic] Starting TEI container: {Args}", dockerArgs);

            var (success, output) = await RunProcessAsync("docker", dockerArgs, cancellationToken);

            if (!success)
            {
                _logger?.LogError("[Semantic] Docker run failed: {Output}", output);
                return false;
            }

            // Wait for TEI to be ready (max 60 seconds)
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000, cancellationToken);
                if (await CheckTeiHealthAsync(endpoint, cancellationToken))
                {
                    _logger?.LogInformation("[Semantic] TEI ready after {Seconds}s", i + 1);
                    return true;
                }
            }

            _logger?.LogWarning("[Semantic] TEI container started but not responding after 60s");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Semantic] Error starting TEI");
            return false;
        }
    }

    private async Task<bool> CheckOllamaHealthAsync(
        string endpoint,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{endpoint.TrimEnd('/')}/api/tags",
                cancellationToken
            );
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckOllamaModelAsync(
        string endpoint,
        string model,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{endpoint.TrimEnd('/')}/api/tags",
                cancellationToken
            );
            if (!response.IsSuccessStatusCode)
                return false;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content.Contains(model.Split(':')[0]); // Match base model name
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> StartOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if ollama is installed
            var (success, _) = await RunProcessAsync("ollama", "--version", cancellationToken);
            if (!success)
            {
                _logger?.LogWarning("[Semantic] Ollama not installed");
                return false;
            }

            // Start ollama serve in background
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.Start();

            // Wait for Ollama to be ready
            await Task.Delay(3000, cancellationToken);

            return await CheckOllamaHealthAsync("http://localhost:11434", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Semantic] Error starting Ollama");
            return false;
        }
    }

    private async Task<bool> PullOllamaModelAsync(string model, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("[Semantic] Pulling Ollama model: {Model}", model);

            var (success, output) = await RunProcessAsync(
                "ollama",
                $"pull {model}",
                cancellationToken,
                timeoutSeconds: 300
            );

            if (success)
            {
                _logger?.LogInformation("[Semantic] ✓ Model pulled successfully");
            }
            else
            {
                _logger?.LogWarning("[Semantic] Model pull failed: {Output}", output);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Semantic] Error pulling model");
            return false;
        }
    }

    private async Task<(bool success, string output)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        int timeoutSeconds = 30
    )
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(waitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                process.Kill(true);
                return (false, "Timeout");
            }

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, string.IsNullOrEmpty(error) ? output : error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

public class HealthCheckResult
{
    public bool IsAvailable { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string Message { get; set; } = string.Empty;
}

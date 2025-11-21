
using System.Diagnostics;

using UltrasharpTools.Tools.Config;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Automatically selects best embedding configuration on first run
/// </summary>
public class AutoConfigurationService
{
    private readonly ILogger<AutoConfigurationService> _logger;
    private readonly EmbeddingServiceHealthChecker _healthChecker;

    public AutoConfigurationService(
        ILogger<AutoConfigurationService> logger,
        EmbeddingServiceHealthChecker healthChecker)
    {
        _logger = logger;
        _healthChecker = healthChecker;
    }

    public class AutoConfigResult
    {
        public SemanticEmbeddingConfig Config { get; set; } = new();
        public string SelectedPlatform { get; set; } = "";
        public string SelectedArchitecture { get; set; } = "";
        public string Reason { get; set; } = "";
        public bool RequiresSetup { get; set; }
        public string SetupInstructions { get; set; } = "";
    }

    /// <summary>
    /// Auto-detect and configure best embedding platform
    /// </summary>
    public async Task<AutoConfigResult> AutoConfigureAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Auto-Configuring Embedding Platform ===");
        _logger.LogInformation("Detecting best available platform...");
        _logger.LogInformation("");

        var result = new AutoConfigResult
        {
            Config = CreateDefaultConfig()
        };

        // Step 1: Detect GPU architecture
        var architecture = await DetectGpuArchitectureAsync();
        result.SelectedArchitecture = architecture;
        result.Config.Embedding.Architecture = architecture;

        _logger.LogInformation("✓ GPU Architecture: {Arch}", architecture);
        _logger.LogInformation("");

        // Step 2: Try platforms in order of preference
        // Priority: Ollama (easiest) > TEI (high performance) > Memory (fallback)

        _logger.LogInformation("Checking available platforms...");

        // Try Ollama first (most user-friendly)
        var ollamaHealth = await _healthChecker.CheckOllamaHealthAsync(
            "http://localhost:11434",
            cancellationToken);

        if (ollamaHealth.IsHealthy)
        {
            var hasModel = await _healthChecker.CheckOllamaModelAsync(
                "http://localhost:11434",
                "granite-embedding:latest",
                cancellationToken);

            if (hasModel)
            {
                result.SelectedPlatform = "ollama";
                result.Config.Embedding.Platform = "ollama";
                result.Reason = "Ollama is available with granite-embedding model";
                result.RequiresSetup = false;

                _logger.LogInformation("✓ Ollama: Available with granite-embedding model");
                _logger.LogInformation("  Selected: Ollama (recommended for ease of use)");
                return result;
            }
            else
            {
                // Ollama is running but model not installed
                result.SelectedPlatform = "ollama";
                result.Config.Embedding.Platform = "ollama";
                result.Reason = "Ollama is available but model needs to be installed";
                result.RequiresSetup = true;
                result.SetupInstructions = "Install embedding model:\n  ollama pull granite-embedding";

                _logger.LogWarning("⚠ Ollama: Available but model not installed");
                _logger.LogInformation("  Selected: Ollama (requires model installation)");
                return result;
            }
        }
        else
        {
            _logger.LogWarning("✗ Ollama: Not available");
            _logger.LogDebug("  {Details}", ollamaHealth.ErrorMessage);
        }

        // Try TEI
        var teiHealth = await _healthChecker.CheckTeiHealthAsync(
            "http://localhost:8080",
            cancellationToken);

        if (teiHealth.IsHealthy)
        {
            result.SelectedPlatform = "tei";
            result.Config.Embedding.Platform = "tei";
            result.Reason = "TEI is available and ready";
            result.RequiresSetup = false;

            _logger.LogInformation("✓ TEI: Available and ready");
            _logger.LogInformation("  Selected: TEI (high performance)");
            return result;
        }
        else
        {
            _logger.LogWarning("✗ TEI: Not available");
            _logger.LogDebug("  {Details}", teiHealth.ErrorMessage);
        }

        // Fallback to Memory (requires setup)
        result.SelectedPlatform = "ollama";  // Still recommend Ollama as best option
        result.Config.Embedding.Platform = "ollama";
        result.Reason = "No embedding platform available - Ollama recommended for easy setup";
        result.RequiresSetup = true;
        result.SetupInstructions = @"
Quick Setup (Recommended - Ollama):
  1. Install Ollama: https://ollama.ai
  2. Install embedding model: ollama pull granite-embedding
  3. Restart MCP server

Alternative (TEI - High Performance):
  1. Run setup script: .\Dev.Scripts\setup-tei.ps1
  2. Restart MCP server
";

        _logger.LogWarning("✗ No embedding platform available");
        _logger.LogInformation("");
        _logger.LogInformation("Recommendation: Install Ollama for easy setup");
        _logger.LogInformation("  Visit: https://ollama.ai");

        return result;
    }

    private async Task<string> DetectGpuArchitectureAsync()
    {
        try
        {
            var scriptPath = FindGpuDetectionScript();
            if (scriptPath == null)
            {
                _logger.LogWarning("GPU detection script not found, defaulting to CPU");
                return "cpu";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning("Failed to start GPU detection script");
                return "cpu";
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var architecture = output.Trim().Split('\n')[^1].Trim();
                return architecture;
            }

            _logger.LogWarning("GPU detection script failed, defaulting to CPU");
            return "cpu";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting GPU architecture, defaulting to CPU");
            return "cpu";
        }
    }

    private string? FindGpuDetectionScript()
    {
        // Try common locations
        var locations = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "detect-gpu-architecture.ps1"),
            Path.Combine(Directory.GetCurrentDirectory(), "detect-gpu-architecture.ps1"),
            Path.Combine(AppContext.BaseDirectory, "..", "detect-gpu-architecture.ps1"),
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
            {
                _logger.LogDebug("Found GPU detection script: {Path}", location);
                return location;
            }
        }

        return null;
    }

    private SemanticEmbeddingConfig CreateDefaultConfig()
    {
        return new SemanticEmbeddingConfig
        {
            Embedding = new EmbeddingSettings
            {
                Platform = "ollama",
                Architecture = "auto",
                Tei = new TeiSettings
                {
                    Endpoint = "http://localhost:8080",
                    SelectedModel = "sentence-transformers/all-MiniLM-L6-v2"
                },
                Ollama = new OllamaSettings
                {
                    Endpoint = "http://localhost:11434",
                    SelectedModel = "granite-embedding:latest"
                },
                Memory = new MemorySettings()
            },
            AutoDetection = new AutoDetectionSettings
            {
                GpuArchitecture = true,
                Language = true,
                CodebaseSize = true
            }
        };
    }

    public void PrintAutoConfigResult(AutoConfigResult result)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Auto-Configuration Complete");
        Console.ResetColor();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        Console.WriteLine($"Selected Platform:    {result.SelectedPlatform}");
        Console.WriteLine($"GPU Architecture:     {result.SelectedArchitecture}");
        Console.WriteLine($"Reason:               {result.Reason}");
        Console.WriteLine();

        if (result.RequiresSetup)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ SETUP REQUIRED");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(result.SetupInstructions);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Ready to use!");
            Console.ResetColor();
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }
}

using System.Diagnostics;
using UltrasharpTools.Tools.Config;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Automatically selects best embedding configuration on first run
/// </summary>
public partial class AutoConfigurationService
{
    private readonly ILogger<AutoConfigurationService> _logger;
    private readonly EmbeddingServiceHealthChecker _healthChecker;

    public AutoConfigurationService(
        ILogger<AutoConfigurationService> logger,
        EmbeddingServiceHealthChecker healthChecker
    )
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
        CancellationToken cancellationToken = default
    )
    {
        LogAutoConfigHeader();
        LogDetectingPlatform();
        LogEmptyLine();

        var result = new AutoConfigResult { Config = CreateDefaultConfig() };

        // Step 1: Detect GPU architecture
        var architecture = await DetectGpuArchitectureAsync();
        result.SelectedArchitecture = architecture;
        result.Config.Embedding.Architecture = architecture;

        LogGpuArchitecture(architecture);
        LogEmptyLine();

        // Step 2: Try platforms in order of preference
        // Priority: Ollama (easiest) > TEI (high performance) > Memory (fallback)

        LogCheckingPlatforms();

        // Try Ollama first (most user-friendly)
        var ollamaHealth = await _healthChecker.CheckOllamaHealthAsync(
            "http://localhost:11434",
            cancellationToken
        );

        if (ollamaHealth.IsHealthy)
        {
            var hasModel = await _healthChecker.CheckOllamaModelAsync(
                "http://localhost:11434",
                "granite-embedding:latest",
                cancellationToken
            );

            if (hasModel)
            {
                result.SelectedPlatform = "ollama";
                result.Config.Embedding.Platform = "ollama";
                result.Reason = "Ollama is available with granite-embedding model";
                result.RequiresSetup = false;

                LogOllamaAvailableWithModel();
                LogOllamaSelectedRecommended();
                return result;
            }
            else
            {
                // Ollama is running but model not installed
                result.SelectedPlatform = "ollama";
                result.Config.Embedding.Platform = "ollama";
                result.Reason = "Ollama is available but model needs to be installed";
                result.RequiresSetup = true;
                result.SetupInstructions =
                    "Install embedding model:\n  ollama pull granite-embedding";

                LogOllamaNoModel();
                LogOllamaSelectedRequiresModel();
                return result;
            }
        }
        else
        {
            LogOllamaNotAvailable();
            LogDetails(ollamaHealth.ErrorMessage);
        }

        // Try TEI
        var teiHealth = await _healthChecker.CheckTeiHealthAsync(
            "http://localhost:8080",
            cancellationToken
        );

        if (teiHealth.IsHealthy)
        {
            result.SelectedPlatform = "tei";
            result.Config.Embedding.Platform = "tei";
            result.Reason = "TEI is available and ready";
            result.RequiresSetup = false;

            LogTeiAvailable();
            LogTeiSelected();
            return result;
        }
        else
        {
            LogTeiNotAvailable();
            LogDetails(teiHealth.ErrorMessage);
        }

        // Fallback to Memory (requires setup)
        result.SelectedPlatform = "ollama"; // Still recommend Ollama as best option
        result.Config.Embedding.Platform = "ollama";
        result.Reason = "No embedding platform available - Ollama recommended for easy setup";
        result.RequiresSetup = true;
        result.SetupInstructions =
            @"
Quick Setup (Recommended - Ollama):
  1. Install Ollama: https://ollama.ai
  2. Install embedding model: ollama pull granite-embedding
  3. Restart MCP server

Alternative (TEI - High Performance):
  1. Run setup script: .\Dev.Scripts\setup-tei.ps1
  2. Restart MCP server
";

        LogNoPlatformAvailable();
        LogEmptyLine();
        LogRecommendOllama();
        LogOllamaUrl();

        return result;
    }

    private async Task<string> DetectGpuArchitectureAsync()
    {
        try
        {
            var scriptPath = FindGpuDetectionScript();
            if (scriptPath == null)
            {
                LogGpuScriptNotFound();
                return "cpu";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                LogGpuScriptStartFailed();
                return "cpu";
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var architecture = output.Trim().Split('\n')[^1].Trim();
                return architecture;
            }

            LogGpuScriptFailed();
            return "cpu";
        }
        catch (Exception ex)
        {
            LogGpuDetectionError(ex);
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
                LogGpuScriptFound(location);
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
                    SelectedModel = "sentence-transformers/all-MiniLM-L6-v2",
                },
                Ollama = new OllamaSettings
                {
                    Endpoint = "http://localhost:11434",
                    SelectedModel = "granite-embedding:latest",
                },
                Memory = new MemorySettings(),
            },
            AutoDetection = new AutoDetectionSettings
            {
                GpuArchitecture = true,
                Language = true,
                CodebaseSize = true,
            },
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

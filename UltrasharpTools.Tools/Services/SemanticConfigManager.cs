using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Config;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Manages semantic embedding configuration with ENV override support, validation, and auto-configuration
/// </summary>
public class SemanticConfigManager
{
    private readonly ILogger<SemanticConfigManager> _logger;
    private readonly CodebaseLanguageDetector _languageDetector;
    private readonly CodebaseSizeDetector _sizeDetector;
    private readonly EmbeddingConfigValidator _validator;
    private readonly AutoConfigurationService _autoConfig;

    private SemanticEmbeddingConfig? _globalConfig;
    private ProjectSemanticConfig? _projectConfig;

    public SemanticConfigManager(
        ILogger<SemanticConfigManager> logger,
        CodebaseLanguageDetector languageDetector,
        CodebaseSizeDetector sizeDetector,
        EmbeddingConfigValidator validator,
        AutoConfigurationService autoConfig)
    {
        _logger = logger;
        _languageDetector = languageDetector;
        _sizeDetector = sizeDetector;
        _validator = validator;
        _autoConfig = autoConfig;
    }

    /// <summary>
    /// Load global config from file, with auto-configuration and validation
    /// </summary>
    public async Task<SemanticEmbeddingConfig> LoadGlobalConfigAsync(
        string? configPath = null,
        CancellationToken cancellationToken = default)
    {
        if (_globalConfig != null)
            return _globalConfig;

        configPath ??= GetDefaultGlobalConfigPath();

        // First run: no config exists - auto-configure
        if (!File.Exists(configPath))
        {
            _logger.LogWarning("═══════════════════════════════════════════════════════════");
            _logger.LogWarning("No configuration found - running first-time setup");
            _logger.LogWarning("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("");

            var autoConfigResult = await _autoConfig.AutoConfigureAsync(cancellationToken);
            _globalConfig = autoConfigResult.Config;

            await SaveGlobalConfigAsync(_globalConfig, configPath);

            _logger.LogInformation("");
            _logger.LogInformation("Configuration saved to: {Path}", configPath);
            _logger.LogInformation("");

            // Print auto-config results
            if (autoConfigResult.RequiresSetup)
            {
                _logger.LogWarning("⚠ SETUP REQUIRED");
                _logger.LogWarning("");
                _logger.LogWarning(autoConfigResult.SetupInstructions);
            }
            else
            {
                _logger.LogInformation("✓ Ready to use!");
            }

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("");
        }
        else
        {
            // Config exists - load and validate
            _logger.LogInformation("Loading global config from: {Path}", configPath);
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            _globalConfig = JsonSerializer.Deserialize<SemanticEmbeddingConfig>(json) ?? CreateDefaultGlobalConfig();

            // Apply ENV overrides
            ApplyEnvironmentOverrides(_globalConfig);

            // Validate configuration
            _logger.LogInformation("Validating configuration...");
            var validation = await _validator.ValidateGlobalConfigAsync(_globalConfig, cancellationToken);

            if (!validation.IsValid || validation.HasWarnings)
            {
                _logger.LogWarning("");
                _validator.PrintValidationResults(validation);

                if (!validation.IsValid)
                {
                    _logger.LogError("Configuration is invalid and cannot be used!");
                    _logger.LogError("Please fix the issues above or run: .\\setup-semantic-embedding.ps1");
                    throw new InvalidOperationException("Invalid embedding configuration - see logs for details");
                }
            }
            else
            {
                _logger.LogInformation("✓ Configuration is valid");
            }
        }

        return _globalConfig;
    }

    /// <summary>
    /// Load project-specific config or create with auto-detection
    /// </summary>
    public async Task<ProjectSemanticConfig> LoadProjectConfigAsync(
        string projectDir,
        Solution solution,
        bool forceAutoDetect = false)
    {
        var configPath = Path.Combine(projectDir, ".sharptools", "semantic-config.json");

        ProjectSemanticConfig config;

        if (!File.Exists(configPath) || forceAutoDetect)
        {
            _logger.LogInformation("Project config not found or force auto-detect, analyzing codebase...");
            config = await CreateProjectConfigWithAutoDetectionAsync(solution);

            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await SaveProjectConfigAsync(config, configPath);
        }
        else
        {
            _logger.LogInformation("Loading project config from: {Path}", configPath);
            var json = await File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<ProjectSemanticConfig>(json) ?? new ProjectSemanticConfig();

            // Auto-detect if config says "auto"
            if (config.Codebase.Size == "auto" || config.Codebase.Language == "auto")
            {
                _logger.LogInformation("Config has 'auto' values, performing detection...");
                config = await CreateProjectConfigWithAutoDetectionAsync(solution);
                await SaveProjectConfigAsync(config, configPath);
            }
        }

        _projectConfig = config;
        return config;
    }

    /// <summary>
    /// Create project config with full auto-detection
    /// </summary>
    private async Task<ProjectSemanticConfig> CreateProjectConfigWithAutoDetectionAsync(Solution solution)
    {
        var config = new ProjectSemanticConfig();

        try
        {
            // Detect size
            var sizeStats = await _sizeDetector.AnalyzeAsync(solution);
            config.Codebase.Size = sizeStats.SizeCategory;
            config.VectorStore.Engine = sizeStats.RecommendedVectorStore;

            // Detect language
            var langStats = await _languageDetector.AnalyzeAsync(solution);
            config.Codebase.Language = langStats.RecommendedLanguage(config.Codebase.MultilingualThreshold);

            // Save stats
            config.Codebase.Stats = new CodebaseStats
            {
                TotalFiles = sizeStats.TotalFiles,
                TotalComments = langStats.TotalComments,
                NonEnglishWords = langStats.NonEnglishWords,
                TotalWords = langStats.TotalWords,
                NonEnglishPercentage = langStats.NonEnglishPercentage
            };

            _logger.LogInformation(
                "Auto-detection complete: Size={Size}, Files={Files}, Language={Lang} ({NonEnglish:F1}% non-English)",
                config.Codebase.Size, sizeStats.TotalFiles, config.Codebase.Language, langStats.NonEnglishPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-detection failed, using defaults");
        }

        return config;
    }

    private void ApplyEnvironmentOverrides(SemanticEmbeddingConfig config)
    {
        // SEMANTIC_PLATFORM override
        var platform = Environment.GetEnvironmentVariable("SEMANTIC_PLATFORM");
        if (!string.IsNullOrEmpty(platform))
        {
            config.Embedding.Platform = platform;
            _logger.LogInformation("ENV override: Platform = {Platform}", platform);
        }

        // SEMANTIC_ARCHITECTURE override
        var arch = Environment.GetEnvironmentVariable("SEMANTIC_ARCHITECTURE");
        if (!string.IsNullOrEmpty(arch))
        {
            config.Embedding.Architecture = arch;
            _logger.LogInformation("ENV override: Architecture = {Arch}", arch);
        }

        // TEI_ENDPOINT override
        var teiEndpoint = Environment.GetEnvironmentVariable("TEI_ENDPOINT");
        if (!string.IsNullOrEmpty(teiEndpoint))
        {
            config.Embedding.Tei.Endpoint = teiEndpoint;
            _logger.LogInformation("ENV override: TEI Endpoint = {Endpoint}", teiEndpoint);
        }

        // OLLAMA_ENDPOINT override
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            config.Embedding.Ollama.Endpoint = ollamaEndpoint;
            _logger.LogInformation("ENV override: Ollama Endpoint = {Endpoint}", ollamaEndpoint);
        }
    }

    private async Task SaveGlobalConfigAsync(SemanticEmbeddingConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Global config saved to: {Path}", path);
    }

    private async Task SaveProjectConfigAsync(ProjectSemanticConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Project config saved to: {Path}", path);
    }

    private static string GetDefaultGlobalConfigPath()
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var exeDir = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(exeDir, "semantic-config.json");
    }

    private static SemanticEmbeddingConfig CreateDefaultGlobalConfig()
    {
        return new SemanticEmbeddingConfig
        {
            Embedding = new EmbeddingSettings
            {
                Platform = "tei",
                Architecture = "auto",
                Tei = new TeiSettings(),
                Ollama = new OllamaSettings(),
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
}

using UltrasharpTools.Tools.Config;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Manages semantic embedding configuration with ENV override support, validation, and auto-configuration
/// </summary>
public partial class SemanticConfigManager
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
        AutoConfigurationService autoConfig
    )
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
        CancellationToken cancellationToken = default
    )
    {
        if (_globalConfig != null)
            return _globalConfig;

        configPath ??= GetDefaultGlobalConfigPath();

        // First run: no config exists - auto-configure
        if (!File.Exists(configPath))
        {
            LogSeparator();
            LogFirstTimeSetup();
            LogSeparator();
            LogEmptyLine();

            var autoConfigResult = await _autoConfig.AutoConfigureAsync(cancellationToken);
            _globalConfig = autoConfigResult.Config;

            await SaveGlobalConfigAsync(_globalConfig, configPath);

            LogEmptyLine();
            LogConfigSaved(configPath);
            LogEmptyLine();

            // Print auto-config results
            if (autoConfigResult.RequiresSetup)
            {
                LogSetupRequired();
                LogWarningEmptyLine();
                LogSetupInstructions(autoConfigResult.SetupInstructions);
            }
            else
            {
                LogReadyToUse();
            }

            LogInfoSeparator();
            LogEmptyLine();
        }
        else
        {
            // Config exists - load and validate
            LogLoadingGlobalConfig(configPath);
            var json = await OptimizedFileIO.ReadAllTextAsync(configPath, null, cancellationToken);
            _globalConfig =
                JsonSerializer.Deserialize<SemanticEmbeddingConfig>(json)
                ?? CreateDefaultGlobalConfig();

            // Apply ENV overrides
            ApplyEnvironmentOverrides(_globalConfig);

            // Validate configuration
            LogValidating();
            var validation = await _validator.ValidateGlobalConfigAsync(
                _globalConfig,
                cancellationToken
            );

            if (!validation.IsValid || validation.HasWarnings)
            {
                LogWarningEmptyLine();
                _validator.PrintValidationResults(validation);

                if (!validation.IsValid)
                {
                    LogConfigInvalid();
                    LogFixInstructions();
                    throw new InvalidOperationException(
                        "Invalid embedding configuration - see logs for details"
                    );
                }
            }
            else
            {
                LogConfigValid();
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
        bool forceAutoDetect = false
    )
    {
        var configPath = Path.Combine(projectDir, ".sharptools", "semantic-config.json");

        ProjectSemanticConfig config;

        if (!File.Exists(configPath) || forceAutoDetect)
        {
            LogAnalyzingCodebase();
            config = await CreateProjectConfigWithAutoDetectionAsync(solution);

            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await SaveProjectConfigAsync(config, configPath);
        }
        else
        {
            LogLoadingProjectConfig(configPath);
            var json = await OptimizedFileIO.ReadAllTextAsync(configPath);
            config =
                JsonSerializer.Deserialize<ProjectSemanticConfig>(json)
                ?? new ProjectSemanticConfig();

            // Auto-detect if config says "auto"
            if (config.Codebase.Size == "auto" || config.Codebase.Language == "auto")
            {
                LogAutoDetecting();
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
    private async Task<ProjectSemanticConfig> CreateProjectConfigWithAutoDetectionAsync(
        Solution solution
    )
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
            config.Codebase.Language = langStats.RecommendedLanguage(
                config.Codebase.MultilingualThreshold
            );

            // Save stats
            config.Codebase.Stats = new CodebaseStats
            {
                TotalFiles = sizeStats.TotalFiles,
                TotalComments = langStats.TotalComments,
                NonEnglishWords = langStats.NonEnglishWords,
                TotalWords = langStats.TotalWords,
                NonEnglishPercentage = langStats.NonEnglishPercentage,
            };

            LogAutoDetectionComplete(config.Codebase.Size, sizeStats.TotalFiles, config.Codebase.Language, langStats.NonEnglishPercentage);
        }
        catch (Exception ex)
        {
            LogAutoDetectionFailed(ex);
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
            LogEnvPlatformOverride(platform);
        }

        // SEMANTIC_ARCHITECTURE override
        var arch = Environment.GetEnvironmentVariable("SEMANTIC_ARCHITECTURE");
        if (!string.IsNullOrEmpty(arch))
        {
            config.Embedding.Architecture = arch;
            LogEnvArchOverride(arch);
        }

        // TEI_ENDPOINT override
        var teiEndpoint = Environment.GetEnvironmentVariable("TEI_ENDPOINT");
        if (!string.IsNullOrEmpty(teiEndpoint))
        {
            config.Embedding.Tei.Endpoint = teiEndpoint;
            LogEnvTeiEndpointOverride(teiEndpoint);
        }

        // OLLAMA_ENDPOINT override
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            config.Embedding.Ollama.Endpoint = ollamaEndpoint;
            LogEnvOllamaEndpointOverride(ollamaEndpoint);
        }
    }

    private async Task SaveGlobalConfigAsync(SemanticEmbeddingConfig config, string path)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        await OptimizedFileIO.WriteAllTextAsync(path, json);
        LogGlobalConfigSaved(path);
    }

    private async Task SaveProjectConfigAsync(ProjectSemanticConfig config, string path)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        await OptimizedFileIO.WriteAllTextAsync(path, json);
        LogProjectConfigSaved(path);
    }

    /// <summary>
    /// Get default global config path (Config\semantic-config.json or fallback to semantic-config.json)
    /// </summary>
    private static string GetDefaultGlobalConfigPath()
    {
        // Use central config directory for all configurations
        var configDir = ProjectPathHelper.GetConfigPath();
        return Path.Combine(configDir, "semantic-config.json");
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
}

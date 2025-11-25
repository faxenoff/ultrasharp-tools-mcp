using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Загрузчик конфигурации для Universal Semantic Mode
/// </summary>
public sealed partial class SemanticModeConfigurationLoader {
    private readonly ILogger<SemanticModeConfigurationLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Централизованная директория конфигурации
    // Windows: %LOCALAPPDATA%\UltraSharpTools\config
    // Linux/macOS: ~/.ultrasharp/config
    private static string GetCentralConfigDir() {
        if (OperatingSystem.IsWindows()) {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "UltraSharpTools", "config");
        } else {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".ultrasharp", "config");
        }
    }

    /// <summary>
    /// Возвращает путь к централизованной директории конфигурации
    /// </summary>
    public static string CentralConfigDirectory => GetCentralConfigDir();

    // Единый конфиг semantic-config.json (объединяет embedding settings + mode settings)
    private static readonly Lazy<string> UnifiedConfigPath = new(() =>
        Path.Combine(GetCentralConfigDir(), "semantic-config.json"));

    // Legacy: для обратной совместимости
    [Obsolete("Use UnifiedConfigPath. Will be removed in v4.0")]
    private static readonly Lazy<string[]> ConfigPaths = new(() => new[]
    {
        UnifiedConfigPath.Value,
    });

    public SemanticModeConfigurationLoader(ILogger<SemanticModeConfigurationLoader> logger) {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
    }

    /// <summary>
    /// Загружает конфигурацию из файла или создаёт default.
    /// Читает unified semantic-config.json который содержит и embedding settings и mode settings.
    /// </summary>
    public async Task<SemanticModeConfig> LoadOrCreateAsync(
        string? explicitPath = null,
        CancellationToken cancellationToken = default
    ) {
        // 1. Explicit path имеет приоритет
        if (!string.IsNullOrWhiteSpace(explicitPath)) {
            if (File.Exists(explicitPath)) {
                LogLoadingFromExplicitPath(explicitPath);
                return await LoadFromFileAsync(explicitPath, cancellationToken);
            } else {
                LogExplicitPathNotFound(explicitPath);
            }
        }

        // 2. Проверяем unified config (semantic-config.json)
        var unifiedPath = UnifiedConfigPath.Value;
        if (File.Exists(unifiedPath)) {
            LogFoundConfig(unifiedPath);
            try {
                return await LoadFromUnifiedConfigAsync(unifiedPath, cancellationToken);
            } catch (Exception ex) {
                LogLoadConfigFailed(ex, unifiedPath);
            }
        }

        // 3. Конфигурация не найдена - создаём default (disabled)
        LogUsingDefaultConfig();
        var defaultConfig = SemanticModeConfig.CreateDefault();
        defaultConfig.Enabled = false; // Disabled until user runs setup script

        return defaultConfig;
    }
    /// <summary>
    /// Загружает SemanticModeConfig из unified semantic-config.json
    /// </summary>
    private async Task<SemanticModeConfig> LoadFromUnifiedConfigAsync(
        string path,
        CancellationToken cancellationToken
    ) {
        try {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var root = doc.RootElement;

            // Check if semantic mode is enabled
            var enabled = root.TryGetProperty("enabled", out var enabledProp)
                && enabledProp.ValueKind == JsonValueKind.True;

            if (!enabled) {
                // Check alternative property name (camelCase)
                enabled = root.TryGetProperty("Enabled", out enabledProp)
                    && enabledProp.ValueKind == JsonValueKind.True;
            }

            // Create config with enabled status from file
            var config = SemanticModeConfig.CreateDefault();
            config.Enabled = enabled;

            // Parse embedding settings
            if (root.TryGetProperty("embedding", out var embeddingProp)) {
                config.Embedding = ParseEmbeddingSettings(embeddingProp);
            }

            LogLoadedConfig(config.Enabled, config.Enrichment.TimeoutSeconds, config.ToolSettings.Count);

            return config;
        } catch (Exception ex) {
            LogLoadConfigError(ex, path);
            throw;
        }
    }

    /// <summary>
    /// Загружает конфигурацию из файла
    /// </summary>
    private async Task<SemanticModeConfig> LoadFromFileAsync(
        string path,
        CancellationToken cancellationToken
    ) {
        try {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var config = JsonSerializer.Deserialize<SemanticModeConfig>(json, _jsonOptions);

            if (config == null) {
                throw new InvalidOperationException($"Failed to deserialize config from {path}");
            }

            // Валидация
            if (!config.Validate(out var errorMessage)) {
                throw new InvalidOperationException($"Invalid config in {path}: {errorMessage}");
            }

            LogLoadedConfig(config.Enabled, config.Enrichment.TimeoutSeconds, config.ToolSettings.Count);

            return config;
        } catch (Exception ex) {
            LogLoadConfigError(ex, path);
            throw;
        }
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public async Task SaveToFileAsync(
        SemanticModeConfig config,
        string path,
        CancellationToken cancellationToken = default
    ) {
        // Валидация перед сохранением
        if (!config.Validate(out var errorMessage)) {
            throw new InvalidOperationException($"Cannot save invalid config: {errorMessage}");
        }

        // Создаём директорию если не существует
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
            LogCreatedDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);

        LogSavedConfig(path);
    }

    /// <summary>
    /// Проверяет существование конфигурации
    /// </summary>
    public string? FindConfigPath() {
        var unifiedPath = UnifiedConfigPath.Value;
        if (File.Exists(unifiedPath)) {
            return Path.GetFullPath(unifiedPath);
        }

        return null;
    }

    /// <summary>
    /// Создаёт example конфигурацию с комментариями
    /// </summary>
    public async Task CreateExampleConfigAsync(
        string path,
        CancellationToken cancellationToken = default
    ) {
        var config = SemanticModeConfig.CreateDefault();

        // Добавляем пример custom query template
        if (config.ToolSettings.TryGetValue("view_definition", out var viewDefSettings)) {
            viewDefSettings.QueryTemplate =
                "Find similar implementations of {toolName} for better understanding";
        }

        await SaveToFileAsync(config, path, cancellationToken);

        // Добавляем header comment вручную (JSON не поддерживает comments в serialize)
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var header = """
            {
              "$schema": "./semantic-mode-config.schema.json",
              // Universal Semantic Mode Configuration (Phase 12)
              // This config controls semantic enrichment for all 52 tools
              // See: https://github.com/your-org/ultrasharp-tools-mcp/docs/semantic-mode.md

            """.TrimEnd();

        json = string.Concat(header, json.AsSpan(1)); // Replace opening brace
        await File.WriteAllTextAsync(path, json, cancellationToken);

        LogCreatedExampleConfig(path);
    }

    /// <summary>
    /// Перезагружает конфигурацию (hot reload)
    /// </summary>
    public async Task<SemanticModeConfig> ReloadAsync(
        string? currentPath = null,
        CancellationToken cancellationToken = default
    ) {
        var path = currentPath ?? FindConfigPath();

        if (path != null) {
            LogReloading(path);
            return await LoadFromFileAsync(path, cancellationToken);
        } else {
            LogNoPathForReload();
            return SemanticModeConfig.CreateDefault();
        }
    }

    /// <summary>
    /// Мержит конфигурацию с overrides
    /// </summary>
    public SemanticModeConfig MergeWithOverrides(
        SemanticModeConfig baseConfig,
        SemanticModeConfig overrides
    ) {
        var merged = JsonSerializer.Deserialize<SemanticModeConfig>(
            JsonSerializer.Serialize(baseConfig, _jsonOptions),
            _jsonOptions
        )!;

        // Override top-level settings
        if (overrides.Enabled != baseConfig.Enabled) {
            merged.Enabled = overrides.Enabled;
        }

        // Merge availability settings (non-default values only)
        // Merge enrichment settings (non-default values only)
        // Merge tool settings (deep merge)

        foreach (var (toolName, overrideSettings) in overrides.ToolSettings) {
            if (merged.ToolSettings.TryGetValue(toolName, out var existing)) {
                // Update existing
                existing.Enabled = overrideSettings.Enabled;
                existing.TopK = overrideSettings.TopK;
                existing.Threshold = overrideSettings.Threshold;
                if (!string.IsNullOrWhiteSpace(overrideSettings.QueryTemplate)) {
                    existing.QueryTemplate = overrideSettings.QueryTemplate;
                }
            } else {
                // Add new
                merged.ToolSettings[toolName] = overrideSettings;
            }
        }

        if (!merged.Validate(out var errorMessage)) {
            LogMergedConfigInvalid(errorMessage ?? "Unknown validation error");
            return baseConfig;
        }

        LogMergedConfig();
        return merged;
    }
    private static SemanticModeConfig.EmbeddingProviderSettings ParseEmbeddingSettings(JsonElement element) {
        var settings = new SemanticModeConfig.EmbeddingProviderSettings();

        if (element.TryGetProperty("platform", out var platformProp)) {
            settings.Platform = platformProp.GetString() ?? "ollama";
        }

        if (element.TryGetProperty("architecture", out var archProp)) {
            settings.Architecture = archProp.GetString();
        }

        if (element.TryGetProperty("tei", out var teiProp)) {
            settings.Tei = new SemanticModeConfig.EmbeddingProviderSettings.TeiSettings();

            if (teiProp.TryGetProperty("endpoint", out var endpointProp)) {
                settings.Tei.Endpoint = endpointProp.GetString() ?? "http://127.0.0.1:8080";
            }

            if (teiProp.TryGetProperty("selected_model", out var modelProp)) {
                settings.Tei.SelectedModel = modelProp.GetString();
            }

            if (teiProp.TryGetProperty("models", out var modelsProp) && modelsProp.ValueKind == JsonValueKind.Array) {
                settings.Tei.Models = [];
                foreach (var modelEl in modelsProp.EnumerateArray()) {
                    var modelInfo = new SemanticModeConfig.EmbeddingProviderSettings.ModelInfo();
                    if (modelEl.TryGetProperty("id", out var idProp)) {
                        modelInfo.Id = idProp.GetString();
                    }
                    if (modelEl.TryGetProperty("vector_size", out var sizeProp)) {
                        modelInfo.VectorSize = sizeProp.GetInt32();
                    }
                    settings.Tei.Models.Add(modelInfo);
                }
            }
        }

        if (element.TryGetProperty("ollama", out var ollamaProp)) {
            settings.Ollama = new SemanticModeConfig.EmbeddingProviderSettings.OllamaSettings();

            if (ollamaProp.TryGetProperty("endpoint", out var endpointProp)) {
                settings.Ollama.Endpoint = endpointProp.GetString() ?? "http://127.0.0.1:11434";
            }

            if (ollamaProp.TryGetProperty("selected_model", out var modelProp)) {
                settings.Ollama.SelectedModel = modelProp.GetString();
            }
        }

        return settings;
    }
}

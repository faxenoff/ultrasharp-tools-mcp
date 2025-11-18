using Microsoft.Extensions.Logging;
using System.Text.Json;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Загрузчик конфигурации для Universal Semantic Mode
/// </summary>
public sealed class SemanticModeConfigurationLoader
{
    private readonly ILogger<SemanticModeConfigurationLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Пути поиска конфигурации (в порядке приоритета)
    private static readonly string[] ConfigPaths = new[]
    {
        ".ultrasharp/semantic-mode-config.json",           // Project-specific
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ultrasharp", "semantic-mode-config.json"), // User-specific
        "semantic-mode-config.json",                       // Current directory
        "Run.Config/semantic-mode-config.json"             // Default location
    };

    public SemanticModeConfigurationLoader(ILogger<SemanticModeConfigurationLoader> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Загружает конфигурацию из файла или создаёт default
    /// </summary>
    public async Task<SemanticModeConfig> LoadOrCreateAsync(string? explicitPath = null, CancellationToken cancellationToken = default)
    {
        // 1. Explicit path имеет приоритет
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
            {
                _logger.LogInformation("Loading semantic mode config from explicit path: {Path}", explicitPath);
                return await LoadFromFileAsync(explicitPath, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Explicit config path not found: {Path}, using default", explicitPath);
            }
        }

        // 2. Поиск конфигурации в стандартных путях
        foreach (var configPath in ConfigPaths)
        {
            if (File.Exists(configPath))
            {
                _logger.LogInformation("Found semantic mode config: {Path}", configPath);
                try
                {
                    return await LoadFromFileAsync(configPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load config from {Path}, trying next location", configPath);
                }
            }
        }

        // 3. Конфигурация не найдена - создаём default
        _logger.LogInformation("No semantic mode config found, using default configuration");
        var defaultConfig = SemanticModeConfig.CreateDefault();

        // Сохраняем default конфигурацию для будущего использования
        var defaultPath = ".ultrasharp/semantic-mode-config.json";
        try
        {
            await SaveToFileAsync(defaultConfig, defaultPath, cancellationToken);
            _logger.LogInformation("Saved default semantic mode config to {Path}", defaultPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save default config to {Path}", defaultPath);
        }

        return defaultConfig;
    }

    /// <summary>
    /// Загружает конфигурацию из файла
    /// </summary>
    private async Task<SemanticModeConfig> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var config = JsonSerializer.Deserialize<SemanticModeConfig>(json, _jsonOptions);

            if (config == null)
            {
                throw new InvalidOperationException($"Failed to deserialize config from {path}");
            }

            // Валидация
            if (!config.Validate(out var errorMessage))
            {
                throw new InvalidOperationException($"Invalid config in {path}: {errorMessage}");
            }

            _logger.LogDebug(
                "Loaded semantic mode config: enabled={Enabled}, enrichmentTimeout={Timeout}s, tools={ToolCount}",
                config.Enabled,
                config.Enrichment.TimeoutSeconds,
                config.ToolSettings.Count);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load semantic mode config from {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public async Task SaveToFileAsync(SemanticModeConfig config, string path, CancellationToken cancellationToken = default)
    {
        // Валидация перед сохранением
        if (!config.Validate(out var errorMessage))
        {
            throw new InvalidOperationException($"Cannot save invalid config: {errorMessage}");
        }

        // Создаём директорию если не существует
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created directory: {Directory}", directory);
        }

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);

        _logger.LogInformation("Saved semantic mode config to {Path}", path);
    }

    /// <summary>
    /// Проверяет существование конфигурации
    /// </summary>
    public string? FindConfigPath()
    {
        foreach (var configPath in ConfigPaths)
        {
            if (File.Exists(configPath))
            {
                return Path.GetFullPath(configPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Создаёт example конфигурацию с комментариями
    /// </summary>
    public async Task CreateExampleConfigAsync(string path, CancellationToken cancellationToken = default)
    {
        var config = SemanticModeConfig.CreateDefault();

        // Добавляем пример custom query template
        if (config.ToolSettings.TryGetValue("view_definition", out var viewDefSettings))
        {
            viewDefSettings.QueryTemplate = "Find similar implementations of {toolName} for better understanding";
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

        json = header + json.Substring(1); // Replace opening brace
        await File.WriteAllTextAsync(path, json, cancellationToken);

        _logger.LogInformation("Created example semantic mode config at {Path}", path);
    }

    /// <summary>
    /// Перезагружает конфигурацию (hot reload)
    /// </summary>
    public async Task<SemanticModeConfig> ReloadAsync(string? currentPath = null, CancellationToken cancellationToken = default)
    {
        var path = currentPath ?? FindConfigPath();

        if (path != null)
        {
            _logger.LogInformation("Reloading semantic mode config from {Path}", path);
            return await LoadFromFileAsync(path, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No config path found for reload, returning default");
            return SemanticModeConfig.CreateDefault();
        }
    }

    /// <summary>
    /// Мержит конфигурацию с overrides
    /// </summary>
    public SemanticModeConfig MergeWithOverrides(SemanticModeConfig baseConfig, SemanticModeConfig overrides)
    {
        var merged = JsonSerializer.Deserialize<SemanticModeConfig>(
            JsonSerializer.Serialize(baseConfig, _jsonOptions),
            _jsonOptions)!;

        // Override top-level settings
        if (overrides.Enabled != baseConfig.Enabled)
        {
            merged.Enabled = overrides.Enabled;
        }

        // Merge availability settings (non-default values only)
        // Merge enrichment settings (non-default values only)
        // Merge tool settings (deep merge)

        foreach (var (toolName, overrideSettings) in overrides.ToolSettings)
        {
            if (merged.ToolSettings.ContainsKey(toolName))
            {
                // Update existing
                var existing = merged.ToolSettings[toolName];
                existing.Enabled = overrideSettings.Enabled;
                existing.TopK = overrideSettings.TopK;
                existing.Threshold = overrideSettings.Threshold;
                if (!string.IsNullOrWhiteSpace(overrideSettings.QueryTemplate))
                {
                    existing.QueryTemplate = overrideSettings.QueryTemplate;
                }
            }
            else
            {
                // Add new
                merged.ToolSettings[toolName] = overrideSettings;
            }
        }

        if (!merged.Validate(out var errorMessage))
        {
            _logger.LogWarning("Merged config is invalid: {Error}, using base config", errorMessage);
            return baseConfig;
        }

        _logger.LogDebug("Merged semantic mode config with overrides");
        return merged;
    }
}

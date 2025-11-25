using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Сервис для загрузки и управления конфигурацией routing
/// </summary>
public sealed partial class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
    }

    /// <summary>
    /// Загружает конфигурацию из файла или создаёт default
    /// </summary>
    /// <param name="solutionPath">Путь к .sln файлу (опционально)</param>
    /// <returns>Конфигурация маршрутизации</returns>
    public ToolRoutingConfig LoadOrCreateConfig(string? solutionPath = null)
    {
        var configPath = GetConfigPath(solutionPath);

        // Если файл существует - загружаем
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ToolRoutingConfig>(json, _jsonOptions);

                if (config != null)
                {
                    if (config.Validate(out var errorMessage))
                    {
                        LogConfigLoaded(configPath);
                        return config;
                    }
                    else
                    {
                        LogInvalidConfig(configPath, errorMessage ?? "Unknown validation error");
                    }
                }
            }
            catch (Exception ex)
            {
                LogLoadConfigFailed(ex, configPath);
            }
        }
        else
        {
            LogConfigNotFound(configPath);
        }

        // Создаём default конфигурацию
        var defaultConfig = ToolRoutingConfig.CreateDefault();

        // Сохраняем для будущего использования
        try
        {
            SaveConfig(defaultConfig, configPath);
            LogDefaultConfigCreated(configPath);
        }
        catch (Exception ex)
        {
            LogSaveDefaultFailed(ex, configPath);
        }

        return defaultConfig;
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public void SaveConfig(ToolRoutingConfig config, string? solutionPath = null)
    {
        var configPath = string.IsNullOrEmpty(solutionPath)
            ? GetConfigPath(solutionPath)
            : solutionPath;

        // Создаём директорию если не существует
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(configPath, json);

        LogConfigSaved(configPath);
    }

    /// <summary>
    /// Получает путь к файлу конфигурации
    /// </summary>
    private string GetConfigPath(string? solutionPath)
    {
        if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
        {
            // Если указан solution - создаём .ultrasharp рядом с ним
            var solutionDir = Path.GetDirectoryName(solutionPath);
            if (!string.IsNullOrEmpty(solutionDir))
            {
                return Path.Combine(solutionDir, ".ultrasharp", "overlord-config.json");
            }
        }

        // По умолчанию - в текущей директории
        return Path.Combine(Directory.GetCurrentDirectory(), ".ultrasharp", "overlord-config.json");
    }
}

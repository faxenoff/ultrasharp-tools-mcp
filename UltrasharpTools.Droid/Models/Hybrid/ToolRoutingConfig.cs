using System.Text.Json.Serialization;

namespace UltrasharpTools.Droid.Models.Hybrid;

/// <summary>
/// Конфигурация маршрутизации инструментов для Hybrid Mode
/// </summary>
public sealed class ToolRoutingConfig
{
    /// <summary>
    /// URL Overlord сервера
    /// </summary>
    [JsonPropertyName("overlordUrl")]
    public string OverlordUrl { get; set; } = "http://localhost:3001";

    /// <summary>
    /// Включить hybrid mode
    /// </summary>
    [JsonPropertyName("enableHybridMode")]
    public bool EnableHybridMode { get; set; } = true;

    /// <summary>
    /// Fallback на локальные tools при недоступности Overlord
    /// </summary>
    [JsonPropertyName("fallbackToLocal")]
    public bool FallbackToLocal { get; set; } = true;

    /// <summary>
    /// Таймаут health check в секундах
    /// </summary>
    [JsonPropertyName("healthCheckTimeoutSeconds")]
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Интервал health check в секундах
    /// </summary>
    [JsonPropertyName("healthCheckIntervalSeconds")]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Правила маршрутизации для конкретных инструментов
    /// Возможные значения: "local", "overlord", "overlord_with_fallback"
    /// </summary>
    [JsonPropertyName("routingRules")]
    public Dictionary<string, string> RoutingRules { get; set; } =
        new()
        {
            // Semantic tools - всегда Overlord (cross-project vector search)
            ["semantic_search"] = "overlord",
            ["semantic_diff"] = "overlord",
            ["find_duplicates"] = "overlord",

            // Batch analysis tools - всегда локально (requires loaded solution + full Roslyn)
            ["detect_code_clones"] = "local",

            // Hybrid tools - Overlord с fallback
            ["pattern_search"] = "overlord_with_fallback",
            ["analyze_complexity"] = "overlord_with_fallback",

            // Local tools - всегда локально
            ["view_definition"] = "local",
            ["get_members"] = "local",
            ["load_solution"] = "local",
            ["load_project"] = "local",
        };

    /// <summary>
    /// Создаёт конфигурацию по умолчанию
    /// </summary>
    public static ToolRoutingConfig CreateDefault()
    {
        return new ToolRoutingConfig();
    }

    /// <summary>
    /// Валидация конфигурации
    /// </summary>
    public bool Validate(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(OverlordUrl))
        {
            errorMessage = "OverlordUrl cannot be empty";
            return false;
        }

        if (!Uri.TryCreate(OverlordUrl, UriKind.Absolute, out _))
        {
            errorMessage = $"Invalid OverlordUrl: {OverlordUrl}";
            return false;
        }

        if (HealthCheckTimeoutSeconds <= 0)
        {
            errorMessage = "HealthCheckTimeoutSeconds must be positive";
            return false;
        }

        if (HealthCheckIntervalSeconds <= 0)
        {
            errorMessage = "HealthCheckIntervalSeconds must be positive";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

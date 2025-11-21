using System.Text.Json.Serialization;

namespace UltrasharpTools.Droid.Models.Hybrid;

/// <summary>
/// Конфигурация для Universal Semantic Mode (Phase 12)
/// Настройки semantic enrichment для всех 52 инструментов
/// </summary>
public sealed class SemanticModeConfig
{
    /// <summary>
    /// Включить Universal Semantic Mode
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Настройки availability check
    /// </summary>
    [JsonPropertyName("availability")]
    public AvailabilitySettings Availability { get; set; } = new();

    /// <summary>
    /// Настройки enrichment
    /// </summary>
    [JsonPropertyName("enrichment")]
    public EnrichmentSettings Enrichment { get; set; } = new();

    /// <summary>
    /// Настройки для конкретных инструментов
    /// </summary>
    [JsonPropertyName("toolSettings")]
    public Dictionary<string, ToolEnrichmentSettings> ToolSettings { get; set; } = new();

    /// <summary>
    /// Создаёт конфигурацию по умолчанию
    /// </summary>
    public static SemanticModeConfig CreateDefault()
    {
        return new SemanticModeConfig
        {
            Enabled = true,
            Availability = AvailabilitySettings.CreateDefault(),
            Enrichment = EnrichmentSettings.CreateDefault(),
            ToolSettings = CreateDefaultToolSettings(),
        };
    }

    /// <summary>
    /// Валидация конфигурации
    /// </summary>
    public bool Validate(out string? errorMessage)
    {
        if (!Availability.Validate(out errorMessage))
        {
            return false;
        }

        if (!Enrichment.Validate(out errorMessage))
        {
            return false;
        }

        // Валидация tool settings
        foreach (var (toolName, settings) in ToolSettings)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                errorMessage = "Tool name cannot be empty";
                return false;
            }

            if (!settings.Validate(out errorMessage))
            {
                errorMessage = $"Invalid settings for tool '{toolName}': {errorMessage}";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static Dictionary<string, ToolEnrichmentSettings> CreateDefaultToolSettings()
    {
        return new Dictionary<string, ToolEnrichmentSettings>
        {
            // Phase 12.1 - Core strategies (5)
            ["view_definition"] = new() { TopK = 5, Threshold = 0.7 },
            ["find_references"] = new() { TopK = 10, Threshold = 0.65 },
            ["modify_code"] = new() { TopK = 8, Threshold = 0.7 },
            ["get_members"] = new() { TopK = 5, Threshold = 0.7 },
            ["analyze_complexity"] = new() { TopK = 5, Threshold = 0.7 },

            // Phase 12.2 - Extended strategies (10)
            ["find_all_references"] = new() { TopK = 10, Threshold = 0.65 },
            ["list_types"] = new() { TopK = 8, Threshold = 0.7 },
            ["search_symbols"] = new() { TopK = 10, Threshold = 0.6 },
            ["trace_execution"] = new() { TopK = 5, Threshold = 0.75 },
            ["analyze_code_style"] = new() { TopK = 5, Threshold = 0.7 },
            ["get_type_hierarchy"] = new() { TopK = 5, Threshold = 0.75 },
            ["get_project_structure"] = new() { TopK = 3, Threshold = 0.65 },
            ["find_usages"] = new() { TopK = 8, Threshold = 0.7 },
            ["get_diagnostics"] = new() { TopK = 5, Threshold = 0.7 },
            ["apply_code_fixes"] = new() { TopK = 5, Threshold = 0.75 },
        };
    }
}

/// <summary>
/// Настройки availability check
/// </summary>
public sealed class AvailabilitySettings
{
    /// <summary>
    /// Интервал validity кэша (в секундах)
    /// </summary>
    [JsonPropertyName("cacheValiditySeconds")]
    public int CacheValiditySeconds { get; set; } = 300; // 5 минут

    /// <summary>
    /// Таймаут для проверки Local embedding (в секундах)
    /// </summary>
    [JsonPropertyName("localCheckTimeoutSeconds")]
    public int LocalCheckTimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Таймаут для проверки Overlord (в секундах)
    /// </summary>
    [JsonPropertyName("overlordCheckTimeoutSeconds")]
    public int OverlordCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Предпочтение источника для embedding
    /// Возможные значения: "local", "overlord", "auto"
    /// </summary>
    [JsonPropertyName("embeddingSourcePreference")]
    public string EmbeddingSourcePreference { get; set; } = "auto";

    /// <summary>
    /// Предпочтение источника для search
    /// Возможные значения: "local", "overlord", "auto"
    /// </summary>
    [JsonPropertyName("searchSourcePreference")]
    public string SearchSourcePreference { get; set; } = "auto";

    public static AvailabilitySettings CreateDefault() => new();

    public bool Validate(out string? errorMessage)
    {
        if (CacheValiditySeconds <= 0)
        {
            errorMessage = "CacheValiditySeconds must be positive";
            return false;
        }

        if (LocalCheckTimeoutSeconds <= 0)
        {
            errorMessage = "LocalCheckTimeoutSeconds must be positive";
            return false;
        }

        if (OverlordCheckTimeoutSeconds <= 0)
        {
            errorMessage = "OverlordCheckTimeoutSeconds must be positive";
            return false;
        }

        var validPreferences = new[] { "local", "overlord", "auto" };
        if (!validPreferences.Contains(EmbeddingSourcePreference.ToLowerInvariant()))
        {
            errorMessage =
                $"Invalid EmbeddingSourcePreference: {EmbeddingSourcePreference}. Must be one of: {string.Join(", ", validPreferences)}";
            return false;
        }

        if (!validPreferences.Contains(SearchSourcePreference.ToLowerInvariant()))
        {
            errorMessage =
                $"Invalid SearchSourcePreference: {SearchSourcePreference}. Must be one of: {string.Join(", ", validPreferences)}";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

/// <summary>
/// Настройки enrichment
/// </summary>
public sealed class EnrichmentSettings
{
    /// <summary>
    /// Таймаут для enrichment операций (в секундах)
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Максимальное количество concurrent enrichment операций
    /// </summary>
    [JsonPropertyName("maxConcurrency")]
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Graceful degradation при ошибках
    /// </summary>
    [JsonPropertyName("gracefulDegradation")]
    public bool GracefulDegradation { get; set; } = true;

    /// <summary>
    /// Логировать successful enrichments
    /// </summary>
    [JsonPropertyName("logSuccessfulEnrichments")]
    public bool LogSuccessfulEnrichments { get; set; } = false;

    /// <summary>
    /// Логировать failed enrichments
    /// </summary>
    [JsonPropertyName("logFailedEnrichments")]
    public bool LogFailedEnrichments { get; set; } = true;

    /// <summary>
    /// Минимальное количество matches для enrichment
    /// </summary>
    [JsonPropertyName("minMatchesForEnrichment")]
    public int MinMatchesForEnrichment { get; set; } = 1;

    public static EnrichmentSettings CreateDefault() => new();

    public bool Validate(out string? errorMessage)
    {
        if (TimeoutSeconds <= 0)
        {
            errorMessage = "TimeoutSeconds must be positive";
            return false;
        }

        if (MaxConcurrency <= 0)
        {
            errorMessage = "MaxConcurrency must be positive";
            return false;
        }

        if (MinMatchesForEnrichment < 0)
        {
            errorMessage = "MinMatchesForEnrichment cannot be negative";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

/// <summary>
/// Настройки enrichment для конкретного инструмента
/// </summary>
public sealed class ToolEnrichmentSettings
{
    /// <summary>
    /// Включить enrichment для этого инструмента
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Top K results для semantic search
    /// </summary>
    [JsonPropertyName("topK")]
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Threshold для semantic similarity
    /// </summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.7;

    /// <summary>
    /// Custom query template (опционально)
    /// Placeholders: {toolName}, {arguments}, {result}
    /// </summary>
    [JsonPropertyName("queryTemplate")]
    public string? QueryTemplate { get; set; }

    public bool Validate(out string? errorMessage)
    {
        if (TopK <= 0)
        {
            errorMessage = "TopK must be positive";
            return false;
        }

        if (Threshold < 0 || Threshold > 1)
        {
            errorMessage = "Threshold must be between 0 and 1";
            return false;
        }

        errorMessage = null;
        return true;
    }
}

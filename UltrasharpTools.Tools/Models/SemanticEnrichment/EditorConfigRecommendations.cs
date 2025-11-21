namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Рекомендации для .editorconfig
/// </summary>
public class EditorConfigRecommendations
{
    /// <summary>
    /// Полный контент .editorconfig файла
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Markdown summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Список правил с метаданными
    /// </summary>
    public List<EditorConfigRule> Rules { get; set; } = new();

    /// <summary>
    /// Случаи требующие ручной проверки
    /// </summary>
    public List<ManualReviewCase> RequiresManualReview { get; set; } = new();

    /// <summary>
    /// Статистика по правилам
    /// </summary>
    public EditorConfigStats Stats { get; set; } = new();
}

/// <summary>
/// Правило для .editorconfig
/// </summary>
public class EditorConfigRule
{
    /// <summary>
    /// Diagnostic ID
    /// </summary>
    public required string DiagnosticId { get; set; }

    /// <summary>
    /// Рекомендуемый severity
    /// </summary>
    public EditorConfigSeverity Severity { get; set; }

    /// <summary>
    /// Обоснование решения
    /// </summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Категория
    /// </summary>
    public DiagnosticCategory Category { get; set; }

    /// <summary>
    /// Строка для .editorconfig (dotnet_diagnostic.CA1873.severity = none)
    /// </summary>
    public string EditorConfigLine { get; set; } = string.Empty;

    /// <summary>
    /// Статистика по этому правилу
    /// </summary>
    public RuleStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Статистика по правилу
/// </summary>
public class RuleStatistics
{
    /// <summary>
    /// Количество срабатываний
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Количество затронутых файлов
    /// </summary>
    public int AffectedFiles { get; set; }

    /// <summary>
    /// Количество затронутых проектов
    /// </summary>
    public int AffectedProjects { get; set; }
}

/// <summary>
/// Случай требующий ручной проверки
/// </summary>
public class ManualReviewCase
{
    /// <summary>
    /// Diagnostic ID
    /// </summary>
    public required string DiagnosticId { get; set; }

    /// <summary>
    /// Причина почему требуется ручная проверка
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Количество срабатываний
    /// </summary>
    public int Occurrences { get; set; }

    /// <summary>
    /// Confidence score (обычно < 0.8)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Примеры для review
    /// </summary>
    public List<DiagnosticExample> Examples { get; set; } = new();

    /// <summary>
    /// Рекомендуемые действия
    /// </summary>
    public List<string> SuggestedActions { get; set; } = new();
}

/// <summary>
/// Статистика по сгенерированным правилам
/// </summary>
public class EditorConfigStats
{
    /// <summary>
    /// Общее количество правил
    /// </summary>
    public int TotalRulesGenerated { get; set; }

    /// <summary>
    /// Автоматически утверждённые (high confidence)
    /// </summary>
    public int AutoApprovedRules { get; set; }

    /// <summary>
    /// Требуют проверки (low confidence)
    /// </summary>
    public int NeedsReviewRules { get; set; }

    /// <summary>
    /// Распределение по категориям
    /// </summary>
    public Dictionary<DiagnosticCategory, int> ByCategory { get; set; } = new();
}

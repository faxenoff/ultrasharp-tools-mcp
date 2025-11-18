using System.Text.Json.Serialization;

namespace UltrasharpTools.Overlord.Models.Notifications;

/// <summary>
/// Базовый класс для всех типов уведомлений
/// </summary>
public abstract class NotificationMessage
{
    /// <summary>
    /// Тип уведомления
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }

    /// <summary>
    /// Временная метка уведомления
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// ID проекта, к которому относится уведомление
    /// </summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }
}

/// <summary>
/// Уведомление о обнаруженном дубликате кода
/// </summary>
public sealed class DuplicateDetectedNotification : NotificationMessage
{
    public override string Type => "duplicate_detected";

    /// <summary>
    /// Сообщение для пользователя
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Похожесть кода (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("similarity")]
    public double Similarity { get; init; }

    /// <summary>
    /// Расположение дубликата
    /// </summary>
    [JsonPropertyName("location")]
    public required string Location { get; init; }

    /// <summary>
    /// Проект, в котором найден дубликат
    /// </summary>
    [JsonPropertyName("duplicate_project")]
    public required string DuplicateProject { get; init; }

    /// <summary>
    /// Файл с дубликатом
    /// </summary>
    [JsonPropertyName("duplicate_file")]
    public required string DuplicateFile { get; init; }

    /// <summary>
    /// Строка начала дубликата
    /// </summary>
    [JsonPropertyName("duplicate_line")]
    public int DuplicateLine { get; init; }

    /// <summary>
    /// Фрагмент кода дубликата
    /// </summary>
    [JsonPropertyName("code_snippet")]
    public string? CodeSnippet { get; init; }
}

/// <summary>
/// Уведомление о конфликте изменений
/// </summary>
public sealed class ConflictAlertNotification : NotificationMessage
{
    public override string Type => "conflict_alert";

    /// <summary>
    /// Сообщение для пользователя
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Файл, в котором обнаружен конфликт
    /// </summary>
    [JsonPropertyName("file")]
    public required string File { get; init; }

    /// <summary>
    /// Тип конфликта
    /// </summary>
    [JsonPropertyName("conflict_type")]
    public required string ConflictType { get; init; }

    /// <summary>
    /// Автор конфликтующего изменения
    /// </summary>
    [JsonPropertyName("conflicting_author")]
    public string? ConflictingAuthor { get; init; }
}

/// <summary>
/// Уведомление о командной активности
/// </summary>
public sealed class TeamActivityNotification : NotificationMessage
{
    public override string Type => "team_activity";

    /// <summary>
    /// Сообщение для пользователя
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Автор активности
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>
    /// Тип активности (commit, branch_created, etc.)
    /// </summary>
    [JsonPropertyName("activity_type")]
    public required string ActivityType { get; init; }
}

/// <summary>
/// Уведомление о рекомендации по переиспользованию кода
/// </summary>
public sealed class CodeReuseRecommendationNotification : NotificationMessage
{
    public override string Type => "code_reuse_recommendation";

    /// <summary>
    /// Сообщение для пользователя
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Проект с готовой реализацией
    /// </summary>
    [JsonPropertyName("source_project")]
    public required string SourceProject { get; init; }

    /// <summary>
    /// Файл с готовой реализацией
    /// </summary>
    [JsonPropertyName("source_file")]
    public required string SourceFile { get; init; }

    /// <summary>
    /// Похожесть кода (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("similarity")]
    public double Similarity { get; init; }

    /// <summary>
    /// Описание готовой реализации
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

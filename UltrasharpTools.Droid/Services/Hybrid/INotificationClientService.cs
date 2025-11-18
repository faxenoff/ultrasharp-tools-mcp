namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Интерфейс для клиента SSE уведомлений от Overlord
/// </summary>
public interface INotificationClientService
{
    /// <summary>
    /// Событие получения уведомления о дубликате кода
    /// </summary>
    event EventHandler<DuplicateDetectedEventArgs>? DuplicateDetected;

    /// <summary>
    /// Событие получения уведомления о конфликте
    /// </summary>
    event EventHandler<ConflictAlertEventArgs>? ConflictAlert;

    /// <summary>
    /// Событие получения уведомления о командной активности
    /// </summary>
    event EventHandler<TeamActivityEventArgs>? TeamActivity;

    /// <summary>
    /// Событие получения рекомендации по переиспользованию кода
    /// </summary>
    event EventHandler<CodeReuseRecommendationEventArgs>? CodeReuseRecommendation;

    /// <summary>
    /// Подключиться к SSE endpoint Overlord
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отключиться от SSE endpoint
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Проверить подключение
    /// </summary>
    bool IsConnected { get; }
}

// Event args
public sealed class DuplicateDetectedEventArgs : EventArgs
{
    public required string Message { get; init; }
    public double Similarity { get; init; }
    public required string Location { get; init; }
    public required string DuplicateProject { get; init; }
    public required string DuplicateFile { get; init; }
    public int DuplicateLine { get; init; }
    public string? CodeSnippet { get; init; }
}

public sealed class ConflictAlertEventArgs : EventArgs
{
    public required string Message { get; init; }
    public required string File { get; init; }
    public required string ConflictType { get; init; }
    public string? ConflictingAuthor { get; init; }
}

public sealed class TeamActivityEventArgs : EventArgs
{
    public required string Message { get; init; }
    public string? Author { get; init; }
    public required string ActivityType { get; init; }
}

public sealed class CodeReuseRecommendationEventArgs : EventArgs
{
    public required string Message { get; init; }
    public required string SourceProject { get; init; }
    public required string SourceFile { get; init; }
    public double Similarity { get; init; }
    public string? Description { get; init; }
}

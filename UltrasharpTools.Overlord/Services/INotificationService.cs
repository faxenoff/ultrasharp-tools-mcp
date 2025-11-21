using UltrasharpTools.Overlord.Models.Notifications;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для отправки real-time уведомлений клиентам через SSE
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Регистрирует нового клиента для получения уведомлений
    /// </summary>
    /// <param name="clientId">Уникальный ID клиента</param>
    /// <param name="project">Проект клиента (для фильтрации уведомлений)</param>
    /// <param name="writer">TextWriter для отправки SSE событий</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task RegisterClientAsync(
        string clientId,
        string? project,
        TextWriter writer,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Отменяет регистрацию клиента
    /// </summary>
    /// <param name="clientId">ID клиента</param>
    void UnregisterClient(string clientId);

    /// <summary>
    /// Отправляет уведомление всем подписанным клиентам
    /// </summary>
    /// <param name="notification">Уведомление для отправки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task BroadcastNotificationAsync(
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Отправляет уведомление конкретному проекту
    /// </summary>
    /// <param name="project">Имя проекта</param>
    /// <param name="notification">Уведомление для отправки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task SendToProjectAsync(
        string project,
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Возвращает количество активных клиентов
    /// </summary>
    int GetActiveClientsCount();
}

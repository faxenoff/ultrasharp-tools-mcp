using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Overlord.Models.Notifications;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация сервиса уведомлений через Server-Sent Events (SSE)
/// </summary>
public sealed partial class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async Task RegisterClientAsync(
        string clientId,
        string? project,
        TextWriter writer,
        CancellationToken cancellationToken
    )
    {
        var connection = new ClientConnection(clientId, project, writer);

        if (_clients.TryAdd(clientId, connection))
        {
            LogClientRegistered(clientId, project ?? "all");

            try
            {
                // Отправляем приветственное сообщение
                await SendEventAsync(
                    writer,
                    "connected",
                    new
                    {
                        clientId,
                        project,
                        timestamp = DateTime.UtcNow,
                    },
                    cancellationToken
                );

                // Ждем пока клиент отключится
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogClientConnectionCancelled(clientId);
            }
            finally
            {
                UnregisterClient(clientId);
            }
        }
        else
        {
            LogClientAlreadyRegistered(clientId);
        }
    }

    public void UnregisterClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out var connection))
        {
            LogClientUnregistered(clientId);
            connection.Dispose();
        }
    }

    public async Task BroadcastNotificationAsync(
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    )
    {
        LogBroadcasting(notification.Type, _clients.Count);

        var tasks = _clients
            .Values.Select(client =>
                SendNotificationToClientAsync(client, notification, cancellationToken)
            )
            .ToList();

        await Task.WhenAll(tasks);
    }

    public async Task SendToProjectAsync(
        string project,
        NotificationMessage notification,
        CancellationToken cancellationToken = default
    )
    {
        LogSendingToProject(notification.Type, project);

        var tasks = _clients
            .Values.Where(c => c.Project == null || c.Project == project)
            .Select(client =>
                SendNotificationToClientAsync(client, notification, cancellationToken)
            )
            .ToList();

        if (tasks.Count == 0)
        {
            LogNoClientsForProject(project);
            return;
        }

        await Task.WhenAll(tasks);
    }

    public int GetActiveClientsCount() => _clients.Count;

    private async Task SendNotificationToClientAsync(
        ClientConnection client,
        NotificationMessage notification,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await SendEventAsync(client.Writer, notification.Type, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            LogSendNotificationFailed(ex, client.ClientId);

            // Отключаем проблемного клиента
            UnregisterClient(client.ClientId);
        }
    }

    private async Task SendEventAsync(
        TextWriter writer,
        string eventType,
        object data,
        CancellationToken cancellationToken
    )
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);

        // SSE формат:
        // event: <type>
        // data: <json>
        // (пустая строка)
        await writer.WriteLineAsync($"event: {eventType}");
        await writer.WriteLineAsync($"data: {json}");
        await writer.WriteLineAsync();
        await writer.FlushAsync(cancellationToken);
    }

    private sealed class ClientConnection : IDisposable
    {
        public string ClientId { get; }
        public string? Project { get; }
        public TextWriter Writer { get; }

        public ClientConnection(string clientId, string? project, TextWriter writer)
        {
            ClientId = clientId;
            Project = project;
            Writer = writer;
        }

        public void Dispose()
        {
            // Writer будет закрыт ASP.NET Core
        }
    }
}

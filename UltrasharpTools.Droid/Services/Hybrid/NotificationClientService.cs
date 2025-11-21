using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Клиент для получения real-time уведомлений от Overlord через SSE
/// </summary>
public sealed class NotificationClientService : INotificationClientService, IDisposable
{
    private readonly ILogger<NotificationClientService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AgentConfig _config;
    private CancellationTokenSource? _connectionCts;
    private bool _isConnected;

    public event EventHandler<DuplicateDetectedEventArgs>? DuplicateDetected;
    public event EventHandler<ConflictAlertEventArgs>? ConflictAlert;
    public event EventHandler<TeamActivityEventArgs>? TeamActivity;
    public event EventHandler<CodeReuseRecommendationEventArgs>? CodeReuseRecommendation;

    public bool IsConnected => _isConnected;

    public NotificationClientService(
        ILogger<NotificationClientService> logger,
        HttpClient httpClient,
        AgentConfig config
    )
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            return;
        }

        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var url =
            $"{_config.ServerUrl}/api/agent/notifications?project={Uri.EscapeDataString(_config.ProjectName)}";

        _logger.LogInformation("Connecting to SSE: {Url}", url);

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        _connectionCts.Token
                    );

                    response.EnsureSuccessStatusCode();
                    _isConnected = true;

                    await using var stream = await response.Content.ReadAsStreamAsync(
                        _connectionCts.Token
                    );
                    using var reader = new StreamReader(stream);

                    await ReadEventsAsync(reader, _connectionCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("SSE connection cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SSE connection error");
                }
                finally
                {
                    _isConnected = false;
                }
            },
            _connectionCts.Token
        );

        await Task.Delay(500, cancellationToken);
    }

    public void Disconnect()
    {
        _connectionCts?.Cancel();
        _isConnected = false;
    }

    private async Task ReadEventsAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? eventType = null;
        string? data = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
            {
                if (eventType != null && data != null)
                {
                    ProcessEvent(eventType, data);
                    eventType = null;
                    data = null;
                }
                continue;
            }

            if (line.StartsWith("event:"))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:"))
                data = line["data:".Length..].Trim();
        }
    }

    private void ProcessEvent(string eventType, string data)
    {
        try
        {
            _logger.LogDebug("Received SSE event: {Type}", eventType);

            switch (eventType)
            {
                case "duplicate_detected":
                    var dupData = JsonSerializer.Deserialize<DupNotification>(data);
                    if (dupData != null)
                    {
                        DuplicateDetected?.Invoke(
                            this,
                            new DuplicateDetectedEventArgs
                            {
                                Message = dupData.Message,
                                Similarity = dupData.Similarity,
                                Location = dupData.Location,
                                DuplicateProject = dupData.DuplicateProject,
                                DuplicateFile = dupData.DuplicateFile,
                                DuplicateLine = dupData.DuplicateLine,
                                CodeSnippet = dupData.CodeSnippet,
                            }
                        );
                    }
                    break;

                case "conflict_alert":
                    var conflictData = JsonSerializer.Deserialize<ConflictNotification>(data);
                    if (conflictData != null)
                    {
                        ConflictAlert?.Invoke(
                            this,
                            new ConflictAlertEventArgs
                            {
                                Message = conflictData.Message,
                                File = conflictData.File,
                                ConflictType = conflictData.ConflictType,
                                ConflictingAuthor = conflictData.ConflictingAuthor,
                            }
                        );
                    }
                    break;

                case "team_activity":
                    var activityData = JsonSerializer.Deserialize<TeamActivityNotification>(data);
                    if (activityData != null)
                    {
                        TeamActivity?.Invoke(
                            this,
                            new TeamActivityEventArgs
                            {
                                Message = activityData.Message,
                                Author = activityData.Author,
                                ActivityType = activityData.ActivityType,
                            }
                        );
                    }
                    break;

                case "code_reuse_recommendation":
                    var reuseData = JsonSerializer.Deserialize<CodeReuseNotification>(data);
                    if (reuseData != null)
                    {
                        CodeReuseRecommendation?.Invoke(
                            this,
                            new CodeReuseRecommendationEventArgs
                            {
                                Message = reuseData.Message,
                                SourceProject = reuseData.SourceProject,
                                SourceFile = reuseData.SourceFile,
                                Similarity = reuseData.Similarity,
                                Description = reuseData.Description,
                            }
                        );
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process SSE event");
        }
    }

    public void Dispose()
    {
        Disconnect();
        _connectionCts?.Dispose();
    }

    // Minimal DTOs
    private sealed class DupNotification
    {
        public string Message { get; set; } = "";
        public double Similarity { get; set; }
        public string Location { get; set; } = "";
        public string DuplicateProject { get; set; } = "";
        public string DuplicateFile { get; set; } = "";
        public int DuplicateLine { get; set; }
        public string? CodeSnippet { get; set; }
    }

    private sealed class ConflictNotification
    {
        public string Message { get; set; } = "";
        public string File { get; set; } = "";
        public string ConflictType { get; set; } = "";
        public string? ConflictingAuthor { get; set; }
    }

    private sealed class TeamActivityNotification
    {
        public string Message { get; set; } = "";
        public string? Author { get; set; }
        public string ActivityType { get; set; } = "";
    }

    private sealed class CodeReuseNotification
    {
        public string Message { get; set; } = "";
        public string SourceProject { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public double Similarity { get; set; }
        public string? Description { get; set; }
    }
}

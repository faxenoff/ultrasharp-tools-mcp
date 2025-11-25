using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация сервиса для отправки событий на Overlord
/// </summary>
public sealed partial class ServerBridgeService : IServerBridgeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerBridgeService> _logger;
    private readonly string _serverUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly RetryPolicy _retryPolicy;

    public ServerBridgeService(
        HttpClient httpClient,
        ILogger<ServerBridgeService> logger,
        AgentConfig config
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _serverUrl = config.ServerUrl.TrimEnd('/');
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        // Retry policy: 3 attempts, 100ms initial delay, exponential backoff up to 10s
        _retryPolicy = new RetryPolicy(
            logger,
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(10),
            backoffMultiplier: 2.0
        );
    }

    public async ValueTask SendFileChangedEventAsync(
        FileChangedEvent evt,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/file-changed";
            var response = await _httpClient
                .PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            LogFileSent(evt.Project, evt.File);
        }
        catch (Exception ex)
        {
            LogFileEventFailed(ex, evt.Project, evt.File);
        }
    }

    public async ValueTask SendBranchSwitchEventAsync(
        BranchSwitchEvent evt,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/branch-switched";
            var response = await _httpClient
                .PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            LogBranchSwitchSent(evt.Project, evt.FromBranch, evt.ToBranch);
        }
        catch (Exception ex)
        {
            LogBranchEventFailed(ex, evt.Project);
        }
    }

    public async ValueTask SendGitCommitEventAsync(
        GitCommitEvent evt,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/git-commit";
            var response = await _httpClient
                .PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            LogCommitEventSent(evt.Project, evt.Branch, evt.CommitSha);
        }
        catch (Exception ex)
        {
            LogCommitEventFailed(ex, evt.Project);
        }
    }

    public async ValueTask<bool> IsServerAvailableAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/health";
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask<string> CallMcpProxyAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _retryPolicy
            .ExecuteAsync(
                async ct =>
                {
                    var url = $"{_serverUrl}/api/agent/mcp-proxy";

                    var request = new
                    {
                        tool = toolName,
                        arguments = argumentsJson,
                        context = new { project = projectContext },
                    };

                    LogMcpProxyCall(toolName, projectContext);

                    var response = await _httpClient
                        .PostAsJsonAsync(url, request, _jsonOptions, ct)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var result = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                    LogMcpProxySuccess(toolName);

                    return result;
                },
                $"CallMcpProxy({toolName})",
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async ValueTask<object> CallMcpProxyAsync(
        string toolName,
        Dictionary<string, object> arguments,
        string? projectContext = null,
        CancellationToken cancellationToken = default
    )
    {
        // Сериализуем Dictionary в JSON
        var argumentsJson = JsonSerializer.Serialize(arguments, _jsonOptions);

        // Вызываем основной метод
        var resultJson = await CallMcpProxyAsync(
                toolName,
                argumentsJson,
                projectContext,
                cancellationToken
            )
            .ConfigureAwait(false);

        // Десериализуем результат обратно
        var result = JsonSerializer.Deserialize<object>(resultJson, _jsonOptions);
        return result ?? new { };
    }
}

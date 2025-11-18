using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация сервиса для отправки событий на Overlord
/// </summary>
public sealed class ServerBridgeService : IServerBridgeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerBridgeService> _logger;
    private readonly string _serverUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServerBridgeService(
        HttpClient httpClient,
        ILogger<ServerBridgeService> logger,
        AgentConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serverUrl = config.ServerUrl.TrimEnd('/');
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task SendFileChangedEventAsync(
        FileChangedEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/file-changed";
            var response = await _httpClient.PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug(
                "Sent file changed event: {Project}/{File}",
                evt.Project,
                evt.File);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send file changed event: {Project}/{File}",
                evt.Project,
                evt.File);
        }
    }

    public async Task SendBranchSwitchEventAsync(
        BranchSwitchEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/branch-switched";
            var response = await _httpClient.PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Sent branch switch event: {Project} {From} -> {To}",
                evt.Project,
                evt.FromBranch,
                evt.ToBranch);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send branch switch event: {Project}",
                evt.Project);
        }
    }

    public async Task SendGitCommitEventAsync(
        GitCommitEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/git-commit";
            var response = await _httpClient.PostAsJsonAsync(url, evt, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Sent git commit event: {Project}/{Branch} {Sha}",
                evt.Project,
                evt.Branch,
                evt.CommitSha);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send git commit event: {Project}",
                evt.Project);
        }
    }

    public async Task<bool> IsServerAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/health";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CallMcpProxyAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_serverUrl}/api/agent/mcp-proxy";

            var request = new
            {
                tool = toolName,
                arguments = argumentsJson,
                context = new
                {
                    project = projectContext
                }
            };

            _logger.LogDebug(
                "Calling MCP proxy: tool={ToolName}, project={Project}",
                toolName,
                projectContext);

            var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug(
                "MCP proxy call succeeded: tool={ToolName}",
                toolName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to call MCP proxy: tool={ToolName}",
                toolName);
            throw;
        }
    }

    public async Task<object> CallMcpProxyAsync(
        string toolName,
        Dictionary<string, object> arguments,
        string? projectContext = null,
        CancellationToken cancellationToken = default)
    {
        // Сериализуем Dictionary в JSON
        var argumentsJson = JsonSerializer.Serialize(arguments, _jsonOptions);

        // Вызываем основной метод
        var resultJson = await CallMcpProxyAsync(toolName, argumentsJson, projectContext, cancellationToken);

        // Десериализуем результат обратно
        var result = JsonSerializer.Deserialize<object>(resultJson, _jsonOptions);
        return result ?? new { };
    }
}

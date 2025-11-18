using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Overlord.Models.Agent;
using UltrasharpTools.Overlord.Services;

namespace UltrasharpTools.Overlord.Controllers;

/// <summary>
/// API endpoint для приема событий от Agent'ов
/// </summary>
[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly ILogger<AgentController> _logger;
    private readonly IMultiProjectVectorStoreService _vectorStore;
    private readonly IMcpProxyService _mcpProxy;
    private readonly INotificationService _notificationService;
    private readonly IConflictDetectionService _conflictDetection;

    public AgentController(
        ILogger<AgentController> logger,
        IMultiProjectVectorStoreService vectorStore,
        IMcpProxyService mcpProxy,
        INotificationService notificationService,
        IConflictDetectionService conflictDetection)
    {
        _logger = logger;
        _vectorStore = vectorStore;
        _mcpProxy = mcpProxy;
        _notificationService = notificationService;
        _conflictDetection = conflictDetection;
    }

    /// <summary>
    /// Обработка события изменения файла
    /// </summary>
    [HttpPost("file-changed")]
    public async Task<IActionResult> FileChanged([FromBody] FileChangedEventDto evt)
    {
        try
        {
            _logger.LogInformation(
                "File changed: {Project}/{Branch}/{File} ({Action})",
                evt.Project, evt.Branch, evt.File, evt.Action);

            // Сохраняем векторы в multi-project хранилище
            if (evt.Vectors != null && evt.Action != "deleted")
            {
                await _vectorStore.StoreVectorsAsync(
                    project: evt.Project,
                    branch: evt.Branch,
                    filePath: evt.File,
                    vectors: evt.Vectors,
                    content: evt.Content,
                    symbols: evt.Symbols);

                // Автоматическая проверка на дубликаты
                var duplicates = await _conflictDetection.DetectDuplicatesAsync(
                    project: evt.Project,
                    branch: evt.Branch,
                    file: evt.File,
                    vectors: evt.Vectors,
                    content: evt.Content,
                    cancellationToken: HttpContext.RequestAborted);

                return Ok(new
                {
                    status = "success",
                    duplicatesFound = duplicates.Count,
                    duplicates = duplicates.Take(3).Select(d => new
                    {
                        project = d.Project,
                        file = d.File,
                        similarity = d.Similarity
                    })
                });
            }
            else if (evt.Action == "deleted")
            {
                await _vectorStore.DeleteVectorsAsync(
                    project: evt.Project,
                    branch: evt.Branch,
                    filePath: evt.File);

                return Ok(new { status = "success", action = "deleted" });
            }

            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file changed event");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Обработка события переключения ветки
    /// </summary>
    [HttpPost("branch-switched")]
    public IActionResult BranchSwitched([FromBody] BranchSwitchEventDto evt)
    {
        try
        {
            _logger.LogInformation(
                "Branch switched: {Project} {From} → {To}",
                evt.Project, evt.FromBranch, evt.ToBranch);

            // TODO: Обновить контекст для этого Agent'а
            // Например, переключить активную branch для поиска

            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process branch switch event");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Обработка события коммита
    /// </summary>
    [HttpPost("git-commit")]
    public IActionResult GitCommit([FromBody] GitCommitEventDto evt)
    {
        try
        {
            _logger.LogInformation(
                "Git commit: {Project}/{Branch} {Sha} ({Files} files)",
                evt.Project, evt.Branch, evt.CommitSha, evt.FilesChanged.Length);

            // TODO: Обновить метаданные проекта
            // Можно сохранять историю коммитов для аналитики

            return Ok(new { status = "success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process git commit event");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Проксирование MCP запросов (от Agent к Overlord MCP tools)
    /// </summary>
    [HttpPost("mcp-proxy")]
    public async Task<IActionResult> McpProxy([FromBody] McpProxyRequest request)
    {
        try
        {
            _logger.LogInformation("MCP proxy request: {Tool}", request.Tool);

            var result = await _mcpProxy.ExecuteToolCallAsync(
                request.Tool,
                request.Arguments,
                request.ProjectContext,
                HttpContext.RequestAborted);

            return Ok(new { result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy MCP request");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить список доступных MCP tools
    /// </summary>
    [HttpGet("mcp-tools")]
    public async Task<IActionResult> GetMcpTools()
    {
        try
        {
            var tools = await _mcpProxy.GetAvailableToolsAsync(HttpContext.RequestAborted);
            return Ok(new { tools });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MCP tools");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// DTO для MCP proxy запросов
    /// </summary>
    public sealed class McpProxyRequest
    {
        public required string Tool { get; init; }
        public required string Arguments { get; init; }
        public string? ProjectContext { get; init; }
    }

    /// <summary>
    /// Health check для Agent'ов
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            activeClients = _notificationService.GetActiveClientsCount()
        });
    }

    /// <summary>
    /// SSE endpoint для real-time уведомлений
    /// </summary>
    [HttpGet("notifications")]
    public async Task Notifications(
        [FromQuery] string? clientId = null,
        [FromQuery] string? project = null)
    {
        // Устанавливаем headers для SSE
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var id = clientId ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "SSE connection established: {ClientId}, project: {Project}",
            id,
            project ?? "all");

        try
        {
            var writer = new StreamWriter(Response.Body)
            {
                AutoFlush = true
            };

            await _notificationService.RegisterClientAsync(
                id,
                project,
                writer,
                HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SSE connection closed for client {ClientId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE connection error for client {ClientId}", id);
        }
    }
}

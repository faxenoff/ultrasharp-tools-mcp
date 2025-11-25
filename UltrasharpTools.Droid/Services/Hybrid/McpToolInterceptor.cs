using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Services;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Global interceptor для всех MCP tool calls с routing и semantic enrichment
/// Использует Decorator Pattern для интеграции с MCP SDK
/// </summary>
public sealed partial class McpToolInterceptor : IMcpToolExecutor
{
    private readonly ILogger<McpToolInterceptor> _logger;
    private readonly IToolRouter _router;
    private readonly IToolEnricher _enricher;
    private readonly IServerBridgeService? _serverBridge;
    private readonly PowerManagementService? _powerManagement;

    // Локальные инструменты будут выполняться через делегаты
    // (регистрируются через RegisterLocalToolExecutor)
    private readonly Dictionary<
        string,
        Func<Dictionary<string, object>, CancellationToken, Task<object>>
    > _localToolExecutors;

    public McpToolInterceptor(
        ILogger<McpToolInterceptor> logger,
        IToolRouter router,
        IToolEnricher enricher,
        IServerBridgeService? serverBridge = null,
        PowerManagementService? powerManagement = null
    )
    {
        _logger = logger;
        _router = router;
        _enricher = enricher;
        _serverBridge = serverBridge;
        _powerManagement = powerManagement;
        _localToolExecutors =
            new Dictionary<
                string,
                Func<Dictionary<string, object>, CancellationToken, Task<object>>
            >();

        LogInitialized();
    }

    /// <summary>
    /// Регистрирует локальный executor для инструмента
    /// </summary>
    public void RegisterLocalToolExecutor(
        string toolName,
        Func<Dictionary<string, object>, CancellationToken, Task<object>> executor
    )
    {
        _localToolExecutors[toolName] = executor;
        LogRegisteredExecutor(toolName);
    }

    /// <inheritdoc/>
    public async Task<McpToolExecutionResult> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default
    )
    {
        // Регистрируем активность - выходим из idle режима при необходимости
        _powerManagement?.RecordActivity();

        var sw = Stopwatch.StartNew();

        LogExecutingTool(toolName, arguments?.Count ?? 0);

        try
        {
            // 1. Determine routing decision
            var routingDecision = _router.DetermineRouting(toolName, arguments);
            LogRoutingDecision(toolName, routingDecision);

            // 2. Execute based on routing
            arguments ??= new Dictionary<string, object>();

            object result = routingDecision switch
            {
                ToolRoutingDecision.Local => await ExecuteLocalAsync(toolName, arguments, ct),
                ToolRoutingDecision.Overlord => await ExecuteOverlordAsync(toolName, arguments, ct),
                ToolRoutingDecision.OverlordWithFallback => await ExecuteWithFallbackAsync(
                    toolName,
                    arguments,
                    ct
                ),
                _ => throw new NotSupportedException(
                    $"Unknown routing decision: {routingDecision}"
                ),
            };

            // 3. Apply semantic enrichment if supported
            var wasEnriched = false;
            if (_enricher.SupportsEnrichment(toolName))
            {
                LogApplyingEnrichment(toolName);
                var enrichedResult = await _enricher.EnrichAsync(toolName, result, arguments, ct);
                result = enrichedResult;
                wasEnriched = enrichedResult.Semantic != null;
            }

            sw.Stop();

            LogToolExecuted(toolName, sw.ElapsedMilliseconds, routingDecision, wasEnriched);

            return new McpToolExecutionResult
            {
                ToolName = toolName,
                Result = result,
                RoutingDecision = routingDecision,
                WasEnriched = wasEnriched,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogToolFailed(ex, toolName, sw.ElapsedMilliseconds);

            return new McpToolExecutionResult
            {
                ToolName = toolName,
                Result = new { error = ex.Message },
                RoutingDecision = ToolRoutingDecision.Local,
                WasEnriched = false,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <inheritdoc/>
    public bool IsToolSupported(string toolName)
    {
        // Инструмент поддерживается если:
        // 1. Есть локальный executor
        // 2. Или доступен Overlord для proxy вызовов
        return _localToolExecutors.ContainsKey(toolName) || _serverBridge != null;
    }

    // === Private Execution Methods ===

    private async Task<object> ExecuteLocalAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct
    )
    {
        LogExecutingLocally(toolName);

        if (!_localToolExecutors.TryGetValue(toolName, out var executor))
        {
            throw new InvalidOperationException(
                $"No local executor registered for tool: {toolName}"
            );
        }

        return await executor(arguments, ct);
    }

    private async Task<object> ExecuteOverlordAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct
    )
    {
        LogExecutingOnOverlord(toolName);

        if (_serverBridge == null)
        {
            throw new InvalidOperationException(
                "Overlord routing requested but ServerBridgeService is not available"
            );
        }

        return await _serverBridge.CallMcpProxyAsync(toolName, arguments, null, ct);
    }

    private async Task<object> ExecuteWithFallbackAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct
    )
    {
        LogExecutingWithFallback(toolName);

        // Сначала пытаемся Overlord
        if (_serverBridge != null)
        {
            try
            {
                var isAvailable = await _router.IsOverlordAvailableAsync(ct);
                if (isAvailable)
                {
                    LogOverlordExecuting(toolName);
                    return await _serverBridge.CallMcpProxyAsync(toolName, arguments, null, ct);
                }
            }
            catch (Exception ex)
            {
                LogOverlordFallback(ex, toolName);
            }
        }

        // Fallback на локальное выполнение
        LogFallingBackToLocal(toolName);
        return await ExecuteLocalAsync(toolName, arguments, ct);
    }
}

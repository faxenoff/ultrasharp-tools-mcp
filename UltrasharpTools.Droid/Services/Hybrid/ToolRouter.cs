using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация маршрутизатора инструментов для Hybrid Mode
/// Определяет какие инструменты должны выполняться локально, а какие на Overlord
/// </summary>
public sealed partial class ToolRouter : IToolRouter
{
    private readonly ILogger<ToolRouter> _logger;
    private readonly IServerBridgeService? _serverBridge;
    private readonly bool _isHybridMode;

    // Semantic tools - всегда требуют Overlord (векторный поиск + embedding + cross-project)
    private static readonly HashSet<string> SemanticTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "find_duplicates", // Cross-project duplicate search (query-based)
        "semantic_search", // Cross-project semantic search
        "semantic_diff", // Semantic similarity comparison
        "reindex_changed_files", // Vector store indexing
    };

    // Hybrid tools - решение зависит от параметров
    private static readonly HashSet<string> HybridTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "pattern_search", // mode="semantic" → Overlord
        "analyze_complexity", // scope="project" → Overlord
        "trace_execution", // large methods → Overlord
        "trace_backwards", // cross-file → Overlord
        "export_call_graph", // project-wide → Overlord
    };

    // Ресурсоёмкие tools - лучше на сервере
    private static readonly HashSet<string> ResourceIntensiveTools = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "analyze_path_feasibility", // Z3 solver
        "analyze_logs", // большие файлы
    };

    public ToolRouter(
        ILogger<ToolRouter> logger,
        IServerBridgeService? serverBridge = null,
        bool isHybridMode = false
    )
    {
        _logger = logger;
        _serverBridge = serverBridge;
        _isHybridMode = isHybridMode;
    }

    public ToolRoutingDecision DetermineRouting(
        string toolName,
        Dictionary<string, object>? arguments = null
    )
    {
        // 1. Если не hybrid mode - всё локально
        if (!_isHybridMode || _serverBridge == null)
        {
            LogLocalRouting(toolName);
            return ToolRoutingDecision.Local;
        }

        // 2. Semantic tools → всегда Overlord
        if (SemanticTools.Contains(toolName))
        {
            LogSemanticRouting(toolName);
            return ToolRoutingDecision.Overlord;
        }

        // 3. Resource-intensive tools → Overlord с fallback
        if (ResourceIntensiveTools.Contains(toolName))
        {
            LogResourceIntensiveRouting(toolName);
            return ToolRoutingDecision.OverlordWithFallback;
        }

        // 4. Hybrid tools → анализ параметров
        if (HybridTools.Contains(toolName) && arguments != null)
        {
            var decision = AnalyzeHybridTool(toolName, arguments);
            LogHybridRouting(toolName, decision);
            return decision;
        }

        // 5. По умолчанию → Local (быстрые Roslyn операции + batch analysis tools)
        // Includes: detect_code_clones (requires loaded solution + SemanticSearchService)
        LogDefaultRouting(toolName);
        return ToolRoutingDecision.Local;
    }

    public async Task<bool> IsOverlordAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_serverBridge == null)
        {
            return false;
        }

        try
        {
            var isAvailable = await _serverBridge.IsServerAvailableAsync(cancellationToken);
            LogAvailabilityCheck(isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            LogAvailabilityCheckFailed(ex);
            return false;
        }
    }

    /// <summary>
    /// Анализирует параметры hybrid инструмента для принятия решения о маршрутизации
    /// </summary>
    private ToolRoutingDecision AnalyzeHybridTool(
        string toolName,
        Dictionary<string, object> arguments
    )
    {
        switch (toolName.ToLowerInvariant())
        {
            case "pattern_search":
                // mode = "semantic" или "hybrid" → Overlord
                if (arguments.TryGetValue("mode", out var mode))
                {
                    var modeStr = mode?.ToString()?.ToLowerInvariant();
                    if (modeStr is "semantic" or "hybrid")
                    {
                        return ToolRoutingDecision.Overlord;
                    }
                }
                return ToolRoutingDecision.Local;

            case "analyze_complexity":
                // scope = "project" → Overlord (ресурсоёмкий)
                if (arguments.TryGetValue("scope", out var scope))
                {
                    var scopeStr = scope?.ToString()?.ToLowerInvariant();
                    if (scopeStr == "project")
                    {
                        return ToolRoutingDecision.OverlordWithFallback;
                    }
                }
                return ToolRoutingDecision.Local;

            case "trace_execution":
                // Если метод большой (> 100 LOC) → Overlord
                // Примечание: оценка размера требует дополнительного анализа
                // Пока по умолчанию Local, но с возможностью Overlord
                return ToolRoutingDecision.OverlordWithFallback;

            case "trace_backwards":
            case "export_call_graph":
                // Cross-file операции лучше на сервере
                return ToolRoutingDecision.OverlordWithFallback;

            default:
                return ToolRoutingDecision.Local;
        }
    }
}

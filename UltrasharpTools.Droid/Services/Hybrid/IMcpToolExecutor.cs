namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Результат выполнения MCP инструмента
/// </summary>
public sealed class McpToolExecutionResult
{
    /// <summary>
    /// Название инструмента
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Результат выполнения (может быть EnrichedToolResult или обычный результат)
    /// </summary>
    public required object Result { get; init; }

    /// <summary>
    /// Решение о routing
    /// </summary>
    public ToolRoutingDecision RoutingDecision { get; init; }

    /// <summary>
    /// Был ли применён semantic enrichment
    /// </summary>
    public bool WasEnriched { get; init; }

    /// <summary>
    /// Время выполнения (мс)
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Сообщение об ошибке (если была)
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Исполнитель MCP инструментов с поддержкой routing и semantic enrichment
/// </summary>
public interface IMcpToolExecutor
{
    /// <summary>
    /// Выполняет MCP инструмент с автоматическим routing и enrichment
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <param name="arguments">Аргументы инструмента</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Результат выполнения</returns>
    Task<McpToolExecutionResult> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default
    );

    /// <summary>
    /// Проверяет, поддерживается ли инструмент
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <returns>True если поддерживается</returns>
    bool IsToolSupported(string toolName);
}

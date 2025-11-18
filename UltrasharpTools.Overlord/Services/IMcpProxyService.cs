namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для проксирования MCP запросов от Agent'ов
/// </summary>
public interface IMcpProxyService
{
    /// <summary>
    /// Выполнить MCP tool call и вернуть результат
    /// </summary>
    Task<string> ExecuteToolCallAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить список доступных MCP tools
    /// </summary>
    Task<List<ToolInfo>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Информация о MCP tool
/// </summary>
public sealed class ToolInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public object? InputSchema { get; init; }
}

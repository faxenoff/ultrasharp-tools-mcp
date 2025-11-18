using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Сервис для коммуникации с Overlord сервером
/// </summary>
public interface IServerBridgeService
{
    /// <summary>
    /// Отправить событие изменения файла
    /// </summary>
    ValueTask SendFileChangedEventAsync(FileChangedEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить событие переключения ветки
    /// </summary>
    ValueTask SendBranchSwitchEventAsync(BranchSwitchEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить событие Git коммита
    /// </summary>
    ValueTask SendGitCommitEventAsync(GitCommitEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить доступность сервера
    /// </summary>
    ValueTask<bool> IsServerAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполнить MCP tool через Overlord proxy
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <param name="argumentsJson">JSON аргументы</param>
    /// <param name="projectContext">Контекст проекта (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>JSON результат выполнения</returns>
    ValueTask<string> CallMcpProxyAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполнить MCP tool через Overlord proxy (перегрузка с Dictionary)
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <param name="arguments">Аргументы инструмента</param>
    /// <param name="projectContext">Контекст проекта (опционально)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Десериализованный результат</returns>
    ValueTask<object> CallMcpProxyAsync(
        string toolName,
        Dictionary<string, object> arguments,
        string? projectContext = null,
        CancellationToken cancellationToken = default);
}

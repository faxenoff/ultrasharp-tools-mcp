namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Интерфейс для маршрутизации инструментов между LOCAL и OVERLORD режимами
/// </summary>
public interface IToolRouter
{
    /// <summary>
    /// Определяет куда должен быть направлен запрос
    /// </summary>
    /// <param name="toolName">Название инструмента</param>
    /// <param name="arguments">Аргументы инструмента (опционально)</param>
    /// <returns>Решение маршрутизации</returns>
    ToolRoutingDecision DetermineRouting(
        string toolName,
        Dictionary<string, object>? arguments = null
    );

    /// <summary>
    /// Проверяет доступность Overlord сервера
    /// </summary>
    Task<bool> IsOverlordAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Решение о маршрутизации инструмента
/// </summary>
public enum ToolRoutingDecision
{
    /// <summary>
    /// Выполнить локально через Roslyn
    /// </summary>
    Local,

    /// <summary>
    /// Отправить на Overlord сервер
    /// </summary>
    Overlord,

    /// <summary>
    /// Попытаться Overlord, при ошибке - fallback на Local
    /// </summary>
    OverlordWithFallback,
}

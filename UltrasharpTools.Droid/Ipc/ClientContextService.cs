using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Ipc;

namespace UltrasharpTools.Droid.Ipc;

/// <summary>
/// Сервис для отслеживания контекста клиентов и reference counting solutions.
/// Позволяет выгружать solution только когда последний клиент его использующий отключился.
/// </summary>
public sealed class ClientContextService : IClientContextService {
    private readonly ILogger<ClientContextService> _logger;
    private readonly ISolutionManager _solutionManager;
    private readonly VectorDBClient? _vectorDBClient;

    // ClientId -> SolutionPath (какой solution загрузил клиент)
    private readonly ConcurrentDictionary<string, string> _clientSolutions = new();

    // SolutionPath -> RefCount (сколько клиентов используют solution)
    private readonly ConcurrentDictionary<string, int> _solutionRefCounts = new();

    private readonly object _lock = new();

    public ClientContextService(
        ISolutionManager solutionManager,
        VectorDBClient? vectorDBClient,
        ILogger<ClientContextService> logger
    ) {
        _solutionManager = solutionManager;
        _vectorDBClient = vectorDBClient;
        _logger = logger;
    }

    /// <summary>
    /// Регистрирует загрузку solution клиентом.
    /// </summary>
    public void RegisterSolutionLoad(string clientId, string solutionPath) {
        var normalizedPath = Path.GetFullPath(solutionPath);

        lock (_lock) {
            // Если клиент уже имел другой solution - сначала отпускаем его
            if (_clientSolutions.TryGetValue(clientId, out var oldSolution)) {
                if (!string.Equals(oldSolution, normalizedPath, StringComparison.OrdinalIgnoreCase)) {
                    DecrementRefCount(oldSolution);
                    _logger.LogInformation(
                        "[ClientContext] Client {ClientId} switching from {OldSolution} to {NewSolution}",
                        clientId, oldSolution, normalizedPath
                    );
                } else {
                    // Тот же solution - ничего не делаем
                    return;
                }
            }

            // Регистрируем новый solution
            _clientSolutions[clientId] = normalizedPath;
            IncrementRefCount(normalizedPath);

            _logger.LogInformation(
                "[ClientContext] Client {ClientId} registered solution: {SolutionPath} (refcount: {RefCount})",
                clientId, normalizedPath, _solutionRefCounts.GetValueOrDefault(normalizedPath, 0)
            );
        }
    }

    /// <summary>
    /// Вызывается при отключении клиента. Возвращает true если solution был выгружен.
    /// </summary>
    public bool OnClientDisconnected(string clientId) {
        lock (_lock) {
            if (!_clientSolutions.TryRemove(clientId, out var solutionPath)) {
                _logger.LogDebug("[ClientContext] Client {ClientId} had no solution registered", clientId);
                return false;
            }

            var newRefCount = DecrementRefCount(solutionPath);

            _logger.LogInformation(
                "[ClientContext] Client {ClientId} disconnected, solution {SolutionPath} refcount: {RefCount}",
                clientId, solutionPath, newRefCount
            );

            if (newRefCount <= 0) {
                // Последний клиент отключился - выгружаем solution
                return UnloadSolution(solutionPath);
            }

            return false;
        }
    }

    /// <summary>
    /// Возвращает количество активных клиентов для solution.
    /// </summary>
    public int GetSolutionRefCount(string solutionPath) {
        var normalizedPath = Path.GetFullPath(solutionPath);
        return _solutionRefCounts.GetValueOrDefault(normalizedPath, 0);
    }

    /// <summary>
    /// Возвращает solution path для клиента (или null если не зарегистрирован).
    /// </summary>
    public string? GetClientSolution(string clientId) {
        return _clientSolutions.GetValueOrDefault(clientId);
    }

    private void IncrementRefCount(string solutionPath) {
        _solutionRefCounts.AddOrUpdate(solutionPath, 1, (_, count) => count + 1);
    }

    private int DecrementRefCount(string solutionPath) {
        if (_solutionRefCounts.TryGetValue(solutionPath, out var count)) {
            var newCount = count - 1;
            if (newCount <= 0) {
                _solutionRefCounts.TryRemove(solutionPath, out _);
                return 0;
            }
            _solutionRefCounts[solutionPath] = newCount;
            return newCount;
        }
        return 0;
    }

    private bool UnloadSolution(string solutionPath) {
        try {
            _logger.LogInformation("[ClientContext] Unloading solution: {SolutionPath}", solutionPath);

            // Выгружаем из SolutionManager только если это текущий solution
            if (_solutionManager.IsSolutionLoaded &&
                string.Equals(_solutionManager.CurrentSolution?.FilePath, solutionPath, StringComparison.OrdinalIgnoreCase)) {
                _solutionManager.UnloadSolution();
                _logger.LogInformation("[ClientContext] Solution unloaded from memory");
            }

            // Очищаем VectorDB (если больше нет клиентов с solutions)
            if (_vectorDBClient != null && _solutionRefCounts.IsEmpty) {
                _logger.LogInformation("[ClientContext] Clearing VectorDB index...");
                _ = _vectorDBClient.ClearAsync(CancellationToken.None);
            }

            return true;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "[ClientContext] Error unloading solution {SolutionPath}", solutionPath);
            return false;
        }
    }
}

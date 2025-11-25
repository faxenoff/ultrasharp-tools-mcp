using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB;

/// <summary>
/// Управляет энергопотреблением процесса.
/// После периода неактивности переключает в энергоэффективный режим (низкий приоритет).
/// При поступлении запроса - мгновенно возвращается в нормальный режим.
/// </summary>
public sealed partial class PowerManagementService : IDisposable {
    private readonly ILogger<PowerManagementService> _logger;
    private readonly TimeSpan _idleTimeout;
    private readonly Timer _idleCheckTimer;
    private readonly object _lock = new();

    private DateTime _lastActivityTime;
    private PowerMode _currentMode = PowerMode.Normal;
    private bool _disposed;

    // Настройки для разных режимов
    private readonly int _normalMinWorkerThreads;
    private readonly int _normalMinCompletionThreads;
    private const int IdleMinWorkerThreads = 1;
    private const int IdleMinCompletionThreads = 1;

    public PowerMode CurrentMode => _currentMode;
    public DateTime LastActivityTime => _lastActivityTime;

    public PowerManagementService(
        ILogger<PowerManagementService> logger,
        TimeSpan? idleTimeout = null) {
        _logger = logger;
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(3);
        _lastActivityTime = DateTime.UtcNow;

        // Сохраняем текущие настройки ThreadPool
        ThreadPool.GetMinThreads(out _normalMinWorkerThreads, out _normalMinCompletionThreads);

        // Проверка каждые 30 секунд
        _idleCheckTimer = new Timer(
            CheckIdleState,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        LogServiceStarted(_idleTimeout.TotalMinutes);
    }

    /// <summary>
    /// Регистрирует активность - вызывать при каждом входящем запросе
    /// </summary>
    public void RecordActivity() {
        lock (_lock) {
            _lastActivityTime = DateTime.UtcNow;

            if (_currentMode == PowerMode.Idle) {
                SwitchToNormalMode();
            }
        }
    }

    /// <summary>
    /// Оборачивает выполнение задачи с автоматической регистрацией активности
    /// </summary>
    public async Task<T> ExecuteWithActivityTrackingAsync<T>(Func<Task<T>> operation) {
        RecordActivity();
        try {
            return await operation();
        } finally {
            RecordActivity();
        }
    }

    /// <summary>
    /// Оборачивает выполнение задачи с автоматической регистрацией активности
    /// </summary>
    public async Task ExecuteWithActivityTrackingAsync(Func<Task> operation) {
        RecordActivity();
        try {
            await operation();
        } finally {
            RecordActivity();
        }
    }

    private void CheckIdleState(object? state) {
        if (_disposed)
            return;

        lock (_lock) {
            var idleTime = DateTime.UtcNow - _lastActivityTime;

            if (_currentMode == PowerMode.Normal && idleTime >= _idleTimeout) {
                SwitchToIdleMode();
            }
        }
    }

    private void SwitchToIdleMode() {
        if (_currentMode == PowerMode.Idle)
            return;

        try {
            // Снижаем приоритет процесса
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.BelowNormal;

            // Уменьшаем минимальные потоки ThreadPool
            ThreadPool.SetMinThreads(IdleMinWorkerThreads, IdleMinCompletionThreads);

            // Принудительная сборка мусора для освобождения памяти
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

            _currentMode = PowerMode.Idle;
            LogSwitchedToIdleMode();
        } catch (Exception ex) {
            LogFailedToSwitchMode(ex, "idle");
        }
    }

    private void SwitchToNormalMode() {
        if (_currentMode == PowerMode.Normal)
            return;

        try {
            // Восстанавливаем приоритет процесса
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.Normal;

            // Восстанавливаем настройки ThreadPool
            ThreadPool.SetMinThreads(_normalMinWorkerThreads, _normalMinCompletionThreads);

            _currentMode = PowerMode.Normal;
            LogSwitchedToNormalMode();
        } catch (Exception ex) {
            LogFailedToSwitchMode(ex, "normal");
        }
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        _idleCheckTimer.Dispose();

        // Восстанавливаем нормальный режим при завершении
        if (_currentMode == PowerMode.Idle) {
            SwitchToNormalMode();
        }
    }

    // Logging
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "[PowerManagement] Service started. Idle timeout: {TimeoutMinutes} minutes")]
    private partial void LogServiceStarted(double timeoutMinutes);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "[PowerManagement] Switched to IDLE mode (low priority, reduced threads)")]
    private partial void LogSwitchedToIdleMode();

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "[PowerManagement] Switched to NORMAL mode (normal priority, full threads)")]
    private partial void LogSwitchedToNormalMode();

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "[PowerManagement] Failed to switch to {Mode} mode")]
    private partial void LogFailedToSwitchMode(Exception ex, string mode);
}

public enum PowerMode {
    Normal,
    Idle
}

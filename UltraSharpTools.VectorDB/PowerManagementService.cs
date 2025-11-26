using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB;

/// <summary>
/// Управляет энергопотреблением процесса.
/// После периода неактивности переключает в энергоэффективный режим (EcoQoS на Windows 11+).
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

    // Настройки для разных режимов ThreadPool
    private readonly int _normalMinWorkerThreads;
    private readonly int _normalMinCompletionThreads;
    // Для VectorDB нужно минимум 2 потока: 1 для WaitForConnectionAsync completion,
    // 1 для обработки запросов. С 1 потоком pipe listener может не успеть принять подключение.
    private const int IdleMinWorkerThreads = 2;
    private const int IdleMinCompletionThreads = 2;

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

        LogServiceStarted(_idleTimeout.TotalMinutes, EfficiencyModeHelper.IsEcoQosSupported());
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
            // NOTE: НЕ включаем EcoQoS - он слишком агрессивно троттлит процесс
            // и pipe listener не успевает принять входящие подключения.
            // Вместо этого только уменьшаем ThreadPool для экономии памяти.
            // EfficiencyModeHelper.EnableEfficiencyMode(_logger); // DISABLED

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
            // NOTE: EcoQoS отключен в idle mode, так что здесь ничего не нужно
            // EfficiencyModeHelper.DisableEfficiencyMode(_logger); // DISABLED

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
    Message = "[PowerManagement] Service started. Idle timeout: {TimeoutMinutes} minutes, EcoQoS supported: {EcoQosSupported}")]
    private partial void LogServiceStarted(double timeoutMinutes, bool ecoQosSupported);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
    Message = "[PowerManagement] Switched to IDLE mode (Efficiency Mode active)")]
    private partial void LogSwitchedToIdleMode();

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
    Message = "[PowerManagement] Switched to NORMAL mode")]
    private partial void LogSwitchedToNormalMode();

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
    Message = "[PowerManagement] Failed to switch to {Mode} mode")]
    private partial void LogFailedToSwitchMode(Exception ex, string mode);
}

public enum PowerMode {
    Normal,
    Idle
}

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UltraSharpTools.VectorDB;
/// <summary>
/// Сервис управления энергопотреблением VectorDB.
/// Поддерживает idle режим с EcoQoS и wake-up через Named Event.
/// </summary>
public sealed partial class PowerManagementService : IDisposable {
    private readonly ILogger<PowerManagementService> _logger;
    private readonly TimeSpan _idleTimeout;
    private readonly Timer _idleCheckTimer;
    private readonly object _lock = new();

    // Named Event для wake-up из idle режима
    // Droid сигнализирует этот event перед подключением
    public const string WakeUpEventName = "UltraSharpTools_VectorDB_WakeUp";
    private EventWaitHandle? _wakeUpEvent;
    private Thread? _wakeUpThread;
    private volatile bool _stopWakeUpThread;

    private DateTime _lastActivityTime;
    private PowerMode _currentMode = PowerMode.Normal;
    private bool _disposed;

    // Настройки для разных режимов ThreadPool
    private readonly int _normalMinWorkerThreads;
    private readonly int _normalMinCompletionThreads;
    // В idle режиме с EcoQoS нам достаточно 1 потока - wake-up теперь через Event
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

        // Создаем Named Event для wake-up (auto-reset)
        try {
            _wakeUpEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: WakeUpEventName
            );

            // Запускаем поток для прослушивания wake-up сигналов
            _wakeUpThread = new Thread(WakeUpThreadLoop) {
                Name = "VectorDB-WakeUp",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _wakeUpThread.Start();

            LogWakeUpEventCreated(WakeUpEventName);
        } catch (Exception ex) {
            LogFailedToCreateWakeUpEvent(ex);
            // Продолжаем без wake-up event - будем полагаться на pipe listener
        }

        // Проверка каждые 30 секунд
        _idleCheckTimer = new Timer(
        CheckIdleState,
        null,
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30));

        LogServiceStarted(_idleTimeout.TotalMinutes, EfficiencyModeHelper.IsEcoQosSupported());
    }

    /// <summary>
    /// Поток прослушивания wake-up сигналов.
    /// Использует WaitForSingleObject - не зависит от ThreadPool.
    /// </summary>
    private void WakeUpThreadLoop() {
        while (!_stopWakeUpThread && _wakeUpEvent != null) {
            try {
                // Ждём сигнал wake-up (с таймаутом для проверки _stopWakeUpThread)
                if (_wakeUpEvent.WaitOne(1000)) {
                    // Получен сигнал wake-up от Droid!
                    LogWakeUpSignalReceived();
                    RecordActivity(); // Это вызовет SwitchToNormalMode() если мы в idle
                }
            } catch (ObjectDisposedException) {
                break;
            } catch (Exception ex) {
                LogWakeUpThreadError(ex);
                Thread.Sleep(1000); // Пауза перед повтором при ошибке
            }
        }
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
            // Включаем EcoQoS для экономии батареи
            // Wake-up теперь через Named Event - не зависит от ThreadPool
            EfficiencyModeHelper.EnableEfficiencyMode(_logger);

            // Минимизируем ThreadPool
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
            // Отключаем EcoQoS
            EfficiencyModeHelper.DisableEfficiencyMode(_logger);

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

        // Останавливаем wake-up поток
        _stopWakeUpThread = true;
        _wakeUpEvent?.Set(); // Разбудить поток чтобы он увидел _stopWakeUpThread
        _wakeUpThread?.Join(2000);

        _wakeUpEvent?.Dispose();
        _wakeUpEvent = null;

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
    Message = "[PowerManagement] Switched to IDLE mode (EcoQoS + minimal ThreadPool)")]
    private partial void LogSwitchedToIdleMode();

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
    Message = "[PowerManagement] Switched to NORMAL mode")]
    private partial void LogSwitchedToNormalMode();

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
    Message = "[PowerManagement] Failed to switch to {Mode} mode")]
    private partial void LogFailedToSwitchMode(Exception ex, string mode);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information,
    Message = "[PowerManagement] Wake-up event created: {EventName}")]
    private partial void LogWakeUpEventCreated(string eventName);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning,
    Message = "[PowerManagement] Failed to create wake-up event")]
    private partial void LogFailedToCreateWakeUpEvent(Exception ex);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information,
    Message = "[PowerManagement] Wake-up signal received from Droid!")]
    private partial void LogWakeUpSignalReceived();

    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning,
    Message = "[PowerManagement] Wake-up thread error")]
    private partial void LogWakeUpThreadError(Exception ex);
}
public enum PowerMode {
    Normal,
    Idle
}

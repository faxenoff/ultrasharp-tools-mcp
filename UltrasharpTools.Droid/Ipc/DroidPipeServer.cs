using System.IO.Pipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Ipc;

/// <summary>
/// Named Pipe сервер для приёма подключений от Comm.
/// Поддерживает несколько одновременных клиентов (каждый Comm = отдельный pipe instance).
/// </summary>
public sealed partial class DroidPipeServer : BackgroundService {
    public const string PipeName = "UltraSharpTools_Droid";
    public const string WakeUpEventName = "UltraSharpTools_Droid_WakeUp";
    private const int MaxClients = 10;

    private readonly ILogger<DroidPipeServer> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Func<Stream, Stream, CancellationToken, Task> _clientHandler;

    private EventWaitHandle? _wakeUpEvent;
    private Thread? _wakeUpThread;
    private volatile bool _stopping;

    /// <summary>
    /// Создаёт pipe сервер.
    /// </summary>
    /// <param name="clientHandler">Обработчик клиента: (input, output, ct) => Task</param>
    public DroidPipeServer(
    ILogger<DroidPipeServer> logger,
    IHostApplicationLifetime lifetime,
    Func<Stream, Stream, CancellationToken, Task> clientHandler) {
        _logger = logger;
        _lifetime = lifetime;
        _clientHandler = clientHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        // Создаём Named Event для wake-up (клиенты сигнализируют перед подключением)
        try {
            _wakeUpEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: WakeUpEventName
            );
            _wakeUpThread = new Thread(WakeUpThreadLoop) {
                Name = "Droid-WakeUp",
                IsBackground = true
            };
            _wakeUpThread.Start();
            LogWakeUpEventCreated(WakeUpEventName);
        } catch (Exception ex) {
            LogFailedToCreateWakeUpEvent(ex);
        }

        LogPipeServerStarted(PipeName, MaxClients);

        // Запускаем listener loop
        var activeConnections = new List<Task>();

        try {
            while (!stoppingToken.IsCancellationRequested) {
                // Создаём pipe instance для следующего клиента
                using var pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                MaxClients,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
                );

                LogWaitingForConnection();

                try {
                    await pipeServer.WaitForConnectionAsync(stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }

                LogClientConnected();

                // Обрабатываем клиента в отдельном Task
                var connectionTask = HandleClientAsync(pipeServer, stoppingToken);
                activeConnections.Add(connectionTask);

                // Очищаем завершённые подключения
                activeConnections.RemoveAll(t => t.IsCompleted);
            }
        } finally {
            // Ждём завершения всех активных подключений
            if (activeConnections.Count > 0) {
                LogWaitingForClientsToDisconnect(activeConnections.Count);
                await Task.WhenAll(activeConnections);
            }

            _stopping = true;
            _wakeUpEvent?.Set();
            _wakeUpThread?.Join(1000);
            _wakeUpEvent?.Dispose();

            LogPipeServerStopped();
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken) {
        try {
            // Создаём дуплексный stream wrapper для клиента
            await _clientHandler(pipe, pipe, stoppingToken);
        } catch (IOException ex) {
            LogClientDisconnectedWithError(ex);
        } catch (OperationCanceledException) {
            // Нормальное завершение
        } catch (Exception ex) {
            LogClientError(ex);
        } finally {
            LogClientDisconnected();
            try {
                if (pipe.IsConnected) {
                    pipe.Disconnect();
                }
            } catch {
                // Ignore disconnect errors
            }
        }
    }

    private void WakeUpThreadLoop() {
        while (!_stopping && _wakeUpEvent != null) {
            try {
                if (_wakeUpEvent.WaitOne(1000)) {
                    LogWakeUpSignalReceived();
                    // Wake-up получен - можно добавить логику выхода из idle mode
                }
            } catch (ObjectDisposedException) {
                break;
            } catch (Exception ex) {
                LogWakeUpThreadError(ex);
                Thread.Sleep(500);
            }
        }
    }

    // Logging
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Started on pipe '{PipeName}', max clients: {MaxClients}")]
    private partial void LogPipeServerStarted(string pipeName, int maxClients);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug,
    Message = "[DroidPipeServer] Waiting for client connection...")]
    private partial void LogWaitingForConnection();

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Client connected")]
    private partial void LogClientConnected();

    [LoggerMessage(EventId = 3003, Level = LogLevel.Debug,
    Message = "[DroidPipeServer] Client disconnected")]
    private partial void LogClientDisconnected();

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning,
    Message = "[DroidPipeServer] Client disconnected with error")]
    private partial void LogClientDisconnectedWithError(Exception ex);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Error,
    Message = "[DroidPipeServer] Client handler error")]
    private partial void LogClientError(Exception ex);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Waiting for {Count} clients to disconnect...")]
    private partial void LogWaitingForClientsToDisconnect(int count);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Stopped")]
    private partial void LogPipeServerStopped();

    [LoggerMessage(EventId = 3008, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Wake-up event created: {EventName}")]
    private partial void LogWakeUpEventCreated(string eventName);

    [LoggerMessage(EventId = 3009, Level = LogLevel.Warning,
    Message = "[DroidPipeServer] Failed to create wake-up event")]
    private partial void LogFailedToCreateWakeUpEvent(Exception ex);

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information,
    Message = "[DroidPipeServer] Wake-up signal received")]
    private partial void LogWakeUpSignalReceived();

    [LoggerMessage(EventId = 3011, Level = LogLevel.Warning,
    Message = "[DroidPipeServer] Wake-up thread error")]
    private partial void LogWakeUpThreadError(Exception ex);
}

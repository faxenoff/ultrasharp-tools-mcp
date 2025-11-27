using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Infrastructure.Cache;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Ipc;
using UltrasharpTools.Tools.Merge;
using UltrasharpTools.Tools.Merge.Git;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Services;

namespace UltrasharpTools.Droid.Ipc;
/// <summary>
/// Режим работы Droid как Named Pipe сервера.
/// Позволяет нескольким Comm подключаться к одному Droid.
/// </summary>
public sealed class PipeServerMode : IAsyncDisposable {
    public const string PipeName = "UltraSharpTools_Droid";
    public const string WakeUpEventName = "UltraSharpTools_Droid_WakeUp";
    private const int MaxClients = 10;

    /// <summary>
    /// Время простоя без клиентов до автоматического завершения (5 минут).
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger;
    private readonly IServiceProvider _sharedServices;
    private readonly IClientContextService _clientContext;
    private readonly CancellationTokenSource _cts = new();

    private EventWaitHandle? _wakeUpEvent;
    private Thread? _wakeUpThread;
    private volatile bool _stopping;

    // Host -> ClientId mapping для отслеживания клиентов
    private readonly Dictionary<IHost, string> _hostClientIds = new();
    private readonly List<IHost> _activeHosts = [];
    private readonly object _hostsLock = new();

    private int _activeClientCount;
    private Timer? _idleTimer;

    public PipeServerMode(IServiceProvider sharedServices, ILogger logger) {
        _sharedServices = sharedServices;
        _logger = logger;

        // Получаем ClientContextService из shared services
        _clientContext = sharedServices.GetService<IClientContextService>()
            ?? new NullClientContextService();
    }

    /// <summary>
    /// Запускает pipe server и ожидает подключений.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default) {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        // Создаём Named Event для wake-up
        SetupWakeUpEvent();

        _logger.LogInformation("[PipeServer] Started on pipe '{PipeName}', max clients: {MaxClients}", PipeName, MaxClients);

        try {
            while (!linkedCts.Token.IsCancellationRequested) {
                // Создаём pipe instance для следующего клиента
                // Pipe disposal handled: on cancel at catch block, otherwise in HandleClientAsync
#pragma warning disable CA2000 // Dispose handled in HandleClientAsync or catch block
                var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    MaxClients,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );
#pragma warning restore CA2000

                _logger.LogDebug("[PipeServer] Waiting for client connection...");

                try {
                    await pipeServer.WaitForConnectionAsync(linkedCts.Token);
                } catch (OperationCanceledException) {
                    pipeServer.Dispose();
                    break;
                }

                _logger.LogInformation("[PipeServer] Client connected");

                // Новый клиент - отменяем idle timer
                OnClientConnected();

                // Запускаем MCP сессию для клиента в отдельном Task
                _ = Task.Run(() => HandleClientAsync(pipeServer, linkedCts.Token), linkedCts.Token);

                // Очищаем завершённые hosts
                CleanupCompletedHosts();
            }
        } finally {
            _stopping = true;

            // Останавливаем idle timer
            _idleTimer?.Dispose();
            _idleTimer = null;

            // Останавливаем wake-up thread
            _wakeUpEvent?.Set();
            _wakeUpThread?.Join(1000);
            _wakeUpEvent?.Dispose();

            // Останавливаем все активные hosts
            await StopAllHostsAsync();

            _logger.LogInformation("[PipeServer] Stopped");
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken) {
        IHost? host = null;
        string? clientId = null;

        try {
            // Генерируем уникальный ID для клиента
            clientId = Guid.NewGuid().ToString("N")[..12];
            _logger.LogInformation("[PipeServer] Client {ClientId} session starting", clientId);

            // Создаём Host с McpServer для этого клиента
            host = CreateHostForClient(pipe, clientId);

            lock (_hostsLock) {
                _activeHosts.Add(host);
                _hostClientIds[host] = clientId;
            }

            // Запускаем MCP сессию
            await host.RunAsync(cancellationToken);

            _logger.LogDebug("[PipeServer] Client {ClientId} session ended normally", clientId);
        } catch (OperationCanceledException) {
            // Normal shutdown
        } catch (IOException ex) {
            _logger.LogWarning("[PipeServer] Client {ClientId} disconnected: {Message}", clientId, ex.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "[PipeServer] Client {ClientId} session error", clientId);
        } finally {
            if (host != null) {
                lock (_hostsLock) {
                    _activeHosts.Remove(host);
                    _hostClientIds.Remove(host);
                }
                host.Dispose();
            }

            try {
                if (pipe.IsConnected) {
                    pipe.Disconnect();
                }
                pipe.Dispose();
            } catch {
                // Ignore cleanup errors
            }

            // Уведомляем ClientContextService об отключении клиента
            if (clientId != null) {
                var solutionUnloaded = _clientContext.OnClientDisconnected(clientId);
                if (solutionUnloaded) {
                    _logger.LogInformation("[PipeServer] Client {ClientId} was last user, solution unloaded", clientId);
                }
            }

            // Клиент отключился - проверяем нужен ли idle timer
            OnClientDisconnected();
        }
    }

    private void OnClientConnected() {
        // Отменяем idle timer если был запущен
        _idleTimer?.Dispose();
        _idleTimer = null;

        Interlocked.Increment(ref _activeClientCount);
        _logger.LogDebug("[PipeServer] Active clients: {Count}", _activeClientCount);
    }

    private void OnClientDisconnected() {
        var count = Interlocked.Decrement(ref _activeClientCount);
        _logger.LogDebug("[PipeServer] Active clients: {Count}", count);

        if (count <= 0) {
            // Последний клиент отключился - запускаем idle timer
            _logger.LogInformation("[PipeServer] No active clients, starting idle timer ({Timeout} minutes)", IdleTimeout.TotalMinutes);
            _idleTimer = new Timer(IdleTimerCallback, null, IdleTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private void IdleTimerCallback(object? state) {
        if (_activeClientCount <= 0 && !_stopping) {
            _logger.LogInformation("[PipeServer] Idle timeout reached, shutting down...");
            _cts.Cancel();
        }
    }

    private IHost CreateHostForClient(NamedPipeServerStream pipe, string clientId) {
        var builder = Host.CreateApplicationBuilder();

        // Регистрируем ClientIdProvider для этого клиента
        builder.Services.AddSingleton<IClientIdProvider>(new PipeClientIdProvider(clientId));

        // Используем общие сервисы (SolutionManager, GitService, etc.)
        RegisterSharedServices(builder.Services);

        // Настраиваем MCP Server с pipe transport и tools
        builder.Services
            .AddMcpServer(options => {
                options.ServerInfo = new Implementation {
                    Name = Program.ApplicationName,
                    Version = Program.ApplicationVersion,
                };
            })
            .WithStreamServerTransport(pipe, pipe) // Используем Named Pipe как transport
            .WithUltrasharpTools(); // Регистрируем MCP tools

        // Минимальное логирование для клиентских сессий
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        return builder.Build();
    }

    private void RegisterSharedServices(IServiceCollection services) {
        // Регистрируем ВСЕ singleton сервисы из shared provider
        // Они будут общими для всех клиентов

        // Core services
        RegisterIfAvailable<ISolutionManager>(services);
        RegisterIfAvailable<IGitService>(services);
        RegisterIfAvailable<IFormattingService>(services);
        RegisterIfAvailable<ICodeAnalysisService>(services);
        RegisterIfAvailable<ICodeModificationService>(services);
        RegisterIfAvailable<IDocumentOperationsService>(services);
        RegisterIfAvailable<ILoadingOrchestrator>(services);
        RegisterIfAvailable<IComplexityAnalysisService>(services);
        RegisterIfAvailable<ISemanticSimilarityService>(services);
        RegisterIfAvailable<ISourceResolutionService>(services);
        RegisterIfAvailable<IExecutionTraceService>(services);
        RegisterIfAvailable<IBacktraceService>(services);
        RegisterIfAvailable<ILogAnalysisService>(services);
        RegisterIfAvailable<IDiagnosticService>(services);
        RegisterIfAvailable<ICodeFixService>(services);
        RegisterIfAvailable<IQuickLintService>(services);
        RegisterIfAvailable<IEditorConfigProvider>(services);
        RegisterIfAvailable<IFuzzyFqnLookupService>(services);
        RegisterIfAvailable<ISymbolicExecutionService>(services);
        RegisterIfAvailable<ICallGraphCacheService>(services);

        // Concrete types that tools depend on
        RegisterIfAvailable<CallGraphIndexer>(services);
        RegisterIfAvailable<AnalysisCacheService>(services);
        RegisterIfAvailable<UltrasharpTools.Tools.Preview.PreviewManager>(services);
        RegisterIfAvailable<UltrasharpTools.Tools.Versioning.VersionManager>(services);
        RegisterIfAvailable<ImportUpdateService>(services);
        RegisterIfAvailable<NuGetHttpService>(services);

        // Semantic services (optional)
        RegisterIfAvailable<ISemanticSearchService>(services);
        RegisterIfAvailable<VectorDBClient>(services);

        // Semantic Replace services
        RegisterIfAvailable<IPatternMatcherService>(services);
        RegisterIfAvailable<IContextExtractorService>(services);
        RegisterIfAvailable<IBatchReplacerService>(services);
        RegisterIfAvailable<ISemanticReplaceService>(services);

        // Semantic Merge services
        RegisterIfAvailable<BranchMergeService>(services);
        RegisterIfAvailable<SemanticMergeService>(services);

        // Semantic Mode Provider (для get_capabilities)
        RegisterIfAvailable<ISemanticModeProvider>(services);

        // Client Context Service (для reference counting)
        RegisterIfAvailable<IClientContextService>(services);

        // Logging
        var loggerFactory = _sharedServices.GetService<ILoggerFactory>();
        if (loggerFactory != null) {
            services.AddSingleton(loggerFactory);
        }
    }

    private void RegisterIfAvailable<T>(IServiceCollection services) where T : class {
        var service = _sharedServices.GetService<T>();
        if (service != null) {
            services.AddSingleton(service);
        }
    }

    private void CleanupCompletedHosts() {
        lock (_hostsLock) {
            // Hosts которые завершились удаляются в HandleClientAsync
            // Здесь можно добавить дополнительную логику очистки
        }
    }

    private async Task StopAllHostsAsync() {
        List<IHost> hostsToStop;
        lock (_hostsLock) {
            hostsToStop = [.. _activeHosts];
            _activeHosts.Clear();
            _hostClientIds.Clear();
        }

        foreach (var host in hostsToStop) {
            try {
                await host.StopAsync(TimeSpan.FromSeconds(5));
                host.Dispose();
            } catch {
                // Ignore stop errors
            }
        }
    }

    private void SetupWakeUpEvent() {
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

            _logger.LogInformation("[PipeServer] Wake-up event created: {EventName}", WakeUpEventName);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "[PipeServer] Failed to create wake-up event");
        }
    }

    private void WakeUpThreadLoop() {
        while (!_stopping && _wakeUpEvent != null) {
            try {
                if (_wakeUpEvent.WaitOne(1000)) {
                    _logger.LogDebug("[PipeServer] Wake-up signal received");
                    // Wake-up обработан - ThreadPool должен быть активен
                }
            } catch (ObjectDisposedException) {
                break;
            } catch (Exception ex) {
                _logger.LogWarning(ex, "[PipeServer] Wake-up thread error");
                Thread.Sleep(500);
            }
        }
    }

    public async ValueTask DisposeAsync() {
        _idleTimer?.Dispose();
        await _cts.CancelAsync();
        _cts.Dispose();
    }
}

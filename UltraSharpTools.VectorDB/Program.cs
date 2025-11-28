using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltraSharpTools.VectorDB;
using UltraSharpTools.VectorDB.Semantic;
using UltraSharpTools.VectorDB.Semantic.Embedding;
using UltraSharpTools.VectorDB.Semantic.GPU;

public class Program {
    public const string ApplicationName = "UltraSharpTools.VectorDB";
    public const string ApplicationVersion = "3.6.1";

    // VectorDB: отдельный процесс для векторизации и семантического поиска
    // Принимает запросы от Droid через Named Pipe IPC

    private const string PipeName = "UltraSharpTools_VectorDB";
    private const int ParentCheckIntervalMs = 2000;

    public static async Task Main(string[] args) {
        // Парсим аргументы
        int? parentPid = null;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--parent-pid" && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid)) {
                parentPid = pid;
            }
        }

        // Читаем конфигурацию из semantic-config.json
        var semanticConfig = LoadSemanticConfig();

        // Настройка DI контейнера
        var services = new ServiceCollection();

        // Логирование
        services.AddLogging(builder => {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // HttpClient для embedding providers
        services.AddHttpClient();

        // Конфигурация для embeddings (читаем из semantic-config.json)
        services.Configure<EmbeddingOptions>(options => {
            options.Provider = semanticConfig.Provider;
            options.Enabled = true;
            options.AutoDetectGPU = false; // Отключено в VectorDB

            // TEI настройки
            options.TEI.BaseUrl = semanticConfig.TeiEndpoint;
            options.TEI.TimeoutMs = 30000;

            // Ollama настройки
            options.Ollama.BaseUrl = semanticConfig.OllamaEndpoint;
            options.Ollama.Model = semanticConfig.OllamaModel;
            options.Ollama.TimeoutMs = 10000;
            options.Ollama.CheckServer = true;
            options.Ollama.AutoPull = true;
        });

        // GPU Detection (stub для Indexer)
        services.AddSingleton<IGPUDetectionService, GPUDetectionService>();

        // Embedding provider factory
        services.AddSingleton<EmbeddingProviderFactory>();

        // Создаем ServiceProvider
        var serviceProvider = services.BuildServiceProvider();

        // Инициализация сервисов (асинхронная часть)
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("[VectorDB] Starting semantic indexing service v{Version}...", ApplicationVersion);

        // Создаем CancellationTokenSource для graceful shutdown
        using var cts = new CancellationTokenSource();

        // Запускаем мониторинг родительского процесса
        if (parentPid.HasValue) {
            _ = MonitorParentProcessAsync(parentPid.Value, cts, logger);
            logger.LogInformation("[VectorDB] Monitoring parent process PID: {ParentPid}", parentPid.Value);
        } else {
            logger.LogWarning("[VectorDB] No parent PID specified - will not auto-terminate when orphaned");
        }

        // Создаем PowerManagementService (idle timeout 3 минуты)
        var powerLogger = serviceProvider.GetRequiredService<ILogger<PowerManagementService>>();
        using var powerManagement = new PowerManagementService(
        powerLogger,
        idleTimeout: TimeSpan.FromMinutes(3)
        );

        // Пытаемся инициализировать semantic сервисы
        VectorDBService indexerService;
        string? providerError = null;

        try {
            indexerService = await InitializeFullServiceAsync(serviceProvider, powerManagement, logger);
            logger.LogInformation("[VectorDB] All services initialized successfully - running in FULL mode");
        } catch (Exception ex) {
            // Graceful degradation: запускаем в degraded режиме
            providerError = ex.Message;
            logger.LogWarning(ex, "[VectorDB] Failed to initialize embedding provider. Starting in DEGRADED mode.");

            var indexerLogger = serviceProvider.GetRequiredService<ILogger<VectorDBService>>();
            indexerService = new VectorDBService(indexerLogger, powerManagement, providerError);

            logger.LogWarning("[VectorDB] Running in DEGRADED mode - semantic operations will return errors");
        }

        // Основной цикл обработки подключений
        await RunConnectionLoopAsync(indexerService, cts, logger);
    }

    /// <summary>
    /// Инициализирует все сервисы в полном режиме
    /// </summary>
    private static async Task<VectorDBService> InitializeFullServiceAsync(
    ServiceProvider serviceProvider,
    PowerManagementService powerManagement,
    ILogger logger) {
        // 1. Создаем embedding provider
        logger.LogInformation("[VectorDB] Initializing embedding provider...");
        var providerFactory = serviceProvider.GetRequiredService<EmbeddingProviderFactory>();
        var embeddingProvider = await providerFactory.CreateAsync();
        logger.LogInformation(
        "[VectorDB] Embedding provider ready: {Provider} (dimension: {Dimension})",
        embeddingProvider.Name,
        embeddingProvider.Dimension
        );

        // 2. Создаем EmbeddingGenerator с provider
        var embeddingGeneratorLogger = serviceProvider.GetRequiredService<ILogger<EmbeddingGenerator>>();
#pragma warning disable CA2000 // EmbeddingGenerator живет до завершения приложения
        var embeddingGenerator = new EmbeddingGenerator(
        embeddingProvider,
        EmbeddingGeneratorConfig.Default,
        embeddingGeneratorLogger
        );
#pragma warning restore CA2000

        // 3. Создаем и инициализируем VectorStore
        logger.LogInformation("[VectorDB] Initializing vector store...");
        var dimension = embeddingProvider.Dimension ?? 384;

        var vectorStoreConfig = VectorStoreConfig.ForProduction(
        databasePath: Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UltraSharpTools",
        "indexer.db"
        ),
        dimension: dimension
        );

        var vectorStoreLogger = serviceProvider.GetRequiredService<ILogger<VectorStore>>();
#pragma warning disable CA2000 // VectorStore живет до завершения приложения
        var vectorStore = new VectorStore(vectorStoreConfig, vectorStoreLogger);
#pragma warning restore CA2000

        await vectorStore.InitializeAsync(
        vectorStoreConfig.ConnectionString,
        dimension,
        CancellationToken.None
        );

        logger.LogInformation("[VectorDB] Vector store ready (backend: {Backend})", vectorStore.GetCurrentBackend());

        // 4. Создаем VectorDBSemanticService с инициализированными зависимостями
        var semanticLogger = serviceProvider.GetRequiredService<ILogger<VectorDBSemanticService>>();
#pragma warning disable CA2000 // VectorDBSemanticService живет до завершения приложения
        var semanticService = new VectorDBSemanticService(
        vectorStore,
        embeddingGenerator,
        semanticLogger
        );
#pragma warning restore CA2000

        // 5. Создаем VectorDBService
        var indexerLogger = serviceProvider.GetRequiredService<ILogger<VectorDBService>>();
        return new VectorDBService(semanticService, indexerLogger, powerManagement, embeddingProvider.Name);
    }

    /// <summary>
    /// Основной цикл обработки подключений
    /// </summary>
    private static async Task RunConnectionLoopAsync(
    VectorDBService indexerService,
    CancellationTokenSource cts,
    ILogger logger) {
        try {
            while (!cts.Token.IsCancellationRequested) {
                // Создаем Named Pipe сервер для каждого подключения
                using var pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1, // Только одно подключение за раз
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
                );

                logger.LogInformation("[VectorDB] Waiting for Droid connection on pipe: {PipeName}", PipeName);

                try {
                    // Ждём подключения бесконечно (пока родительский процесс жив)
                    await pipeServer.WaitForConnectionAsync(cts.Token);

                    if (cts.Token.IsCancellationRequested) {
                        logger.LogInformation("[VectorDB] Shutdown requested during connection wait");
                        return;
                    }

                    logger.LogInformation("[VectorDB] Droid connected. Processing requests...");

                    // Обрабатываем запросы от Droid
                    await indexerService.ProcessRequestsAsync(pipeServer, cts.Token);

                    logger.LogInformation("[VectorDB] Client disconnected. Waiting for new connection...");
                } catch (OperationCanceledException) {
                    // Родитель умер или запрошено завершение
                    break;
                } catch (IOException ex) {
                    // Ошибка pipe (клиент резко отключился)
                    logger.LogWarning("[VectorDB] Pipe error: {Message}. Restarting listener...", ex.Message);
                }
            }

            logger.LogInformation("[VectorDB] Shutting down gracefully.");
        } catch (OperationCanceledException) {
            logger.LogInformation("[VectorDB] Shutdown requested. Exiting gracefully.");
        } catch (Exception ex) {
            logger.LogError(ex, "[VectorDB] Fatal error during connection processing");
            Console.Error.WriteLine($"[VectorDB] Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Мониторит родительский процесс и инициирует завершение при его смерти
    /// </summary>
    private static async Task MonitorParentProcessAsync(int parentPid, CancellationTokenSource cts, ILogger logger) {
        try {
            while (!cts.Token.IsCancellationRequested) {
                await Task.Delay(ParentCheckIntervalMs, cts.Token);

                // Проверяем существование процесса каждую итерацию
                // (не кэшируем Process объект - он может устареть)
                if (!IsProcessRunning(parentPid)) {
                    logger.LogInformation("[VectorDB] Parent process (PID: {ParentPid}) has exited. Initiating shutdown.", parentPid);
                    await cts.CancelAsync();
                    return;
                }
            }
        } catch (OperationCanceledException) {
            // Нормальное завершение
        } catch (Exception ex) {
            logger.LogError(ex, "[VectorDB] Error monitoring parent process. Continuing without parent monitoring.");
            // НЕ завершаемся при ошибке мониторинга - продолжаем работать
        }
    }

    /// <summary>
    /// Проверяет, запущен ли процесс с указанным PID
    /// </summary>
    private static bool IsProcessRunning(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            // Refresh для получения актуального состояния
            process.Refresh();
            return !process.HasExited;
        } catch (ArgumentException) {
            // Процесс не существует
            return false;
        } catch (InvalidOperationException) {
            // Процесс завершился между GetProcessById и HasExited
            return false;
        }
    }

    /// <summary>
    /// Загружает конфигурацию из semantic-config.json
    /// </summary>
    private static SemanticConfigResult LoadSemanticConfig() {
        var result = new SemanticConfigResult();

        try {
            // Централизованная директория конфигурации
            string configDir;
            if (OperatingSystem.IsWindows()) {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                configDir = Path.Combine(localAppData, "UltraSharpTools", "config");
            } else {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configDir = Path.Combine(home, ".ultrasharp", "config");
            }

            var configPath = Path.Combine(configDir, "semantic-config.json");

            if (!File.Exists(configPath)) {
                Console.WriteLine($"[VectorDB] Config not found: {configPath}, using defaults (ollama)");
                return result;
            }

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var root = doc.RootElement;

            // Парсим embedding.platform
            if (root.TryGetProperty("embedding", out var embedding)) {
                if (embedding.TryGetProperty("platform", out var platform)) {
                    result.Provider = platform.GetString()?.ToLowerInvariant() ?? "ollama";
                }

                // TEI настройки
                if (embedding.TryGetProperty("tei", out var tei)) {
                    if (tei.TryGetProperty("endpoint", out var endpoint)) {
                        result.TeiEndpoint = endpoint.GetString() ?? result.TeiEndpoint;
                    }
                }

                // Ollama настройки
                if (embedding.TryGetProperty("ollama", out var ollama)) {
                    if (ollama.TryGetProperty("endpoint", out var endpoint)) {
                        result.OllamaEndpoint = endpoint.GetString() ?? result.OllamaEndpoint;
                    }
                    if (ollama.TryGetProperty("selected_model", out var model)) {
                        result.OllamaModel = model.GetString() ?? result.OllamaModel;
                    }
                }
            }

            Console.WriteLine($"[VectorDB] Config loaded: provider={result.Provider}, tei={result.TeiEndpoint}, ollama={result.OllamaEndpoint}");
        } catch (Exception ex) {
            Console.WriteLine($"[VectorDB] Error loading config: {ex.Message}, using defaults");
        }

        return result;
    }

    /// <summary>
    /// Результат загрузки конфигурации
    /// </summary>
    private sealed class SemanticConfigResult {
        public string Provider { get; set; } = "ollama";
        public string TeiEndpoint { get; set; } = "http://127.0.0.1:8080";
        public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434";
        public string OllamaModel { get; set; } = "granite-embedding";
    }
}

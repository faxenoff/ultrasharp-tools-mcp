using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltraSharpTools.VectorDB;
using UltraSharpTools.VectorDB.Semantic;
using UltraSharpTools.VectorDB.Semantic.Embedding;
using UltraSharpTools.VectorDB.Semantic.GPU;

public class Program
{
    public const string ApplicationName = "UltraSharpTools.VectorDB";
    public const string ApplicationVersion = "3.0.7";

    // VectorDB: отдельный процесс для векторизации и семантического поиска
    // Принимает запросы от Droid через Named Pipe IPC

    private const string PipeName = "UltraSharpTools_VectorDB";

    public static async Task Main(string[] args)
    {
        // Настройка DI контейнера
        var services = new ServiceCollection();

        // Логирование
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // HttpClient для embedding providers
        services.AddHttpClient();

        // Конфигурация для embeddings
        services.Configure<EmbeddingOptions>(options =>
        {
            // Используем Ollama по умолчанию
            options.Provider = "ollama";
            options.Enabled = true;
            options.AutoDetectGPU = false; // Отключено в VectorDB

            // Ollama настройки
            options.Ollama.BaseUrl = "http://127.0.0.1:11434";
            options.Ollama.Model = "granite-embedding";
            options.Ollama.TimeoutMs = 10000;
            options.Ollama.CheckServer = true;
            options.Ollama.AutoPull = true;
        });

        // GPU Detection (stub для Indexer)
        services.AddSingleton<IGPUDetectionService, GPUDetectionService>();

        // Embedding provider factory
        services.AddSingleton<EmbeddingProviderFactory>();

        // Все semantic сервисы (VectorStore, EmbeddingGenerator, VectorDBSemanticService, VectorDBService)
        // будут созданы вручную после асинхронной инициализации embedding provider

        // Создаем ServiceProvider
        var serviceProvider = services.BuildServiceProvider();

        // Инициализация сервисов (асинхронная часть)
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("[VectorDB] Starting semantic indexing service...");

        try
        {
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
            var vectorStore = new VectorStore(vectorStoreConfig, vectorStoreLogger);

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
            var indexerService = new VectorDBService(semanticService, indexerLogger);

            logger.LogInformation("[VectorDB] All services initialized successfully");

            // Создаем Named Pipe сервер
            using var pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1, // Только одно подключение (от Droid)
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            logger.LogInformation("[VectorDB] Waiting for Droid connection on pipe: {PipeName}", PipeName);

            await pipeServer.WaitForConnectionAsync();

            logger.LogInformation("[VectorDB] Droid connected. Processing requests...");

            // Обрабатываем запросы от Droid
            await indexerService.ProcessRequestsAsync(pipeServer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VectorDB] Fatal error during initialization or processing");
            Console.Error.WriteLine($"[VectorDB] Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}

using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltraSharpTools.Indexer;
using UltraSharpTools.Indexer.Semantic;
using UltraSharpTools.Indexer.Semantic.Embedding;
using UltraSharpTools.Indexer.Semantic.GPU;

public class Program
{
    public const string ApplicationName = "UltraSharpTools.Indexer";
    public const string ApplicationVersion = "3.0.7";

    // Indexer: отдельный процесс для семантической индексации и векторной БД
    // Принимает запросы от Droid через Named Pipe IPC

    private const string PipeName = "UltraSharpTools_Indexer";

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
            options.AutoDetectGPU = false; // Отключено в Indexer

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

        // Все semantic сервисы (VectorStore, EmbeddingGenerator, IndexerSemanticService, IndexerService)
        // будут созданы вручную после асинхронной инициализации embedding provider

        // Создаем ServiceProvider
        var serviceProvider = services.BuildServiceProvider();

        // Инициализация сервисов (асинхронная часть)
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("[Indexer] Starting semantic indexing service...");

        try
        {
            // 1. Создаем embedding provider
            logger.LogInformation("[Indexer] Initializing embedding provider...");
            var providerFactory = serviceProvider.GetRequiredService<EmbeddingProviderFactory>();
            var embeddingProvider = await providerFactory.CreateAsync();
            logger.LogInformation(
                "[Indexer] Embedding provider ready: {Provider} (dimension: {Dimension})",
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
            logger.LogInformation("[Indexer] Initializing vector store...");
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

            logger.LogInformation("[Indexer] Vector store ready (backend: {Backend})", vectorStore.GetCurrentBackend());

            // 4. Создаем IndexerSemanticService с инициализированными зависимостями
            var semanticLogger = serviceProvider.GetRequiredService<ILogger<IndexerSemanticService>>();
#pragma warning disable CA2000 // IndexerSemanticService живет до завершения приложения
            var semanticService = new IndexerSemanticService(
                vectorStore,
                embeddingGenerator,
                semanticLogger
            );
#pragma warning restore CA2000

            // 5. Создаем IndexerService
            var indexerLogger = serviceProvider.GetRequiredService<ILogger<IndexerService>>();
            var indexerService = new IndexerService(semanticService, indexerLogger);

            logger.LogInformation("[Indexer] All services initialized successfully");

            // Создаем Named Pipe сервер
            using var pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1, // Только одно подключение (от Droid)
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            logger.LogInformation("[Indexer] Waiting for Droid connection on pipe: {PipeName}", PipeName);

            await pipeServer.WaitForConnectionAsync();

            logger.LogInformation("[Indexer] Droid connected. Processing requests...");

            // Обрабатываем запросы от Droid
            await indexerService.ProcessRequestsAsync(pipeServer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Indexer] Fatal error during initialization or processing");
            Console.Error.WriteLine($"[Indexer] Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using UltrasharpTools.Tools.Config;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Logging;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Services;
using UltrasharpTools.Droid.Services;

#pragma warning disable CS0162 // Unreachable code detected (enableConsoleOutput = false)

namespace UltrasharpTools.Droid;

public static class Program
{
    public const string ApplicationName = "UltrasharpToolsMcpDroid";
    public const string ApplicationVersion = "3.2.0";

    private static readonly JsonSerializerOptions SemanticConfigJsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null, // Allow both snake_case and PascalCase
        };

    public static async Task<int> Main(string[] args)
    {
        _ = typeof(SolutionTools);
        _ = typeof(AnalysisTools);
        _ = typeof(ModificationTools);
        _ = typeof(SystemTools);

        var logDirOption = new Option<string?>("--log-directory")
        {
            Description =
                "Optional path to a log directory. If not specified, uses .ultrasharp/logs in project root.",
        };

        var logLevelOption = new Option<LogLevel>("--log-level")
        {
            Description = "Minimum log level for console and file.",
            DefaultValueFactory = _ => LogLevel.Information,
        };

        var loadSolutionOption = new Option<string?>("--load-solution")
        {
            Description = "Path to a solution file (.sln) to load immediately on startup.",
        };

        var buildConfigurationOption = new Option<string?>("--build-configuration")
        {
            Description =
                "Build configuration to use when loading the solution (Debug, Release, etc.).",
        };

        var disableGitOption = new Option<bool>("--disable-git")
        {
            Description = "Disable Git integration.",
            DefaultValueFactory = _ => false,
        };

        var modeOption = new Option<string>("--mode")
        {
            Description = "Operation mode: local (default) or hybrid (connect to Overlord server)",
            DefaultValueFactory = _ => "local",
        };

        var serverUrlOption = new Option<string?>("--server-url")
        {
            Description = "Overlord server URL (required for hybrid mode)",
            DefaultValueFactory = _ => null,
        };

        var embeddingUrlOption = new Option<string?>("--embedding-url")
        {
            Description = "Embedding service URL for hybrid mode (Ollama/TEI)",
            DefaultValueFactory = _ => "http://127.0.0.1:11434",
        };

        var embeddingModelOption = new Option<string?>("--embedding-model")
        {
            Description = "Embedding model name for hybrid mode",
            DefaultValueFactory = _ => "nomic-embed-text",
        };

        var gitBranchRetentionCountOption = new Option<int?>("--git-branch-retention-count")
        {
            Description = "Keep only the N most recent sharptools/* branches. (null = no limit)",
            DefaultValueFactory = _ => 10,
        };

        var gitBranchRetentionDaysOption = new Option<int?>("--git-branch-retention-days")
        {
            Description =
                "Keep sharptools/* branches created within the last N days. (null = no limit)",
            DefaultValueFactory = _ => null,
        };

        var gitAutoCleanupOption = new Option<bool>("--git-auto-cleanup")
        {
            Description = "Automatically cleanup old branches after each modification.",
            DefaultValueFactory = _ => true,
        };

        var autoReloadOption = new Option<bool>("--auto-reload")
        {
            Description = "Enable automatic solution reload when .csproj or .sln files change.",
            DefaultValueFactory = _ => false,
        };

        var reloadDebounceOption = new Option<int>("--reload-debounce-ms")
        {
            Description = "Debounce delay in milliseconds before triggering auto-reload.",
            DefaultValueFactory = _ => 2000,
        };

        var symbolCacheEnabledOption = new Option<bool>("--symbol-cache")
        {
            Description =
                "Enable persistent symbol cache for 10x faster solution initialization (33s → 3-5s).",
            DefaultValueFactory = _ => true,
        };

        var symbolCacheClearOption = new Option<bool>("--symbol-cache-clear")
        {
            Description = "Clear all symbol cache data on startup.",
            DefaultValueFactory = _ => false,
        };

        var symbolCacheDirectoryOption = new Option<string?>("--symbol-cache-directory")
        {
            Description =
                "Custom directory for symbol cache (default: %TEMP%/UltrasharpTools/SymbolCache).",
        };

        var lowMemoryOption = new Option<bool>("--low-memory")
        {
            Description =
                "Enable low memory mode - uses SQLite for reflection types instead of in-memory cache (~50MB savings).",
            DefaultValueFactory = _ => false,
        };

        var rootCommand = new RootCommand("UltrasharpTools MCP Droid")
        {
            logDirOption,
            logLevelOption,
            loadSolutionOption,
            buildConfigurationOption,
            disableGitOption,
            modeOption,
            serverUrlOption,
            embeddingUrlOption,
            embeddingModelOption,
            gitBranchRetentionCountOption,
            gitBranchRetentionDaysOption,
            gitAutoCleanupOption,
            autoReloadOption,
            reloadDebounceOption,
            symbolCacheEnabledOption,
            symbolCacheClearOption,
            symbolCacheDirectoryOption,
            lowMemoryOption,
        };

        // Parse arguments first to get values
        var parseResult = rootCommand.Parse(args);

        string? logDirPath = parseResult.GetValue(logDirOption);
        LogLevel minimumLogLevel = parseResult.GetValue(logLevelOption);
        string? solutionPath = parseResult.GetValue(loadSolutionOption);

        // Auto-detect solution file if not specified
        if (string.IsNullOrEmpty(solutionPath))
        {
            var currentDir = Directory.GetCurrentDirectory();
            var solutionFiles = Directory.GetFiles(currentDir, "*.sln");
            if (solutionFiles.Length == 1)
            {
                solutionPath = solutionFiles[0];
                // Console.WriteLine($"Auto-detected solution: {Path.GetFileName(solutionPath)}");
            }
            else if (solutionFiles.Length > 1)
            {
                // Console.WriteLine(
                //     $"Multiple solution files found in {currentDir}. Use --load-solution to specify which one to load."
                // );
            }
        }

        string? buildConfiguration = parseResult.GetValue(buildConfigurationOption);
        bool disableGit = parseResult.GetValue(disableGitOption);
        string mode = parseResult.GetValue(modeOption) ?? "local";
        string? serverUrl = parseResult.GetValue(serverUrlOption);
        string? embeddingUrl = parseResult.GetValue(embeddingUrlOption);
        string? embeddingModel = parseResult.GetValue(embeddingModelOption);
        int? gitBranchRetentionCount = parseResult.GetValue(gitBranchRetentionCountOption);
        int? gitBranchRetentionDays = parseResult.GetValue(gitBranchRetentionDaysOption);
        bool gitAutoCleanup = parseResult.GetValue(gitAutoCleanupOption);
        bool autoReload = parseResult.GetValue(autoReloadOption);
        int reloadDebounceMs = parseResult.GetValue(reloadDebounceOption);
        bool symbolCacheEnabled = parseResult.GetValue(symbolCacheEnabledOption);
        bool symbolCacheClear = parseResult.GetValue(symbolCacheClearOption);
        string? symbolCacheDirectory = parseResult.GetValue(symbolCacheDirectoryOption);
        bool lowMemoryMode = parseResult.GetValue(lowMemoryOption);

        // Use project-local logs directory if not specified
        if (string.IsNullOrWhiteSpace(logDirPath))
        {
            logDirPath = ProjectPathHelper.GetLogsPath(solutionPath);
        }

        // Create log directory if it doesn't exist
        if (!Directory.Exists(logDirPath))
        {
            try
            {
                Directory.CreateDirectory(logDirPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to create log directory: {ex.Message}");
                return 1;
            }
        }

        string logFilePath = Path.Combine(logDirPath, $"{ApplicationName}-{{Date:yyyyMMdd}}.log");
        // Console.Error.WriteLine(
        //     $"Logging to directory: {Path.GetFullPath(logDirPath)} with minimum level {minimumLogLevel}"
        // );

        // Early startup information (before DI/logging is configured)

        // Hybrid mode validation
        bool isHybridMode = mode == "hybrid";
        if (isHybridMode && string.IsNullOrEmpty(serverUrl))
        {
            Console.Error.WriteLine("Error: --server-url is required for hybrid mode");
            return 1;
        }

        // Console output disabled for MCP - all info goes to file logs only
        const bool enableConsoleOutput = false;

        if (enableConsoleOutput)
        {
            if (isHybridMode)
            {
                Console.WriteLine($"Running in HYBRID mode, server: {serverUrl}");
                Console.WriteLine($"Embedding service: {embeddingUrl}");
                Console.WriteLine($"Embedding model: {embeddingModel}");
            }
            else
            {
                Console.WriteLine("Running in LOCAL mode");
            }

            if (disableGit)
            {
                Console.WriteLine("Git integration is disabled.");
            }

            if (!string.IsNullOrEmpty(buildConfiguration))
            {
                Console.WriteLine($"Using build configuration: {buildConfiguration}");
            }

            if (autoReload)
            {
                Console.WriteLine($"Auto-reload is enabled with {reloadDebounceMs}ms debounce");
            }

            if (symbolCacheEnabled)
            {
                Console.WriteLine("Symbol cache is enabled (10x faster solution initialization)");
                if (!string.IsNullOrEmpty(symbolCacheDirectory))
                {
                    Console.WriteLine($"Symbol cache directory: {symbolCacheDirectory}");
                }
                if (symbolCacheClear)
                {
                    Console.WriteLine("Symbol cache will be cleared on startup");
                }
            }
            else
            {
                Console.WriteLine("Symbol cache is disabled");
            }
        }

        // Create builder WITHOUT default logging providers (MCP requires pure JSON-RPC)
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            DisableDefaults = true, // Disable all defaults including console logging
        });

        // Set content root to exe directory (not current working directory)
        var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exeDirectory))
        {
            builder.Environment.ContentRootPath = exeDirectory;
            Directory.SetCurrentDirectory(exeDirectory);
        }

        // CRITICAL: Disable ALL console output for MCP (requires pure JSON-RPC on stdout)
        builder.Logging.ClearProviders();
        builder.Logging.AddFile(logFilePath, minimumLogLevel);
        builder.Logging.SetMinimumLevel(minimumLogLevel);

        // Block all logging to console - only file logging allowed
        builder.Logging.AddFilter<Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>((category, level) => false);

        // Configure logging overrides - file only
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.CodeAnalysis", LogLevel.Information);
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

        // Suppress console status messages (Application started/stopped)
        builder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);

        // Create GitOptions from command line arguments
        var gitOptions = new UltrasharpTools.Tools.Models.GitOptions
        {
            RetentionCount = gitBranchRetentionCount,
            RetentionDays = gitBranchRetentionDays,
            AutoCleanup = gitAutoCleanup && !disableGit, // Only enable if Git is enabled
        };

        // Create SolutionReloadOptions from command line arguments
        var reloadOptions = new UltrasharpTools.Tools.Models.SolutionReloadOptions
        {
            AutoReloadEnabled = autoReload,
            DebounceDelayMs = reloadDebounceMs,
        };

        // Create SymbolCacheOptions from command line arguments
        var symbolCacheOptions = new UltrasharpTools.Tools.Models.SymbolCacheOptions
        {
            Enabled = symbolCacheEnabled,
            ClearOnStartup = symbolCacheClear,
            CacheDirectory = symbolCacheDirectory,
        };

        builder.Services.WithUltrasharpToolsServices(
            !disableGit,
            buildConfiguration,
            gitOptions,
            reloadOptions,
            symbolCacheOptions,
            lowMemoryMode
        );

        // Register PowerManagementService for energy efficiency mode
        // After 3 minutes of inactivity, switches to EcoQoS mode (Windows 11+)
        builder.Services.AddHostedService<PowerManagementService>();

        // Auto-enable semantic RAG if semantic-config.json exists
        // Use centralized config directory: %LOCALAPPDATA%\UltraSharpTools\config (Windows)
        // or ~/.ultrasharp/config (Linux/macOS)
        var centralConfigDir = UltrasharpTools.Droid.Services.Hybrid.SemanticModeConfigurationLoader.CentralConfigDirectory;
        var semanticConfigPath = Path.Combine(centralConfigDir, "semantic-config.json");
        var hasSemanticConfig = File.Exists(semanticConfigPath);

        // Debug logging (before logger is available)
        File.AppendAllText(
            Path.Combine(logDirPath, "semantic-debug.log"),
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - centralConfigDir: {centralConfigDir}\n"
                + $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - semanticConfigPath: {semanticConfigPath}\n"
                + $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - hasSemanticConfig: {hasSemanticConfig}\n"
        );

        bool semanticEnabled = false;

        if (hasSemanticConfig)
        {
            if (enableConsoleOutput)
            {
                Console.WriteLine($"[Semantic] Found {Path.GetFileName(semanticConfigPath)}");
            }

            File.AppendAllText(
                Path.Combine(logDirPath, "semantic-debug.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Found semantic-config.json at: {semanticConfigPath}\n"
            );

            try
            {
                // Load config
                var configJson = await File.ReadAllTextAsync(semanticConfigPath);
                File.AppendAllText(
                    Path.Combine(logDirPath, "semantic-debug.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Config JSON length: {configJson.Length}\n"
                );

                var config = JsonSerializer.Deserialize<SemanticEmbeddingConfig>(
                    configJson,
                    SemanticConfigJsonOptions
                );

                File.AppendAllText(
                    Path.Combine(logDirPath, "semantic-debug.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Config deserialized: {config != null}, Platform: {config?.Embedding?.Platform}\n"
                );

                if (config != null)
                {
                    File.AppendAllText(
                        Path.Combine(logDirPath, "semantic-debug.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Starting health check for {config.Embedding.Platform}\n"
                    );

                    // Log endpoint before health check
                    var endpoint = string.Equals(
                        config.Embedding.Platform,
                        "tei",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? config.Embedding.Tei?.Endpoint
                        : config.Embedding.Ollama?.Endpoint;
                    File.AppendAllText(
                        Path.Combine(logDirPath, "semantic-debug.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Endpoint: {endpoint ?? "NULL"}\n"
                    );

                    // Quick check if service is available (10s timeout - generous for debugging)
                    var healthCheck = new SemanticServiceHealthCheck(
                        httpClientFactory: null,
                        logger: null
                    );
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    File.AppendAllText(
                        Path.Combine(logDirPath, "semantic-debug.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Calling QuickCheckAsync with 10s timeout\n"
                    );

                    bool isAvailable = false;
                    try
                    {
                        isAvailable = await healthCheck.QuickCheckAsync(config, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(
                            Path.Combine(logDirPath, "semantic-debug.log"),
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - QuickCheckAsync exception: {ex.GetType().Name}: {ex.Message}\n"
                        );
                    }

                    File.AppendAllText(
                        Path.Combine(logDirPath, "semantic-debug.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Health check result: {isAvailable}\n"
                    );

                    if (isAvailable)
                    {
                        if (enableConsoleOutput)
                        {
                            Console.WriteLine(
                                $"[Semantic] ✓ {config.Embedding.Platform.ToUpperInvariant()} service is available"
                            );
                        }

                        // Determine database path based on solution
                        string? databasePath = null;
                        if (!string.IsNullOrEmpty(solutionPath))
                        {
                            var solutionDir = Path.GetDirectoryName(solutionPath);
                            if (!string.IsNullOrEmpty(solutionDir))
                            {
                                databasePath = Path.Combine(
                                    solutionDir,
                                    ".ultrasharp",
                                    "semantic.db"
                                );
                            }
                        }

                        // Register semantic RAG services via external Indexer process
                        // Indexer.exe manages its own embedding configuration (Ollama/TEI)
                        // Communication via Named Pipe IPC for process isolation and performance
                        builder.Services.WithSemanticRagIndexer();

                        if (enableConsoleOutput)
                        {
                            Console.WriteLine(
                                $"[Semantic] Semantic RAG enabled (database: {databasePath ?? "in-memory"})"
                            );
                        }

                        semanticEnabled = true;

                        // Start VectorDB process asynchronously (fire-and-forget)
                        // VectorDB has slow startup, so we start it early
                        // Note: LoggerFactory not disposed - VectorDBLauncher needs it for async logging
#pragma warning disable CA2000 // LoggerFactory intentionally not disposed - needed by async task
                        var vectorDbLoggerFactory = LoggerFactory.Create(b =>
                        {
                            if (enableConsoleOutput) b.AddConsole();
                            b.SetMinimumLevel(LogLevel.Information);
                        });
                        var vectorDbLauncher = new UltrasharpTools.Droid.Services.Hybrid.VectorDBLauncher(
                            vectorDbLoggerFactory.CreateLogger<UltrasharpTools.Droid.Services.Hybrid.VectorDBLauncher>()
                        );
                        vectorDbLauncher.StartAsync();
#pragma warning restore CA2000

                        if (enableConsoleOutput)
                        {
                            Console.WriteLine("[Semantic] VectorDB process starting in background...");
                        }
                    }
                    else
                    {
                        if (enableConsoleOutput)
                        {
                            Console.WriteLine(
                                $"[Semantic] {config.Embedding.Platform.ToUpperInvariant()} service not responding"
                            );
                            Console.WriteLine($"[Semantic] Starting auto-recovery in background...");
                        }

                        // Start background auto-recovery (non-blocking)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await healthCheck.CheckAndStartAsync(
                                    config,
                                    CancellationToken.None
                                );

                                if (result.IsAvailable)
                                {
                                    if (enableConsoleOutput)
                                    {
                                        Console.WriteLine($"[Semantic] ✓ {result.Message}");
                                        Console.WriteLine(
                                            $"[Semantic] Restart MCP server to enable semantic mode"
                                        );
                                    }
                                }
                                else
                                {
                                    if (enableConsoleOutput)
                                    {
                                        Console.WriteLine($"[Semantic] ✗ {result.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (enableConsoleOutput)
                                {
                                    Console.WriteLine($"[Semantic] Auto-recovery failed: {ex.Message}");
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableConsoleOutput)
                {
                    Console.WriteLine($"[Semantic] Failed to load config: {ex.Message}");
                }
            }
        }
        else
        {
            if (enableConsoleOutput)
            {
                Console.WriteLine("[Semantic] No semantic-config.json found, semantic mode disabled");
                Console.WriteLine(
                    "[Semantic] Run Config\\setup-semantic-embedding.cmd to configure semantic search"
                );
            }
        }

        if (!semanticEnabled)
        {
            // CRITICAL: Register dummy SemanticSearchService to prevent "No service of the requested type was found"
            // MCP framework requires all parameters to be resolvable, even if nullable
            // This allows pattern_search to work in entity/content modes without semantic mode configured
            builder.Services.AddSingleton<UltrasharpTools.Tools.Semantic.SemanticSearchService>(
                sp =>
                {
                    // Return a real instance with null dependencies - pattern_search checks for null and falls back
                    return new UltrasharpTools.Tools.Semantic.SemanticSearchService(
                        indexer: null!,
                        solutionManager: null!,
                        config: null,
                        logger: null
                    );
                }
            );
        }

        // Register hybrid mode services if enabled
        if (isHybridMode)
        {
            // Determine project name from solution path or use directory name
            var projectName = !string.IsNullOrEmpty(solutionPath)
                ? Path.GetFileNameWithoutExtension(solutionPath)
                : Path.GetFileName(Directory.GetCurrentDirectory());

            var repositoryPath = !string.IsNullOrEmpty(solutionPath)
                ? Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory()
                : Directory.GetCurrentDirectory();

            var agentConfig = new UltrasharpTools.Droid.Models.Hybrid.AgentConfig
            {
                ProjectName = projectName,
                RepositoryPath = repositoryPath,
                ServerUrl = serverUrl!,
                EmbeddingUrl = embeddingUrl ?? "http://127.0.0.1:11434",
                EmbeddingModel = embeddingModel ?? "nomic-embed-text",
            };

            builder.Services.AddSingleton(agentConfig);

            // Configure HttpClient with optimized connection pooling
            builder
                .Services.AddHttpClient<
                    UltrasharpTools.Droid.Services.Hybrid.IServerBridgeService,
                    UltrasharpTools.Droid.Services.Hybrid.ServerBridgeService
                >()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        MaxConnectionsPerServer = 10,
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                    }
                )
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan); // Prevent handler rotation

            // Embedding service - explicit registration with IHttpClientFactory
            builder.Services.AddHttpClient("EmbeddingService")
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        MaxConnectionsPerServer = 10,
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                    }
                )
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IEmbeddingService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("EmbeddingService");
                var logger = sp.GetRequiredService<ILogger<UltrasharpTools.Droid.Services.Hybrid.EmbeddingService>>();
                var config = sp.GetRequiredService<UltrasharpTools.Droid.Models.Hybrid.AgentConfig>();
                return new UltrasharpTools.Droid.Services.Hybrid.EmbeddingService(client, logger, config);
            });

            // Notification client service
            builder
                .Services.AddHttpClient<
                    UltrasharpTools.Droid.Services.Hybrid.INotificationClientService,
                    UltrasharpTools.Droid.Services.Hybrid.NotificationClientService
                >()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        MaxConnectionsPerServer = 5,
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                    }
                )
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            // Background services для автоматической векторизации
            builder.Services.AddHostedService<UltrasharpTools.Droid.Services.Hybrid.FileWatcherService>();
            builder.Services.AddHostedService<UltrasharpTools.Droid.Services.Hybrid.GitWatcherService>();

            // Request batching service для оптимизации semantic queries
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.RequestBatchingService>(
                sp =>
                {
                    var serverBridge =
                        sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IServerBridgeService>();
                    var logger = sp.GetRequiredService<
                        ILogger<UltrasharpTools.Droid.Services.Hybrid.RequestBatchingService>
                    >();
                    return new UltrasharpTools.Droid.Services.Hybrid.RequestBatchingService(
                        serverBridge,
                        logger,
                        batchWindow: TimeSpan.FromMilliseconds(50),
                        maxBatchSize: 10
                    );
                }
            );

            // ToolRouter для маршрутизации LOCAL/OVERLORD
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IToolRouter>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.ToolRouter>
                >();
                var serverBridge =
                    sp.GetService<UltrasharpTools.Droid.Services.Hybrid.IServerBridgeService>();
                return new UltrasharpTools.Droid.Services.Hybrid.ToolRouter(
                    logger,
                    serverBridge,
                    isHybridMode: true
                );
            });

            // ConfigurationService для загрузки routing config
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.ConfigurationService>();

            // Health check background service
            builder.Services.AddHostedService(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.HealthCheckHostedService>
                >();
                var router =
                    sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IToolRouter>();
                var configService =
                    sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.ConfigurationService>();
                return new UltrasharpTools.Droid.Services.Hybrid.HealthCheckHostedService(
                    logger,
                    router,
                    configService,
                    solutionPath
                );
            });

            // Universal Semantic Mode - Phase 12
            // Загружаем конфигурацию для Semantic Mode (Phase 12.4)
            // LoggerFactory without console output for MCP compatibility
            using var loggerFactory714 = LoggerFactory.Create(b => { /* No console logging */ });
            var semanticConfigLoader =
                new UltrasharpTools.Droid.Services.Hybrid.SemanticModeConfigurationLoader(
                    loggerFactory714
                        .CreateLogger<UltrasharpTools.Droid.Services.Hybrid.SemanticModeConfigurationLoader>()
                );
            var semanticConfig = await semanticConfigLoader.LoadOrCreateAsync(semanticConfigPath);

            // SemanticModeProvider для auto-detection Local/Overlord embedding
            builder.Services.AddSingleton<ISemanticModeProvider>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.SemanticModeProvider>
                >();

                // Debug: try to resolve IEmbeddingService
                UltrasharpTools.Droid.Services.Hybrid.IEmbeddingService? localEmbedding = null;
                try
                {
                    localEmbedding = sp.GetService<UltrasharpTools.Droid.Services.Hybrid.IEmbeddingService>();
                    File.AppendAllText(
                        @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - IEmbeddingService resolved: {localEmbedding != null}\n"
                    );
                }
                catch (Exception ex)
                {
                    File.AppendAllText(
                        @"D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\semantic-debug.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - IEmbeddingService resolution FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n"
                    );
                }

                var serverBridge =
                    sp.GetService<UltrasharpTools.Droid.Services.Hybrid.IServerBridgeService>();
                return new UltrasharpTools.Droid.Services.Hybrid.SemanticModeProvider(
                    logger,
                    localEmbedding,
                    serverBridge,
                    serverUrl,
                    semanticConfig
                );
            });

            // ToolEnricher для semantic enrichment всех инструментов
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IToolEnricher>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.ToolEnricher>
                >();
                var semanticProvider = sp.GetRequiredService<ISemanticModeProvider>();
                return new UltrasharpTools.Droid.Services.Hybrid.ToolEnricher(
                    logger,
                    semanticProvider,
                    semanticConfig
                );
            });

            // McpToolInterceptor для global routing + enrichment
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IMcpToolExecutor>(
                sp =>
                {
                    var logger = sp.GetRequiredService<
                        ILogger<UltrasharpTools.Droid.Services.Hybrid.McpToolInterceptor>
                    >();
                    var router =
                        sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IToolRouter>();
                    var enricher =
                        sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IToolEnricher>();
                    var serverBridge =
                        sp.GetService<UltrasharpTools.Droid.Services.Hybrid.IServerBridgeService>();
                    return new UltrasharpTools.Droid.Services.Hybrid.McpToolInterceptor(
                        logger,
                        router,
                        enricher,
                        serverBridge
                    );
                }
            );

            Console.WriteLine($"Hybrid mode services registered for project: {projectName}");
            Console.WriteLine("Background services enabled:");
            Console.WriteLine(
                "  - FileWatcher: monitoring {0}",
                string.Join(", ", agentConfig.WatchPatterns)
            );
            Console.WriteLine(
                "  - GitWatcher: checking every {0}ms",
                agentConfig.GitCheckIntervalMs
            );
            Console.WriteLine(
                "  - EmbeddingService: {0}",
                agentConfig.AutoVectorizeEnabled ? "enabled" : "disabled"
            );
            Console.WriteLine("  - NotificationClient: SSE real-time notifications");
            Console.WriteLine("  - ToolRouter: automatic routing LOCAL/OVERLORD");
            Console.WriteLine("  - SemanticMode: Universal semantic enrichment for ALL tools");
            Console.WriteLine(
                "  - McpToolInterceptor: Global tool execution with routing + enrichment"
            );
        }
        else
        {
            // Local mode - ToolRouter с fallback на LOCAL
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IToolRouter>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.ToolRouter>
                >();
                return new UltrasharpTools.Droid.Services.Hybrid.ToolRouter(
                    logger,
                    null,
                    isHybridMode: false
                );
            });

            // ConfigurationService всегда доступен
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.ConfigurationService>();

            // AgentConfig для local mode
            var projectName = !string.IsNullOrEmpty(solutionPath)
                ? Path.GetFileNameWithoutExtension(solutionPath)
                : Path.GetFileName(Directory.GetCurrentDirectory());

            var repositoryPath = !string.IsNullOrEmpty(solutionPath)
                ? Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory()
                : Directory.GetCurrentDirectory();

            var agentConfig = new UltrasharpTools.Droid.Models.Hybrid.AgentConfig
            {
                ProjectName = projectName,
                RepositoryPath = repositoryPath,
                ServerUrl = serverUrl ?? "",
                EmbeddingUrl = embeddingUrl ?? "http://127.0.0.1:11434",
                EmbeddingModel = embeddingModel ?? "nomic-embed-text",
            };
            builder.Services.AddSingleton(agentConfig);

            // EmbeddingService для local mode (нужен для SemanticModeProvider)
            builder.Services.AddHttpClient("EmbeddingService")
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        MaxConnectionsPerServer = 10,
                        EnableMultipleHttp2Connections = true,
                        ConnectTimeout = TimeSpan.FromSeconds(10),
                    }
                )
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IEmbeddingService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("EmbeddingService");
                var logger = sp.GetRequiredService<ILogger<UltrasharpTools.Droid.Services.Hybrid.EmbeddingService>>();
                var config = sp.GetRequiredService<UltrasharpTools.Droid.Models.Hybrid.AgentConfig>();
                return new UltrasharpTools.Droid.Services.Hybrid.EmbeddingService(client, logger, config);
            });

            // Universal Semantic Mode - Phase 12 (local mode)
            // Загружаем конфигурацию для Semantic Mode (Phase 12.4)
            // LoggerFactory without console output for MCP compatibility
            using var loggerFactory818 = LoggerFactory.Create(b => { /* No console logging */ });
            var semanticConfigLoader =
                new UltrasharpTools.Droid.Services.Hybrid.SemanticModeConfigurationLoader(
                    loggerFactory818
                        .CreateLogger<UltrasharpTools.Droid.Services.Hybrid.SemanticModeConfigurationLoader>()
                );
            var semanticConfig = await semanticConfigLoader.LoadOrCreateAsync();

            // SemanticModeProvider (только локальный embedding если доступен)
            builder.Services.AddSingleton<ISemanticModeProvider>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.SemanticModeProvider>
                >();

                // Используем IEmbeddingService (зарегистрирован выше через HttpClient)
                var localEmbedding = sp.GetService<UltrasharpTools.Droid.Services.Hybrid.IEmbeddingService>();

                // В local mode нет serverBridge и Overlord
                return new UltrasharpTools.Droid.Services.Hybrid.SemanticModeProvider(
                    logger,
                    localEmbedding,
                    null,
                    null,
                    semanticConfig
                );
            });

            // ToolEnricher для semantic enrichment
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IToolEnricher>(sp =>
            {
                var logger = sp.GetRequiredService<
                    ILogger<UltrasharpTools.Droid.Services.Hybrid.ToolEnricher>
                >();
                var semanticProvider = sp.GetRequiredService<ISemanticModeProvider>();
                return new UltrasharpTools.Droid.Services.Hybrid.ToolEnricher(
                    logger,
                    semanticProvider,
                    semanticConfig
                );
            });

            // McpToolInterceptor (только локальное выполнение)
            builder.Services.AddSingleton<UltrasharpTools.Droid.Services.Hybrid.IMcpToolExecutor>(
                sp =>
                {
                    var logger = sp.GetRequiredService<
                        ILogger<UltrasharpTools.Droid.Services.Hybrid.McpToolInterceptor>
                    >();
                    var router =
                        sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IToolRouter>();
                    var enricher =
                        sp.GetRequiredService<UltrasharpTools.Droid.Services.Hybrid.IToolEnricher>();
                    return new UltrasharpTools.Droid.Services.Hybrid.McpToolInterceptor(
                        logger,
                        router,
                        enricher,
                        null
                    );
                }
            );

            if (enableConsoleOutput)
            {
                Console.WriteLine(
                    "Local mode - Universal Semantic Mode available if local embedding configured"
                );
            }
        }

        // Check semantic mode availability for MCP Initialize capabilities
        if (enableConsoleOutput)
        {
            Console.WriteLine("Checking semantic mode availability...");
        }
        var semanticAvailability =
            await UltrasharpTools.Droid.Services.Hybrid.SemanticModeBootstrapCheck.CheckAvailabilityAsync(
                embeddingUrl,
                serverUrl,
                timeoutMs: 3000
            );

        if (enableConsoleOutput)
        {
            if (semanticAvailability.IsAvailable)
            {
                Console.WriteLine($"Semantic mode: AVAILABLE ({semanticAvailability.Source})");
            }
            else
            {
                Console.WriteLine("Semantic mode: NOT AVAILABLE");
            }
        }

        builder
            .Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = ApplicationName,
                    Version = ApplicationVersion,
                };

                // Note: Semantic mode capabilities are available via get_capabilities tool
                // Experimental capabilities cannot be set here due to source-generated JSON serializer limitations
            })
            .WithStdioServerTransport()
            .WithUltrasharpTools();

        try
        {
            // Console.WriteLine($"Starting {ApplicationName} v{ApplicationVersion}");
            var host = builder.Build();
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(ApplicationName);

            // Start background solution loading through LoadingOrchestrator
            // Orchestrator prevents duplicate loading if MCP load_solution is called
            if (!string.IsNullOrEmpty(solutionPath))
            {
                var loadingOrchestrator =
                    host.Services.GetRequiredService<ILoadingOrchestrator>();

                logger.LogInformation(
                    "Starting background solution loading: {SolutionPath}",
                    solutionPath
                );

                // Fire and forget - loading happens in background
                // If MCP load_solution is called, it will attach to this operation
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await loadingOrchestrator.RequestLoadingAsync(
                            solutionPath,
                            LoadingSource.BackgroundStartup,
                            CancellationToken.None
                        );

                        if (result.Success)
                        {
                            logger.LogInformation(
                                "Background loading completed: {SolutionPath} with {ProjectCount} projects",
                                result.SolutionPath,
                                result.ProjectCount
                            );
                        }
                        else
                        {
                            logger.LogError(
                                "Background loading failed: {SolutionPath} - {ErrorMessage}",
                                result.SolutionPath,
                                result.ErrorMessage
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background loading threw exception: {SolutionPath}", solutionPath);
                    }
                });

                logger.LogInformation(
                    "Background loading started. MCP server ready to accept requests."
                );
            }
            else
            {
                logger.LogInformation("MCP server ready to accept requests");
            }

            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ApplicationName} terminated unexpectedly: {ex}");
            return 1;
        }
        finally
        {
            // Console.WriteLine($"{ApplicationName} shutting down.");
        }
    }
}

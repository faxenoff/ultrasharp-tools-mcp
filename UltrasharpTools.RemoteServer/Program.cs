using UltrasharpTools.Tools.Services;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Infrastructure;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.AspNetCore.HttpLogging;
using ModelContextProtocol.Protocol;
using System.Reflection;
using Microsoft.Extensions.Logging;
namespace UltrasharpTools.SseServer;

public class Program {
    // --- Application ---
    public const string ApplicationName = "UltrasharpToolsMcpSseServer";
    public const string ApplicationVersion = "1.0.0";
    public static async Task<int> Main(string[] args) {
        // Ensure tool assemblies are loaded for MCP SDK's WithToolsFromAssembly
        _ = typeof(SolutionTools);
        _ = typeof(AnalysisTools);
        _ = typeof(ModificationTools);

        var portOption = new Option<int>("--port") {
            Description = "The port number for the MCP server to listen on.",
            DefaultValueFactory = _ => 3001
        };

        var logFileOption = new Option<string?>("--log-file") {
            Description = "Optional path to a log file. If not specified, uses .ultrasharp/logs/{ApplicationName}-.log in project root."
        };

        var logLevelOption = new Option<LogLevel>("--log-level") {
            Description = "Minimum log level for console and file.",
            DefaultValueFactory = _ => LogLevel.Information
        };

        var loadSolutionOption = new Option<string?>("--load-solution") {
            Description = "Path to a solution file (.sln) to load immediately on startup."
        };

        var buildConfigurationOption = new Option<string?>("--build-configuration") {
            Description = "Build configuration to use when loading the solution (Debug, Release, etc.)."
        };

        var disableGitOption = new Option<bool>("--disable-git") {
            Description = "Disable Git integration.",
            DefaultValueFactory = _ => false
        };

        var symbolCacheEnabledOption = new Option<bool>("--symbol-cache") {
            Description = "Enable persistent symbol cache for 10x faster solution initialization (33s → 3-5s).",
            DefaultValueFactory = _ => true
        };

        var symbolCacheClearOption = new Option<bool>("--symbol-cache-clear") {
            Description = "Clear all symbol cache data on startup.",
            DefaultValueFactory = _ => false
        };

        var symbolCacheDirectoryOption = new Option<string?>("--symbol-cache-directory") {
            Description = "Custom directory for symbol cache (default: %TEMP%/UltrasharpTools/SymbolCache)."
        };

        var rootCommand = new RootCommand("UltrasharpTools MCP Server") {
        portOption,
        logFileOption,
        logLevelOption,
        loadSolutionOption,
        buildConfigurationOption,
        disableGitOption,
        symbolCacheEnabledOption,
        symbolCacheClearOption,
        symbolCacheDirectoryOption
    };

        // Parse arguments first to get values
        var parseResult = rootCommand.Parse(args);

        int port = parseResult.GetValue(portOption);
        string? logFilePath = parseResult.GetValue(logFileOption);
        LogLevel minimumLogLevel = parseResult.GetValue(logLevelOption);
        string? solutionPath = parseResult.GetValue(loadSolutionOption);
        string? buildConfiguration = parseResult.GetValue(buildConfigurationOption);
        bool disableGit = parseResult.GetValue(disableGitOption);
        bool symbolCacheEnabled = parseResult.GetValue(symbolCacheEnabledOption);
        bool symbolCacheClear = parseResult.GetValue(symbolCacheClearOption);
        string? symbolCacheDirectory = parseResult.GetValue(symbolCacheDirectoryOption);
        string serverUrl = $"http://localhost:{port}";

        // Use project-local logs directory if not specified
        if (string.IsNullOrWhiteSpace(logFilePath)) {
            var logsDir = ProjectPathHelper.GetLogsPath(solutionPath);
            logFilePath = Path.Combine(logsDir, $"{ApplicationName}-.log");
        }

        // Create log directory if it doesn't exist
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(logDirectory) && !Directory.Exists(logDirectory)) {
            Directory.CreateDirectory(logDirectory);
        }

        Console.WriteLine($"Logging to file: {Path.GetFullPath(logFilePath)} with minimum level {minimumLogLevel}");

        // Early startup information (before DI/logging is configured)
        if (disableGit) {
            Console.WriteLine("Git integration is disabled.");
        }

        if (!string.IsNullOrEmpty(buildConfiguration)) {
            Console.WriteLine($"Using build configuration: {buildConfiguration}");
        }

        if (symbolCacheEnabled) {
            Console.WriteLine("Symbol cache is enabled (10x faster solution initialization)");
            if (!string.IsNullOrEmpty(symbolCacheDirectory)) {
                Console.WriteLine($"Symbol cache directory: {symbolCacheDirectory}");
            }
            if (symbolCacheClear) {
                Console.WriteLine("Symbol cache will be cleared on startup");
            }
        } else {
            Console.WriteLine("Symbol cache is disabled");
        }

        try {
            Console.WriteLine($"Configuring {ApplicationName} v{ApplicationVersion} to run on {serverUrl} with minimum log level {minimumLogLevel}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(minimumLogLevel);

            // Configure logging overrides
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Information);
            builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft.CodeAnalysis", LogLevel.Information);
            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

            // Add file logging
            builder.Logging.AddFile(logFilePath, minimumLogLevel);

            // Add W3CLogging for detailed HTTP request logging
            builder.Services.AddW3CLogging(logging => {
                logging.LoggingFields = W3CLoggingFields.All; // Log all available fields
                logging.FileSizeLimit = 5 * 1024 * 1024; // 5 MB
                logging.RetainedFileCountLimit = 2;
                logging.FileName = "access-"; // Prefix for log files
                                              // By default, logs to a 'logs' subdirectory of the app's content root.
                                              // Can be configured: logging.RootPath = ...
            });

            // Create SymbolCacheOptions from command line arguments
            var symbolCacheOptions = new UltrasharpTools.Tools.Models.SymbolCacheOptions
            {
                Enabled = symbolCacheEnabled,
                ClearOnStartup = symbolCacheClear,
                CacheDirectory = symbolCacheDirectory
            };

            builder.Services.WithUltrasharpToolsServices(!disableGit, buildConfiguration, null, null, symbolCacheOptions);

            builder.Services
                .AddMcpServer(options => {
                    options.ServerInfo = new Implementation {
                        Name = ApplicationName,
                        Version = ApplicationVersion,
                    };
                    // For debugging, you can hook into handlers here if needed,
                    // but ModelContextProtocol's own Debug logging should be sufficient.
                })
                .WithHttpTransport()
                .WithUltrasharpTools();

            var app = builder.Build();
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(ApplicationName);

            // Load solution if specified in command line arguments
            if (!string.IsNullOrEmpty(solutionPath)) {
                try {
                    var solutionManager = app.Services.GetRequiredService<ISolutionManager>();
                    var editorConfigProvider = app.Services.GetRequiredService<IEditorConfigProvider>();

                    logger.LogInformation("Loading solution: {SolutionPath}", solutionPath);
                    await solutionManager.LoadSolutionAsync(solutionPath, CancellationToken.None);

                    var solutionDir = Path.GetDirectoryName(solutionPath);
                    if (!string.IsNullOrEmpty(solutionDir)) {
                        await editorConfigProvider.InitializeAsync(solutionDir, CancellationToken.None);
                        logger.LogInformation("Solution loaded successfully: {SolutionPath}", solutionPath);
                    } else {
                        logger.LogWarning("Could not determine directory for solution path: {SolutionPath}", solutionPath);
                    }
                } catch (Exception ex) {
                    logger.LogError(ex, "Error loading solution: {SolutionPath}", solutionPath);
                }
            }

            // --- ASP.NET Core Middleware ---

            // 1. W3C Logging Middleware (if enabled and configured to log to a file separate from standard logging)
            //    If W3CLogging is configured to write to files, it has its own middleware.
            // app.UseW3CLogging(); // This is needed if W3CLogging is writing its own files.

            // 2. Custom Request Logging Middleware (very early in the pipeline)
            app.Use(async (context, next) => {
                var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
                var requestLogger = loggerFactory.CreateLogger(ApplicationName);
                requestLogger.LogDebug("Incoming Request: {Method} {Path} {QueryString} from {RemoteIpAddress}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString,
                    context.Connection.RemoteIpAddress);

                // Log headers for more detail if needed (can be verbose)
                // foreach (var header in context.Request.Headers) {
                //     logger.LogTrace("Header: {Key}: {Value}", header.Key, header.Value);
                // }
                try {
                    await next(context);
                } catch (Exception ex) {
                    requestLogger.LogError(ex, "Error processing request: {Method} {Path}", context.Request.Method, context.Request.Path);
                    throw; // Re-throw to let ASP.NET Core handle it
                }

                requestLogger.LogDebug("Outgoing Response: {StatusCode} for {Method} {Path}",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path);
            });


            // 3. Standard ASP.NET Core middleware (HTTPS redirection, routing, auth, etc. - not used here yet)
            // if (app.Environment.IsDevelopment()) { }
            // app.UseHttpsRedirection(); 

            // 4. MCP Middleware
            app.MapMcp(); // Maps the MCP endpoint (typically "/mcp")

            logger.LogInformation("Starting {AppName} server...", ApplicationName);
            await app.RunAsync(serverUrl);

            return 0;

        } catch (Exception ex) {
            Console.Error.WriteLine($"{ApplicationName} terminated unexpectedly: {ex}");
            return 1;
        } finally {
            Console.WriteLine($"{ApplicationName} shutting down.");
        }
    }
}
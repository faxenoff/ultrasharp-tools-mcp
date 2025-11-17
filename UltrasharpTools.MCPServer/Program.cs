using UltrasharpTools.Tools.Services;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Infrastructure;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace UltrasharpTools.StdioServer;

public static class Program {
    public const string ApplicationName = "UltrasharpToolsMcpStdioServer";
    public const string ApplicationVersion = "1.0.0";
    public static async Task<int> Main(string[] args) {
        _ = typeof(SolutionTools);
        _ = typeof(AnalysisTools);
        _ = typeof(ModificationTools);

        var logDirOption = new Option<string?>("--log-directory") {
            Description = "Optional path to a log directory. If not specified, uses .ultrasharp/logs in project root."
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

        var gitBranchRetentionCountOption = new Option<int?>("--git-branch-retention-count") {
            Description = "Keep only the N most recent sharptools/* branches. (null = no limit)",
            DefaultValueFactory = _ => 10
        };

        var gitBranchRetentionDaysOption = new Option<int?>("--git-branch-retention-days") {
            Description = "Keep sharptools/* branches created within the last N days. (null = no limit)",
            DefaultValueFactory = _ => null
        };

        var gitAutoCleanupOption = new Option<bool>("--git-auto-cleanup") {
            Description = "Automatically cleanup old branches after each modification.",
            DefaultValueFactory = _ => true
        };

        var autoReloadOption = new Option<bool>("--auto-reload") {
            Description = "Enable automatic solution reload when .csproj or .sln files change.",
            DefaultValueFactory = _ => false
        };

        var reloadDebounceOption = new Option<int>("--reload-debounce-ms") {
            Description = "Debounce delay in milliseconds before triggering auto-reload.",
            DefaultValueFactory = _ => 2000
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

        var rootCommand = new RootCommand("UltrasharpTools MCP StdIO Server")
        {
        logDirOption,
        logLevelOption,
        loadSolutionOption,
        buildConfigurationOption,
        disableGitOption,
        gitBranchRetentionCountOption,
        gitBranchRetentionDaysOption,
        gitAutoCleanupOption,
        autoReloadOption,
        reloadDebounceOption,
        symbolCacheEnabledOption,
        symbolCacheClearOption,
        symbolCacheDirectoryOption
    };

        // Parse arguments first to get values
        var parseResult = rootCommand.Parse(args);

        string? logDirPath = parseResult.GetValue(logDirOption);
        LogLevel minimumLogLevel = parseResult.GetValue(logLevelOption);
        string? solutionPath = parseResult.GetValue(loadSolutionOption);
        string? buildConfiguration = parseResult.GetValue(buildConfigurationOption);
        bool disableGit = parseResult.GetValue(disableGitOption);
        int? gitBranchRetentionCount = parseResult.GetValue(gitBranchRetentionCountOption);
        int? gitBranchRetentionDays = parseResult.GetValue(gitBranchRetentionDaysOption);
        bool gitAutoCleanup = parseResult.GetValue(gitAutoCleanupOption);
        bool autoReload = parseResult.GetValue(autoReloadOption);
        int reloadDebounceMs = parseResult.GetValue(reloadDebounceOption);
        bool symbolCacheEnabled = parseResult.GetValue(symbolCacheEnabledOption);
        bool symbolCacheClear = parseResult.GetValue(symbolCacheClearOption);
        string? symbolCacheDirectory = parseResult.GetValue(symbolCacheDirectoryOption);

        // Use project-local logs directory if not specified
        if (string.IsNullOrWhiteSpace(logDirPath)) {
            logDirPath = ProjectPathHelper.GetLogsPath(solutionPath);
        }

        // Create log directory if it doesn't exist
        if (!Directory.Exists(logDirPath)) {
            try {
                Directory.CreateDirectory(logDirPath);
            } catch (Exception ex) {
                Console.Error.WriteLine($"Failed to create log directory: {ex.Message}");
                return 1;
            }
        }

        string logFilePath = Path.Combine(logDirPath, $"{ApplicationName}-.log");
        Console.Error.WriteLine($"Logging to directory: {Path.GetFullPath(logDirPath)} with minimum level {minimumLogLevel}");

        // Early startup information (before DI/logging is configured)
        if (disableGit) {
            Console.WriteLine("Git integration is disabled.");
        }

        if (!string.IsNullOrEmpty(buildConfiguration)) {
            Console.WriteLine($"Using build configuration: {buildConfiguration}");
        }

        if (autoReload) {
            Console.WriteLine($"Auto-reload is enabled with {reloadDebounceMs}ms debounce");
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

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(minimumLogLevel);

        // Configure logging overrides
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.CodeAnalysis", LogLevel.Information);
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

        // Add file logging
        builder.Logging.AddFile(logFilePath, minimumLogLevel);

        // Create GitOptions from command line arguments
        var gitOptions = new UltrasharpTools.Tools.Models.GitOptions
        {
            RetentionCount = gitBranchRetentionCount,
            RetentionDays = gitBranchRetentionDays,
            AutoCleanup = gitAutoCleanup && !disableGit  // Only enable if Git is enabled
        };

        // Create SolutionReloadOptions from command line arguments
        var reloadOptions = new UltrasharpTools.Tools.Models.SolutionReloadOptions
        {
            AutoReloadEnabled = autoReload,
            DebounceDelayMs = reloadDebounceMs
        };

        // Create SymbolCacheOptions from command line arguments
        var symbolCacheOptions = new UltrasharpTools.Tools.Models.SymbolCacheOptions
        {
            Enabled = symbolCacheEnabled,
            ClearOnStartup = symbolCacheClear,
            CacheDirectory = symbolCacheDirectory
        };

        builder.Services.WithUltrasharpToolsServices(!disableGit, buildConfiguration, gitOptions, reloadOptions, symbolCacheOptions);

        builder.Services
            .AddMcpServer(options => {
                options.ServerInfo = new Implementation {
                    Name = ApplicationName,
                    Version = ApplicationVersion,
                };
            })
            .WithStdioServerTransport()
            .WithUltrasharpTools();

        try {
            Console.WriteLine($"Starting {ApplicationName} v{ApplicationVersion}");
            var host = builder.Build();
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(ApplicationName);

            if (!string.IsNullOrEmpty(solutionPath)) {
                try {
                    var solutionManager = host.Services.GetRequiredService<ISolutionManager>();
                    var editorConfigProvider = host.Services.GetRequiredService<IEditorConfigProvider>();

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

            await host.RunAsync();
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine($"{ApplicationName} terminated unexpectedly: {ex}");
            return 1;
        } finally {
            Console.WriteLine($"{ApplicationName} shutting down.");
        }
    }
}


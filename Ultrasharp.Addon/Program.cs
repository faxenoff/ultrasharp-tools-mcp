using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Ultrasharp.Addon.Ipc;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Logging;

namespace Ultrasharp.Addon;

public static class Program
{
    public const string ApplicationName = "Ultrasharp.Addon";
    public const string ApplicationVersion = "3.7.2";

    private const int ParentCheckIntervalMs = 3000;

    public static async Task<int> Main(string[] args)
    {
        var pipeOption = new Option<string>("--pipe")
        {
            Description = "Named Pipe name for IPC communication with ultrascript-tools-mcp.",
            Required = true,
        };

        var parentPidOption = new Option<int?>("--parent-pid")
        {
            Description = "Parent process PID for orphan detection. Addon shuts down when parent dies.",
        };

        var logDirectoryOption = new Option<string?>("--log-directory")
        {
            Description = "Directory for file-based logging. If not specified, logs to stderr.",
        };

        var slnOption = new Option<string?>("--sln")
        {
            Description = "Path to .sln file for eager loading on startup (Phase 2).",
        };

        var logLevelOption = new Option<LogLevel>("--log-level")
        {
            Description = "Minimum log level.",
            DefaultValueFactory = _ => LogLevel.Information,
        };

        var rootCommand = new RootCommand("Ultrasharp.Addon — Roslyn service for ultrascript-tools-mcp")
        {
            pipeOption,
            parentPidOption,
            logDirectoryOption,
            slnOption,
            logLevelOption,
        };

        var parseResult = rootCommand.Parse(args);

        string pipeName = parseResult.GetValue(pipeOption) ?? "UltraScript_Roslyn_default";
        int? parentPid = parseResult.GetValue(parentPidOption);
        string? logDirectory = parseResult.GetValue(logDirectoryOption);
        string? slnPath = parseResult.GetValue(slnOption);
        LogLevel logLevel = parseResult.GetValue(logLevelOption);

        // Build host with DI
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            DisableDefaults = true,
        });

        // Set content root to exe directory
        var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exeDirectory))
        {
            builder.Environment.ContentRootPath = exeDirectory;
        }

        // Configure logging — file only (stdout/stderr reserved for IPC diagnostics)
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(logLevel);

        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, $"{ApplicationName}-{{Date:yyyyMMdd}}.log");
            builder.Logging.AddFile(logFilePath, logLevel);
        }
        else
        {
            // Fallback: log to stderr via console (not stdout — that's for IPC)
            builder.Logging.AddConsole(opts =>
            {
                opts.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        }

        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.CodeAnalysis", LogLevel.Warning);

        // Register Roslyn services (subset — no MCP, no SQLite, no VectorDB)
        AddonServiceRegistration.RegisterServices(builder.Services);

        // Register IPC infrastructure
        builder.Services.AddSingleton(new PipeServerOptions { PipeName = pipeName });
        builder.Services.AddSingleton<RequestRouter>();
        builder.Services.AddSingleton<PipeServer>();
        builder.Services.AddSingleton<AddonLifecycleService>();

        // Register handlers
        Handlers.HandlerRegistration.RegisterHandlers(builder.Services);

        var host = builder.Build();

        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(ApplicationName);

        logger.LogInformation("[Addon] Starting {Name} v{Version}", ApplicationName, ApplicationVersion);
        logger.LogInformation("[Addon] Pipe: {PipeName}, ParentPID: {ParentPid}, Sln: {SlnPath}",
            pipeName, parentPid?.ToString() ?? "none", slnPath ?? "none");

        using var cts = new CancellationTokenSource();

        // Monitor parent process
        if (parentPid.HasValue)
        {
            _ = MonitorParentProcessAsync(parentPid.Value, cts, logger);
            logger.LogInformation("[Addon] Monitoring parent process PID: {Pid}", parentPid.Value);
        }

        // Handle SIGTERM / Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("[Addon] Ctrl+C received, shutting down...");
            cts.Cancel();
        };

        try
        {
            // Phase 1: Start pipe server (immediate)
            var lifecycle = host.Services.GetRequiredService<AddonLifecycleService>();
            await lifecycle.StartAsync(slnPath, cts.Token);

            // Run pipe server loop
            var pipeServer = host.Services.GetRequiredService<PipeServer>();
            await pipeServer.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[Addon] Shutdown requested.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Addon] Fatal error");
            return 1;
        }
        finally
        {
            logger.LogInformation("[Addon] Exiting.");
        }

        return 0;
    }

    private static async Task MonitorParentProcessAsync(int parentPid, CancellationTokenSource cts, ILogger logger)
    {
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(ParentCheckIntervalMs, cts.Token);

                if (!IsProcessRunning(parentPid))
                {
                    logger.LogWarning("[Addon] Parent process (PID: {Pid}) has exited. Initiating shutdown.", parentPid);
                    await cts.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Addon] Error monitoring parent process.");
        }
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Refresh();
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}

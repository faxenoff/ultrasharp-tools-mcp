using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Launches VectorDB process asynchronously when semantic mode is enabled.
/// VectorDB is started in the background to reduce perceived latency.
/// </summary>
public sealed partial class VectorDBLauncher {
    private const string VectorDBProcessName = "UltraSharpTools.VectorDB";

    private readonly ILogger<VectorDBLauncher> _logger;
    private Process? _vectorDbProcess;

    public VectorDBLauncher(ILogger<VectorDBLauncher> logger) {
        _logger = logger;
    }

    /// <summary>
    /// Start VectorDB process if not already running.
    /// This is fire-and-forget - does not block.
    /// </summary>
    public void StartAsync() {
        _ = Task.Run(async () => {
            try {
                await StartInternalAsync();
            } catch (Exception ex) {
                LogStartException(ex.Message, ex);
            }
        });
    }
    private async Task StartInternalAsync() {
        // Debug log file for MCP mode (no console)
        var logsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UltraSharpTools", "logs"
        );
        Directory.CreateDirectory(logsDir);

        var launcherLogPath = Path.Combine(logsDir, "vectordb-launcher.log");
        var vectorDbLogPath = Path.Combine(logsDir, "vectordb-output.log");

        void DebugLog(string msg) => File.AppendAllText(launcherLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\n");
        void VectorDbLog(string msg) => File.AppendAllText(vectorDbLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\n");

        DebugLog("StartInternalAsync called");

        // Check if already running
        if (IsVectorDBRunning()) {
            LogAlreadyRunning();
            DebugLog("VectorDB is already running");
            return;
        }

        // Find VectorDB executable
        var vectorDbPath = FindVectorDBExecutable();
        DebugLog($"FindVectorDBExecutable returned: {vectorDbPath ?? "null"}");

        if (vectorDbPath == null) {
            LogExecutableNotFound();
            DebugLog("VectorDB executable not found");
            return;
        }

        LogStartingVectorDB(vectorDbPath);
        DebugLog($"Starting VectorDB from: {vectorDbPath}");

        // Start process with parent PID for orphan detection
        var currentPid = Environment.ProcessId;
        DebugLog($"Current Droid PID: {currentPid}");

        var startInfo = new ProcessStartInfo {
            FileName = vectorDbPath,
            Arguments = $"--parent-pid {currentPid}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(vectorDbPath),
        };

        try {
            _vectorDbProcess = Process.Start(startInfo);
        } catch (Exception ex) {
            DebugLog($"Process.Start exception: {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        if (_vectorDbProcess == null) {
            LogStartFailed();
            DebugLog("Process.Start returned null");
            return;
        }

        // Capture stdout/stderr for debugging
        _vectorDbProcess.OutputDataReceived += (_, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                VectorDbLog($"[OUT] {e.Data}");
        };
        _vectorDbProcess.ErrorDataReceived += (_, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                VectorDbLog($"[ERR] {e.Data}");
        };
        _vectorDbProcess.EnableRaisingEvents = true;
        _vectorDbProcess.Exited += (_, _) => {
            var exitCode = -1;
            try { exitCode = _vectorDbProcess.ExitCode; } catch { }
            DebugLog($"VectorDB process exited with code: {exitCode}");
            VectorDbLog($"[EXIT] Process exited with code: {exitCode}");
        };

        _vectorDbProcess.BeginOutputReadLine();
        _vectorDbProcess.BeginErrorReadLine();

        LogStartedWithPid(_vectorDbProcess.Id);
        DebugLog($"VectorDB started with PID: {_vectorDbProcess.Id}");

        // Wait for process to stabilize (don't connect to pipe - that consumes the slot!)
        DebugLog("Waiting for process to stabilize...");
        var ready = await WaitForProcessReadyAsync(_vectorDbProcess, TimeSpan.FromSeconds(10));

        if (ready) {
            LogPipeReady();
            DebugLog("VectorDB is ready (pipe available)");
        } else {
            LogPipeTimeout();
            DebugLog("VectorDB started but pipe not available within timeout");

            // Log early exit info
            if (_vectorDbProcess.HasExited) {
                DebugLog($"VectorDB exited early with code: {_vectorDbProcess.ExitCode}");
            }
        }
    }
    private static bool IsVectorDBRunning() {
        var processes = Process.GetProcessesByName(VectorDBProcessName);
        return processes.Length > 0;
    }

    private string? FindVectorDBExecutable() {
        // Try AppContext.BaseDirectory (works with single-file/AOT)
        var baseDir = AppContext.BaseDirectory;
        LogSearchBaseDir(baseDir);

        var baseDirPath = Path.Combine(baseDir, $"{VectorDBProcessName}.exe");
        if (File.Exists(baseDirPath)) {
            LogFoundAt(baseDirPath);
            return baseDirPath;
        }

        // Try Assembly location (fallback)
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation)) {
            var droidDir = Path.GetDirectoryName(assemblyLocation);
            if (droidDir != null) {
                var sameDirPath = Path.Combine(droidDir, $"{VectorDBProcessName}.exe");
                if (File.Exists(sameDirPath)) {
                    LogFoundAt(sameDirPath);
                    return sameDirPath;
                }
            }
        }

        // Try Run.Publish/Droid (for development)
        var currentDir = Directory.GetCurrentDirectory();
        LogCurrentDir(currentDir);

        var publishPath = Path.Combine(currentDir, "Run.Publish", "Droid", $"{VectorDBProcessName}.exe");
        if (File.Exists(publishPath)) {
            LogFoundAt(publishPath);
            return publishPath;
        }

        // Try bin/Debug (for development)
        var debugPath = Path.Combine(currentDir, "UltraSharpTools.VectorDB", "bin", "Debug", "net10.0", $"{VectorDBProcessName}.exe");
        if (File.Exists(debugPath)) {
            LogFoundAt(debugPath);
            return debugPath;
        }

        LogNotFoundAnywhere(baseDir, currentDir);
        return null;
    }

    private async Task<bool> WaitForProcessReadyAsync(Process process, TimeSpan timeout) {
        // Don't connect to pipe - that would consume VectorDB's single connection slot!
        // Just wait for process to stabilize (not exit immediately)
        var deadline = DateTime.UtcNow + timeout;
        var stableTime = TimeSpan.FromSeconds(3);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline) {
            if (process.HasExited) {
                return false; // Process crashed
            }

            // If process has been running for stableTime, consider it ready
            if (DateTime.UtcNow - startTime >= stableTime) {
                return true;
            }

            await Task.Delay(500);
        }

        return !process.HasExited;
    }

    /// <summary>
    /// Stop VectorDB process if we started it.
    /// </summary>
    public void Stop() {
        if (_vectorDbProcess is { HasExited: false }) {
            try {
                _vectorDbProcess.Kill(entireProcessTree: true);
                LogProcessStopped();
            } catch (Exception ex) {
                LogStopFailed(ex);
            }
        }
    }
}

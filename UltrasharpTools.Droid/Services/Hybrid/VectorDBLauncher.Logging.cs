using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class VectorDBLauncher
{
    // VectorDB Launcher events (5400-5419)

    [LoggerMessage(EventId = 5400, Level = LogLevel.Debug,
        Message = "VectorDB is already running")]
    private partial void LogAlreadyRunning();

    [LoggerMessage(EventId = 5401, Level = LogLevel.Warning,
        Message = "VectorDB executable not found")]
    private partial void LogExecutableNotFound();

    [LoggerMessage(EventId = 5402, Level = LogLevel.Information,
        Message = "Starting VectorDB from: {Path}")]
    private partial void LogStartingVectorDB(string path);

    [LoggerMessage(EventId = 5403, Level = LogLevel.Error,
        Message = "Failed to start VectorDB process")]
    private partial void LogStartFailed();

    [LoggerMessage(EventId = 5404, Level = LogLevel.Information,
        Message = "VectorDB started with PID: {PID}")]
    private partial void LogStartedWithPid(int pid);

    [LoggerMessage(EventId = 5405, Level = LogLevel.Information,
        Message = "VectorDB is ready (process stable)")]
    private partial void LogPipeReady();

    [LoggerMessage(EventId = 5406, Level = LogLevel.Warning,
        Message = "VectorDB process exited or not stable within timeout")]
    private partial void LogPipeTimeout();

    [LoggerMessage(EventId = 5407, Level = LogLevel.Warning,
        Message = "Failed to start VectorDB process: {Message}")]
    private partial void LogStartException(string message, Exception ex);

    [LoggerMessage(EventId = 5408, Level = LogLevel.Information,
        Message = "VectorDB process stopped")]
    private partial void LogProcessStopped();

    [LoggerMessage(EventId = 5409, Level = LogLevel.Warning,
        Message = "Failed to stop VectorDB process")]
    private partial void LogStopFailed(Exception ex);

    [LoggerMessage(EventId = 5410, Level = LogLevel.Debug,
        Message = "Searching VectorDB, BaseDirectory: {BaseDir}")]
    private partial void LogSearchBaseDir(string baseDir);

    [LoggerMessage(EventId = 5411, Level = LogLevel.Debug,
        Message = "Found VectorDB at: {Path}")]
    private partial void LogFoundAt(string path);

    [LoggerMessage(EventId = 5412, Level = LogLevel.Debug,
        Message = "CurrentDirectory: {CurrentDir}")]
    private partial void LogCurrentDir(string currentDir);

    [LoggerMessage(EventId = 5413, Level = LogLevel.Warning,
        Message = "VectorDB not found. Searched BaseDir: {BaseDir}, CurrentDir: {CurrentDir}")]
    private partial void LogNotFoundAnywhere(string baseDir, string currentDir);
}

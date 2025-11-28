namespace UltrasharpTools.Tools.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Orchestrates solution loading to prevent duplicate loads and allow multiple subscribers.
/// This is a step towards separating the Indexer into a separate process - it manages
/// loading state and ensures only one loading operation runs at a time.
/// </summary>
public interface ILoadingOrchestrator {
    /// <summary>
    /// Requests solution loading. If already loading, returns the existing task.
    /// If not loading, starts a new loading operation.
    /// </summary>
    /// <param name="solutionPath">Path to solution file</param>
    /// <param name="source">Source of the loading request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the loading operation</returns>
    Task<LoadingResult> RequestLoadingAsync(string solutionPath, LoadingSource source, CancellationToken cancellationToken);

    /// <summary>
    /// Gets current loading status
    /// </summary>
    LoadingStatus GetStatus();

    /// <summary>
    /// Waits for any in-progress loading to complete.
    /// Returns immediately if no loading is in progress.
    /// </summary>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if ready (loaded or no loading in progress), false if timeout</returns>
    Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
public record LoadingResult {
    public bool Success { get; init; }
    public string? SolutionPath { get; init; }
    public int ProjectCount { get; init; }
    public string? ErrorMessage { get; init; }
    public LoadingSource Source { get; init; }

    /// <summary>
    /// Предупреждения при загрузке (битые референсы, проблемы с проектами и т.д.)
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static LoadingResult CreateSuccess(
        string solutionPath,
        int projectCount,
        LoadingSource source,
        IReadOnlyList<string>? warnings = null) {
        return new LoadingResult {
            Success = true,
            SolutionPath = solutionPath,
            ProjectCount = projectCount,
            Source = source,
            Warnings = warnings ?? [],
        };
    }

    public static LoadingResult CreateFailure(string solutionPath, string errorMessage, LoadingSource source) {
        return new LoadingResult {
            Success = false,
            SolutionPath = solutionPath,
            ErrorMessage = errorMessage,
            Source = source,
        };
    }
}
/// <summary>
/// Source of the loading request
/// </summary>
public enum LoadingSource {
    /// <summary>Background loading at startup</summary>
    BackgroundStartup,
    /// <summary>Explicit MCP tool call</summary>
    McpTool,
    /// <summary>Reload request</summary>
    Reload,
}

/// <summary>
/// Current loading status
/// </summary>
public record LoadingStatus {
    public bool IsLoading { get; init; }
    public string? CurrentSolutionPath { get; init; }
    public string? LoadedSolutionPath { get; init; }
    public bool IsLoaded { get; init; }
}

/// <summary>
/// Implementation of loading orchestrator
/// </summary>
public class LoadingOrchestrator : ILoadingOrchestrator {
    private readonly ISolutionManager _solutionManager;
    private readonly IEditorConfigProvider _editorConfigProvider;
    private readonly ILogger<LoadingOrchestrator> _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private Task<LoadingResult>? _currentLoadingTask;
    private string? _currentSolutionPath;
    private LoadingResult? _lastResult;

    public LoadingOrchestrator(
    ISolutionManager solutionManager,
    IEditorConfigProvider editorConfigProvider,
    ILogger<LoadingOrchestrator> logger
    ) {
        _solutionManager = solutionManager;
        _editorConfigProvider = editorConfigProvider;
        _logger = logger;
    }

    public async Task<LoadingResult> RequestLoadingAsync(
    string solutionPath,
    LoadingSource source,
    CancellationToken cancellationToken
    ) {
        var normalizedPath = Path.GetFullPath(solutionPath);

        await _lock.WaitAsync(cancellationToken);
        try {
            // If already loading the same solution, return existing task
            if (_currentLoadingTask != null && _currentSolutionPath == normalizedPath) {
                _logger.LogInformation(
                "Solution loading already in progress for {SolutionPath}, {NewSource} will attach to existing operation",
                normalizedPath,
                source
                );

                // Release lock before awaiting to allow other threads to attach
                _lock.Release();
                try {
                    return await _currentLoadingTask;
                } finally {
                    // Re-acquire lock for cleanup in finally block
                    await _lock.WaitAsync(cancellationToken);
                }
            }

            // If loading different solution or not loading at all, start new operation
            _logger.LogInformation("Starting new solution loading for {SolutionPath} from {Source}", normalizedPath, source);
            _currentSolutionPath = normalizedPath;
            _currentLoadingTask = LoadSolutionInternalAsync(normalizedPath, source, cancellationToken);

            // Release lock before awaiting
            _lock.Release();
            try {
                var result = await _currentLoadingTask;
                _lastResult = result;
                return result;
            } finally {
                await _lock.WaitAsync(cancellationToken);
                _currentLoadingTask = null;
                _currentSolutionPath = null;
            }
        } finally {
            if (_lock.CurrentCount == 0) {
                _lock.Release();
            }
        }
    }

    public LoadingStatus GetStatus() {
        return new LoadingStatus {
            IsLoading = _currentLoadingTask != null,
            CurrentSolutionPath = _currentSolutionPath,
            LoadedSolutionPath = _lastResult?.SolutionPath,
            IsLoaded = _lastResult?.Success ?? false,
        };
    }
    private async Task<LoadingResult> LoadSolutionInternalAsync(
        string solutionPath,
        LoadingSource source,
        CancellationToken cancellationToken
    ) {
        try {
            _logger.LogInformation("Loading solution from {Source}: {SolutionPath}", source, solutionPath);

            await _solutionManager.LoadSolutionAsync(solutionPath, cancellationToken);

            var solutionDir = Path.GetDirectoryName(solutionPath);
            if (!string.IsNullOrEmpty(solutionDir)) {
                await _editorConfigProvider.InitializeAsync(solutionDir, cancellationToken);
            }

            var projectCount = _solutionManager.GetProjects().Count();
            var warnings = _solutionManager.GetWorkspaceDiagnostics();

            _logger.LogInformation(
                "Solution loaded successfully from {Source}: {SolutionPath} with {ProjectCount} projects, {WarningCount} warnings",
                source,
                solutionPath,
                projectCount,
                warnings.Count
            );

            return LoadingResult.CreateSuccess(solutionPath, projectCount, source, warnings);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to load solution from {Source}: {SolutionPath}", source, solutionPath);
            return LoadingResult.CreateFailure(solutionPath, ex.Message, source);
        }
    }

    public async Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();

            Task<LoadingResult>? loadingTask;
            await _lock.WaitAsync(cancellationToken);
            try {
                loadingTask = _currentLoadingTask;
            } finally {
                _lock.Release();
            }

            // Нет загрузки в процессе - готов
            if (loadingTask == null) {
                return true;
            }

            // Ждём завершения текущей загрузки или таймаута
            var remainingTime = deadline - DateTime.UtcNow;
            if (remainingTime <= TimeSpan.Zero) {
                _logger.LogWarning("[LoadingOrchestrator] WaitForReadyAsync timeout after {Timeout}", timeout);
                return false;
            }

            try {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(remainingTime);
                await loadingTask.WaitAsync(cts.Token);
                return true;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                // Timeout
                _logger.LogWarning("[LoadingOrchestrator] WaitForReadyAsync timeout after {Timeout}", timeout);
                return false;
            }
        }

        return false;
    }
}

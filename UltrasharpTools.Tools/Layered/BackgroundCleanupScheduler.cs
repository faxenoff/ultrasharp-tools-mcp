using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Background scheduler for periodic cleanup tasks (delta compaction and orphaned deltas removal).
/// Phase 6.4: Background Cleanup Task Scheduler
/// </summary>
public class BackgroundCleanupScheduler : IDisposable
{
    private readonly DeltaCompactionService _compactionService;
    private readonly OrphanedDeltaCleanupService _cleanupService;
    private readonly ILogger<BackgroundCleanupScheduler> _logger;
    private readonly string _solutionPath;
    private readonly TimeSpan _compactionInterval;
    private readonly TimeSpan _cleanupInterval;

    private CancellationTokenSource? _cts;
    private Task? _compactionTask;
    private Task? _cleanupTask;
    private bool _isRunning;
    private bool _disposed;

    public BackgroundCleanupScheduler(
        DeltaCompactionService compactionService,
        OrphanedDeltaCleanupService cleanupService,
        string solutionPath,
        TimeSpan? compactionInterval = null,
        TimeSpan? cleanupInterval = null,
        ILogger<BackgroundCleanupScheduler>? logger = null
    )
    {
        _compactionService =
            compactionService ?? throw new ArgumentNullException(nameof(compactionService));
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
        _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));
        _logger = logger ?? NullLogger<BackgroundCleanupScheduler>.Instance;

        // Default: compaction every 30 minutes, cleanup every 60 minutes
        _compactionInterval = compactionInterval ?? TimeSpan.FromMinutes(30);
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(60);

        _logger.LogInformation(
            "BackgroundCleanupScheduler initialized. Compaction interval: {CompactionInterval}, Cleanup interval: {CleanupInterval}",
            _compactionInterval,
            _cleanupInterval
        );
    }

    /// <summary>
    /// Start background cleanup tasks.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogWarning("BackgroundCleanupScheduler is already running");
            return;
        }

        _logger.LogInformation("Starting background cleanup scheduler...");

        _cts = new CancellationTokenSource();
        _isRunning = true;

        // Start compaction task
        _compactionTask = Task.Run(
            async () =>
            {
                await RunCompactionLoopAsync(_cts.Token);
            },
            _cts.Token
        );

        // Start cleanup task
        _cleanupTask = Task.Run(
            async () =>
            {
                await RunCleanupLoopAsync(_cts.Token);
            },
            _cts.Token
        );

        _logger.LogInformation("Background cleanup scheduler started");
    }

    /// <summary>
    /// Stop background cleanup tasks gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("BackgroundCleanupScheduler is not running");
            return;
        }

        _logger.LogInformation("Stopping background cleanup scheduler...");

        _cts?.Cancel();
        _isRunning = false;

        // Wait for tasks to complete
        try
        {
            if (_compactionTask != null)
            {
                await _compactionTask;
            }

            if (_cleanupTask != null)
            {
                await _cleanupTask;
            }

            _logger.LogInformation("Background cleanup scheduler stopped");
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            _logger.LogDebug("Background tasks cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping background cleanup scheduler");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Periodic compaction loop.
    /// </summary>
    private async Task RunCompactionLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delta compaction loop started");

        using var timer = new PeriodicTimer(_compactionInterval);

        try
        {
            // Run immediately on startup
            await RunCompactionAsync(cancellationToken);

            // Then run periodically
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RunCompactionAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Delta compaction loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delta compaction loop failed");
        }
    }

    /// <summary>
    /// Periodic cleanup loop.
    /// </summary>
    private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Orphaned delta cleanup loop started");

        using var timer = new PeriodicTimer(_cleanupInterval);

        try
        {
            // Run immediately on startup
            await RunCleanupAsync(cancellationToken);

            // Then run periodically
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RunCleanupAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Orphaned delta cleanup loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orphaned delta cleanup loop failed");
        }
    }

    /// <summary>
    /// Run delta compaction pass.
    /// </summary>
    private async Task RunCompactionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running delta compaction pass...");

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            await _compactionService.CompactAllLargeDeltasAsync(_solutionPath, cancellationToken);

            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "Delta compaction pass completed in {Elapsed}ms",
                elapsed.TotalMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delta compaction pass failed");
        }
    }

    /// <summary>
    /// Run orphaned delta cleanup pass.
    /// </summary>
    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running orphaned delta cleanup pass...");

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            await _cleanupService.CleanupOrphanedDeltasAsync(_solutionPath, cancellationToken);

            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "Orphaned delta cleanup pass completed in {Elapsed}ms",
                elapsed.TotalMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orphaned delta cleanup pass failed");
        }
    }

    /// <summary>
    /// Trigger compaction immediately (outside of scheduled interval).
    /// </summary>
    public async Task TriggerCompactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual compaction triggered");
        await RunCompactionAsync(cancellationToken);
    }

    /// <summary>
    /// Trigger cleanup immediately (outside of scheduled interval).
    /// </summary>
    public async Task TriggerCleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual cleanup triggered");
        await RunCleanupAsync(cancellationToken);
    }

    /// <summary>
    /// Check if scheduler is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_isRunning)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        _cts?.Dispose();
    }
}


using System.Threading.Channels;

using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Queues and batches incremental updates to FastSymbolIndex.
/// Handles workspace change events with throttling and error recovery.
/// </summary>
public sealed class IncrementalUpdateQueue : IDisposable
{
    private readonly FastSymbolIndex _symbolIndex;
    private readonly ILogger<IncrementalUpdateQueue> _logger;
    private readonly Channel<DocumentUpdate> _updateChannel;
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly Task _processorTask;

    // Batching configuration
    private readonly TimeSpan _batchWindow = TimeSpan.FromMilliseconds(300);
    private readonly int _maxBatchSize = 50;

    // Deduplication
    private readonly ConcurrentDictionary<DocumentId, DocumentUpdate> _pendingUpdates = new();

    public IncrementalUpdateQueue(
        FastSymbolIndex symbolIndex,
        ILogger<IncrementalUpdateQueue>? logger = null)
    {
        _symbolIndex = symbolIndex ?? throw new ArgumentNullException(nameof(symbolIndex));
        _logger = logger ?? NullLogger<IncrementalUpdateQueue>.Instance;

        // Unbounded channel for updates (backpressure handled by batching)
        _updateChannel = Channel.CreateUnbounded<DocumentUpdate>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start background processor
        _processorTask = Task.Run(ProcessUpdatesAsync, _disposalCts.Token);

        _logger.LogInformation(
            "IncrementalUpdateQueue started (batch window: {BatchWindow}ms, max batch: {MaxBatch})",
            _batchWindow.TotalMilliseconds,
            _maxBatchSize);
    }

    /// <summary>
    /// Enqueue document update for processing.
    /// Deduplicates updates to same document.
    /// </summary>
    public async Task EnqueueAsync(DocumentUpdate update, CancellationToken cancellationToken = default)
    {
        // Deduplicate: only keep latest update for each document
        _pendingUpdates.AddOrUpdate(
            update.DocumentId,
            update,
            (_, _) => update);

        await _updateChannel.Writer.WriteAsync(update, cancellationToken);
    }

    /// <summary>
    /// Background processor: batches and applies updates.
    /// </summary>
    private async Task ProcessUpdatesAsync()
    {
        var cancellationToken = _disposalCts.Token;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Collect batch within time window
                var batch = await CollectBatchAsync(cancellationToken);
                if (batch.Count == 0)
                {
                    continue;
                }

                _logger.LogDebug("Processing batch of {Count} updates", batch.Count);

                // Apply updates
                await ApplyBatchAsync(batch, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("IncrementalUpdateQueue processor stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in IncrementalUpdateQueue processor");
        }
    }

    /// <summary>
    /// Collect batch of updates within time window.
    /// </summary>
    private async Task<List<DocumentUpdate>> CollectBatchAsync(CancellationToken cancellationToken)
    {
        var batch = new List<DocumentUpdate>();
        var batchStart = DateTime.UtcNow;

        try
        {
            // Wait for first update
            var firstUpdate = await _updateChannel.Reader.ReadAsync(cancellationToken);
            batch.Add(firstUpdate);

            // Collect more updates within time window
            while (batch.Count < _maxBatchSize)
            {
                var elapsed = DateTime.UtcNow - batchStart;
                var remaining = _batchWindow - elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    break; // Batch window expired
                }

                // Try to read more updates with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(remaining);

                try
                {
                    var update = await _updateChannel.Reader.ReadAsync(timeoutCts.Token);
                    batch.Add(update);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout - batch window expired
                    break;
                }
            }

            return batch;
        }
        catch (OperationCanceledException)
        {
            return batch; // Return partial batch
        }
    }

    /// <summary>
    /// Apply batch of updates to symbol index.
    /// </summary>
    private async Task ApplyBatchAsync(List<DocumentUpdate> batch, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var successCount = 0;
        var failureCount = 0;

        // Deduplicate batch: only keep latest update per document
        var deduplicatedBatch = batch
            .GroupBy(u => u.DocumentId)
            .Select(g => g.Last())
            .ToList();

        _logger.LogDebug("Applying {Count} updates (deduplicated from {Original})",
            deduplicatedBatch.Count, batch.Count);

        foreach (var update in deduplicatedBatch)
        {
            try
            {
                // Remove from pending
                _pendingUpdates.TryRemove(update.DocumentId, out _);

                // Apply update
                await ApplyUpdateAsync(update, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to apply update for document {DocumentId} ({Kind})",
                    update.DocumentId, update.Kind);
                failureCount++;
            }
        }

        sw.Stop();
        _logger.LogInformation(
            "Batch completed: {Success} succeeded, {Failure} failed in {Elapsed}ms",
            successCount, failureCount, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Apply single update to symbol index.
    /// </summary>
    private async Task ApplyUpdateAsync(DocumentUpdate update, CancellationToken cancellationToken)
    {
        var document = update.Solution.GetDocument(update.DocumentId);
        if (document == null)
        {
            _logger.LogWarning("Document not found: {DocumentId}", update.DocumentId);
            return;
        }

        switch (update.Kind)
        {
            case DocumentChangeKind.Added:
                await _symbolIndex.AddDocumentAsync(update.Solution, update.DocumentId, cancellationToken);
                _logger.LogDebug("Added document: {FilePath}", document.FilePath);
                break;

            case DocumentChangeKind.Modified:
                await _symbolIndex.UpdateDocumentAsync(update.Solution, update.DocumentId, cancellationToken);
                _logger.LogDebug("Updated document: {FilePath}", document.FilePath);
                break;

            case DocumentChangeKind.Removed:
                await _symbolIndex.RemoveDocumentAsync(update.DocumentId, cancellationToken);
                _logger.LogDebug("Removed document: {DocumentId}", update.DocumentId);
                break;

            default:
                _logger.LogWarning("Unknown change kind: {Kind}", update.Kind);
                break;
        }
    }

    /// <summary>
    /// Get number of pending updates in queue.
    /// </summary>
    public int PendingCount => _updateChannel.Reader.Count;

    public void Dispose()
    {
        _logger.LogInformation("Disposing IncrementalUpdateQueue...");

        _disposalCts.Cancel();
        _updateChannel.Writer.Complete();

        try
        {
            _processorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for processor task to complete");
        }

        _disposalCts.Dispose();
    }
}

/// <summary>
/// Represents a document update event.
/// </summary>
public record DocumentUpdate(
    DocumentId DocumentId,
    DocumentChangeKind Kind,
    Solution Solution);

/// <summary>
/// Type of document change.
/// </summary>
public enum DocumentChangeKind
{
    Added,
    Modified,
    Removed
}

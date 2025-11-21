using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Сервис для batching semantic queries к Overlord
/// Группирует несколько запросов в один batch для повышения эффективности
/// </summary>
public sealed class RequestBatchingService : IAsyncDisposable
{
    private readonly IServerBridgeService _serverBridge;
    private readonly ILogger<RequestBatchingService> _logger;
    private readonly TimeSpan _batchWindow;
    private readonly int _maxBatchSize;

    private readonly ConcurrentQueue<BatchedRequest> _pendingRequests = new();
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _batchLock = new(1, 1);

    public RequestBatchingService(
        IServerBridgeService serverBridge,
        ILogger<RequestBatchingService> logger,
        TimeSpan? batchWindow = null,
        int maxBatchSize = 10
    )
    {
        _serverBridge = serverBridge;
        _logger = logger;
        _batchWindow = batchWindow ?? TimeSpan.FromMilliseconds(50);
        _maxBatchSize = maxBatchSize;

        // Timer для периодической отправки batch
        _batchTimer = new Timer(
            _ => ProcessBatchAsync().GetAwaiter().GetResult(),
            null,
            _batchWindow,
            _batchWindow
        );
    }

    /// <summary>
    /// Добавить запрос в batch queue
    /// </summary>
    public async Task<string> ExecuteWithBatchingAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default
    )
    {
        // Только для semantic tools применяем batching
        if (!IsSemanticTool(toolName))
        {
            // Для non-semantic tools - прямой вызов
            return await _serverBridge.CallMcpProxyAsync(
                toolName,
                argumentsJson,
                projectContext,
                cancellationToken
            );
        }

        var request = new BatchedRequest
        {
            ToolName = toolName,
            ArgumentsJson = argumentsJson,
            ProjectContext = projectContext,
            CancellationToken = cancellationToken,
            CompletionSource = new TaskCompletionSource<string>(),
        };

        _pendingRequests.Enqueue(request);

        _logger.LogDebug(
            "Request queued for batching: {ToolName}, queue size: {QueueSize}",
            toolName,
            _pendingRequests.Count
        );

        // Если достигли max batch size, обработать немедленно
        if (_pendingRequests.Count >= _maxBatchSize)
        {
            _ = Task.Run(() => ProcessBatchAsync(), CancellationToken.None);
        }

        // Ждем результата
        return await request.CompletionSource.Task;
    }

    /// <summary>
    /// Обработать накопленные запросы batch'ем
    /// </summary>
    private async Task ProcessBatchAsync()
    {
        if (_pendingRequests.IsEmpty)
        {
            return;
        }

        await _batchLock.WaitAsync();
        try
        {
            var batch = new List<BatchedRequest>();

            // Собрать до maxBatchSize запросов
            while (batch.Count < _maxBatchSize && _pendingRequests.TryDequeue(out var request))
            {
                batch.Add(request);
            }

            if (batch.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Processing batch of {BatchSize} requests", batch.Count);

            var sw = Stopwatch.StartNew();

            // Выполнить все запросы параллельно
            var tasks = batch
                .Select(req =>
                    Task.Run(
                        async () =>
                        {
                            try
                            {
                                var result = await _serverBridge.CallMcpProxyAsync(
                                    req.ToolName,
                                    req.ArgumentsJson,
                                    req.ProjectContext,
                                    req.CancellationToken
                                );

                                req.CompletionSource.SetResult(result);
                            }
                            catch (Exception ex)
                            {
                                req.CompletionSource.SetException(ex);
                            }
                        },
                        req.CancellationToken
                    )
                )
                .ToArray();

            await Task.WhenAll(tasks);

            sw.Stop();

            _logger.LogInformation(
                "Batch of {BatchSize} requests completed in {ElapsedMs}ms ({AvgMs}ms per request)",
                batch.Count,
                sw.ElapsedMilliseconds,
                sw.ElapsedMilliseconds / batch.Count
            );
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Проверить является ли tool semantic (подходит для batching)
    /// </summary>
    private static bool IsSemanticTool(string toolName)
    {
        return toolName switch
        {
            "semantic_search" => true,
            "semantic_diff" => true,
            "find_duplicates" => true,
            "pattern_search" => true,
            "reindex_changed_files" => true,
            _ => false,
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _batchTimer.DisposeAsync();
        _batchLock.Dispose();

        // Обработать оставшиеся запросы
        if (!_pendingRequests.IsEmpty)
        {
            await ProcessBatchAsync();
        }
    }

    private sealed class BatchedRequest
    {
        public required string ToolName { get; init; }
        public required string ArgumentsJson { get; init; }
        public required string? ProjectContext { get; init; }
        public required CancellationToken CancellationToken { get; init; }
        public required TaskCompletionSource<string> CompletionSource { get; init; }
    }
}

using System.Buffers;
using System.IO.Pipes;
using Ultrasharp.Addon.Models;

namespace Ultrasharp.Addon.Ipc;

public sealed class PipeServerOptions
{
    public string PipeName { get; set; } = "UltraScript_Roslyn_default";
}

/// <summary>
/// Named Pipe server that accepts one client at a time and processes binary-framed JSON messages.
/// Pattern: VectorDB — single connection, reconnect loop.
/// </summary>
public sealed class PipeServer
{
    private readonly PipeServerOptions _options;
    private readonly RequestRouter _router;
    private readonly ILogger<PipeServer> _logger;

    // Pre-allocated read buffer (16 KB)
    private const int ReadBufferSize = 16384;

    public PipeServer(PipeServerOptions options, RequestRouter router, ILogger<PipeServer> logger)
    {
        _options = options;
        _router = router;
        _logger = logger;
    }

    /// <summary>
    /// Sends an event to the currently connected client. Thread-safe via SemaphoreSlim in write path.
    /// </summary>
    public Func<AddonEvent, ValueTask>? SendEvent { get; set; }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    _options.PipeName,
                    PipeDirection.InOut,
                    1, // single client
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogInformation("[Pipe] Waiting for connection on: {Name}", _options.PipeName);
                await pipe.WaitForConnectionAsync(ct);
                _logger.LogInformation("[Pipe] Client connected.");

                await ProcessConnectionAsync(pipe, ct);

                _logger.LogInformation("[Pipe] Client disconnected. Restarting listener...");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                _logger.LogWarning("[Pipe] IO error: {Message}. Restarting...", ex.Message);
            }
        }
    }

    private async Task ProcessConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var writeSemaphore = new SemaphoreSlim(1, 1);
        var accumulatedBuffer = new MemoryStream();
        var readBuffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);

        // Wire up event sending for this connection
        SendEvent = async (evt) =>
        {
            if (!pipe.IsConnected) return;
            var frame = BinaryFrameCodec.Encode(evt);
            await writeSemaphore.WaitAsync(ct);
            try
            {
                await pipe.WriteAsync(frame, ct);
                await pipe.FlushAsync(ct);
            }
            finally
            {
                writeSemaphore.Release();
            }
        };

        try
        {
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                int bytesRead = await pipe.ReadAsync(readBuffer.AsMemory(), ct);
                if (bytesRead == 0)
                    break; // Client disconnected

                // Append to accumulation buffer
                accumulatedBuffer.Write(readBuffer, 0, bytesRead);

                // Try to decode complete frames
                var data = new ReadOnlyMemory<byte>(accumulatedBuffer.GetBuffer(), 0, (int)accumulatedBuffer.Length);
                while (true)
                {
                    var request = BinaryFrameCodec.TryDecode<AddonRequest>(ref data);
                    if (request == null)
                        break;

                    // Process request (fire-and-forget for concurrent handling)
                    var req = request;
                    _ = Task.Run(async () =>
                    {
                        AddonResponse response;
                        try
                        {
                            response = await _router.HandleAsync(req, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[Pipe] Handler error for {Method}", req.Method);
                            response = AddonResponse.Fail(req.Id, ErrorCodes.InternalError, ex.Message);
                        }

                        if (!pipe.IsConnected) return;

                        var frame = BinaryFrameCodec.Encode(response);
                        await writeSemaphore.WaitAsync(ct);
                        try
                        {
                            await pipe.WriteAsync(frame, ct);
                            await pipe.FlushAsync(ct);
                        }
                        finally
                        {
                            writeSemaphore.Release();
                        }
                    }, ct);
                }

                // Compact buffer: keep remaining bytes
                if (data.Length > 0)
                {
                    var remaining = data.ToArray();
                    accumulatedBuffer.SetLength(0);
                    accumulatedBuffer.Write(remaining);
                }
                else
                {
                    accumulatedBuffer.SetLength(0);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { /* client disconnected */ }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            SendEvent = null;
        }
    }
}

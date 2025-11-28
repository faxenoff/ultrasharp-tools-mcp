using System.Buffers;
using System.IO.Pipelines;

namespace UltraSharpTools.Comm;

/// <summary>
/// MCP Bridge: proxies MCP messages between Claude Desktop (stdin/stdout) and Droid process
/// Pure byte-level proxy - no JSON parsing or deserialization
/// </summary>
public sealed class McpBridge
{
    private readonly DroidProcessManager _processManager;

    public McpBridge(DroidProcessManager processManager)
    {
        _processManager = processManager;
    }

    /// <summary>
    /// Starts bidirectional proxy between Claude Desktop and Droid
    /// </summary>
    public async Task RunAsync(Stream stdin, Stream stdout, CancellationToken cancellationToken = default)
    {
        // Start Droid process and get its streams
        var droidStreams = _processManager.StartDroid();

        // Bidirectional proxy (pure bytes, no parsing):
        // Claude Desktop stdin → Droid stdin
        // Droid stdout → Claude Desktop stdout

#pragma warning disable CA2000 // CancellationTokenSource disposed in using
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore CA2000

        var stdinTask = ProxyStreamAsync(stdin, droidStreams.Input, cts.Token);
        var stdoutTask = ProxyStreamAsync(droidStreams.Output, stdout, cts.Token);

        // Wait for either direction to complete
        await Task.WhenAny(stdinTask, stdoutTask);

        // Cancel remaining task
        cts.Cancel();

        // Give graceful shutdown time
        await Task.Delay(100, CancellationToken.None);
    }

    /// <summary>
    /// Proxies raw bytes from source to destination (zero overhead)
    /// </summary>
    private static async Task ProxyStreamAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var reader = PipeReader.Create(source);
        var writer = PipeWriter.Create(destination);

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    // Write raw bytes without any processing
                    foreach (var segment in buffer)
                    {
                        await writer.WriteAsync(segment, cancellationToken);
                    }
                    await writer.FlushAsync(cancellationToken);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex) when (ex.Message.Contains("closed") || ex.Message.Contains("broken pipe"))
        {
            // Graceful shutdown - connection closed
        }
        finally
        {
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }
}

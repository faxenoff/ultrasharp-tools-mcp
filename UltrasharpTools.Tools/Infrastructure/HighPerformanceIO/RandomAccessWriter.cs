using Microsoft.Win32.SafeHandles;

namespace UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

/// <summary>
/// High-performance random access file writer using RandomAccess API (.NET 6+).
/// - Thread-safe without locks
/// - Positional writes without seeking
/// - Gather I/O (write multiple buffers in one syscall)
/// - Supports file preallocation
/// </summary>
public sealed class RandomAccessWriter : IAsyncDisposable
{
    private SafeFileHandle? _handle;
    private bool _disposed;

    /// <summary>
    /// Create a random access file writer.
    /// </summary>
    /// <param name="filePath">Path to file</param>
    /// <param name="preallocationSize">Optional file preallocation size</param>
    public RandomAccessWriter(string filePath, long? preallocationSize = null)
    {
        _handle = File.OpenHandle(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileOptions.Asynchronous,
            preallocationSize: preallocationSize ?? 0
        );

        _disposed = false;
    }

    /// <summary>
    /// Write data at specific offset (thread-safe).
    /// No seeking required - direct access.
    /// </summary>
    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        long fileOffset,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await RandomAccess.WriteAsync(_handle!, buffer, fileOffset, ct);
    }

    /// <summary>
    /// Gather I/O - write multiple buffers in one syscall.
    /// Perfect for writing structured data (header + body + footer).
    /// Example: Write header at offset 0, body at 1024, footer at end.
    /// </summary>
    public async ValueTask WriteGatherAsync(
        IReadOnlyList<ReadOnlyMemory<byte>> buffers,
        long fileOffset,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await RandomAccess.WriteAsync(_handle!, buffers, fileOffset, ct);
    }

    /// <summary>
    /// Write multiple regions in parallel.
    /// Each write is independent and thread-safe.
    /// </summary>
    public async Task WriteParallelAsync(
        IEnumerable<(long Offset, byte[] Data)> regions,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var tasks = regions.Select(region =>
            RandomAccess.WriteAsync(_handle!, region.Data, region.Offset, ct).AsTask()
        );

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Flush data to disk.
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();

        RandomAccess.FlushToDisk(_handle!);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessWriter));
    }

    /// <summary>
    /// Dispose file handle.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _handle?.Dispose();
        _handle = null;

        return ValueTask.CompletedTask;
    }
}

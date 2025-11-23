using System.Buffers;

namespace UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

/// <summary>
/// MemoryManager that wraps a pooled array and returns it when disposed.
/// Enables returning Memory&lt;T&gt; backed by ArrayPool.
/// This allows zero-copy access to pooled buffers through Memory&lt;T&gt;.
/// </summary>
/// <typeparam name="T">Element type (typically byte or char)</typeparam>
internal sealed class PooledMemoryManager<T> : MemoryManager<T>
{
    private T[]? _array;
    private readonly int _start;
    private readonly int _length;
    private bool _disposed;

    /// <summary>
    /// Create a PooledMemoryManager wrapping a pooled array.
    /// </summary>
    /// <param name="array">Pooled array to wrap</param>
    /// <param name="start">Start offset in the array</param>
    /// <param name="length">Length of the valid portion</param>
    public PooledMemoryManager(T[] array, int start, int length)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (start < 0 || start >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || start + length > array.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        _array = array;
        _start = start;
        _length = length;
        _disposed = false;
    }

    /// <summary>
    /// Get span representing the valid portion of the pooled array.
    /// </summary>
    public override Span<T> GetSpan()
    {
        if (_disposed || _array == null)
            throw new ObjectDisposedException(nameof(PooledMemoryManager<T>));

        return _array.AsSpan(_start, _length);
    }

    /// <summary>
    /// Pinning is not supported for pooled memory.
    /// Use Span&lt;T&gt; instead if pinning is required.
    /// </summary>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        throw new NotSupportedException(
            "Pinning is not supported for pooled memory. " +
            "Use Span<T> instead if pinning is required."
        );
    }

    /// <summary>
    /// Unpin is not supported (pinning is not supported).
    /// </summary>
    public override void Unpin()
    {
        // No-op - pinning is not supported
    }

    /// <summary>
    /// Return the pooled array to the pool.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed || _array == null)
            return;

        if (disposing)
        {
            // Return array to pool based on type
            if (typeof(T) == typeof(byte))
            {
                BufferPoolManager.ReturnBytes((byte[])(object)_array);
            }
            else if (typeof(T) == typeof(char))
            {
                BufferPoolManager.ReturnChars((char[])(object)_array);
            }

            _array = null;
            _disposed = true;
        }
    }
}

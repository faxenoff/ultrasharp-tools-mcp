using System.Buffers;

namespace UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

/// <summary>
/// Centralized buffer pool management using ArrayPool&lt;T&gt;.
/// Eliminates ~100% of temporary array allocations by reusing buffers.
/// Thread-safe and high-performance.
/// </summary>
public static class BufferPoolManager
{
    /// <summary>
    /// Rent a byte array from the shared pool.
    /// Array length will be >= minimumLength.
    /// IMPORTANT: Must call Return() after use or use RentDisposable().
    /// </summary>
    /// <param name="minimumLength">Minimum required length</param>
    /// <returns>Rented byte array (may be larger than requested)</returns>
    public static byte[] RentBytes(int minimumLength)
    {
        return ArrayPool<byte>.Shared.Rent(minimumLength);
    }

    /// <summary>
    /// Rent a char array from the shared pool.
    /// Array length will be >= minimumLength.
    /// IMPORTANT: Must call Return() after use or use RentDisposable().
    /// </summary>
    /// <param name="minimumLength">Minimum required length</param>
    /// <returns>Rented char array (may be larger than requested)</returns>
    public static char[] RentChars(int minimumLength)
    {
        return ArrayPool<char>.Shared.Rent(minimumLength);
    }

    /// <summary>
    /// Return a byte array to the pool.
    /// </summary>
    /// <param name="buffer">Buffer to return</param>
    /// <param name="clearArray">If true, clears the array before returning</param>
    public static void ReturnBytes(byte[] buffer, bool clearArray = false)
    {
        ArrayPool<byte>.Shared.Return(buffer, clearArray);
    }

    /// <summary>
    /// Return a char array to the pool.
    /// </summary>
    /// <param name="buffer">Buffer to return</param>
    /// <param name="clearArray">If true, clears the array before returning</param>
    public static void ReturnChars(char[] buffer, bool clearArray = false)
    {
        ArrayPool<char>.Shared.Return(buffer, clearArray);
    }

    /// <summary>
    /// Rent a byte array that will be automatically returned when disposed.
    /// Recommended for most scenarios to avoid forgetting to return buffers.
    /// </summary>
    /// <param name="minimumLength">Minimum required length</param>
    /// <returns>Disposable buffer wrapper</returns>
    public static PooledBuffer<byte> RentBytesDisposable(int minimumLength)
    {
        return new PooledBuffer<byte>(minimumLength);
    }

    /// <summary>
    /// Rent a char array that will be automatically returned when disposed.
    /// Recommended for most scenarios to avoid forgetting to return buffers.
    /// </summary>
    /// <param name="minimumLength">Minimum required length</param>
    /// <returns>Disposable buffer wrapper</returns>
    public static PooledBuffer<char> RentCharsDisposable(int minimumLength)
    {
        return new PooledBuffer<char>(minimumLength);
    }
}

/// <summary>
/// Disposable wrapper for pooled arrays.
/// Automatically returns buffer to pool when disposed.
/// Use with 'using' statement or 'using var' declaration.
/// </summary>
/// <typeparam name="T">Array element type (byte or char)</typeparam>
public ref struct PooledBuffer<T>
{
    private T[]? _buffer;
    private readonly int _length;
    private bool _disposed;

    /// <summary>
    /// Create a pooled buffer of specified size.
    /// </summary>
    /// <param name="minimumLength">Minimum required length</param>
    internal PooledBuffer(int minimumLength)
    {
        if (typeof(T) == typeof(byte))
        {
            _buffer = (T[])(object)ArrayPool<byte>.Shared.Rent(minimumLength);
        }
        else if (typeof(T) == typeof(char))
        {
            _buffer = (T[])(object)ArrayPool<char>.Shared.Rent(minimumLength);
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T)} is not supported. Only byte and char are supported.");
        }

        _length = minimumLength;
        _disposed = false;
    }

    /// <summary>
    /// Get the rented buffer as a span.
    /// Span is limited to the requested length (not the actual buffer length).
    /// </summary>
    public readonly Span<T> Span
    {
        get
        {
            if (_disposed || _buffer == null)
                throw new ObjectDisposedException(nameof(PooledBuffer<T>));

            return _buffer.AsSpan(0, _length);
        }
    }

    /// <summary>
    /// Get the rented buffer as memory.
    /// Memory is limited to the requested length (not the actual buffer length).
    /// </summary>
    public readonly Memory<T> Memory
    {
        get
        {
            if (_disposed || _buffer == null)
                throw new ObjectDisposedException(nameof(PooledBuffer<T>));

            return _buffer.AsMemory(0, _length);
        }
    }

    /// <summary>
    /// Get the actual rented array.
    /// WARNING: Array length may be larger than requested.
    /// Use Span or Memory for safe access to the valid portion.
    /// </summary>
    public readonly T[] Array
    {
        get
        {
            if (_disposed || _buffer == null)
                throw new ObjectDisposedException(nameof(PooledBuffer<T>));

            return _buffer;
        }
    }

    /// <summary>
    /// Get the requested length (valid portion of the buffer).
    /// </summary>
    public readonly int Length => _length;

    /// <summary>
    /// Return buffer to pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed || _buffer == null)
            return;

        if (typeof(T) == typeof(byte))
        {
            ArrayPool<byte>.Shared.Return((byte[])(object)_buffer);
        }
        else if (typeof(T) == typeof(char))
        {
            ArrayPool<char>.Shared.Return((char[])(object)_buffer);
        }

        _buffer = null;
        _disposed = true;
    }
}

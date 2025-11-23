using System.IO.MemoryMappedFiles;

namespace UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

/// <summary>
/// Memory-mapped file writer for very large files (>100MB).
/// - Zero-copy writes
/// - Instant random access (no seek overhead)
/// - OS manages flushing automatically
/// - Perfect for large structured file formats
/// </summary>
public sealed class MemoryMappedFileWriter : IDisposable
{
private MemoryMappedFile? _mmf;
private MemoryMappedViewAccessor? _accessor;
private readonly long _capacity;
private bool _disposed;

/// <summary>
/// Create a memory-mapped file writer.
/// Best for files >100MB with random access write patterns.
/// </summary>
/// <param name="filePath">Path to file</param>
/// <param name="capacity">File capacity in bytes</param>
public MemoryMappedFileWriter(string filePath, long capacity)
{
if (capacity <= 0)
throw new ArgumentException("Capacity must be positive", nameof(capacity));

_capacity = capacity;

// Use FileStream with FileShare.Read to allow reading while writing
// FileStream is owned by MemoryMappedFile (leaveOpen: false)
#pragma warning disable CA2000 // FileStream disposed by MemoryMappedFile
var fileStream = new FileStream(
filePath,
FileMode.Create,
FileAccess.ReadWrite,
FileShare.Read,
bufferSize: 4096,
FileOptions.RandomAccess
);

// Set file length
fileStream.SetLength(capacity);

_mmf = MemoryMappedFile.CreateFromFile(
fileStream,
mapName: null,
capacity: capacity,
MemoryMappedFileAccess.ReadWrite,
HandleInheritability.None,
leaveOpen: false  // FileStream будет автоматически закрыт
);
#pragma warning restore CA2000

_accessor = _mmf.CreateViewAccessor(
offset: 0,
size: 0,
MemoryMappedFileAccess.ReadWrite
);

_disposed = false;
}

/// <summary>
/// Get file capacity.
/// </summary>
public long Capacity => _capacity;

/// <summary>
/// Write a structure at specific position (instant, no I/O).
/// Perfect for writing headers, metadata, fixed-size records.
/// </summary>
public void Write<T>(long position, ref T value) where T : struct
{
ThrowIfDisposed();

_accessor!.Write(position, ref value);
}

/// <summary>
/// Write array of structures starting at position.
/// </summary>
public void WriteArray<T>(long position, T[] array, int offset, int count) where T : struct
{
ThrowIfDisposed();

_accessor!.WriteArray(position, array, offset, count);
}

/// <summary>
/// Write bytes at position from provided buffer.
/// </summary>
public void WriteBytes(long position, byte[] buffer, int offset, int count)
{
ThrowIfDisposed();

_accessor!.WriteArray(position, buffer, offset, count);
}

/// <summary>
/// Get span for zero-copy write access (requires unsafe).
/// FASTEST possible access - directly writes to file-backed memory.
/// WARNING: Span is only valid while accessor is alive.
/// </summary>
public unsafe Span<byte> GetSpan(long offset, int length)
{
ThrowIfDisposed();

if (offset < 0 || offset >= _capacity)
throw new ArgumentOutOfRangeException(nameof(offset));

if (length < 0 || offset + length > _capacity)
throw new ArgumentOutOfRangeException(nameof(length));

byte* ptr = null;
_accessor!.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

// Note: Caller must ensure accessor stays alive while using span
return new Span<byte>(ptr + offset, length);
}

/// <summary>
/// Write string at position (null-terminated or fixed length).
/// </summary>
public void WriteString(long position, string text, int maxLength, System.Text.Encoding? encoding = null)
{
ThrowIfDisposed();

encoding ??= System.Text.Encoding.UTF8;

var buffer = BufferPoolManager.RentBytes(maxLength);
try
{
int bytesWritten = encoding.GetBytes(text, 0, Math.Min(text.Length, maxLength), buffer, 0);

// Add null terminator if space available
if (bytesWritten < maxLength)
buffer[bytesWritten] = 0;

_accessor!.WriteArray(position, buffer, 0, maxLength);
}
finally
{
BufferPoolManager.ReturnBytes(buffer);
}
}

/// <summary>
/// Flush changes to disk.
/// Note: OS manages flushing automatically, but this forces immediate write.
/// </summary>
public void Flush()
{
ThrowIfDisposed();

_accessor!.Flush();
}

private void ThrowIfDisposed()
{
if (_disposed || _accessor == null)
throw new ObjectDisposedException(nameof(MemoryMappedFileWriter));
}

/// <summary>
/// Dispose memory-mapped file and accessor.
/// </summary>
public void Dispose()
{
if (_disposed)
return;

_disposed = true;
_accessor?.Flush();
_accessor?.Dispose();
_mmf?.Dispose();
_accessor = null;
_mmf = null;
}
}

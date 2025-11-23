using System.Text;

namespace UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

/// <summary>
/// High-level API for file I/O.
/// For typical code files (&lt;10MB), this is a transparent wrapper around File.* APIs.
/// Specialized methods are available for large files:
/// - ReadLargeFileAsync() - streaming I/O with pooled buffers
/// - ReadLinesAsync() - constant memory O(1) line-by-line reading
/// - CopyFileAsync() - parallel chunk-based copying
/// </summary>
public static class OptimizedFileIO
{
/// <summary>
/// Read entire file as string.
/// For typical code files, this is a direct passthrough to File.ReadAllTextAsync.
/// </summary>
public static Task<string> ReadAllTextAsync(
string filePath,
Encoding? encoding = null,
CancellationToken cancellationToken = default)
{
// Transparent wrapper - no overhead
return File.ReadAllTextAsync(filePath, encoding ?? Encoding.UTF8, cancellationToken);
}

/// <summary>
/// Write entire string to file.
/// For typical code files, this is a direct passthrough to File.WriteAllTextAsync.
/// </summary>
public static Task WriteAllTextAsync(
string filePath,
string content,
Encoding? encoding = null,
CancellationToken cancellationToken = default)
{
// Transparent wrapper - no overhead
return File.WriteAllTextAsync(filePath, content, encoding ?? Encoding.UTF8, cancellationToken);
}

/// <summary>
/// Read entire file as byte array.
/// For typical files, this is a direct passthrough to File.ReadAllBytesAsync.
/// </summary>
public static Task<byte[]> ReadAllBytesAsync(
string filePath,
CancellationToken cancellationToken = default)
{
// Transparent wrapper - no overhead
return File.ReadAllBytesAsync(filePath, cancellationToken);
}

/// <summary>
/// Write byte array to file.
/// For typical files, this is a direct passthrough to File.WriteAllBytesAsync.
/// </summary>
public static Task WriteAllBytesAsync(
string filePath,
byte[] bytes,
CancellationToken cancellationToken = default)
{
// Transparent wrapper - no overhead
return File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
}

// ==================== Specialized Methods for Large Files ====================

/// <summary>
/// Read large file (>10MB) using streaming I/O with pooled buffers.
/// More efficient than ReadAllTextAsync for very large files.
/// </summary>
public static async Task<string> ReadLargeFileAsync(
string filePath,
Encoding? encoding = null,
CancellationToken cancellationToken = default)
{
encoding ??= Encoding.UTF8;

var fileInfo = new FileInfo(filePath);
if (!fileInfo.Exists)
throw new FileNotFoundException("File not found", filePath);

// Rent buffer from pool (avoid LOH allocation)
var buffer = BufferPoolManager.RentBytes((int)fileInfo.Length + 4096);
try
{
await using var stream = File.OpenRead(filePath);
int totalRead = 0;

while (totalRead < buffer.Length)
{
int bytesRead = await stream.ReadAsync(
buffer.AsMemory(totalRead),
cancellationToken
);

if (bytesRead == 0)
break;

totalRead += bytesRead;
}

return encoding.GetString(buffer, 0, totalRead);
}
finally
{
BufferPoolManager.ReturnBytes(buffer);
}
}

/// <summary>
/// Write large file (>10MB) using streaming I/O with preallocation.
/// More efficient than WriteAllTextAsync for very large content.
/// </summary>
public static async Task WriteLargeFileAsync(
string filePath,
string content,
Encoding? encoding = null,
CancellationToken cancellationToken = default)
{
encoding ??= Encoding.UTF8;

var contentSize = encoding.GetByteCount(content);

await using var writer = new StreamingFileWriter(
filePath,
bufferSize: 81920,
preallocationSize: contentSize
);

await writer.WriteAsync(content, encoding, cancellationToken);
}

/// <summary>
/// Write lines to file using streaming I/O.
/// More efficient than WriteAllTextAsync for large collections.
/// </summary>
public static async Task WriteLinesAsync(
string filePath,
IEnumerable<string> lines,
Encoding? encoding = null,
CancellationToken cancellationToken = default)
{
encoding ??= Encoding.UTF8;

await using var writer = new StreamingFileWriter(filePath);
await writer.WriteLinesAsync(lines, encoding, lineEnding: Environment.NewLine, cancellationToken);
}

/// <summary>
/// Read lines from file using streaming I/O.
/// Constant memory O(1) - perfect for large files.
/// </summary>
public static async IAsyncEnumerable<string> ReadLinesAsync(
string filePath,
Encoding? encoding = null,
[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
{
encoding ??= Encoding.UTF8;

await using var reader = new StreamingFileReader(filePath);
await foreach (var line in reader.ReadLinesAsync(encoding, cancellationToken))
{
yield return line;
}
}

/// <summary>
/// Copy file using optimal strategy.
/// For large files (>10MB), uses RandomAccess API with parallel chunks.
/// For typical files, uses standard File.Copy.
/// </summary>
public static async Task CopyFileAsync(
string sourcePath,
string destPath,
bool overwrite = false,
CancellationToken cancellationToken = default)
{
// Check file size to determine strategy
long fileSize;
try
{
fileSize = new FileInfo(sourcePath).Length;
}
catch
{
// Fallback to standard copy
File.Copy(sourcePath, destPath, overwrite);
return;
}

// Small/Medium files: use standard API
if (fileSize < 10 * 1024 * 1024) // <10MB
{
File.Copy(sourcePath, destPath, overwrite);
return;
}

// Large files: use RandomAccess parallel read/write
await using var reader = new RandomAccessReader(sourcePath);
await using var writer = new RandomAccessWriter(destPath, fileSize);

const int chunkSize = 4 * 1024 * 1024; // 4 MB chunks
var chunks = (int)Math.Ceiling((double)fileSize / chunkSize);

var regions = Enumerable.Range(0, chunks)
.Select(i => (
Offset: (long)i * chunkSize,
Length: (int)Math.Min(chunkSize, fileSize - (long)i * chunkSize)
));

// Read all chunks in parallel
var dataChunks = await reader.ReadParallelAsync(regions, cancellationToken);

// Write all chunks in parallel
var writeRegions = dataChunks.Select((data, i) => (
Offset: (long)i * chunkSize,
Data: data
));

await writer.WriteParallelAsync(writeRegions, cancellationToken);
writer.Flush();
}
}

using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;
using Xunit;

namespace UltrasharpTools.Test.Common;

public class MemoryMappedFileTests : IDisposable
{
private readonly string _testDirectory;

public MemoryMappedFileTests()
{
_testDirectory = Path.Combine(Path.GetTempPath(), $"mmf_tests_{Guid.NewGuid():N}");
Directory.CreateDirectory(_testDirectory);
}

public void Dispose()
{
if (Directory.Exists(_testDirectory))
{
try
{
Directory.Delete(_testDirectory, recursive: true);
}
catch
{
// Best effort cleanup
}
}
}

[Fact]
public void MemoryMappedFileWriter_WriteAndRead_Structure()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_struct.bin");
var testStruct = new TestStruct { Id = 42, Value = 3.14159 };

// Act - Write
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
writer.Write(0, ref testStruct);
writer.Flush();
}

// Act - Read
using (var reader = new MemoryMappedFileReader(filePath))
{
reader.Read<TestStruct>(0, out var result);

// Assert
Assert.Equal(testStruct.Id, result.Id);
Assert.Equal(testStruct.Value, result.Value);
}
}

[Fact]
public void MemoryMappedFileWriter_WriteAndRead_Array()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_array.bin");
var testArray = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

// Act - Write
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
writer.WriteArray(0, testArray, 0, testArray.Length);
writer.Flush();
}

// Act - Read
using (var reader = new MemoryMappedFileReader(filePath))
{
var result = reader.ReadArray<int>(0, testArray.Length);

// Assert
Assert.Equal(testArray, result);
}
}

[Fact]
public void MemoryMappedFileWriter_WriteAndRead_Bytes()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_bytes.bin");
var testData = System.Text.Encoding.UTF8.GetBytes("Hello, Memory-Mapped Files!");

// Act - Write
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
writer.WriteBytes(0, testData, 0, testData.Length);
writer.Flush();
}

// Act - Read
using (var reader = new MemoryMappedFileReader(filePath))
{
var buffer = new byte[testData.Length];
int bytesRead = reader.ReadBytes(0, buffer, 0, testData.Length);

// Assert
Assert.Equal(testData.Length, bytesRead);
Assert.Equal(testData, buffer);
}
}

[Fact]
public void MemoryMappedFileWriter_WriteAndRead_String()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_string.bin");
var testString = "Привет, мир!";

// Act - Write
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
writer.WriteString(0, testString, 256);
writer.Flush();
}

// Act - Read
using (var reader = new MemoryMappedFileReader(filePath))
{
var result = reader.ReadString(0, 256);

// Assert
Assert.Equal(testString, result);
}
}

[Fact]
public unsafe void MemoryMappedFileWriter_GetSpan_ZeroCopyWrite()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_span.bin");
var testData = new byte[] { 1, 2, 3, 4, 5 };

// Act - Write via span
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
var span = writer.GetSpan(0, testData.Length);
testData.CopyTo(span);
writer.Flush();
}

// Act - Read via span
using (var reader = new MemoryMappedFileReader(filePath))
{
var span = reader.GetSpan(0, testData.Length);

// Assert
Assert.True(span.SequenceEqual(testData));
}
}

[Fact]
public void MemoryMappedFileWriter_MultipleWrites_AtDifferentPositions()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_multiple.bin");
var data1 = new byte[] { 1, 2, 3 };
var data2 = new byte[] { 4, 5, 6 };
var data3 = new byte[] { 7, 8, 9 };

// Act - Write at different positions
using (var writer = new MemoryMappedFileWriter(filePath, 1024))
{
writer.WriteBytes(0, data1, 0, data1.Length);
writer.WriteBytes(100, data2, 0, data2.Length);
writer.WriteBytes(500, data3, 0, data3.Length);
writer.Flush();
}

// Act - Read back
using (var reader = new MemoryMappedFileReader(filePath))
{
var buffer1 = new byte[3];
var buffer2 = new byte[3];
var buffer3 = new byte[3];

reader.ReadBytes(0, buffer1, 0, 3);
reader.ReadBytes(100, buffer2, 0, 3);
reader.ReadBytes(500, buffer3, 0, 3);

// Assert
Assert.Equal(data1, buffer1);
Assert.Equal(data2, buffer2);
Assert.Equal(data3, buffer3);
}
}

[Fact]
public void MemoryMappedFileReader_Length_ReturnsCorrectValue()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_length.bin");
var testData = new byte[2048];

using (var writer = new MemoryMappedFileWriter(filePath, testData.Length))
{
writer.WriteBytes(0, testData, 0, testData.Length);
writer.Flush();
}

// Act
using (var reader = new MemoryMappedFileReader(filePath))
{
// Assert
Assert.Equal(testData.Length, reader.Length);
}
}

[Fact]
public void MemoryMappedFileWriter_Capacity_ReturnsCorrectValue()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_capacity.bin");
long expectedCapacity = 4096;

// Act
using (var writer = new MemoryMappedFileWriter(filePath, expectedCapacity))
{
// Assert
Assert.Equal(expectedCapacity, writer.Capacity);
}
}

[Fact]
public void MemoryMappedFileReader_GetSpan_ThrowsOnInvalidOffset()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_invalid.bin");
var testData = new byte[100];

using (var writer = new MemoryMappedFileWriter(filePath, testData.Length))
{
writer.WriteBytes(0, testData, 0, testData.Length);
writer.Flush();
}

// Act & Assert
using (var reader = new MemoryMappedFileReader(filePath))
{
Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetSpan(-1, 10));
Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetSpan(200, 10));
Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetSpan(0, -1));
Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetSpan(0, 1000));
}
}

[Fact]
public void MemoryMappedFileWriter_Dispose_ThrowsOnUse()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_dispose.bin");
var writer = new MemoryMappedFileWriter(filePath, 1024);
var testStruct = new TestStruct { Id = 1, Value = 2.0 };

// Act
writer.Dispose();

// Assert
Assert.Throws<ObjectDisposedException>(() => writer.Write(0, ref testStruct));
Assert.Throws<ObjectDisposedException>(() => writer.Flush());
}

[Fact]
public void MemoryMappedFileReader_Dispose_ThrowsOnUse()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_dispose_reader.bin");
var testData = new byte[100];

using (var writer = new MemoryMappedFileWriter(filePath, testData.Length))
{
writer.WriteBytes(0, testData, 0, testData.Length);
writer.Flush();
}

var reader = new MemoryMappedFileReader(filePath);

// Act
reader.Dispose();

// Assert
Assert.Throws<ObjectDisposedException>(() => reader.Read<int>(0, out _));
Assert.Throws<ObjectDisposedException>(() => reader.GetSpan(0, 10));
}

[Fact]
public void MemoryMappedFiles_LargeFile_Performance()
{
// Arrange
var filePath = Path.Combine(_testDirectory, "test_large.bin");
const int recordSize = 16;
const int recordCount = 100_000; // 1.6 MB
var capacity = recordSize * recordCount;

// Act - Write
using (var writer = new MemoryMappedFileWriter(filePath, capacity))
{
for (int i = 0; i < recordCount; i++)
{
var record = new TestStruct { Id = i, Value = i * 3.14 };
writer.Write(i * recordSize, ref record);
}
writer.Flush();
}

// Act - Read random access (should be instant)
using (var reader = new MemoryMappedFileReader(filePath))
{
// Read first record
reader.Read<TestStruct>(0, out var first);
Assert.Equal(0, first.Id);

// Read middle record
reader.Read<TestStruct>(50_000 * recordSize, out var middle);
Assert.Equal(50_000, middle.Id);

// Read last record
reader.Read<TestStruct>((recordCount - 1) * recordSize, out var last);
Assert.Equal(recordCount - 1, last.Id);
}
}

private struct TestStruct
{
public int Id;
public double Value;
}
}

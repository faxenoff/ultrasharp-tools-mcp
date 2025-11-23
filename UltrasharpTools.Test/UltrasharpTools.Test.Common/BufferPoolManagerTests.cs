using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;
using Xunit;

namespace UltrasharpTools.Test.Common;

/// <summary>
/// Tests for BufferPoolManager to verify correct buffer rental and return.
/// </summary>
public class BufferPoolManagerTests
{
    [Fact]
    public void RentBytes_ReturnsBufferOfCorrectSize()
    {
        // Arrange & Act
        var buffer = BufferPoolManager.RentBytes(1024);

        try
        {
            // Assert
            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= 1024, $"Buffer length {buffer.Length} should be >= 1024");
        }
        finally
        {
            // Cleanup
            BufferPoolManager.ReturnBytes(buffer);
        }
    }

    [Fact]
    public void RentChars_ReturnsBufferOfCorrectSize()
    {
        // Arrange & Act
        var buffer = BufferPoolManager.RentChars(512);

        try
        {
            // Assert
            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= 512, $"Buffer length {buffer.Length} should be >= 512");
        }
        finally
        {
            // Cleanup
            BufferPoolManager.ReturnChars(buffer);
        }
    }

    [Fact]
    public void RentBytesDisposable_AutomaticallyReturnsBuffer()
    {
        // Arrange
        byte firstByte;

        // Act
        using (var buffer = BufferPoolManager.RentBytesDisposable(1024))
        {
            Assert.Equal(1024, buffer.Length);
            Assert.True(buffer.Array.Length >= 1024);

            buffer.Span[0] = 42;
            firstByte = buffer.Span[0];
        }

        // Assert
        Assert.Equal(42, firstByte);
        // Buffer automatically returned to pool
    }

    [Fact]
    public void RentCharsDisposable_AutomaticallyReturnsBuffer()
    {
        // Arrange
        char firstChar;

        // Act
        using (var buffer = BufferPoolManager.RentCharsDisposable(512))
        {
            Assert.Equal(512, buffer.Length);
            Assert.True(buffer.Array.Length >= 512);

            buffer.Span[0] = 'A';
            firstChar = buffer.Span[0];
        }

        // Assert
        Assert.Equal('A', firstChar);
        // Buffer automatically returned to pool
    }

    [Fact]
    public void PooledBuffer_SpanAccessWorks()
    {
        // Arrange & Act
        using var buffer = BufferPoolManager.RentBytesDisposable(256);

        // Assert
        Assert.Equal(256, buffer.Span.Length);

        // Can write to span
        buffer.Span[0] = 1;
        buffer.Span[1] = 2;
        buffer.Span[255] = 255;

        Assert.Equal(1, buffer.Span[0]);
        Assert.Equal(2, buffer.Span[1]);
        Assert.Equal(255, buffer.Span[255]);
    }

    [Fact]
    public void PooledBuffer_MemoryAccessWorks()
    {
        // Arrange & Act
        using var buffer = BufferPoolManager.RentCharsDisposable(128);

        // Assert
        Assert.Equal(128, buffer.Memory.Length);

        // Can write to memory
        buffer.Memory.Span[0] = 'H';
        buffer.Memory.Span[1] = 'i';

        Assert.Equal('H', buffer.Memory.Span[0]);
        Assert.Equal('i', buffer.Memory.Span[1]);
    }

    [Fact]
    public void PooledBuffer_ThrowsAfterDispose()
    {
        // Arrange
        var buffer = BufferPoolManager.RentBytesDisposable(64);
        buffer.Dispose();

        // Act & Assert - ref struct cannot be used in lambda, so test manually
        bool spanThrows = false;
        bool memoryThrows = false;
        bool arrayThrows = false;

        try { var _ = buffer.Span; } catch (ObjectDisposedException) { spanThrows = true; }
        try { var _ = buffer.Memory; } catch (ObjectDisposedException) { memoryThrows = true; }
        try { var _ = buffer.Array; } catch (ObjectDisposedException) { arrayThrows = true; }

        Assert.True(spanThrows, "Accessing Span after dispose should throw ObjectDisposedException");
        Assert.True(memoryThrows, "Accessing Memory after dispose should throw ObjectDisposedException");
        Assert.True(arrayThrows, "Accessing Array after dispose should throw ObjectDisposedException");
    }

    [Fact]
    public void RentReturn_RepeatedOperations_NoMemoryLeak()
    {
        // Arrange
        const int iterations = 10000;

        // Act - rent and return many times
        for (int i = 0; i < iterations; i++)
        {
            var buffer = BufferPoolManager.RentBytes(8192);
            buffer[0] = (byte)(i % 256);
            BufferPoolManager.ReturnBytes(buffer);
        }

        // Force GC to collect any leaked objects
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Another round
        for (int i = 0; i < iterations; i++)
        {
            var buffer = BufferPoolManager.RentBytes(8192);
            buffer[0] = (byte)(i % 256);
            BufferPoolManager.ReturnBytes(buffer);
        }

        var memoryAfter = GC.GetTotalMemory(false);

        // Assert - memory should not grow significantly
        var growth = memoryAfter - memoryBefore;
        Assert.True(growth < 1_000_000, $"Memory grew by {growth / 1024}KB, expected < 1MB");
    }

    [Fact]
    public void RentBytesDisposable_RepeatedOperations_NoMemoryLeak()
    {
        // Arrange
        const int iterations = 10000;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using var buffer = BufferPoolManager.RentBytesDisposable(4096);
            buffer.Span[0] = (byte)(i % 256);
        }

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Another round
        for (int i = 0; i < iterations; i++)
        {
            using var buffer = BufferPoolManager.RentBytesDisposable(4096);
            buffer.Span[0] = (byte)(i % 256);
        }

        var memoryAfter = GC.GetTotalMemory(false);

        // Assert
        var growth = memoryAfter - memoryBefore;
        Assert.True(growth < 500_000, $"Memory grew by {growth / 1024}KB, expected < 500KB");
    }

    [Fact]
    public void RentReturn_DifferentSizes_WorksCorrectly()
    {
        // Arrange
        var sizes = new[] { 64, 256, 1024, 4096, 16384, 65536 };

        // Act & Assert
        foreach (var size in sizes)
        {
            var buffer = BufferPoolManager.RentBytes(size);
            Assert.True(buffer.Length >= size, $"Buffer for size {size} has length {buffer.Length}");
            BufferPoolManager.ReturnBytes(buffer);
        }
    }

    [Fact]
    public void ReturnBytes_WithClearArray_ClearsBuffer()
    {
        // Arrange
        var buffer = BufferPoolManager.RentBytes(128);
        buffer[0] = 42;
        buffer[127] = 99;

        // Act
        BufferPoolManager.ReturnBytes(buffer, clearArray: true);

        // Rent again (might get same buffer)
        var buffer2 = BufferPoolManager.RentBytes(128);

        try
        {
            // Assert - if we got the same buffer, it should be cleared
            // Note: we might not get the same buffer, so this test is probabilistic
            if (ReferenceEquals(buffer, buffer2))
            {
                Assert.Equal(0, buffer2[0]);
                Assert.Equal(0, buffer2[127]);
            }
        }
        finally
        {
            BufferPoolManager.ReturnBytes(buffer2);
        }
    }
}

using System.Buffers;
using System.IO.Hashing;

using System.Security.Cryptography;

namespace UltrasharpTools.Tools.Infrastructure;
/// <summary>
/// Fast hashing utilities optimized for performance using Span&lt;T&gt; to minimize allocations.
/// Uses stackalloc for small strings (&lt;1KB) and ArrayPool for larger strings.
/// </summary>
public static class FastHash
{
    private const int StackAllocThreshold = 1024; // Use stackalloc for strings producing <1KB of UTF8 bytes

    /// <summary>
    /// Fast non-cryptographic hash for cache keys using xxHash3.
    /// 10x faster than SHA256 (~0.8μs vs ~8μs for 1KB data).
    /// Optimized with Span&lt;T&gt; to avoid heap allocations for small strings.
    /// Suitable for: cache keys, in-memory lookups, non-security-critical hashing.
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>Hex string representation of the hash (64-bit)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ComputeHash(string input)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            // Use stackalloc for small strings - zero heap allocations
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), buffer);
            var hashBytes = XxHash3.Hash(buffer.Slice(0, actualByteCount));
            return Convert.ToHexString(hashBytes);
        }
        else
        {
            // Use ArrayPool for large strings
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), rented.AsSpan());
                var hashBytes = XxHash3.Hash(rented.AsSpan(0, actualByteCount));
                return Convert.ToHexString(hashBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Fast non-cryptographic hash with 128-bit output using xxHash128.
    /// Lower collision rate than xxHash3 while still being ~7x faster than SHA256.
    /// Optimized with Span&lt;T&gt; to avoid heap allocations for small strings.
    /// Suitable for: cache keys with lower collision tolerance.
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>Hex string representation of the hash (128-bit)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ComputeHash128(string input)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), buffer);
            var hashBytes = XxHash128.Hash(buffer.Slice(0, actualByteCount));
            return Convert.ToHexString(hashBytes);
        }
        else
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), rented.AsSpan());
                var hashBytes = XxHash128.Hash(rented.AsSpan(0, actualByteCount));
                return Convert.ToHexString(hashBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Cryptographic hash for file integrity using SHA256.
    /// USE ONLY when security/integrity verification is required.
    /// Suitable for: file tampering detection, solution/project file hashes for cache validation.
    /// </summary>
    /// <param name="stream">Stream to hash</param>
    /// <returns>Hex string representation of the hash (256-bit)</returns>
    public static string ComputeCryptoHash(Stream stream)
    {
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Cryptographic hash for strings using SHA256.
    /// USE ONLY when security is required (rare for our use cases).
    /// Prefer ComputeHash() for non-security-critical hashing.
    /// Optimized with Span&lt;T&gt; to avoid heap allocations for small strings.
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>Hex string representation of the hash (256-bit)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ComputeCryptoHash(string input)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), buffer);
            var hashBytes = SHA256.HashData(buffer.Slice(0, actualByteCount));
            return Convert.ToHexString(hashBytes);
        }
        else
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), rented.AsSpan());
                var hashBytes = SHA256.HashData(rented.AsSpan(0, actualByteCount));
                return Convert.ToHexString(hashBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Compute xxHash32 for 32-bit hash codes (useful for Dictionary/HashSet keys).
    /// Optimized with Span&lt;T&gt; to avoid heap allocations for small strings.
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>32-bit hash code</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeHash32(string input)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(input.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), buffer);
            return unchecked((int)XxHash32.HashToUInt32(buffer.Slice(0, actualByteCount)));
        }
        else
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                int actualByteCount = Encoding.UTF8.GetBytes(input.AsSpan(), rented.AsSpan());
                return unchecked((int)XxHash32.HashToUInt32(rented.AsSpan(0, actualByteCount)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
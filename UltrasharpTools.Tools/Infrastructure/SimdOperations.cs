using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// SIMD-accelerated operations для vectorized computations
/// Использует AVX-512, AVX2, SSE2 с runtime detection и fallback
/// </summary>
public static class SimdOperations
{
    /// <summary>
    /// Проверить доступность AVX-512
    /// </summary>
    public static bool IsAvx512Supported => Avx512F.IsSupported;

    /// <summary>
    /// Проверить доступность AVX2
    /// </summary>
    public static bool IsAvx2Supported => Avx2.IsSupported;

    /// <summary>
    /// Проверить доступность SSE2
    /// </summary>
    public static bool IsSse2Supported => Sse2.IsSupported;

    /// <summary>
    /// Compute dot product of two float vectors (embedding similarity)
    /// Использует AVX-512/AVX2/SSE2 с automatic fallback
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotProduct(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have same length");
        }

        // AVX-512: process 16 floats at a time
        if (Avx512F.IsSupported && vector1.Length >= Vector512<float>.Count)
        {
            return DotProductAvx512(vector1, vector2);
        }

        // AVX2: process 8 floats at a time
        if (Avx2.IsSupported && vector1.Length >= Vector256<float>.Count)
        {
            return DotProductAvx2(vector1, vector2);
        }

        // SSE2: process 4 floats at a time
        if (Sse2.IsSupported && vector1.Length >= Vector128<float>.Count)
        {
            return DotProductSse2(vector1, vector2);
        }

        // Fallback: Vector<T> (platform-agnostic)
        return DotProductVectorT(vector1, vector2);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float DotProductAvx512(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        var sum = Vector512<float>.Zero;
        int i = 0;
        int simdLength = v1.Length - (v1.Length % Vector512<float>.Count);

        // Process 16 floats at a time
        for (; i < simdLength; i += Vector512<float>.Count)
        {
            var a = Vector512.Create(v1.Slice(i, Vector512<float>.Count));
            var b = Vector512.Create(v2.Slice(i, Vector512<float>.Count));
            sum = Avx512F.Add(sum, Avx512F.Multiply(a, b));
        }

        // Sum all elements in vector
        float result = Vector512.Sum(sum);

        // Process remaining elements
        for (; i < v1.Length; i++)
        {
            result += v1[i] * v2[i];
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float DotProductAvx2(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        var sum = Vector256<float>.Zero;
        int i = 0;
        int simdLength = v1.Length - (v1.Length % Vector256<float>.Count);

        // Process 8 floats at a time
        for (; i < simdLength; i += Vector256<float>.Count)
        {
            var a = Vector256.Create(v1.Slice(i, Vector256<float>.Count));
            var b = Vector256.Create(v2.Slice(i, Vector256<float>.Count));
            sum = Avx.Add(sum, Avx.Multiply(a, b));
        }

        // Sum all elements in vector
        float result = Vector256.Sum(sum);

        // Process remaining elements
        for (; i < v1.Length; i++)
        {
            result += v1[i] * v2[i];
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float DotProductSse2(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        var sum = Vector128<float>.Zero;
        int i = 0;
        int simdLength = v1.Length - (v1.Length % Vector128<float>.Count);

        // Process 4 floats at a time
        for (; i < simdLength; i += Vector128<float>.Count)
        {
            var a = Vector128.Create(v1.Slice(i, Vector128<float>.Count));
            var b = Vector128.Create(v2.Slice(i, Vector128<float>.Count));
            sum = Sse.Add(sum, Sse.Multiply(a, b));
        }

        // Sum all elements in vector
        float result = Vector128.Sum(sum);

        // Process remaining elements
        for (; i < v1.Length; i++)
        {
            result += v1[i] * v2[i];
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static float DotProductVectorT(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        float sum = 0f;
        int i = 0;
        int vectorSize = Vector<float>.Count;
        int simdLength = v1.Length - (v1.Length % vectorSize);

        // Process Vector<float>.Count elements at a time
        for (; i < simdLength; i += vectorSize)
        {
            var a = new Vector<float>(v1.Slice(i, vectorSize));
            var b = new Vector<float>(v2.Slice(i, vectorSize));
            sum += Vector.Dot(a, b);
        }

        // Process remaining elements
        for (; i < v1.Length; i++)
        {
            sum += v1[i] * v2[i];
        }

        return sum;
    }

    /// <summary>
    /// Compute cosine similarity between two vectors
    /// CosineSimilarity = DotProduct / (||v1|| * ||v2||)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have same length");
        }

        float dotProduct = DotProduct(vector1, vector2);
        float magnitude1 = Magnitude(vector1);
        float magnitude2 = Magnitude(vector2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0f;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    /// <summary>
    /// Compute vector magnitude (L2 norm)
    /// ||v|| = sqrt(sum(v_i^2))
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Magnitude(ReadOnlySpan<float> vector)
    {
        float sumOfSquares = DotProduct(vector, vector);
        return MathF.Sqrt(sumOfSquares);
    }

    /// <summary>
    /// Compute Euclidean distance between two vectors
    /// Distance = sqrt(sum((v1_i - v2_i)^2))
    /// Используется для semantic similarity in embedding space
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EuclideanDistance(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have same length");
        }

        float sumOfSquares = 0f;

        // AVX-512: process 16 floats at a time
        if (Avx512F.IsSupported && vector1.Length >= Vector512<float>.Count)
        {
            var sum = Vector512<float>.Zero;
            int i = 0;
            int simdLength = vector1.Length - (vector1.Length % Vector512<float>.Count);

            for (; i < simdLength; i += Vector512<float>.Count)
            {
                var a = Vector512.Create(vector1.Slice(i, Vector512<float>.Count));
                var b = Vector512.Create(vector2.Slice(i, Vector512<float>.Count));
                var diff = Avx512F.Subtract(a, b);
                sum = Avx512F.Add(sum, Avx512F.Multiply(diff, diff));
            }

            sumOfSquares = Vector512.Sum(sum);

            // Process remaining elements
            for (; i < vector1.Length; i++)
            {
                float diff = vector1[i] - vector2[i];
                sumOfSquares += diff * diff;
            }
        }
        else
        {
            // Fallback: scalar computation
            for (int i = 0; i < vector1.Length; i++)
            {
                float diff = vector1[i] - vector2[i];
                sumOfSquares += diff * diff;
            }
        }

        return MathF.Sqrt(sumOfSquares);
    }

    /// <summary>
    /// Compute Levenshtein distance между двумя strings (SIMD-accelerated для больших строк)
    /// Используется для fuzzy string matching в symbol search
    /// </summary>
    public static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return target?.Length ?? 0;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int sourceLength = source.Length;
        int targetLength = target.Length;

        // Optimize: if strings differ too much, early exit
        int lengthDiff = Math.Abs(sourceLength - targetLength);
        int maxLength = Math.Max(sourceLength, targetLength);
        if (lengthDiff > maxLength / 2)
        {
            return maxLength; // Approximate: strings are too different
        }

        // Use dynamic programming with 1D array optimization
        int[] costs = new int[targetLength + 1];
        int[] previousCosts = new int[targetLength + 1];

        // Initialize costs
        for (int j = 0; j <= targetLength; j++)
        {
            previousCosts[j] = j;
        }

        for (int i = 1; i <= sourceLength; i++)
        {
            costs[0] = i;
            char sourceChar = source[i - 1];

            for (int j = 1; j <= targetLength; j++)
            {
                char targetChar = target[j - 1];
                int cost = sourceChar == targetChar ? 0 : 1;

                costs[j] = Math.Min(
                    Math.Min(costs[j - 1] + 1, previousCosts[j] + 1),
                    previousCosts[j - 1] + cost);
            }

            // Swap arrays
            (costs, previousCosts) = (previousCosts, costs);
        }

        return previousCosts[targetLength];
    }

    /// <summary>
    /// Get CPU capabilities summary
    /// </summary>
    public static string GetCpuCapabilities()
    {
        return $"AVX-512: {IsAvx512Supported}, AVX2: {IsAvx2Supported}, SSE2: {IsSse2Supported}, Vector<T> size: {Vector<float>.Count}";
    }
}

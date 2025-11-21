using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for SIMD-optimized cosine similarity calculation.
/// Compares scalar vs SIMD performance for different vector sizes.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class SIMDBenchmarks
{
    private Dictionary<string, int> _smallVec1 = null!;
    private Dictionary<string, int> _smallVec2 = null!;
    private Dictionary<string, int> _mediumVec1 = null!;
    private Dictionary<string, int> _mediumVec2 = null!;
    private Dictionary<string, int> _largeVec1 = null!;
    private Dictionary<string, int> _largeVec2 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small vectors (10 elements) - below SIMD threshold
        _smallVec1 = CreateVector(10);
        _smallVec2 = CreateVector(10);

        // Medium vectors (50 elements) - SIMD beneficial
        _mediumVec1 = CreateVector(50);
        _mediumVec2 = CreateVector(50);

        // Large vectors (200 elements) - SIMD highly beneficial
        _largeVec1 = CreateVector(200);
        _largeVec2 = CreateVector(200);
    }

    private Dictionary<string, int> CreateVector(int size)
    {
        var dict = new Dictionary<string, int>();
        for (int i = 0; i < size; i++)
        {
            dict[$"Operation{i}"] = Random.Shared.Next(1, 10);
        }
        return dict;
    }

    [Benchmark(Baseline = true)]
    public double CosineSimilarity_Small_Scalar()
    {
        return CalculateCosineSimilarityScalar(_smallVec1, _smallVec2);
    }

    [Benchmark]
    public double CosineSimilarity_Medium_SIMD()
    {
        return CalculateCosineSimilaritySIMD(_mediumVec1, _mediumVec2);
    }

    [Benchmark]
    public double CosineSimilarity_Large_SIMD()
    {
        return CalculateCosineSimilaritySIMD(_largeVec1, _largeVec2);
    }

    // Scalar implementation for comparison
    private double CalculateCosineSimilarityScalar(
        Dictionary<string, int> vec1,
        Dictionary<string, int> vec2
    )
    {
        if (vec1.Count() == 0 && vec2.Count() == 0)
            return 1.0;
        if (vec1.Count() == 0 || vec2.Count() == 0)
            return 0.0;

        var allKeys = vec1.Keys.Union(vec2.Keys);

        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;

        foreach (var key in allKeys)
        {
            int val1 = vec1.GetValueOrDefault(key, 0);
            int val2 = vec2.GetValueOrDefault(key, 0);

            dotProduct += val1 * val2;
            magnitude1 += val1 * val1;
            magnitude2 += val2 * val2;
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0.0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    // SIMD implementation (simplified version of actual implementation)
    private double CalculateCosineSimilaritySIMD(
        Dictionary<string, int> vec1,
        Dictionary<string, int> vec2
    )
    {
        if (vec1.Count() == 0 && vec2.Count() == 0)
            return 1.0;
        if (vec1.Count() == 0 || vec2.Count() == 0)
            return 0.0;

        var allKeys = vec1.Keys.Union(vec2.Keys).ToArray();
        int count = allKeys.Length;

        var values1 = new int[count];
        var values2 = new int[count];

        for (int j = 0; j < count; j++)
        {
            values1[j] = vec1.GetValueOrDefault(allKeys[j], 0);
            values2[j] = vec2.GetValueOrDefault(allKeys[j], 0);
        }

        // SIMD computation using Vector<int>
        int vectorSize = System.Numerics.Vector<int>.Count;
        long dotProduct = 0;
        long magnitude1Squared = 0;
        long magnitude2Squared = 0;

        int i = 0;
        for (; i <= count - vectorSize; i += vectorSize)
        {
            var v1 = new System.Numerics.Vector<int>(values1, i);
            var v2 = new System.Numerics.Vector<int>(values2, i);

            dotProduct += System.Numerics.Vector.Dot(v1, v2);
            magnitude1Squared += System.Numerics.Vector.Dot(v1, v1);
            magnitude2Squared += System.Numerics.Vector.Dot(v2, v2);
        }

        // Scalar remainder
        for (; i < count; i++)
        {
            int val1 = values1[i];
            int val2 = values2[i];

            dotProduct += (long)val1 * val2;
            magnitude1Squared += (long)val1 * val1;
            magnitude2Squared += (long)val2 * val2;
        }

        double magnitude1 = Math.Sqrt(magnitude1Squared);
        double magnitude2 = Math.Sqrt(magnitude2Squared);

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0.0;

        return dotProduct / (magnitude1 * magnitude2);
    }
}

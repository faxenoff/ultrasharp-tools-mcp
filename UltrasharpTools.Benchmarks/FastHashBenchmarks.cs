using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for FastHash Span&lt;T&gt; optimizations.
/// Measures the performance improvement from using stackalloc + ArrayPool vs Encoding.UTF8.GetBytes.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class FastHashBenchmarks
{
    private string _shortString = null!;
    private string _mediumString = null!;
    private string _longString = null!;
    private string _veryLongString = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 10 chars - should use stackalloc
        _shortString = "MyClass.MyMethod";

        // 100 chars - should use stackalloc
        _mediumString = new string('a', 100);

        // 500 chars - at threshold, should use stackalloc
        _longString = new string('b', 500);

        // 2000 chars - should use ArrayPool
        _veryLongString = new string('c', 2000);
    }

    [Benchmark(Baseline = true)]
    public string ComputeHash_Short() => FastHash.ComputeHash(_shortString);

    [Benchmark]
    public string ComputeHash_Medium() => FastHash.ComputeHash(_mediumString);

    [Benchmark]
    public string ComputeHash_Long() => FastHash.ComputeHash(_longString);

    [Benchmark]
    public string ComputeHash_VeryLong() => FastHash.ComputeHash(_veryLongString);

    [Benchmark]
    public string ComputeHash128_Short() => FastHash.ComputeHash128(_shortString);

    [Benchmark]
    public string ComputeHash128_Medium() => FastHash.ComputeHash128(_mediumString);

    [Benchmark]
    public int ComputeHash32_Short() => FastHash.ComputeHash32(_shortString);

    [Benchmark]
    public int ComputeHash32_Medium() => FastHash.ComputeHash32(_mediumString);
}

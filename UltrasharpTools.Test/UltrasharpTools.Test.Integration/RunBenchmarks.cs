using BenchmarkDotNet.Running;

namespace UltrasharpTools.Test.Integration;

/// <summary>
/// Entry point для запуска бенчмарков
/// ВАЖНО: Запускать ТОЛЬКО через xUnit Fact, НЕ как standalone exe
/// </summary>
public class BenchmarkRunner
{
    /// <summary>
    /// Тест для запуска бенчмарков вручную
    /// Раскомментируйте [Fact] для запуска через test runner
    /// </summary>
    // [Fact]
    public void RunAllBenchmarks()
    {
        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<EnrichmentBenchmarks>();
        Console.WriteLine($"\n=== Benchmark Summary ===");
        Console.WriteLine($"Total benchmarks: {summary.Reports.Length}");
        Console.WriteLine($"Results saved to: {summary.ResultsDirectoryPath}");
    }
}

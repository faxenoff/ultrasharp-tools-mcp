using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for LogAnalysis streaming optimizations.
/// Compares memory usage and performance between full-file loading vs streaming approaches.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class LogAnalysisBenchmarks
{
    private string _smallLogPath = null!;
    private string _mediumLogPath = null!;
    private string _largeLogPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks");
        Directory.CreateDirectory(tempDir);

        // Small log: 1000 lines (~50KB)
        _smallLogPath = Path.Combine(tempDir, "small.log");
        GenerateLogFile(_smallLogPath, 1000);

        // Medium log: 10,000 lines (~500KB)
        _mediumLogPath = Path.Combine(tempDir, "medium.log");
        GenerateLogFile(_mediumLogPath, 10000);

        // Large log: 100,000 lines (~5MB)
        _largeLogPath = Path.Combine(tempDir, "large.log");
        GenerateLogFile(_largeLogPath, 100000);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks");
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private void GenerateLogFile(string path, int lineCount)
    {
        using var writer = new StreamWriter(path);
        var random = new Random(42);

        for (int i = 0; i < lineCount; i++)
        {
            var level = random.Next(100) switch
            {
                < 5 => "ERROR",
                < 15 => "WARN",
                < 30 => "INFO",
                _ => "DEBUG",
            };

            var timestamp = DateTime
                .UtcNow.AddSeconds(-lineCount + i)
                .ToString("yyyy-MM-dd HH:mm:ss.fff");
            var message = $"Operation{random.Next(100)} completed";

            writer.WriteLine($"{timestamp} [{level}] {message}");

            // Добавляем иногда stacktrace для ERROR
            if (level == "ERROR" && random.Next(10) < 3)
            {
                writer.WriteLine($"   at System.Example.Method{random.Next(10)}()");
                writer.WriteLine($"   at System.Example.Method{random.Next(10)}()");
            }
        }
    }

    // Baseline: старый подход - загрузка всего файла в память
    [Benchmark(Baseline = true)]
    public List<string> LoadAll_Small()
    {
        return File.ReadAllLines(_smallLogPath)
            .Where(line => line.Contains("ERROR"))
            .Take(10)
            .ToList();
    }

    [Benchmark]
    public List<string> LoadAll_Medium()
    {
        return File.ReadAllLines(_mediumLogPath)
            .Where(line => line.Contains("ERROR"))
            .Take(10)
            .ToList();
    }

    [Benchmark]
    public List<string> LoadAll_Large()
    {
        return File.ReadAllLines(_largeLogPath)
            .Where(line => line.Contains("ERROR"))
            .Take(10)
            .ToList();
    }

    // Streaming approach: O(1) memory
    [Benchmark]
    public async Task<List<string>> Streaming_Small()
    {
        return await StreamingReadAsync(_smallLogPath, "ERROR", take: 10);
    }

    [Benchmark]
    public async Task<List<string>> Streaming_Medium()
    {
        return await StreamingReadAsync(_mediumLogPath, "ERROR", take: 10);
    }

    [Benchmark]
    public async Task<List<string>> Streaming_Large()
    {
        return await StreamingReadAsync(_largeLogPath, "ERROR", take: 10);
    }

    // Sliding window approach: O(window_size) memory
    [Benchmark]
    public async Task<List<string>> SlidingWindow_Small()
    {
        return await SlidingWindowReadAsync(_smallLogPath, "ERROR", take: 10, contextLines: 2);
    }

    [Benchmark]
    public async Task<List<string>> SlidingWindow_Medium()
    {
        return await SlidingWindowReadAsync(_mediumLogPath, "ERROR", take: 10, contextLines: 2);
    }

    [Benchmark]
    public async Task<List<string>> SlidingWindow_Large()
    {
        return await SlidingWindowReadAsync(_largeLogPath, "ERROR", take: 10, contextLines: 2);
    }

    private async Task<List<string>> StreamingReadAsync(string filePath, string keyword, int take)
    {
        var results = new List<string>();
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920
        );
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(line);
                if (results.Count >= take)
                {
                    break;
                }
            }
        }

        return results;
    }

    private async Task<List<string>> SlidingWindowReadAsync(
        string filePath,
        string keyword,
        int take,
        int contextLines
    )
    {
        var results = new List<string>();
        var window = new Queue<string>(contextLines * 2 + 1);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920
        );
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (window.Count >= contextLines * 2 + 1)
            {
                window.Dequeue();
            }
            window.Enqueue(line);

            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(line);
                if (results.Count >= take)
                {
                    break;
                }
            }
        }

        return results;
    }
}

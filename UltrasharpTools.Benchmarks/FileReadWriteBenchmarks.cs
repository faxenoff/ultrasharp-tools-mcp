using BenchmarkDotNet.Attributes;
using System.Text;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Baseline benchmarks for file read/write operations.
/// Measures current performance before high-performance optimizations.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class FileReadWriteBenchmarks
{
    private string _file1KB = null!;
    private string _file10KB = null!;
    private string _file100KB = null!;
    private string _file1MB = null!;
    private string _file10MB = null!;
    private string _outputDir = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks_FileIO");
        Directory.CreateDirectory(tempDir);
        _outputDir = tempDir;

        // Create test files of various sizes
        _file1KB = Path.Combine(tempDir, "test_1kb.txt");
        CreateTestFile(_file1KB, 1 * 1024);

        _file10KB = Path.Combine(tempDir, "test_10kb.txt");
        CreateTestFile(_file10KB, 10 * 1024);

        _file100KB = Path.Combine(tempDir, "test_100kb.txt");
        CreateTestFile(_file100KB, 100 * 1024);

        _file1MB = Path.Combine(tempDir, "test_1mb.txt");
        CreateTestFile(_file1MB, 1024 * 1024);

        _file10MB = Path.Combine(tempDir, "test_10mb.txt");
        CreateTestFile(_file10MB, 10 * 1024 * 1024);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    private void CreateTestFile(string path, int sizeBytes)
    {
        var random = new Random(42);
        var content = new StringBuilder(sizeBytes);

        while (content.Length < sizeBytes)
        {
            content.AppendLine($"Line {content.Length}: {Guid.NewGuid()}");
        }

        File.WriteAllText(path, content.ToString().Substring(0, sizeBytes));
    }

    #region Read Benchmarks - Baseline

    [Benchmark(Baseline = true)]
    public async Task<string> ReadAllText_1KB()
    {
        return await File.ReadAllTextAsync(_file1KB);
    }

    [Benchmark]
    public async Task<string> ReadAllText_10KB()
    {
        return await File.ReadAllTextAsync(_file10KB);
    }

    [Benchmark]
    public async Task<string> ReadAllText_100KB()
    {
        return await File.ReadAllTextAsync(_file100KB);
    }

    [Benchmark]
    public async Task<string> ReadAllText_1MB()
    {
        return await File.ReadAllTextAsync(_file1MB);
    }

    [Benchmark]
    public async Task<string> ReadAllText_10MB()
    {
        return await File.ReadAllTextAsync(_file10MB);
    }

    #endregion

    #region Write Benchmarks - Baseline

    [Benchmark]
    public async Task WriteAllText_1KB()
    {
        var content = await File.ReadAllTextAsync(_file1KB);
        var outputPath = Path.Combine(_outputDir, "output_1kb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    [Benchmark]
    public async Task WriteAllText_10KB()
    {
        var content = await File.ReadAllTextAsync(_file10KB);
        var outputPath = Path.Combine(_outputDir, "output_10kb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    [Benchmark]
    public async Task WriteAllText_100KB()
    {
        var content = await File.ReadAllTextAsync(_file100KB);
        var outputPath = Path.Combine(_outputDir, "output_100kb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    [Benchmark]
    public async Task WriteAllText_1MB()
    {
        var content = await File.ReadAllTextAsync(_file1MB);
        var outputPath = Path.Combine(_outputDir, "output_1mb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    [Benchmark]
    public async Task WriteAllText_10MB()
    {
        var content = await File.ReadAllTextAsync(_file10MB);
        var outputPath = Path.Combine(_outputDir, "output_10mb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    #endregion

    #region Read+Write Combined

    [Benchmark]
    public async Task ReadWrite_1MB()
    {
        var content = await File.ReadAllTextAsync(_file1MB);
        var outputPath = Path.Combine(_outputDir, "combined_1mb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    [Benchmark]
    public async Task ReadWrite_10MB()
    {
        var content = await File.ReadAllTextAsync(_file10MB);
        var outputPath = Path.Combine(_outputDir, "combined_10mb.txt");
        await File.WriteAllTextAsync(outputPath, content);
    }

    #endregion
}

using BenchmarkDotNet.Attributes;
using System.Text;
using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for OptimizedFileIO - high-performance file operations.
/// Compares performance against baseline File.* APIs.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class OptimizedFileIOBenchmarks
{
private string _file1KB = null!;
private string _file10KB = null!;
private string _file100KB = null!;
private string _file1MB = null!;
private string _file10MB = null!;
private string _file100MB = null!;
private string _outputDir = null!;

[GlobalSetup]
public void Setup()
{
var tempDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks_OptimizedIO");
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

_file100MB = Path.Combine(tempDir, "test_100mb.txt");
CreateTestFile(_file100MB, 100 * 1024 * 1024);
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

#region Read Benchmarks - Baseline (File.*)

[Benchmark(Baseline = true, Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_1KB()
{
return await File.ReadAllTextAsync(_file1KB);
}

[Benchmark(Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_10KB()
{
return await File.ReadAllTextAsync(_file10KB);
}

[Benchmark(Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_100KB()
{
return await File.ReadAllTextAsync(_file100KB);
}

[Benchmark(Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_1MB()
{
return await File.ReadAllTextAsync(_file1MB);
}

[Benchmark(Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_10MB()
{
return await File.ReadAllTextAsync(_file10MB);
}

[Benchmark(Description = "File.ReadAllTextAsync (Baseline)")]
[BenchmarkCategory("Read", "Baseline")]
public async Task<string> Baseline_ReadAllText_100MB()
{
return await File.ReadAllTextAsync(_file100MB);
}

#endregion

#region Read Benchmarks - Optimized (OptimizedFileIO)

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_1KB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file1KB);
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_10KB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file10KB);
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_100KB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file100KB);
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_1MB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file1MB);
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_10MB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file10MB);
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync")]
[BenchmarkCategory("Read", "Optimized")]
public async Task<string> Optimized_ReadAllText_100MB()
{
return await OptimizedFileIO.ReadAllTextAsync(_file100MB);
}

#endregion

#region Write Benchmarks - Baseline (File.*)

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_1KB()
{
var content = await File.ReadAllTextAsync(_file1KB);
var outputPath = Path.Combine(_outputDir, "baseline_output_1kb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_10KB()
{
var content = await File.ReadAllTextAsync(_file10KB);
var outputPath = Path.Combine(_outputDir, "baseline_output_10kb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_100KB()
{
var content = await File.ReadAllTextAsync(_file100KB);
var outputPath = Path.Combine(_outputDir, "baseline_output_100kb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_1MB()
{
var content = await File.ReadAllTextAsync(_file1MB);
var outputPath = Path.Combine(_outputDir, "baseline_output_1mb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_10MB()
{
var content = await File.ReadAllTextAsync(_file10MB);
var outputPath = Path.Combine(_outputDir, "baseline_output_10mb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "File.WriteAllTextAsync (Baseline)")]
[BenchmarkCategory("Write", "Baseline")]
public async Task Baseline_WriteAllText_100MB()
{
var content = await File.ReadAllTextAsync(_file100MB);
var outputPath = Path.Combine(_outputDir, "baseline_output_100mb.txt");
await File.WriteAllTextAsync(outputPath, content);
}

#endregion

#region Write Benchmarks - Optimized (OptimizedFileIO)

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_1KB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file1KB);
var outputPath = Path.Combine(_outputDir, "optimized_output_1kb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_10KB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file10KB);
var outputPath = Path.Combine(_outputDir, "optimized_output_10kb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_100KB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file100KB);
var outputPath = Path.Combine(_outputDir, "optimized_output_100kb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_1MB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file1MB);
var outputPath = Path.Combine(_outputDir, "optimized_output_1mb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_10MB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file10MB);
var outputPath = Path.Combine(_outputDir, "optimized_output_10mb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync")]
[BenchmarkCategory("Write", "Optimized")]
public async Task Optimized_WriteAllText_100MB()
{
var content = await OptimizedFileIO.ReadAllTextAsync(_file100MB);
var outputPath = Path.Combine(_outputDir, "optimized_output_100mb.txt");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}

#endregion
}

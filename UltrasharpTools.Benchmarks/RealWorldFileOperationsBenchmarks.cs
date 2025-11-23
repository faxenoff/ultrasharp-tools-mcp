using BenchmarkDotNet.Attributes;
using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks using real-world files from aspnetcore and roslyn repositories.
/// Tests actual workloads that UltrasharpTools will handle.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class RealWorldFileOperationsBenchmarks
{
private const string AspNetCoreRepoPath = @"D:\github\repos-for-test\aspnetcore";
private const string RoslynRepoPath = @"D:\github\repos-for-test\roslyn";

private List<string> _smallFiles = new();     // < 10KB
private List<string> _mediumFiles = new();    // 10KB - 100KB
private List<string> _largeFiles = new();     // > 100KB

private string _outputDir = null!;

[GlobalSetup]
public void Setup()
{
_outputDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks_RealWorld");
Directory.CreateDirectory(_outputDir);

// Collect files from aspnetcore repository
if (Directory.Exists(AspNetCoreRepoPath))
{
CollectCSharpFiles(AspNetCoreRepoPath);
}
else
{
Console.WriteLine($"WARNING: aspnetcore repository not found at {AspNetCoreRepoPath}");
}

Console.WriteLine($"Collected {_smallFiles.Count} small files, {_mediumFiles.Count} medium files, {_largeFiles.Count} large files");
}

[GlobalCleanup]
public void Cleanup()
{
if (Directory.Exists(_outputDir))
{
Directory.Delete(_outputDir, recursive: true);
}
}

private void CollectCSharpFiles(string repoPath)
{
var csFiles = Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories)
.Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
.Take(100) // Limit to 100 files for reasonable benchmark time
.ToList();

foreach (var file in csFiles)
{
var fileInfo = new FileInfo(file);
var sizeKB = fileInfo.Length / 1024;

if (sizeKB < 10)
{
_smallFiles.Add(file);
}
else if (sizeKB < 100)
{
_mediumFiles.Add(file);
}
else
{
_largeFiles.Add(file);
}
}

// Ensure we have at least some files in each category
if (_smallFiles.Count == 0 || _mediumFiles.Count == 0)
{
Console.WriteLine("WARNING: Not enough files in some categories");
}
}

#region Read Operations - Small Files (< 10KB)

[Benchmark(Baseline = true, Description = "File.ReadAllTextAsync - Small Files")]
[BenchmarkCategory("Read", "Small", "Baseline")]
public async Task Baseline_ReadSmallFiles()
{
var filesToRead = _smallFiles.Take(10).ToList();
foreach (var file in filesToRead)
{
_ = await File.ReadAllTextAsync(file);
}
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync - Small Files")]
[BenchmarkCategory("Read", "Small", "Optimized")]
public async Task Optimized_ReadSmallFiles()
{
var filesToRead = _smallFiles.Take(10).ToList();
foreach (var file in filesToRead)
{
_ = await OptimizedFileIO.ReadAllTextAsync(file);
}
}

#endregion

#region Read Operations - Medium Files (10-100KB)

[Benchmark(Description = "File.ReadAllTextAsync - Medium Files")]
[BenchmarkCategory("Read", "Medium", "Baseline")]
public async Task Baseline_ReadMediumFiles()
{
var filesToRead = _mediumFiles.Take(10).ToList();
foreach (var file in filesToRead)
{
_ = await File.ReadAllTextAsync(file);
}
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync - Medium Files")]
[BenchmarkCategory("Read", "Medium", "Optimized")]
public async Task Optimized_ReadMediumFiles()
{
var filesToRead = _mediumFiles.Take(10).ToList();
foreach (var file in filesToRead)
{
_ = await OptimizedFileIO.ReadAllTextAsync(file);
}
}

#endregion

#region Read Operations - Large Files (> 100KB)

[Benchmark(Description = "File.ReadAllTextAsync - Large Files")]
[BenchmarkCategory("Read", "Large", "Baseline")]
public async Task Baseline_ReadLargeFiles()
{
var filesToRead = _largeFiles.Take(5).ToList();
foreach (var file in filesToRead)
{
_ = await File.ReadAllTextAsync(file);
}
}

[Benchmark(Description = "OptimizedFileIO.ReadAllTextAsync - Large Files")]
[BenchmarkCategory("Read", "Large", "Optimized")]
public async Task Optimized_ReadLargeFiles()
{
var filesToRead = _largeFiles.Take(5).ToList();
foreach (var file in filesToRead)
{
_ = await OptimizedFileIO.ReadAllTextAsync(file);
}
}

#endregion

#region Write Operations - Small Files

[Benchmark(Description = "File.WriteAllTextAsync - Small Files")]
[BenchmarkCategory("Write", "Small", "Baseline")]
public async Task Baseline_WriteSmallFiles()
{
var filesToWrite = _smallFiles.Take(10).ToList();
for (int i = 0; i < filesToWrite.Count; i++)
{
var content = await File.ReadAllTextAsync(filesToWrite[i]);
var outputPath = Path.Combine(_outputDir, $"baseline_small_{i}.cs");
await File.WriteAllTextAsync(outputPath, content);
}
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync - Small Files")]
[BenchmarkCategory("Write", "Small", "Optimized")]
public async Task Optimized_WriteSmallFiles()
{
var filesToWrite = _smallFiles.Take(10).ToList();
for (int i = 0; i < filesToWrite.Count; i++)
{
var content = await OptimizedFileIO.ReadAllTextAsync(filesToWrite[i]);
var outputPath = Path.Combine(_outputDir, $"optimized_small_{i}.cs");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}
}

#endregion

#region Write Operations - Medium Files

[Benchmark(Description = "File.WriteAllTextAsync - Medium Files")]
[BenchmarkCategory("Write", "Medium", "Baseline")]
public async Task Baseline_WriteMediumFiles()
{
var filesToWrite = _mediumFiles.Take(10).ToList();
for (int i = 0; i < filesToWrite.Count; i++)
{
var content = await File.ReadAllTextAsync(filesToWrite[i]);
var outputPath = Path.Combine(_outputDir, $"baseline_medium_{i}.cs");
await File.WriteAllTextAsync(outputPath, content);
}
}

[Benchmark(Description = "OptimizedFileIO.WriteAllTextAsync - Medium Files")]
[BenchmarkCategory("Write", "Medium", "Optimized")]
public async Task Optimized_WriteMediumFiles()
{
var filesToWrite = _mediumFiles.Take(10).ToList();
for (int i = 0; i < filesToWrite.Count; i++)
{
var content = await OptimizedFileIO.ReadAllTextAsync(filesToWrite[i]);
var outputPath = Path.Combine(_outputDir, $"optimized_medium_{i}.cs");
await OptimizedFileIO.WriteAllTextAsync(outputPath, content);
}
}

#endregion

#region Read+Write Combined - Real Workflow

[Benchmark(Description = "File.* - Read+Process+Write (10 files)")]
[BenchmarkCategory("Combined", "Baseline")]
public async Task Baseline_ReadProcessWrite()
{
var filesToProcess = _mediumFiles.Take(10).ToList();
for (int i = 0; i < filesToProcess.Count; i++)
{
var content = await File.ReadAllTextAsync(filesToProcess[i]);
// Simulate some processing (like code formatting)
var processed = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
var outputPath = Path.Combine(_outputDir, $"baseline_combined_{i}.cs");
await File.WriteAllTextAsync(outputPath, processed);
}
}

[Benchmark(Description = "OptimizedFileIO - Read+Process+Write (10 files)")]
[BenchmarkCategory("Combined", "Optimized")]
public async Task Optimized_ReadProcessWrite()
{
var filesToProcess = _mediumFiles.Take(10).ToList();
for (int i = 0; i < filesToProcess.Count; i++)
{
var content = await OptimizedFileIO.ReadAllTextAsync(filesToProcess[i]);
// Simulate some processing (like code formatting)
var processed = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
var outputPath = Path.Combine(_outputDir, $"optimized_combined_{i}.cs");
await OptimizedFileIO.WriteAllTextAsync(outputPath, processed);
}
}

#endregion
}

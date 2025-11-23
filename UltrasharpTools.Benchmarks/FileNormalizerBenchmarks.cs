using BenchmarkDotNet.Attributes;
using System.Text;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for FileNormalizer to measure encoding/BOM/line ending normalization performance.
/// Tests current implementation vs optimized approaches with ArrayPool.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class FileNormalizerBenchmarks
{
    private string _fileUtf8NoBom = null!;
    private string _fileUtf8WithBom = null!;
    private string _fileUtf16LE = null!;
    private string _fileMixedLineEndings = null!;
    private string _file1MB = null!;
    private string _file10MB = null!;
    private string _outputDir = null!;

    [GlobalSetup]
    public void Setup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UltrasharpToolsBenchmarks_FileNormalizer");
        Directory.CreateDirectory(tempDir);
        _outputDir = tempDir;

        // UTF-8 without BOM (most common)
        _fileUtf8NoBom = Path.Combine(tempDir, "utf8_nobom.txt");
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(_fileUtf8NoBom, GenerateContent(100 * 1024), utf8NoBom);

        // UTF-8 with BOM
        _fileUtf8WithBom = Path.Combine(tempDir, "utf8_bom.txt");
        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllText(_fileUtf8WithBom, GenerateContent(100 * 1024), utf8WithBom);

        // UTF-16 LE
        _fileUtf16LE = Path.Combine(tempDir, "utf16le.txt");
        File.WriteAllText(_fileUtf16LE, GenerateContent(100 * 1024), Encoding.Unicode);

        // Mixed line endings (CRLF, LF, CR)
        _fileMixedLineEndings = Path.Combine(tempDir, "mixed_lineendings.txt");
        CreateMixedLineEndingsFile(_fileMixedLineEndings, 10000);

        // Large files for stress testing
        _file1MB = Path.Combine(tempDir, "large_1mb.txt");
        File.WriteAllText(_file1MB, GenerateContent(1024 * 1024), utf8NoBom);

        _file10MB = Path.Combine(tempDir, "large_10mb.txt");
        File.WriteAllText(_file10MB, GenerateContent(10 * 1024 * 1024), utf8NoBom);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    private string GenerateContent(int targetSize)
    {
        var sb = new StringBuilder(targetSize);
        var random = new Random(42);

        while (sb.Length < targetSize)
        {
            sb.AppendLine($"Line {sb.Length}: Sample content {random.Next()}");
        }

        return sb.ToString().Substring(0, Math.Min(targetSize, sb.Length));
    }

    private void CreateMixedLineEndingsFile(string path, int lineCount)
    {
        var sb = new StringBuilder();
        var random = new Random(42);

        for (int i = 0; i < lineCount; i++)
        {
            sb.Append($"Line {i}: Content");

            // Randomly use different line endings
            var choice = random.Next(3);
            if (choice == 0)
                sb.Append("\r\n"); // Windows CRLF
            else if (choice == 1)
                sb.Append("\n");   // Unix LF
            else
                sb.Append("\r");   // Old Mac CR
        }

        File.WriteAllText(path, sb.ToString());
    }

    #region Read and Normalize - Current Implementation (Baseline)

    [Benchmark(Baseline = true)]
    public async Task<string> ReadNormalized_Utf8NoBom()
    {
        return await FileNormalizer.ReadNormalizedAsync(_fileUtf8NoBom);
    }

    [Benchmark]
    public async Task<string> ReadNormalized_Utf8WithBom()
    {
        return await FileNormalizer.ReadNormalizedAsync(_fileUtf8WithBom);
    }

    [Benchmark]
    public async Task<string> ReadNormalized_Utf16LE()
    {
        return await FileNormalizer.ReadNormalizedAsync(_fileUtf16LE);
    }

    [Benchmark]
    public async Task<string> ReadNormalized_MixedLineEndings()
    {
        return await FileNormalizer.ReadNormalizedAsync(_fileMixedLineEndings);
    }

    [Benchmark]
    public async Task<string> ReadNormalized_1MB()
    {
        return await FileNormalizer.ReadNormalizedAsync(_file1MB);
    }

    [Benchmark]
    public async Task<string> ReadNormalized_10MB()
    {
        return await FileNormalizer.ReadNormalizedAsync(_file10MB);
    }

    #endregion

    #region Write Normalized - Current Implementation

    [Benchmark]
    public async Task WriteNormalized_100KB()
    {
        var content = await FileNormalizer.ReadNormalizedAsync(_fileUtf8NoBom);
        var outputPath = Path.Combine(_outputDir, "output_normalized.txt");
        await FileNormalizer.WriteNormalizedAsync(outputPath, content);
    }

    [Benchmark]
    public async Task WriteNormalized_1MB()
    {
        var content = await FileNormalizer.ReadNormalizedAsync(_file1MB);
        var outputPath = Path.Combine(_outputDir, "output_1mb.txt");
        await FileNormalizer.WriteNormalizedAsync(outputPath, content);
    }

    #endregion

    #region Line Ending Normalization

    [Benchmark]
    public string NormalizeLineEndings_CRLF()
    {
        var content = "Line 1\r\nLine 2\r\nLine 3\r\nLine 4\r\nLine 5\r\n".Repeat(1000);
        return FileNormalizer.NormalizeLineEndings(content);
    }

    [Benchmark]
    public string NormalizeLineEndings_Mixed()
    {
        var content = "Line 1\r\nLine 2\nLine 3\rLine 4\r\nLine 5\n".Repeat(1000);
        return FileNormalizer.NormalizeLineEndings(content);
    }

    #endregion

    #region Hash Computation

    [Benchmark]
    public string ComputeHash_100KB()
    {
        var content = GenerateContent(100 * 1024);
        return FileNormalizer.ComputeContentHash(content);
    }

    [Benchmark]
    public string ComputeHash_1MB()
    {
        var content = GenerateContent(1024 * 1024);
        return FileNormalizer.ComputeContentHash(content);
    }

    #endregion
}

/// <summary>
/// Helper extension for repeating strings in benchmarks.
/// </summary>
internal static class StringExtensions
{
    public static string Repeat(this string text, int count)
    {
        if (count <= 0) return string.Empty;

        var sb = new StringBuilder(text.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(text);
        }
        return sb.ToString();
    }
}

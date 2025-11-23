using BenchmarkDotNet.Attributes;
using System.Text;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks comparing Substring (allocates) vs Span&lt;T&gt; (zero-copy).
/// Demonstrates the performance benefits of Span for string slicing operations.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class SpanVsSubstringBenchmarks
{
    private string _sampleText = null!;
    private string _longText = null!;
    private List<string> _lines = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sampleText = "  Hello World from Ultrasharp Tools!  ";

        var sb = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            sb.AppendLine($"  Line {i}: Sample content with leading/trailing spaces  ");
        }
        _longText = sb.ToString();

        _lines = _longText.Split('\n').ToList();
    }

    #region Trim Leading Spaces - Substring vs Span

    [Benchmark(Baseline = true)]
    public string TrimLeadingSpaces_Substring()
    {
        int i = 0;
        while (i < _sampleText.Length && char.IsWhiteSpace(_sampleText[i]))
            i++;

        return i > 0 ? _sampleText.Substring(i) : _sampleText;
    }

    [Benchmark]
    public ReadOnlySpan<char> TrimLeadingSpaces_Span()
    {
        ReadOnlySpan<char> span = _sampleText.AsSpan();
        int i = 0;
        while (i < span.Length && char.IsWhiteSpace(span[i]))
            i++;

        return i > 0 ? span.Slice(i) : span;
    }

    #endregion

    #region Multiple Lines Processing

    [Benchmark]
    public int ProcessLines_Substring()
    {
        int totalLength = 0;
        foreach (var line in _lines)
        {
            // Trim leading spaces
            int i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            var trimmed = i > 0 ? line.Substring(i) : line;
            totalLength += trimmed.Length;
        }
        return totalLength;
    }

    [Benchmark]
    public int ProcessLines_Span()
    {
        int totalLength = 0;
        foreach (var line in _lines)
        {
            // Trim leading spaces
            ReadOnlySpan<char> span = line.AsSpan();
            int i = 0;
            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            var trimmed = i > 0 ? span.Slice(i) : span;
            totalLength += trimmed.Length;
        }
        return totalLength;
    }

    #endregion

    #region String Splitting and Processing

    private const string LogLine = "2024-01-15 10:30:45.123 [ERROR] Database connection failed: timeout after 30 seconds";

    [Benchmark]
    public (string timestamp, string level, string message) ParseLogLine_Substring()
    {
        // Extract timestamp (0-23)
        var timestamp = LogLine.Substring(0, 23);

        // Extract level (25-30)
        var level = LogLine.Substring(25, 5);

        // Extract message (after 32)
        var message = LogLine.Substring(32);

        return (timestamp, level, message);
    }

    [Benchmark]
    public int ParseLogLine_Span()
    {
        ReadOnlySpan<char> span = LogLine.AsSpan();

        // Extract timestamp (0-23)
        var timestamp = span.Slice(0, 23);

        // Extract level (25-30)
        var level = span.Slice(25, 5);

        // Extract message (after 32)
        var message = span.Slice(32);

        // Return combined length to prevent optimization
        return timestamp.Length + level.Length + message.Length;
    }

    #endregion

    #region Number Parsing

    private const string NumberString = "  12345  ";

    [Benchmark]
    public int ParseNumber_Substring()
    {
        var trimmed = NumberString.Trim();
        return int.Parse(trimmed);
    }

    [Benchmark]
    public int ParseNumber_Span()
    {
        ReadOnlySpan<char> span = NumberString.AsSpan().Trim();
        return int.Parse(span);
    }

    #endregion

    #region IndexOf and Slice Operations

    private const string PathString = "/var/log/application/app.log";

    [Benchmark]
    public string GetFileName_Substring()
    {
        int lastSlash = PathString.LastIndexOf('/');
        return lastSlash >= 0 ? PathString.Substring(lastSlash + 1) : PathString;
    }

    [Benchmark]
    public ReadOnlySpan<char> GetFileName_Span()
    {
        ReadOnlySpan<char> span = PathString.AsSpan();
        int lastSlash = span.LastIndexOf('/');
        return lastSlash >= 0 ? span.Slice(lastSlash + 1) : span;
    }

    #endregion

    #region Repeated Operations (Hot Path)

    [Benchmark]
    public int HotPath_Substring_1000x()
    {
        int total = 0;
        for (int i = 0; i < 1000; i++)
        {
            int idx = _sampleText.IndexOf("World");
            if (idx >= 0)
            {
                var after = _sampleText.Substring(idx + 5);
                total += after.Length;
            }
        }
        return total;
    }

    [Benchmark]
    public int HotPath_Span_1000x()
    {
        int total = 0;
        for (int i = 0; i < 1000; i++)
        {
            ReadOnlySpan<char> span = _sampleText.AsSpan();
            int idx = span.IndexOf("World".AsSpan());
            if (idx >= 0)
            {
                var after = span.Slice(idx + 5);
                total += after.Length;
            }
        }
        return total;
    }

    #endregion
}

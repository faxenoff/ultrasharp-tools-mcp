namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Detects codebase size for auto-selecting vector store engine
/// </summary>
public class CodebaseSizeDetector
{
    public class SizeStats
    {
        public int TotalFiles { get; set; }
        public long TotalLines { get; set; }
        public long TotalBytes { get; set; }

        public string SizeCategory =>
            TotalFiles switch
            {
                < 1000 => "small",
                < 10000 => "medium",
                _ => "large",
            };

        public string RecommendedVectorStore =>
            SizeCategory switch
            {
                "small" => "sqlite-vec",
                "medium" => "sqlite-vec",
                "large" => "vectorlite",
                _ => "sqlite-vec",
            };
    }

    /// <summary>
    /// Analyze codebase size across all C# files in workspace
    /// </summary>
    public async Task<SizeStats> AnalyzeAsync(
        Solution solution,
        CancellationToken cancellationToken = default
    )
    {
        var stats = new SizeStats();

        var projects = solution.Projects.ToList();

        foreach (var project in projects)
        {
            var documents = project.Documents.ToList();

            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stats.TotalFiles++;

                var text = await document.GetTextAsync(cancellationToken);
                if (text != null)
                {
                    stats.TotalLines += text.Lines.Count;
                    stats.TotalBytes += text.Length;
                }
            }
        }

        return stats;
    }
}

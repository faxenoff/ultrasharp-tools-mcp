namespace UltrasharpTools.Tools.Config;

/// <summary>
/// Project-specific semantic configuration (stored near database)
/// </summary>
public class ProjectSemanticConfig
{
    public CodebaseSettings Codebase { get; set; } = new();
    public VectorStoreSettings VectorStore { get; set; } = new();
}

public class CodebaseSettings
{
    /// <summary>
    /// Size: auto, small (&lt;1000 files), medium (1000-10000), large (&gt;10000)
    /// </summary>
    public string Size { get; set; } = "auto";

    /// <summary>
    /// Language: auto, english, russian, chinese, multilingual
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Threshold for switching to multilingual model (% of non-english words)
    /// </summary>
    public int MultilingualThreshold { get; set; } = 20;

    /// <summary>
    /// Detected statistics (cached)
    /// </summary>
    public CodebaseStats? Stats { get; set; }
}

public class CodebaseStats
{
    public int TotalFiles { get; set; }
    public int TotalComments { get; set; }
    public int NonEnglishWords { get; set; }
    public int TotalWords { get; set; }
    public double NonEnglishPercentage { get; set; }
}

public class VectorStoreSettings
{
    /// <summary>
    /// Engine: auto, sqlite-vec, vectorlite
    /// </summary>
    public string Engine { get; set; } = "auto";

    public AutoSelectionSettings AutoSelection { get; set; } = new()
    {
        Small = "sqlite-vec",
        Medium = "sqlite-vec",
        Large = "vectorlite"
    };
}

public class AutoSelectionSettings
{
    public string Small { get; set; } = "sqlite-vec";
    public string Medium { get; set; } = "sqlite-vec";
    public string Large { get; set; } = "vectorlite";
}

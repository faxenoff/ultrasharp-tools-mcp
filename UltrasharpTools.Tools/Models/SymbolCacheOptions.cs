namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Options for symbol cache behavior
/// </summary>
public class SymbolCacheOptions
{
    /// <summary>
    /// Enable persistent symbol cache for 10x faster solution initialization (33s → 3-5s)
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom cache directory path (null = use default temp directory)
    /// Default: %TEMP%/UltrasharpTools/SymbolCache
    /// </summary>
    public string? CacheDirectory { get; set; } = null;

    /// <summary>
    /// Clear all cached symbol data on startup
    /// Default: false
    /// </summary>
    public bool ClearOnStartup { get; set; } = false;
}

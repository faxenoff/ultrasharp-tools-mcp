namespace UltrasharpTools.Tools.Semantic.GPU;

/// <summary>
/// GPU hardware information
/// </summary>
public sealed class GPUInfo
{
    /// <summary>
    /// GPU vendor (nvidia, amd, intel, unknown)
    /// </summary>
    public string Vendor { get; init; } = "unknown";

    /// <summary>
    /// GPU model name (e.g., "GeForce GTX 1650 Ti")
    /// </summary>
    public string Model { get; init; } = "Unknown";

    /// <summary>
    /// NVIDIA Compute Capability (e.g., 7.5 for GTX 1650, 8.0+ for RTX 30xx)
    /// Only available for NVIDIA GPUs
    /// </summary>
    public float? ComputeCapability { get; init; }

    /// <summary>
    /// GPU memory in MB
    /// </summary>
    public int MemoryMB { get; init; }

    /// <summary>
    /// Whether CUDA is available (NVIDIA only)
    /// </summary>
    public bool CudaAvailable { get; init; }

    /// <summary>
    /// CUDA Toolkit version (e.g., "12.3", "13.0")
    /// </summary>
    public string? CudaVersion { get; init; }

    /// <summary>
    /// Whether cuDNN is available
    /// </summary>
    public bool CudnnAvailable { get; init; }

    /// <summary>
    /// GPU clock rate in MHz
    /// </summary>
    public int? ClockRateMHz { get; init; }

    /// <summary>
    /// Recommended embedding provider based on GPU capabilities
    /// </summary>
    public string RecommendedProvider => DetermineRecommendedProvider();

    /// <summary>
    /// Maximum supported context tokens for embeddings
    /// </summary>
    public int MaxContextTokens => DetermineMaxContextTokens();

    private string DetermineRecommendedProvider()
    {
        // TEI requires RTX 30xx+ (Compute Capability 8.0+)
        if (CudaAvailable && ComputeCapability >= 8.0f)
        {
            return "tei"; // 8192 tokens
        }

        // Ollama for older GPUs or non-NVIDIA
        if (CudaAvailable || Vendor != "unknown")
        {
            return "ollama"; // 512 tokens
        }

        // Fallback to memory provider
        return "memory"; // no ML embeddings
    }

    private int DetermineMaxContextTokens()
    {
        return RecommendedProvider switch
        {
            "tei" => 8192,
            "ollama" => 512,
            _ => 0,
        };
    }

    public override string ToString()
    {
        var cc = ComputeCapability.HasValue ? $" (CC {ComputeCapability:F1})" : "";
        var mem = MemoryMB > 0 ? $", {MemoryMB / 1024.0:F1} GB" : "";
        var cuda = CudaVersion != null ? $", CUDA {CudaVersion}" : "";
        var cudnn = CudnnAvailable ? " + cuDNN" : "";
        return $"{Vendor.ToUpper()} {Model}{cc}{mem}{cuda}{cudnn} → {RecommendedProvider} ({MaxContextTokens} tokens)";
    }
}

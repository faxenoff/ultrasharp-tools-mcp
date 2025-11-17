namespace UltrasharpTools.Tools.Semantic.GPU;

/// <summary>
/// Service for detecting GPU capabilities
/// </summary>
public interface IGPUDetectionService
{
    /// <summary>
    /// Detect GPU capabilities
    /// Result is cached for performance
    /// </summary>
    Task<GPUInfo> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear cached GPU info
    /// </summary>
    void ClearCache();
}

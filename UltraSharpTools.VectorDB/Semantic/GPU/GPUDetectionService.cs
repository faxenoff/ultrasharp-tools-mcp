using Microsoft.Extensions.Logging;
namespace UltraSharpTools.VectorDB.Semantic.GPU;

/// <summary>
/// Заглушка GPU detection для Indexer
/// Всегда возвращает null (GPU detection не используется в Indexer)
/// </summary>
public sealed class GPUDetectionService : IGPUDetectionService
{
    public Task<GPUInfo?> DetectGPUAsync()
    {
        // Indexer не использует GPU detection
        return Task.FromResult<GPUInfo?>(null);
    }
}

using Microsoft.Extensions.Logging;
namespace UltraSharpTools.Indexer.Semantic.GPU;

/// <summary>
/// Интерфейс для определения GPU
/// Заглушка для Indexer (полная версия в Tools)
/// </summary>
public interface IGPUDetectionService
{
    Task<GPUInfo?> DetectGPUAsync();
}

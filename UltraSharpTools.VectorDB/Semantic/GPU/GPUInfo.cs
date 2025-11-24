using Microsoft.Extensions.Logging;
namespace UltraSharpTools.VectorDB.Semantic.GPU;

/// <summary>
/// Информация о GPU
/// Заглушка для Indexer (полная версия в Tools)
/// </summary>
public sealed record GPUInfo(
    string Name,
    string Architecture,
    long Memory,
    string Vendor
);

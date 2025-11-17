namespace UltrasharpTools.Tools.Semantic.Models;

/// <summary>
/// Metadata информация об embedding provider.
/// </summary>
public sealed record ProviderInfo
{
/// <summary>
/// Название провайдера: "ollama", "tei", "memory"
/// </summary>
public required string Name { get; init; }

/// <summary>
/// Название модели: "granite-278m", "granite-125m", "granite-30m", "xxhash"
/// </summary>
public required string Model { get; init; }

/// <summary>
/// Размерность векторов, которые генерирует этот provider.
/// </summary>
public required int Dimension { get; init; }

/// <summary>
/// Максимальное количество токенов для input текста.
/// </summary>
public required int MaxTokens { get; init; }

/// <summary>
/// True, если provider работает локально (Ollama, TEI, Memory).
/// False для cloud providers (в будущем: OpenAI, HuggingFace API).
/// </summary>
public required bool IsLocal { get; init; }

/// <summary>
/// Версия provider или модели.
/// </summary>
public required string Version { get; init; }

/// <summary>
/// Дополнительные capabilities (опционально).
/// Пример: batch support, streaming, etc.
/// </summary>
public Dictionary<string, object>? Capabilities { get; init; }
}

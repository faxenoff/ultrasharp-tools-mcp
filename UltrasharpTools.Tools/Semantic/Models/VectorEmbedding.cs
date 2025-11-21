namespace UltrasharpTools.Tools.Semantic.Models;

/// <summary>
/// Представляет vector embedding для фрагмента кода.
/// </summary>
public sealed record VectorEmbedding
{
    /// <summary>
    /// Уникальный идентификатор embedding (обычно FQN символа или hash).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Оригинальный текст кода, который был преобразован в вектор.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Vector embedding (обычно float32 array).
    /// Dimension зависит от модели: 384 (Granite 30M), 768 (Granite 125M/278M).
    /// </summary>
    public required float[] Vector { get; init; }

    /// <summary>
    /// Размерность вектора (384, 768, и т.д.).
    /// </summary>
    public required int Dimension { get; init; }

    /// <summary>
    /// Metadata в JSON формате.
    /// Пример: { "type": "method", "name": "Calculate", "filePath": "Math.cs", "lineNumber": 42 }
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Timestamp создания embedding (Unix time).
    /// </summary>
    public long CreatedAt { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// Название провайдера, который сгенерировал embedding.
    /// Примеры: "ollama:granite-278m", "tei:granite-125m", "memory:xxhash"
    /// </summary>
    public string? Provider { get; init; }
}

namespace UltrasharpTools.Tools.Semantic.Models;

/// <summary>
/// Результат поиска по similarity (cosine similarity).
/// </summary>
public sealed record SimilarityResult
{
/// <summary>
/// ID найденного embedding.
/// </summary>
public required string Id { get; init; }

/// <summary>
/// Оригинальный контент кода.
/// </summary>
public required string Content { get; init; }

/// <summary>
/// Cosine similarity score (0.0 - 1.0, где 1.0 = идентичные векторы).
/// </summary>
public required float Similarity { get; init; }

/// <summary>
/// Metadata в JSON формате (если доступно).
/// </summary>
public string? Metadata { get; init; }

/// <summary>
/// Distance metric (опционально, для отладки).
/// Euclidean distance = sqrt(2 * (1 - cosine_similarity))
/// </summary>
public float? Distance { get; init; }

/// <summary>
/// Rank в результатах поиска (1 = лучший результат).
/// </summary>
public int Rank { get; init; }
}

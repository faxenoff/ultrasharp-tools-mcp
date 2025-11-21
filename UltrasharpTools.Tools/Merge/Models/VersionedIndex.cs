using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Индекс кода для одной версии (base, branchA, branchB, merged).
/// Содержит все CodeUnit и их embeddings.
/// </summary>
public sealed record VersionedIndex
{
    /// <summary>Название версии (base, branchA, branchB, merged)</summary>
    public required string Version { get; init; }

    /// <summary>Git commit SHA (если доступен)</summary>
    public string? CommitSha { get; init; }

    /// <summary>Git branch name</summary>
    public string? BranchName { get; init; }

    /// <summary>Все units в этой версии (по ID)</summary>
    public required Dictionary<string, CodeUnit> Units { get; init; }

    /// <summary>Vector store для семантического поиска</summary>
    public required VectorStore VectorStore { get; init; }

    /// <summary>Timestamp создания индекса</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Статистика индекса</summary>
    public required IndexStatistics Statistics { get; init; }
}

/// <summary>
/// Статистика индекса.
/// </summary>
public sealed record IndexStatistics
{
    /// <summary>Общее количество units</summary>
    public required int TotalUnits { get; init; }

    /// <summary>Количество по типам</summary>
    public required Dictionary<CodeUnitType, int> UnitsByType { get; init; }

    /// <summary>Количество файлов</summary>
    public required int FileCount { get; init; }

    /// <summary>Количество units с embeddings</summary>
    public required int UnitsWithEmbeddings { get; init; }

    /// <summary>Время индексации (мс)</summary>
    public required long IndexingTimeMs { get; init; }
}

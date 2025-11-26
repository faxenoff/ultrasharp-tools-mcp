using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Результат поиска паттерна с полным контекстом.
/// Используется для preview и apply операций.
/// </summary>
public sealed record CodeMatch
{
    /// <summary>Уникальный ID для референса в apply</summary>
    public required string Id { get; init; }

    /// <summary>FQN контейнера (метод, класс)</summary>
    public required string ContainerFqn { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Строка где найден паттерн (1-based)</summary>
    public required int MatchLine { get; init; }

    /// <summary>Колонка где найден паттерн (1-based)</summary>
    public required int MatchColumn { get; init; }

    /// <summary>Полный код контейнера (scope)</summary>
    public required string FullCode { get; init; }

    /// <summary>Фрагмент где найден паттерн</summary>
    public required string MatchFragment { get; init; }

    /// <summary>Тип контейнера</summary>
    public required CodeUnitType ContainerType { get; init; }

    /// <summary>Начальная строка контейнера в файле (1-based)</summary>
    public required int ContainerStartLine { get; init; }

    /// <summary>Конечная строка контейнера в файле (1-based)</summary>
    public required int ContainerEndLine { get; init; }

    /// <summary>Метаданные (зависимости, используемые типы, etc)</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>Semantic similarity score (только для semantic search)</summary>
    public float? SemanticSimilarity { get; init; }
}

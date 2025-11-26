using Microsoft.CodeAnalysis;

namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Результат поиска паттерна (внутренняя модель до извлечения контекста).
/// </summary>
public sealed record PatternMatch
{
    /// <summary>ID документа в Roslyn workspace</summary>
    public required DocumentId DocumentId { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Позиция начала match в тексте</summary>
    public required int StartPosition { get; init; }

    /// <summary>Длина match</summary>
    public required int Length { get; init; }

    /// <summary>Строка (1-based)</summary>
    public required int Line { get; init; }

    /// <summary>Колонка (1-based)</summary>
    public required int Column { get; init; }

    /// <summary>Текст match</summary>
    public required string MatchedText { get; init; }

    /// <summary>Roslyn symbol (для Roslyn mode)</summary>
    public ISymbol? Symbol { get; init; }

    /// <summary>Semantic similarity score (для Semantic mode)</summary>
    public float? SemanticSimilarity { get; init; }

    /// <summary>FQN из семантического поиска (для Semantic mode)</summary>
    public string? SemanticFqn { get; init; }
}

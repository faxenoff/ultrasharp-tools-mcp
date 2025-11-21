using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Fast Path matching: O(1) lookup по hash/signature.
/// Обрабатывает 90% случаев без embeddings.
/// </summary>
public sealed class FastPathMatcher
{
    private readonly ILogger<FastPathMatcher> _logger;

    public FastPathMatcher(ILogger<FastPathMatcher>? logger = null)
    {
        _logger = logger ?? NullLogger<FastPathMatcher>.Instance;
    }

    /// <summary>
    /// Попытка быстрого сопоставления двух CodeUnit.
    /// </summary>
    public FastPathMatchResult? TryMatch(CodeUnit unitA, CodeUnit unitB)
    {
        // Level 1: Exact content match (100% идентичны)
        if (unitA.ContentHash == unitB.ContentHash)
        {
            _logger.LogDebug("Exact match (content hash): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.ExactContent,
                Confidence = 1.0f,
            };
        }

        // Level 2: Structural match (одинаковая структура, разные комментарии/whitespace)
        if (unitA.StructuralHash == unitB.StructuralHash)
        {
            _logger.LogDebug("Structural match (AST hash): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.StructuralSame,
                Confidence = 0.95f,
            };
        }

        // Level 3: Signature match (для методов - FQN + parameters)
        if (
            unitA.Signature != null
            && unitB.Signature != null
            && unitA.Signature == unitB.Signature
        )
        {
            // Та же сигнатура, но разное тело метода
            _logger.LogDebug("Signature match: {Signature}", unitA.Signature);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.SignatureMatch,
                Confidence = 0.85f,
            };
        }

        // Level 4: ID match (переименование, но тот же символ)
        if (unitA.Id == unitB.Id)
        {
            _logger.LogDebug("ID match (renamed?): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.IdMatch,
                Confidence = 0.7f,
            };
        }

        // Fast path не сработал - нужен Slow Path
        return null;
    }

    /// <summary>
    /// Bulk matching для всех units в версии.
    /// </summary>
    public Dictionary<string, FastPathMatchResult> BulkMatch(
        VersionedIndex baseVersion,
        VersionedIndex targetVersion
    )
    {
        var matches = new Dictionary<string, FastPathMatchResult>();

        // Создать lookup таблицы для O(1) доступа
        var hashToUnits = targetVersion
            .Units.Values.GroupBy(u => u.ContentHash)
            .ToDictionary(g => g.Key, g => g.ToList());

        var structHashToUnits = targetVersion
            .Units.Values.GroupBy(u => u.StructuralHash)
            .ToDictionary(g => g.Key, g => g.ToList());

        var signatureToUnits = targetVersion
            .Units.Values.Where(u => u.Signature != null)
            .GroupBy(u => u.Signature!)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var baseUnit in baseVersion.Units.Values)
        {
            // Попробовать content hash
            if (hashToUnits.TryGetValue(baseUnit.ContentHash, out var exactMatches))
            {
                // Обычно должен быть только один
                var match = exactMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.ExactContent,
                    Confidence = 1.0f,
                };
                continue;
            }

            // Попробовать structural hash
            if (structHashToUnits.TryGetValue(baseUnit.StructuralHash, out var structMatches))
            {
                var match = structMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.StructuralSame,
                    Confidence = 0.95f,
                };
                continue;
            }

            // Попробовать signature match
            if (
                baseUnit.Signature != null
                && signatureToUnits.TryGetValue(baseUnit.Signature, out var sigMatches)
            )
            {
                var match = sigMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.SignatureMatch,
                    Confidence = 0.85f,
                };
                continue;
            }

            // ID match
            if (targetVersion.Units.TryGetValue(baseUnit.Id, out var idMatch))
            {
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = idMatch,
                    MatchType = FastPathMatchType.IdMatch,
                    Confidence = 0.7f,
                };
            }

            // Если ничего не нашли - unit попадёт в Slow Path
        }

        _logger.LogInformation(
            "Fast Path matched {Matched}/{Total} units ({Percent:F1}%)",
            matches.Count,
            baseVersion.Units.Count,
            matches.Count * 100.0 / baseVersion.Units.Count
        );

        return matches;
    }

    /// <summary>
    /// Найти все units в target версии, которые не были matched.
    /// Это candidates для Slow Path matching.
    /// </summary>
    public List<CodeUnit> FindUnmatchedUnits(
        VersionedIndex targetVersion,
        Dictionary<string, FastPathMatchResult> matches
    )
    {
        var matchedTargetIds = matches.Values.Select(m => m.UnitB.Id).ToHashSet();

        return targetVersion.Units.Values.Where(u => !matchedTargetIds.Contains(u.Id)).ToList();
    }

    /// <summary>
    /// Статистика Fast Path matching.
    /// </summary>
    public FastPathStatistics ComputeStatistics(
        VersionedIndex baseVersion,
        Dictionary<string, FastPathMatchResult> matches
    )
    {
        var totalUnits = baseVersion.Units.Count;
        var matchedCount = matches.Count;

        var byType = matches
            .Values.GroupBy(m => m.MatchType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new FastPathStatistics
        {
            TotalUnits = totalUnits,
            MatchedUnits = matchedCount,
            UnmatchedUnits = totalUnits - matchedCount,
            MatchRate = totalUnits > 0 ? (float)matchedCount / totalUnits : 0f,
            MatchesByType = byType,
        };
    }
}

/// <summary>
/// Результат Fast Path matching.
/// </summary>
public sealed record FastPathMatchResult
{
    public required CodeUnit UnitA { get; init; }
    public required CodeUnit UnitB { get; init; }
    public required FastPathMatchType MatchType { get; init; }
    public required float Confidence { get; init; }
}

/// <summary>
/// Тип Fast Path matching.
/// </summary>
public enum FastPathMatchType
{
    ExactContent, // 100% совпадение контента
    StructuralSame, // Одинаковая AST структура
    SignatureMatch, // Совпадает сигнатура (FQN + params)
    IdMatch, // Совпадает ID (возможно переименование)
}

/// <summary>
/// Статистика Fast Path matching.
/// </summary>
public sealed record FastPathStatistics
{
    public required int TotalUnits { get; init; }
    public required int MatchedUnits { get; init; }
    public required int UnmatchedUnits { get; init; }
    public required float MatchRate { get; init; }
    public required Dictionary<FastPathMatchType, int> MatchesByType { get; init; }

    public override string ToString()
    {
        var typeBreakdown = string.Join(
            ", ",
            MatchesByType.Select(kvp => $"{kvp.Key}: {kvp.Value}")
        );

        return $"Fast Path: {MatchedUnits}/{TotalUnits} matched ({MatchRate:P1}) - {typeBreakdown}";
    }
}

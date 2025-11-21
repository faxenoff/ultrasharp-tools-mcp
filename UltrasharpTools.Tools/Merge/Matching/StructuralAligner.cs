using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Нормализация порядка элементов для сравнения структур.
/// Определяет, является ли переупорядочивание членов единственным изменением.
/// </summary>
public sealed class StructuralAligner
{
    private readonly ILogger<StructuralAligner> _logger;

    public StructuralAligner(ILogger<StructuralAligner>? logger = null)
    {
        _logger = logger ?? NullLogger<StructuralAligner>.Instance;
    }

    /// <summary>
    /// Проверить, эквивалентны ли структуры с учётом переупорядочивания.
    /// </summary>
    public bool AreStructurallyEquivalent(
        CodeUnit unitA,
        CodeUnit unitB,
        Dictionary<string, FastPathMatchResult>? matches = null
    )
    {
        // 1. Базовые проверки
        if (unitA.Type != unitB.Type)
            return false;

        if (unitA.ChildIds.Count != unitB.ChildIds.Count)
            return false;

        // 2. Если нет детей - сравнить по StructuralHash
        if (unitA.ChildIds.Count == 0)
        {
            return unitA.StructuralHash == unitB.StructuralHash;
        }

        // 3. Проверить, что все дети matched (независимо от порядка)
        return AreChildrenEquivalent(unitA, unitB, matches);
    }

    /// <summary>
    /// Проверить эквивалентность детей (игнорируя порядок).
    /// </summary>
    private bool AreChildrenEquivalent(
        CodeUnit unitA,
        CodeUnit unitB,
        Dictionary<string, FastPathMatchResult>? matches
    )
    {
        // Если нет matches - невозможно определить
        if (matches == null)
            return false;

        // Проверить, что каждый child из A имеет match в B
        var childrenAMatched = new HashSet<string>();

        foreach (var childIdA in unitA.ChildIds)
        {
            if (matches.TryGetValue(childIdA, out var match))
            {
                // Проверить, что matched child принадлежит unitB
                if (unitB.ChildIds.Contains(match.UnitB.Id))
                {
                    childrenAMatched.Add(childIdA);
                }
            }
        }

        // Все дети должны быть matched
        return childrenAMatched.Count == unitA.ChildIds.Count;
    }

    /// <summary>
    /// Обнаружить переупорядочивания между двумя units.
    /// </summary>
    public List<Reordering> DetectReorderings(
        CodeUnit unitA,
        CodeUnit unitB,
        Dictionary<string, FastPathMatchResult> matches
    )
    {
        _logger.LogDebug("Detecting reorderings between {IdA} and {IdB}", unitA.Id, unitB.Id);

        var reorderings = new List<Reordering>();

        // Построить mapping children A -> B
        var childMapping = new Dictionary<string, string>();
        var childrenA = unitA.ChildIds.ToList();
        var childrenB = unitB.ChildIds.ToList();

        foreach (var childIdA in childrenA)
        {
            if (
                matches.TryGetValue(childIdA, out var match)
                && unitB.ChildIds.Contains(match.UnitB.Id)
            )
            {
                childMapping[childIdA] = match.UnitB.Id;
            }
        }

        // Найти переупорядочивания
        for (int i = 0; i < childrenA.Count; i++)
        {
            var childIdA = childrenA[i];

            if (!childMapping.TryGetValue(childIdA, out var childIdB))
                continue;

            var indexInA = i;
            var indexInB = childrenB.IndexOf(childIdB);

            if (indexInA != indexInB)
            {
                reorderings.Add(
                    new Reordering
                    {
                        UnitId = childIdA,
                        OriginalIndex = indexInA,
                        NewIndex = indexInB,
                        Description = $"{childIdA} moved from position {indexInA} to {indexInB}",
                    }
                );
            }
        }

        if (reorderings.Count > 0)
        {
            _logger.LogInformation(
                "Detected {Count} reorderings in {Id}",
                reorderings.Count,
                unitA.Id
            );
        }

        return reorderings;
    }

    /// <summary>
    /// Вычислить "canonical order" для children (по signature).
    /// </summary>
    public List<string> ComputeCanonicalOrder(
        List<string> childIds,
        Dictionary<string, CodeUnit> unitsById
    )
    {
        var children = childIds
            .Where(id => unitsById.ContainsKey(id))
            .Select(id => unitsById[id])
            .OrderBy(u => u.Signature ?? u.Name)
            .ThenBy(u => u.Name)
            .Select(u => u.Id)
            .ToList();

        return children;
    }

    /// <summary>
    /// Сравнить два units с canonical ordering.
    /// </summary>
    public bool AreEquivalentWithCanonicalOrdering(
        CodeUnit unitA,
        CodeUnit unitB,
        Dictionary<string, CodeUnit> unitsById
    )
    {
        if (unitA.ChildIds.Count != unitB.ChildIds.Count)
            return false;

        var canonicalA = ComputeCanonicalOrder(unitA.ChildIds.ToList(), unitsById);

        var canonicalB = ComputeCanonicalOrder(unitB.ChildIds.ToList(), unitsById);

        // Сравнить canonical порядки
        return canonicalA.SequenceEqual(canonicalB);
    }

    /// <summary>
    /// Определить, является ли изменение только reordering (без других изменений).
    /// </summary>
    public bool IsOnlyReordering(
        CodeUnit unitA,
        CodeUnit unitB,
        Dictionary<string, FastPathMatchResult> matches
    )
    {
        // 1. Structural hash должен отличаться (иначе нет изменений вообще)
        if (unitA.StructuralHash == unitB.StructuralHash)
            return false;

        // 2. Дети должны быть эквивалентны
        if (!AreChildrenEquivalent(unitA, unitB, matches))
            return false;

        // 3. Должны быть реальные переупорядочивания
        var reorderings = DetectReorderings(unitA, unitB, matches);

        return reorderings.Count > 0;
    }
}

/// <summary>
/// Переупорядочивание элемента.
/// </summary>
public sealed record Reordering
{
    public required string UnitId { get; init; }
    public required int OriginalIndex { get; init; }
    public required int NewIndex { get; init; }
    public required string Description { get; init; }
}

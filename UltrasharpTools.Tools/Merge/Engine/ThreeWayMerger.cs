using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Matching;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Engine;

/// <summary>
/// Three-way merge: base + branchA + branchB → merged.
/// Главный компонент Semantic Merge Engine.
/// </summary>
public sealed class ThreeWayMerger
{
private readonly FastPathMatcher _fastPathMatcher;
private readonly SemanticMatcher _semanticMatcher;
private readonly MovementDetector _movementDetector;
private readonly LazyEmbeddingGenerator _embeddingGenerator;
private readonly ILogger<ThreeWayMerger> _logger;

public ThreeWayMerger(
FastPathMatcher fastPathMatcher,
SemanticMatcher semanticMatcher,
MovementDetector movementDetector,
LazyEmbeddingGenerator embeddingGenerator,
ILogger<ThreeWayMerger>? logger = null)
{
_fastPathMatcher = fastPathMatcher;
_semanticMatcher = semanticMatcher;
_movementDetector = movementDetector;
_embeddingGenerator = embeddingGenerator;
_logger = logger ?? NullLogger<ThreeWayMerger>.Instance;
}

/// <summary>
/// Выполнить 3-way merge.
/// </summary>
public async Task<MergeResult> MergeAsync(
VersionedIndex baseIndex,
VersionedIndex branchAIndex,
VersionedIndex branchBIndex,
CancellationToken ct = default)
{
_logger.LogInformation(
"Starting 3-way merge: {Base} + {BranchA} + {BranchB}",
baseIndex.Version,
branchAIndex.Version,
branchBIndex.Version);

var sw = Stopwatch.StartNew();

// 1. Fast Path matching
var fastMatchesA = _fastPathMatcher.BulkMatch(baseIndex, branchAIndex);
var fastMatchesB = _fastPathMatcher.BulkMatch(baseIndex, branchBIndex);

_logger.LogInformation(
"Fast Path: A={CountA}, B={CountB}",
fastMatchesA.Count,
fastMatchesB.Count);

// 2. Найти unmatched units
var unmatchedA = FindUnmatchedUnits(baseIndex, branchAIndex, fastMatchesA);
var unmatchedB = FindUnmatchedUnits(baseIndex, branchBIndex, fastMatchesB);

_logger.LogInformation(
"Unmatched units: A={CountA}, B={CountB}",
unmatchedA.Count,
unmatchedB.Count);

// 3. Генерировать embeddings для unmatched (Slow Path)
if (unmatchedA.Count > 0)
{
var enrichedA = await _embeddingGenerator.GenerateEmbeddingsAsync(
unmatchedA,
ct);

// Обновить в индексе
foreach (var unit in enrichedA)
{
branchAIndex.Units[unit.Id] = unit;
}
}

if (unmatchedB.Count > 0)
{
var enrichedB = await _embeddingGenerator.GenerateEmbeddingsAsync(
unmatchedB,
ct);

foreach (var unit in enrichedB)
{
branchBIndex.Units[unit.Id] = unit;
}
}

// 4. Semantic matching для unmatched
var semanticMatchesA = await _semanticMatcher.FindSemanticMatchesBatchAsync(
unmatchedA,
branchAIndex,
ct);

var semanticMatchesB = await _semanticMatcher.FindSemanticMatchesBatchAsync(
unmatchedB,
branchBIndex,
ct);

_logger.LogInformation(
"Semantic matches: A={CountA}, B={CountB}",
semanticMatchesA.Count,
semanticMatchesB.Count);

// 5. Обнаружить movements
var movementsA = _movementDetector.DetectMovements(
baseIndex,
branchAIndex,
fastMatchesA,
semanticMatchesA);

var movementsB = _movementDetector.DetectMovements(
baseIndex,
branchBIndex,
fastMatchesB,
semanticMatchesB);

// 6. Анализ изменений и создание actions
var mergeActions = new List<MergeAction>();
var conflicts = new List<SemanticConflict>();

// Обработать matched units
ProcessMatchedUnits(
baseIndex,
branchAIndex,
branchBIndex,
fastMatchesA,
fastMatchesB,
semanticMatchesA,
semanticMatchesB,
mergeActions,
conflicts);

// Обработать added units (новые в A или B)
ProcessAddedUnits(
baseIndex,
branchAIndex,
branchBIndex,
fastMatchesA,
fastMatchesB,
mergeActions);

// Обработать deleted units (удалённые в A или B)
ProcessDeletedUnits(
baseIndex,
branchAIndex,
branchBIndex,
fastMatchesA,
fastMatchesB,
mergeActions,
conflicts);

sw.Stop();

// 7. Собрать статистику
var statistics = new MergeStatistics
{
TotalChanges = mergeActions.Count + conflicts.Count,
AutoMergedChanges = mergeActions.Count,
ConflictCount = conflicts.Count,
FastPathMatches = fastMatchesA.Count + fastMatchesB.Count,
SlowPathMatches = semanticMatchesA.Count + semanticMatchesB.Count,
MergeTimeMs = sw.ElapsedMilliseconds
};

_logger.LogInformation(
"Merge completed: {Actions} actions, {Conflicts} conflicts in {Time}ms",
mergeActions.Count,
conflicts.Count,
sw.ElapsedMilliseconds);

return new MergeResult
{
Actions = mergeActions,
Conflicts = conflicts,
Statistics = statistics
};
}

/// <summary>
/// Найти unmatched units.
/// </summary>
private List<CodeUnit> FindUnmatchedUnits(
VersionedIndex baseIndex,
VersionedIndex targetIndex,
Dictionary<string, FastPathMatchResult> fastMatches)
{
return baseIndex.Units.Values
.Where(u => !fastMatches.ContainsKey(u.Id))
.ToList();
}

/// <summary>
/// Обработать matched units (существуют в обеих ветках).
/// </summary>
private void ProcessMatchedUnits(
VersionedIndex baseIndex,
VersionedIndex branchAIndex,
VersionedIndex branchBIndex,
Dictionary<string, FastPathMatchResult> fastMatchesA,
Dictionary<string, FastPathMatchResult> fastMatchesB,
Dictionary<string, SemanticMatchResult> semanticMatchesA,
Dictionary<string, SemanticMatchResult> semanticMatchesB,
List<MergeAction> actions,
List<SemanticConflict> conflicts)
{
foreach (var baseUnit in baseIndex.Units.Values)
{
// Найти в обеих ветках
FastPathMatchResult? matchA = null;
SemanticMatchResult? semanticA = null;
var hasMatchA = fastMatchesA.TryGetValue(baseUnit.Id, out matchA) ||
semanticMatchesA.TryGetValue(baseUnit.Id, out semanticA);

FastPathMatchResult? matchB = null;
SemanticMatchResult? semanticB = null;
var hasMatchB = fastMatchesB.TryGetValue(baseUnit.Id, out matchB) ||
semanticMatchesB.TryGetValue(baseUnit.Id, out semanticB);

if (!hasMatchA || !hasMatchB)
continue;

var unitA = matchA?.UnitB ?? semanticA?.TargetUnit;
var unitB = matchB?.UnitB ?? semanticB?.TargetUnit;

if (unitA == null || unitB == null)
continue;

// Случай 1: Обе ветки идентичны base (нет изменений)
if (unitA.ContentHash == baseUnit.ContentHash &&
unitB.ContentHash == baseUnit.ContentHash)
{
// Нет изменений - пропустить
continue;
}

// Случай 2: Только A изменила
if (unitA.ContentHash != baseUnit.ContentHash &&
unitB.ContentHash == baseUnit.ContentHash)
{
actions.Add(CreateMergeAction(
unitA,
MergeActionType.Update,
"branchA",
1.0f));
continue;
}

// Случай 3: Только B изменила
if (unitA.ContentHash == baseUnit.ContentHash &&
unitB.ContentHash != baseUnit.ContentHash)
{
actions.Add(CreateMergeAction(
unitB,
MergeActionType.Update,
"branchB",
1.0f));
continue;
}

// Случай 4: Обе ветки изменили одинаково
if (unitA.ContentHash == unitB.ContentHash)
{
actions.Add(CreateMergeAction(
unitA,
MergeActionType.Update,
"merged",
1.0f));
continue;
}

// Случай 5: Конфликт - обе ветки изменили по-разному
conflicts.Add(CreateConflict(
baseUnit,
unitA,
unitB,
ConflictType.ContentConflict));
}
}

/// <summary>
/// Обработать добавленные units.
/// </summary>
private void ProcessAddedUnits(
VersionedIndex baseIndex,
VersionedIndex branchAIndex,
VersionedIndex branchBIndex,
Dictionary<string, FastPathMatchResult> fastMatchesA,
Dictionary<string, FastPathMatchResult> fastMatchesB,
List<MergeAction> actions)
{
// Units в A, но не в base
var addedInA = branchAIndex.Units.Values
.Where(u => !baseIndex.Units.ContainsKey(u.Id))
.ToList();

foreach (var unit in addedInA)
{
actions.Add(CreateMergeAction(
unit,
MergeActionType.Create,
"branchA",
0.95f));
}

// Units в B, но не в base
var addedInB = branchBIndex.Units.Values
.Where(u => !baseIndex.Units.ContainsKey(u.Id))
.ToList();

foreach (var unit in addedInB)
{
actions.Add(CreateMergeAction(
unit,
MergeActionType.Create,
"branchB",
0.95f));
}
}

/// <summary>
/// Обработать удалённые units.
/// </summary>
private void ProcessDeletedUnits(
VersionedIndex baseIndex,
VersionedIndex branchAIndex,
VersionedIndex branchBIndex,
Dictionary<string, FastPathMatchResult> fastMatchesA,
Dictionary<string, FastPathMatchResult> fastMatchesB,
List<MergeAction> actions,
List<SemanticConflict> conflicts)
{
foreach (var baseUnit in baseIndex.Units.Values)
{
var existsInA = fastMatchesA.ContainsKey(baseUnit.Id);
var existsInB = fastMatchesB.ContainsKey(baseUnit.Id);

// Удалено в обеих ветках
if (!existsInA && !existsInB)
{
actions.Add(CreateMergeAction(
baseUnit,
MergeActionType.Delete,
"merged",
1.0f));
continue;
}

// Удалено только в A
if (!existsInA && existsInB)
{
// TODO: Проверить, не была ли модифицирована в B
actions.Add(CreateMergeAction(
baseUnit,
MergeActionType.Delete,
"branchA",
0.8f));
}

// Удалено только в B
if (existsInA && !existsInB)
{
actions.Add(CreateMergeAction(
baseUnit,
MergeActionType.Delete,
"branchB",
0.8f));
}
}
}

/// <summary>
/// Создать MergeAction.
/// </summary>
private MergeAction CreateMergeAction(
CodeUnit unit,
MergeActionType type,
string source,
float confidence)
{
return new MergeAction
{
TargetPath = unit.FilePath,
Type = type,
Content = unit.Content,
Intent = new ChangeIntent
{
Type = IntentType.Modification,
Description = $"{type} {unit.Name}",
AffectedSymbols = new List<string> { unit.FullyQualifiedName },
Confidence = confidence
},
Confidence = confidence,
Source = source
};
}

/// <summary>
/// Создать SemanticConflict.
/// </summary>
private SemanticConflict CreateConflict(
CodeUnit baseUnit,
CodeUnit unitA,
CodeUnit unitB,
ConflictType conflictType)
{
return new SemanticConflict
{
Id = $"conflict-{Guid.NewGuid():N}",
BaseUnit = baseUnit,
VersionA = unitA,
VersionB = unitB,
ConflictType = conflictType,
Description = $"Conflicting changes in {baseUnit.FullyQualifiedName}",
SuggestedResolutions = new List<ConflictResolution>(),
Severity = ConflictSeverity.Medium
};
}
}

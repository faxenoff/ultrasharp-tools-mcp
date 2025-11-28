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
public sealed class ThreeWayMerger {
    private readonly FastPathMatcher _fastPathMatcher;
    private readonly SemanticMatcher _semanticMatcher;
    private readonly MovementDetector _movementDetector;
    private readonly LazyEmbeddingGenerator _embeddingGenerator;
    private readonly SemanticConflictResolver _conflictResolver;
    private readonly ILogger<ThreeWayMerger> _logger;

    public ThreeWayMerger(
        FastPathMatcher fastPathMatcher,
        SemanticMatcher semanticMatcher,
        MovementDetector movementDetector,
        LazyEmbeddingGenerator embeddingGenerator,
        SemanticConflictResolver conflictResolver,
        ILogger<ThreeWayMerger>? logger = null
    ) {
        _fastPathMatcher = fastPathMatcher;
        _semanticMatcher = semanticMatcher;
        _movementDetector = movementDetector;
        _embeddingGenerator = embeddingGenerator;
        _conflictResolver = conflictResolver;
        _logger = logger ?? NullLogger<ThreeWayMerger>.Instance;
    }

    /// <summary>
    /// Выполнить 3-way merge.
    /// </summary>
    public async Task<MergeResult> MergeAsync(
        VersionedIndex baseIndex,
        VersionedIndex branchAIndex,
        VersionedIndex branchBIndex,
        CancellationToken ct = default
    ) {
        _logger.LogDebug(
            "[3WAY] === Starting 3-way merge: {Base} + {BranchA} + {BranchB} ===",
            baseIndex.Version,
            branchAIndex.Version,
            branchBIndex.Version
        );
        _logger.LogDebug("[3WAY] Base units: {Count}", baseIndex.Units.Count);
        _logger.LogDebug("[3WAY] BranchA units: {Count}", branchAIndex.Units.Count);
        _logger.LogDebug("[3WAY] BranchB units: {Count}", branchBIndex.Units.Count);

        var sw = Stopwatch.StartNew();

        // 1. Fast Path matching
        _logger.LogDebug("[3WAY] Step 1: Fast Path matching...");
        var fastMatchesA = _fastPathMatcher.BulkMatch(baseIndex, branchAIndex);
        var fastMatchesB = _fastPathMatcher.BulkMatch(baseIndex, branchBIndex);

        _logger.LogDebug(
            "[3WAY] Fast Path completed: A={CountA}, B={CountB}",
            fastMatchesA.Count,
            fastMatchesB.Count
        );

        // 2. Найти unmatched units
        _logger.LogDebug("[3WAY] Step 2: Finding unmatched units...");
        var unmatchedA = FindUnmatchedUnits(baseIndex, branchAIndex, fastMatchesA);
        var unmatchedB = FindUnmatchedUnits(baseIndex, branchBIndex, fastMatchesB);

        _logger.LogDebug(
            "[3WAY] Unmatched units: A={CountA}, B={CountB}",
            unmatchedA.Count,
            unmatchedB.Count
        );

        // 3. Генерировать embeddings для unmatched (Slow Path)
        _logger.LogDebug("[3WAY] Step 3: Generating embeddings for unmatched units (Slow Path)...");
        if (unmatchedA.Count > 0) {
            _logger.LogDebug("[3WAY] Generating embeddings for {Count} unmatched units in branch A...", unmatchedA.Count);
            var enrichedA = await _embeddingGenerator.GenerateEmbeddingsAsync(unmatchedA, ct);
            _logger.LogDebug("[3WAY] Embeddings generated for branch A: {Count} units", enrichedA.Count);

            // Обновить в индексе
            foreach (var unit in enrichedA) {
                branchAIndex.Units[unit.Id] = unit;
            }
        }

        if (unmatchedB.Count > 0) {
            _logger.LogDebug("[3WAY] Generating embeddings for {Count} unmatched units in branch B...", unmatchedB.Count);
            var enrichedB = await _embeddingGenerator.GenerateEmbeddingsAsync(unmatchedB, ct);
            _logger.LogDebug("[3WAY] Embeddings generated for branch B: {Count} units", enrichedB.Count);

            foreach (var unit in enrichedB) {
                branchBIndex.Units[unit.Id] = unit;
            }
        }

        // 4. Semantic matching для unmatched
        _logger.LogDebug("[3WAY] Step 4: Semantic matching for unmatched units...");
        var semanticMatchesA = await _semanticMatcher.FindSemanticMatchesBatchAsync(
            unmatchedA,
            branchAIndex,
            ct
        );

        var semanticMatchesB = await _semanticMatcher.FindSemanticMatchesBatchAsync(
            unmatchedB,
            branchBIndex,
            ct
        );

        _logger.LogDebug(
            "[3WAY] Semantic matches completed: A={CountA}, B={CountB}",
            semanticMatchesA.Count,
            semanticMatchesB.Count
        );

        // 5. Обнаружить movements
        _logger.LogDebug("[3WAY] Step 5: Detecting movements...");
        var movementsA = _movementDetector.DetectMovements(
            baseIndex,
            branchAIndex,
            fastMatchesA,
            semanticMatchesA
        );

        var movementsB = _movementDetector.DetectMovements(
            baseIndex,
            branchBIndex,
            fastMatchesB,
            semanticMatchesB
        );
        _logger.LogDebug("[3WAY] Movements detected: A={CountA}, B={CountB}", movementsA.Count, movementsB.Count);

        // 6. Анализ изменений и создание actions
        _logger.LogDebug("[3WAY] Step 6: Processing changes and creating actions...");
        var mergeActions = new List<MergeAction>();
        var conflicts = new List<SemanticConflict>();

        // Обработать matched units (с семантическим разрешением конфликтов)
        _logger.LogDebug("[3WAY] Processing matched units...");
        await ProcessMatchedUnitsAsync(
            baseIndex,
            branchAIndex,
            branchBIndex,
            fastMatchesA,
            fastMatchesB,
            semanticMatchesA,
            semanticMatchesB,
            mergeActions,
            conflicts,
            ct
        );
        _logger.LogDebug("[3WAY] After matched: {Actions} actions, {Conflicts} conflicts", mergeActions.Count, conflicts.Count);

        // Обработать added units (новые в A или B)
        _logger.LogDebug("[3WAY] Processing added units...");
        ProcessAddedUnits(
            baseIndex,
            branchAIndex,
            branchBIndex,
            fastMatchesA,
            fastMatchesB,
            mergeActions
        );
        _logger.LogDebug("[3WAY] After added: {Actions} actions", mergeActions.Count);

        // Обработать deleted units (удалённые в A или B)
        _logger.LogDebug("[3WAY] Processing deleted units...");
        ProcessDeletedUnits(
            baseIndex,
            branchAIndex,
            branchBIndex,
            fastMatchesA,
            fastMatchesB,
            mergeActions,
            conflicts
        );
        _logger.LogDebug("[3WAY] After deleted: {Actions} actions, {Conflicts} conflicts", mergeActions.Count, conflicts.Count);

        sw.Stop();

        // 7. Собрать статистику
        _logger.LogDebug("[3WAY] Step 7: Collecting statistics...");
        var statistics = new MergeStatistics {
            TotalChanges = mergeActions.Count + conflicts.Count,
            AutoMergedChanges = mergeActions.Count,
            ConflictCount = conflicts.Count,
            FastPathMatches = fastMatchesA.Count + fastMatchesB.Count,
            SlowPathMatches = semanticMatchesA.Count + semanticMatchesB.Count,
            MergeTimeMs = sw.ElapsedMilliseconds,
        };

        _logger.LogDebug(
            "[3WAY] === Merge completed: {Actions} actions, {Conflicts} conflicts in {Time}ms ===",
            mergeActions.Count,
            conflicts.Count,
            sw.ElapsedMilliseconds
        );

        return new MergeResult {
            Actions = mergeActions,
            Conflicts = conflicts,
            Statistics = statistics,
        };
    }

    /// <summary>
    /// Найти unmatched units.
    /// </summary>
    private List<CodeUnit> FindUnmatchedUnits(
        VersionedIndex baseIndex,
        VersionedIndex targetIndex,
        Dictionary<string, FastPathMatchResult> fastMatches
    ) {
        return baseIndex.Units.Values.Where(u => !fastMatches.ContainsKey(u.Id)).ToList();
    }
    /// <summary>
    /// Обработать matched units (существуют в обеих ветках).
    /// </summary>
    private async Task ProcessMatchedUnitsAsync(
        VersionedIndex baseIndex,
        VersionedIndex branchAIndex,
        VersionedIndex branchBIndex,
        Dictionary<string, FastPathMatchResult> fastMatchesA,
        Dictionary<string, FastPathMatchResult> fastMatchesB,
        Dictionary<string, SemanticMatchResult> semanticMatchesA,
        Dictionary<string, SemanticMatchResult> semanticMatchesB,
        List<MergeAction> actions,
        List<SemanticConflict> conflicts,
        CancellationToken ct
    ) {
        var potentialConflicts = new List<(CodeUnit Base, CodeUnit A, CodeUnit B)>();

        // ВАЖНО: Обрабатываем только File-level units чтобы избежать перезаписи файлов
        // частичным контентом от Type/Method/Property units.
        // File-level units содержат полный контент файла.
        foreach (var baseUnit in baseIndex.Units.Values.Where(u => u.Type == CodeUnitType.File)) {
            // Найти в обеих ветках
            FastPathMatchResult? matchA = null;
            SemanticMatchResult? semanticA = null;
            var hasMatchA =
                fastMatchesA.TryGetValue(baseUnit.Id, out matchA)
                || semanticMatchesA.TryGetValue(baseUnit.Id, out semanticA);

            FastPathMatchResult? matchB = null;
            SemanticMatchResult? semanticB = null;
            var hasMatchB =
                fastMatchesB.TryGetValue(baseUnit.Id, out matchB)
                || semanticMatchesB.TryGetValue(baseUnit.Id, out semanticB);

            if (!hasMatchA || !hasMatchB)
                continue;

            var unitA = matchA?.UnitB ?? semanticA?.TargetUnit;
            var unitB = matchB?.UnitB ?? semanticB?.TargetUnit;

            if (unitA == null || unitB == null)
                continue;

            // Случай 1: Обе ветки идентичны base (нет изменений)
            if (
                unitA.ContentHash == baseUnit.ContentHash
                && unitB.ContentHash == baseUnit.ContentHash
            ) {
                // Нет изменений - пропустить
                continue;
            }

            // Случай 2: Только A изменила
            if (
                unitA.ContentHash != baseUnit.ContentHash
                && unitB.ContentHash == baseUnit.ContentHash
            ) {
                _logger.LogDebug("[3WAY] File changed only in A: {Path}", baseUnit.FilePath);
                actions.Add(CreateMergeAction(unitA, MergeActionType.Update, "branchA", 1.0f));
                continue;
            }

            // Случай 3: Только B изменила
            if (
                unitA.ContentHash == baseUnit.ContentHash
                && unitB.ContentHash != baseUnit.ContentHash
            ) {
                _logger.LogDebug("[3WAY] File changed only in B: {Path}", baseUnit.FilePath);
                actions.Add(CreateMergeAction(unitB, MergeActionType.Update, "branchB", 1.0f));
                continue;
            }

            // Случай 4: Обе ветки изменили одинаково
            if (unitA.ContentHash == unitB.ContentHash) {
                _logger.LogDebug("[3WAY] File changed identically in both branches: {Path}", baseUnit.FilePath);
                actions.Add(CreateMergeAction(unitA, MergeActionType.Update, "merged", 1.0f));
                continue;
            }

            // Случай 5: Потенциальный конфликт - обе ветки изменили по-разному
            // Собираем для семантического разрешения
            _logger.LogDebug("[3WAY] File has potential conflict: {Path}", baseUnit.FilePath);
            potentialConflicts.Add((baseUnit, unitA, unitB));
        }

        // Попытка семантически разрешить конфликты
        if (potentialConflicts.Count > 0) {
            _logger.LogDebug("[3WAY] Attempting to resolve {Count} potential conflicts semantically...", potentialConflicts.Count);

            var resolved = 0;
            foreach (var (baseUnit, unitA, unitB) in potentialConflicts) {
                ct.ThrowIfCancellationRequested();

                var resolution = await _conflictResolver.TryResolveAsync(baseUnit, unitA, unitB, ct);

                if (resolution.IsResolved && resolution.MergedUnit != null) {
                    actions.Add(CreateMergeAction(
                        resolution.MergedUnit,
                        MergeActionType.Update,
                        $"semantic-merge ({resolution.Resolution})",
                        0.9f));
                    resolved++;
                    _logger.LogDebug("[3WAY] Resolved conflict for {Symbol}: {Resolution}",
                        baseUnit.FullyQualifiedName, resolution.Resolution);
                } else {
                    conflicts.Add(CreateConflict(baseUnit, unitA, unitB, ConflictType.ContentConflict));
                }
            }

            _logger.LogDebug("[3WAY] Semantic conflict resolution: {Resolved}/{Total} resolved",
                resolved, potentialConflicts.Count);
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
        List<MergeAction> actions
    ) {
        // Units в A, но не в base
        var addedInA = branchAIndex
            .Units.Values.Where(u => !baseIndex.Units.ContainsKey(u.Id))
            .ToList();

        foreach (var unit in addedInA) {
            actions.Add(CreateMergeAction(unit, MergeActionType.Create, "branchA", 0.95f));
        }

        // Units в B, но не в base
        var addedInB = branchBIndex
            .Units.Values.Where(u => !baseIndex.Units.ContainsKey(u.Id))
            .ToList();

        foreach (var unit in addedInB) {
            actions.Add(CreateMergeAction(unit, MergeActionType.Create, "branchB", 0.95f));
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
        List<SemanticConflict> conflicts
    ) {
        foreach (var baseUnit in baseIndex.Units.Values) {
            var existsInA = fastMatchesA.ContainsKey(baseUnit.Id);
            var existsInB = fastMatchesB.ContainsKey(baseUnit.Id);

            // Удалено в обеих ветках
            if (!existsInA && !existsInB) {
                actions.Add(CreateMergeAction(baseUnit, MergeActionType.Delete, "merged", 1.0f));
                continue;
            }

            // Удалено только в A
            if (!existsInA && existsInB) {
                // TODO: Проверить, не была ли модифицирована в B
                actions.Add(CreateMergeAction(baseUnit, MergeActionType.Delete, "branchA", 0.8f));
            }

            // Удалено только в B
            if (existsInA && !existsInB) {
                actions.Add(CreateMergeAction(baseUnit, MergeActionType.Delete, "branchB", 0.8f));
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
        float confidence
    ) {
        return new MergeAction {
            TargetPath = unit.FilePath,
            Type = type,
            Content = unit.Content,
            Intent = new ChangeIntent {
                Type = IntentType.Modification,
                Description = $"{type} {unit.Name}",
                AffectedSymbols = new List<string> { unit.FullyQualifiedName },
                Confidence = confidence,
            },
            Confidence = confidence,
            Source = source,
        };
    }

    /// <summary>
    /// Создать SemanticConflict.
    /// </summary>
    private SemanticConflict CreateConflict(
        CodeUnit baseUnit,
        CodeUnit unitA,
        CodeUnit unitB,
        ConflictType conflictType
    ) {
        return new SemanticConflict {
            Id = $"conflict-{Guid.NewGuid():N}",
            BaseUnit = baseUnit,
            VersionA = unitA,
            VersionB = unitB,
            ConflictType = conflictType,
            Description = $"Conflicting changes in {baseUnit.FullyQualifiedName}",
            SuggestedResolutions = new List<ConflictResolution>(),
            Severity = ConflictSeverity.Medium,
        };
    }
}

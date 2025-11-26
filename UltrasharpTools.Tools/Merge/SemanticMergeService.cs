using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Merge.Engine;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge;

/// <summary>
/// Главный сервис для Semantic Merge.
/// Координирует все компоненты: индексация, matching, merging.
/// </summary>
public sealed class SemanticMergeService {
    private readonly MultiVersionIndexer _indexer;
    private readonly ThreeWayMerger _merger;
    private readonly ILogger<SemanticMergeService> _logger;

    public SemanticMergeService(
        MultiVersionIndexer indexer,
        ThreeWayMerger merger,
        ILogger<SemanticMergeService>? logger = null
    ) {
        _indexer = indexer;
        _merger = merger;
        _logger = logger ?? NullLogger<SemanticMergeService>.Instance;
    }

    /// <summary>
    /// Выполнить полный semantic merge workflow.
    /// </summary>
    public async Task<MergeResult> MergeAsync(
        IndexingRequest request,
        CancellationToken ct = default
    ) {
        _logger.LogDebug("Starting Semantic Merge workflow");

        // 1. Индексация всех версий
        _logger.LogDebug("Step 1: Indexing versions");
        var indexResult = await _indexer.IndexAllVersionsAsync(request, ct);

        // 2. Three-way merge
        _logger.LogDebug("Step 2: Three-way merge");
        var mergeResult = await _merger.MergeAsync(
            indexResult.BaseIndex,
            indexResult.BranchAIndex,
            indexResult.BranchBIndex,
            ct
        );

        _logger.LogDebug(
            "Semantic Merge completed: {Success}, {Actions} actions, {Conflicts} conflicts",
            mergeResult.IsSuccess,
            mergeResult.Actions.Count,
            mergeResult.Conflicts.Count
        );

        return mergeResult;
    }

    /// <summary>
    /// Получить summary мерджа.
    /// </summary>
    public string GetMergeSummary(MergeResult result) {
        var summary =
            $@"
=== Semantic Merge Summary ===
Status: {(result.IsSuccess ? "SUCCESS" : "CONFLICTS DETECTED")}

Statistics:
- Total changes: {result.Statistics.TotalChanges}
- Auto-merged: {result.Statistics.AutoMergedChanges}
- Conflicts: {result.Statistics.ConflictCount}
- Fast Path matches: {result.Statistics.FastPathMatches}
- Slow Path (semantic) matches: {result.Statistics.SlowPathMatches}
- Merge time: {result.Statistics.MergeTimeMs}ms

Actions:
- Create: {result.Actions.Count(a => a.Type == MergeActionType.Create)}
- Update: {result.Actions.Count(a => a.Type == MergeActionType.Update)}
- Delete: {result.Actions.Count(a => a.Type == MergeActionType.Delete)}
- Move: {result.Actions.Count(a => a.Type == MergeActionType.Move)}
- Rename: {result.Actions.Count(a => a.Type == MergeActionType.Rename)}
";

        if (result.Conflicts.Count > 0) {
            var sb = ObjectPoolProvider.Instance.GetStringBuilder();
            try {
                sb.Append(summary);
                sb.Append("\nConflicts:\n");

                foreach (var conflict in result.Conflicts.Take(10)) {
                    sb.Append("- ");
                    sb.Append(conflict.Description);
                    sb.Append(" (severity: ");
                    sb.Append(conflict.Severity);
                    sb.Append(")\n");
                }

                if (result.Conflicts.Count > 10) {
                    sb.Append("... and ");
                    sb.Append(result.Conflicts.Count - 10);
                    sb.Append(" more\n");
                }

                return sb.ToString();
            } finally {
                ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
            }
        }

        return summary;
    }
    /// <summary>
    /// Выполнить semantic merge с инструкциями.
    /// </summary>
    public async Task<MergeResult> MergeWithInstructionsAsync(
        VersionedIndex baseIndex,
        VersionedIndex sourceIndex,
        VersionedIndex targetIndex,
        MergeInstructions? instructions = null,
        CancellationToken ct = default) {
        _logger.LogDebug(
            "Starting Semantic Merge with instructions: {Source} -> {Target}",
            sourceIndex.Version, targetIndex.Version);

        if (instructions != null && !string.IsNullOrEmpty(instructions.RawInstructions)) {
            _logger.LogDebug("Merge instructions: {Instructions}", instructions.RawInstructions);
        }

        // Выполнить merge
        var mergeResult = await _merger.MergeAsync(baseIndex, sourceIndex, targetIndex, ct);

        // Применить инструкции к результату
        if (instructions != null) {
            mergeResult = ApplyInstructions(mergeResult, instructions, sourceIndex, targetIndex);
        }

        _logger.LogDebug(
            "Merge with instructions completed: {Actions} actions, {Conflicts} conflicts",
            mergeResult.Actions.Count,
            mergeResult.Conflicts.Count);

        return mergeResult;
    }

    private MergeResult ApplyInstructions(
        MergeResult result,
        MergeInstructions instructions,
        VersionedIndex sourceIndex,
        VersionedIndex targetIndex) {
        var filteredActions = new List<MergeAction>();
        var filteredConflicts = new List<SemanticConflict>();

        // Фильтровать actions по инструкциям
        foreach (var action in result.Actions) {
            // Проверить исключение файла
            if (instructions.ShouldExcludeFile(action.TargetPath)) {
                _logger.LogDebug("Excluded by instruction: {Path}", action.TargetPath);
                continue;
            }

            // Проверить приоритет ветки
            var priority = instructions.GetBranchPriority(action.TargetPath, action.Content);

            if (priority == BranchPriority.PreferTarget && action.Source.Contains("source", StringComparison.OrdinalIgnoreCase)) {
                // Пропустить изменение из source
                continue;
            }

            if (priority == BranchPriority.PreferSource && action.Source.Contains("target", StringComparison.OrdinalIgnoreCase)) {
                // Взять версию из source вместо target
                var sourceUnit = sourceIndex.Units.Values.FirstOrDefault(u => u.FilePath == action.TargetPath);
                if (sourceUnit != null) {
                    filteredActions.Add(action with { Content = sourceUnit.Content, Source = "source (instruction)" });
                    continue;
                }
            }

            filteredActions.Add(action);
        }

        // Авто-разрешить конфликты по инструкциям
        if (instructions.AutoResolveConflicts) {
            foreach (var conflict in result.Conflicts) {
                var priority = instructions.GetBranchPriority(
                    conflict.BaseUnit?.FilePath ?? "",
                    conflict.VersionA?.Content ?? conflict.VersionB?.Content);

                if (priority == BranchPriority.PreferSource && conflict.VersionA != null) {
                    // Авто-разрешить: взять source
                    filteredActions.Add(new MergeAction {
                        TargetPath = conflict.VersionA.FilePath,
                        Type = MergeActionType.Update,
                        Content = conflict.VersionA.Content,
                        Intent = new ChangeIntent {
                            Type = IntentType.Modification,
                            Description = "Auto-resolved by instruction: prefer source",
                            AffectedSymbols = new List<string> { conflict.VersionA.FullyQualifiedName },
                            Confidence = 0.9f,
                        },
                        Source = "source (auto-resolved)",
                        Confidence = 0.9f,
                    });
                    continue;
                }

                if (priority == BranchPriority.PreferTarget && conflict.VersionB != null) {
                    // Авто-разрешить: взять target
                    filteredActions.Add(new MergeAction {
                        TargetPath = conflict.VersionB.FilePath,
                        Type = MergeActionType.Update,
                        Content = conflict.VersionB.Content,
                        Intent = new ChangeIntent {
                            Type = IntentType.Modification,
                            Description = "Auto-resolved by instruction: prefer target",
                            AffectedSymbols = new List<string> { conflict.VersionB.FullyQualifiedName },
                            Confidence = 0.9f,
                        },
                        Source = "target (auto-resolved)",
                        Confidence = 0.9f,
                    });
                    continue;
                }

                // Не удалось авто-разрешить
                filteredConflicts.Add(conflict);
            }
        } else {
            filteredConflicts.AddRange(result.Conflicts);
        }

        return result with {
            Actions = filteredActions,
            Conflicts = filteredConflicts,
            Statistics = result.Statistics with {
                AutoMergedChanges = filteredActions.Count,
                ConflictCount = filteredConflicts.Count,
            }
        };
    }
}

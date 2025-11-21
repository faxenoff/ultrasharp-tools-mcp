
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Engine;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge;

/// <summary>
/// Главный сервис для Semantic Merge.
/// Координирует все компоненты: индексация, matching, merging.
/// </summary>
public sealed class SemanticMergeService
{
private readonly MultiVersionIndexer _indexer;
private readonly ThreeWayMerger _merger;
private readonly ILogger<SemanticMergeService> _logger;

public SemanticMergeService(
MultiVersionIndexer indexer,
ThreeWayMerger merger,
ILogger<SemanticMergeService>? logger = null)
{
_indexer = indexer;
_merger = merger;
_logger = logger ?? NullLogger<SemanticMergeService>.Instance;
}

/// <summary>
/// Выполнить полный semantic merge workflow.
/// </summary>
public async Task<MergeResult> MergeAsync(
IndexingRequest request,
CancellationToken ct = default)
{
_logger.LogInformation(
"Starting Semantic Merge workflow");

// 1. Индексация всех версий
_logger.LogInformation("Step 1: Indexing versions");
var indexResult = await _indexer.IndexAllVersionsAsync(request, ct);

// 2. Three-way merge
_logger.LogInformation("Step 2: Three-way merge");
var mergeResult = await _merger.MergeAsync(
indexResult.BaseIndex,
indexResult.BranchAIndex,
indexResult.BranchBIndex,
ct);

_logger.LogInformation(
"Semantic Merge completed: {Success}, {Actions} actions, {Conflicts} conflicts",
mergeResult.IsSuccess,
mergeResult.Actions.Count,
mergeResult.Conflicts.Count);

return mergeResult;
}

/// <summary>
/// Получить summary мерджа.
/// </summary>
public string GetMergeSummary(MergeResult result)
{
var summary = $@"
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

if (result.Conflicts.Count > 0)
{
summary += "\nConflicts:\n";
foreach (var conflict in result.Conflicts.Take(10))
{
summary += $"- {conflict.Description} (severity: {conflict.Severity})\n";
}

if (result.Conflicts.Count > 10)
{
summary += $"... and {result.Conflicts.Count - 10} more\n";
}
}

return summary;
}
}

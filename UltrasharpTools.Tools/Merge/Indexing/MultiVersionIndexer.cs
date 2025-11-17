using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Индексация кода для 4 версий: base, branchA, branchB, merged.
/// Создаёт VersionedIndex для каждой версии.
/// </summary>
public sealed class MultiVersionIndexer
{
private readonly CodeUnitExtractor _extractor;
private readonly ILogger<MultiVersionIndexer> _logger;

public MultiVersionIndexer(
CodeUnitExtractor extractor,
ILogger<MultiVersionIndexer>? logger = null)
{
_extractor = extractor;
_logger = logger ?? NullLogger<MultiVersionIndexer>.Instance;
}

/// <summary>
/// Индексировать одну версию кода.
/// </summary>
public async Task<VersionedIndex> IndexVersionAsync(
string versionName,
string directoryPath,
string[]? filePatterns = null,
string? commitSha = null,
string? branchName = null,
CancellationToken ct = default)
{
var sw = Stopwatch.StartNew();

_logger.LogInformation(
"Indexing version {Version} from {Directory}",
versionName,
directoryPath);

// Default patterns
filePatterns ??= new[] { "*.cs", "*.json" };

// 1. Извлечь CodeUnits
var units = await _extractor.ExtractFromDirectoryAsync(
directoryPath,
filePatterns,
ct);

// 2. Построить иерархию
_extractor.BuildHierarchy(units);

// 3. Создать словарь units
var unitsDict = units.ToDictionary(u => u.Id);

// 4. Создать VectorStore (пустой, embeddings добавятся позже)
var vectorStore = new VectorStore();

// 5. Собрать статистику
var statistics = ComputeStatistics(units, sw.ElapsedMilliseconds);

sw.Stop();

_logger.LogInformation(
"Indexed version {Version}: {Count} units in {Time}ms",
versionName,
units.Count,
sw.ElapsedMilliseconds);

return new VersionedIndex
{
Version = versionName,
CommitSha = commitSha,
BranchName = branchName,
Units = unitsDict,
VectorStore = vectorStore,
CreatedAt = DateTimeOffset.UtcNow,
Statistics = statistics
};
}

/// <summary>
/// Индексировать все 4 версии для 3-way merge.
/// </summary>
public async Task<MultiVersionIndexResult> IndexAllVersionsAsync(
IndexingRequest request,
CancellationToken ct = default)
{
_logger.LogInformation(
"Starting multi-version indexing for 3-way merge");

var sw = Stopwatch.StartNew();

// Индексировать base версию
var baseIndex = await IndexVersionAsync(
"base",
request.BaseDirectory,
request.FilePatterns,
request.BaseCommitSha,
request.BaseBranch,
ct);

// Индексировать branchA
var branchAIndex = await IndexVersionAsync(
"branchA",
request.BranchADirectory,
request.FilePatterns,
request.BranchACommitSha,
request.BranchA,
ct);

// Индексировать branchB
var branchBIndex = await IndexVersionAsync(
"branchB",
request.BranchBDirectory,
request.FilePatterns,
request.BranchBCommitSha,
request.BranchB,
ct);

// Merged версия будет создана позже
VersionedIndex? mergedIndex = null;
if (request.MergedDirectory != null)
{
mergedIndex = await IndexVersionAsync(
"merged",
request.MergedDirectory,
request.FilePatterns,
request.MergedCommitSha,
request.MergedBranch,
ct);
}

sw.Stop();

_logger.LogInformation(
"Multi-version indexing completed in {Time}ms",
sw.ElapsedMilliseconds);

return new MultiVersionIndexResult
{
BaseIndex = baseIndex,
BranchAIndex = branchAIndex,
BranchBIndex = branchBIndex,
MergedIndex = mergedIndex,
TotalIndexingTimeMs = sw.ElapsedMilliseconds
};
}

/// <summary>
/// Вычислить статистику индекса.
/// </summary>
private IndexStatistics ComputeStatistics(
List<CodeUnit> units,
long indexingTimeMs)
{
var unitsByType = units
.GroupBy(u => u.Type)
.ToDictionary(g => g.Key, g => g.Count());

var fileCount = units.Count(u => u.Type == CodeUnitType.File);
var unitsWithEmbeddings = units.Count(u => u.Embedding != null);

return new IndexStatistics
{
TotalUnits = units.Count,
UnitsByType = unitsByType,
FileCount = fileCount,
UnitsWithEmbeddings = unitsWithEmbeddings,
IndexingTimeMs = indexingTimeMs
};
}
}

/// <summary>
/// Запрос на индексацию.
/// </summary>
public sealed record IndexingRequest
{
public required string BaseDirectory { get; init; }
public required string BranchADirectory { get; init; }
public required string BranchBDirectory { get; init; }
public string? MergedDirectory { get; init; }

public string[]? FilePatterns { get; init; }

public string? BaseCommitSha { get; init; }
public string? BranchACommitSha { get; init; }
public string? BranchBCommitSha { get; init; }
public string? MergedCommitSha { get; init; }

public string? BaseBranch { get; init; }
public string? BranchA { get; init; }
public string? BranchB { get; init; }
public string? MergedBranch { get; init; }
}

/// <summary>
/// Результат индексации всех версий.
/// </summary>
public sealed record MultiVersionIndexResult
{
public required VersionedIndex BaseIndex { get; init; }
public required VersionedIndex BranchAIndex { get; init; }
public required VersionedIndex BranchBIndex { get; init; }
public VersionedIndex? MergedIndex { get; init; }

public required long TotalIndexingTimeMs { get; init; }

public int TotalUnits =>
BaseIndex.Statistics.TotalUnits +
BranchAIndex.Statistics.TotalUnits +
BranchBIndex.Statistics.TotalUnits +
(MergedIndex?.Statistics.TotalUnits ?? 0);
}

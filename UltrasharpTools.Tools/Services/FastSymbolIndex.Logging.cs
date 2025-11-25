using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public sealed partial class FastSymbolIndex
{
    [LoggerMessage(EventId = 3830, Level = LogLevel.Information,
        Message = "Building fast symbol index from solution...")]
    private partial void LogBuildingIndex();

    [LoggerMessage(EventId = 3831, Level = LogLevel.Information,
        Message = "Valid cache found with {Count} symbols (created: {Created}). Attempting to restore from cache...")]
    private partial void LogValidCacheFound(int count, DateTimeOffset created);

    [LoggerMessage(EventId = 3832, Level = LogLevel.Information,
        Message = "Successfully restored {Count} symbols from cache in {ElapsedMs}ms")]
    private partial void LogCacheRestored(int count, long elapsedMs);

    [LoggerMessage(EventId = 3833, Level = LogLevel.Warning,
        Message = "Cache restoration failed or incomplete, falling back to full build")]
    private partial void LogCacheRestorationFailed();

    [LoggerMessage(EventId = 3834, Level = LogLevel.Warning,
        Message = "Could not get compilation for project: {ProjectName}")]
    private partial void LogCompilationFailed(string projectName);

    [LoggerMessage(EventId = 3835, Level = LogLevel.Debug,
        Message = "Collected {AddedCount} symbols from project {ProjectName} (total: {TotalCount})")]
    private partial void LogSymbolsCollected(int addedCount, string projectName, int totalCount);

    [LoggerMessage(EventId = 3836, Level = LogLevel.Debug,
        Message = "Collected {SymbolCount} unique symbols, building indices...")]
    private partial void LogBuildingIndices(int symbolCount);

    [LoggerMessage(EventId = 3837, Level = LogLevel.Information,
        Message = "Fast symbol index built: {SymbolCount} symbols indexed in {ElapsedMs}ms. Bloom filter: {BloomStats}. Namespaces: {NamespaceCount}")]
    private partial void LogIndexBuilt(int symbolCount, long elapsedMs, string bloomStats, int namespaceCount);

    [LoggerMessage(EventId = 3838, Level = LogLevel.Warning,
        Message = "Failed to save symbol cache (non-critical)")]
    private partial void LogSaveCacheFailed(Exception exception);

    [LoggerMessage(EventId = 3839, Level = LogLevel.Warning,
        Message = "Error indexing symbol: {SymbolName}")]
    private partial void LogIndexingError(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3840, Level = LogLevel.Warning,
        Message = "Compilation has null GlobalNamespace: {AssemblyName}")]
    private partial void LogNullGlobalNamespace(string? assemblyName);

    [LoggerMessage(EventId = 3841, Level = LogLevel.Debug,
        Message = "GlobalNamespace has {MemberCount} direct members")]
    private partial void LogGlobalNamespaceMembers(int memberCount);

    [LoggerMessage(EventId = 3842, Level = LogLevel.Debug,
        Message = "CollectSymbolsFromCompilation stats: Visited={Visited}, Skipped (Duplicates={Duplicates}, Implicit={Implicit}, ByKind={ByKind})")]
    private partial void LogCollectionStats(int visited, int duplicates, int @implicit, int byKind);

    [LoggerMessage(EventId = 3843, Level = LogLevel.Warning,
        Message = "No symbols found to index - created empty indices")]
    private partial void LogNoSymbolsFound();

    [LoggerMessage(EventId = 3844, Level = LogLevel.Debug,
        Message = "Rebuilt lookup structures for {SymbolCount} symbols")]
    private partial void LogLookupStructuresRebuilt(int symbolCount);

    [LoggerMessage(EventId = 3845, Level = LogLevel.Information,
        Message = "Restoring {Count} symbols from cache...")]
    private partial void LogRestoringFromCache(int count);

    [LoggerMessage(EventId = 3846, Level = LogLevel.Debug,
        Message = "Cache restoration progress: {Percent}% ({Processed}/{Total})")]
    private partial void LogCacheProgress(int percent, int processed, int total);

    [LoggerMessage(EventId = 3847, Level = LogLevel.Warning,
        Message = "Early cache check: success rate too low ({Rate:F1}% after {Count} symbols), aborting cache restoration")]
    private partial void LogEarlyCacheCheckFailed(double rate, int count);

    [LoggerMessage(EventId = 3848, Level = LogLevel.Information,
        Message = "Cache restoration complete: {Success}/{Total} symbols restored ({Rate:F1}%), {Failed} failed")]
    private partial void LogCacheRestorationComplete(int success, int total, double rate, int failed);

    [LoggerMessage(EventId = 3849, Level = LogLevel.Warning,
        Message = "Cache restoration success rate too low ({Rate:F1}%), discarding cache")]
    private partial void LogCacheDiscarded(double rate);

    [LoggerMessage(EventId = 3850, Level = LogLevel.Error,
        Message = "Error restoring symbols from cache")]
    private partial void LogCacheRestoreError(Exception exception);

    [LoggerMessage(EventId = 3851, Level = LogLevel.Debug,
        Message = "Incremental update: Processing document {DocumentId}")]
    private partial void LogIncrementalUpdate(DocumentId documentId);

    [LoggerMessage(EventId = 3852, Level = LogLevel.Warning,
        Message = "Document {DocumentId} not found in solution")]
    private partial void LogDocumentNotFound(DocumentId documentId);

    [LoggerMessage(EventId = 3853, Level = LogLevel.Information,
        Message = "Incremental update completed: {DocumentId}, removed {Removed} symbols, added {Added} symbols in {ElapsedMs}ms (update #{Counter})")]
    private partial void LogIncrementalUpdateComplete(DocumentId documentId, int removed, int added, long elapsedMs, int counter);

    [LoggerMessage(EventId = 3854, Level = LogLevel.Debug,
        Message = "Incremental add: Processing new document {DocumentId}")]
    private partial void LogIncrementalAdd(DocumentId documentId);

    [LoggerMessage(EventId = 3855, Level = LogLevel.Information,
        Message = "Incremental add completed: {DocumentId}, added {Added} symbols in {ElapsedMs}ms (update #{Counter})")]
    private partial void LogIncrementalAddComplete(DocumentId documentId, int added, long elapsedMs, int counter);

    [LoggerMessage(EventId = 3856, Level = LogLevel.Debug,
        Message = "Incremental remove: Processing deleted document {DocumentId}")]
    private partial void LogIncrementalRemove(DocumentId documentId);

    [LoggerMessage(EventId = 3857, Level = LogLevel.Information,
        Message = "Incremental remove completed: {DocumentId}, removed {Removed} symbols in {ElapsedMs}ms (update #{Counter})")]
    private partial void LogIncrementalRemoveComplete(DocumentId documentId, int removed, long elapsedMs, int counter);

    // Parallel compilation logging (3860-3869)
    [LoggerMessage(EventId = 3860, Level = LogLevel.Information,
        Message = "Starting parallel build: {ProjectCount} projects, {LevelCount} dependency levels, max parallelism = {MaxParallelism}")]
    private partial void LogParallelBuildStart(int projectCount, int levelCount, int maxParallelism);

    [LoggerMessage(EventId = 3861, Level = LogLevel.Debug,
        Message = "Completed level {Level}/{TotalLevels}: {ProjectCount} projects in {ElapsedMs}ms")]
    private partial void LogLevelCompleted(int level, int totalLevels, int projectCount, long elapsedMs);

    [LoggerMessage(EventId = 3862, Level = LogLevel.Information,
        Message = "Parallel build complete: {SymbolCount} symbols from {ProjectCount} projects")]
    private partial void LogParallelBuildComplete(int symbolCount, int projectCount);

    [LoggerMessage(EventId = 3863, Level = LogLevel.Warning,
        Message = "Error indexing project {ProjectName}")]
    private partial void LogProjectIndexingError(Exception exception, string projectName);
}

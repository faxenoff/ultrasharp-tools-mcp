using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class DiagnosticService
{
    // Analysis operations (3100-3119)
    [LoggerMessage(EventId = 3100, Level = LogLevel.Information,
        Message = "Starting diagnostic analysis for solution: {SolutionPath}, Preset: {Preset}, Ids: {Ids}")]
    private partial void LogStartingAnalysis(string solutionPath, string? preset, string? ids);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information,
        Message = "Starting semantic enrichment for {Count} diagnostics")]
    private partial void LogStartingSemanticEnrichment(int count);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information,
        Message = "Semantic enrichment complete. Clusters: {Clusters}, Categories: {Categories}")]
    private partial void LogSemanticEnrichmentComplete(int clusters, int categories);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Information,
        Message = "EditorConfig generation complete. Rules: {Rules}, Manual review: {ManualReview}")]
    private partial void LogEditorConfigGenerated(int rules, int manualReview);

    [LoggerMessage(EventId = 3104, Level = LogLevel.Warning,
        Message = "Failed to perform semantic enrichment")]
    private partial void LogSemanticEnrichmentFailed(Exception exception);

    [LoggerMessage(EventId = 3105, Level = LogLevel.Information,
        Message = "Diagnostic analysis complete. Total: {Total}, Filtered: {Filtered}, Returned: {Returned}")]
    private partial void LogAnalysisComplete(int total, int filtered, int returned);

    [LoggerMessage(EventId = 3106, Level = LogLevel.Information,
        Message = "Diagnostic cache cleared. Removed {Count} entries")]
    private partial void LogCacheCleared(int count);

    // Cache operations (3120-3129)
    [LoggerMessage(EventId = 3120, Level = LogLevel.Information,
        Message = "Using cached diagnostics for {SolutionPath} (age: {Age:F1}s)")]
    private partial void LogUsingCachedDiagnostics(string solutionPath, double age);

    [LoggerMessage(EventId = 3121, Level = LogLevel.Information,
        Message = "Cache expired for {SolutionPath} (age: {Age:F1}s)")]
    private partial void LogCacheExpired(string solutionPath, double age);

    [LoggerMessage(EventId = 3122, Level = LogLevel.Information,
        Message = "Cached {Count} diagnostics for {SolutionPath} from {ProjectCount} projects")]
    private partial void LogDiagnosticsCached(int count, string solutionPath, int projectCount);

    // Solution loading (3130-3139)
    [LoggerMessage(EventId = 3130, Level = LogLevel.Information,
        Message = "Loading solution: {SolutionPath}")]
    private partial void LogLoadingSolution(string solutionPath);

    [LoggerMessage(EventId = 3131, Level = LogLevel.Debug,
        Message = "Solution already loaded: {SolutionPath}")]
    private partial void LogSolutionAlreadyLoaded(string solutionPath);

    // Incremental analysis (3140-3149)
    [LoggerMessage(EventId = 3140, Level = LogLevel.Information,
        Message = "INCREMENTAL: Detected {ChangedCount} changed files (out of {TotalFiles} total files)")]
    private partial void LogIncrementalChangedFiles(int changedCount, int totalFiles);

    [LoggerMessage(EventId = 3141, Level = LogLevel.Information,
        Message = "INCREMENTAL: {AffectedCount} files affected by changes (potential {Savings:F1}% analysis skip)")]
    private partial void LogIncrementalAffectedFiles(int affectedCount, double savings);

    [LoggerMessage(EventId = 3142, Level = LogLevel.Information,
        Message = "INCREMENTAL: No changed files detected (100% cache hit potential)")]
    private partial void LogIncrementalNoChanges();

    [LoggerMessage(EventId = 3143, Level = LogLevel.Debug,
        Message = "INCREMENTAL: Using cached diagnostics for project {ProjectName} ({Count} diagnostics)")]
    private partial void LogIncrementalProjectCached(string projectName, int count);

    [LoggerMessage(EventId = 3144, Level = LogLevel.Information,
        Message = "INCREMENTAL: {CachedProjects}/{TotalProjects} projects fully cached, analyzing {NeedAnalysis} projects")]
    private partial void LogIncrementalCacheStatus(int cachedProjects, int totalProjects, int needAnalysis);

    // Project analysis (3150-3159)
    [LoggerMessage(EventId = 3150, Level = LogLevel.Information,
        Message = "Filtered to {FilteredProjects} projects: {ProjectNames}")]
    private partial void LogFilteredProjects(int filteredProjects, string projectNames);

    [LoggerMessage(EventId = 3151, Level = LogLevel.Information,
        Message = "Found {TotalProjects} compilable projects to analyze")]
    private partial void LogCompilableProjects(int totalProjects);

    [LoggerMessage(EventId = 3152, Level = LogLevel.Debug,
        Message = "Filtered analyzers for {ProjectName}: {FilteredCount}/{TotalCount} (requested IDs: {RequestedIds})")]
    private partial void LogFilteredAnalyzers(string projectName, int filteredCount, int totalCount, string requestedIds);

    // Project analysis additional (3160-3169)
    [LoggerMessage(EventId = 3160, Level = LogLevel.Debug,
        Message = "Analyzed project {ProjectName} with {AnalyzerCount} analyzers")]
    private partial void LogProjectAnalyzed(string projectName, int analyzerCount);

    [LoggerMessage(EventId = 3161, Level = LogLevel.Debug,
        Message = "Analyzer execution failed for {ProjectName}, falling back to compilation diagnostics")]
    private partial void LogAnalyzerFallback(Exception exception, string projectName);

    [LoggerMessage(EventId = 3162, Level = LogLevel.Debug,
        Message = "No analyzers found for project {ProjectName}, using compilation diagnostics only")]
    private partial void LogNoAnalyzers(string projectName);

    [LoggerMessage(EventId = 3163, Level = LogLevel.Debug,
        Message = "Cached {Count} diagnostics for file {FilePath}")]
    private partial void LogFileDiagnosticsCached(int count, string? filePath);

    [LoggerMessage(EventId = 3164, Level = LogLevel.Warning,
        Message = "Failed to analyze project: {ProjectName}")]
    private partial void LogProjectAnalysisFailed(Exception exception, string projectName);

    // File tracking (3170-3179)
    [LoggerMessage(EventId = 3170, Level = LogLevel.Debug,
        Message = "Found {ChangedCount} changed files")]
    private partial void LogChangedFilesFound(int changedCount);

    [LoggerMessage(EventId = 3171, Level = LogLevel.Debug,
        Message = "Building dependency graph...")]
    private partial void LogBuildingDependencyGraph();

    [LoggerMessage(EventId = 3172, Level = LogLevel.Debug,
        Message = "Dependency graph built with {Count} entries")]
    private partial void LogDependencyGraphBuilt(int count);

    [LoggerMessage(EventId = 3173, Level = LogLevel.Debug,
        Message = "Found dependency: {TypeName} depends on {DependencyCount} types")]
    private partial void LogDependencyFound(string typeName, int dependencyCount);

    [LoggerMessage(EventId = 3174, Level = LogLevel.Debug,
        Message = "Checking file {FilePath} for caching (diagnostics: {Count})")]
    private partial void LogCheckingFileForCache(string? filePath, int count);

    [LoggerMessage(EventId = 3175, Level = LogLevel.Debug,
        Message = "INCREMENTAL: Cached diagnostics for {FileCount} files in project {ProjectName}")]
    private partial void LogIncrementalFileCached(int fileCount, string projectName);

    [LoggerMessage(EventId = 3176, Level = LogLevel.Debug,
        Message = "Failed to build dependencies for {FilePath}")]
    private partial void LogBuildDependenciesFailed(Exception exception, string? filePath);

    [LoggerMessage(EventId = 3177, Level = LogLevel.Debug,
        Message = "Affected files: {AffectedCount} (changed: {ChangedCount})")]
    private partial void LogAffectedFiles(int affectedCount, int changedCount);
}

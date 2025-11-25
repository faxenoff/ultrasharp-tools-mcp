using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SymbolResolver
{
    [LoggerMessage(EventId = 3710, Level = LogLevel.Debug,
        Message = "Building type cache for {ProjectName}...")]
    private partial void LogBuildingTypeCache(string projectName);

    [LoggerMessage(EventId = 3711, Level = LogLevel.Debug,
        Message = "Built type cache for {ProjectName}: {TypeCount} types in {ElapsedMs}ms")]
    private partial void LogTypeCacheBuilt(string projectName, int typeCount, long elapsedMs);

    [LoggerMessage(EventId = 3712, Level = LogLevel.Trace,
        Message = "Multiple candidates for member {MemberName}, using first match")]
    private partial void LogMultipleCandidates(string memberName);

    [LoggerMessage(EventId = 3713, Level = LogLevel.Warning,
        Message = "Skipping {InvalidCount} symbols from {ProjectCount} invalid/missing projects: {Projects}")]
    private partial void LogSkippingInvalidSymbols(int invalidCount, int projectCount, string projects);

    [LoggerMessage(EventId = 3714, Level = LogLevel.Warning,
        Message = "No valid symbols to resolve after filtering")]
    private partial void LogNoValidSymbols();

    [LoggerMessage(EventId = 3715, Level = LogLevel.Information,
        Message = "Symbol resolution: {ValidSymbols}/{TotalSymbols} symbols from {NeededProjects}/{TotalProjects} projects")]
    private partial void LogSymbolResolutionStart(int validSymbols, int totalSymbols, int neededProjects, int totalProjects);

    [LoggerMessage(EventId = 3716, Level = LogLevel.Warning,
        Message = "Project {ProjectName} not found in solution")]
    private partial void LogProjectNotFound(string projectName);

    [LoggerMessage(EventId = 3717, Level = LogLevel.Debug,
        Message = "Loading compilation for project {ProjectName}...")]
    private partial void LogLoadingCompilation(string projectName);

    [LoggerMessage(EventId = 3718, Level = LogLevel.Debug,
        Message = "Loaded compilation for {ProjectName} ({TypeCount} types)")]
    private partial void LogCompilationLoaded(string projectName, int typeCount);

    [LoggerMessage(EventId = 3719, Level = LogLevel.Error,
        Message = "Compilation loading for {ProjectName} timed out after {Timeout}")]
    private partial void LogCompilationTimeout(string projectName, TimeSpan timeout);

    [LoggerMessage(EventId = 3720, Level = LogLevel.Information,
        Message = "Processing {TotalSymbols} symbols in {BatchCount} batches of {BatchSize}")]
    private partial void LogProcessingBatches(int totalSymbols, int batchCount, int batchSize);

    [LoggerMessage(EventId = 3721, Level = LogLevel.Information,
        Message = "Symbol resolution complete: {Loaded}/{Needed} projects loaded, {Resolved}/{Total} symbols resolved")]
    private partial void LogResolutionComplete(int loaded, int needed, int resolved, int total);

    [LoggerMessage(EventId = 3722, Level = LogLevel.Trace,
        Message = "Compilation or type cache not available for project {ProjectName}")]
    private partial void LogCompilationNotAvailable(string projectName);

    [LoggerMessage(EventId = 3723, Level = LogLevel.Trace,
        Message = "Error resolving symbol {Fqn}")]
    private partial void LogSymbolResolveError(Exception exception, string fqn);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Overlord.Services;

public sealed partial class SymbolResolutionService
{
    // FindSymbol (7000-7006)
    [LoggerMessage(EventId = 7000, Level = LogLevel.Warning,
        Message = "Cannot find symbol {Fqn}: no solution loaded")]
    private partial void LogNoSolutionLoaded(string fqn);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Debug,
        Message = "Found symbol {Fqn} via SolutionManager")]
    private partial void LogFoundSymbol(string fqn);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Debug,
        Message = "Symbol {Fqn} not found via SolutionManager, trying fuzzy lookup")]
    private partial void LogTryingFuzzyLookup(string fqn);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information,
        Message = "Found symbol {Fqn} via fuzzy lookup (score: {Score}, reason: {Reason})")]
    private partial void LogFoundViaFuzzy(string fqn, double score, string reason);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Warning,
        Message = "Symbol {Fqn} not found in solution")]
    private partial void LogSymbolNotFound(string fqn);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Error,
        Message = "Error finding symbol {Fqn}")]
    private partial void LogFindSymbolError(Exception exception, string fqn);

    // FindNamedTypeSymbol (7006-7011)
    [LoggerMessage(EventId = 7006, Level = LogLevel.Warning,
        Message = "Cannot find type {Fqn}: no solution loaded")]
    private partial void LogNoSolutionLoadedForType(string fqn);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Debug,
        Message = "Found named type symbol {Fqn}")]
    private partial void LogFoundNamedType(string fqn);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Debug,
        Message = "Type {Fqn} not found via SolutionManager, trying fuzzy lookup")]
    private partial void LogTryingFuzzyLookupForType(string fqn);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Information,
        Message = "Found type {Fqn} via fuzzy lookup (score: {Score})")]
    private partial void LogFoundTypeViaFuzzy(string fqn, double score);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Warning,
        Message = "Type {Fqn} not found in solution")]
    private partial void LogTypeNotFound(string fqn);

    [LoggerMessage(EventId = 7011, Level = LogLevel.Error,
        Message = "Error finding type {Fqn}")]
    private partial void LogFindTypeError(Exception exception, string fqn);
}

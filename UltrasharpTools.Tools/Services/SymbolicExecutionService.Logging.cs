using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SymbolicExecutionService
{
    [LoggerMessage(EventId = 3700, Level = LogLevel.Warning,
        Message = "Could not create CFG for {Method}")]
    private partial void LogCfgCreationFailed(Exception exception, string method);

    [LoggerMessage(EventId = 3701, Level = LogLevel.Warning,
        Message = "CFG for {EntryPoint} is null or has no blocks")]
    private partial void LogCfgNullOrEmpty(string entryPoint);

    [LoggerMessage(EventId = 3702, Level = LogLevel.Information,
        Message = "Symbolic execution completed: {Feasible}/{Total} paths feasible, {Issues} issues found")]
    private partial void LogExecutionCompleted(int feasible, int total, int issues);

    [LoggerMessage(EventId = 3703, Level = LogLevel.Error,
        Message = "Error during symbolic execution for {Entry}")]
    private partial void LogExecutionError(Exception exception, string entry);

    [LoggerMessage(EventId = 3704, Level = LogLevel.Debug,
        Message = "Path {PathId} is INFEASIBLE: {Reason}")]
    private partial void LogPathInfeasible(int pathId, string? reason);

    [LoggerMessage(EventId = 3705, Level = LogLevel.Debug,
        Message = "Path {PathId} is FEASIBLE. Example inputs: {Inputs}")]
    private partial void LogPathFeasible(int pathId, string inputs);

    [LoggerMessage(EventId = 3706, Level = LogLevel.Warning,
        Message = "Could not parse constraint: {Constraint}")]
    private partial void LogConstraintParseWarning(string constraint);

    [LoggerMessage(EventId = 3707, Level = LogLevel.Error,
        Message = "Error parsing constraint: {Constraint}")]
    private partial void LogConstraintParseError(Exception exception, string constraint);
}

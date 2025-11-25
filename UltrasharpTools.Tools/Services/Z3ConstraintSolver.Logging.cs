using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class Z3ConstraintSolver
{
    [LoggerMessage(EventId = 3580, Level = LogLevel.Debug,
        Message = "Constraints satisfiable. Example: {Example}")]
    private partial void LogSatisfiable(string example);

    [LoggerMessage(EventId = 3581, Level = LogLevel.Debug,
        Message = "Constraints unsatisfiable")]
    private partial void LogUnsatisfiable();

    [LoggerMessage(EventId = 3582, Level = LogLevel.Warning,
        Message = "Z3 solver returned UNKNOWN")]
    private partial void LogUnknown();

    [LoggerMessage(EventId = 3583, Level = LogLevel.Error,
        Message = "Error during constraint solving")]
    private partial void LogSolverError(Exception exception);

    [LoggerMessage(EventId = 3584, Level = LogLevel.Warning,
        Message = "Could not parse Z3 expression: {Expr}")]
    private partial void LogParseWarning(string expr);

    [LoggerMessage(EventId = 3585, Level = LogLevel.Error,
        Message = "Error parsing Z3 expression: {Expr}")]
    private partial void LogParseError(Exception exception, string expr);
}

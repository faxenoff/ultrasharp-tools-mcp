namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Represents a complete execution trace from entry point to exit point.
/// </summary>
public sealed class ExecutionTrace
{
public required string EntryPointFqn { get; init; }
public string? ExitPointFqn { get; init; }
public required List<TraceStep> Steps { get; init; }
public List<ExecutionPath>? Paths { get; init; }
public int TotalSteps => Steps.Count;
public int TotalPaths => Paths?.Count ?? 0;
public int MaxDepthReached { get; init; }
public bool ExitPointReached { get; init; }
public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a single execution path through the code (for multiple paths analysis).
/// </summary>
public sealed class ExecutionPath
{
public required int PathId { get; init; }
public required List<TraceStep> Steps { get; init; }
public int Depth => Steps.Count > 0 ? Steps.Max(s => s.Depth) : 0;
public bool ReachedExitPoint { get; init; }
public string? PathConditions { get; init; }
}

/// <summary>
/// Represents a single step in the execution trace.
/// </summary>
public sealed class TraceStep
{
public required int StepNumber { get; init; }
public required TraceStepType Type { get; init; }
public required int Depth { get; init; }
public required string Description { get; init; }
public string? MethodFqn { get; init; }
public List<VariableInfo>? Variables { get; init; }
public string? SourceLocation { get; init; }
public string? ConditionExpression { get; init; }
public AsyncAwaitInfo? AsyncInfo { get; init; }
public LinqQueryInfo? LinqInfo { get; init; }
}

/// <summary>
/// Information about async/await operation.
/// </summary>
public sealed class AsyncAwaitInfo
{
public required string AwaitedExpression { get; init; }
public string? TaskType { get; init; }
public int? StateMachineState { get; init; }
public bool ConfigureAwaitUsed { get; init; }
public bool ContinueOnCapturedContext { get; init; }
}

/// <summary>
/// Information about LINQ query operation.
/// </summary>
public sealed class LinqQueryInfo
{
public required string QueryExpression { get; init; }
public string? QueryType { get; init; } // "IQueryable", "IEnumerable"
public bool IsDeferred { get; init; }
public List<string>? Operations { get; init; } // ["Where", "Select", "OrderBy"]
}

/// <summary>
/// Type of trace step.
/// </summary>
public enum TraceStepType
{
/// <summary>Entry point of the trace</summary>
Entry,

/// <summary>Method call</summary>
MethodCall,

/// <summary>Method return</summary>
Return,

/// <summary>Variable assignment</summary>
Assignment,

/// <summary>Conditional branch (if/switch)</summary>
Conditional,

/// <summary>Loop iteration</summary>
Loop,

/// <summary>Object creation</summary>
ObjectCreation,

/// <summary>Exit point reached</summary>
Exit,

/// <summary>External library call (no source available)</summary>
ExternalCall,

/// <summary>Async await point</summary>
AsyncAwait,

/// <summary>Async continuation after await</summary>
AsyncContinuation,

/// <summary>LINQ query expression</summary>
LinqQuery,

/// <summary>Lambda expression call</summary>
LambdaCall
}

/// <summary>
/// Information about a variable at a specific point in execution.
/// </summary>
public sealed class VariableInfo
{
public required string Name { get; init; }
public required string Type { get; init; }
public string? Operation { get; init; } // "created", "assigned", "mutated", "passed"
public string? Scope { get; init; } // "parameter", "local", "field"
}

namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Result of symbolic execution analysis.
/// </summary>
public sealed class SymbolicExecutionResult
{
    public required string EntryPointFqn { get; init; }
    public string? ExitPointFqn { get; init; }
    public required List<SymbolicPath> Paths { get; init; }

    // Statistics
    public int FeasiblePaths => Paths.Count(p => p.IsFeasible);
    public int InfeasiblePaths => Paths.Count(p => !p.IsFeasible);
    public int TotalPaths => Paths.Count;
    public int TotalConstraints { get; init; }

    // Analysis results
    public required List<PotentialIssue> Issues { get; init; }
    public bool ExitPointReachable { get; init; }
    public int MaxDepthReached { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents one symbolic execution path through the code.
/// </summary>
public sealed class SymbolicPath
{
    public required int PathId { get; init; }
    public required List<SymbolicStep> Steps { get; init; }

    // Path constraints that must be satisfied for this path
    public required List<string> Constraints { get; init; }

    // Feasibility
    public required bool IsFeasible { get; init; }

    // If feasible, example inputs that trigger this path
    public Dictionary<string, object>? ExampleInputs { get; init; }

    // If infeasible, reason why
    public string? InfeasibilityReason { get; init; }

    // Depth of this path
    public int Depth => Steps.Count;
}

/// <summary>
/// One step in symbolic execution.
/// </summary>
public sealed class SymbolicStep
{
    public required int StepNumber { get; init; }
    public required TraceStepType Type { get; init; }
    public required string Description { get; init; }
    public string? SourceLocation { get; init; }

    // Symbolic state at this point (variable -> symbolic expression)
    public required Dictionary<string, string> SymbolicState { get; init; }

    // Constraint added at this step (if any)
    public string? AddedConstraint { get; init; }

    // Whether this step is reachable
    public bool IsReachable { get; init; } = true;
}

/// <summary>
/// Potential issue detected during symbolic execution.
/// </summary>
public sealed class PotentialIssue
{
    public required IssueType Type { get; init; }
    public required string Description { get; init; }
    public required string SourceLocation { get; init; }
    public required List<string> TriggeringConstraints { get; init; }

    // Severity
    public IssueSeverity Severity { get; init; } = IssueSeverity.Warning;

    // Example inputs that trigger this issue
    public Dictionary<string, object>? ExampleInputs { get; init; }
}

/// <summary>
/// Types of issues that can be detected.
/// </summary>
public enum IssueType
{
    DeadCode,
    NullReference,
    DivisionByZero,
    ArrayOutOfBounds,
    UnreachableExit,
    InfiniteLoop,
    InvalidCast,
    Overflow
}

/// <summary>
/// Severity of detected issue.
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for tracing code execution flow through static analysis.
/// </summary>
public interface IExecutionTraceService
{
    /// <summary>
    /// Traces execution from an entry point, optionally to an exit point.
    /// </summary>
    /// <param name="entryPointFqn">Fully qualified name of the entry point method</param>
    /// <param name="exitPointFqn">Optional fully qualified name of the exit point method</param>
    /// <param name="maxDepth">Maximum depth of method calls to trace (default: 10)</param>
    /// <param name="includeExternalCalls">Whether to show external library calls (default: true, signature only)</param>
    /// <param name="traceAllPaths">Whether to trace all conditional branches (default: false, single path)</param>
    /// <param name="maxPaths">Maximum number of paths to trace when traceAllPaths is true (default: 10)</param>
    /// <param name="unwrapAsync">Whether to unwrap async/await operations and show state machine transitions (default: true)</param>
    /// <param name="unwrapLinq">Whether to unwrap LINQ queries and show lambda executions (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution trace with steps</returns>
    Task<ExecutionTrace> TraceExecutionAsync(
        string entryPointFqn,
        string? exitPointFqn = null,
        int maxDepth = 10,
        bool includeExternalCalls = true,
        bool traceAllPaths = false,
        int maxPaths = 10,
        bool unwrapAsync = true,
        bool unwrapLinq = false,
        CancellationToken cancellationToken = default
    );
}

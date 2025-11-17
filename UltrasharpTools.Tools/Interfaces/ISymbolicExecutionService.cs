using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for symbolic execution analysis.
/// </summary>
public interface ISymbolicExecutionService
{
/// <summary>
/// Analyzes path feasibility using symbolic execution.
/// </summary>
/// <param name="entryPointFqn">Entry point method FQN</param>
/// <param name="exitPointFqn">Optional exit point FQN</param>
/// <param name="maxDepth">Maximum depth to analyze</param>
/// <param name="initialConstraints">Initial constraints on input parameters</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Symbolic execution result with paths and issues</returns>
Task<SymbolicExecutionResult> AnalyzePathFeasibilityAsync(
string entryPointFqn,
string? exitPointFqn = null,
int maxDepth = 10,
Dictionary<string, string>? initialConstraints = null,
CancellationToken cancellationToken = default
);
}

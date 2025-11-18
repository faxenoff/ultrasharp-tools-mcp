using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using UltrasharpTools.Tools.Mcp;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Services;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Mcp.Tools;

// Marker class for ILogger<T> category specific to TraceTools
public class TraceToolsLogCategory { }

[McpServerToolType]
public static partial class TraceTools
{
/// <summary>
/// Traces code execution flow from an entry point, showing method calls, variable operations, and control flow.
/// Uses static analysis (Control Flow Graph) - does not execute code.
/// </summary>
[McpServerTool(
Name = "trace_execution",
Idempotent = true,
ReadOnly = true,
Destructive = false,
OpenWorld = false
)]
[Description(
"Traces code execution flow from an entry point method, optionally to an exit point. " +
"Shows the sequence of method calls, variable operations, assignments, and control flow branches. " +
"Uses static analysis via Roslyn Control Flow Graph - does not execute code. " +
"Useful for understanding what happens during code execution without running it."
)]
public static async Task<object> TraceExecution(
ISolutionManager solutionManager,
IExecutionTraceService traceService,
ILogger<TraceToolsLogCategory> logger,
[Description("Fully qualified name of the entry point method (e.g., MyNamespace.MyClass.MyMethod)")]
string entryPointFqn,
[Description("Optional: Fully qualified name of the exit point method. Trace stops when this method is called.")]
string? exitPointFqn = null,
[Description("Maximum depth of method calls to trace. Default: 10")]
int maxDepth = 10,
[Description("Whether to show external library calls (signature only). Default: true")]
bool includeExternalCalls = true,
[Description("Whether to trace all conditional branches. Default: false (single path)")]
bool traceAllPaths = false,
[Description("Maximum number of paths to trace when traceAllPaths is true. Default: 10")]
int maxPaths = 10,
[Description("Whether to unwrap async/await operations and show state machine transitions. Default: true")]
bool unwrapAsync = true,
[Description("Whether to unwrap LINQ queries and show lambda executions. Default: false")]
bool unwrapLinq = false,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(
entryPointFqn,
nameof(entryPointFqn),
logger
);
await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
solutionManager,
logger,
nameof(TraceExecution),
cancellationToken
);

logger.LogInformation(
"Executing {ToolName} for entry: {EntryPoint}, exit: {ExitPoint}, maxDepth: {MaxDepth}",
nameof(TraceExecution),
entryPointFqn,
exitPointFqn ?? "none",
maxDepth
);

// Validate parameters
if (maxDepth < 1 || maxDepth > 50)
{
throw new McpException(
$"maxDepth must be between 1 and 50, got: {maxDepth}"
);
}

// Execute trace
var trace = await traceService.TraceExecutionAsync(
entryPointFqn,
exitPointFqn,
maxDepth,
includeExternalCalls,
traceAllPaths,
maxPaths,
unwrapAsync,
unwrapLinq,
cancellationToken
);

// Format result
var result = FormatTraceResult(trace);

logger.LogInformation(
"Trace completed: {Steps} steps, max depth: {Depth}, exit reached: {ExitReached}",
trace.TotalSteps,
trace.MaxDepthReached,
trace.ExitPointReached
);

return ToolHelpers.ToJson(result);
},
logger,
nameof(TraceExecution),
cancellationToken
);
}

private static object FormatTraceResult(ExecutionTrace trace)
{
var result = new Dictionary<string, object>
{
["entryPoint"] = trace.EntryPointFqn,
["exitPoint"] = trace.ExitPointFqn ?? "none",
["totalSteps"] = trace.TotalSteps,
["maxDepthReached"] = trace.MaxDepthReached,
["exitPointReached"] = trace.ExitPointReached
};

if (!string.IsNullOrEmpty(trace.ErrorMessage))
{
result["error"] = trace.ErrorMessage;
}

// Format steps into readable text
var sb = ObjectPoolProvider.Instance.GetStringBuilder();
try
{
sb.AppendLine();
sb.AppendLine($"Execution Trace: {trace.EntryPointFqn}");
sb.AppendLine(new string('═', 80));
sb.AppendLine();

foreach (var step in trace.Steps)
{
// Indentation based on depth
var indent = new string(' ', step.Depth * 2);

// Step header
sb.Append($"[{step.StepNumber}] {indent}");

// Format based on type
switch (step.Type)
{
case TraceStepType.Entry:
sb.AppendLine($"🚀 {step.Description}");
break;

case TraceStepType.MethodCall:
sb.AppendLine($"📞 {step.Description}");
break;

case TraceStepType.Return:
sb.AppendLine($"↩️  {step.Description}");
break;

case TraceStepType.Assignment:
sb.AppendLine($"✏️  {step.Description}");
break;

case TraceStepType.ObjectCreation:
sb.AppendLine($"🆕 {step.Description}");
break;

case TraceStepType.Conditional:
sb.AppendLine($"🔀 {step.Description}");
break;

case TraceStepType.Loop:
sb.AppendLine($"🔁 {step.Description}");
break;

case TraceStepType.Exit:
sb.AppendLine($"🎯 {step.Description}");
break;

case TraceStepType.ExternalCall:
sb.AppendLine($"📦 {step.Description}");
break;

default:
sb.AppendLine(step.Description);
break;
}

// Add variables if present
if (step.Variables?.Count > 0)
{
foreach (var variable in step.Variables)
{
sb.AppendLine(
$"{indent}  ├─ {variable.Name}: {variable.Type} ({variable.Operation})"
);
}
}

// Add source location
if (!string.IsNullOrEmpty(step.SourceLocation))
{
sb.AppendLine($"{indent}  └─ {step.SourceLocation}");
}

sb.AppendLine();
}

result["trace"] = sb.ToString();
}
finally
{
ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
}

// Also include raw steps for programmatic access
result["steps"] = trace.Steps.Select(
s =>
new
{
step = s.StepNumber,
type = s.Type.ToString(),
depth = s.Depth,
description = s.Description,
methodFqn = s.MethodFqn,
variables = s.Variables?.Select(
v =>
new
{
name = v.Name,
type = v.Type,
operation = v.Operation,
scope = v.Scope
}
).ToList(),
sourceLocation = s.SourceLocation,
conditionExpression = s.ConditionExpression
}
).ToList();

return result;
}

/// <summary>
/// Traces backwards from a crash/failure point to find how execution reached that point.
/// Shows all possible call paths from entry points to the crash.
/// </summary>
[McpServerTool(
Name = "trace_backwards",
Idempotent = true,
ReadOnly = true,
Destructive = false,
OpenWorld = false
)]
[Description(
"Traces backwards from a crash or failure point to find all possible call paths that lead to it. " +
"Useful for understanding how execution reached a problematic point. " +
"Can use stack trace hints to rank paths by likelihood. " +
"Returns multiple possible paths ordered by confidence."
)]
public static async Task<object> TraceBackwards(
ISolutionManager solutionManager,
IBacktraceService backtraceService,
ILogger<TraceToolsLogCategory> logger,
[Description("Fully qualified name of the crash/failure point method (e.g., MyNamespace.MyClass.ProblematicMethod)")]
string crashPointFqn,
[Description("Optional: Fully qualified name of the expected start/entry point. If not specified, searches for all entry points.")]
string? startPointFqn = null,
[Description("Optional: Stack trace lines (from logs) to help identify the most likely path")]
List<string>? stackTraceHints = null,
[Description("Maximum depth to search backwards. Default: 15")]
int maxDepth = 15,
[Description("Maximum number of paths to return. Default: 5")]
int maxPaths = 5,
[Description("Whether to include calls from external libraries. Default: false")]
bool includeExternalCallers = false,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(
crashPointFqn,
nameof(crashPointFqn),
logger
);
await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
solutionManager,
logger,
nameof(TraceBackwards),
cancellationToken
);

logger.LogInformation(
"Executing {ToolName} for crash: {CrashPoint}, start: {StartPoint}, maxDepth: {MaxDepth}, maxPaths: {MaxPaths}",
nameof(TraceBackwards),
crashPointFqn,
startPointFqn ?? "any entry point",
maxDepth,
maxPaths
);

// Validate parameters
if (maxDepth < 1 || maxDepth > 50)
{
throw new McpException(
$"maxDepth must be between 1 and 50, got: {maxDepth}"
);
}

if (maxPaths < 1 || maxPaths > 20)
{
throw new McpException(
$"maxPaths must be between 1 and 20, got: {maxPaths}"
);
}

// Execute backtrace
var result = await backtraceService.BacktraceFromCrashAsync(
crashPointFqn,
startPointFqn,
stackTraceHints,
maxDepth,
maxPaths,
includeExternalCallers,
cancellationToken
);

// Format result
var formattedResult = FormatBacktraceResult(result);

logger.LogInformation(
"Backtrace completed: {Paths} paths found, max depth: {Depth}, start reached: {StartReached}",
result.TotalPaths,
result.MaxDepthReached,
result.StartPointReached
);

return ToolHelpers.ToJson(formattedResult);
},
logger,
nameof(TraceBackwards),
cancellationToken
);
}

private static object FormatBacktraceResult(BacktraceResult result)
{
var formatted = new Dictionary<string, object>
{
["crashPoint"] = result.CrashPointFqn,
["startPoint"] = result.StartPointFqn ?? "any entry point",
["totalPaths"] = result.TotalPaths,
["maxDepthReached"] = result.MaxDepthReached,
["startPointReached"] = result.StartPointReached
};

if (!string.IsNullOrEmpty(result.ErrorMessage))
{
formatted["error"] = result.ErrorMessage;
}

if (result.StackTraceHints?.Count > 0)
{
formatted["stackTraceHints"] = result.StackTraceHints;
}

// Format each call path
var sb = ObjectPoolProvider.Instance.GetStringBuilder();
try
{
sb.AppendLine();
sb.AppendLine($"Backtrace from: {result.CrashPointFqn}");
sb.AppendLine(new string('═', 80));
sb.AppendLine();

if (result.TotalPaths == 0)
{
sb.AppendLine("No call paths found.");
}
else
{
foreach (var path in result.CallPaths)
{
sb.AppendLine($"Path #{path.PathId} (Confidence: {path.Confidence:P0}, Depth: {path.Depth})");
sb.AppendLine(new string('─', 80));

if (path.ReachedEntryPoint)
{
sb.AppendLine("✓ Reached entry point");
}
else
{
sb.AppendLine("⚠ Did not reach entry point (stopped at max depth)");
}

sb.AppendLine();

foreach (var frame in path.Frames)
{
var indent = new string(' ', frame.FrameNumber * 2);
var matchIndicator = frame.MatchesStackTrace ? "📍" : "  ";

sb.AppendLine($"{matchIndicator}[{frame.FrameNumber}] {indent}{frame.Description}");
sb.AppendLine($"   {indent}└─ {frame.MethodFqn}");

if (frame.Parameters?.Count > 0)
{
sb.AppendLine(
$"   {indent}   Parameters: {string.Join(", ", frame.Parameters.Select(p => $"{p.Type} {p.Name}"))}"
);
}

if (frame.CallSite != null)
{
sb.AppendLine($"   {indent}   Called from: {frame.CallSite.SourceLocation}");
if (!string.IsNullOrEmpty(frame.CallSite.CallExpression) &&
frame.CallSite.CallExpression.Length < 100)
{
sb.AppendLine($"   {indent}   Expression: {frame.CallSite.CallExpression}");
}
}

sb.AppendLine();
}

sb.AppendLine();
}
}

formatted["backtrace"] = sb.ToString();
}
finally
{
ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
}

// Include raw paths for programmatic access
formatted["paths"] = result.CallPaths.Select(
p =>
new
{
pathId = p.PathId,
confidence = p.Confidence,
depth = p.Depth,
reachedEntryPoint = p.ReachedEntryPoint,
frames = p.Frames.Select(
f =>
new
{
frameNumber = f.FrameNumber,
methodFqn = f.MethodFqn,
description = f.Description,
sourceLocation = f.SourceLocation,
matchesStackTrace = f.MatchesStackTrace,
parameters = f.Parameters?.Select(
v =>
new
{
name = v.Name,
type = v.Type,
operation = v.Operation,
scope = v.Scope
}
).ToList(),
callSite = f.CallSite != null
? new
{
callingMethodFqn = f.CallSite.CallingMethodFqn,
sourceLocation = f.CallSite.SourceLocation,
callExpression = f.CallSite.CallExpression
}
: null
}
).ToList()
}
).ToList();

return formatted;
}

/// <summary>
/// Exports call graph visualization from backtrace result.
/// </summary>
[McpServerTool(
Name = "export_call_graph",
Idempotent = true,
ReadOnly = true,
Destructive = false,
OpenWorld = false
)]
[Description(
"Exports call graph visualization in various formats (DOT, Mermaid, GraphML). " +
"Use after TraceBackwards to visualize the call paths. " +
"DOT format can be rendered with Graphviz. Mermaid format works in Markdown. " +
"GraphML is compatible with yEd, Gephi, and other graph tools."
)]
public static async Task<object> ExportCallGraph(
ISolutionManager solutionManager,
IBacktraceService backtraceService,
ILogger<TraceToolsLogCategory> logger,
[Description("Fully qualified name of the crash/failure point method")]
string crashPointFqn,
[Description("Export format: 'dot', 'mermaid', or 'graphml'. Default: 'dot'")]
string format = "dot",
[Description("Optional: Stack trace lines for better path ranking")]
List<string>? stackTraceHints = null,
[Description("Maximum depth to search backwards. Default: 15")]
int maxDepth = 15,
[Description("Maximum number of paths to return. Default: 5")]
int maxPaths = 5,
[Description("Whether to include confidence scores in visualization. Default: true")]
bool includeConfidence = true,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
async () =>
{
ErrorHandlingHelpers.ValidateStringParameter(
crashPointFqn,
nameof(crashPointFqn),
logger
);
await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
solutionManager,
logger,
nameof(ExportCallGraph),
cancellationToken
);

logger.LogInformation(
"Executing {ToolName} for crash: {CrashPoint}, format: {Format}",
nameof(ExportCallGraph),
crashPointFqn,
format
);

// Validate format
var validFormats = new[] { "dot", "mermaid", "graphml" };
if (!validFormats.Contains(format.ToLowerInvariant()))
{
throw new McpException(
$"Invalid format '{format}'. Must be one of: {string.Join(", ", validFormats)}"
);
}

// Execute backtrace
var result = await backtraceService.BacktraceFromCrashAsync(
crashPointFqn,
startPointFqn: null,
stackTraceHints,
maxDepth,
maxPaths,
includeExternalCallers: false,
cancellationToken
);

// Export to requested format
string visualization;
switch (format.ToLowerInvariant())
{
case "dot":
visualization = CallGraphExporter.ToDot(result, includeConfidence);
break;
case "mermaid":
visualization = CallGraphExporter.ToMermaid(result, includeConfidence);
break;
case "graphml":
visualization = CallGraphExporter.ToGraphML(result);
break;
default:
throw new McpException($"Unsupported format: {format}");
}

logger.LogInformation(
"Call graph exported: {Paths} paths, format: {Format}, size: {Size} chars",
result.TotalPaths,
format,
visualization.Length
);

return ToolHelpers.ToJson(new
{
format,
crashPoint = crashPointFqn,
totalPaths = result.TotalPaths,
maxDepthReached = result.MaxDepthReached,
visualization
});
},
logger,
nameof(ExportCallGraph),
cancellationToken
);
}

/// <summary>
/// Analyzes path feasibility using symbolic execution. Detects dead code, null references, division by zero, and other potential issues.
/// </summary>
[McpServerTool(
Name = "analyze_path_feasibility",
Idempotent = true,
ReadOnly = true,
Destructive = false,
OpenWorld = false
)]
[Description(
"Analyzes path feasibility using symbolic execution and Z3 SMT solver. " +
"Explores all execution paths through the method, tracking symbolic constraints on variables. " +
"Detects potential issues: dead code, null references, division by zero, array out of bounds, infinite loops. " +
"For each path, determines feasibility and generates example inputs that would trigger it. " +
"Uses static analysis via Roslyn Control Flow Graph + Z3 constraint solving - does not execute code."
)]
public static async Task<object> AnalyzePathFeasibility(
ISolutionManager solutionManager,
ISymbolicExecutionService symbolicExecutionService,
ILogger<TraceToolsLogCategory> logger,
[Description("Fully qualified name of the entry point method to analyze (e.g., MyNamespace.MyClass.MyMethod)")]
string entryPointFqn,
[Description("Optional: Fully qualified name of the exit point. Analysis focuses on paths reaching this point.")]
string? exitPointFqn = null,
[Description("Maximum depth of path exploration. Default: 10")]
int maxDepth = 10,
[Description("Optional: Initial constraints on input parameters (JSON dictionary mapping parameter names to constraint expressions)")]
Dictionary<string, string>? initialConstraints = null,
CancellationToken cancellationToken = default
)
{
return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
async () =>
{
if (solutionManager.CurrentSolution == null)
{
throw new McpException("No solution loaded. Use UltrasharpTool_LoadSolution first.");
}

logger.LogInformation(
"Starting symbolic execution analysis: {Entry} → {Exit}, maxDepth: {MaxDepth}",
entryPointFqn,
exitPointFqn ?? "(any exit)",
maxDepth
);

// Perform symbolic execution
var result = await symbolicExecutionService.AnalyzePathFeasibilityAsync(
entryPointFqn,
exitPointFqn,
maxDepth,
initialConstraints,
cancellationToken
);

// Build response
var response = new
{
entryPoint = result.EntryPointFqn,
exitPoint = result.ExitPointFqn,
summary = new
{
totalPaths = result.TotalPaths,
feasiblePaths = result.FeasiblePaths,
infeasiblePaths = result.InfeasiblePaths,
totalIssues = result.Issues.Count,
exitPointReachable = result.ExitPointReachable,
maxDepthReached = result.MaxDepthReached,
totalConstraints = result.TotalConstraints
},
paths = result.Paths.Select(p => new
{
pathId = p.PathId,
depth = p.Depth,
isFeasible = p.IsFeasible,
infeasibilityReason = p.InfeasibilityReason,
constraintCount = p.Constraints.Count,
constraints = p.Constraints,
exampleInputs = p.ExampleInputs,
steps = p.Steps.Select(s => new
{
stepNumber = s.StepNumber,
type = s.Type.ToString(),
description = s.Description,
sourceLocation = s.SourceLocation,
symbolicState = s.SymbolicState,
addedConstraint = s.AddedConstraint
}).ToList()
}).ToList(),
issues = result.Issues.Select(i => new
{
type = i.Type.ToString(),
severity = i.Severity.ToString(),
description = i.Description,
location = i.SourceLocation,
triggeringConstraints = i.TriggeringConstraints
}).ToList(),
errorMessage = result.ErrorMessage
};

logger.LogInformation(
"Symbolic execution completed: {Feasible}/{Total} paths feasible, {Issues} issues found",
result.FeasiblePaths,
result.TotalPaths,
result.Issues.Count
);

return ToolHelpers.ToJson(response);
},
logger,
nameof(AnalyzePathFeasibility),
cancellationToken
);
}
}

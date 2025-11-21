

using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Service for tracing code execution flow through static analysis using Roslyn CFG.
/// Enhanced with multiple paths support, async/await tracking, and LINQ expression unwrapping.
/// </summary>
public sealed class ExecutionTraceService(
ISolutionManager solutionManager,
ICodeAnalysisService codeAnalysisService
) : IExecutionTraceService
{
    private readonly ISolutionManager _solutionManager = solutionManager;
    private readonly ICodeAnalysisService _codeAnalysisService = codeAnalysisService;

    private sealed class TraceContext
    {
        public List<ExecutionPath> Paths { get; } = new();
        public int StepCounter { get; set; }
        public int MaxDepthReached { get; set; }
        public bool ExitPointReached { get; set; }
        public string? ExitPointFqn { get; set; }
        public int MaxDepth { get; set; }
        public bool IncludeExternalCalls { get; set; }
        public bool TraceAllPaths { get; set; }
        public int MaxPaths { get; set; }
        public bool UnwrapAsync { get; set; }
        public bool UnwrapLinq { get; set; }
    }

    public async Task<ExecutionTrace> TraceExecutionAsync(
    string entryPointFqn,
    string? exitPointFqn = null,
    int maxDepth = 10,
    bool includeExternalCalls = true,
    bool traceAllPaths = false,
    int maxPaths = 10,
    bool unwrapAsync = true,
    bool unwrapLinq = false,
    CancellationToken cancellationToken = default
    )
    {
        var context = new TraceContext
        {
            ExitPointFqn = exitPointFqn,
            MaxDepth = maxDepth,
            IncludeExternalCalls = includeExternalCalls,
            TraceAllPaths = traceAllPaths,
            MaxPaths = maxPaths,
            UnwrapAsync = unwrapAsync,
            UnwrapLinq = unwrapLinq
        };

        string? errorMessage = null;
        List<TraceStep> legacySteps = new();

        try
        {
            // Find entry point method
            var entrySymbol = await FindMethodSymbolAsync(entryPointFqn, cancellationToken);
            if (entrySymbol == null)
            {
                return new ExecutionTrace
                {
                    EntryPointFqn = entryPointFqn,
                    ExitPointFqn = exitPointFqn,
                    Steps = legacySteps,
                    Paths = context.Paths,
                    MaxDepthReached = 0,
                    ExitPointReached = false,
                    ErrorMessage = $"Entry point method not found: {entryPointFqn}"
                };
            }

            // Create initial step and path
            var entryStep = new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.Entry,
                Depth = 0,
                Description = $"ENTRY: {entrySymbol.ToDisplayString()}",
                MethodFqn = entrySymbol.ToDisplayString(),
                Variables = GetMethodParameters(entrySymbol),
                SourceLocation = GetSourceLocation(entrySymbol)
            };

            // Trace method execution
            if (traceAllPaths)
            {
                // Multiple paths mode
                var initialPath = new List<TraceStep> { entryStep };
                await TraceMethodAllPathsAsync(entrySymbol, initialPath, context, 0, cancellationToken);

                // Legacy single Steps list for backward compatibility
                legacySteps = context.Paths.FirstOrDefault()?.Steps ?? new List<TraceStep> { entryStep };
            }
            else
            {
                // Single path mode (legacy)
                legacySteps.Add(entryStep);
                await TraceMethodSinglePathAsync(entrySymbol, legacySteps, context, 0, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error during trace: {ex.Message}";
        }

        return new ExecutionTrace
        {
            EntryPointFqn = entryPointFqn,
            ExitPointFqn = exitPointFqn,
            Steps = legacySteps,
            Paths = traceAllPaths ? context.Paths : null,
            MaxDepthReached = context.MaxDepthReached,
            ExitPointReached = context.ExitPointReached,
            ErrorMessage = errorMessage
        };
    }

    // Single path tracing (legacy behavior)
    private async Task TraceMethodSinglePathAsync(
    IMethodSymbol methodSymbol,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        if (currentDepth > context.MaxDepth || context.ExitPointReached)
        {
            return;
        }

        context.MaxDepthReached = Math.Max(context.MaxDepthReached, currentDepth);

        // Get method syntax and semantic model
        var syntaxReferences = methodSymbol.DeclaringSyntaxReferences;
        if (syntaxReferences.Length == 0)
        {
            // External method (no source available)
            if (context.IncludeExternalCalls)
            {
                currentSteps.Add(new TraceStep
                {
                    StepNumber = ++context.StepCounter,
                    Type = TraceStepType.ExternalCall,
                    Depth = currentDepth,
                    Description = $"EXTERNAL: {methodSymbol.ToDisplayString()}",
                    MethodFqn = methodSymbol.ToDisplayString(),
                    SourceLocation = "External library"
                });
            }
            return;
        }

        var syntaxNode = await syntaxReferences[0].GetSyntaxAsync(cancellationToken);

        // Get the document containing this syntax
        var document = _solutionManager.CurrentSolution?.GetDocument(syntaxNode.SyntaxTree);
        if (document == null)
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
        {
            return;
        }

        // Get method body
        BaseMethodDeclarationSyntax? methodBody = syntaxNode switch
        {
            MethodDeclarationSyntax method => method,
            ConstructorDeclarationSyntax ctor => ctor,
            _ => null
        };

        if (methodBody?.Body == null && methodBody is MethodDeclarationSyntax { ExpressionBody: null })
        {
            return;
        }

        // Null check for methodBody
        if (methodBody == null)
        {
            return;
        }

        // Build Control Flow Graph
        ControlFlowGraph? cfg = null;
        try
        {
            cfg = ControlFlowGraph.Create(methodBody, semanticModel, cancellationToken);
        }
        catch
        {
            // CFG creation can fail for some methods
            return;
        }

        if (cfg == null)
        {
            return;
        }

        // Traverse CFG blocks (single path)
        var visitedBlocks = new HashSet<BasicBlock>();
        await TraverseCfgSinglePathAsync(
        cfg.Blocks[0],
        cfg,
        visitedBlocks,
        semanticModel,
        currentSteps,
        context,
        currentDepth,
        cancellationToken
        );
    }

    // Multiple paths tracing (new feature)
    private async Task TraceMethodAllPathsAsync(
    IMethodSymbol methodSymbol,
    List<TraceStep> currentPath,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        if (currentDepth > context.MaxDepth || context.Paths.Count >= context.MaxPaths)
        {
            // Save current path as completed
            SaveCompletedPath(currentPath, context);
            return;
        }

        context.MaxDepthReached = Math.Max(context.MaxDepthReached, currentDepth);

        // Get method syntax and semantic model
        var syntaxReferences = methodSymbol.DeclaringSyntaxReferences;
        if (syntaxReferences.Length == 0)
        {
            // External method (no source available)
            if (context.IncludeExternalCalls)
            {
                var newPath = new List<TraceStep>(currentPath)
{
new TraceStep
{
StepNumber = ++context.StepCounter,
Type = TraceStepType.ExternalCall,
Depth = currentDepth,
Description = $"EXTERNAL: {methodSymbol.ToDisplayString()}",
MethodFqn = methodSymbol.ToDisplayString(),
SourceLocation = "External library"
}
};
                SaveCompletedPath(newPath, context);
            }
            else
            {
                SaveCompletedPath(currentPath, context);
            }
            return;
        }

        var syntaxNode = await syntaxReferences[0].GetSyntaxAsync(cancellationToken);

        // Get the document containing this syntax
        var document = _solutionManager.CurrentSolution?.GetDocument(syntaxNode.SyntaxTree);
        if (document == null)
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel == null)
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        // Get method body
        BaseMethodDeclarationSyntax? methodBody = syntaxNode switch
        {
            MethodDeclarationSyntax method => method,
            ConstructorDeclarationSyntax ctor => ctor,
            _ => null
        };

        if (methodBody?.Body == null && methodBody is MethodDeclarationSyntax { ExpressionBody: null })
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        // Null check for methodBody
        if (methodBody == null)
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        // Build Control Flow Graph
        ControlFlowGraph? cfg = null;
        try
        {
            cfg = ControlFlowGraph.Create(methodBody, semanticModel, cancellationToken);
        }
        catch
        {
            // CFG creation can fail for some methods
            SaveCompletedPath(currentPath, context);
            return;
        }

        if (cfg == null)
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        // Traverse CFG blocks (all paths)
        await TraverseCfgAllPathsAsync(
        cfg.Blocks[0],
        cfg,
        new HashSet<BasicBlock>(),
        semanticModel,
        currentPath,
        context,
        currentDepth,
        cancellationToken
        );
    }

    private void SaveCompletedPath(List<TraceStep> steps, TraceContext context)
    {
        if (context.Paths.Count >= context.MaxPaths)
        {
            return;
        }

        var exitReached = steps.Any(s => s.Type == TraceStepType.Exit);

        context.Paths.Add(new ExecutionPath
        {
            PathId = context.Paths.Count + 1,
            Steps = new List<TraceStep>(steps),
            ReachedExitPoint = exitReached,
            PathConditions = ExtractPathConditions(steps)
        });
    }

    private string? ExtractPathConditions(List<TraceStep> steps)
    {
        var conditions = steps
        .Where(s => s.Type == TraceStepType.Conditional)
        .Select(s => s.ConditionExpression)
        .Where(c => !string.IsNullOrEmpty(c))
        .ToList();

        return conditions.Count > 0 ? string.Join(" AND ", conditions) : null;
    }

    private async Task TraverseCfgSinglePathAsync(
    BasicBlock block,
    ControlFlowGraph cfg,
    HashSet<BasicBlock> visitedBlocks,
    SemanticModel semanticModel,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        if (visitedBlocks.Contains(block) || block.Kind == BasicBlockKind.Exit || context.ExitPointReached)
        {
            return;
        }

        visitedBlocks.Add(block);

        // Process operations in the block
        foreach (var operation in block.Operations)
        {
            await ProcessOperationSinglePathAsync(
            operation,
            semanticModel,
            currentSteps,
            context,
            currentDepth,
            cancellationToken
            );

            if (context.ExitPointReached)
            {
                return;
            }
        }

        // Process branch condition if exists
        if (block.BranchValue != null)
        {
            currentSteps.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.Conditional,
                Depth = currentDepth,
                Description = $"BRANCH: {block.BranchValue.Syntax.ToString().Trim()}",
                ConditionExpression = block.BranchValue.Syntax.ToString().Trim(),
                SourceLocation = GetSourceLocationFromSyntax(block.BranchValue.Syntax)
            });
        }

        // Follow conditional branches (take first path for simplicity)
        if (block.ConditionalSuccessor?.Destination != null)
        {
            await TraverseCfgSinglePathAsync(
            block.ConditionalSuccessor.Destination,
            cfg,
            visitedBlocks,
            semanticModel,
            currentSteps,
            context,
            currentDepth,
            cancellationToken
            );
        }

        // Follow fallthrough
        if (block.FallThroughSuccessor?.Destination != null && !context.ExitPointReached)
        {
            await TraverseCfgSinglePathAsync(
            block.FallThroughSuccessor.Destination,
            cfg,
            visitedBlocks,
            semanticModel,
            currentSteps,
            context,
            currentDepth,
            cancellationToken
            );
        }
    }

    private async Task TraverseCfgAllPathsAsync(
    BasicBlock block,
    ControlFlowGraph cfg,
    HashSet<BasicBlock> visitedBlocks,
    SemanticModel semanticModel,
    List<TraceStep> currentPath,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        if (block.Kind == BasicBlockKind.Exit || context.Paths.Count >= context.MaxPaths)
        {
            SaveCompletedPath(currentPath, context);
            return;
        }

        // Allow revisiting blocks in different paths (don't use visited set globally)
        if (visitedBlocks.Contains(block))
        {
            // Cycle detected, save current path
            SaveCompletedPath(currentPath, context);
            return;
        }

        var newVisitedBlocks = new HashSet<BasicBlock>(visitedBlocks) { block };

        // Process operations in the block
        var pathAfterOperations = new List<TraceStep>(currentPath);
        foreach (var operation in block.Operations)
        {
            await ProcessOperationAllPathsAsync(
            operation,
            semanticModel,
            pathAfterOperations,
            context,
            currentDepth,
            cancellationToken
            );

            // Check for exit point
            if (pathAfterOperations.Any(s => s.Type == TraceStepType.Exit))
            {
                SaveCompletedPath(pathAfterOperations, context);
                return;
            }
        }

        // Process branch condition if exists
        if (block.BranchValue != null)
        {
            pathAfterOperations.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.Conditional,
                Depth = currentDepth,
                Description = $"BRANCH: {block.BranchValue.Syntax.ToString().Trim()}",
                ConditionExpression = block.BranchValue.Syntax.ToString().Trim(),
                SourceLocation = GetSourceLocationFromSyntax(block.BranchValue.Syntax)
            });
        }

        // Explore both conditional and fallthrough branches
        var hasConditional = block.ConditionalSuccessor?.Destination != null;
        var hasFallthrough = block.FallThroughSuccessor?.Destination != null;

        if (hasConditional && hasFallthrough)
        {
            // Fork: explore both paths
            var conditionalPath = new List<TraceStep>(pathAfterOperations);
            var conditionalDest = block.ConditionalSuccessor?.Destination;
            if (conditionalDest != null)
            {
                await TraverseCfgAllPathsAsync(
                conditionalDest,
                cfg,
                newVisitedBlocks,
                semanticModel,
                conditionalPath,
                context,
                currentDepth,
                cancellationToken
                );
            }

            var fallthroughPath = new List<TraceStep>(pathAfterOperations);
            var fallthroughDest = block.FallThroughSuccessor?.Destination;
            if (fallthroughDest != null)
            {
                await TraverseCfgAllPathsAsync(
                fallthroughDest,
                cfg,
                newVisitedBlocks,
                semanticModel,
                fallthroughPath,
                context,
                currentDepth,
                cancellationToken
                );
            }
        }
        else if (hasConditional)
        {
            var conditionalDest = block.ConditionalSuccessor?.Destination;
            if (conditionalDest != null)
            {
                await TraverseCfgAllPathsAsync(
                conditionalDest,
                cfg,
                newVisitedBlocks,
                semanticModel,
                pathAfterOperations,
                context,
                currentDepth,
                cancellationToken
                );
            }
        }
        else if (hasFallthrough)
        {
            var fallthroughDest = block.FallThroughSuccessor?.Destination;
            if (fallthroughDest != null)
            {
                await TraverseCfgAllPathsAsync(
                fallthroughDest,
                cfg,
                newVisitedBlocks,
                semanticModel,
                pathAfterOperations,
                context,
                currentDepth,
                cancellationToken
                );
            }
        }
        else
        {
            // No successors, path ends here
            SaveCompletedPath(pathAfterOperations, context);
        }
    }

    private async Task ProcessOperationSinglePathAsync(
    IOperation operation,
    SemanticModel semanticModel,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        switch (operation)
        {
            case IAwaitOperation awaitOp when context.UnwrapAsync:
                ProcessAwait(awaitOp, currentSteps, context, currentDepth);
                break;

            case IInvocationOperation invocation:
                await ProcessInvocationSinglePathAsync(invocation, currentSteps, context, currentDepth, cancellationToken);
                break;

            case ISimpleAssignmentOperation assignment:
                ProcessAssignment(assignment, currentSteps, context, currentDepth);
                break;

            case IObjectCreationOperation objectCreation:
                ProcessObjectCreation(objectCreation, currentSteps, context, currentDepth);
                break;

            case IVariableDeclaratorOperation variableDeclarator:
                ProcessVariableDeclaration(variableDeclarator, currentSteps, context, currentDepth);
                break;

            case IReturnOperation returnOp:
                ProcessReturn(returnOp, currentSteps, context, currentDepth);
                break;
        }
    }

    private async Task ProcessOperationAllPathsAsync(
    IOperation operation,
    SemanticModel semanticModel,
    List<TraceStep> currentPath,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        switch (operation)
        {
            case IAwaitOperation awaitOp when context.UnwrapAsync:
                ProcessAwait(awaitOp, currentPath, context, currentDepth);
                break;

            case IInvocationOperation invocation:
                await ProcessInvocationAllPathsAsync(invocation, currentPath, context, currentDepth, cancellationToken);
                break;

            case ISimpleAssignmentOperation assignment:
                ProcessAssignment(assignment, currentPath, context, currentDepth);
                break;

            case IObjectCreationOperation objectCreation:
                ProcessObjectCreation(objectCreation, currentPath, context, currentDepth);
                break;

            case IVariableDeclaratorOperation variableDeclarator:
                ProcessVariableDeclaration(variableDeclarator, currentPath, context, currentDepth);
                break;

            case IReturnOperation returnOp:
                ProcessReturn(returnOp, currentPath, context, currentDepth);
                break;
        }
    }

    private async Task ProcessInvocationSinglePathAsync(
    IInvocationOperation invocation,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        // Check for LINQ operations first
        if (context.UnwrapLinq && LinqQueryAnalyzer.IsLinqMethod(invocation))
        {
            ProcessLinqQuery(invocation, currentSteps, context, currentDepth);
            // Still continue to trace the method call
        }

        var method = invocation.TargetMethod;
        var methodFqn = method.ToDisplayString();

        // Check if this is the exit point
        if (context.ExitPointFqn != null && string.Equals(methodFqn, context.ExitPointFqn, StringComparison.OrdinalIgnoreCase))
        {
            currentSteps.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.Exit,
                Depth = currentDepth,
                Description = $"EXIT: {methodFqn}",
                MethodFqn = methodFqn,
                Variables = GetInvocationArguments(invocation),
                SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
            });
            context.ExitPointReached = true;
            return;
        }

        // Add method call step
        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.MethodCall,
            Depth = currentDepth,
            Description = $"CALL: {method.Name}({string.Join(", ", method.Parameters.Select(p => p.Type.Name))})",
            MethodFqn = methodFqn,
            Variables = GetInvocationArguments(invocation),
            SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
        });

        // Recursively trace called method
        if (currentDepth < context.MaxDepth)
        {
            await TraceMethodSinglePathAsync(method, currentSteps, context, currentDepth + 1, cancellationToken);
        }
    }

    private async Task ProcessInvocationAllPathsAsync(
    IInvocationOperation invocation,
    List<TraceStep> currentPath,
    TraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        // Check for LINQ operations first
        if (context.UnwrapLinq && LinqQueryAnalyzer.IsLinqMethod(invocation))
        {
            ProcessLinqQuery(invocation, currentPath, context, currentDepth);
            // Still continue to trace the method call
        }

        var method = invocation.TargetMethod;
        var methodFqn = method.ToDisplayString();

        // Check if this is the exit point
        if (context.ExitPointFqn != null && string.Equals(methodFqn, context.ExitPointFqn, StringComparison.OrdinalIgnoreCase))
        {
            currentPath.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.Exit,
                Depth = currentDepth,
                Description = $"EXIT: {methodFqn}",
                MethodFqn = methodFqn,
                Variables = GetInvocationArguments(invocation),
                SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
            });
            return;
        }

        // Add method call step
        currentPath.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.MethodCall,
            Depth = currentDepth,
            Description = $"CALL: {method.Name}({string.Join(", ", method.Parameters.Select(p => p.Type.Name))})",
            MethodFqn = methodFqn,
            Variables = GetInvocationArguments(invocation),
            SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
        });

        // Recursively trace called method
        if (currentDepth < context.MaxDepth)
        {
            await TraceMethodAllPathsAsync(method, currentPath, context, currentDepth + 1, cancellationToken);
        }
    }

    private void ProcessAssignment(
    ISimpleAssignmentOperation assignment,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var target = assignment.Target;
        var value = assignment.Value;

        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.Assignment,
            Depth = currentDepth,
            Description = $"ASSIGN: {target.Syntax} = {value.Syntax.ToString().Trim()}",
            Variables =
        [
        new VariableInfo
{
Name = target.Syntax.ToString(),
Type = target.Type?.ToDisplayString() ?? "unknown",
Operation = "assigned",
Scope = DetermineScope(target)
}
        ],
            SourceLocation = GetSourceLocationFromSyntax(assignment.Syntax)
        });
    }

    private void ProcessObjectCreation(
    IObjectCreationOperation objectCreation,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var type = objectCreation.Type?.ToDisplayString() ?? "unknown";

        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.ObjectCreation,
            Depth = currentDepth,
            Description = $"NEW: {type}",
            Variables =
        [
        new VariableInfo
{
Name = type,
Type = type,
Operation = "created",
Scope = "local"
}
        ],
            SourceLocation = GetSourceLocationFromSyntax(objectCreation.Syntax)
        });
    }

    private void ProcessVariableDeclaration(
    IVariableDeclaratorOperation variableDeclarator,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var variable = variableDeclarator.Symbol;

        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.Assignment,
            Depth = currentDepth,
            Description = $"DECLARE: {variable.Type.ToDisplayString()} {variable.Name}",
            Variables =
        [
        new VariableInfo
{
Name = variable.Name,
Type = variable.Type.ToDisplayString(),
Operation = "declared",
Scope = "local"
}
        ],
            SourceLocation = GetSourceLocation(variable)
        });
    }

    private void ProcessReturn(
    IReturnOperation returnOp,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var returnValue = returnOp.ReturnedValue?.Syntax.ToString().Trim() ?? "void";

        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.Return,
            Depth = currentDepth,
            Description = $"RETURN: {returnValue}",
            SourceLocation = GetSourceLocationFromSyntax(returnOp.Syntax)
        });
    }

    private List<VariableInfo> GetMethodParameters(IMethodSymbol method)
    {
        return method.Parameters.Select(
        p =>
        new VariableInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(),
            Operation = "parameter",
            Scope = "parameter"
        }
        ).ToList();
    }

    private List<VariableInfo> GetInvocationArguments(IInvocationOperation invocation)
    {
        return invocation.Arguments.Select(
        arg =>
        new VariableInfo
        {
            Name = arg.Parameter?.Name ?? "?",
            Type = arg.Parameter?.Type.ToDisplayString() ?? "unknown",
            Operation = "passed",
            Scope = "parameter"
        }
        ).ToList();
    }

    private string DetermineScope(IOperation operation)
    {
        return operation switch
        {
            { Parent: IParameterReferenceOperation } => "parameter",
            { Parent: IFieldReferenceOperation } => "field",
            _ => "local"
        };
    }

    private string? GetSourceLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        if (location == null || !location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return $"{Path.GetFileName(lineSpan.Path)}:{lineSpan.StartLinePosition.Line + 1}";
    }

    private string? GetSourceLocationFromSyntax(SyntaxNode syntax)
    {
        var location = syntax.GetLocation();
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return $"{Path.GetFileName(lineSpan.Path)}:{lineSpan.StartLinePosition.Line + 1}";
    }

    private void ProcessLinqQuery(
    IInvocationOperation invocation,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var linqInfo = LinqQueryAnalyzer.GetLinqInfo(invocation);

        // Add LINQ query step
        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.LinqQuery,
            Depth = currentDepth,
            Description = $"LINQ: {linqInfo.MethodName}({string.Join(", ", linqInfo.LambdaExpressions.Select(l => l.Expression))})",
            SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax),
            LinqInfo = new LinqQueryInfo
            {
                QueryExpression = linqInfo.FullExpression,
                QueryType = linqInfo.QueryType,
                IsDeferred = linqInfo.IsDeferred,
                Operations = new List<string> { linqInfo.MethodName }
            }
        });

        // Add lambda execution steps
        foreach (var lambda in linqInfo.LambdaExpressions)
        {
            currentSteps.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.LambdaCall,
                Depth = currentDepth + 1,
                Description = $"LAMBDA: {lambda.Expression}",
                SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
            });
        }

        // Add deferred/immediate execution note
        if (linqInfo.IsDeferred)
        {
            currentSteps.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.LinqQuery,
                Depth = currentDepth,
                Description = $"DEFERRED: Query not executed yet (returns {linqInfo.QueryType})",
                SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
            });
        }
        else if (linqInfo.IsImmediate)
        {
            currentSteps.Add(new TraceStep
            {
                StepNumber = ++context.StepCounter,
                Type = TraceStepType.LinqQuery,
                Depth = currentDepth,
                Description = $"ENUMERATE: Executing deferred query via {linqInfo.MethodName}",
                SourceLocation = GetSourceLocationFromSyntax(invocation.Syntax)
            });
        }
    }

    private void ProcessAwait(
    IAwaitOperation awaitOp,
    List<TraceStep> currentSteps,
    TraceContext context,
    int currentDepth
    )
    {
        var awaitedExpression = awaitOp.Operation.Syntax.ToString().Trim();
        var awaitedType = awaitOp.Operation.Type?.ToDisplayString();

        // Check for ConfigureAwait usage
        bool configureAwaitUsed = false;
        bool continueOnCapturedContext = true;

        if (awaitOp.Operation is IInvocationOperation invocation)
        {
            var methodName = invocation.TargetMethod.Name;
            if (methodName == "ConfigureAwait")
            {
                configureAwaitUsed = true;
                // Try to get the boolean argument
                if (invocation.Arguments.Length > 0)
                {
                    var arg = invocation.Arguments[0];
                    if (arg.Value is ILiteralOperation literal && literal.ConstantValue.HasValue)
                    {
                        continueOnCapturedContext = (bool)literal.ConstantValue.Value!;
                    }
                }
            }
        }

        // Add await step
        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.AsyncAwait,
            Depth = currentDepth,
            Description = $"AWAIT: {awaitedExpression}",
            SourceLocation = GetSourceLocationFromSyntax(awaitOp.Syntax),
            AsyncInfo = new AsyncAwaitInfo
            {
                AwaitedExpression = awaitedExpression,
                TaskType = awaitedType,
                ConfigureAwaitUsed = configureAwaitUsed,
                ContinueOnCapturedContext = continueOnCapturedContext
            }
        });

        // Add continuation step
        currentSteps.Add(new TraceStep
        {
            StepNumber = ++context.StepCounter,
            Type = TraceStepType.AsyncContinuation,
            Depth = currentDepth,
            Description = $"CONTINUATION: after {awaitedExpression}",
            SourceLocation = GetSourceLocationFromSyntax(awaitOp.Syntax)
        });
    }

    private async Task<IMethodSymbol?> FindMethodSymbolAsync(
    string fqn,
    CancellationToken cancellationToken
    )
    {
        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, cancellationToken);
        return symbol as IMethodSymbol;
    }
}

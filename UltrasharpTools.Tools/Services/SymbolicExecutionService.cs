
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Performs symbolic execution analysis using Roslyn CFG and Z3 SMT solver.
/// Simplified implementation for proof-of-concept.
/// </summary>
public sealed class SymbolicExecutionService : ISymbolicExecutionService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<SymbolicExecutionService> _logger;
    private readonly Z3ConstraintSolver _z3Solver;

    public SymbolicExecutionService(
    ISolutionManager solutionManager,
    ILogger<SymbolicExecutionService> logger,
    Z3ConstraintSolver z3Solver)
    {
        _solutionManager = solutionManager;
        _logger = logger;
        _z3Solver = z3Solver;
    }

    public async Task<SymbolicExecutionResult> AnalyzePathFeasibilityAsync(
    string entryPointFqn,
    string? exitPointFqn = null,
    int maxDepth = 10,
    Dictionary<string, string>? initialConstraints = null,
    CancellationToken cancellationToken = default)
    {
        try
        {
            // Find entry point method
            var entrySymbol = await _solutionManager.FindRoslynSymbolAsync(entryPointFqn, cancellationToken);
            if (entrySymbol is not IMethodSymbol entryMethod)
            {
                return CreateErrorResult(entryPointFqn, exitPointFqn, $"Entry point not found: {entryPointFqn}");
            }

            // Get method syntax and semantic model
            var syntaxRef = entryMethod.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
            {
                return CreateErrorResult(entryPointFqn, exitPointFqn, "Entry point has no syntax reference");
            }

            var methodSyntax = await syntaxRef.GetSyntaxAsync(cancellationToken);
            var semanticModel = await _solutionManager.CurrentSolution!.GetDocument(syntaxRef.SyntaxTree)!
            .GetSemanticModelAsync(cancellationToken);

            if (semanticModel == null)
            {
                return CreateErrorResult(entryPointFqn, exitPointFqn, "Could not get semantic model");
            }

            // Get control flow graph
            ControlFlowGraph? cfg = null;
            try
            {
                cfg = ControlFlowGraph.Create(methodSyntax, semanticModel, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create CFG for {Method}", entryPointFqn);
                return CreateErrorResult(entryPointFqn, exitPointFqn, $"CFG creation failed: {ex.Message}");
            }

            // Initialize symbolic state with parameters
            var initialState = InitializeSymbolicState(entryMethod, initialConstraints);

            // Perform symbolic execution
            var context = new SymbolicExecutionContext
            {
                Paths = new List<SymbolicPath>(),
                Issues = new List<PotentialIssue>(),
                PathIdCounter = 0
            };

            // Check if CFG is valid before accessing
            if (cfg == null || cfg.Blocks == null || cfg.Blocks.Length == 0)
            {
                _logger.LogWarning("CFG for {EntryPoint} is null or has no blocks", entryPointFqn);
                return new SymbolicExecutionResult
                {
                    EntryPointFqn = entryPointFqn,
                    ExitPointFqn = exitPointFqn,
                    Paths = new List<SymbolicPath>(),
                    Issues = new List<PotentialIssue>()
                };
            }

            await ExplorePathsAsync(
            cfg,
            cfg.Blocks![0],
            new List<SymbolicConstraint>(),
            initialState,
            new List<SymbolicStep>(),
            context,
            currentDepth: 0,
            maxDepth,
            exitPointFqn,
            cancellationToken
            );

            var paths = context.Paths;
            var issues = context.Issues;

            // Check each path's feasibility
            foreach (var path in paths)
            {
                CheckPathFeasibility(path);
            }

            var result = new SymbolicExecutionResult
            {
                EntryPointFqn = entryPointFqn,
                ExitPointFqn = exitPointFqn,
                Paths = paths,
                Issues = issues,
                TotalConstraints = paths.Sum(p => p.Constraints.Count),
                ExitPointReachable = exitPointFqn == null || paths.Any(p => p.IsFeasible && p.Steps.Any(s => s.Description.Contains("EXIT"))),
                MaxDepthReached = paths.Any() ? paths.Max(p => p.Depth) : 0
            };

            _logger.LogInformation(
            "Symbolic execution completed: {Feasible}/{Total} paths feasible, {Issues} issues found",
            result.FeasiblePaths,
            result.TotalPaths,
            result.Issues.Count
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during symbolic execution for {Entry}", entryPointFqn);
            return CreateErrorResult(entryPointFqn, exitPointFqn, $"Execution error: {ex.Message}");
        }
    }

    private Dictionary<string, SymbolicValue> InitializeSymbolicState(
    IMethodSymbol method,
    Dictionary<string, string>? initialConstraints)
    {
        var state = new Dictionary<string, SymbolicValue>();

        foreach (var param in method.Parameters)
        {
            var paramName = param.Name;
            var paramType = param.Type.ToDisplayString();

            // Create symbolic value based on type
            SymbolicValue symbolicValue = paramType switch
            {
                "int" or "System.Int32" => SymbolicValue.Integer(paramName),
                "bool" or "System.Boolean" => SymbolicValue.Boolean(paramName),
                "string" or "System.String" => SymbolicValue.String(paramName),
                _ when param.Type.IsReferenceType => SymbolicValue.Reference(paramType, paramName),
                _ when param.Type is IArrayTypeSymbol arrayType =>
                SymbolicValue.Array(arrayType.ElementType.ToDisplayString(), paramName),
                _ => SymbolicValue.Reference(paramType, paramName)
            };

            state[paramName] = symbolicValue;
        }

        return state;
    }

    private async Task ExplorePathsAsync(
    ControlFlowGraph cfg,
    BasicBlock currentBlock,
    List<SymbolicConstraint> currentConstraints,
    Dictionary<string, SymbolicValue> currentState,
    List<SymbolicStep> currentSteps,
    SymbolicExecutionContext context,
    int currentDepth,
    int maxDepth,
    string? exitPointFqn,
    CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || currentBlock == null)
        {
            // Path terminated - save it
            SavePath(context, currentConstraints, currentSteps);
            return;
        }

        // Process operations in this block
        foreach (var operation in currentBlock.Operations)
        {
            ProcessOperation(operation, currentConstraints, currentState, currentSteps, context.Issues, currentDepth);
        }

        // Handle branching
        var branchValue = currentBlock.BranchValue;
        if (branchValue != null && currentBlock.ConditionalSuccessor != null)
        {
            // Conditional branch - fork execution

            // True branch
            var trueConstraints = new List<SymbolicConstraint>(currentConstraints);
            var trueCondition = ExtractCondition(branchValue, currentState, isTrue: true);
            if (trueCondition != null)
            {
                trueConstraints.Add(trueCondition);
            }

            var trueState = new Dictionary<string, SymbolicValue>(currentState);
            var trueSteps = new List<SymbolicStep>(currentSteps)
{
new SymbolicStep
{
StepNumber = currentSteps.Count + 1,
Type = TraceStepType.Conditional,
Description = $"Branch: {branchValue.Syntax} → TRUE",
SymbolicState = StateToStrings(trueState),
AddedConstraint = trueCondition?.ToString()
}
};

            if (currentBlock.ConditionalSuccessor?.Destination != null)
            {
                await ExplorePathsAsync(
                cfg,
                currentBlock.ConditionalSuccessor.Destination,
                trueConstraints,
                trueState,
                trueSteps,
                context,
                currentDepth + 1,
                maxDepth,
                exitPointFqn,
                cancellationToken
                );
            }

            // False branch (fallthrough)
            if (currentBlock.FallThroughSuccessor?.Destination != null)
            {
                var falseConstraints = new List<SymbolicConstraint>(currentConstraints);
                var falseCondition = ExtractCondition(branchValue, currentState, isTrue: false);
                if (falseCondition != null)
                {
                    falseConstraints.Add(falseCondition);
                }

                var falseState = new Dictionary<string, SymbolicValue>(currentState);
                var falseSteps = new List<SymbolicStep>(currentSteps)
{
new SymbolicStep
{
StepNumber = currentSteps.Count + 1,
Type = TraceStepType.Conditional,
Description = $"Branch: {branchValue.Syntax} → FALSE",
SymbolicState = StateToStrings(falseState),
AddedConstraint = falseCondition?.ToString()
}
};

                await ExplorePathsAsync(
                cfg,
                currentBlock.FallThroughSuccessor.Destination,
                falseConstraints,
                falseState,
                falseSteps,
                context,
                currentDepth + 1,
                maxDepth,
                exitPointFqn,
                cancellationToken
                );
            }
        }
        else
        {
            // No branching - continue to next block
            var nextBranch = currentBlock.FallThroughSuccessor ?? currentBlock.ConditionalSuccessor;
            if (nextBranch?.Destination != null)
            {
                await ExplorePathsAsync(
                cfg,
                nextBranch.Destination,
                currentConstraints,
                currentState,
                currentSteps,
                context,
                currentDepth + 1,
                maxDepth,
                exitPointFqn,
                cancellationToken
                );
            }
            else
            {
                // End of path
                SavePath(context, currentConstraints, currentSteps);
            }
        }
    }

    private void ProcessOperation(
    IOperation operation,
    List<SymbolicConstraint> constraints,
    Dictionary<string, SymbolicValue> state,
    List<SymbolicStep> steps,
    List<PotentialIssue> issues,
    int depth)
    {
        // ✅ Enhanced operation processing with state updates

        // Handle different operation types
        switch (operation)
        {
            case ISimpleAssignmentOperation assignment:
                // Update state for assignment: target = value
                var targetName = assignment.Target.Syntax?.ToString().Trim();
                if (targetName != null)
                {
                    var valueName = assignment.Value.Syntax?.ToString().Trim() ?? "value";
                    var valueType = assignment.Value.Type?.ToDisplayString() ?? "object";

                    // Create symbolic value for assignment
                    state[targetName] = valueType switch
                    {
                        "int" or "System.Int32" => SymbolicValue.Integer(valueName),
                        "bool" or "System.Boolean" => SymbolicValue.Boolean(valueName),
                        "string" or "System.String" => SymbolicValue.String(valueName),
                        _ => SymbolicValue.Reference(valueType, valueName)
                    };

                    steps.Add(new SymbolicStep
                    {
                        StepNumber = steps.Count + 1,
                        Type = TraceStepType.Assignment,
                        Description = $"{targetName} = {valueName}",
                        SymbolicState = StateToStrings(state)
                    });
                }
                break;

            case IVariableDeclaratorOperation declarator:
                // Variable declaration with initializer
                var varName = declarator.Symbol.Name;
                if (declarator.Initializer != null)
                {
                    var initValue = declarator.Initializer.Value.Syntax?.ToString().Trim() ?? "init";
                    var varType = declarator.Symbol.Type.ToDisplayString();

                    state[varName] = varType switch
                    {
                        "int" or "System.Int32" => SymbolicValue.Integer(initValue),
                        "bool" or "System.Boolean" => SymbolicValue.Boolean(initValue),
                        "string" or "System.String" => SymbolicValue.String(initValue),
                        _ => SymbolicValue.Reference(varType, initValue)
                    };

                    steps.Add(new SymbolicStep
                    {
                        StepNumber = steps.Count + 1,
                        Type = TraceStepType.Assignment, // Use Assignment for variable declarations
                        Description = $"var {varName} = {initValue}",
                        SymbolicState = StateToStrings(state)
                    });
                }
                break;

            case IInvocationOperation invocation:
                // Method call - simplified handling
                var methodName = invocation.TargetMethod.Name;
                steps.Add(new SymbolicStep
                {
                    StepNumber = steps.Count + 1,
                    Type = TraceStepType.MethodCall,
                    Description = $"Call: {methodName}()",
                    SymbolicState = StateToStrings(state)
                });
                break;

            case IReturnOperation returnOp:
                // Return statement
                var returnValue = returnOp.ReturnedValue?.Syntax?.ToString().Trim() ?? "void";
                steps.Add(new SymbolicStep
                {
                    StepNumber = steps.Count + 1,
                    Type = TraceStepType.Return,
                    Description = $"return {returnValue}",
                    SymbolicState = StateToStrings(state)
                });
                break;

            default:
                // Generic operation
                steps.Add(new SymbolicStep
                {
                    StepNumber = steps.Count + 1,
                    Type = TraceStepType.Assignment, // Generic fallback
                    Description = operation.Syntax?.ToString().Trim() ?? "operation",
                    SymbolicState = StateToStrings(state)
                });
                break;
        }

        // ✅ Check for potential issues
        DetectIssues(operation, constraints, state, issues);
    }

    private void DetectIssues(
    IOperation operation,
    List<SymbolicConstraint> constraints,
    Dictionary<string, SymbolicValue> state,
    List<PotentialIssue> issues)
    {
        // ✅ 1. Division by zero detection
        if (operation is IBinaryOperation binaryOp &&
        (binaryOp.OperatorKind == BinaryOperatorKind.Divide ||
        binaryOp.OperatorKind == BinaryOperatorKind.Remainder))
        {
            var rightOperand = binaryOp.RightOperand.Syntax?.ToString().Trim();

            // Check if divisor is constant zero
            if (rightOperand == "0")
            {
                issues.Add(new PotentialIssue
                {
                    Type = IssueType.DivisionByZero,
                    Description = $"Division by zero: {operation.Syntax}",
                    SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                    TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                    Severity = IssueSeverity.Error
                });
            }
            // Check if divisor could be zero based on constraints
            else if (rightOperand != null && !IsDefinitelyNonZero(rightOperand, constraints))
            {
                issues.Add(new PotentialIssue
                {
                    Type = IssueType.DivisionByZero,
                    Description = $"Potential division by zero: {operation.Syntax}",
                    SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                    TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                    Severity = IssueSeverity.Warning
                });
            }
        }

        // ✅ 2. Null reference detection
        if (operation is IMemberReferenceOperation memberRef && memberRef.Instance != null)
        {
            var instanceName = memberRef.Instance.Syntax?.ToString().Trim();

            if (instanceName != null)
            {
                // Check state for null tracking
                if (state.TryGetValue(instanceName, out var symbolicValue))
                {
                    if (symbolicValue is SymbolicReference refValue && refValue.IsNull)
                    {
                        issues.Add(new PotentialIssue
                        {
                            Type = IssueType.NullReference,
                            Description = $"Null reference: {operation.Syntax}",
                            SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                            TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                            Severity = IssueSeverity.Error
                        });
                    }
                    else if (symbolicValue is SymbolicReference refVal && refVal.IsUnknown)
                    {
                        // Unknown nullability
                        issues.Add(new PotentialIssue
                        {
                            Type = IssueType.NullReference,
                            Description = $"Potential null reference: {operation.Syntax}",
                            SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                            TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                            Severity = IssueSeverity.Warning
                        });
                    }
                }
            }
        }

        // ✅ 3. Array out of bounds detection
        if (operation is IArrayElementReferenceOperation arrayRef)
        {
            var arrayName = arrayRef.ArrayReference.Syntax?.ToString().Trim();
            var indices = arrayRef.Indices;

            if (arrayName != null && indices.Length > 0 && state.TryGetValue(arrayName, out var symbolicValue))
            {
                if (symbolicValue is SymbolicArray arrayValue && arrayValue.Length.HasValue)
                {
                    // Check if index could be out of bounds
                    var indexExpr = indices[0].Syntax?.ToString().Trim();
                    if (indexExpr != null && int.TryParse(indexExpr, out var constantIndex))
                    {
                        if (constantIndex < 0 || constantIndex >= arrayValue.Length.Value)
                        {
                            issues.Add(new PotentialIssue
                            {
                                Type = IssueType.ArrayOutOfBounds,
                                Description = $"Array index out of bounds: {operation.Syntax}",
                                SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                                TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                                Severity = IssueSeverity.Error
                            });
                        }
                    }
                }
            }
        }

        // ✅ 4. Invalid cast detection
        if (operation is IConversionOperation conversion && !conversion.IsImplicit)
        {
            // Check for potentially invalid explicit casts
            if (conversion.Operand.Type != null && conversion.Type != null)
            {
                var fromType = conversion.Operand.Type.ToDisplayString();
                var toType = conversion.Type.ToDisplayString();

                // Simplified: Flag downcasts as potential issues
                if (!IsCompatibleCast(fromType, toType))
                {
                    issues.Add(new PotentialIssue
                    {
                        Type = IssueType.InvalidCast,
                        Description = $"Potential invalid cast: ({toType}){conversion.Operand.Syntax}",
                        SourceLocation = operation.Syntax?.GetLocation().ToString() ?? "unknown",
                        TriggeringConstraints = constraints.Select(c => c.ToString()).ToList(),
                        Severity = IssueSeverity.Warning
                    });
                }
            }
        }
    }

    /// <summary>
    /// Check if a variable is definitely non-zero based on constraints.
    /// </summary>
    private bool IsDefinitelyNonZero(string variable, List<SymbolicConstraint> constraints)
    {
        foreach (var constraint in constraints)
        {
            if (constraint is ComparisonConstraint comp)
            {
                // Check constraints like "x > 0" or "x != 0"
                if (comp.Left == variable && comp.Operator == ComparisonOp.NotEqual && comp.Right == "0")
                {
                    return true;
                }
                if (comp.Left == variable && comp.Operator == ComparisonOp.GreaterThan && comp.Right == "0")
                {
                    return true;
                }
                if (comp.Left == "0" && comp.Operator == ComparisonOp.LessThan && comp.Right == variable)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Check if a cast is compatible (simplified heuristic).
    /// </summary>
    private bool IsCompatibleCast(string fromType, string toType)
    {
        // Simplified: Allow value type conversions and reference type upcasts
        // In production, would use Roslyn's ConversionKind analysis

        // Allow numeric conversions
        var numericTypes = new[] { "int", "long", "short", "byte", "double", "float", "decimal" };
        if (numericTypes.Contains(fromType) && numericTypes.Contains(toType))
        {
            return true;
        }

        // Allow string conversions
        if (toType == "string")
        {
            return true;
        }

        // Conservative: Flag other casts as potentially invalid
        return false;
    }

    private SymbolicConstraint? ExtractCondition(
    IOperation branchValue,
    Dictionary<string, SymbolicValue> state,
    bool isTrue)
    {
        // Simplified condition extraction
        if (branchValue is IBinaryOperation binaryOp)
        {
            var left = binaryOp.LeftOperand.Syntax?.ToString() ?? "left";
            var right = binaryOp.RightOperand.Syntax?.ToString() ?? "right";

            var op = binaryOp.OperatorKind switch
            {
                BinaryOperatorKind.Equals => ComparisonOp.Equal,
                BinaryOperatorKind.NotEquals => ComparisonOp.NotEqual,
                BinaryOperatorKind.GreaterThan => ComparisonOp.GreaterThan,
                BinaryOperatorKind.LessThan => ComparisonOp.LessThan,
                BinaryOperatorKind.GreaterThanOrEqual => ComparisonOp.GreaterThanOrEqual,
                BinaryOperatorKind.LessThanOrEqual => ComparisonOp.LessThanOrEqual,
                _ => (ComparisonOp?)null
            };

            if (op.HasValue)
            {
                var constraint = new ComparisonConstraint
                {
                    Left = left,
                    Operator = isTrue ? op.Value : NegateOp(op.Value),
                    Right = right
                };

                return constraint;
            }
        }

        return null;
    }

    private ComparisonOp NegateOp(ComparisonOp op) => op switch
    {
        ComparisonOp.Equal => ComparisonOp.NotEqual,
        ComparisonOp.NotEqual => ComparisonOp.Equal,
        ComparisonOp.GreaterThan => ComparisonOp.LessThanOrEqual,
        ComparisonOp.LessThan => ComparisonOp.GreaterThanOrEqual,
        ComparisonOp.GreaterThanOrEqual => ComparisonOp.LessThan,
        ComparisonOp.LessThanOrEqual => ComparisonOp.GreaterThan,
        _ => op
    };

    private void SavePath(
    SymbolicExecutionContext context,
    List<SymbolicConstraint> constraints,
    List<SymbolicStep> steps)
    {
        var path = new SymbolicPath
        {
            PathId = context.PathIdCounter++,
            Steps = new List<SymbolicStep>(steps),
            Constraints = constraints.Select(c => c.ToString()).ToList(),
            IsFeasible = true // Will be checked later
        };

        context.Paths.Add(path);
    }

    private sealed class SymbolicExecutionContext
    {
        public required List<SymbolicPath> Paths { get; init; }
        public required List<PotentialIssue> Issues { get; init; }
        public int PathIdCounter { get; set; }
    }

    private void CheckPathFeasibility(SymbolicPath path)
    {
        if (path.Constraints.Count == 0)
        {
            // No constraints - always feasible
            return;
        }

        // Parse string constraints back to SymbolicConstraint objects
        var constraints = new List<SymbolicConstraint>();

        foreach (var constraintStr in path.Constraints)
        {
            var constraint = ParseConstraintString(constraintStr);
            if (constraint != null)
            {
                constraints.Add(constraint);
            }
        }

        if (constraints.Count == 0)
        {
            // No valid constraints - assume feasible
            return;
        }

        // ✅ Use Z3 solver to check feasibility
        var result = _z3Solver.IsSatisfiable(constraints);

        // Update path with results (using reflection to bypass init-only properties)
        var pathType = typeof(SymbolicPath);

        if (!result.IsSatisfiable)
        {
            // Mark as infeasible
            var isFeasibleProp = pathType.GetProperty(nameof(SymbolicPath.IsFeasible))!;
            isFeasibleProp.SetValue(path, false);

            var reasonProp = pathType.GetProperty(nameof(SymbolicPath.InfeasibilityReason))!;
            reasonProp.SetValue(path, result.Reason ?? "Unsatisfiable constraints");

            _logger.LogDebug("Path {PathId} is INFEASIBLE: {Reason}",
            path.PathId, result.Reason);
        }
        else if (result.ExampleInputs != null && result.ExampleInputs.Count > 0)
        {
            // Add example inputs
            var exampleProp = pathType.GetProperty(nameof(SymbolicPath.ExampleInputs))!;
            exampleProp.SetValue(path, result.ExampleInputs);

            _logger.LogDebug("Path {PathId} is FEASIBLE. Example inputs: {Inputs}",
            path.PathId, string.Join(", ", result.ExampleInputs.Select(kv => $"{kv.Key}={kv.Value}")));
        }
    }

    /// <summary>
    /// Parse a constraint string back to SymbolicConstraint object.
    /// Handles format: "x > 5", "a == b", etc.
    /// </summary>
    private SymbolicConstraint? ParseConstraintString(string constraintStr)
    {
        try
        {
            // Handle comparison operators: ==, !=, >, <, >=, <=
            var operators = new[] { ">=", "<=", "==", "!=", ">", "<" };

            foreach (var op in operators)
            {
                var index = constraintStr.IndexOf($" {op} ");
                if (index > 0)
                {
                    var left = constraintStr.Substring(0, index).Trim();
                    var right = constraintStr.Substring(index + op.Length + 2).Trim();

                    var compOp = op switch
                    {
                        "==" => ComparisonOp.Equal,
                        "!=" => ComparisonOp.NotEqual,
                        ">" => ComparisonOp.GreaterThan,
                        "<" => ComparisonOp.LessThan,
                        ">=" => ComparisonOp.GreaterThanOrEqual,
                        "<=" => ComparisonOp.LessThanOrEqual,
                        _ => (ComparisonOp?)null
                    };

                    if (compOp.HasValue)
                    {
                        return new ComparisonConstraint
                        {
                            Left = left,
                            Operator = compOp.Value,
                            Right = right
                        };
                    }
                }
            }

            // Handle null checks: "x == null", "x != null"
            if (constraintStr.EndsWith("== null"))
            {
                var variable = constraintStr.Replace("== null", "").Trim();
                return new NullCheckConstraint { Variable = variable, IsNull = true };
            }
            if (constraintStr.EndsWith("!= null"))
            {
                var variable = constraintStr.Replace("!= null", "").Trim();
                return new NullCheckConstraint { Variable = variable, IsNull = false };
            }

            _logger.LogWarning("Could not parse constraint: {Constraint}", constraintStr);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing constraint: {Constraint}", constraintStr);
            return null;
        }
    }

    private Dictionary<string, string> StateToStrings(Dictionary<string, SymbolicValue> state)
    {
        return state.ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value.ToString()
        );
    }

    private SymbolicExecutionResult CreateErrorResult(string entryFqn, string? exitFqn, string error)
    {
        return new SymbolicExecutionResult
        {
            EntryPointFqn = entryFqn,
            ExitPointFqn = exitFqn,
            Paths = new List<SymbolicPath>(),
            Issues = new List<PotentialIssue>(),
            TotalConstraints = 0,
            ExitPointReachable = false,
            MaxDepthReached = 0,
            ErrorMessage = error
        };
    }
}

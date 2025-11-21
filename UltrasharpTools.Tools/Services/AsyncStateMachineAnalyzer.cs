namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Analyzes async methods and their compiler-generated state machines.
/// </summary>
public static class AsyncStateMachineAnalyzer
{
    /// <summary>
    /// Checks if a method is async.
    /// </summary>
    public static bool IsAsyncMethod(IMethodSymbol method)
    {
        // Check if method returns Task or Task<T>
        var returnType = method.ReturnType;
        if (returnType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName == "System.Threading.Tasks.Task"
            || typeName == "System.Threading.Tasks.Task<TResult>"
            || typeName == "System.Threading.Tasks.ValueTask"
            || typeName == "System.Threading.Tasks.ValueTask<TResult>";
    }

    /// <summary>
    /// Gets the state machine type for an async method.
    /// </summary>
    public static INamedTypeSymbol? GetStateMachineType(IMethodSymbol method)
    {
        // Look for AsyncStateMachineAttribute
        var attr = method
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == nameof(AsyncStateMachineAttribute));

        if (attr?.ConstructorArguments.Length > 0)
        {
            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Value is INamedTypeSymbol stateMachineType)
            {
                return stateMachineType;
            }
        }

        // If attribute not found, try to find compiler-generated class
        // Pattern: <MethodName>d__N
        var containingType = method.ContainingType;
        if (containingType == null)
        {
            return null;
        }

        var expectedPrefix = $"<{method.Name}>d__";
        var stateMachine = containingType
            .GetTypeMembers()
            .FirstOrDefault(t =>
                t.Name.StartsWith(expectedPrefix, StringComparison.Ordinal)
                && t.GetAttributes()
                    .Any(a => a.AttributeClass?.Name == nameof(CompilerGeneratedAttribute))
            );

        return stateMachine;
    }

    /// <summary>
    /// Extracts information about await expressions in a method.
    /// </summary>
    public static List<AwaitExpressionInfo> GetAwaitExpressions(
        MethodDeclarationSyntax methodSyntax,
        SemanticModel semanticModel
    )
    {
        var awaitExpressions = new List<AwaitExpressionInfo>();

        var awaitNodes = methodSyntax.DescendantNodes().OfType<AwaitExpressionSyntax>().ToList();

        int stateIndex = 0;
        foreach (var awaitNode in awaitNodes)
        {
            // Get awaited type
            var typeInfo = semanticModel.GetTypeInfo(awaitNode.Expression);
            var awaitedType = typeInfo.Type?.ToDisplayString();

            var info = new AwaitExpressionInfo
            {
                Expression = awaitNode.Expression.ToString(),
                State = stateIndex++,
                Location = awaitNode.GetLocation(),
                AwaitedType = awaitedType,
                ConfigureAwaitUsed = IsConfigureAwaitUsed(
                    awaitNode,
                    out bool continueOnCapturedContext
                ),
                ContinueOnCapturedContext = continueOnCapturedContext,
            };

            awaitExpressions.Add(info);
        }

        return awaitExpressions;
    }

    /// <summary>
    /// Checks if ConfigureAwait is used on an await expression.
    /// </summary>
    private static bool IsConfigureAwaitUsed(
        AwaitExpressionSyntax awaitExpr,
        out bool continueOnCapturedContext
    )
    {
        continueOnCapturedContext = true; // default

        // Check if expression is a method invocation
        if (awaitExpr.Expression is InvocationExpressionSyntax invocation)
        {
            // Check if it's a member access like task.ConfigureAwait(false)
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "ConfigureAwait")
                {
                    // Try to get the argument value
                    if (invocation.ArgumentList.Arguments.Count > 0)
                    {
                        var arg = invocation.ArgumentList.Arguments[0];
                        if (arg.Expression is LiteralExpressionSyntax literal)
                        {
                            continueOnCapturedContext = literal.Token.ValueText != "false";
                        }
                    }
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a method contains any await expressions.
    /// </summary>
    public static bool ContainsAwait(MethodDeclarationSyntax methodSyntax)
    {
        return methodSyntax.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();
    }
}

/// <summary>
/// Information about an await expression.
/// </summary>
public class AwaitExpressionInfo
{
    public required string Expression { get; init; }
    public int State { get; init; }
    public Location? Location { get; init; }
    public string? AwaitedType { get; init; }
    public bool ConfigureAwaitUsed { get; init; }
    public bool ContinueOnCapturedContext { get; init; }
}

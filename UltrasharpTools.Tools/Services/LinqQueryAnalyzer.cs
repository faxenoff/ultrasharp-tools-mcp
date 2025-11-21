

using Microsoft.CodeAnalysis.Operations;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Analyzes LINQ queries and their execution patterns.
/// </summary>
public static class LinqQueryAnalyzer
{
private static readonly HashSet<string> LinqMethods = new()
{
"Where", "Select", "SelectMany", "Join", "GroupBy", "GroupJoin",
"OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
"Take", "Skip", "TakeWhile", "SkipWhile",
"First", "FirstOrDefault", "Last", "LastOrDefault",
"Single", "SingleOrDefault", "Any", "All", "Count", "Sum",
"Min", "Max", "Average", "Aggregate",
"Distinct", "Union", "Intersect", "Except", "Concat",
"ToList", "ToArray", "ToDictionary", "ToHashSet",
"AsEnumerable", "AsQueryable", "Cast", "OfType"
};

private static readonly HashSet<string> DeferredExecutionMethods = new()
{
"Where", "Select", "SelectMany", "Join", "GroupBy", "GroupJoin",
"OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
"Take", "Skip", "TakeWhile", "SkipWhile",
"Distinct", "Union", "Intersect", "Except", "Concat",
"AsEnumerable", "AsQueryable", "Cast", "OfType", "Reverse"
};

private static readonly HashSet<string> ImmediateExecutionMethods = new()
{
"ToList", "ToArray", "ToDictionary", "ToHashSet",
"First", "FirstOrDefault", "Last", "LastOrDefault",
"Single", "SingleOrDefault", "Any", "All", "Count", "Sum",
"Min", "Max", "Average", "Aggregate", "Contains", "ElementAt"
};

/// <summary>
/// Checks if an invocation is a LINQ method.
/// </summary>
public static bool IsLinqMethod(IInvocationOperation invocation)
{
var methodName = invocation.TargetMethod.Name;
if (!LinqMethods.Contains(methodName))
{
return false;
}

// Check if it's an extension method from System.Linq
var containingType = invocation.TargetMethod.ContainingType;
if (containingType == null)
{
return false;
}

var typeName = containingType.ToDisplayString();
return typeName.StartsWith("System.Linq.Enumerable") ||
typeName.StartsWith("System.Linq.Queryable") ||
typeName == "System.Linq.Enumerable" ||
typeName == "System.Linq.Queryable";
}

/// <summary>
/// Gets information about a LINQ query operation.
/// </summary>
public static LinqOperationInfo GetLinqInfo(IInvocationOperation invocation)
{
var methodName = invocation.TargetMethod.Name;
var isDeferred = DeferredExecutionMethods.Contains(methodName);
var isImmediate = ImmediateExecutionMethods.Contains(methodName);

// Determine query type (IQueryable vs IEnumerable)
var returnType = invocation.Type?.ToDisplayString() ?? "unknown";
var queryType = returnType.Contains("IQueryable") ? "IQueryable" : "IEnumerable";

// Extract lambda expressions
var lambdaExpressions = ExtractLambdaExpressions(invocation);

return new LinqOperationInfo
{
MethodName = methodName,
QueryType = queryType,
IsDeferred = isDeferred,
IsImmediate = isImmediate,
LambdaExpressions = lambdaExpressions,
FullExpression = invocation.Syntax.ToString()
};
}

/// <summary>
/// Extracts lambda expressions from LINQ method arguments.
/// </summary>
private static List<LambdaExpressionInfo> ExtractLambdaExpressions(IInvocationOperation invocation)
{
var result = new List<LambdaExpressionInfo>();

foreach (var argument in invocation.Arguments)
{
if (argument.Value is IDelegateCreationOperation delegateCreation)
{
if (delegateCreation.Target is IAnonymousFunctionOperation lambda)
{
result.Add(new LambdaExpressionInfo
{
Parameters = lambda.Symbol.Parameters.Select(p => p.Name).ToList(),
Expression = lambda.Syntax.ToString(),
ReturnType = lambda.Symbol.ReturnType?.ToDisplayString()
});
}
}
else if (argument.Value is IAnonymousFunctionOperation directLambda)
{
result.Add(new LambdaExpressionInfo
{
Parameters = directLambda.Symbol.Parameters.Select(p => p.Name).ToList(),
Expression = directLambda.Syntax.ToString(),
ReturnType = directLambda.Symbol.ReturnType?.ToDisplayString()
});
}
}

return result;
}

/// <summary>
/// Analyzes a query expression syntax (from ... where ... select).
/// </summary>
public static QueryExpressionInfo? AnalyzeQueryExpression(QueryExpressionSyntax query, SemanticModel semanticModel)
{
var operations = new List<string>();
var clauses = query.Body.Clauses.ToList();

// From clause
operations.Add($"from {query.FromClause.Identifier}");

// Body clauses
foreach (var clause in clauses)
{
switch (clause)
{
case WhereClauseSyntax whereClause:
operations.Add($"where {whereClause.Condition}");
break;
case OrderByClauseSyntax orderByClause:
operations.Add($"orderby {string.Join(", ", orderByClause.Orderings)}");
break;
case JoinClauseSyntax joinClause:
operations.Add($"join {joinClause.Identifier}");
break;
case LetClauseSyntax letClause:
operations.Add($"let {letClause.Identifier} = {letClause.Expression}");
break;
}
}

// Select/Group at end
if (query.Body.SelectOrGroup is SelectClauseSyntax finalSelect)
{
operations.Add($"select {finalSelect.Expression}");
}
else if (query.Body.SelectOrGroup is GroupClauseSyntax finalGroup)
{
operations.Add($"group {finalGroup.GroupExpression} by {finalGroup.ByExpression}");
}

// Continuation
if (query.Body.Continuation != null)
{
operations.Add($"into {query.Body.Continuation.Identifier}");
}

return new QueryExpressionInfo
{
FullExpression = query.ToString(),
Operations = operations,
IsDeferred = true // Query expressions are always deferred
};
}
}

/// <summary>
/// Information about a LINQ operation.
/// </summary>
public class LinqOperationInfo
{
public required string MethodName { get; init; }
public required string QueryType { get; init; }
public required bool IsDeferred { get; init; }
public required bool IsImmediate { get; init; }
public required List<LambdaExpressionInfo> LambdaExpressions { get; init; }
public required string FullExpression { get; init; }
}

/// <summary>
/// Information about a lambda expression in LINQ.
/// </summary>
public class LambdaExpressionInfo
{
public required List<string> Parameters { get; init; }
public required string Expression { get; init; }
public string? ReturnType { get; init; }
}

/// <summary>
/// Information about a query expression (from ... where ... select).
/// </summary>
public class QueryExpressionInfo
{
public required string FullExpression { get; init; }
public required List<string> Operations { get; init; }
public required bool IsDeferred { get; init; }
}

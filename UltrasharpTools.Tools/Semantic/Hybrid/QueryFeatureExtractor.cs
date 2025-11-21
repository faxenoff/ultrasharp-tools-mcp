

using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Extensions;

using UltrasharpTools.Tools.Mcp;

namespace UltrasharpTools.Tools.Semantic.Hybrid;

/// <summary>
/// Извлекает structural features из query кода для feature-based поиска.
/// Создаёт временную компиляцию для анализа partial/incomplete кода.
/// </summary>
public sealed class QueryFeatureExtractor
{
private readonly ILogger<QueryFeatureExtractor> _logger;

// Базовые references для компиляции
private static readonly MetadataReference[] DefaultReferences =
[
MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
];

public QueryFeatureExtractor(ILogger<QueryFeatureExtractor>? logger = null)
{
_logger = logger ?? NullLogger<QueryFeatureExtractor>.Instance;
}

/// <summary>
/// Извлечь features из query метода.
/// </summary>
public async Task<MethodSemanticFeatures?> ExtractMethodFeaturesAsync(
string queryCode,
CancellationToken ct = default)
{
try
{
// Wrap код в class если это standalone method
var wrappedCode = WrapMethodCode(queryCode);

// Создать temporary compilation
var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode, cancellationToken: ct);
var compilation = CSharpCompilation.Create(
"QueryAnalysis",
new[] { syntaxTree },
DefaultReferences,
new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var semanticModel = compilation.GetSemanticModel(syntaxTree);
var root = await syntaxTree.GetRootAsync(ct);

// Найти первый метод
var methodDecl = root.DescendantNodes()
.OfType<MethodDeclarationSyntax>()
.FirstOrDefault();

if (methodDecl == null)
{
_logger.LogWarning("No method declaration found in query code");
return null;
}

var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, ct) as IMethodSymbol;
if (methodSymbol == null)
{
_logger.LogWarning("Could not get method symbol from query code");
return null;
}

// Извлечь features
return await ExtractFeaturesFromMethodAsync(
methodSymbol,
methodDecl,
semanticModel,
compilation,
ct);
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to extract method features from query code");
return null;
}
}

/// <summary>
/// Извлечь features из query класса.
/// </summary>
public async Task<ClassSemanticFeatures?> ExtractClassFeaturesAsync(
string queryCode,
CancellationToken ct = default)
{
try
{
// Parse как class
var syntaxTree = CSharpSyntaxTree.ParseText(queryCode, cancellationToken: ct);
var compilation = CSharpCompilation.Create(
"QueryAnalysis",
new[] { syntaxTree },
DefaultReferences,
new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var semanticModel = compilation.GetSemanticModel(syntaxTree);
var root = await syntaxTree.GetRootAsync(ct);

// Найти первый класс
var classDecl = root.DescendantNodes()
.OfType<ClassDeclarationSyntax>()
.FirstOrDefault();

if (classDecl == null)
{
_logger.LogWarning("No class declaration found in query code");
return null;
}

var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
if (classSymbol == null)
{
_logger.LogWarning("Could not get class symbol from query code");
return null;
}

// Извлечь features
return await ExtractFeaturesFromClassAsync(
classSymbol,
classDecl,
semanticModel,
compilation,
ct);
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to extract class features from query code");
return null;
}
}

// Private implementation

private async Task<MethodSemanticFeatures?> ExtractFeaturesFromMethodAsync(
IMethodSymbol methodSymbol,
MethodDeclarationSyntax methodDecl,
SemanticModel semanticModel,
Compilation compilation,
CancellationToken ct)
{
var methodName = methodSymbol.Name;
var fullyQualifiedMethodName = methodSymbol.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal);
var returnTypeName = methodSymbol.ReturnType.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal);
var parameterTypeNames = methodSymbol.Parameters
.Select(p => p.Type.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal))
.ToList();

// Invoked methods
var invokedMethodSignatures = new HashSet<string>();

// Operations
var operationCounts = new Dictionary<string, int>();
var distinctAccessedMemberTypes = new HashSet<string>();

// CFG features
int basicBlockCount = 0;
int conditionalBranchCount = 0;
int loopCount = 0;
int cyclomaticComplexity = 1;

// Analyze method body
SyntaxNode? bodyOrExpressionBody = methodDecl.Body ?? (SyntaxNode?)methodDecl.ExpressionBody?.Expression;

if (bodyOrExpressionBody != null)
{
try
{
// CFG analysis
var controlFlowGraph = ControlFlowGraph.Create(methodDecl, semanticModel, ct);
if (controlFlowGraph != null && controlFlowGraph.Blocks.Any())
{
basicBlockCount = controlFlowGraph.Blocks.Length;

// Count conditional branches and loops
foreach (var block in controlFlowGraph.Blocks)
{
if (block.ConditionalSuccessor != null)
{
conditionalBranchCount++;
}

// Detect loops (back edges)
foreach (var predecessor in block.Predecessors)
{
if (predecessor.Source.Ordinal > block.Ordinal)
{
loopCount++;
}
}
}

// Estimate cyclomatic complexity: edges - nodes + 2
cyclomaticComplexity = Math.Max(1, controlFlowGraph.Blocks.Length - 1);
}
}
catch (Exception ex)
{
_logger.LogDebug(ex, "CFG analysis failed for query method (expected for partial code)");
}

try
{
// IOperation analysis
var operation = semanticModel.GetOperation(bodyOrExpressionBody, ct);
if (operation != null)
{
foreach (var op in operation.DescendantsAndSelf())
{
var opKind = op.Kind.ToString();
operationCounts[opKind] = operationCounts.GetValueOrDefault(opKind) + 1;

// Invoked methods
if (op is IInvocationOperation invocation)
{
var signature = invocation.TargetMethod.ToDisplayString(
ToolHelpers.FullyQualifiedFormatWithoutGlobal);
invokedMethodSignatures.Add(signature);
}

// Accessed members
if (op is IMemberReferenceOperation memberRef)
{
var memberType = memberRef.Member.ContainingType?.ToDisplayString(
ToolHelpers.FullyQualifiedFormatWithoutGlobal);
if (memberType != null)
{
distinctAccessedMemberTypes.Add(memberType);
}
}
}
}
}
catch (Exception ex)
{
_logger.LogDebug(ex, "Operation analysis failed for query method (expected for partial code)");
}
}

return new MethodSemanticFeatures(
fullyQualifiedMethodName: fullyQualifiedMethodName,
filePath: "query",
startLine: 0,
methodName: methodName,
returnTypeName: returnTypeName,
parameterTypeNames: parameterTypeNames,
invokedMethodSignatures: invokedMethodSignatures,
basicBlockCount: basicBlockCount,
conditionalBranchCount: conditionalBranchCount,
loopCount: loopCount,
cyclomaticComplexity: cyclomaticComplexity,
operationCounts: operationCounts,
distinctAccessedMemberTypes: distinctAccessedMemberTypes
);
}

private async Task<ClassSemanticFeatures?> ExtractFeaturesFromClassAsync(
INamedTypeSymbol classSymbol,
ClassDeclarationSyntax classDecl,
SemanticModel semanticModel,
Compilation compilation,
CancellationToken ct)
{
var className = classSymbol.Name;
var fullyQualifiedClassName = classSymbol.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal);

// Base class
var baseClassName = classSymbol.BaseType?.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal);

// Implemented interfaces
var implementedInterfaceNames = classSymbol.Interfaces
.Select(i => i.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal))
.ToList();

// Count members by visibility/type
int publicMethodCount = 0;
int protectedMethodCount = 0;
int privateMethodCount = 0;
int staticMethodCount = 0;
int abstractMethodCount = 0;
int virtualMethodCount = 0;

int propertyCount = 0;
int readOnlyPropertyCount = 0;
int staticPropertyCount = 0;

int fieldCount = 0;
int staticFieldCount = 0;
int readonlyFieldCount = 0;
int constFieldCount = 0;

int eventCount = 0;
int nestedClassCount = 0;
int nestedStructCount = 0;
int nestedEnumCount = 0;
int nestedInterfaceCount = 0;

foreach (var member in classSymbol.GetMembers())
{
switch (member)
{
case IMethodSymbol method when !ToolHelpers.IsPropertyAccessor(method):
if (method.DeclaredAccessibility == Accessibility.Public) publicMethodCount++;
if (method.DeclaredAccessibility == Accessibility.Protected) protectedMethodCount++;
if (method.DeclaredAccessibility == Accessibility.Private) privateMethodCount++;
if (method.IsStatic) staticMethodCount++;
if (method.IsAbstract) abstractMethodCount++;
if (method.IsVirtual) virtualMethodCount++;
break;

case IPropertySymbol property:
propertyCount++;
if (property.IsReadOnly) readOnlyPropertyCount++;
if (property.IsStatic) staticPropertyCount++;
break;

case IFieldSymbol field:
fieldCount++;
if (field.IsStatic) staticFieldCount++;
if (field.IsReadOnly) readonlyFieldCount++;
if (field.IsConst) constFieldCount++;
break;

case IEventSymbol:
eventCount++;
break;

case INamedTypeSymbol nestedType:
switch (nestedType.TypeKind)
{
case TypeKind.Class: nestedClassCount++; break;
case TypeKind.Struct: nestedStructCount++; break;
case TypeKind.Enum: nestedEnumCount++; break;
case TypeKind.Interface: nestedInterfaceCount++; break;
}
break;
}
}

// Referenced types and namespaces
var referencedTypes = new HashSet<string>();
var usedNamespaces = new HashSet<string>();

foreach (var descendant in classDecl.DescendantNodes())
{
var symbolInfo = semanticModel.GetSymbolInfo(descendant, ct);
if (symbolInfo.Symbol != null)
{
var typeSymbol = symbolInfo.Symbol as ITypeSymbol ?? symbolInfo.Symbol.ContainingType;
if (typeSymbol != null)
{
var typeName = typeSymbol.ToDisplayString(ToolHelpers.FullyQualifiedFormatWithoutGlobal);
referencedTypes.Add(typeName);

var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
{
usedNamespaces.Add(ns);
}
}
}
}

// Method features (simplified for query)
var methodFeatures = new List<MethodSemanticFeatures>();

var totalLinesOfCode = classDecl.GetLocation().GetLineSpan().EndLinePosition.Line -
classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

return new ClassSemanticFeatures(
FullyQualifiedClassName: fullyQualifiedClassName,
FilePath: "query",
StartLine: 0,
ClassName: className,
BaseClassName: baseClassName,
ImplementedInterfaceNames: implementedInterfaceNames,
PublicMethodCount: publicMethodCount,
ProtectedMethodCount: protectedMethodCount,
PrivateMethodCount: privateMethodCount,
StaticMethodCount: staticMethodCount,
AbstractMethodCount: abstractMethodCount,
VirtualMethodCount: virtualMethodCount,
PropertyCount: propertyCount,
ReadOnlyPropertyCount: readOnlyPropertyCount,
StaticPropertyCount: staticPropertyCount,
FieldCount: fieldCount,
StaticFieldCount: staticFieldCount,
ReadonlyFieldCount: readonlyFieldCount,
ConstFieldCount: constFieldCount,
EventCount: eventCount,
NestedClassCount: nestedClassCount,
NestedStructCount: nestedStructCount,
NestedEnumCount: nestedEnumCount,
NestedInterfaceCount: nestedInterfaceCount,
AverageMethodComplexity: 1.0,
DistinctReferencedExternalTypeFqns: referencedTypes,
DistinctUsedNamespaceFqns: usedNamespaces,
TotalLinesOfCode: totalLinesOfCode,
MethodFeatures: methodFeatures
);
}

private static string WrapMethodCode(string methodCode)
{
// Если код уже содержит class declaration - вернуть как есть
if (methodCode.Contains("class ") || methodCode.Contains("record "))
{
return methodCode;
}

// Wrap в temporary class
return $@"

public class QueryClass
{{
{methodCode}
}}";
}
}

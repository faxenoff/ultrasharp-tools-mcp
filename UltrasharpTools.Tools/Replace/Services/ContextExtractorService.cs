using System.IO.Hashing;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Services;

/// <summary>
/// Сервис для извлечения полного контекста кода из PatternMatch.
/// </summary>
public sealed partial class ContextExtractorService : IContextExtractorService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<ContextExtractorService> _logger;

    public ContextExtractorService(
        ISolutionManager solutionManager,
        ILogger<ContextExtractorService>? logger = null)
    {
        _solutionManager = solutionManager;
        _logger = logger ?? NullLogger<ContextExtractorService>.Instance;
    }

    public async Task<CodeMatch> ExtractContextAsync(
        PatternMatch match,
        ReplaceScope scope,
        CancellationToken ct = default)
    {
        var document = _solutionManager.CurrentSolution?.GetDocument(match.DocumentId)
            ?? throw new InvalidOperationException($"Document not found: {match.DocumentId}");

        var syntaxTree = await document.GetSyntaxTreeAsync(ct)
            ?? throw new InvalidOperationException("Failed to get syntax tree");

        var root = await syntaxTree.GetRootAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);

        // Find syntax node at match position
        var token = root.FindToken(match.StartPosition);
        var node = token.Parent;

        if (node == null)
            throw new InvalidOperationException($"No syntax node found at position {match.StartPosition}");

        // Expand to required scope
        var containerNode = ExpandToScope(node, scope);
        var containerSymbol = semanticModel?.GetDeclaredSymbol(containerNode, ct)
            ?? semanticModel?.GetSymbolInfo(containerNode, ct).Symbol;

        // Extract metadata
        var metadata = ExtractMetadata(containerNode, semanticModel);

        var lineSpan = containerNode.GetLocation().GetLineSpan();

        // Use FQN from semantic search if available (more accurate for semantic mode)
        var containerFqn = match.SemanticFqn
            ?? containerSymbol?.ToDisplayString()
            ?? GetNodeName(containerNode);

        return new CodeMatch
        {
            Id = GenerateMatchId(match),
            ContainerFqn = containerFqn,
            FilePath = match.FilePath,
            MatchLine = match.Line,
            MatchColumn = match.Column,
            FullCode = TrimIndentation(containerNode.ToFullString()),
            MatchFragment = match.MatchedText,
            ContainerType = GetCodeUnitType(containerNode),
            ContainerStartLine = lineSpan.StartLinePosition.Line + 1,
            ContainerEndLine = lineSpan.EndLinePosition.Line + 1,
            Metadata = metadata,
            SemanticSimilarity = match.SemanticSimilarity
        };
    }

    public async Task<IReadOnlyList<CodeMatch>> ExtractContextBatchAsync(
        IReadOnlyList<PatternMatch> matches,
        ReplaceScope scope,
        CancellationToken ct = default)
    {
        var results = new List<CodeMatch>(matches.Count);

        foreach (var match in matches)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var codeMatch = await ExtractContextAsync(match, scope, ct);
                results.Add(codeMatch);
            }
            catch (Exception ex)
            {
                LogExtractionFailed(match.FilePath, match.Line, ex.Message);
                // Skip failed extractions
            }
        }

        return results;
    }

    private static SyntaxNode ExpandToScope(SyntaxNode node, ReplaceScope scope)
    {
        return scope switch
        {
            ReplaceScope.Statement => FindContainingStatement(node),
            ReplaceScope.Block => FindContainingBlock(node),
            ReplaceScope.Member => FindContainingMember(node),
            ReplaceScope.Type => FindContainingType(node),
            ReplaceScope.File => node.SyntaxTree.GetRoot(),
            _ => node
        };
    }

    private static SyntaxNode FindContainingStatement(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is StatementSyntax)
            ?? node;
    }

    private static SyntaxNode FindContainingBlock(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is BlockSyntax
                or SwitchSectionSyntax
                or IfStatementSyntax
                or WhileStatementSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or TryStatementSyntax
                or CatchClauseSyntax)
            ?? FindContainingStatement(node);
    }

    private static SyntaxNode FindContainingMember(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is MemberDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AccessorDeclarationSyntax)
            ?? node;
    }

    private static SyntaxNode FindContainingType(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is TypeDeclarationSyntax)
            ?? node;
    }

    private static Dictionary<string, object> ExtractMetadata(
        SyntaxNode containerNode,
        SemanticModel? semanticModel)
    {
        var metadata = new Dictionary<string, object>();

        // For methods
        if (containerNode is MethodDeclarationSyntax method)
        {
            metadata["parameters"] = method.ParameterList.Parameters
                .Select(p => $"{p.Type} {p.Identifier}")
                .ToList();

            metadata["returnType"] = method.ReturnType.ToString();
            metadata["isAsync"] = method.Modifiers.Any(SyntaxKind.AsyncKeyword);

            // Check for ILogger field in parent class
            if (method.Parent is TypeDeclarationSyntax containingType)
            {
                var loggerField = containingType.Members
                    .OfType<FieldDeclarationSyntax>()
                    .FirstOrDefault(f => f.Declaration.Type.ToString().Contains("ILogger"));

                metadata["hasILoggerField"] = loggerField != null;
                if (loggerField != null)
                {
                    metadata["loggerFieldName"] = loggerField.Declaration.Variables.First().Identifier.Text;
                }
            }
        }

        // For types
        if (containerNode is TypeDeclarationSyntax typeDecl)
        {
            var constructor = typeDecl.Members
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            if (constructor != null)
            {
                metadata["constructorParams"] = constructor.ParameterList.Parameters
                    .Select(p => $"{p.Type} {p.Identifier}")
                    .ToList();
            }

            // Used types (if semantic model available)
            if (semanticModel != null)
            {
                var usedTypes = new HashSet<string>();
                foreach (var identifier in containerNode.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol is ITypeSymbol typeSymbol)
                    {
                        usedTypes.Add(typeSymbol.ToDisplayString());
                    }
                }
                metadata["usedTypes"] = usedTypes.ToList();
            }
        }

        return metadata;
    }

    private static string GenerateMatchId(PatternMatch match)
    {
        var input = $"{match.FilePath}:{match.Line}:{match.Column}:{match.MatchedText}";
        var hash = XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(input));
        return $"sr-{hash:x8}";
    }

    private static string GetNodeName(SyntaxNode node)
    {
        return node switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "field",
            TypeDeclarationSyntax t => t.Identifier.Text,
            NamespaceDeclarationSyntax n => n.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax fn => fn.Name.ToString(),
            _ => node.Kind().ToString()
        };
    }

    private static CodeUnitType GetCodeUnitType(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax => CodeUnitType.Method,
        ConstructorDeclarationSyntax => CodeUnitType.Method,
        PropertyDeclarationSyntax => CodeUnitType.Property,
        FieldDeclarationSyntax => CodeUnitType.Field,
        ClassDeclarationSyntax => CodeUnitType.Type,
        InterfaceDeclarationSyntax => CodeUnitType.Type,
        StructDeclarationSyntax => CodeUnitType.Type,
        RecordDeclarationSyntax => CodeUnitType.Type,
        EnumDeclarationSyntax => CodeUnitType.Type,
        NamespaceDeclarationSyntax => CodeUnitType.Namespace,
        FileScopedNamespaceDeclarationSyntax => CodeUnitType.Namespace,
        CompilationUnitSyntax => CodeUnitType.File,
        BlockSyntax => CodeUnitType.Block,
        _ => CodeUnitType.Statement
    };

    private static string TrimIndentation(string code)
    {
        if (string.IsNullOrEmpty(code))
            return code;

        var lines = code.Split('\n');
        if (lines.Length == 0)
            return code;

        // Find minimum indentation (excluding empty lines)
        var minIndent = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.TakeWhile(char.IsWhiteSpace).Count())
            .DefaultIfEmpty(0)
            .Min();

        // Remove common indentation
        var trimmedLines = lines.Select(l =>
            l.Length >= minIndent ? l.Substring(minIndent) : l);

        return string.Join("\n", trimmedLines).Trim();
    }

    // Logging
    [LoggerMessage(Level = LogLevel.Warning, Message = "Context extraction failed for {FilePath}:{Line}: {Error}")]
    private partial void LogExtractionFailed(string filePath, int line, string error);
}

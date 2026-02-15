using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Ultrasharp.Addon.Models;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles "parse" and "parseBatch" requests.
/// Works in Phase 1 — no solution required, pure syntax tree analysis.
/// </summary>
public sealed class ParseHandler
{
    private readonly ILogger<ParseHandler> _logger;

    public ParseHandler(ILogger<ParseHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse a single C# file and return entities.
    /// Params: { "filePath": string, "content"?: string }
    /// If content is provided, parse from string. Otherwise read from disk.
    /// </summary>
    public async Task<object?> HandleParseAsync(AddonRequest request, CancellationToken ct)
    {
        var filePath = request.Params?.GetProperty("filePath").GetString() ?? "";
        string? content = null;
        if (request.Params?.TryGetProperty("content", out var contentEl) == true)
            content = contentEl.GetString();

        if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(filePath))
            content = await File.ReadAllTextAsync(filePath, ct);

        if (string.IsNullOrEmpty(content))
            return new { entities = Array.Empty<ParsedEntityDto>() };

        var entities = ParseContent(content, filePath);
        return new { entities };
    }

    /// <summary>
    /// Parse multiple files in batch.
    /// Params: { "files": [{ "filePath": string, "content"?: string }] }
    /// </summary>
    public async Task<object?> HandleParseBatchAsync(AddonRequest request, CancellationToken ct)
    {
        var files = new List<(string path, string? content)>();

        if (request.Params?.TryGetProperty("files", out var filesEl) == true && filesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in filesEl.EnumerateArray())
            {
                var path = f.GetProperty("filePath").GetString() ?? "";
                string? content = null;
                if (f.TryGetProperty("content", out var c))
                    content = c.GetString();
                files.Add((path, content));
            }
        }

        var results = new List<object>();
        foreach (var (path, content) in files)
        {
            ct.ThrowIfCancellationRequested();
            var text = content ?? (File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null);
            if (text == null) continue;

            var entities = ParseContent(text, path);
            results.Add(new { filePath = path, entities });
        }

        return new { files = results };
    }

    /// <summary>
    /// Parse C# content into a list of entities using Roslyn syntax tree.
    /// </summary>
    private List<ParsedEntityDto> ParseContent(string content, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
        var root = tree.GetRoot();
        var entities = new List<ParsedEntityDto>();

        // Extract file-level using directives
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().TrimEnd(';').Trim())
            .ToList();

        // Get syntax diagnostics
        var diagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d =>
            {
                var lineSpan = d.Location.GetLineSpan();
                return new DiagnosticDto
                {
                    Id = d.Id,
                    Message = d.GetMessage(),
                    Severity = d.Severity.ToString().ToLowerInvariant(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                };
            })
            .ToList();

        // Determine file-level namespace
        var fileNamespace = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
            ?? root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

        // Walk top-level type declarations
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case ClassDeclarationSyntax cls:
                    entities.Add(ExtractTypeEntity(cls, filePath, "class", content, fileNamespace, usings));
                    break;
                case InterfaceDeclarationSyntax iface:
                    entities.Add(ExtractTypeEntity(iface, filePath, "interface", content, fileNamespace, usings));
                    break;
                case StructDeclarationSyntax strct:
                    entities.Add(ExtractTypeEntity(strct, filePath, "class", content, fileNamespace, usings));
                    break;
                case RecordDeclarationSyntax rec:
                    entities.Add(ExtractTypeEntity(rec, filePath, "class", content, fileNamespace, usings));
                    break;
                case EnumDeclarationSyntax enm:
                    entities.Add(ExtractEnumEntity(enm, filePath, content, fileNamespace, usings));
                    break;
                case DelegateDeclarationSyntax del:
                    entities.Add(ExtractDelegateEntity(del, filePath, content, fileNamespace));
                    break;
            }
        }

        // Attach file-level diagnostics to the first entity (or create a virtual file entity)
        if (diagnostics.Count > 0 && entities.Count > 0)
        {
            var meta = entities[0].Metadata ??= new EntityMetadataDto();
            meta.Diagnostics = diagnostics;
        }

        return entities;
    }

    private ParsedEntityDto ExtractTypeEntity(TypeDeclarationSyntax typeDecl, string filePath, string entityType, string content, string? ns, List<string>? usings)
    {
        var lineSpan = typeDecl.GetLocation().GetLineSpan();
        var typeName = typeDecl.Identifier.Text;
        var fqn = ns != null ? $"{ns}.{typeName}" : typeName;

        var entity = new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = typeName,
            Type = entityType,
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = typeDecl.ToString(),
            Metadata = new EntityMetadataDto
            {
                Namespace = ns,
                Fqn = fqn,
                Accessibility = GetAccessibility(typeDecl.Modifiers),
                IsStatic = typeDecl.Modifiers.Any(SyntaxKind.StaticKeyword),
                IsAbstract = typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword),
                Usings = usings?.Count > 0 ? usings : null,
                BaseTypes = typeDecl.BaseList?.Types.Select(t => t.ToString()).ToList(),
                TypeParameters = typeDecl.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList(),
                Attributes = typeDecl.AttributeLists.SelectMany(a => a.Attributes.Select(attr => attr.ToString())).ToList() is { Count: > 0 } attrs ? attrs : null,
                DocComment = ExtractDocComment(typeDecl),
            },
            Children = [],
        };

        // Extract members
        foreach (var member in typeDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    entity.Children.Add(ExtractMethodEntity(method, filePath, fqn, content));
                    break;
                case ConstructorDeclarationSyntax ctor:
                    entity.Children.Add(ExtractConstructorEntity(ctor, filePath, fqn, content));
                    break;
                case PropertyDeclarationSyntax prop:
                    entity.Children.Add(ExtractPropertyEntity(prop, filePath, fqn));
                    break;
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        entity.Children.Add(ExtractFieldEntity(field, variable, filePath, fqn));
                    }
                    break;
                case EventDeclarationSyntax evt:
                    entity.Children.Add(ExtractEventEntity(evt, filePath, fqn));
                    break;
                // Nested types are handled by top-level walk
            }
        }

        if (entity.Children.Count == 0)
            entity.Children = null;

        return entity;
    }

    private ParsedEntityDto ExtractMethodEntity(MethodDeclarationSyntax method, string filePath, string parentFqn, string content)
    {
        var lineSpan = method.GetLocation().GetLineSpan();
        var name = method.Identifier.Text;
        var fqn = $"{parentFqn}.{name}";

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "method",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = method.ToString(),
            ParentId = $"{filePath}:{parentFqn}",
            Metadata = new EntityMetadataDto
            {
                Fqn = fqn,
                Accessibility = GetAccessibility(method.Modifiers),
                IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
                IsAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword),
                IsAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword),
                ReturnType = method.ReturnType.ToString(),
                Parameters = method.ParameterList.Parameters.Select(p => new ParameterDto
                {
                    Name = p.Identifier.Text,
                    Type = p.Type?.ToString() ?? "object",
                    IsOptional = p.Default != null,
                    DefaultValue = p.Default?.Value.ToString(),
                }).ToList(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList(),
                Calls = ExtractCalls(method),
                Complexity = CalculateCyclomaticComplexity(method),
                Attributes = method.AttributeLists.SelectMany(a => a.Attributes.Select(attr => attr.ToString())).ToList() is { Count: > 0 } attrs ? attrs : null,
                DocComment = ExtractDocComment(method),
            },
        };
    }

    private ParsedEntityDto ExtractConstructorEntity(ConstructorDeclarationSyntax ctor, string filePath, string parentFqn, string content)
    {
        var lineSpan = ctor.GetLocation().GetLineSpan();
        var name = ctor.Identifier.Text;
        var fqn = $"{parentFqn}..ctor";

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "constructor",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = ctor.ToString(),
            ParentId = $"{filePath}:{parentFqn}",
            Metadata = new EntityMetadataDto
            {
                Fqn = fqn,
                Accessibility = GetAccessibility(ctor.Modifiers),
                IsStatic = ctor.Modifiers.Any(SyntaxKind.StaticKeyword),
                Parameters = ctor.ParameterList.Parameters.Select(p => new ParameterDto
                {
                    Name = p.Identifier.Text,
                    Type = p.Type?.ToString() ?? "object",
                    IsOptional = p.Default != null,
                    DefaultValue = p.Default?.Value.ToString(),
                }).ToList(),
                Calls = ExtractCalls(ctor),
            },
        };
    }

    private ParsedEntityDto ExtractPropertyEntity(PropertyDeclarationSyntax prop, string filePath, string parentFqn)
    {
        var lineSpan = prop.GetLocation().GetLineSpan();
        var name = prop.Identifier.Text;
        var fqn = $"{parentFqn}.{name}";

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "property",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = prop.ToString(),
            ParentId = $"{filePath}:{parentFqn}",
            Metadata = new EntityMetadataDto
            {
                Fqn = fqn,
                Accessibility = GetAccessibility(prop.Modifiers),
                IsStatic = prop.Modifiers.Any(SyntaxKind.StaticKeyword),
                PropertyType = prop.Type.ToString(),
                Attributes = prop.AttributeLists.SelectMany(a => a.Attributes.Select(attr => attr.ToString())).ToList() is { Count: > 0 } attrs ? attrs : null,
            },
        };
    }

    private ParsedEntityDto ExtractFieldEntity(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable, string filePath, string parentFqn)
    {
        var lineSpan = variable.GetLocation().GetLineSpan();
        var name = variable.Identifier.Text;
        var fqn = $"{parentFqn}.{name}";

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "field",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = field.ToString(),
            ParentId = $"{filePath}:{parentFqn}",
            Metadata = new EntityMetadataDto
            {
                Fqn = fqn,
                Accessibility = GetAccessibility(field.Modifiers),
                IsStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword),
                FieldType = field.Declaration.Type.ToString(),
            },
        };
    }

    private ParsedEntityDto ExtractEventEntity(EventDeclarationSyntax evt, string filePath, string parentFqn)
    {
        var lineSpan = evt.GetLocation().GetLineSpan();
        var name = evt.Identifier.Text;
        var fqn = $"{parentFqn}.{name}";

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "event",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = evt.ToString(),
            ParentId = $"{filePath}:{parentFqn}",
            Metadata = new EntityMetadataDto
            {
                Fqn = fqn,
                Accessibility = GetAccessibility(evt.Modifiers),
                FieldType = evt.Type.ToString(),
            },
        };
    }

    private ParsedEntityDto ExtractEnumEntity(EnumDeclarationSyntax enm, string filePath, string content, string? ns, List<string>? usings)
    {
        var lineSpan = enm.GetLocation().GetLineSpan();
        var name = enm.Identifier.Text;
        var fqn = ns != null ? $"{ns}.{name}" : name;

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "enum",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = enm.ToString(),
            Metadata = new EntityMetadataDto
            {
                Namespace = ns,
                Fqn = fqn,
                Accessibility = GetAccessibility(enm.Modifiers),
                Usings = usings?.Count > 0 ? usings : null,
                Attributes = enm.AttributeLists.SelectMany(a => a.Attributes.Select(attr => attr.ToString())).ToList() is { Count: > 0 } attrs ? attrs : null,
            },
        };
    }

    private ParsedEntityDto ExtractDelegateEntity(DelegateDeclarationSyntax del, string filePath, string content, string? ns)
    {
        var lineSpan = del.GetLocation().GetLineSpan();
        var name = del.Identifier.Text;
        var fqn = ns != null ? $"{ns}.{name}" : name;

        return new ParsedEntityDto
        {
            Id = $"{filePath}:{fqn}",
            Name = name,
            Type = "delegate",
            FilePath = filePath,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            Content = del.ToString(),
            Metadata = new EntityMetadataDto
            {
                Namespace = ns,
                Fqn = fqn,
                Accessibility = GetAccessibility(del.Modifiers),
                ReturnType = del.ReturnType.ToString(),
                Parameters = del.ParameterList.Parameters.Select(p => new ParameterDto
                {
                    Name = p.Identifier.Text,
                    Type = p.Type?.ToString() ?? "object",
                }).ToList(),
            },
        };
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword)) return "protected internal";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return "protected";
        if (modifiers.Any(SyntaxKind.InternalKeyword)) return "internal";
        if (modifiers.Any(SyntaxKind.PrivateKeyword)) return "private";
        return "private"; // default for class members
    }

    private static List<CallInfoDto>? ExtractCalls(SyntaxNode node)
    {
        var invocations = node.DescendantNodes().OfType<InvocationExpressionSyntax>();
        var calls = new List<CallInfoDto>();

        foreach (var inv in invocations)
        {
            var lineSpan = inv.GetLocation().GetLineSpan();

            switch (inv.Expression)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    calls.Add(new CallInfoDto
                    {
                        Name = memberAccess.Name.Identifier.Text,
                        Receiver = memberAccess.Expression.ToString(),
                        Line = lineSpan.StartLinePosition.Line + 1,
                    });
                    break;
                case IdentifierNameSyntax identifier:
                    calls.Add(new CallInfoDto
                    {
                        Name = identifier.Identifier.Text,
                        Line = lineSpan.StartLinePosition.Line + 1,
                    });
                    break;
                default:
                    calls.Add(new CallInfoDto
                    {
                        Name = inv.Expression.ToString(),
                        Line = lineSpan.StartLinePosition.Line + 1,
                    });
                    break;
            }
        }

        return calls.Count > 0 ? calls : null;
    }

    private static int CalculateCyclomaticComplexity(SyntaxNode node)
    {
        int complexity = 1; // base

        foreach (var descendant in node.DescendantNodes())
        {
            switch (descendant)
            {
                case IfStatementSyntax:
                case ConditionalExpressionSyntax:
                case CaseSwitchLabelSyntax:
                case CasePatternSwitchLabelSyntax:
                case WhileStatementSyntax:
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case DoStatementSyntax:
                case CatchClauseSyntax:
                case ConditionalAccessExpressionSyntax:
                    complexity++;
                    break;
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression):
                    complexity++;
                    break;
            }
        }

        return complexity;
    }

    private static string? ExtractDocComment(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (trivia == default) return null;

        var xml = trivia.ToString();
        // Extract summary content
        var summaryStart = xml.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
        var summaryEnd = xml.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var inner = xml[(summaryStart + 9)..summaryEnd].Trim();
            // Clean up XML comment prefixes
            inner = string.Join(" ", inner.Split('\n').Select(l => l.Trim().TrimStart('/', ' ')));
            return string.IsNullOrWhiteSpace(inner) ? null : inner;
        }

        return null;
    }
}

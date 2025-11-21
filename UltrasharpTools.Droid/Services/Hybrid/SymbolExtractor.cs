using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Утилита для извлечения символов из C# кода без полной Roslyn workspace
/// </summary>
public static class SymbolExtractor
{
    /// <summary>
    /// Извлекает символы из C# кода (классы, методы, свойства и т.д.)
    /// </summary>
    public static Models.Hybrid.SymbolInfo[] ExtractSymbols(string code, string filePath)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Array.Empty<Models.Hybrid.SymbolInfo>();
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
            var root = tree.GetRoot();

            var symbols = new List<Models.Hybrid.SymbolInfo>();

            // Extract namespaces
            foreach (var ns in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = ns.Name.ToString(),
                        Kind = "namespace",
                        Line = tree.GetLineSpan(ns.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract classes
            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullTypeName(cls),
                        Kind = "class",
                        Line = tree.GetLineSpan(cls.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract interfaces
            foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullTypeName(iface),
                        Kind = "interface",
                        Line = tree.GetLineSpan(iface.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract structs
            foreach (var str in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullTypeName(str),
                        Kind = "struct",
                        Line = tree.GetLineSpan(str.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract enums
            foreach (var enm in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullTypeName(enm),
                        Kind = "enum",
                        Line = tree.GetLineSpan(enm.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract methods
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullMemberName(method),
                        Kind = "method",
                        Line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract properties
            foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                symbols.Add(
                    new Models.Hybrid.SymbolInfo
                    {
                        Name = GetFullMemberName(prop),
                        Kind = "property",
                        Line = tree.GetLineSpan(prop.Span).StartLinePosition.Line + 1,
                    }
                );
            }

            // Extract fields
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    symbols.Add(
                        new Models.Hybrid.SymbolInfo
                        {
                            Name = GetFullMemberName(field, variable.Identifier.Text),
                            Kind = "field",
                            Line = tree.GetLineSpan(variable.Span).StartLinePosition.Line + 1,
                        }
                    );
                }
            }

            return symbols.ToArray();
        }
        catch
        {
            // Parsing failed - return empty array
            return Array.Empty<Models.Hybrid.SymbolInfo>();
        }
    }

    private static string GetFullTypeName(BaseTypeDeclarationSyntax typeDecl)
    {
        var parts = new List<string> { typeDecl.Identifier.Text };

        // Walk up to find containing namespace
        var parent = typeDecl.Parent;
        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax ns)
            {
                parts.Insert(0, ns.Name.ToString());
            }
            else if (parent is BaseTypeDeclarationSyntax parentType)
            {
                parts.Insert(0, parentType.Identifier.Text);
            }
            parent = parent.Parent;
        }

        return string.Join(".", parts);
    }

    private static string GetFullMemberName(MemberDeclarationSyntax memberDecl)
    {
        var memberName = memberDecl switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax prop => prop.Identifier.Text,
            _ => "Unknown",
        };

        return GetFullMemberName(memberDecl, memberName);
    }

    private static string GetFullMemberName(MemberDeclarationSyntax memberDecl, string memberName)
    {
        var parts = new List<string> { memberName };

        // Walk up to find containing type and namespace
        var parent = memberDecl.Parent;
        while (parent != null)
        {
            if (parent is BaseTypeDeclarationSyntax typeDecl)
            {
                parts.Insert(0, typeDecl.Identifier.Text);
            }
            else if (parent is BaseNamespaceDeclarationSyntax ns)
            {
                parts.Insert(0, ns.Name.ToString());
            }
            parent = parent.Parent;
        }

        return string.Join(".", parts);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер C# кода для извлечения CodeUnits.
/// Использует Roslyn для AST parsing.
/// </summary>
public sealed class CSharpParser
{
    private readonly ILogger<CSharpParser> _logger;
    private readonly StructuralFingerprint _fingerprint;
    private readonly ContentNormalizer _normalizer;

    public CSharpParser(
        StructuralFingerprint fingerprint,
        ContentNormalizer normalizer,
        ILogger<CSharpParser>? logger = null
    )
    {
        _fingerprint = fingerprint;
        _normalizer = normalizer;
        _logger = logger ?? NullLogger<CSharpParser>.Instance;
    }

    /// <summary>
    /// Парсить C# файл и извлечь все CodeUnits.
    /// </summary>
    public async Task<List<CodeUnit>> ParseFileAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        // 1. Нормализовать контент
        var normalized = await _normalizer.NormalizeAsync(filePath, ct);
        var content = normalized.Content;

        // 2. Парсить SyntaxTree
        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: ct);
        var root = await tree.GetRootAsync(ct);

        // 3. Извлечь все units
        var units = new List<CodeUnit>();

        // File-level unit
        var fileUnit = CreateFileUnit(filePath, content);
        units.Add(fileUnit);

        // Namespaces
        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>();
        foreach (var ns in namespaces)
        {
            var nsUnit = CreateNamespaceUnit(ns, filePath, fileUnit.Id);
            units.Add(nsUnit);

            // Types в namespace - только прямые потомки (не из вложенных namespaces)
            var types = ns.DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var type in types)
            {
                // Пропустить nested types - они будут обработаны рекурсивно
                if (type.Parent is TypeDeclarationSyntax)
                    continue;

                // Пропустить типы из вложенных namespaces - они будут обработаны отдельно
                if (
                    type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() != ns
                )
                    continue;

                ExtractTypeUnits(type, filePath, nsUnit.Id, units);
            }
        }

        // Types без namespace (global namespace)
        var globalTypes = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(t => t.Parent is CompilationUnitSyntax);

        foreach (var type in globalTypes)
        {
            ExtractTypeUnits(type, filePath, fileUnit.Id, units);
        }

        _logger.LogInformation("Parsed {FilePath}: extracted {Count} units", filePath, units.Count);

        return units;
    }

    /// <summary>
    /// Создать File-level CodeUnit.
    /// </summary>
    private CodeUnit CreateFileUnit(string filePath, string content)
    {
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        return new CodeUnit
        {
            Id = $"file:{filePath}",
            Type = CodeUnitType.File,
            FilePath = filePath,
            Name = Path.GetFileName(filePath),
            FullyQualifiedName = filePath,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = null,
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = null,
            ChildIds = new HashSet<string>(),
            StartLine = 1,
            EndLine = content.Split('\n').Length,
            Metadata = new Dictionary<string, object>
            {
                ["FileSize"] = content.Length,
                ["Extension"] = Path.GetExtension(filePath),
            },
        };
    }

    /// <summary>
    /// Создать Namespace CodeUnit.
    /// </summary>
    private CodeUnit CreateNamespaceUnit(
        BaseNamespaceDeclarationSyntax ns,
        string filePath,
        string parentId
    )
    {
        var name = ns.Name.ToString();
        var content = ns.ToFullString();
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        var span = ns.GetLocation().GetLineSpan();

        return new CodeUnit
        {
            Id = $"namespace:{name}@{filePath}",
            Type = CodeUnitType.Namespace,
            FilePath = filePath,
            Name = name,
            FullyQualifiedName = name,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = name,
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Metadata = new Dictionary<string, object>(),
        };
    }

    /// <summary>
    /// Рекурсивно извлечь Type и его члены.
    /// </summary>
    private void ExtractTypeUnits(
        TypeDeclarationSyntax type,
        string filePath,
        string parentId,
        List<CodeUnit> units
    )
    {
        var typeUnit = CreateTypeUnit(type, filePath, parentId);
        units.Add(typeUnit);

        // Members
        foreach (var member in type.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    var methodUnit = CreateMethodUnit(method, filePath, typeUnit.Id);
                    units.Add(methodUnit);
                    break;

                case PropertyDeclarationSyntax property:
                    var propUnit = CreatePropertyUnit(property, filePath, typeUnit.Id);
                    units.Add(propUnit);
                    break;

                case FieldDeclarationSyntax field:
                    var fieldUnits = CreateFieldUnits(field, filePath, typeUnit.Id);
                    units.AddRange(fieldUnits);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    var ctorUnit = CreateConstructorUnit(ctor, filePath, typeUnit.Id);
                    units.Add(ctorUnit);
                    break;

                case TypeDeclarationSyntax nestedType:
                    ExtractTypeUnits(nestedType, filePath, typeUnit.Id, units);
                    break;

                // Другие члены (events, indexers, operators) можно добавить позже
            }
        }
    }

    /// <summary>
    /// Создать Type CodeUnit.
    /// </summary>
    private CodeUnit CreateTypeUnit(TypeDeclarationSyntax type, string filePath, string parentId)
    {
        var typeName = type.Identifier.Text;
        var fqn = BuildFqn(type);
        var content = type.ToFullString();
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        // Используем Identifier.GetLocation() чтобы получить позицию имени класса, а не атрибутов
        var identifierSpan = type.Identifier.GetLocation().GetLineSpan();
        var fullSpan = type.GetLocation().GetLineSpan();

        var typeKind = type switch
        {
            ClassDeclarationSyntax => "class",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax => "record",
            _ => "type",
        };

        // Для ID используем строку с именем класса, для StartLine/EndLine - полный span
        var startLine = identifierSpan.StartLinePosition.Line + 1;

        return new CodeUnit
        {
            // Добавляем line number к ID чтобы различать типы в одном файле (multiple classes/nested namespaces)
            Id = $"type:{fqn}@{filePath}:{startLine}",
            Type = CodeUnitType.Type,
            FilePath = filePath,
            Name = typeName,
            FullyQualifiedName = fqn,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = $"{typeKind} {fqn}",
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = fullSpan.StartLinePosition.Line + 1,
            EndLine = fullSpan.EndLinePosition.Line + 1,
            Metadata = new Dictionary<string, object>
            {
                ["TypeKind"] = typeKind,
                ["IsAbstract"] = type.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                ["IsSealed"] = type.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)),
                ["IsStatic"] = type.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            },
        };
    }

    /// <summary>
    /// Создать Method CodeUnit.
    /// </summary>
    private CodeUnit CreateMethodUnit(
        MethodDeclarationSyntax method,
        string filePath,
        string parentId
    )
    {
        var methodName = method.Identifier.Text;
        var fqn = BuildFqn(method);
        var content = method.ToFullString();
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        var signature = BuildMethodSignature(method, fqn);
        var span = method.GetLocation().GetLineSpan();

        // Добавляем type parameters в ID для generic методов (например Method``1)
        var typeParamSuffix = "";
        if (method.TypeParameterList != null && method.TypeParameterList.Parameters.Count > 0)
        {
            typeParamSuffix = $"``{method.TypeParameterList.Parameters.Count}";
        }

        // Добавляем параметры в ID чтобы различать перегрузки (включая ref/out/in модификаторы)
        var paramTypes = string.Join(
            ",",
            method.ParameterList.Parameters.Select(p =>
            {
                // Включаем модификаторы ref/out/in
                var modifier = "";
                foreach (var mod in p.Modifiers)
                {
                    if (mod.IsKind(SyntaxKind.RefKeyword))
                        modifier = "ref ";
                    else if (mod.IsKind(SyntaxKind.OutKeyword))
                        modifier = "out ";
                    else if (mod.IsKind(SyntaxKind.InKeyword))
                        modifier = "in ";
                    else if (mod.IsKind(SyntaxKind.ParamsKeyword))
                        modifier = "params ";
                }
                return modifier + p.Type!.ToString();
            })
        );

        return new CodeUnit
        {
            Id = $"method:{fqn}{typeParamSuffix}({paramTypes})@{filePath}",
            Type = CodeUnitType.Method,
            FilePath = filePath,
            Name = methodName,
            FullyQualifiedName = fqn,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = signature,
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Metadata = new Dictionary<string, object>
            {
                ["ReturnType"] = method.ReturnType.ToString(),
                ["ParameterCount"] = method.ParameterList.Parameters.Count,
                ["IsAsync"] = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                ["IsStatic"] = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            },
        };
    }

    /// <summary>
    /// Создать Property CodeUnit.
    /// </summary>
    private CodeUnit CreatePropertyUnit(
        PropertyDeclarationSyntax property,
        string filePath,
        string parentId
    )
    {
        var propName = property.Identifier.Text;
        var fqn = BuildFqn(property);
        var content = property.ToFullString();
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        var span = property.GetLocation().GetLineSpan();

        return new CodeUnit
        {
            Id = $"property:{fqn}@{filePath}",
            Type = CodeUnitType.Property,
            FilePath = filePath,
            Name = propName,
            FullyQualifiedName = fqn,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = $"{property.Type} {fqn}",
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Metadata = new Dictionary<string, object>
            {
                ["PropertyType"] = property.Type.ToString(),
                ["HasGetter"] =
                    property.AccessorList?.Accessors.Any(a =>
                        a.IsKind(SyntaxKind.GetAccessorDeclaration)
                    ) ?? false,
                ["HasSetter"] =
                    property.AccessorList?.Accessors.Any(a =>
                        a.IsKind(SyntaxKind.SetAccessorDeclaration)
                    ) ?? false,
            },
        };
    }

    /// <summary>
    /// Создать Field CodeUnits (может быть несколько в одном declaration).
    /// </summary>
    private List<CodeUnit> CreateFieldUnits(
        FieldDeclarationSyntax field,
        string filePath,
        string parentId
    )
    {
        var units = new List<CodeUnit>();
        var span = field.GetLocation().GetLineSpan();

        foreach (var variable in field.Declaration.Variables)
        {
            var fieldName = variable.Identifier.Text;
            var fqn = BuildFieldFqn(field, variable);
            var content = field.ToFullString();
            var contentHash = ContentNormalizer.ComputeContentHash(content);
            var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

            var unit = new CodeUnit
            {
                Id = $"field:{fqn}@{filePath}",
                Type = CodeUnitType.Field,
                FilePath = filePath,
                Name = fieldName,
                FullyQualifiedName = fqn,
                Content = content,
                ContentHash = contentHash,
                StructuralHash = structuralHash,
                Signature = $"{field.Declaration.Type} {fqn}",
                Embedding = null,
                Structure = null,
                CFG = null,
                ParentId = parentId,
                ChildIds = new HashSet<string>(),
                StartLine = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                Metadata = new Dictionary<string, object>
                {
                    ["FieldType"] = field.Declaration.Type.ToString(),
                    ["IsStatic"] = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                    ["IsReadonly"] = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
                    ["IsConst"] = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)),
                },
            };

            units.Add(unit);
        }

        return units;
    }

    /// <summary>
    /// Создать Constructor CodeUnit.
    /// </summary>
    private CodeUnit CreateConstructorUnit(
        ConstructorDeclarationSyntax ctor,
        string filePath,
        string parentId
    )
    {
        var ctorName = ctor.Identifier.Text;
        var fqn = BuildFqn(ctor);
        var content = ctor.ToFullString();
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        var signature = BuildConstructorSignature(ctor, fqn);
        var span = ctor.GetLocation().GetLineSpan();

        // Добавляем параметры в ID чтобы различать перегрузки (включая ref/out/in модификаторы)
        var paramTypes = string.Join(
            ",",
            ctor.ParameterList.Parameters.Select(p =>
            {
                // Включаем модификаторы ref/out/in
                var modifier = "";
                foreach (var mod in p.Modifiers)
                {
                    if (mod.IsKind(SyntaxKind.RefKeyword))
                        modifier = "ref ";
                    else if (mod.IsKind(SyntaxKind.OutKeyword))
                        modifier = "out ";
                    else if (mod.IsKind(SyntaxKind.InKeyword))
                        modifier = "in ";
                    else if (mod.IsKind(SyntaxKind.ParamsKeyword))
                        modifier = "params ";
                }
                return modifier + p.Type!.ToString();
            })
        );

        return new CodeUnit
        {
            Id = $"constructor:{fqn}({paramTypes})@{filePath}",
            Type = CodeUnitType.Method, // Constructor рассматривается как метод
            FilePath = filePath,
            Name = ctorName,
            FullyQualifiedName = fqn,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = signature,
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = ctor.ParameterList.Parameters.Count,
                ["IsStatic"] = ctor.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            },
        };
    }

    /// <summary>
    /// Построить FQN для символа.
    /// </summary>
    private string BuildFqn(SyntaxNode node)
    {
        var parts = new Stack<string>();

        // Traverse вверх по дереву и складываем в Stack (обратный порядок)
        var current = node;
        while (current != null)
        {
            switch (current)
            {
                case NamespaceDeclarationSyntax ns:
                    parts.Push(ns.Name.ToString());
                    break;

                case FileScopedNamespaceDeclarationSyntax fsns:
                    parts.Push(fsns.Name.ToString());
                    break;

                case TypeDeclarationSyntax type:
                    // Для generic типов добавляем arity suffix (например MyType`1)
                    var typeName = type.Identifier.Text;
                    if (
                        type.TypeParameterList != null
                        && type.TypeParameterList.Parameters.Count > 0
                    )
                    {
                        typeName = $"{typeName}`{type.TypeParameterList.Parameters.Count}";
                    }
                    parts.Push(typeName);
                    break;

                case MethodDeclarationSyntax method:
                    // Для explicit interface implementation нужно взять полное имя
                    var methodName =
                        method.ExplicitInterfaceSpecifier != null
                            ? $"{method.ExplicitInterfaceSpecifier.Name}.{method.Identifier.Text}"
                            : method.Identifier.Text;
                    parts.Push(methodName);
                    break;

                case PropertyDeclarationSyntax property:
                    // Для explicit interface implementation нужно взять полное имя
                    var propName =
                        property.ExplicitInterfaceSpecifier != null
                            ? $"{property.ExplicitInterfaceSpecifier.Name}.{property.Identifier.Text}"
                            : property.Identifier.Text;
                    parts.Push(propName);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    parts.Push(ctor.Identifier.Text);
                    break;
            }

            current = current.Parent;
        }

        // Stack уже в правильном порядке благодаря LIFO (namespace.type.member)
        return string.Join(".", parts);
    }

    /// <summary>
    /// Построить FQN для field.
    /// </summary>
    private string BuildFieldFqn(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable)
    {
        var parts = new Stack<string>();
        parts.Push(variable.Identifier.Text);

        var current = field.Parent;
        while (current != null)
        {
            switch (current)
            {
                case NamespaceDeclarationSyntax ns:
                    parts.Push(ns.Name.ToString());
                    break;

                case FileScopedNamespaceDeclarationSyntax fsns:
                    parts.Push(fsns.Name.ToString());
                    break;

                case TypeDeclarationSyntax type:
                    // Для generic типов добавляем arity suffix (например MyType`1)
                    var typeName = type.Identifier.Text;
                    if (
                        type.TypeParameterList != null
                        && type.TypeParameterList.Parameters.Count > 0
                    )
                    {
                        typeName = $"{typeName}`{type.TypeParameterList.Parameters.Count}";
                    }
                    parts.Push(typeName);
                    break;
            }

            current = current.Parent;
        }

        // Stack уже в правильном порядке благодаря LIFO
        return string.Join(".", parts);
    }

    /// <summary>
    /// Построить сигнатуру метода.
    /// </summary>
    private string BuildMethodSignature(MethodDeclarationSyntax method, string fqn)
    {
        var parameters = string.Join(
            ", ",
            method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}")
        );

        return $"{method.ReturnType} {fqn}({parameters})";
    }

    /// <summary>
    /// Построить сигнатуру конструктора.
    /// </summary>
    private string BuildConstructorSignature(ConstructorDeclarationSyntax ctor, string fqn)
    {
        var parameters = string.Join(
            ", ",
            ctor.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}")
        );

        return $"{fqn}({parameters})";
    }
}

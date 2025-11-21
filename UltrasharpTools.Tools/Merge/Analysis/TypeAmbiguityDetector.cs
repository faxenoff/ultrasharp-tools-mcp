using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Analysis;

/// <summary>
/// Обнаружение неоднозначности типов (когда имя типа совпадает с системным или глобальным).
/// Например: Thread может быть System.Threading.Thread или кастомным классом.
/// </summary>
public sealed class TypeAmbiguityDetector
{
    private readonly ILogger<TypeAmbiguityDetector> _logger;

    // Системные типы, которые часто конфликтуют с пользовательскими
    private static readonly HashSet<string> CommonSystemTypes = new()
    {
        // System.Threading
        "Thread",
        "Task",
        "Timer",
        "Mutex",
        "Semaphore",
        // System.IO
        "File",
        "Directory",
        "Path",
        "Stream",
        // System.Collections
        "List",
        "Dictionary",
        "Queue",
        "Stack",
        "Set",
        // System
        "Object",
        "String",
        "Type",
        "Exception",
        "Attribute",
        "Action",
        "Func",
        "Delegate",
        "Event",
        // System.Net
        "HttpClient",
        "WebClient",
        "Socket",
        "Request",
        "Response",
        // Common patterns
        "Context",
        "Builder",
        "Factory",
        "Manager",
        "Service",
        "Repository",
        "Controller",
        "Model",
        "View",
    };

    public TypeAmbiguityDetector(ILogger<TypeAmbiguityDetector>? logger = null)
    {
        _logger = logger ?? NullLogger<TypeAmbiguityDetector>.Instance;
    }

    /// <summary>
    /// Проверить код на неоднозначные типы.
    /// </summary>
    public List<TypeAmbiguityWarning> DetectAmbiguities(CodeUnit unit)
    {
        var warnings = new List<TypeAmbiguityWarning>();

        try
        {
            // Парсим только C# код
            if (
                unit.Type != CodeUnitType.Method
                && unit.Type != CodeUnitType.Type
                && unit.Type != CodeUnitType.Property
                && unit.Type != CodeUnitType.Field
            )
            {
                return warnings;
            }

            var tree = CSharpSyntaxTree.ParseText(unit.Content);
            var root = tree.GetRoot();

            // Ищем все используемые типы
            var typeReferences = FindTypeReferences(root);

            foreach (var typeRef in typeReferences)
            {
                var typeName = typeRef.TypeName;

                // Проверяем на совпадение с системными типами
                if (CommonSystemTypes.Contains(typeName))
                {
                    var isFullyQualified = typeRef.IsFullyQualified;

                    if (!isFullyQualified)
                    {
                        warnings.Add(
                            new TypeAmbiguityWarning
                            {
                                TypeName = typeName,
                                Location = $"{unit.FilePath}:{typeRef.Line}",
                                Severity = AmbiguitySeverity.Medium,
                                Message =
                                    $"Тип '{typeName}' может быть неоднозначным. "
                                    + $"Рекомендуется использовать полное имя (например, System.Threading.{typeName}) "
                                    + $"или добавить using для явного указания.",
                                Suggestion = $"Используйте полное имя или добавьте using directive",
                            }
                        );

                        _logger.LogWarning(
                            "Ambiguous type '{TypeName}' found at {Location}",
                            typeName,
                            typeRef.Line
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect ambiguities in {UnitId}", unit.Id);
        }

        return warnings;
    }

    /// <summary>
    /// Batch проверка для множества units.
    /// </summary>
    public Dictionary<string, List<TypeAmbiguityWarning>> DetectAmbiguitiesBatch(
        List<CodeUnit> units
    )
    {
        var result = new Dictionary<string, List<TypeAmbiguityWarning>>();

        foreach (var unit in units)
        {
            var warnings = DetectAmbiguities(unit);
            if (warnings.Count > 0)
            {
                result[unit.Id] = warnings;
            }
        }

        _logger.LogInformation(
            "Type ambiguity detection: {UnitsWithWarnings}/{TotalUnits} units have warnings",
            result.Count,
            units.Count
        );

        return result;
    }

    /// <summary>
    /// Найти все ссылки на типы в синтаксическом дереве.
    /// </summary>
    private List<TypeReference> FindTypeReferences(SyntaxNode root)
    {
        var references = new List<TypeReference>();

        // Ищем все IdentifierNameSyntax (простые имена типов)
        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            // Проверяем, используется ли как тип
            if (IsTypeContext(identifier))
            {
                var typeName = identifier.Identifier.ValueText;
                var isFullyQualified = IsFullyQualifiedName(identifier);
                var lineSpan = identifier.GetLocation().GetLineSpan();

                references.Add(
                    new TypeReference
                    {
                        TypeName = typeName,
                        IsFullyQualified = isFullyQualified,
                        Line = lineSpan.StartLinePosition.Line + 1,
                    }
                );
            }
        }

        // Также проверяем GenericNameSyntax (List<T>, Dictionary<K,V>)
        var genericNames = root.DescendantNodes().OfType<GenericNameSyntax>();

        foreach (var genericName in genericNames)
        {
            if (IsTypeContext(genericName))
            {
                var typeName = genericName.Identifier.ValueText;
                var isFullyQualified = IsFullyQualifiedName(genericName);
                var lineSpan = genericName.GetLocation().GetLineSpan();

                references.Add(
                    new TypeReference
                    {
                        TypeName = typeName,
                        IsFullyQualified = isFullyQualified,
                        Line = lineSpan.StartLinePosition.Line + 1,
                    }
                );
            }
        }

        return references;
    }

    /// <summary>
    /// Проверить, используется ли identifier как тип.
    /// </summary>
    private bool IsTypeContext(SyntaxNode node)
    {
        var parent = node.Parent;

        return parent
            is VariableDeclarationSyntax
                or ParameterSyntax
                or ObjectCreationExpressionSyntax
                or CastExpressionSyntax
                or TypeOfExpressionSyntax
                or BaseTypeSyntax
                or TypeConstraintSyntax
                or MethodDeclarationSyntax; // return type
    }

    /// <summary>
    /// Проверить, является ли имя полным (например, System.Threading.Thread).
    /// </summary>
    private bool IsFullyQualifiedName(SimpleNameSyntax name)
    {
        // Если parent - QualifiedNameSyntax, то это часть полного имени
        return name.Parent is QualifiedNameSyntax qualifiedName && qualifiedName.Right == name;
    }

    /// <summary>
    /// Создать отчёт о неоднозначностях для всех units.
    /// </summary>
    public string GenerateAmbiguityReport(
        Dictionary<string, List<TypeAmbiguityWarning>> ambiguities
    )
    {
        if (ambiguities.Count == 0)
        {
            return "No type ambiguities detected.";
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Type Ambiguity Report ===");
        report.AppendLine();
        report.AppendLine($"Total units with ambiguities: {ambiguities.Count}");
        report.AppendLine($"Total warnings: {ambiguities.Values.Sum(w => w.Count)}");
        report.AppendLine();

        // Группировать по типам
        var byType = ambiguities
            .Values.SelectMany(w => w)
            .GroupBy(w => w.TypeName)
            .OrderByDescending(g => g.Count());

        report.AppendLine("Most common ambiguous types:");
        foreach (var group in byType.Take(10))
        {
            report.AppendLine($"  {group.Key}: {group.Count()} occurrences");
        }

        report.AppendLine();
        report.AppendLine("Details:");

        foreach (var kvp in ambiguities.Take(20))
        {
            report.AppendLine($"\nUnit: {kvp.Key}");
            foreach (var warning in kvp.Value)
            {
                report.AppendLine($"  [{warning.Severity}] {warning.Message}");
                report.AppendLine($"  Location: {warning.Location}");
            }
        }

        if (ambiguities.Count > 20)
        {
            report.AppendLine($"\n... and {ambiguities.Count - 20} more units with warnings");
        }

        return report.ToString();
    }
}

/// <summary>
/// Ссылка на тип в коде.
/// </summary>
internal record TypeReference
{
    public required string TypeName { get; init; }
    public required bool IsFullyQualified { get; init; }
    public required int Line { get; init; }
}

/// <summary>
/// Предупреждение о неоднозначности типа.
/// </summary>
public record TypeAmbiguityWarning
{
    public required string TypeName { get; init; }
    public required string Location { get; init; }
    public required AmbiguitySeverity Severity { get; init; }
    public required string Message { get; init; }
    public required string Suggestion { get; init; }
}

/// <summary>
/// Уровень серьёзности неоднозначности.
/// </summary>
public enum AmbiguitySeverity
{
    Low, // Маловероятный конфликт
    Medium, // Возможный конфликт
    High, // Вероятный конфликт
}

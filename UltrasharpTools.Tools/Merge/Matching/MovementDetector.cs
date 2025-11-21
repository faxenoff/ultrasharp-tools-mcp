using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Обнаружение перемещений кода между версиями.
/// Определяет когда код переместился в другой файл, класс, namespace.
/// </summary>
public sealed class MovementDetector
{
    private readonly ILogger<MovementDetector> _logger;

    public MovementDetector(ILogger<MovementDetector>? logger = null)
    {
        _logger = logger ?? NullLogger<MovementDetector>.Instance;
    }

    /// <summary>
    /// Обнаружить перемещения между двумя версиями.
    /// </summary>
    public List<CodeMovement> DetectMovements(
        VersionedIndex baseVersion,
        VersionedIndex targetVersion,
        Dictionary<string, FastPathMatchResult> fastMatches,
        Dictionary<string, SemanticMatchResult> semanticMatches
    )
    {
        _logger.LogInformation(
            "Detecting code movements between {Base} and {Target}",
            baseVersion.Version,
            targetVersion.Version
        );

        var movements = new List<CodeMovement>();

        // Проверить Fast Path matches
        foreach (var match in fastMatches.Values)
        {
            var movement = DetectMovement(match.UnitA, match.UnitB);
            if (movement != null)
            {
                movements.Add(movement);
            }
        }

        // Проверить Semantic matches
        foreach (var match in semanticMatches.Values)
        {
            var movement = DetectMovement(match.SourceUnit, match.TargetUnit);
            if (movement != null)
            {
                movements.Add(movement);
            }
        }

        _logger.LogInformation("Detected {Count} code movements", movements.Count);

        return movements;
    }

    /// <summary>
    /// Обнаружить перемещение для пары units.
    /// </summary>
    private CodeMovement? DetectMovement(CodeUnit sourceUnit, CodeUnit targetUnit)
    {
        // Если FilePath изменился - это movement
        if (sourceUnit.FilePath != targetUnit.FilePath)
        {
            return new CodeMovement
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                MovementType = CodeMovementType.FileChange,
                SourceLocation = sourceUnit.FilePath,
                TargetLocation = targetUnit.FilePath,
                Description = $"Moved from {sourceUnit.FilePath} to {targetUnit.FilePath}",
            };
        }

        // Если ParentId изменился - это movement внутри файла
        if (sourceUnit.ParentId != targetUnit.ParentId)
        {
            var movementType = ClassifyIntraFileMovement(sourceUnit, targetUnit);

            return new CodeMovement
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                MovementType = movementType,
                SourceLocation = sourceUnit.ParentId ?? "root",
                TargetLocation = targetUnit.ParentId ?? "root",
                Description = $"Moved from {sourceUnit.ParentId} to {targetUnit.ParentId}",
            };
        }

        // Если FQN изменился (но родитель тот же) - это переименование
        if (sourceUnit.FullyQualifiedName != targetUnit.FullyQualifiedName)
        {
            return new CodeMovement
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                MovementType = CodeMovementType.Rename,
                SourceLocation = sourceUnit.FullyQualifiedName,
                TargetLocation = targetUnit.FullyQualifiedName,
                Description =
                    $"Renamed from {sourceUnit.FullyQualifiedName} to {targetUnit.FullyQualifiedName}",
            };
        }

        // Нет движения
        return null;
    }

    /// <summary>
    /// Классифицировать тип движения внутри файла.
    /// </summary>
    private CodeMovementType ClassifyIntraFileMovement(CodeUnit sourceUnit, CodeUnit targetUnit)
    {
        // Type изменил namespace
        if (
            sourceUnit.Type == CodeUnitType.Type
            && IsNamespaceChange(sourceUnit.ParentId, targetUnit.ParentId)
        )
        {
            return CodeMovementType.NamespaceChange;
        }

        // Method/Property/Field изменил parent class
        if (
            (
                sourceUnit.Type == CodeUnitType.Method
                || sourceUnit.Type == CodeUnitType.Property
                || sourceUnit.Type == CodeUnitType.Field
            ) && IsTypeChange(sourceUnit.ParentId, targetUnit.ParentId)
        )
        {
            return CodeMovementType.ClassChange;
        }

        // Generic movement
        return CodeMovementType.ParentChange;
    }

    /// <summary>
    /// Проверить, является ли изменение namespace.
    /// </summary>
    private bool IsNamespaceChange(string? sourceParentId, string? targetParentId)
    {
        return sourceParentId?.StartsWith("namespace:") == true
            && targetParentId?.StartsWith("namespace:") == true;
    }

    /// <summary>
    /// Проверить, является ли изменение type.
    /// </summary>
    private bool IsTypeChange(string? sourceParentId, string? targetParentId)
    {
        return sourceParentId?.StartsWith("type:") == true
            && targetParentId?.StartsWith("type:") == true;
    }

    /// <summary>
    /// Найти все units, которые были перемещены ИЗ указанного родителя.
    /// </summary>
    public List<CodeMovement> FindMovementsFrom(string parentId, List<CodeMovement> allMovements)
    {
        return allMovements.Where(m => m.SourceLocation == parentId).ToList();
    }

    /// <summary>
    /// Найти все units, которые были перемещены В указанного родителя.
    /// </summary>
    public List<CodeMovement> FindMovementsTo(string parentId, List<CodeMovement> allMovements)
    {
        return allMovements.Where(m => m.TargetLocation == parentId).ToList();
    }

    /// <summary>
    /// Группировать movements по типу.
    /// </summary>
    public Dictionary<CodeMovementType, List<CodeMovement>> GroupByType(
        List<CodeMovement> movements
    )
    {
        return movements.GroupBy(m => m.MovementType).ToDictionary(g => g.Key, g => g.ToList());
    }
}

/// <summary>
/// Перемещение кода.
/// </summary>
public sealed record CodeMovement
{
    public required CodeUnit SourceUnit { get; init; }
    public required CodeUnit TargetUnit { get; init; }
    public required CodeMovementType MovementType { get; init; }
    public required string SourceLocation { get; init; }
    public required string TargetLocation { get; init; }
    public required string Description { get; init; }
}

/// <summary>
/// Тип перемещения кода.
/// </summary>
public enum CodeMovementType
{
    FileChange, // Переместился в другой файл
    NamespaceChange, // Изменил namespace
    ClassChange, // Переместился в другой класс
    ParentChange, // Изменил родителя (generic)
    Rename, // Переименование (без движения)
}



namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Универсальная единица кода на любом уровне гранулярности.
/// Может быть: файлом, классом, методом, блоком кода, JSON объектом.
/// </summary>
public sealed record CodeUnit
{
    /// <summary>Уникальный стабильный ID (переживает rename/move)</summary>
    public required string Id { get; init; }

    /// <summary>Тип единицы (File, Type, Method, Block)</summary>
    public required CodeUnitType Type { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Простое имя (без namespace/parent)</summary>
    public required string Name { get; init; }

    /// <summary>Fully Qualified Name (полный путь)</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>Начальная строка в файле (1-based)</summary>
    public int StartLine { get; init; }

    /// <summary>Конечная строка в файле (1-based)</summary>
    public int EndLine { get; init; }

    /// <summary>Исходный код/контент</summary>
    public required string Content { get; init; }

    /// <summary>SHA256 hash контента (для Fast Path)</summary>
    public required string ContentHash { get; init; }

    /// <summary>Структурный fingerprint (AST hash, игнорирует whitespace/comments)</summary>
    public required string StructuralHash { get; init; }

    /// <summary>Signature (для методов: FQN + параметры)</summary>
    public string? Signature { get; init; }

    /// <summary>Embedding (генерируется лениво, может быть null)</summary>
    public float[]? Embedding { get; init; }

    /// <summary>AST representation (для структурного анализа)</summary>
    public CodeStructure? Structure { get; init; }

    /// <summary>Control Flow Graph (только для методов)</summary>
    public ControlFlowGraph? CFG { get; init; }

    /// <summary>Иерархия: ID родителя</summary>
    public string? ParentId { get; init; }

    /// <summary>Иерархия: ID детей</summary>
    public IReadOnlySet<string> ChildIds { get; init; } = new HashSet<string>();

    /// <summary>Metadata (зависит от типа парсера)</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
    new Dictionary<string, object>();

    /// <summary>Позиция в исходном файле (строка, колонка)</summary>
    public SourceLocation? Location { get; init; }
}

/// <summary>
/// Тип единицы кода.
/// </summary>
public enum CodeUnitType
{
    File,           // Весь файл
    Namespace,      // Namespace
    Type,           // Class, Interface, Struct, Enum
    Method,         // Method, Constructor
    Property,       // Property
    Field,          // Field
    Block,          // Control flow block (if, loop, try-catch)
    Statement,      // Single statement
    JsonObject,     // JSON object (для .json файлов)
    JsonArray,      // JSON array
    JsonProperty,   // JSON property
    XmlElement,     // XML element (для .xml, .csproj, .targets)
    XmlAttribute,   // XML attribute
    YamlNode,       // YAML scalar node
    YamlSequence,   // YAML sequence (array)
    YamlMapping,    // YAML mapping (dict)
    ScriptFunction, // Function в PowerShell/Shell скрипте
    ScriptBlock     // Code block в скрипте
}

/// <summary>
/// Структурное представление кода.
/// </summary>
public sealed record CodeStructure
{
    /// <summary>Нормализованный AST (без whitespace, comments)</summary>
    public required string NormalizedAst { get; init; }

    /// <summary>Список всех идентификаторов (variables, methods, types)</summary>
    public required IReadOnlySet<string> Identifiers { get; init; }

    /// <summary>Список всех типов, используемых в коде</summary>
    public required IReadOnlySet<string> UsedTypes { get; init; }

    /// <summary>Канонический порядок членов (для классов)</summary>
    public IReadOnlyList<string>? CanonicalOrder { get; init; }
}

/// <summary>
/// Позиция в исходном коде.
/// </summary>
public sealed record SourceLocation
{
    public required int StartLine { get; init; }
    public required int StartColumn { get; init; }
    public required int EndLine { get; init; }
    public required int EndColumn { get; init; }
}

/// <summary>
/// Control Flow Graph (упрощённое представление).
/// </summary>
public sealed record ControlFlowGraph
{
    /// <summary>Количество basic blocks</summary>
    public required int BlockCount { get; init; }

    /// <summary>Количество условных веток (if, switch)</summary>
    public required int BranchCount { get; init; }

    /// <summary>Количество циклов (for, while, foreach)</summary>
    public required int LoopCount { get; init; }

    /// <summary>Количество точек выхода (return, throw)</summary>
    public required int ExitPointCount { get; init; }

    /// <summary>Цикломатическая сложность</summary>
    public required int CyclomaticComplexity { get; init; }

    /// <summary>Список путей выполнения (упрощённо)</summary>
    public IReadOnlyList<ExecutionPath>? Paths { get; init; }
}

/// <summary>
/// Путь выполнения в CFG.
/// </summary>
public sealed record ExecutionPath
{
    public required string Start { get; init; }
    public required string End { get; init; }
    public required string Description { get; init; }
}

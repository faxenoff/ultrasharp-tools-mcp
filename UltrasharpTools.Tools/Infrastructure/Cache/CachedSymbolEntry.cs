using System.Text.Json.Serialization;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Тип символа для кэширования.
/// </summary>
public enum SymbolKindEnum
{
    Unknown = 0,
    Namespace = 1,
    Class = 2,
    Interface = 3,
    Struct = 4,
    Enum = 5,
    Delegate = 6,
    Method = 7,
    Constructor = 8,
    Property = 9,
    Field = 10,
    Event = 11,
    EnumMember = 12,
    Constant = 13,
    Parameter = 14,
    TypeParameter = 15,
    Local = 16,
    Label = 17,
    Other = 255
}

/// <summary>
/// Расширенная сериализуемая модель символа для кэширования.
/// Включает все данные, необходимые для semantic merge и быстрого восстановления индекса.
/// </summary>
public sealed class CachedSymbolEntry
{
    /// <summary>
    /// Fully qualified name символа.
    /// </summary>
    [JsonPropertyName("fqn")]
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// Простое имя.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Namespace.
    /// </summary>
    [JsonPropertyName("ns")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Тип символа.
    /// </summary>
    [JsonPropertyName("kind")]
    public SymbolKindEnum Kind { get; set; }

    /// <summary>
    /// Сигнатура (для методов, свойств).
    /// </summary>
    [JsonPropertyName("sig")]
    public string? Signature { get; set; }

    /// <summary>
    /// Путь к файлу (относительный от solution root).
    /// </summary>
    [JsonPropertyName("file")]
    public string? FilePath { get; set; }

    /// <summary>
    /// Начальная строка.
    /// </summary>
    [JsonPropertyName("line")]
    public int StartLine { get; set; }

    /// <summary>
    /// Конечная строка.
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    /// <summary>
    /// Assembly name.
    /// </summary>
    [JsonPropertyName("asm")]
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Project name.
    /// </summary>
    [JsonPropertyName("proj")]
    public string? ProjectName { get; set; }

    /// <summary>
    /// Fingerprint для Fast Path matching.
    /// </summary>
    [JsonPropertyName("fp")]
    public ulong Fingerprint { get; set; }

    /// <summary>
    /// Content hash для проверки изменений.
    /// </summary>
    [JsonPropertyName("hash")]
    public string? ContentHash { get; set; }

    /// <summary>
    /// Флаги метаданных (accessibility, modifiers и т.д.).
    /// </summary>
    [JsonPropertyName("flags")]
    public long Flags { get; set; }

    /// <summary>
    /// FQN родительского символа (для иерархии).
    /// </summary>
    [JsonPropertyName("parent")]
    public string? ParentFqn { get; set; }

    /// <summary>
    /// Содержимое символа (тело метода, определение класса).
    /// Опционально - может быть null для экономии места.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Конвертировать из SerializableSymbolEntry.
    /// </summary>
    public static CachedSymbolEntry FromSerializableEntry(
        SerializableSymbolEntry entry,
        string? filePath = null,
        int startLine = 0,
        int endLine = 0,
        string? signature = null,
        string? content = null,
        ulong fingerprint = 0)
    {
        return new CachedSymbolEntry
        {
            FullyQualifiedName = entry.CanonicalFqn,
            Name = entry.SimpleName,
            Namespace = entry.Namespace,
            Kind = ExtractKindFromFlags(entry.Flags),
            Signature = signature,
            FilePath = filePath,
            StartLine = startLine,
            EndLine = endLine,
            AssemblyName = entry.AssemblyName,
            ProjectName = entry.ProjectName,
            Fingerprint = fingerprint,
            Flags = entry.Flags,
            Content = content
        };
    }

    /// <summary>
    /// Конвертировать в SerializableSymbolEntry (для совместимости).
    /// </summary>
    public SerializableSymbolEntry ToSerializableEntry()
    {
        return new SerializableSymbolEntry
        {
            CanonicalFqn = FullyQualifiedName,
            SimpleName = Name,
            Namespace = Namespace ?? string.Empty,
            Flags = Flags,
            NamespaceDepth = (byte)(Namespace?.Count(c => c == '.') ?? 0),
            NameLength = (ushort)Name.Length,
            FqnLength = (ushort)FullyQualifiedName.Length,
            FirstChar = Name.Length > 0 ? char.ToLowerInvariant(Name[0]) : '\0',
            SimpleNameHashCode = Name.GetHashCode(),
            AssemblyName = AssemblyName ?? string.Empty,
            ProjectName = ProjectName ?? string.Empty
        };
    }

    /// <summary>
    /// Извлечь SymbolKind из флагов.
    /// </summary>
    private static SymbolKindEnum ExtractKindFromFlags(long flags)
    {
        // Маска для типа символа (биты 0-7)
        var kindValue = (int)(flags & 0xFF);
        return Enum.IsDefined(typeof(SymbolKindEnum), kindValue)
            ? (SymbolKindEnum)kindValue
            : SymbolKindEnum.Other;
    }
}

/// <summary>
/// Batch данных символов для эффективной сериализации.
/// </summary>
public sealed class CachedSymbolBatch
{
    /// <summary>
    /// Версия формата.
    /// </summary>
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// SHA коммита.
    /// </summary>
    [JsonPropertyName("commit")]
    public string CommitSha { get; set; } = string.Empty;

    /// <summary>
    /// Имя ветки.
    /// </summary>
    [JsonPropertyName("branch")]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// Время создания.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Символы.
    /// </summary>
    [JsonPropertyName("symbols")]
    public List<CachedSymbolEntry> Symbols { get; set; } = new();

    /// <summary>
    /// Количество символов.
    /// </summary>
    [JsonIgnore]
    public int Count => Symbols.Count;
}

/// <summary>
/// Batch embeddings для эффективной сериализации.
/// </summary>
public sealed class CachedEmbeddingBatch
{
    /// <summary>
    /// Версия формата.
    /// </summary>
    [JsonPropertyName("v")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Размерность векторов.
    /// </summary>
    [JsonPropertyName("dim")]
    public int Dimension { get; set; }

    /// <summary>
    /// Embeddings по FQN символа.
    /// </summary>
    [JsonPropertyName("embeddings")]
    public Dictionary<string, float[]> Embeddings { get; set; } = new();
}

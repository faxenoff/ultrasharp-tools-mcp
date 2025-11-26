using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Кэш одной ветки - содержит дельту изменений относительно base.
/// Структура:
/// - base/ - полный кэш основной ветки (main/master)
/// - branches/{branch-name}/ - дельта относительно base
/// </summary>
public sealed class BranchCacheEntry
{
    /// <summary>
    /// Имя ветки (нормализованное, без спецсимволов).
    /// </summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// Оригинальное имя ветки (feature/foo-bar).
    /// </summary>
    public string OriginalBranchName { get; set; } = string.Empty;

    /// <summary>
    /// SHA коммита, на котором создан кэш.
    /// </summary>
    public string CommitSha { get; set; } = string.Empty;

    /// <summary>
    /// SHA базового коммита (merge-base с main).
    /// </summary>
    public string BaseCommitSha { get; set; } = string.Empty;

    /// <summary>
    /// Время создания кэша.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Время последней валидации.
    /// </summary>
    public DateTimeOffset LastValidatedAt { get; set; }

    /// <summary>
    /// Тип кэша: Full (полный) или Delta (дельта относительно base).
    /// </summary>
    public CacheType Type { get; set; } = CacheType.Delta;

    /// <summary>
    /// Статистика кэша.
    /// </summary>
    public CacheStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Информация о файлах в этой ветке.
    /// Key: относительный путь от solution root.
    /// </summary>
    public Dictionary<string, FileState> Files { get; set; } = new();

    /// <summary>
    /// Список файлов, добавленных в этой ветке (относительно base).
    /// </summary>
    public List<string> AddedFiles { get; set; } = new();

    /// <summary>
    /// Список файлов, изменённых в этой ветке (относительно base).
    /// </summary>
    public List<string> ModifiedFiles { get; set; } = new();

    /// <summary>
    /// Список файлов, удалённых в этой ветке (относительно base).
    /// </summary>
    public List<string> DeletedFiles { get; set; } = new();

    /// <summary>
    /// Checksum файла с символами (для проверки целостности).
    /// </summary>
    public string SymbolsChecksum { get; set; } = string.Empty;

    /// <summary>
    /// Checksum файла с embeddings (если есть).
    /// </summary>
    public string? EmbeddingsChecksum { get; set; }

    /// <summary>
    /// Сериализовать в JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, BranchCacheJsonContext.Default.BranchCacheEntry);
    }

    /// <summary>
    /// Десериализовать из JSON.
    /// </summary>
    public static BranchCacheEntry? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, BranchCacheJsonContext.Default.BranchCacheEntry);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Нормализовать имя ветки для использования в пути.
    /// feature/foo-bar → feature_foo-bar
    /// </summary>
    public static string NormalizeBranchName(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
            return "unknown";

        // Заменяем / на _ и удаляем недопустимые символы
        var normalized = branchName
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_');

        // Ограничиваем длину
        if (normalized.Length > 100)
            normalized = normalized[..100];

        return normalized;
    }
}

/// <summary>
/// Тип кэша.
/// </summary>
public enum CacheType
{
    /// <summary>
    /// Полный кэш (для base ветки).
    /// </summary>
    Full,

    /// <summary>
    /// Дельта относительно base (для feature веток).
    /// </summary>
    Delta
}

/// <summary>
/// Состояние файла в кэше.
/// </summary>
public sealed class FileState
{
    /// <summary>
    /// Время последней модификации файла.
    /// </summary>
    public DateTimeOffset Mtime { get; set; }

    /// <summary>
    /// Размер файла в байтах.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// SHA256 хэш содержимого (вычисляется лениво).
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>
    /// Статус файла относительно base.
    /// </summary>
    public FileStatus Status { get; set; } = FileStatus.Unchanged;

    /// <summary>
    /// Количество символов (типов, методов и т.д.) в файле.
    /// </summary>
    public int SymbolCount { get; set; }
}

/// <summary>
/// Статус файла относительно base ветки.
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// Файл не изменён.
    /// </summary>
    Unchanged,

    /// <summary>
    /// Файл добавлен в этой ветке.
    /// </summary>
    Added,

    /// <summary>
    /// Файл изменён в этой ветке.
    /// </summary>
    Modified,

    /// <summary>
    /// Файл удалён в этой ветке.
    /// </summary>
    Deleted
}

/// <summary>
/// Статистика кэша.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Общее количество файлов.
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Общее количество символов.
    /// </summary>
    public int TotalSymbols { get; set; }

    /// <summary>
    /// Количество символов с embeddings.
    /// </summary>
    public int SymbolsWithEmbeddings { get; set; }

    /// <summary>
    /// Размер кэша символов в байтах.
    /// </summary>
    public long SymbolsCacheSize { get; set; }

    /// <summary>
    /// Размер кэша embeddings в байтах.
    /// </summary>
    public long EmbeddingsCacheSize { get; set; }

    /// <summary>
    /// Время индексации в миллисекундах.
    /// </summary>
    public long IndexingTimeMs { get; set; }

    /// <summary>
    /// Количество добавленных файлов (для дельты).
    /// </summary>
    public int AddedFilesCount { get; set; }

    /// <summary>
    /// Количество изменённых файлов (для дельты).
    /// </summary>
    public int ModifiedFilesCount { get; set; }

    /// <summary>
    /// Количество удалённых файлов (для дельты).
    /// </summary>
    public int DeletedFilesCount { get; set; }
}

/// <summary>
/// Source-generated JSON context.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(BranchCacheEntry))]
[JsonSerializable(typeof(FileState))]
[JsonSerializable(typeof(CacheStatistics))]
[JsonSerializable(typeof(Dictionary<string, FileState>))]
[JsonSerializable(typeof(List<string>))]
internal partial class BranchCacheJsonContext : JsonSerializerContext
{
}

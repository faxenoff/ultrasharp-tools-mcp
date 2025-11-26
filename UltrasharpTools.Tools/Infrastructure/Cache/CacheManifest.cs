using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Манифест кэша solution - содержит метаданные для инвалидации.
/// </summary>
public sealed class CacheManifest
{
    /// <summary>
    /// Версия формата кэша. При изменении формата - инкрементировать.
    /// </summary>
    public const string CurrentVersion = "1.0";

    /// <summary>
    /// Версия формата манифеста.
    /// </summary>
    public string Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Абсолютный путь к solution файлу.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 хэш solution файла (.sln).
    /// </summary>
    public string SolutionHash { get; set; } = string.Empty;

    /// <summary>
    /// Время создания кэша.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Время последней успешной валидации кэша.
    /// </summary>
    public DateTimeOffset LastValidatedAt { get; set; }

    /// <summary>
    /// Количество файлов в кэше.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Максимальное время модификации среди всех файлов.
    /// Используется для быстрой проверки инвалидации.
    /// </summary>
    public DateTimeOffset MaxMtime { get; set; }

    /// <summary>
    /// SHA256 checksum файла index.bin для проверки целостности.
    /// </summary>
    public string IndexChecksum { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 checksum файла embeddings.bin (если есть).
    /// </summary>
    public string? EmbeddingsChecksum { get; set; }

    /// <summary>
    /// Информация о каждом файле для инкрементальной инвалидации.
    /// Key: относительный путь от solution directory.
    /// </summary>
    public Dictionary<string, FileInfo> Files { get; set; } = new();

    /// <summary>
    /// Информация о файле в кэше.
    /// </summary>
    public sealed class FileInfo
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
        /// SHA256 хэш содержимого файла (опционально, для точной проверки).
        /// Вычисляется лениво только при необходимости.
        /// </summary>
        public string? ContentHash { get; set; }
    }

    /// <summary>
    /// Проверить совместимость версии манифеста.
    /// </summary>
    public bool IsVersionCompatible => Version == CurrentVersion;

    /// <summary>
    /// Сериализовать манифест в JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, ManifestJsonContext.Default.CacheManifest);
    }

    /// <summary>
    /// Десериализовать манифест из JSON.
    /// </summary>
    public static CacheManifest? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, ManifestJsonContext.Default.CacheManifest);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Вычислить SHA256 хэш файла.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Вычислить SHA256 хэш строки.
    /// </summary>
    public static string ComputeStringHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Вычислить SHA256 хэш байтов.
    /// </summary>
    public static string ComputeBytesHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Source-generated JSON context для CacheManifest.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CacheManifest))]
[JsonSerializable(typeof(CacheManifest.FileInfo))]
[JsonSerializable(typeof(Dictionary<string, CacheManifest.FileInfo>))]
internal partial class ManifestJsonContext : JsonSerializerContext
{
}

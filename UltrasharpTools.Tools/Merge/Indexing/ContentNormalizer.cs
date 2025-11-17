using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Нормализует содержимое файлов перед сравнением/хэшированием для Semantic Merge.
/// КРИТИЧНО: Encoding, BOM, line endings могут различаться,
/// но семантически код идентичен.
/// </summary>
public sealed class ContentNormalizer
{
private readonly ILogger<ContentNormalizer> _logger;
private readonly ContentNormalizerConfig _config;

// Целевая нормализация для всех файлов
private static readonly Encoding TargetEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
private const string TargetLineEnding = "\n"; // LF (Unix-style)

public ContentNormalizer(
ContentNormalizerConfig? config = null,
ILogger<ContentNormalizer>? logger = null)
{
_config = config ?? ContentNormalizerConfig.Default;
_logger = logger ?? NullLogger<ContentNormalizer>.Instance;
}

/// <summary>
/// Нормализовать содержимое файла для точного сравнения.
/// </summary>
public async Task<NormalizedContent> NormalizeAsync(
string filePath,
CancellationToken ct = default)
{
// 1. Прочитать raw bytes
var rawBytes = await File.ReadAllBytesAsync(filePath, ct);

// 2. Определить encoding
var detectedEncoding = DetectEncoding(rawBytes, out var hasBom);

// 3. Декодировать в string
var content = detectedEncoding.GetString(rawBytes);

// 4. Удалить BOM если есть (в начале string)
if (hasBom && content.Length > 0 && content[0] == '\uFEFF')
{
content = content.Substring(1);
_logger.LogDebug("Removed BOM from {FilePath}", filePath);
}

// Сохранить original line ending ДО нормализации
var originalLineEnding = DetectLineEnding(content);

// 5. Нормализовать line endings (CR/LF → LF)
var normalizedContent = NormalizeLineEndings(content);

// 6. Опционально: trim trailing whitespace на каждой строке
if (_config.TrimTrailingWhitespace)
{
normalizedContent = TrimTrailingWhitespace(normalizedContent);
}

// 7. Опционально: удалить trailing empty lines в конце файла
if (_config.RemoveTrailingEmptyLines)
{
normalizedContent = normalizedContent.TrimEnd('\n', '\r');
normalizedContent += "\n"; // Всегда заканчиваем на один \n
}

return new NormalizedContent
{
Content = normalizedContent,
OriginalEncoding = detectedEncoding,
HadBOM = hasBom,
OriginalLineEnding = originalLineEnding,
NormalizedEncoding = TargetEncoding,
NormalizedLineEnding = TargetLineEnding
};
}

/// <summary>
/// Определить encoding файла через BOM detection.
/// </summary>
private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
{
hasBom = false;

if (bytes.Length < 2)
return Encoding.UTF8;

// BOM detection

// UTF-8 BOM: EF BB BF
if (bytes.Length >= 3 &&
bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
{
hasBom = true;
return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
}

// UTF-16 LE BOM: FF FE
if (bytes[0] == 0xFF && bytes[1] == 0xFE)
{
hasBom = true;
return Encoding.Unicode; // UTF-16 LE
}

// UTF-16 BE BOM: FE FF
if (bytes[0] == 0xFE && bytes[1] == 0xFF)
{
hasBom = true;
return Encoding.BigEndianUnicode; // UTF-16 BE
}

// UTF-32 LE BOM: FF FE 00 00
if (bytes.Length >= 4 &&
bytes[0] == 0xFF && bytes[1] == 0xFE &&
bytes[2] == 0x00 && bytes[3] == 0x00)
{
hasBom = true;
return Encoding.UTF32; // UTF-32 LE
}

// Default: UTF-8 без BOM
return Encoding.UTF8;
}

/// <summary>
/// Нормализовать line endings: CR/LF, CR → LF.
/// </summary>
private static string NormalizeLineEndings(string content)
{
// Replace CR/LF (Windows) → LF
content = content.Replace("\r\n", "\n");

// Replace CR (old Mac) → LF
content = content.Replace("\r", "\n");

return content;
}

/// <summary>
/// Определить какой line ending используется (для метаданных).
/// </summary>
private static string DetectLineEnding(string content)
{
if (content.Contains("\r\n"))
return "\r\n"; // Windows (CR/LF)
if (content.Contains("\n"))
return "\n"; // Unix (LF)
if (content.Contains("\r"))
return "\r"; // Old Mac (CR)

return "\n"; // Default: Unix
}

/// <summary>
/// Удалить trailing whitespace в конце каждой строки.
/// </summary>
private static string TrimTrailingWhitespace(string content)
{
var lines = content.Split('\n');
for (int i = 0; i < lines.Length; i++)
{
lines[i] = lines[i].TrimEnd(' ', '\t');
}
return string.Join("\n", lines);
}

/// <summary>
/// Вычислить hash нормализованного контента.
/// ВАЖНО: используется для FastPath matching.
/// </summary>
public static string ComputeContentHash(string normalizedContent)
{
using var sha256 = System.Security.Cryptography.SHA256.Create();
var bytes = TargetEncoding.GetBytes(normalizedContent);
var hash = sha256.ComputeHash(bytes);
return Convert.ToHexString(hash);
}
}

/// <summary>
/// Результат нормализации файла.
/// </summary>
public sealed record NormalizedContent
{
public required string Content { get; init; }
public required Encoding OriginalEncoding { get; init; }
public required bool HadBOM { get; init; }
public required string OriginalLineEnding { get; init; }
public required Encoding NormalizedEncoding { get; init; }
public required string NormalizedLineEnding { get; init; }
}

/// <summary>
/// Конфигурация ContentNormalizer.
/// </summary>
public sealed record ContentNormalizerConfig
{
/// <summary>
/// Удалять trailing whitespace в конце строк.
/// </summary>
public bool TrimTrailingWhitespace { get; init; } = true;

/// <summary>
/// Удалять пустые строки в конце файла.
/// </summary>
public bool RemoveTrailingEmptyLines { get; init; } = true;

/// <summary>
/// Игнорировать whitespace-only изменения при сравнении.
/// </summary>
public bool IgnoreWhitespaceChanges { get; init; } = false;

public static ContentNormalizerConfig Default => new();

/// <summary>
/// Строгая нормализация (для production).
/// </summary>
public static ContentNormalizerConfig Strict => new()
{
TrimTrailingWhitespace = true,
RemoveTrailingEmptyLines = true,
IgnoreWhitespaceChanges = false
};

/// <summary>
/// Relaxed mode (игнорирует whitespace различия).
/// </summary>
public static ContentNormalizerConfig Relaxed => new()
{
TrimTrailingWhitespace = true,
RemoveTrailingEmptyLines = true,
IgnoreWhitespaceChanges = true
};
}

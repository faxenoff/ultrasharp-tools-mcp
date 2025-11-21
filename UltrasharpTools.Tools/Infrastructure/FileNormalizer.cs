namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Утилита для нормализации содержимого файлов.
/// Используется для raw file operations (не C# через Roslyn).
/// КРИТИЧНО для корректного сравнения файлов независимо от encoding/BOM/line endings.
/// </summary>
public static class FileNormalizer
{
    private static readonly Encoding TargetEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false
    );

    /// <summary>
    /// Прочитать и нормализовать файл.
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Нормализованное содержимое (UTF-8 no BOM, LF line endings)</returns>
    public static async Task<string> ReadNormalizedAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        // 1. Прочитать raw bytes
        var bytes = await File.ReadAllBytesAsync(filePath, ct);

        // 2. Определить encoding
        var encoding = DetectEncoding(bytes, out var hasBom);

        // 3. Декодировать
        var content = encoding.GetString(bytes);

        // 4. Удалить BOM если есть (в начале string)
        if (hasBom && content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content.Substring(1);
        }

        // 5. Нормализовать line endings
        content = NormalizeLineEndings(content);

        return content;
    }

    /// <summary>
    /// Записать нормализованный файл.
    /// Всегда использует UTF-8 без BOM и LF line endings.
    /// </summary>
    public static async Task WriteNormalizedAsync(
        string filePath,
        string content,
        CancellationToken ct = default
    )
    {
        // Нормализовать line endings перед записью
        content = NormalizeLineEndings(content);

        // Всегда используем UTF-8 без BOM
        var bytes = TargetEncoding.GetBytes(content);
        await File.WriteAllBytesAsync(filePath, bytes, ct);
    }

    /// <summary>
    /// Определить encoding файла через BOM detection.
    /// </summary>
    /// <param name="bytes">Raw bytes файла</param>
    /// <param name="hasBom">Out: есть ли BOM</param>
    /// <returns>Detected encoding</returns>
    private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
    {
        hasBom = false;

        if (bytes.Length < 2)
            return Encoding.UTF8;

        // BOM detection

        // UTF-8 BOM: EF BB BF
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
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
        if (
            bytes.Length >= 4
            && bytes[0] == 0xFF
            && bytes[1] == 0xFE
            && bytes[2] == 0x00
            && bytes[3] == 0x00
        )
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
    /// <param name="content">Исходный контент</param>
    /// <returns>Контент с нормализованными line endings (только LF)</returns>
    public static string NormalizeLineEndings(string content)
    {
        // Replace CR/LF (Windows) → LF
        content = content.Replace("\r\n", "\n");

        // Replace CR (old Mac) → LF
        content = content.Replace("\r", "\n");

        return content;
    }

    /// <summary>
    /// Определить какой line ending используется в файле (для метаданных).
    /// </summary>
    public static string DetectLineEnding(string content)
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
    /// Вычислить стабильный hash нормализованного контента.
    /// ВАЖНО: используется для сравнения файлов (не требует криптостойкости).
    /// Использует xxHash128 (7x быстрее SHA256).
    /// </summary>
    public static string ComputeContentHash(string normalizedContent)
    {
        return FastHash.ComputeHash128(normalizedContent);
    }
}

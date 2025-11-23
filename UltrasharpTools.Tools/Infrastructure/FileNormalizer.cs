using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Утилита для нормализации содержимого файлов.
/// Используется для raw file operations (не C# через Roslyn).
/// КРИТИЧНО для корректного сравнения файлов независимо от encoding/BOM/line endings.
///
/// ОПТИМИЗИРОВАНО: Использует ArrayPool для сокращения GC аллокаций.
/// </summary>
public static class FileNormalizer
{
    private static readonly Encoding TargetEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false
    );

    /// <summary>
    /// Прочитать и нормализовать файл.
    /// ОПТИМИЗИРОВАНО: Использует BufferPoolManager для сокращения аллокаций.
    /// </summary>
    /// <param name="filePath">Путь к файлу</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Нормализованное содержимое (UTF-8 no BOM, LF line endings)</returns>
    public static async Task<string> ReadNormalizedAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        // 1. Get file size for buffer allocation
        var fileInfo = new FileInfo(filePath);
        var fileSize = (int)fileInfo.Length;

        // 2. Rent buffer from pool instead of allocating
        var byteBuffer = BufferPoolManager.RentBytes(fileSize);
        try
        {
            // 3. Read file into pooled buffer
            using var stream = File.OpenRead(filePath);
            int bytesRead = await stream.ReadAsync(byteBuffer.AsMemory(0, fileSize), ct);

            // 4. Determine encoding
            var encoding = DetectEncoding(byteBuffer.AsSpan(0, bytesRead), out var hasBom);

            // 5. Decode bytes to string
            var content = encoding.GetString(byteBuffer, 0, bytesRead);

            // 6. Remove BOM if present (in string)
            if (hasBom && content.Length > 0 && content[0] == '\uFEFF')
            {
                content = content.Substring(1);
            }

            // 7. Normalize line endings
            content = NormalizeLineEndings(content);

            return content;
        }
        finally
        {
            // 8. Always return buffer to pool
            BufferPoolManager.ReturnBytes(byteBuffer);
        }
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
    /// ОПТИМИЗИРОВАНО: Принимает ReadOnlySpan&lt;byte&gt; для zero-copy доступа.
    /// </summary>
    /// <param name="bytes">Raw bytes файла</param>
    /// <param name="hasBom">Out: есть ли BOM</param>
    /// <returns>Detected encoding</returns>
    private static Encoding DetectEncoding(ReadOnlySpan<byte> bytes, out bool hasBom)
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

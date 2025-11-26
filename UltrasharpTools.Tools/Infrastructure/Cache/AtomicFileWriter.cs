namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Атомарная запись файлов с защитой от сбоев.
/// Использует паттерн write-to-temp + atomic-rename.
/// </summary>
public static class AtomicFileWriter
{
    /// <summary>
    /// Атомарно записать текст в файл.
    /// </summary>
    /// <param name="filePath">Путь к целевому файлу.</param>
    /// <param name="content">Содержимое для записи.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteAllTextAsync(string filePath, string content, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = GetTempPath(filePath);

        try
        {
            // 1. Записать во временный файл
            await File.WriteAllTextAsync(tempPath, content, ct);

            // 2. Flush на диск (fsync)
            await FlushFileAsync(tempPath);

            // 3. Atomic rename (заменяет существующий файл)
            AtomicMove(tempPath, filePath);
        }
        finally
        {
            // Cleanup временного файла если что-то пошло не так
            TryDeleteFile(tempPath);
        }
    }

    /// <summary>
    /// Атомарно записать байты в файл.
    /// </summary>
    /// <param name="filePath">Путь к целевому файлу.</param>
    /// <param name="data">Данные для записи.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteAllBytesAsync(string filePath, byte[] data, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = GetTempPath(filePath);

        try
        {
            // 1. Записать во временный файл
            await File.WriteAllBytesAsync(tempPath, data, ct);

            // 2. Flush на диск (fsync)
            await FlushFileAsync(tempPath);

            // 3. Atomic rename
            AtomicMove(tempPath, filePath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    /// <summary>
    /// Атомарно записать stream в файл.
    /// </summary>
    public static async Task WriteStreamAsync(string filePath, Stream sourceStream, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = GetTempPath(filePath);

        try
        {
            // 1. Записать во временный файл
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await sourceStream.CopyToAsync(fileStream, ct);
                await fileStream.FlushAsync(ct);
            }

            // 2. Atomic rename
            AtomicMove(tempPath, filePath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    /// <summary>
    /// Получить путь к временному файлу.
    /// Формат: {original}.{guid}.tmp
    /// </summary>
    private static string GetTempPath(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? ".";
        var fileName = Path.GetFileName(filePath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>
    /// Flush файла на диск (fsync).
    /// Гарантирует, что данные записаны на физический носитель.
    /// </summary>
    private static async Task FlushFileAsync(string filePath)
    {
        await using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.None);

        // На Windows это вызывает FlushFileBuffers
        // На Linux/Mac - fsync
        fs.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Атомарное перемещение файла с заменой существующего.
    /// </summary>
    private static void AtomicMove(string sourcePath, string destPath)
    {
        // File.Move с overwrite=true атомарен на большинстве FS
        // На NTFS и ext4 это atomic rename
        File.Move(sourcePath, destPath, overwrite: true);
    }

    /// <summary>
    /// Попытаться удалить файл (игнорируя ошибки).
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Игнорируем - временный файл удалится позже
        }
    }

    /// <summary>
    /// Удалить все временные файлы в директории (.*.tmp).
    /// Вызывать при старте для cleanup после crash.
    /// </summary>
    public static void CleanupTempFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            foreach (var tempFile in Directory.EnumerateFiles(directory, ".*.tmp"))
            {
                TryDeleteFile(tempFile);
            }
        }
        catch
        {
            // Игнорируем ошибки при cleanup
        }
    }
}

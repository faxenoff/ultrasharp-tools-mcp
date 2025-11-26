using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Infrastructure.Cache;

/// <summary>
/// Межпроцессная блокировка на основе файлов.
/// Защищает от race conditions при параллельном доступе к кэшу.
/// Корректно обрабатывает внезапное завершение процессов (stale locks).
/// </summary>
public sealed partial class ProcessLock : IDisposable
{
    private readonly string _lockPath;
    private readonly FileStream? _lockStream;
    private readonly bool _acquired;

    /// <summary>
    /// True если блокировка успешно захвачена.
    /// </summary>
    public bool IsAcquired => _acquired;

    private ProcessLock(string lockPath, FileStream? lockStream, bool acquired)
    {
        _lockPath = lockPath;
        _lockStream = lockStream;
        _acquired = acquired;
    }

    /// <summary>
    /// Попытаться захватить блокировку.
    /// </summary>
    /// <param name="lockPath">Путь к lock файлу.</param>
    /// <param name="timeout">Максимальное время ожидания.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ProcessLock с IsAcquired=true при успехе.</returns>
    public static async Task<ProcessLock> TryAcquireAsync(
        string lockPath,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deadline = DateTime.UtcNow + timeout;
        var retryDelay = TimeSpan.FromMilliseconds(50);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            // Попытка 1: Захватить блокировку напрямую
            var (stream, acquired) = TryAcquireLockFile(lockPath);
            if (acquired && stream != null)
            {
                // Записать информацию о текущем процессе
                await WriteLockInfoAsync(stream);
                return new ProcessLock(lockPath, stream, true);
            }

            // Попытка 2: Проверить, не stale ли существующий lock
            if (await TryCleanStaleLockAsync(lockPath))
            {
                // Lock был stale и удалён, попробуем ещё раз
                continue;
            }

            // Подождать и повторить
            await Task.Delay(retryDelay, ct);
            retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 1.5, 500));
        }

        // Timeout - не удалось захватить блокировку
        return new ProcessLock(lockPath, null, false);
    }

    /// <summary>
    /// Захватить блокировку синхронно (для простых случаев).
    /// </summary>
    public static ProcessLock TryAcquire(string lockPath, TimeSpan timeout)
    {
        return TryAcquireAsync(lockPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Попытаться создать lock файл с эксклюзивным доступом.
    /// </summary>
    private static (FileStream? Stream, bool Acquired) TryAcquireLockFile(string lockPath)
    {
        try
        {
            // FileShare.None = эксклюзивный доступ
            var stream = new FileStream(
                lockPath,
                FileMode.CreateNew, // Fail если файл существует
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.DeleteOnClose); // Автоудаление при закрытии

            return (stream, true);
        }
        catch (IOException)
        {
            // Файл уже существует или занят другим процессом
            return (null, false);
        }
    }

    /// <summary>
    /// Записать информацию о текущем процессе в lock файл.
    /// </summary>
    private static async Task WriteLockInfoAsync(FileStream stream)
    {
        var lockInfo = new LockInfo
        {
            Pid = Environment.ProcessId,
            MachineName = Environment.MachineName,
            StartTime = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            AcquiredAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(lockInfo, LockInfoJsonContext.Default.LockInfo);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        stream.SetLength(0);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Проверить и удалить stale lock (от мёртвого процесса).
    /// </summary>
    private static async Task<bool> TryCleanStaleLockAsync(string lockPath)
    {
        if (!File.Exists(lockPath))
            return false;

        try
        {
            // Попробовать прочитать информацию о lock
            var json = await File.ReadAllTextAsync(lockPath);
            var lockInfo = JsonSerializer.Deserialize(json, LockInfoJsonContext.Default.LockInfo);

            if (lockInfo == null)
            {
                // Повреждённый lock файл - удалить
                TryDeleteLock(lockPath);
                return true;
            }

            // Проверить, жив ли процесс
            if (!IsProcessAlive(lockInfo.Pid, lockInfo.StartTime))
            {
                // Процесс мёртв - это stale lock
                TryDeleteLock(lockPath);
                return true;
            }

            // Процесс жив - lock валиден
            return false;
        }
        catch
        {
            // Не можем прочитать lock файл - возможно занят
            // Не удаляем, вернёмся позже
            return false;
        }
    }

    /// <summary>
    /// Проверить, жив ли процесс с указанным PID.
    /// </summary>
    private static bool IsProcessAlive(int pid, DateTime expectedStartTime)
    {
        try
        {
            var process = Process.GetProcessById(pid);

            // Проверить время запуска (защита от PID reuse)
            // Допускаем погрешность в 1 секунду
            var actualStartTime = process.StartTime.ToUniversalTime();
            var timeDiff = Math.Abs((actualStartTime - expectedStartTime).TotalSeconds);

            return timeDiff < 2;
        }
        catch (ArgumentException)
        {
            // Процесс с таким PID не существует
            return false;
        }
        catch (InvalidOperationException)
        {
            // Процесс завершился
            return false;
        }
    }

    /// <summary>
    /// Попытаться удалить lock файл.
    /// </summary>
    private static void TryDeleteLock(string lockPath)
    {
        try
        {
            File.Delete(lockPath);
        }
        catch
        {
            // Игнорируем - возможно файл уже удалён или занят
        }
    }

    public void Dispose()
    {
        _lockStream?.Dispose();
        // FileOptions.DeleteOnClose автоматически удалит файл
    }

    /// <summary>
    /// Информация о процессе, держащем блокировку.
    /// </summary>
    private sealed class LockInfo
    {
        public int Pid { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime AcquiredAt { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(LockInfo))]
    private partial class LockInfoJsonContext : JsonSerializerContext
    {
    }
}

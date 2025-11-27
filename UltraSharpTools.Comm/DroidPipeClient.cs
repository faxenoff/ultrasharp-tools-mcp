using System.Diagnostics;
using System.IO.Pipes;

namespace UltraSharpTools.Comm;

/// <summary>
/// Клиент для подключения к Droid через Named Pipe.
/// Если Droid не запущен - запускает его в pipe-server режиме.
/// </summary>
public sealed class DroidPipeClient : IDisposable {
    public const string PipeName = "UltraSharpTools_Droid";
    public const string WakeUpEventName = "UltraSharpTools_Droid_WakeUp";

    private readonly string _droidPath;
    private readonly string[] _args;
    private NamedPipeClientStream? _pipe;
    private Process? _droidProcess;

    public DroidPipeClient(string[]? args = null) {
        var currentDir = AppContext.BaseDirectory;

        // Ищем Droid в нескольких местах:
        // 1. Та же директория (для разработки)
        // 2. ../Droid/ (для publish структуры)
        var sameDirPath = Path.Combine(currentDir, "UltrasharpTools.Droid.exe");
        var siblingDirPath = Path.Combine(currentDir, "..", "Droid", "UltrasharpTools.Droid.exe");

        if (File.Exists(sameDirPath)) {
            _droidPath = sameDirPath;
        } else if (File.Exists(siblingDirPath)) {
            _droidPath = Path.GetFullPath(siblingDirPath);
        } else {
            // Fallback - будет ошибка при попытке запуска
            _droidPath = sameDirPath;
        }

        _args = args ?? [];
    }

    /// <summary>
    /// Подключается к Droid через Named Pipe.
    /// Если Droid не запущен - запускает его.
    /// </summary>
    public async Task<DroidStreams> ConnectAsync(CancellationToken cancellationToken = default) {
        // Сигнализируем wake-up event (если Droid в idle mode)
        SignalWakeUpEvent();
        await Task.Delay(100, cancellationToken);

        // Пробуем подключиться к существующему Droid
        try {
            _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipe.ConnectAsync(2000, cancellationToken);
            return new DroidStreams(_pipe, _pipe);
        } catch (TimeoutException) {
            _pipe?.Dispose();
            _pipe = null;
        }

        // Droid не запущен - запускаем его
        if (!IsDroidRunning()) {
            await StartDroidAsync(cancellationToken);
        }

        // Повторяем wake-up и подключение
        SignalWakeUpEvent();
        await Task.Delay(200, cancellationToken);

        _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(30000, cancellationToken); // Даём время на старт

        return new DroidStreams(_pipe, _pipe);
    }

    private async Task StartDroidAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_droidPath)) {
            throw new FileNotFoundException(
            $"Droid executable not found: {_droidPath}. " +
            "Ensure UltrasharpTools.Droid.exe is in the same directory as Comm."
            );
        }

        // Используем ТЕКУЩИЙ рабочий каталог (где Claude Desktop запустил Comm)
        // чтобы автоопределение solution в Droid нашло .sln файл проекта
        var workingDirectory = Environment.CurrentDirectory;

        var startInfo = new ProcessStartInfo {
            FileName = _droidPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = false,
            RedirectStandardOutput = false,
            RedirectStandardInput = false,
        };

        // Добавляем --pipe-server для multi-client режима
        startInfo.ArgumentList.Add("--pipe-server");

        // Проброс аргументов (solution path, etc.)
        foreach (var arg in _args) {
            startInfo.ArgumentList.Add(arg);
        }

        // Check for shared runtime folder
        var sharedRuntimePath = Path.Combine(Path.GetDirectoryName(_droidPath)!, "shared");
        if (Directory.Exists(sharedRuntimePath)) {
            startInfo.Environment["DOTNET_ROOT"] = sharedRuntimePath;
        }

        _droidProcess = Process.Start(startInfo);

        if (_droidProcess == null) {
            throw new InvalidOperationException("Failed to start Droid process");
        }

        // Ждём пока Droid создаст Named Pipe
        await WaitForPipeAsync(cancellationToken);
    }

    private async Task WaitForPipeAsync(CancellationToken cancellationToken) {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(30);

        while (sw.Elapsed < timeout) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                using var testPipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await testPipe.ConnectAsync(500, cancellationToken);
                // Pipe существует - Droid готов
                return;
            } catch (TimeoutException) {
                // Pipe ещё не создан, ждём
                await Task.Delay(200, cancellationToken);
            }
        }

        throw new TimeoutException($"Droid did not create Named Pipe within {timeout.TotalSeconds} seconds");
    }

    private bool IsDroidRunning() {
        try {
            var processes = Process.GetProcessesByName("UltrasharpTools.Droid");
            var isRunning = processes.Length > 0;
            foreach (var p in processes) {
                p.Dispose();
            }
            return isRunning;
        } catch {
            return false;
        }
    }

    private void SignalWakeUpEvent() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        try {
            using var wakeUpEvent = EventWaitHandle.OpenExisting(WakeUpEventName);
            wakeUpEvent.Set();
        } catch (WaitHandleCannotBeOpenedException) {
            // Event не существует - Droid ещё не создал его
        } catch {
            // Ignore other errors
        }
    }

    public void Dispose() {
        _pipe?.Dispose();
        // НЕ убиваем Droid - он shared между всеми Comm
    }
}

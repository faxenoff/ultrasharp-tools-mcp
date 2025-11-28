using System.IO.Pipelines;
using UltraSharpTools.Comm;

public class Program {
    public const string ApplicationName = "UltraSharpTools.Comm";
    public const string ApplicationVersion = "3.5.0";

    public static async Task Main(string[] args) {
        // Устанавливаем заголовок с названием каталога для идентификации в Task Manager
        var dirName = Path.GetFileName(Environment.CurrentDirectory) ?? "unknown";
        Console.Title = $"{ApplicationName} [{dirName}]";

        // Show help if requested
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h")) {
            Console.WriteLine($"{ApplicationName} v{ApplicationVersion}");
            Console.WriteLine("Lightweight proxy for UltraSharpTools MCP server.");
            Console.WriteLine("Connects to Droid via Named Pipe (starts Droid if not running).");
            return;
        }

        if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v")) {
            Console.WriteLine($"{ApplicationName} v{ApplicationVersion}");
            return;
        }

        // Подключаемся к Droid через Named Pipe (запускает Droid если не запущен)
        using var pipeClient = new DroidPipeClient(args);

        try {
            var droidStreams = await pipeClient.ConnectAsync();

            // Проксируем stdin/stdout ↔ Named Pipe
            using var cts = new CancellationTokenSource();

            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            var stdinTask = ProxyStreamAsync(
                stdin,
                droidStreams.Input,
                cts.Token);

            var stdoutTask = ProxyStreamAsync(
                droidStreams.Output,
                stdout,
                cts.Token);

            // Ждём завершения любого направления
            await Task.WhenAny(stdinTask, stdoutTask);

            // Останавливаем другое направление
            await cts.CancelAsync();
            await Task.Delay(100);
        } catch {
            // Ошибка подключения - Claude Desktop увидит что процесс завершился
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Проксирует байты из source в destination.
    /// </summary>
    private static async Task ProxyStreamAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken) {
        var reader = PipeReader.Create(source);
        var writer = PipeWriter.Create(destination);

        try {
            while (true) {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length > 0) {
                    foreach (var segment in buffer) {
                        await writer.WriteAsync(segment, cancellationToken);
                    }
                    await writer.FlushAsync(cancellationToken);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted) {
                    break;
                }
            }
        } catch (OperationCanceledException) {
            // Нормальное завершение
        } catch {
            // Pipe закрыт
        } finally {
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }
}

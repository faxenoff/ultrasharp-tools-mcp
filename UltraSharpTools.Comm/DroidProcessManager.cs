using System.Diagnostics;

namespace UltraSharpTools.Comm;

/// <summary>
/// Manages Droid process lifecycle and provides access to its stdin/stdout streams
/// </summary>
public sealed class DroidProcessManager : IDisposable
{
    private Process? _droidProcess;
    private readonly string _droidPath;
    private readonly string[] _args;

    public DroidProcessManager(string[]? args = null)
    {
        var currentDir = AppContext.BaseDirectory;
        _droidPath = Path.Combine(currentDir, "UltrasharpTools.Droid.exe");
        _args = args ?? [];
    }

    /// <summary>
    /// Starts Droid process and returns wrapper for stdin/stdout communication
    /// </summary>
    public DroidStreams StartDroid()
    {
        if (!File.Exists(_droidPath))
        {
            throw new FileNotFoundException(
                $"Droid executable not found: {_droidPath}. " +
                "Ensure UltrasharpTools.Droid.exe is in the same directory as Comm."
            );
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _droidPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Forward all command line arguments to Droid
        foreach (var arg in _args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        _droidProcess = Process.Start(startInfo);

        if (_droidProcess == null)
        {
            throw new InvalidOperationException("Failed to start Droid process");
        }

        // Read stderr asynchronously to prevent blocking
        // (Droid logs to file, but we read stderr just in case)
        _droidProcess.BeginErrorReadLine();

        return new DroidStreams(
            _droidProcess.StandardOutput.BaseStream,
            _droidProcess.StandardInput.BaseStream
        );
    }

    public void Dispose()
    {
        if (_droidProcess != null && !_droidProcess.HasExited)
        {
            _droidProcess.Kill(entireProcessTree: true);
        }
        _droidProcess?.Dispose();
    }
}

/// <summary>
/// Wrapper for Droid process stdin/stdout streams
/// </summary>
public record DroidStreams(Stream Output, Stream Input);

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Фоновый сервис для отслеживания Git изменений
/// </summary>
public sealed partial class GitWatcherService : BackgroundService
{
    private static readonly char[] newLineSeparator = new[] { '\r', '\n' };
    private readonly AgentConfig _config;
    private readonly IServerBridgeService _bridge;
    private readonly ILogger<GitWatcherService> _logger;
    private string? _currentBranch;
    private string? _lastCommitSha;

    public GitWatcherService(
        AgentConfig config,
        IServerBridgeService bridge,
        ILogger<GitWatcherService> logger
    )
    {
        _config = config;
        _bridge = bridge;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(_config.RepositoryPath);

        // Инициализация текущего состояния
        _currentBranch = GetCurrentBranch();
        _lastCommitSha = GetLastCommitSha();

        LogInitialState(_currentBranch ?? "unknown", _lastCommitSha ?? "unknown");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_config.GitCheckIntervalMs, stoppingToken);
                await CheckGitChanges(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            LogStopping();
        }
        catch (Exception ex)
        {
            LogServiceError(ex);
        }
    }

    private async Task CheckGitChanges(CancellationToken cancellationToken)
    {
        try
        {
            // Проверка переключения ветки
            var newBranch = GetCurrentBranch();
            if (newBranch != _currentBranch && newBranch != null && _currentBranch != null)
            {
                LogBranchSwitched(_currentBranch, newBranch);

                var evt = new BranchSwitchEvent
                {
                    Project = _config.ProjectName,
                    FromBranch = _currentBranch,
                    ToBranch = newBranch,
                };

                await _bridge.SendBranchSwitchEventAsync(evt, cancellationToken);
                _currentBranch = newBranch;
            }

            // Проверка новых коммитов
            var newCommitSha = GetLastCommitSha();
            if (newCommitSha != _lastCommitSha && newCommitSha != null && _lastCommitSha != null)
            {
                LogNewCommit(_lastCommitSha[..7], newCommitSha[..7]);

                var changedFiles = GetChangedFilesInCommit(newCommitSha);

                var evt = new GitCommitEvent
                {
                    Project = _config.ProjectName,
                    Branch = _currentBranch ?? "unknown",
                    CommitSha = newCommitSha,
                    FilesChanged = changedFiles,
                };

                await _bridge.SendGitCommitEventAsync(evt, cancellationToken);
                _lastCommitSha = newCommitSha;
            }
        }
        catch (Exception ex)
        {
            LogCheckFailed(ex);
        }
    }

    private string? GetCurrentBranch()
    {
        try
        {
            var gitHeadPath = Path.Combine(_config.RepositoryPath, ".git", "HEAD");
            if (!File.Exists(gitHeadPath))
            {
                return null;
            }

            var headContent = File.ReadAllText(gitHeadPath).Trim();
            if (headContent.StartsWith("ref: refs/heads/"))
            {
                return headContent["ref: refs/heads/".Length..];
            }

            // Detached HEAD state
            return headContent[..7]; // Первые 7 символов SHA
        }
        catch (Exception ex)
        {
            LogGetBranchFailed(ex);
            return null;
        }
    }

    private string? GetLastCommitSha()
    {
        try
        {
            var branch = GetCurrentBranch();
            if (branch == null)
            {
                return null;
            }

            // Если detached HEAD, branch уже содержит SHA
            if (branch.Length == 7 && !branch.Contains('/'))
            {
                return branch;
            }

            // Читаем SHA из refs/heads/<branch>
            var refPath = Path.Combine(_config.RepositoryPath, ".git", "refs", "heads", branch);
            if (File.Exists(refPath))
            {
                return File.ReadAllText(refPath).Trim();
            }

            // Пробуем packed-refs
            var packedRefsPath = Path.Combine(_config.RepositoryPath, ".git", "packed-refs");
            if (File.Exists(packedRefsPath))
            {
                var lines = File.ReadAllLines(packedRefsPath);
                foreach (var line in lines)
                {
                    if (line.Contains($"refs/heads/{branch}"))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            return parts[0];
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogGetCommitFailed(ex);
            return null;
        }
    }

    private string[] GetChangedFilesInCommit(string commitSha)
    {
        try
        {
            // Используем git CLI для получения списка изменённых файлов
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff-tree --no-commit-id --name-only -r {commitSha}",
                WorkingDirectory = _config.RepositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                LogGitProcessFailed();
                return Array.Empty<string>();
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                LogDiffTreeFailed(error);
                return Array.Empty<string>();
            }

            // Парсим вывод - каждая строка это путь к файлу
            var files = output
                .Split(newLineSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Replace('\\', '/'))
                .ToArray();

            if (files.Length > 0)
            {
                LogFoundChangedFiles(files.Length, commitSha[..7]);
            }

            return files;
        }
        catch (Exception ex)
        {
            LogGetChangedFilesFailed(ex, commitSha);
            return Array.Empty<string>();
        }
    }
}

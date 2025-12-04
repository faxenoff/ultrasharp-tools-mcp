using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Merge.Git;

/// <summary>
/// Читает файлы из git веток без создания worktrees.
/// Использует git show для получения содержимого файлов.
/// </summary>
public sealed class GitBranchReader {
    private readonly string _repositoryPath;
    private readonly ILogger<GitBranchReader> _logger;

    public GitBranchReader(string repositoryPath, ILogger<GitBranchReader>? logger = null) {
        _repositoryPath = repositoryPath;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<GitBranchReader>();
    }

    /// <summary>
    /// Найти merge-base (общий предок) двух веток.
    /// </summary>
    public async Task<string?> FindMergeBaseAsync(string branch1, string branch2, CancellationToken ct = default) {
        var result = await RunGitCommandAsync($"merge-base {branch1} {branch2}", ct);
        return result.Success ? result.Output.Trim() : null;
    }

    /// <summary>
    /// Получить содержимое файла из указанной ветки/коммита.
    /// </summary>
    public async Task<string?> GetFileContentAsync(string branchOrCommit, string filePath, CancellationToken ct = default) {
        // git show branch:path/to/file
        var gitPath = filePath.Replace('\\', '/');
        var result = await RunGitCommandAsync($"show {branchOrCommit}:{gitPath}", ct);
        return result.Success ? result.Output : null;
    }

    /// <summary>
    /// Получить список файлов в ветке по паттерну.
    /// </summary>
    public async Task<List<string>> ListFilesAsync(string branchOrCommit, string pattern = "*", CancellationToken ct = default) {
        // git ls-tree -r --name-only branch
        var result = await RunGitCommandAsync($"ls-tree -r --name-only {branchOrCommit}", ct);
        if (!result.Success)
            return new List<string>();

        var files = result.Output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(f => MatchesPattern(f, pattern))
        .ToList();

        return files;
    }

    /// <summary>
    /// Получить список изменённых файлов между двумя ветками.
    /// </summary>
    public async Task<List<string>> GetChangedFilesAsync(string baseBranch, string targetBranch, CancellationToken ct = default) {
        // git diff --name-only base..target
        var result = await RunGitCommandAsync($"diff --name-only {baseBranch}..{targetBranch}", ct);
        if (!result.Success)
            return new List<string>();

        return result.Output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .ToList();
    }

    /// <summary>
    /// Проверить существует ли ветка.
    /// </summary>
    public async Task<bool> BranchExistsAsync(string branch, CancellationToken ct = default) {
        var result = await RunGitCommandAsync($"rev-parse --verify {branch}", ct);
        return result.Success;
    }

    /// <summary>
    /// Получить текущую ветку.
    /// </summary>
    public async Task<string?> GetCurrentBranchAsync(CancellationToken ct = default) {
        var result = await RunGitCommandAsync("rev-parse --abbrev-ref HEAD", ct);
        return result.Success ? result.Output.Trim() : null;
    }

    /// <summary>
    /// Получить короткий хеш коммита.
    /// </summary>
    public async Task<string?> GetShortHashAsync(string branchOrCommit, CancellationToken ct = default) {
        var result = await RunGitCommandAsync($"rev-parse --short {branchOrCommit}", ct);
        return result.Success ? result.Output.Trim() : null;
    }

    /// <summary>
    /// Записать файл в рабочую директорию (unstaged).
    /// </summary>
    public async Task WriteFileAsync(string relativePath, string content, CancellationToken ct = default) {
        var fullPath = Path.Combine(_repositoryPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, ct);
        _logger.LogDebug("Written file: {Path}", relativePath);
    }

    /// <summary>
    /// Batch чтение файлов из ветки.
    /// </summary>
    public async Task<Dictionary<string, string>> GetFilesContentAsync(
    string branchOrCommit,
    IEnumerable<string> filePaths,
    CancellationToken ct = default) {
        var result = new Dictionary<string, string>();

        // Параллельное чтение для производительности
        var tasks = filePaths.Select(async path => {
            var content = await GetFileContentAsync(branchOrCommit, path, ct);
            return (path, content);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (path, content) in results) {
            if (content != null) {
                result[path] = content;
            }
        }

        return result;
    }

    private bool MatchesPattern(string filePath, string pattern) {
        if (pattern == "*")
            return true;

        // Простой glob matching
        if (pattern.StartsWith("*.")) {
            var extension = pattern.Substring(1);
            return filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.Contains("*")) {
            // Конвертируем glob в regex
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*")
            .Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(filePath, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool Success, string Output)> RunGitCommandAsync(string arguments, CancellationToken ct) {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var shortArgs = arguments.Length > 80 ? arguments[..80] + "..." : arguments;
        _logger.LogTrace("[GIT] Starting: git {Args} in {WorkDir}", shortArgs, _repositoryPath);
        try {
            var startInfo = new ProcessStartInfo {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _repositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true, // Redirect stdin to prevent waiting for input
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Disable git pager and interactive prompts
            startInfo.Environment["GIT_PAGER"] = "";
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GIT_ASKPASS"] = "";
            startInfo.Environment["SSH_ASKPASS"] = "";
            startInfo.Environment["GCM_INTERACTIVE"] = "never";

            using var process = new Process { StartInfo = startInfo };

            process.Start();
            process.StandardInput.Close(); // Close stdin immediately to prevent waiting for input

            // Use timeout to prevent hanging
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout per git command

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try {
                await process.WaitForExitAsync(timeoutCts.Token);
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                _logger.LogError("[GIT] !!! TIMEOUT after 30s: git {Args}", shortArgs);
                try { process.Kill(entireProcessTree: true); } catch { }
                return (false, "Git command timed out after 30 seconds");
            }

            var output = await outputTask;
            var error = await errorTask;

            sw.Stop();
            if (process.ExitCode != 0) {
                _logger.LogWarning("[GIT] Failed ({Ms}ms, exit={ExitCode}): git {Args} -> {Error}", sw.ElapsedMilliseconds, process.ExitCode, shortArgs, error);
                return (false, error);
            }

            _logger.LogDebug("[GIT] Success ({Ms}ms): git {Args}", sw.ElapsedMilliseconds, shortArgs);
            return (true, output);
        } catch (OperationCanceledException) {
            sw.Stop();
            _logger.LogWarning("[GIT] Cancelled ({Ms}ms): git {Args}", sw.ElapsedMilliseconds, shortArgs);
            throw;
        } catch (Exception ex) {
            sw.Stop();
            _logger.LogWarning(ex, "[GIT] Exception ({Ms}ms): git {Args}", sw.ElapsedMilliseconds, shortArgs);
            return (false, ex.Message);
        }
    }
    /// <summary>
    /// Получить список изменённых файлов между двумя ветками С ИХ СТАТУСОМ.
    /// Использует git diff --name-status для получения типа изменения (A/M/D/R).
    /// </summary>
    public async Task<List<GitFileChange>> GetChangedFilesWithStatusAsync(
        string baseBranch,
        string targetBranch,
        CancellationToken ct = default) {
        // git diff --name-status base..target
        // Output format: "M\tpath/to/file" or "R100\told/path\tnew/path"
        var cmd = $"diff --name-status {baseBranch}..{targetBranch}";
        _logger.LogInformation("[GIT] GetChangedFilesWithStatusAsync: {Cmd} in {WorkDir}", cmd, _repositoryPath);
        var result = await RunGitCommandAsync(cmd, ct);
        _logger.LogInformation("[GIT] GetChangedFilesWithStatusAsync result: Success={Success}, OutputLen={Len}, Output={Output}",
            result.Success, result.Output?.Length ?? 0, result.Output?.Length > 200 ? result.Output[..200] + "..." : result.Output);
        if (!result.Success) {
            _logger.LogWarning("[GIT] GetChangedFilesWithStatusAsync failed for {Base}..{Target}", baseBranch, targetBranch);
            return new List<GitFileChange>();
        }

        var changes = new List<GitFileChange>();
        if (string.IsNullOrEmpty(result.Output))
            return changes;
        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation("[GIT] GetChangedFilesWithStatusAsync: {LineCount} lines parsed from output", lines.Length);

        foreach (var line in lines) {
            var parts = line.Split('\t');
            if (parts.Length < 2)
                continue;

            var statusCode = parts[0];
            var status = ParseGitStatus(statusCode);

            if (status == GitFileStatus.Renamed || status == GitFileStatus.Copied) {
                // R100\told/path\tnew/path or C100\told/path\tnew/path
                if (parts.Length >= 3) {
                    changes.Add(new GitFileChange(parts[2], status, parts[1]));
                }
            } else {
                changes.Add(new GitFileChange(parts[1], status));
            }
        }

        return changes;
    }

    private static GitFileStatus ParseGitStatus(string code) {
        if (string.IsNullOrEmpty(code))
            return GitFileStatus.Unknown;

        return code[0] switch {
            'A' => GitFileStatus.Added,
            'M' => GitFileStatus.Modified,
            'D' => GitFileStatus.Deleted,
            'R' => GitFileStatus.Renamed,
            'C' => GitFileStatus.Copied,
            _ => GitFileStatus.Unknown
        };
    }
    /// <summary>
    /// Batch извлечение файлов из ветки в целевую директорию.
    /// Использует git archive для эффективного чтения множества файлов одним запросом.
    /// </summary>
    /// <param name="branchOrCommit">Ветка или коммит</param>
    /// <param name="filePaths">Список путей к файлам</param>
    /// <param name="targetDirectory">Директория для извлечения</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Количество успешно извлечённых файлов</returns>
    public async Task<int> ExtractFilesToDirectoryAsync(
        string branchOrCommit,
        IReadOnlyList<string> filePaths,
        string targetDirectory,
        CancellationToken ct = default) {
        if (filePaths.Count == 0)
            return 0;

        _logger.LogDebug("[GIT] ExtractFilesToDirectoryAsync: {Branch}, {Count} files to {Dir}",
            branchOrCommit, filePaths.Count, targetDirectory);

        // Создать директорию если не существует
        if (!Directory.Exists(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        // Формируем аргументы для git archive
        // git archive branch -- path1 path2 path3 | tar -x -C targetDir
        var pathsArg = string.Join(" ", filePaths.Select(p => $"\"{p.Replace('\\', '/')}\""));

        // На Windows используем tar из git (он идёт в комплекте)
        var gitDir = await FindGitExecutableDirectoryAsync(ct);
        var tarPath = gitDir != null ? Path.Combine(gitDir, "tar.exe") : "tar";

        var archiveArgs = $"archive {branchOrCommit} -- {pathsArg}";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try {
            // Запускаем git archive и пайпим в tar
            var gitStartInfo = new ProcessStartInfo {
                FileName = "git",
                Arguments = archiveArgs,
                WorkingDirectory = _repositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            gitStartInfo.Environment["GIT_PAGER"] = "";
            gitStartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

            var tarStartInfo = new ProcessStartInfo {
                FileName = tarPath,
                Arguments = $"-x -C \"{targetDirectory}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var gitProcess = new Process { StartInfo = gitStartInfo };
            using var tarProcess = new Process { StartInfo = tarStartInfo };

            gitProcess.Start();
            gitProcess.StandardInput.Close();
            tarProcess.Start();

            // Копируем stdout git в stdin tar
            var copyTask = gitProcess.StandardOutput.BaseStream.CopyToAsync(tarProcess.StandardInput.BaseStream, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

            try {
                await copyTask;
                tarProcess.StandardInput.Close();

                await Task.WhenAll(
                    gitProcess.WaitForExitAsync(timeoutCts.Token),
                    tarProcess.WaitForExitAsync(timeoutCts.Token)
                );
            } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                _logger.LogError("[GIT] ExtractFilesToDirectoryAsync TIMEOUT after 60s");
                try { gitProcess.Kill(entireProcessTree: true); } catch { }
                try { tarProcess.Kill(entireProcessTree: true); } catch { }
                return 0;
            }

            sw.Stop();

            if (gitProcess.ExitCode != 0) {
                var error = await gitProcess.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("[GIT] git archive failed: {Error}. Falling back to individual file extraction.", error.Trim());

                // Fallback: извлекаем файлы по одному через git show
                return await ExtractFilesIndividuallyAsync(branchOrCommit, filePaths, targetDirectory, ct);
            }

            // Подсчитать извлечённые файлы
            var extractedCount = filePaths.Count(f =>
                File.Exists(Path.Combine(targetDirectory, f.Replace('/', Path.DirectorySeparatorChar))));

            _logger.LogDebug("[GIT] ExtractFilesToDirectoryAsync completed in {Ms}ms: {Count}/{Total} files",
                sw.ElapsedMilliseconds, extractedCount, filePaths.Count);

            return extractedCount;
        } catch (Exception ex) {
            sw.Stop();
            _logger.LogWarning(ex, "[GIT] ExtractFilesToDirectoryAsync failed after {Ms}ms. Falling back to individual extraction.", sw.ElapsedMilliseconds);

            // Fallback: извлекаем файлы по одному через git show
            return await ExtractFilesIndividuallyAsync(branchOrCommit, filePaths, targetDirectory, ct);
        }
    }

    /// <summary>
    /// Fallback: извлечение файлов по одному через git show.
    /// Медленнее, но надёжнее - игнорирует отсутствующие файлы.
    /// </summary>
    private async Task<int> ExtractFilesIndividuallyAsync(
        string branchOrCommit,
        IReadOnlyList<string> filePaths,
        string targetDirectory,
        CancellationToken ct) {

        _logger.LogDebug("[GIT] ExtractFilesIndividuallyAsync: extracting {Count} files one by one", filePaths.Count);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var extracted = 0;

        foreach (var filePath in filePaths) {
            ct.ThrowIfCancellationRequested();
            try {
                var normalizedPath = filePath.Replace('\\', '/');
                var content = await GetFileContentAsync(branchOrCommit, normalizedPath, ct);
                if (content != null) {
                    var targetPath = Path.Combine(targetDirectory, filePath.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(targetPath, content, ct);
                    extracted++;
                }
            } catch (Exception ex) {
                _logger.LogDebug("[GIT] Failed to extract {File} from {Branch}: {Error}",
                    filePath, branchOrCommit, ex.Message);
            }
        }

        sw.Stop();
        _logger.LogDebug("[GIT] ExtractFilesIndividuallyAsync completed in {Ms}ms: {Count}/{Total} files",
            sw.ElapsedMilliseconds, extracted, filePaths.Count);

        return extracted;
    }

private async Task<string?> FindGitExecutableDirectoryAsync(CancellationToken ct) {
    try {
        var result = await RunGitCommandAsync("--exec-path", ct);
        if (result.Success) {
            var path = result.Output.Trim();
            // git --exec-path возвращает путь к libexec/git-core, нам нужен usr/bin
            var gitRoot = Path.GetDirectoryName(Path.GetDirectoryName(path));
            if (gitRoot != null) {
                var binPath = Path.Combine(gitRoot, "usr", "bin");
                if (Directory.Exists(binPath)) return binPath;
            }
        }
    } catch { }
    return null;
}}

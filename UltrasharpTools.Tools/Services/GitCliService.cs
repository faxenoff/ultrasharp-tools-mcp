using System.Diagnostics;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Git service implementation using direct git CLI calls instead of LibGit2Sharp.
/// Saves ~3-4 MB by removing native dependencies and improves cross-platform compatibility.
/// </summary>
public partial class GitCliService(ILogger<GitCliService> logger, GitOptions? gitOptions = null)
    : IGitService
{
    private readonly ILogger<GitCliService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly GitOptions _gitOptions = gitOptions ?? new GitOptions();
    private const string SharpToolsBranchPrefix = "sharptools/";
    private const string SharpToolsUndoBranchPrefix = "sharptools/undo/";

    public async Task<bool> IsRepositoryAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var solutionDirectory = Path.GetDirectoryName(solutionPath);
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                return false;
            }

            var result = await RunGitCommandAsync(
                solutionDirectory,
                cancellationToken,
                "rev-parse",
                "--git-dir"
            );
            return result.Success;
        }
        catch (Exception ex)
        {
            LogRepositoryCheckError(ex.Message);
            return false;
        }
    }

    public async Task<bool> IsOnSharpToolsBranchAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return false;
            }

            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            if (!result.Success)
            {
                return false;
            }

            var currentBranch = result.Output.Trim();
            var isOnSharpToolsBranch = currentBranch.StartsWith(
                SharpToolsBranchPrefix,
                StringComparison.OrdinalIgnoreCase
            );

            LogCurrentBranchCheck(currentBranch, isOnSharpToolsBranch);

            return isOnSharpToolsBranch;
        }
        catch (Exception ex)
        {
            LogBranchCheckError(ex.Message);
            return false;
        }
    }

    public async Task EnsureSharpToolsBranchAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd/HH-mm-ss");
            var branchName = $"{SharpToolsBranchPrefix}{timestamp}";

            // Check if we're already on a sharptools branch
            var currentBranchResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            if (currentBranchResult.Success)
            {
                var currentBranch = currentBranchResult.Output.Trim();
                if (
                    currentBranch.StartsWith(
                        SharpToolsBranchPrefix,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    LogAlreadyOnSharpToolsBranch(currentBranch);
                    return;
                }
            }

            // Create and checkout the new branch
            var createResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "checkout",
                "-b",
                branchName
            );
            if (!createResult.Success)
            {
                LogBranchCreateFailed(branchName, createResult.Error);
                throw new InvalidOperationException(
                    $"Failed to create git branch: {createResult.Error}"
                );
            }

            LogBranchCreated(branchName);
        }
        catch (Exception ex)
        {
            LogEnsureBranchError(ex, solutionPath);
            throw;
        }

        // Auto cleanup old branches if enabled
        if (_gitOptions.AutoCleanup)
        {
            try
            {
                await CleanupOldBranchesAsync(
                    solutionPath,
                    _gitOptions.RetentionCount,
                    _gitOptions.RetentionDays,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                LogAutoCleanupFailed(ex);
            }
        }
    }

    public async Task CommitChangesAsync(
        string solutionPath,
        IEnumerable<string> changedFilePaths,
        string commitMessage,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return;
            }

            // Stage the changed files
            var stagedFiles = new List<string>();
            foreach (var filePath in changedFilePaths)
            {
                try
                {
                    // Convert absolute path to relative path from repository root
                    var relativePath = Path.GetRelativePath(repositoryPath, filePath);

                    // Stage the file
                    var stageResult = await RunGitCommandAsync(
                        repositoryPath,
                        cancellationToken,
                        "add",
                        relativePath
                    );
                    if (stageResult.Success)
                    {
                        stagedFiles.Add(relativePath);
                        LogStagedFile(relativePath);
                    }
                    else
                    {
                        LogStageFileFailed(filePath, stageResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogStageFileFailed(filePath, ex.Message);
                }
            }

            if (stagedFiles.Count == 0)
            {
                LogNoFilesStaged();
                return;
            }

            // Create commit
            var commitResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "commit",
                "-m",
                commitMessage
            );
            if (!commitResult.Success)
            {
                LogCommitFailed(commitResult.Error);
                throw new InvalidOperationException(
                    $"Failed to create git commit: {commitResult.Error}"
                );
            }

            // Get commit SHA
            var shaResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "rev-parse",
                "HEAD"
            );
            var commitSha = shaResult.Success ? shaResult.Output.Trim()[..8] : "unknown";

            LogCommitCreated(commitSha, stagedFiles.Count, commitMessage);
        }
        catch (Exception ex)
        {
            LogCommitError(ex, solutionPath);
            throw;
        }
    }

    private async Task<string?> GetRepositoryPathAsync(
        string solutionPath,
        CancellationToken cancellationToken
    )
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrEmpty(solutionDirectory))
        {
            return null;
        }

        // Find git directory
        var result = await RunGitCommandAsync(
            solutionDirectory,
            cancellationToken,
            "rev-parse",
            "--show-toplevel"
        );
        if (!result.Success)
        {
            return null;
        }

        return result.Output.Trim();
    }

    public async Task<string> CreateUndoBranchAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return string.Empty;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd/HH-mm-ss");
            var branchName = $"{SharpToolsUndoBranchPrefix}{timestamp}";

            // Create a new branch at the current commit, but don't checkout
            var createResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                branchName
            );
            if (!createResult.Success)
            {
                LogUndoBranchFailed(branchName, createResult.Error);
                return string.Empty;
            }

            // Get current commit SHA
            var shaResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "rev-parse",
                "HEAD"
            );
            var commitSha = shaResult.Success ? shaResult.Output.Trim()[..8] : "unknown";

            LogUndoBranchCreated(branchName, commitSha);

            return branchName;
        }
        catch (Exception ex)
        {
            LogUndoBranchError(ex, solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetDiffAsync(
        string solutionPath,
        string oldCommitSha,
        string newCommitSha,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return string.Empty;
            }

            // Get diff between commits
            var diffResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "diff",
                $"{oldCommitSha}..{newCommitSha}"
            );
            if (!diffResult.Success)
            {
                LogDiffFailed(oldCommitSha?[..8] ?? "null", newCommitSha?[..8] ?? "null", diffResult.Error);
                return string.Empty;
            }

            return $"Changes between {oldCommitSha[..8]} and {newCommitSha[..8]}:\n\n{diffResult.Output}";
        }
        catch (Exception ex)
        {
            LogGetDiffError(ex, solutionPath);
            return $"Error generating diff: {ex.Message}";
        }
    }

    public async Task<(bool success, string diff)> RevertLastCommitAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return (false, string.Empty);
            }

            // Get current branch
            var branchResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            if (!branchResult.Success)
            {
                return (false, string.Empty);
            }

            var currentBranch = branchResult.Output.Trim();

            // Ensure we're on a sharptools branch
            if (
                !currentBranch.StartsWith(
                    SharpToolsBranchPrefix,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                LogNotOnSharpToolsBranch(currentBranch);
                return (false, string.Empty);
            }

            // Get current commit and parent
            var currentShaResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "rev-parse",
                "HEAD"
            );
            var parentShaResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "rev-parse",
                "HEAD~1"
            );

            if (!currentShaResult.Success || !parentShaResult.Success)
            {
                LogRevertShasFailed();
                return (false, string.Empty);
            }

            var currentCommitSha = currentShaResult.Output.Trim();
            var parentCommitSha = parentShaResult.Output.Trim();

            LogReverting(currentCommitSha[..8], parentCommitSha[..8]);

            // First, create an undo branch at the current commit
            var undoBranchName = await CreateUndoBranchAsync(solutionPath, cancellationToken);
            if (string.IsNullOrEmpty(undoBranchName))
            {
                LogUndoBranchCreateFailed();
            }

            // Get the diff before we reset
            var diff = await GetDiffAsync(
                solutionPath,
                parentCommitSha,
                currentCommitSha,
                cancellationToken
            );

            // Reset to the parent commit (hard reset)
            var resetResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "reset",
                "--hard",
                "HEAD~1"
            );
            if (!resetResult.Success)
            {
                LogResetFailed(resetResult.Error);
                return (false, $"Error: {resetResult.Error}");
            }

            LogRevertSuccess(parentCommitSha[..8]);

            var resultMessage = !string.IsNullOrEmpty(undoBranchName)
                ? $"The changes have been preserved in branch '{undoBranchName}' for future reference."
                : string.Empty;

            return (true, diff + "\n\n" + resultMessage);
        }
        catch (Exception ex)
        {
            LogRevertError(ex, solutionPath);
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<string> GetBranchOriginCommitAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepository(solutionPath);
                return string.Empty;
            }

            // Get current branch
            var branchResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            if (!branchResult.Success)
            {
                return string.Empty;
            }

            var currentBranch = branchResult.Output.Trim();

            // Ensure we're on a sharptools branch
            if (
                !currentBranch.StartsWith(
                    SharpToolsBranchPrefix,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                LogNotOnSharpToolsBranchDebug(currentBranch);
                return string.Empty;
            }

            // Get the first commit of this branch (using reflog)
            var reflogResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "reflog",
                "show",
                currentBranch,
                "--pretty=%H",
                "--"
            );

            if (!reflogResult.Success || string.IsNullOrEmpty(reflogResult.Output))
            {
                return string.Empty;
            }

            // Get the last line (oldest entry)
            var lines = reflogResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var originCommitSha = lines[^1].Trim();

            LogBranchOriginCommit(currentBranch, originCommitSha[..8]);

            return originCommitSha;
        }
        catch (Exception ex)
        {
            LogBranchOriginError(ex, solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetCurrentBranchAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return string.Empty;
            }

            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            return result.Success ? result.Output.Trim() : string.Empty;
        }
        catch (Exception ex)
        {
            LogGetCurrentBranchError(ex, solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetCurrentCommitShaAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return string.Empty;
            }

            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "rev-parse",
                "HEAD"
            );
            return result.Success ? result.Output.Trim() : string.Empty;
        }
        catch (Exception ex)
        {
            LogGetCommitShaError(ex, solutionPath);
            return string.Empty;
        }
    }

    public async Task<string?> GetMergeBaseCommitAsync(
        string solutionPath,
        string branch1,
        string branch2,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return null;
            }

            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "merge-base",
                branch1,
                branch2
            );
            return result.Success ? result.Output.Trim() : null;
        }
        catch (Exception ex)
        {
            LogMergeBaseError(ex, branch1, branch2);
            return null;
        }
    }

    public async Task<List<(string FilePath, string ChangeType)>> GetChangedFilesAsync(
        string solutionPath,
        string fromCommitSha,
        string toCommitSha,
        CancellationToken cancellationToken = default
    )
    {
        var changedFiles = new List<(string FilePath, string ChangeType)>();

        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return changedFiles;
            }

            // Get changed files with status
            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "diff",
                "--name-status",
                $"{fromCommitSha}..{toCommitSha}"
            );

            if (!result.Success)
            {
                return changedFiles;
            }

            // Parse output: each line is "Status\tFilePath"
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2)
                {
                    var status = parts[0].Trim();
                    var filePath = parts[1].Trim();

                    var changeType = status switch
                    {
                        "A" => "Added",
                        "M" => "Modified",
                        "D" => "Deleted",
                        "R" => "Renamed",
                        _ => status,
                    };

                    changedFiles.Add((filePath, changeType));
                }
            }
        }
        catch (Exception ex)
        {
            LogGetChangedFilesError(ex, fromCommitSha, toCommitSha);
        }

        return changedFiles;
    }

    public async Task<List<string>> GetAllBranchesAsync(
        string solutionPath,
        CancellationToken cancellationToken = default
    )
    {
        var branches = new List<string>();

        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return branches;
            }

            // Get all local and remote branches
            var result = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "-a",
                "--format=%(refname:short)"
            );

            if (!result.Success)
            {
                return branches;
            }

            branches = result
                .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .ToList();
        }
        catch (Exception ex)
        {
            LogGetBranchesError(ex, solutionPath);
        }

        return branches;
    }

    public async Task<List<string>> CleanupOldBranchesAsync(
        string solutionPath,
        int? retentionCount = null,
        int? retentionDays = null,
        CancellationToken cancellationToken = default
    )
    {
        var deletedBranches = new List<string>();

        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                LogNoRepositoryForCleanup(solutionPath);
                return deletedBranches;
            }

            // Get current branch
            var currentBranchResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--show-current"
            );
            var currentBranch = currentBranchResult.Success
                ? currentBranchResult.Output.Trim()
                : string.Empty;

            // Get all sharptools branches
            var branchesResult = await RunGitCommandAsync(
                repositoryPath,
                cancellationToken,
                "branch",
                "--list",
                $"{SharpToolsBranchPrefix}*",
                "--format=%(refname:short)|%(committerdate:iso8601)"
            );
            if (!branchesResult.Success)
            {
                return deletedBranches;
            }

            var branches = branchesResult
                .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var parts = line.Split('|');
                    var name = parts[0].Trim();
                    var dateStr = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                    DateTimeOffset.TryParse(dateStr, out var date);
                    return (Name: name, Date: date);
                })
                .OrderByDescending(b => b.Date)
                .ToList();

            LogFoundBranches(branches.Count);

            var effectiveRetentionCount = retentionCount ?? _gitOptions.RetentionCount;
            var effectiveRetentionDays = retentionDays ?? _gitOptions.RetentionDays;

            foreach (var branch in branches)
            {
                var branchName = branch.Name;
                var branchDate = branch.Date;

                // Never delete the current branch
                if (string.Equals(branchName, currentBranch, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check retention count
                var branchIndex = branches.FindIndex(b => b.Name == branchName);
                var shouldDeleteByCount = branchIndex >= effectiveRetentionCount;

                // Check retention days
                var age = DateTimeOffset.Now - branchDate;
                var shouldDeleteByAge =
                    effectiveRetentionDays > 0 && age.TotalDays > effectiveRetentionDays;

                if (shouldDeleteByCount || shouldDeleteByAge)
                {
                    var deleteResult = await RunGitCommandAsync(
                        repositoryPath,
                        cancellationToken,
                        "branch",
                        "-D",
                        branchName
                    );
                    if (deleteResult.Success)
                    {
                        deletedBranches.Add(branchName);
                        LogBranchDeleted(branchName, age.TotalDays);
                    }
                    else
                    {
                        LogBranchDeleteFailed(branchName, deleteResult.Error);
                    }
                }
            }

            if (deletedBranches.Count > 0)
            {
                LogCleanupComplete(deletedBranches.Count);
            }
        }
        catch (Exception ex)
        {
            LogCleanupError(ex, solutionPath);
        }

        return deletedBranches;
    }

    /// <summary>
    /// Runs a git command and returns the result.
    /// Cross-platform: works on Windows, Linux, and macOS.
    /// </summary>
    private async Task<(bool Success, string Output, string Error)> RunGitCommandAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments
    )
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    outputBuilder.AppendLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    errorBuilder.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Используем timeout 10 секунд для git команд + cancellationToken
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout - убиваем процесс
                LogGitTimeout(string.Join(" ", arguments));
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Игнорируем ошибки при kill
                }
                return (false, string.Empty, "Git command timed out after 10 seconds");
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();
            var success = process.ExitCode == 0;

            return (success, output, error);
        }
        catch (Exception ex)
        {
            LogGitCommandError(ex, string.Join(" ", arguments));
            return (false, string.Empty, ex.Message);
        }
    }
}

using System.Diagnostics;
using System.Text;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Git service implementation using direct git CLI calls instead of LibGit2Sharp.
/// Saves ~3-4 MB by removing native dependencies and improves cross-platform compatibility.
/// </summary>
public class GitCliService(ILogger<GitCliService> logger, GitOptions? gitOptions = null) : IGitService
{
    private readonly ILogger<GitCliService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly GitOptions _gitOptions = gitOptions ?? new GitOptions();
    private const string SharpToolsBranchPrefix = "sharptools/";
    private const string SharpToolsUndoBranchPrefix = "sharptools/undo/";

    public async Task<bool> IsRepositoryAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var solutionDirectory = Path.GetDirectoryName(solutionPath);
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                return false;
            }

            var result = await RunGitCommandAsync(solutionDirectory, cancellationToken, "rev-parse", "--git-dir");
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error checking if path is a Git repository: {Error}", ex.Message);
            return false;
        }
    }

    public async Task<bool> IsOnSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return false;
            }

            var result = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            if (!result.Success)
            {
                return false;
            }

            var currentBranch = result.Output.Trim();
            var isOnSharpToolsBranch = currentBranch.StartsWith(SharpToolsBranchPrefix, StringComparison.OrdinalIgnoreCase);

            _logger.LogDebug("Current branch: {BranchName}, IsSharpToolsBranch: {IsSharpToolsBranch}",
                currentBranch, isOnSharpToolsBranch);

            return isOnSharpToolsBranch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error checking current branch: {Error}", ex.Message);
            return false;
        }
    }

    public async Task EnsureSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
                return;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd/HH-mm-ss");
            var branchName = $"{SharpToolsBranchPrefix}{timestamp}";

            // Check if we're already on a sharptools branch
            var currentBranchResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            if (currentBranchResult.Success)
            {
                var currentBranch = currentBranchResult.Output.Trim();
                if (currentBranch.StartsWith(SharpToolsBranchPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Already on SharpTools branch: {BranchName}", currentBranch);
                    return;
                }
            }

            // Create and checkout the new branch
            var createResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "checkout", "-b", branchName);
            if (!createResult.Success)
            {
                _logger.LogError("Failed to create and checkout branch {BranchName}: {Error}", branchName, createResult.Error);
                throw new InvalidOperationException($"Failed to create git branch: {createResult.Error}");
            }

            _logger.LogInformation("Created and switched to SharpTools branch: {BranchName}", branchName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring SharpTools branch for solution at {SolutionPath}", solutionPath);
            throw;
        }

        // Auto cleanup old branches if enabled
        if (_gitOptions.AutoCleanup)
        {
            try
            {
                await CleanupOldBranchesAsync(solutionPath, _gitOptions.RetentionCount, _gitOptions.RetentionDays, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto cleanup of old branches failed, but continuing");
            }
        }
    }

    public async Task CommitChangesAsync(string solutionPath, IEnumerable<string> changedFilePaths,
        string commitMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
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
                    var stageResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "add", relativePath);
                    if (stageResult.Success)
                    {
                        stagedFiles.Add(relativePath);
                        _logger.LogDebug("Staged file: {FilePath}", relativePath);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to stage file {FilePath}: {Error}", filePath, stageResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to stage file {FilePath}: {Error}", filePath, ex.Message);
                }
            }

            if (stagedFiles.Count == 0)
            {
                _logger.LogWarning("No files were staged for commit");
                return;
            }

            // Create commit
            var commitResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "commit", "-m", commitMessage);
            if (!commitResult.Success)
            {
                _logger.LogError("Failed to create commit: {Error}", commitResult.Error);
                throw new InvalidOperationException($"Failed to create git commit: {commitResult.Error}");
            }

            // Get commit SHA
            var shaResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "rev-parse", "HEAD");
            var commitSha = shaResult.Success ? shaResult.Output.Trim()[..8] : "unknown";

            _logger.LogInformation("Created commit {CommitSha} with {FileCount} files: {CommitMessage}",
                commitSha, stagedFiles.Count, commitMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing changes for solution at {SolutionPath}", solutionPath);
            throw;
        }
    }

    private async Task<string?> GetRepositoryPathAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrEmpty(solutionDirectory))
        {
            return null;
        }

        // Find git directory
        var result = await RunGitCommandAsync(solutionDirectory, cancellationToken, "rev-parse", "--show-toplevel");
        if (!result.Success)
        {
            return null;
        }

        return result.Output.Trim();
    }

    public async Task<string> CreateUndoBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
                return string.Empty;
            }

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd/HH-mm-ss");
            var branchName = $"{SharpToolsUndoBranchPrefix}{timestamp}";

            // Create a new branch at the current commit, but don't checkout
            var createResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", branchName);
            if (!createResult.Success)
            {
                _logger.LogError("Failed to create undo branch {BranchName}: {Error}", branchName, createResult.Error);
                return string.Empty;
            }

            // Get current commit SHA
            var shaResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "rev-parse", "HEAD");
            var commitSha = shaResult.Success ? shaResult.Output.Trim()[..8] : "unknown";

            _logger.LogInformation("Created undo branch: {BranchName} at commit {CommitSha}",
                branchName, commitSha);

            return branchName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating undo branch for solution at {SolutionPath}", solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetDiffAsync(string solutionPath, string oldCommitSha, string newCommitSha, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
                return string.Empty;
            }

            // Get diff between commits
            var diffResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "diff", $"{oldCommitSha}..{newCommitSha}");
            if (!diffResult.Success)
            {
                _logger.LogWarning("Could not get diff between {OldSha} and {NewSha}: {Error}",
                    oldCommitSha?[..8] ?? "null", newCommitSha?[..8] ?? "null", diffResult.Error);
                return string.Empty;
            }

            return $"Changes between {oldCommitSha[..8]} and {newCommitSha[..8]}:\n\n{diffResult.Output}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diff for solution at {SolutionPath}", solutionPath);
            return $"Error generating diff: {ex.Message}";
        }
    }

    public async Task<(bool success, string diff)> RevertLastCommitAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
                return (false, string.Empty);
            }

            // Get current branch
            var branchResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            if (!branchResult.Success)
            {
                return (false, string.Empty);
            }

            var currentBranch = branchResult.Output.Trim();

            // Ensure we're on a sharptools branch
            if (!currentBranch.StartsWith(SharpToolsBranchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Not on a SharpTools branch, cannot revert. Current branch: {BranchName}", currentBranch);
                return (false, string.Empty);
            }

            // Get current commit and parent
            var currentShaResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "rev-parse", "HEAD");
            var parentShaResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "rev-parse", "HEAD~1");

            if (!currentShaResult.Success || !parentShaResult.Success)
            {
                _logger.LogWarning("Could not get commit SHAs for revert");
                return (false, string.Empty);
            }

            var currentCommitSha = currentShaResult.Output.Trim();
            var parentCommitSha = parentShaResult.Output.Trim();

            _logger.LogInformation("Reverting from commit {CurrentSha} to parent {ParentSha}",
                currentCommitSha[..8], parentCommitSha[..8]);

            // First, create an undo branch at the current commit
            var undoBranchName = await CreateUndoBranchAsync(solutionPath, cancellationToken);
            if (string.IsNullOrEmpty(undoBranchName))
            {
                _logger.LogWarning("Failed to create undo branch");
            }

            // Get the diff before we reset
            var diff = await GetDiffAsync(solutionPath, parentCommitSha, currentCommitSha, cancellationToken);

            // Reset to the parent commit (hard reset)
            var resetResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "reset", "--hard", "HEAD~1");
            if (!resetResult.Success)
            {
                _logger.LogError("Failed to reset to parent commit: {Error}", resetResult.Error);
                return (false, $"Error: {resetResult.Error}");
            }

            _logger.LogInformation("Successfully reverted to commit {CommitSha}", parentCommitSha[..8]);

            var resultMessage = !string.IsNullOrEmpty(undoBranchName)
                ? $"The changes have been preserved in branch '{undoBranchName}' for future reference."
                : string.Empty;

            return (true, diff + "\n\n" + resultMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverting last commit for solution at {SolutionPath}", solutionPath);
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<string> GetBranchOriginCommitAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogWarning("No Git repository found for solution at {SolutionPath}", solutionPath);
                return string.Empty;
            }

            // Get current branch
            var branchResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            if (!branchResult.Success)
            {
                return string.Empty;
            }

            var currentBranch = branchResult.Output.Trim();

            // Ensure we're on a sharptools branch
            if (!currentBranch.StartsWith(SharpToolsBranchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Not on a SharpTools branch: {BranchName}", currentBranch);
                return string.Empty;
            }

            // Get the first commit of this branch (using reflog)
            var reflogResult = await RunGitCommandAsync(repositoryPath, cancellationToken,
                "reflog", "show", currentBranch, "--pretty=%H", "--");

            if (!reflogResult.Success || string.IsNullOrEmpty(reflogResult.Output))
            {
                return string.Empty;
            }

            // Get the last line (oldest entry)
            var lines = reflogResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var originCommitSha = lines[^1].Trim();

            _logger.LogDebug("Origin commit for branch {BranchName}: {CommitSha}",
                currentBranch, originCommitSha[..8]);

            return originCommitSha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch origin commit for solution at {SolutionPath}", solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetCurrentBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return string.Empty;
            }

            var result = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            return result.Success ? result.Output.Trim() : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current branch for solution at {SolutionPath}", solutionPath);
            return string.Empty;
        }
    }

    public async Task<string> GetCurrentCommitShaAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return string.Empty;
            }

            var result = await RunGitCommandAsync(repositoryPath, cancellationToken, "rev-parse", "HEAD");
            return result.Success ? result.Output.Trim() : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current commit SHA for solution at {SolutionPath}", solutionPath);
            return string.Empty;
        }
    }

    public async Task<string?> GetMergeBaseCommitAsync(string solutionPath, string branch1, string branch2, CancellationToken cancellationToken = default)
    {
        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                return null;
            }

            var result = await RunGitCommandAsync(repositoryPath, cancellationToken, "merge-base", branch1, branch2);
            return result.Success ? result.Output.Trim() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting merge base for branches {Branch1} and {Branch2}", branch1, branch2);
            return null;
        }
    }

    public async Task<List<(string FilePath, string ChangeType)>> GetChangedFilesAsync(string solutionPath, string fromCommitSha, string toCommitSha, CancellationToken cancellationToken = default)
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
            var result = await RunGitCommandAsync(repositoryPath, cancellationToken,
                "diff", "--name-status", $"{fromCommitSha}..{toCommitSha}");

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
                        _ => status
                    };

                    changedFiles.Add((filePath, changeType));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changed files between {From} and {To}", fromCommitSha, toCommitSha);
        }

        return changedFiles;
    }

    public async Task<List<string>> GetAllBranchesAsync(string solutionPath, CancellationToken cancellationToken = default)
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
            var result = await RunGitCommandAsync(repositoryPath, cancellationToken,
                "branch", "-a", "--format=%(refname:short)");

            if (!result.Success)
            {
                return branches;
            }

            branches = result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all branches for solution at {SolutionPath}", solutionPath);
        }

        return branches;
    }

    public async Task<List<string>> CleanupOldBranchesAsync(string solutionPath, int? retentionCount = null, int? retentionDays = null, CancellationToken cancellationToken = default)
    {
        var deletedBranches = new List<string>();

        try
        {
            var repositoryPath = await GetRepositoryPathAsync(solutionPath, cancellationToken);
            if (repositoryPath == null)
            {
                _logger.LogDebug("No Git repository found for solution at {SolutionPath}, skipping cleanup", solutionPath);
                return deletedBranches;
            }

            // Get current branch
            var currentBranchResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--show-current");
            var currentBranch = currentBranchResult.Success ? currentBranchResult.Output.Trim() : string.Empty;

            // Get all sharptools branches
            var branchesResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "--list", $"{SharpToolsBranchPrefix}*", "--format=%(refname:short)|%(committerdate:iso8601)");
            if (!branchesResult.Success)
            {
                return deletedBranches;
            }

            var branches = branchesResult.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
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

            _logger.LogDebug("Found {Count} SharpTools branches", branches.Count);

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
                var shouldDeleteByAge = effectiveRetentionDays > 0 && age.TotalDays > effectiveRetentionDays;

                if (shouldDeleteByCount || shouldDeleteByAge)
                {
                    var deleteResult = await RunGitCommandAsync(repositoryPath, cancellationToken, "branch", "-D", branchName);
                    if (deleteResult.Success)
                    {
                        deletedBranches.Add(branchName);
                        _logger.LogInformation("Deleted old SharpTools branch: {BranchName} (age: {AgeDays:F1} days)",
                            branchName, age.TotalDays);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete branch {BranchName}: {Error}", branchName, deleteResult.Error);
                    }
                }
            }

            if (deletedBranches.Count > 0)
            {
                _logger.LogInformation("Cleanup complete: deleted {Count} old branches", deletedBranches.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old branches for solution at {SolutionPath}", solutionPath);
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
        params string[] arguments)
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
                CreateNoWindow = true
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

            await process.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();
            var success = process.ExitCode == 0;

            return (success, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running git command: {Arguments}", string.Join(" ", arguments));
            return (false, string.Empty, ex.Message);
        }
    }
}

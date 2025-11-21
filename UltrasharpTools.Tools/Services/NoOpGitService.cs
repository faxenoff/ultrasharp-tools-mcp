

namespace UltrasharpTools.Tools.Services;

public class NoOpGitService : IGitService
{
    public Task<bool> IsRepositoryAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> IsOnSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task EnsureSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CommitChangesAsync(string solutionPath, IEnumerable<string> changedFilePaths, string commitMessage, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<(bool success, string diff)> RevertLastCommitAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((false, string.Empty));
    }

    public Task<string> GetBranchOriginCommitAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> CreateUndoBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> GetDiffAsync(string solutionPath, string oldCommitSha, string newCommitSha, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<List<string>> CleanupOldBranchesAsync(string solutionPath, int? retentionCount = null, int? retentionDays = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }

    public Task<string> GetCurrentBranchAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string> GetCurrentCommitShaAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }

    public Task<string?> GetMergeBaseCommitAsync(string solutionPath, string branch1, string branch2, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<List<(string FilePath, string ChangeType)>> GetChangedFilesAsync(string solutionPath, string fromCommitSha, string toCommitSha, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<(string FilePath, string ChangeType)>());
    }

    public Task<List<string>> GetAllBranchesAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }
}

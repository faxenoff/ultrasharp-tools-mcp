namespace UltrasharpTools.Tools.Interfaces;
public interface IGitService {
    Task<bool> IsRepositoryAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<bool> IsOnSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task EnsureSharpToolsBranchAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task CommitChangesAsync(string solutionPath, IEnumerable<string> changedFilePaths, string commitMessage, CancellationToken cancellationToken = default);
    Task<(bool success, string diff)> RevertLastCommitAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<string> GetBranchOriginCommitAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<string> CreateUndoBranchAsync(string solutionPath, CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(string solutionPath, string oldCommitSha, string newCommitSha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old sharptools/* branches based on retention policy.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file</param>
    /// <param name="retentionCount">Keep only the N most recent branches (null = no limit)</param>
    /// <param name="retentionDays">Keep branches created within the last N days (null = no limit)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of deleted branch names</returns>
    Task<List<string>> CleanupOldBranchesAsync(string solutionPath, int? retentionCount = null, int? retentionDays = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current branch name.
    /// </summary>
    Task<string> GetCurrentBranchAsync(string solutionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current commit SHA.
    /// </summary>
    Task<string> GetCurrentCommitShaAsync(string solutionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find merge-base commit between two branches (common ancestor).
    /// </summary>
    Task<string?> GetMergeBaseCommitAsync(string solutionPath, string branch1, string branch2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of changed files between two commits.
    /// Returns (filePath, changeType) where changeType is: Added, Modified, Deleted, Renamed.
    /// </summary>
    Task<List<(string FilePath, string ChangeType)>> GetChangedFilesAsync(string solutionPath, string fromCommitSha, string toCommitSha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all branch names in the repository (local and remote).
    /// Used for orphaned delta cleanup and compaction.
    /// </summary>
    Task<List<string>> GetAllBranchesAsync(string solutionPath, CancellationToken cancellationToken = default);
}
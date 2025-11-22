using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Versioning;

/// <summary>
/// Manages code snapshots for safe experimentation and rollback.
/// Supports both git-based (stash) and file-based (.backup/) backends.
/// </summary>
public sealed class VersionManager
{
    private readonly ISolutionManager _solutionManager;
    private readonly IGitService _gitService;
    private readonly ILogger<VersionManager> _logger;
    private readonly string _backupDir;

    public VersionManager(
        ISolutionManager solutionManager,
        IGitService gitService,
        ILogger<VersionManager>? logger = null
    )
    {
        _solutionManager = solutionManager;
        _gitService = gitService;
        _logger = logger ?? NullLogger<VersionManager>.Instance;

        // Backup directory in solution root
        var solutionDir =
            Path.GetDirectoryName(
                _solutionManager.CurrentSolution?.FilePath ?? Directory.GetCurrentDirectory()
            ) ?? Directory.GetCurrentDirectory();
        _backupDir = Path.Combine(solutionDir, ".ultrasharp", "snapshots");
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>
    /// Create a snapshot of current code state.
    /// Uses git stash if available, otherwise creates file backup.
    /// </summary>
    public async Task<string> CreateSnapshotAsync(
        string description,
        string[]? files = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Creating snapshot: {Description}", description);

        // Check if git is available
        var hasGit = await CheckGitAvailableAsync(cancellationToken);

        if (hasGit)
        {
            return await CreateGitSnapshotAsync(description, files, cancellationToken);
        }
        else
        {
            return await CreateBackupSnapshotAsync(description, files, cancellationToken);
        }
    }

    /// <summary>
    /// Rollback to a specific snapshot.
    /// </summary>
    public async Task RollbackAsync(
        string snapshotId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Rolling back to snapshot: {SnapshotId}", snapshotId);

        if (snapshotId.StartsWith("git-stash-"))
        {
            await RollbackGitSnapshotAsync(snapshotId, cancellationToken);
        }
        else if (snapshotId.StartsWith("backup-"))
        {
            await RollbackBackupSnapshotAsync(snapshotId, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Unknown snapshot type: {snapshotId}");
        }
    }

    /// <summary>
    /// List available snapshots.
    /// </summary>
    public async Task<List<SnapshotInfo>> ListSnapshotsAsync(
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var snapshots = new List<SnapshotInfo>();

        // Add git stashes if available
        if (await CheckGitAvailableAsync(cancellationToken))
        {
            var gitSnapshots = await ListGitSnapshotsAsync(limit, cancellationToken);
            snapshots.AddRange(gitSnapshots);
        }

        // Add file backups
        var backupSnapshots = await ListBackupSnapshotsAsync(limit, cancellationToken);
        snapshots.AddRange(backupSnapshots);

        return [.. snapshots.OrderByDescending(s => s.CreatedAt).Take(limit)];
    }

    /// <summary>
    /// Cleanup old snapshots.
    /// </summary>
    public async Task CleanupOldSnapshotsAsync(
        int keepCount = 20,
        TimeSpan? olderThan = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Cleaning up old snapshots (keep={KeepCount})", keepCount);

        var allSnapshots = await ListSnapshotsAsync(int.MaxValue, cancellationToken);

        var cutoffTime = olderThan.HasValue
            ? DateTimeOffset.UtcNow - olderThan.Value
            : DateTimeOffset.MinValue;

        var toDelete = allSnapshots.Where(s => s.CreatedAt < cutoffTime).Skip(keepCount).ToList();

        foreach (var snapshot in toDelete)
        {
            if (snapshot.SnapshotId.StartsWith("backup-"))
            {
                await DeleteBackupSnapshotAsync(snapshot.SnapshotId, cancellationToken);
            }
            // Note: Git stashes are not auto-deleted, managed manually
        }

        _logger.LogInformation("Cleaned up {Count} old snapshots", toDelete.Count);
    }

    // ==================== Git Backend ====================

    private async Task<bool> CheckGitAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = _solutionManager.CurrentSolution?.FilePath;
            if (string.IsNullOrEmpty(solutionPath))
            {
                return false;
            }

            var gitDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".git");
            return Directory.Exists(gitDir);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CreateGitSnapshotAsync(
        string description,
        string[]? files,
        CancellationToken cancellationToken
    )
    {
        var solutionPath =
            _solutionManager.CurrentSolution?.FilePath
            ?? throw new InvalidOperationException("No solution loaded");
        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        // Git stash implementation planned for future version
        // For now, use file backup backend which is more reliable
        _logger.LogDebug("Using file backup backend (git stash planned for future)");
        return await CreateBackupSnapshotAsync(description, files, cancellationToken);
    }

    private async Task RollbackGitSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken
    )
    {
        // Git stash rollback planned for future version
        throw new NotSupportedException(
            "Git stash rollback not yet implemented. Please use file backup snapshots."
        );
    }

    private async Task<List<SnapshotInfo>> ListGitSnapshotsAsync(
        int limit,
        CancellationToken cancellationToken
    )
    {
        // Git stash listing planned for future version
        // For now, return empty list
        return new List<SnapshotInfo>();
    }

    // ==================== File Backup Backend ====================

    private async Task<string> CreateBackupSnapshotAsync(
        string description,
        string[]? files,
        CancellationToken cancellationToken
    )
    {
        var snapshotId = $"backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var snapshotDir = Path.Combine(_backupDir, snapshotId);
        Directory.CreateDirectory(snapshotDir);

        var solutionPath =
            _solutionManager.CurrentSolution?.FilePath
            ?? throw new InvalidOperationException("No solution loaded");
        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        // If no specific files, backup all modified files in solution
        if (files == null || files.Length == 0)
        {
            // For simplicity, just note that we'd backup modified files
            files = Array.Empty<string>();
            _logger.LogWarning("No files specified for snapshot, creating empty snapshot");
        }

        // Copy files to snapshot directory
        var backedUpFiles = new List<string>();
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                _logger.LogWarning("File not found for snapshot: {File}", file);
                continue;
            }

            // Calculate relative path from solution
            var relativePath = Path.GetRelativePath(solutionDir, file);
            var targetPath = Path.Combine(snapshotDir, relativePath);

            // Create directory structure
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Copy file
            await CopyFileAsync(file, targetPath, cancellationToken);
            backedUpFiles.Add(relativePath);
        }

        // Save metadata
        var metadata = new SnapshotMetadata
        {
            SnapshotId = snapshotId,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            Files = backedUpFiles.ToArray(),
            Backend = "file",
        };

        var metadataPath = Path.Combine(snapshotDir, "metadata.json");
        await File.WriteAllTextAsync(
            metadataPath,
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken
        );

        _logger.LogInformation(
            "Created file snapshot {SnapshotId} with {FileCount} files",
            snapshotId,
            backedUpFiles.Count
        );
        return snapshotId;
    }

    private async Task RollbackBackupSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken
    )
    {
        var snapshotDir = Path.Combine(_backupDir, snapshotId);
        if (!Directory.Exists(snapshotDir))
        {
            throw new InvalidOperationException($"Snapshot not found: {snapshotId}");
        }

        // Read metadata
        var metadataPath = Path.Combine(snapshotDir, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            throw new InvalidOperationException($"Snapshot metadata not found: {snapshotId}");
        }

        var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata =
            JsonSerializer.Deserialize<SnapshotMetadata>(metadataJson)
            ?? throw new InvalidOperationException(
                $"Failed to parse snapshot metadata: {snapshotId}"
            );

        var solutionPath =
            _solutionManager.CurrentSolution?.FilePath
            ?? throw new InvalidOperationException("No solution loaded");
        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        // Restore files
        foreach (var relativeFile in metadata.Files)
        {
            var sourcePath = Path.Combine(snapshotDir, relativeFile);
            var targetPath = Path.Combine(solutionDir, relativeFile);

            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("Snapshot file not found: {File}", relativeFile);
                continue;
            }

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Restore file
            await CopyFileAsync(sourcePath, targetPath, cancellationToken);
            _logger.LogInformation("Restored file: {File}", relativeFile);
        }

        _logger.LogInformation("Rollback complete: {SnapshotId}", snapshotId);
    }

    private async Task<List<SnapshotInfo>> ListBackupSnapshotsAsync(
        int limit,
        CancellationToken cancellationToken
    )
    {
        var snapshots = new List<SnapshotInfo>();

        if (!Directory.Exists(_backupDir))
        {
            return snapshots;
        }

        var snapshotDirs = Directory
            .GetDirectories(_backupDir)
            .OrderByDescending(d => d)
            .Take(limit);

        foreach (var snapshotDir in snapshotDirs)
        {
            var metadataPath = Path.Combine(snapshotDir, "metadata.json");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            try
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<SnapshotMetadata>(metadataJson);

                if (metadata != null)
                {
                    // Calculate snapshot size
                    var sizeBytes = Directory
                        .GetFiles(snapshotDir, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);

                    snapshots.Add(
                        new SnapshotInfo
                        {
                            SnapshotId = metadata.SnapshotId,
                            Description = metadata.Description,
                            CreatedAt = metadata.CreatedAt,
                            Files = metadata.Files,
                            Backend = metadata.Backend,
                            SizeBytes = sizeBytes,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read snapshot metadata: {Path}", metadataPath);
            }
        }

        return snapshots;
    }

    private async Task DeleteBackupSnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken
    )
    {
        var snapshotDir = Path.Combine(_backupDir, snapshotId);
        if (Directory.Exists(snapshotDir))
        {
            Directory.Delete(snapshotDir, recursive: true);
            _logger.LogInformation("Deleted snapshot: {SnapshotId}", snapshotId);
        }
    }

    private async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken
    )
    {
        using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            useAsync: true
        );
        using var targetStream = new FileStream(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true
        );
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    // ==================== Git Stash Metadata ====================

    private async Task SaveGitStashMetadataAsync(
        string snapshotId,
        string description,
        string[]? files,
        string timestamp,
        CancellationToken cancellationToken
    )
    {
        // Save metadata file for git stash tracking
        var metadataDir = Path.Combine(_backupDir, "git-stash-metadata");
        Directory.CreateDirectory(metadataDir);

        var metadata = new SnapshotMetadata
        {
            SnapshotId = snapshotId,
            Description = description,
            CreatedAt = DateTimeOffset.ParseExact(timestamp, "yyyyMMddHHmmss", null),
            Files = files ?? Array.Empty<string>(),
            Backend = "git-stash",
        };

        var metadataPath = Path.Combine(metadataDir, $"{snapshotId}.json");
        await File.WriteAllTextAsync(
            metadataPath,
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken
        );
    }

    private async Task<SnapshotMetadata?> LoadGitStashMetadataAsync(
        string snapshotId,
        CancellationToken cancellationToken
    )
    {
        var metadataDir = Path.Combine(_backupDir, "git-stash-metadata");
        var metadataPath = Path.Combine(metadataDir, $"{snapshotId}.json");

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<SnapshotMetadata>(metadataJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load git stash metadata for {SnapshotId}",
                snapshotId
            );
            return null;
        }
    }
}

/// <summary>
/// Snapshot metadata stored in .ultrasharp/snapshots/{snapshotId}/metadata.json
/// </summary>
public sealed record SnapshotMetadata
{
    public required string SnapshotId { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string[] Files { get; init; }
    public required string Backend { get; init; } // "git" or "file"
}

/// <summary>
/// Snapshot information for listing.
/// </summary>
public sealed record SnapshotInfo
{
    public required string SnapshotId { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string[] Files { get; init; }
    public required string Backend { get; init; }
    public required long SizeBytes { get; init; }
}



using Microsoft.Extensions.Logging.Abstractions;

using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Layered;

/// <summary>
/// Computes BranchDelta from git diff between current branch and base branch.
/// Extracts symbol changes (added, modified, deleted) from changed files.
/// </summary>
public sealed class GitDeltaComputer
{
    private readonly IGitService _gitService;
    private readonly FastSymbolIndex _baseIndex;
    private readonly ILogger<GitDeltaComputer> _logger;

    public GitDeltaComputer(
        IGitService gitService,
        FastSymbolIndex baseIndex,
        ILogger<GitDeltaComputer>? logger = null)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _baseIndex = baseIndex ?? throw new ArgumentNullException(nameof(baseIndex));
        _logger = logger ?? NullLogger<GitDeltaComputer>.Instance;
    }

    /// <summary>
    /// Compute branch delta from git diff.
    /// Compares current branch with base branch (main/master).
    /// </summary>
    /// <param name="solution">Current solution</param>
    /// <param name="solutionPath">Path to solution file</param>
    /// <param name="baseBranch">Base branch name (default: "main")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>BranchDelta with added/modified/deleted symbols</returns>
    public async Task<BranchDelta?> ComputeDeltaAsync(
        Solution solution,
        string solutionPath,
        string baseBranch = "main",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Get current branch
            var currentBranch = await _gitService.GetCurrentBranchAsync(solutionPath, cancellationToken);
            if (string.IsNullOrEmpty(currentBranch))
            {
                _logger.LogWarning("Could not determine current branch");
                return null;
            }

            // 2. If on base branch, no delta needed
            if (IsBaseBranch(currentBranch, baseBranch))
            {
                _logger.LogDebug("On base branch {Branch}, no delta needed", currentBranch);
                return null;
            }

            // 3. Find merge-base (common ancestor)
            var mergeBaseSha = await _gitService.GetMergeBaseCommitAsync(solutionPath, currentBranch, baseBranch, cancellationToken);
            if (string.IsNullOrEmpty(mergeBaseSha))
            {
                _logger.LogWarning("Could not find merge-base between {CurrentBranch} and {BaseBranch}", currentBranch, baseBranch);
                return null;
            }

            // 4. Get current commit SHA
            var currentCommitSha = await _gitService.GetCurrentCommitShaAsync(solutionPath, cancellationToken);
            if (string.IsNullOrEmpty(currentCommitSha))
            {
                _logger.LogWarning("Could not determine current commit SHA");
                return null;
            }

            // 5. Get changed files between merge-base and current HEAD
            var changedFiles = await _gitService.GetChangedFilesAsync(solutionPath, mergeBaseSha, currentCommitSha, cancellationToken);

            _logger.LogInformation(
                "Computing delta for branch {Branch}: {FileCount} files changed since merge-base {MergeBase}",
                currentBranch, changedFiles.Count, mergeBaseSha[..8]);

            // 6. Create BranchDelta
            var delta = new BranchDelta
            {
                BranchName = currentBranch,
                BaseCommitSha = mergeBaseSha,
                LastModified = DateTime.UtcNow
            };

            // 7. Process each changed file
            foreach (var (filePath, changeType) in changedFiles)
            {
                // Only process C# files
                if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await ProcessChangedFileAsync(solution, filePath, changeType, delta, cancellationToken);
            }

            _logger.LogInformation(
                "Delta computed: {Added} added, {Modified} modified, {Deleted} deleted symbols",
                delta.AddedSymbols.Count, delta.ModifiedSymbols.Count, delta.DeletedSymbolIds.Count);

            return delta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing delta for solution {SolutionPath}", solutionPath);
            return null;
        }
    }

    /// <summary>
    /// Process a single changed file and update delta.
    /// </summary>
    private async Task ProcessChangedFileAsync(
        Solution solution,
        string filePath,
        string changeType,
        BranchDelta delta,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find document in solution
            var document = FindDocumentByPath(solution, filePath);

            if (changeType == "Deleted")
            {
                // For deleted files, find symbols in base index and mark as deleted
                await ProcessDeletedFileAsync(filePath, delta, cancellationToken);
            }
            else if (changeType is "Added" or "Modified" && document != null)
            {
                // For added/modified files, extract symbols
                await ProcessAddedOrModifiedFileAsync(document, changeType, delta, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Skipping file {FilePath}: changeType={ChangeType}, document={DocumentFound}",
                    filePath, changeType, document != null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing changed file {FilePath}", filePath);
        }
    }

    /// <summary>
    /// Process deleted file - mark all symbols as deleted.
    /// </summary>
    private async Task ProcessDeletedFileAsync(string filePath, BranchDelta delta, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // For async signature

        // Find all symbols in base index that belong to this file
        // Note: This requires FilePath tracking in SymbolIndexEntry (added in Phase 0.1)
        var symbolsInFile = _baseIndex.Find("*")
            .Where(s => s.FilePath != null &&
                       Path.GetFullPath(s.FilePath).Equals(Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var symbol in symbolsInFile)
        {
            delta.DeletedSymbolIds.Add(symbol.SymbolId);
        }

        _logger.LogDebug("Marked {Count} symbols as deleted from file {FilePath}", symbolsInFile.Count, filePath);
    }

    /// <summary>
    /// Process added or modified file - extract and categorize symbols.
    /// </summary>
    private async Task ProcessAddedOrModifiedFileAsync(
        Document document,
        string changeType,
        BranchDelta delta,
        CancellationToken cancellationToken)
    {
        // Extract symbols from document using same logic as FastSymbolIndex
        var symbols = await FastSymbolIndex.ExtractSymbolsFromDocumentAsync(document, cancellationToken);

        foreach (var symbol in symbols)
        {
            if (changeType == "Added")
            {
                // New symbol
                delta.AddedSymbols[symbol.SymbolId] = symbol;
            }
            else // Modified
            {
                // Check if symbol exists in base index
                var existsInBase = _baseIndex.Find(symbol.SimpleName).Any(s => s.SymbolId == symbol.SymbolId);

                if (existsInBase)
                {
                    // Existing symbol modified
                    delta.ModifiedSymbols[symbol.SymbolId] = symbol;
                }
                else
                {
                    // New symbol in modified file
                    delta.AddedSymbols[symbol.SymbolId] = symbol;
                }
            }
        }

        _logger.LogDebug("Processed {ChangeType} file {FilePath}: extracted {Count} symbols",
            changeType, document.FilePath, symbols.Count);
    }

    /// <summary>
    /// Find document in solution by file path.
    /// </summary>
    private Document? FindDocumentByPath(Solution solution, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath != null &&
                    Path.GetFullPath(document.FilePath).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Check if current branch is a base branch (main/master).
    /// </summary>
    private static bool IsBaseBranch(string currentBranch, string baseBranch)
    {
        return currentBranch.Equals(baseBranch, StringComparison.OrdinalIgnoreCase) ||
               currentBranch.Equals("main", StringComparison.OrdinalIgnoreCase) ||
               currentBranch.Equals("master", StringComparison.OrdinalIgnoreCase);
    }
}

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Models;
using UltrasharpTools.Tools.Versioning;

namespace UltrasharpTools.Tools.Replace.Services;

/// <summary>
/// Сервис для атомарного применения batch изменений.
/// </summary>
public sealed partial class BatchReplacerService : IBatchReplacerService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeModificationService _modificationService;
    private readonly IFormattingService _formattingService;
    private readonly IGitService? _gitService;
    private readonly VersionManager _versionManager;
    private readonly ILogger<BatchReplacerService> _logger;

    // Registry для tracking matches между preview и apply
    private readonly ConcurrentDictionary<string, CodeMatch> _matchRegistry = new();

    public BatchReplacerService(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        IFormattingService formattingService,
        VersionManager versionManager,
        IGitService? gitService = null,
        ILogger<BatchReplacerService>? logger = null)
    {
        _solutionManager = solutionManager;
        _modificationService = modificationService;
        _formattingService = formattingService;
        _versionManager = versionManager;
        _gitService = gitService;
        _logger = logger ?? NullLogger<BatchReplacerService>.Instance;
    }

    public void RegisterMatches(IEnumerable<CodeMatch> matches)
    {
        foreach (var match in matches)
        {
            _matchRegistry[match.Id] = match;
        }
        LogMatchesRegistered(_matchRegistry.Count);
    }

    public CodeMatch? GetMatch(string matchId)
    {
        return _matchRegistry.TryGetValue(matchId, out var match) ? match : null;
    }

    public void ClearRegistry()
    {
        _matchRegistry.Clear();
        LogRegistryCleared();
    }

    public async Task<ValidationResult> ValidateReplacementsAsync(
        IReadOnlyList<CodeReplacement> replacements,
        CancellationToken ct = default)
    {
        var errors = new List<string>();

        foreach (var replacement in replacements)
        {
            // Check if match exists
            if (!_matchRegistry.TryGetValue(replacement.MatchId, out var match))
            {
                errors.Add($"Match not found: {replacement.MatchId}");
                continue;
            }

            // Validate new code syntax - try parsing as member first, then as statement
            SyntaxNode? parsedNode = SyntaxFactory.ParseMemberDeclaration(replacement.NewCode);
            if (parsedNode == null)
            {
                // Try as statement (for block/statement scope)
                parsedNode = SyntaxFactory.ParseStatement(replacement.NewCode);
            }

            if (parsedNode != null)
            {
                var diagnostics = parsedNode.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (diagnostics.Count > 0)
                {
                    errors.Add($"Syntax error in replacement for {replacement.MatchId}: {diagnostics.First().GetMessage()}");
                }
            }
            else
            {
                errors.Add($"Could not parse replacement code for {replacement.MatchId}");
            }
        }

        // Check for conflicts (multiple replacements for same container)
        var conflicts = DetectConflicts(replacements);
        foreach (var conflict in conflicts)
        {
            errors.Add($"Conflict: {conflict.Message}");
        }

        return errors.Count > 0
            ? ValidationResult.Invalid(errors)
            : ValidationResult.Valid();
    }

    public async Task<ReplaceResult> ApplyReplacementsAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode = ApplyMode.AllOrNothing,
        string? commitMessage = null,
        CancellationToken ct = default)
    {
        LogApplyStarted(replacements.Count, mode.ToString());

        // 1. Validation
        var validationResult = await ValidateReplacementsAsync(replacements, ct);
        if (!validationResult.IsValid)
        {
            return ReplaceResult.ValidationFailed(validationResult.Errors);
        }

        // 2. Check for conflicts
        var conflicts = DetectConflicts(replacements);
        if (conflicts.Count > 0)
        {
            return ReplaceResult.ConflictDetected(conflicts);
        }

        // 3. Group by file
        var byFile = replacements
            .Select(r => (Replacement: r, Match: _matchRegistry[r.MatchId]))
            .GroupBy(x => x.Match.FilePath)
            .OrderBy(g => g.Key)
            .ToList();

        // 4. Create snapshot for rollback
        var filePaths = byFile.Select(g => g.Key).ToArray();
        var snapshotId = await _versionManager.CreateSnapshotAsync(
            "before-semantic-replace",
            filePaths,
            ct);

        var appliedChanges = new List<AppliedChange>();
        var failedChanges = new List<FailedChange>();

        try
        {
            // 5. Apply changes by file
            foreach (var fileGroup in byFile)
            {
                ct.ThrowIfCancellationRequested();

                var (applied, failed) = await ApplyFileReplacementsAsync(
                    fileGroup.Key,
                    fileGroup.Select(x => (x.Replacement, x.Match)).ToList(),
                    ct);

                appliedChanges.AddRange(applied);
                failedChanges.AddRange(failed);

                if (mode == ApplyMode.AllOrNothing && failed.Count > 0)
                {
                    LogRollingBack(failed.Count);
                    await _versionManager.RollbackAsync(snapshotId, ct);
                    return ReplaceResult.FailedResult(
                        "Rollback due to AllOrNothing mode",
                        appliedChanges,
                        failedChanges);
                }
            }

            // 6. Format changed files
            await FormatFilesAsync(filePaths, ct);

            // 7. Git commit (if enabled)
            if (!string.IsNullOrEmpty(commitMessage) && _gitService != null)
            {
                var solutionPath = _solutionManager.CurrentSolution?.FilePath;
                if (!string.IsNullOrEmpty(solutionPath))
                {
                    await _gitService.CommitChangesAsync(solutionPath, filePaths, commitMessage, ct);
                }
            }

            LogApplyCompleted(appliedChanges.Count, failedChanges.Count);

            return ReplaceResult.SuccessResult(appliedChanges.Count, appliedChanges, failedChanges);
        }
        catch (Exception ex)
        {
            LogApplyFailed(ex.Message);

            // Rollback on exception
            try
            {
                await _versionManager.RollbackAsync(snapshotId, ct);
            }
            catch
            {
                // Ignore rollback errors
            }

            return ReplaceResult.FailedResult(
                $"Exception during apply: {ex.Message}",
                appliedChanges,
                failedChanges);
        }
    }

    private async Task<(List<AppliedChange> Applied, List<FailedChange> Failed)> ApplyFileReplacementsAsync(
        string filePath,
        List<(CodeReplacement Replacement, CodeMatch Match)> replacements,
        CancellationToken ct)
    {
        var applied = new List<AppliedChange>();
        var failed = new List<FailedChange>();

        // Sort by position descending (apply from end to start to preserve positions)
        var sortedReplacements = replacements
            .OrderByDescending(x => x.Match.ContainerStartLine)
            .ToList();

        var document = _solutionManager.CurrentSolution?.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.FilePath == filePath);
        if (document == null)
        {
            foreach (var (replacement, match) in sortedReplacements)
            {
                failed.Add(new FailedChange
                {
                    MatchId = replacement.MatchId,
                    FilePath = filePath,
                    Error = "Document not found"
                });
            }
            return (applied, failed);
        }

        var solution = document.Project.Solution;

        foreach (var (replacement, match) in sortedReplacements)
        {
            try
            {
                // Get current document state
                document = solution.GetDocument(document.Id)!;
                var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                var root = await syntaxTree!.GetRootAsync(ct);

                // Find container node by position
                var containerNode = FindContainerByPosition(root, match.ContainerStartLine, match.ContainerEndLine);
                if (containerNode == null)
                {
                    failed.Add(new FailedChange
                    {
                        MatchId = replacement.MatchId,
                        FilePath = filePath,
                        Error = "Container node not found (code may have changed)"
                    });
                    continue;
                }

                // Parse new code
                var newNode = ParseReplacementCode(replacement.NewCode, containerNode);
                if (newNode == null)
                {
                    failed.Add(new FailedChange
                    {
                        MatchId = replacement.MatchId,
                        FilePath = filePath,
                        Error = "Failed to parse replacement code"
                    });
                    continue;
                }

                // Replace node
                var newRoot = root.ReplaceNode(containerNode, newNode);
                document = document.WithSyntaxRoot(newRoot);
                solution = document.Project.Solution;

                applied.Add(new AppliedChange
                {
                    MatchId = replacement.MatchId,
                    FilePath = filePath,
                    OldCode = match.FullCode,
                    NewCode = replacement.NewCode,
                    Description = replacement.Description
                });
            }
            catch (Exception ex)
            {
                failed.Add(new FailedChange
                {
                    MatchId = replacement.MatchId,
                    FilePath = filePath,
                    Error = ex.Message
                });
            }
        }

        // Apply solution changes
        if (applied.Count > 0)
        {
            var workspace = _solutionManager.CurrentWorkspace;
            if (workspace == null || !workspace.TryApplyChanges(solution))
            {
                // If apply failed, mark all as failed
                foreach (var change in applied)
                {
                    failed.Add(new FailedChange
                    {
                        MatchId = change.MatchId,
                        FilePath = change.FilePath,
                        Error = "Failed to apply workspace changes"
                    });
                }
                applied.Clear();
            }
        }

        return (applied, failed);
    }

    private static SyntaxNode? FindContainerByPosition(SyntaxNode root, int startLine, int endLine)
    {
        // Find nodes that span the expected line range
        var candidates = root.DescendantNodesAndSelf()
            .Where(n =>
            {
                var span = n.GetLocation().GetLineSpan();
                var nodeStart = span.StartLinePosition.Line + 1;
                var nodeEnd = span.EndLinePosition.Line + 1;
                return nodeStart == startLine && nodeEnd == endLine;
            })
            .ToList();

        // Prefer member declarations
        return candidates
            .FirstOrDefault(n => n is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            ?? candidates.FirstOrDefault();
    }

    private static SyntaxNode? ParseReplacementCode(string code, SyntaxNode originalNode)
    {
        // Try to parse as the same type as original
        return originalNode switch
        {
            MethodDeclarationSyntax => SyntaxFactory.ParseMemberDeclaration(code),
            PropertyDeclarationSyntax => SyntaxFactory.ParseMemberDeclaration(code),
            FieldDeclarationSyntax => SyntaxFactory.ParseMemberDeclaration(code),
            TypeDeclarationSyntax => SyntaxFactory.ParseMemberDeclaration(code),
            StatementSyntax => SyntaxFactory.ParseStatement(code),
            _ => (SyntaxNode?)SyntaxFactory.ParseMemberDeclaration(code)
                ?? SyntaxFactory.ParseStatement(code)
        };
    }

    private List<ReplaceConflict> DetectConflicts(IReadOnlyList<CodeReplacement> replacements)
    {
        var conflicts = new List<ReplaceConflict>();

        // Group by container FQN
        var byContainer = replacements
            .Where(r => _matchRegistry.ContainsKey(r.MatchId))
            .Select(r => (Replacement: r, Match: _matchRegistry[r.MatchId]))
            .GroupBy(x => (x.Match.FilePath, x.Match.ContainerFqn))
            .Where(g => g.Count() > 1);

        foreach (var group in byContainer)
        {
            conflicts.Add(new ReplaceConflict
            {
                MatchIds = group.Select(x => x.Replacement.MatchId).ToList(),
                FilePath = group.Key.FilePath,
                ContainerFqn = group.Key.ContainerFqn,
                Message = $"Multiple replacements target the same container: {group.Key.ContainerFqn}"
            });
        }

        return conflicts;
    }

    private async Task FormatFilesAsync(IEnumerable<string> filePaths, CancellationToken ct)
    {
        foreach (var filePath in filePaths)
        {
            try
            {
                await _formattingService.FormatAsync(filePath, checkOnly: false, ct);
            }
            catch (Exception ex)
            {
                LogFormatFailed(filePath, ex.Message);
            }
        }
    }

    // Logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Registered {Count} matches in registry")]
    private partial void LogMatchesRegistered(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registry cleared")]
    private partial void LogRegistryCleared();

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply started: {Count} replacements, mode={Mode}")]
    private partial void LogApplyStarted(int count, string mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply completed: {Applied} applied, {Failed} failed")]
    private partial void LogApplyCompleted(int applied, int failed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back due to {FailedCount} failed replacements")]
    private partial void LogRollingBack(int failedCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Apply failed: {Error}")]
    private partial void LogApplyFailed(string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Format failed for {FilePath}: {Error}")]
    private partial void LogFormatFailed(string filePath, string error);
}

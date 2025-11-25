using Microsoft.CodeAnalysis.CodeActions;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для применения автоматических исправлений кода
/// </summary>
public partial class CodeFixService(
    ILogger<CodeFixService> logger,
    ISolutionManager solutionManager,
    IGitService gitService
) : ICodeFixService
{
    private readonly ILogger<CodeFixService> _logger = logger;
    private readonly ISolutionManager _solutionManager = solutionManager;
    private readonly IGitService _gitService = gitService;

    public async Task<CodeFixResult> ApplyFixesAsync(
        string solutionPath,
        string diagnosticId,
        bool preview,
        CancellationToken cancellationToken = default
    )
    {
        LogStartingCodeFix(solutionPath, diagnosticId, preview);

        var appliedFixes = new List<string>();
        var errors = new List<(string Location, string Error)>();

        await _solutionManager.LoadSolutionAsync(solutionPath, cancellationToken);
        var solution = _solutionManager.CurrentSolution!;

        // ✅ OPTIMIZATION: Parallel project processing with Task.WhenAll
        var fixTasks = solution
            .Projects.Where(p => p.SupportsCompilation)
            .Select(async project =>
            {
                var projectFixes = new List<string>();
                try
                {
                    var compilation = await project.GetCompilationAsync(cancellationToken);
                    if (compilation == null)
                        return projectFixes;

                    var diagnostics = compilation
                        .GetDiagnostics()
                        .Where(d =>
                            !d.IsSuppressed && (diagnosticId == "all" || d.Id == diagnosticId)
                        )
                        .ToList();

                    foreach (var diagnostic in diagnostics)
                    {
                        if (diagnostic.Location.SourceTree == null)
                            continue;

                        var document = solution.GetDocument(diagnostic.Location.SourceTree);
                        if (document == null)
                            continue;

                        var actions = await GetCodeActionsAsync(
                            document,
                            diagnostic,
                            cancellationToken
                        );

                        if (actions.Count() > 0)
                        {
                            var lineSpan = diagnostic.Location.GetLineSpan();
                            var actionDescription =
                                $"Fix '{diagnostic.Id}' at {Path.GetFileName(diagnostic.Location.SourceTree.FilePath)}:{lineSpan.StartLinePosition.Line + 1}";
                            projectFixes.Add(actionDescription);

                            if (!preview)
                            {
                                try
                                {
                                    var firstAction = actions.First();
                                    var operations = await firstAction.GetOperationsAsync(
                                        cancellationToken
                                    );

                                    // Apply operations to solution
                                    foreach (var operation in operations)
                                    {
                                        if (operation is ApplyChangesOperation applyChangesOp)
                                        {
                                            // В реальном сценарии нужно применить изменения через workspace
                                            // Для упрощения пока только логируем
                                            LogWouldApplyFix(firstAction.Title);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogApplyFixFailed(ex, diagnostic.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogProcessProjectFailed(ex, project.Name);
                }

                return projectFixes;
            });

        // Wait for all projects to be processed in parallel
        var projectFixResults = await Task.WhenAll(fixTasks);
        var fixes = projectFixResults.SelectMany(x => x).ToList();

        LogCodeFixComplete(fixes.Count, preview);

        // Note: Git commit is not implemented yet because we need to track which files were actually modified
        // by the code fixes. This would require implementing ApplyChangesOperation properly.
        // For now, code fixes are detected but not applied automatically.

        return new CodeFixResult
        {
            AppliedFixes = fixes,
            TotalFixableIssues = fixes.Count,
            Errors = errors,
            WasPreview = preview,
        };
    }

    private async Task<IEnumerable<CodeAction>> GetCodeActionsAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken
    )
    {
        var actions = new List<CodeAction>();

        // IDE0005 - Remove unnecessary using
        if (diagnostic.Id == "IDE0005")
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root != null)
            {
                var usingDirective =
                    root.FindNode(diagnostic.Location.SourceSpan) as UsingDirectiveSyntax;
                if (usingDirective != null)
                {
                    actions.Add(
                        CodeAction.Create(
                            "Remove unused using",
                            ct =>
                                Task.FromResult(
                                    document.WithSyntaxRoot(
                                        root.RemoveNode(
                                            usingDirective,
                                            SyntaxRemoveOptions.KeepNoTrivia
                                        )!
                                    )
                                ),
                            "RemoveUnusedUsing"
                        )
                    );
                }
            }
        }

        // CS8019 - Unnecessary using directive
        if (diagnostic.Id == "CS8019")
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root != null)
            {
                var usingDirective =
                    root.FindNode(diagnostic.Location.SourceSpan) as UsingDirectiveSyntax;
                if (usingDirective != null)
                {
                    actions.Add(
                        CodeAction.Create(
                            "Remove unnecessary using",
                            ct =>
                                Task.FromResult(
                                    document.WithSyntaxRoot(
                                        root.RemoveNode(
                                            usingDirective,
                                            SyntaxRemoveOptions.KeepNoTrivia
                                        )!
                                    )
                                ),
                            "RemoveUnnecessaryUsing"
                        )
                    );
                }
            }
        }

        // Можно добавить больше code fixes для разных диагностик
        // IDE0001 - Simplify name
        // IDE0002 - Simplify member access
        // IDE0003/IDE0009 - this/Me qualification
        // IDE0004 - Cast is redundant
        // и т.д.

        return actions;
    }
}

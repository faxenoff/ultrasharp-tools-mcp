using ModelContextProtocol;
using UltrasharpTools.Tools.Merge.Git;

namespace UltrasharpTools.Tools.Mcp.Tools;
/// <summary>
/// MCP Tools для Semantic Merge.
/// </summary>
[McpServerToolType]
public sealed class SemanticMergeTools {
    private readonly BranchMergeService? _branchMergeService;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<SemanticMergeTools> _logger;

    public SemanticMergeTools(
    BranchMergeService? branchMergeService,
    ISolutionManager solutionManager,
    ILogger<SemanticMergeTools> logger) {
        _branchMergeService = branchMergeService;
        _solutionManager = solutionManager;
        _logger = logger;
    }

    /// <summary>
    /// Получить информацию о semantic merge.
    /// </summary>
    [McpServerTool]
    [Description("Get information about semantic merge capabilities and usage")]
    public Task<string> GetSemanticMergeInfo(CancellationToken cancellationToken = default) {
        return ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
        () => {
            var info =
    @"
=== Semantic Merge Info ===

Semantic Merge использует гибридную архитектуру:

1. Fast Path (90% случаев):
- Content hash matching (O(1))
- Structural fingerprint (AST hash, ignores whitespace/comments)
- Signature matching (FQN + parameters)
- ID matching (renamed symbols)

2. Slow Path (10% случаев):
- Vector embeddings для semantic similarity
- Обнаружение перемещений кода
- Структурное выравнивание

3. Features:
- Обнаружение code movements (между файлами, классами)
- Определение refactoring vs logic changes
- Сохранение control flow
- Поддержка C# и JSON файлов

4. Output:
- MergeActions (create/update/delete/move/rename)
- SemanticConflicts (требуют ручного разрешения)
- Детальная статистика

Использование:
semantic_merge(
sourceBranch: 'feature/caching',
targetBranch: 'main',
instructions: 'ignore swagger; prefer source for caching',
filePatterns: '*.cs',
apply: true
)

Поддерживаемые инструкции:
- 'ignore swagger' / 'swagger не мержить' - исключить swagger файлы
- 'skip appsettings' / 'исключить appsettings' - исключить конфиги
- 'prefer source for X' / 'X в приоритете на source' - брать X из source
- 'prefer target for X' - брать X из target
";

            return Task.FromResult(info);
        },
        _logger,
        "GetSemanticMergeInfo",
        cancellationToken
        );
    }

    /// <summary>
    /// AI-powered semantic merge of git branches with natural language instructions.
    /// </summary>
    [McpServerTool]
    [Description(
    "AI-powered semantic merge of git branches. Specify branch names and optional natural language instructions. " +
    "Automatically finds merge-base, reads files from branches, performs semantic 3-way merge, " +
    "and writes results as unstaged changes. Supports instructions like " +
    "'ignore swagger files' or 'prefer source for caching'."
    )]
    public async Task<string> SemanticMerge(
    [Description("Source branch name (where changes come from), e.g. 'feature/caching'")] string sourceBranch,
    [Description("Target branch name (where to merge), e.g. 'main'. Defaults to current branch.")] string? targetBranch = null,
    [Description("Natural language merge instructions, e.g. 'ignore swagger files; prefer source for caching'")] string? instructions = null,
    [Description("File patterns to merge (comma-separated), e.g. '*.cs,*.json'. Default: '*' (all files)")] string filePatterns = "*",
    [Description("Apply changes as unstaged files (true) or just preview (false)")] bool apply = true,
    CancellationToken cancellationToken = default) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
        async () => {
            if (_branchMergeService == null) {
                return "Error: BranchMergeService not available. SemanticMerge requires proper initialization.";
            }

            // Получить путь к репозиторию из загруженного solution
            var solutionPath = _solutionManager.CurrentSolution?.FilePath;
            if (string.IsNullOrEmpty(solutionPath)) {
                return "Error: No solution loaded. Use load_solution first.";
            }
            var repositoryPath = Path.GetDirectoryName(solutionPath)!;

            // Если target не указан, использовать текущую ветку
            if (string.IsNullOrEmpty(targetBranch)) {
                var gitReader = new GitBranchReader(repositoryPath, null);
                var currentBranch = await gitReader.GetCurrentBranchAsync(cancellationToken);
                _logger.LogInformation("GetCurrentBranchAsync returned: '{Branch}' for repo: {Repo}", currentBranch ?? "NULL", repositoryPath);
                targetBranch = currentBranch ?? "main";
            }

            var result = await _branchMergeService.MergeBranchesAsync(
    repositoryPath,
    sourceBranch,
    targetBranch,
    instructions,
    filePatterns,
    apply,
    cancellationToken);

            return FormatBranchMergeResult(result);
        },
        _logger,
        "SemanticMerge",
        cancellationToken);
    }

    private static string FormatBranchMergeResult(BranchMergeResult result) {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Semantic Branch Merge ===");
        sb.AppendLine();

        if (!result.Success) {
            sb.AppendLine($"❌ Error: {result.ErrorMessage}");
            return sb.ToString();
        }

        sb.AppendLine($"✅ {result.Summary}");
        sb.AppendLine();
        sb.AppendLine($"Merge: {result.SourceBranch} → {result.TargetBranch}");
        sb.AppendLine($"Base: {result.MergeBase}");
        sb.AppendLine();

        if (result.Instructions != null && !string.IsNullOrEmpty(result.Instructions.RawInstructions)) {
            sb.AppendLine($"📝 Instructions: {result.Instructions.RawInstructions}");
            if (result.Instructions.ExcludePatterns.Count > 0)
                sb.AppendLine($"   Excluded: {string.Join(", ", result.Instructions.ExcludePatterns)}");
            if (result.Instructions.PreferSourceKeywords.Count > 0)
                sb.AppendLine($"   Prefer source: {string.Join(", ", result.Instructions.PreferSourceKeywords)}");
            sb.AppendLine();
        }

        if (result.Statistics != null) {
            sb.AppendLine("📊 Statistics:");
            sb.AppendLine($"   Total changes: {result.Statistics.TotalChanges}");
            sb.AppendLine($"   Auto-merged: {result.Statistics.AutoMerged}");
            sb.AppendLine($"   Conflicts: {result.Statistics.Conflicts}");
            sb.AppendLine($"   Fast path: {result.Statistics.FastPathMatches}");
            sb.AppendLine($"   Slow path: {result.Statistics.SlowPathMatches}");
            sb.AppendLine($"   Time: {result.Statistics.MergeTimeMs}ms");
            sb.AppendLine();
        }

        if (result.Actions.Count > 0) {
            sb.AppendLine("📁 Actions:");
            foreach (var action in result.Actions.Take(20)) {
                sb.AppendLine($"   {action.ActionType}: {action.FilePath} ({action.Source}, {action.Confidence:P0})");
            }
            if (result.Actions.Count > 20)
                sb.AppendLine($"   ... and {result.Actions.Count - 20} more");
            sb.AppendLine();
        }

        if (result.Conflicts.Count > 0) {
            sb.AppendLine("⚠️ Conflicts (require manual resolution):");
            foreach (var conflict in result.Conflicts.Take(10)) {
                sb.AppendLine($"   {conflict.FilePath}: {conflict.Description}");
            }
            if (result.Conflicts.Count > 10)
                sb.AppendLine($"   ... and {result.Conflicts.Count - 10} more");
        }

        return sb.ToString();
    }
}
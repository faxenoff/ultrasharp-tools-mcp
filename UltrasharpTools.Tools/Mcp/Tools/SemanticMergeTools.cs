using ModelContextProtocol;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Merge.Git;

namespace UltrasharpTools.Tools.Mcp.Tools;
/// <summary>
/// MCP Tools для Semantic Merge.
/// </summary>
[McpServerToolType]
public sealed class SemanticMergeTools {
    private readonly BranchMergeService? _branchMergeService;
    private readonly ISolutionManager _solutionManager;
    private readonly ILoadingOrchestrator _loadingOrchestrator;
    private readonly ILogger<SemanticMergeTools> _logger;

    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromMinutes(5);

    public SemanticMergeTools(
    BranchMergeService? branchMergeService,
    ISolutionManager solutionManager,
    ILoadingOrchestrator loadingOrchestrator,
    ILogger<SemanticMergeTools> logger) {
        _branchMergeService = branchMergeService;
        _solutionManager = solutionManager;
        _loadingOrchestrator = loadingOrchestrator;
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
    [Description("Path to git repository. If not specified, uses loaded solution's directory.")] string? repositoryPath = null,
    CancellationToken cancellationToken = default) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
        async () => {
            // Ждём завершения инициализации (загрузка solution, индексов и т.д.)
            var status = _loadingOrchestrator.GetStatus();
            if (status.IsLoading) {
                _logger.LogInformation("[SemanticMerge] Waiting for solution loading to complete...");
                var isReady = await _loadingOrchestrator.WaitForReadyAsync(ReadinessTimeout, cancellationToken);
                if (!isReady) {
                    return "Error: Server is still initializing. Please wait a few seconds and try again.";
                }
            }

            if (_branchMergeService == null) {
                return "Error: BranchMergeService not available. SemanticMerge requires proper initialization.";
            }

            // Определить путь к репозиторию
            if (string.IsNullOrEmpty(repositoryPath)) {
                // Fallback: использовать путь из загруженного solution
                var solutionPath = _solutionManager.CurrentSolution?.FilePath;
                if (string.IsNullOrEmpty(solutionPath)) {
                    return "Error: No solution loaded and no repositoryPath specified. Either load_solution or provide repositoryPath.";
                }
                repositoryPath = Path.GetDirectoryName(solutionPath)!;
            }

            // Проверить что директория существует и содержит .git
            if (!Directory.Exists(repositoryPath)) {
                return $"Error: Repository path does not exist: {repositoryPath}";
            }
            if (!Directory.Exists(Path.Combine(repositoryPath, ".git"))) {
                return $"Error: Not a git repository (no .git folder): {repositoryPath}";
            }

            _logger.LogInformation("[MERGE] Using repository path: {Path}", repositoryPath);

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

        // Краткая сводка
        sb.AppendLine($"Merge: {result.SourceBranch} → {result.TargetBranch} (base: {result.MergeBase})");
        sb.AppendLine();

        // Инструкции (если есть)
        if (result.Instructions != null && !string.IsNullOrEmpty(result.Instructions.RawInstructions)) {
            sb.AppendLine($"📝 Instructions: {result.Instructions.RawInstructions}");
            sb.AppendLine();
        }

        // Статистика в читаемом формате
        if (result.Statistics != null) {
            var stats = result.Statistics;
            sb.AppendLine("📊 Statistics:");
            sb.AppendLine($"   Files to merge: {stats.FilesToMerge}");
            sb.AppendLine($"   Lines: +{stats.TotalInsertions} / -{stats.TotalDeletions}");
            sb.AppendLine($"   Auto-merged: {stats.AutoMergedFiles} files");
            if (stats.ConflictFiles > 0)
                sb.AppendLine($"   Conflicts: {stats.ConflictFiles} files");
            sb.AppendLine($"   Time: {stats.MergeTimeMs}ms");
            sb.AppendLine();
        }

        // Файлы с изменениями строк
        if (result.Actions.Count > 0 || result.Conflicts.Count > 0) {
            sb.AppendLine("📁 Files:");

            // Успешно смердженные файлы
            foreach (var action in result.Actions.Take(30)) {
                var lineInfo = action.Insertions > 0 || action.Deletions > 0
                    ? $" (+{action.Insertions}/-{action.Deletions})"
                    : "";
                sb.AppendLine($"   ✅ {action.FilePath}{lineInfo}");
            }
            if (result.Actions.Count > 30)
                sb.AppendLine($"   ... and {result.Actions.Count - 30} more files");

            // Конфликты
            foreach (var conflict in result.Conflicts.Take(10)) {
                sb.AppendLine($"   ⚠️ {conflict.FilePath} (conflict)");
            }
            if (result.Conflicts.Count > 10)
                sb.AppendLine($"   ... and {result.Conflicts.Count - 10} more conflicts");
        }

        sb.AppendLine();
        sb.AppendLine(result.Summary);

        return sb.ToString();
    }
}
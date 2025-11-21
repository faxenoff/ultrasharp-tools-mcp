using ModelContextProtocol;
using UltrasharpTools.Tools.Merge;
using UltrasharpTools.Tools.Merge.Indexing;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP Tools для Semantic Merge.
/// </summary>
public sealed class SemanticMergeTools
{
    private readonly SemanticMergeService _mergeService;
    private readonly ILogger<SemanticMergeTools> _logger;

    public SemanticMergeTools(SemanticMergeService mergeService, ILogger<SemanticMergeTools> logger)
    {
        _mergeService = mergeService;
        _logger = logger;
    }

    /// <summary>
    /// Выполнить семантический merge трёх веток (3-way merge).
    /// </summary>
    [McpServerTool]
    [Description(
        "Perform semantic 3-way merge of git branches using AI-powered code understanding. "
            + "Detects code movements, refactorings, and semantic equivalence beyond textual diffs."
    )]
    public async Task<string> SemanticMerge(
        [Description("Path to base (common ancestor) directory")] string baseDirectory,
        [Description("Path to branch A directory")] string branchADirectory,
        [Description("Path to branch B directory")] string branchBDirectory,
        [Description("File patterns to merge (e.g. '*.cs,*.json')")] string? filePatterns = null,
        [Description("Base commit SHA (optional)")] string? baseCommitSha = null,
        [Description("Branch A commit SHA (optional)")] string? branchACommitSha = null,
        [Description("Branch B commit SHA (optional)")] string? branchBCommitSha = null,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                var patterns = filePatterns?.Split(',') ?? new[] { "*.cs", "*.json" };

                var request = new IndexingRequest
                {
                    BaseDirectory = baseDirectory,
                    BranchADirectory = branchADirectory,
                    BranchBDirectory = branchBDirectory,
                    FilePatterns = patterns,
                    BaseCommitSha = baseCommitSha,
                    BranchACommitSha = branchACommitSha,
                    BranchBCommitSha = branchBCommitSha,
                };

                var result = await _mergeService.MergeAsync(request, cancellationToken);

                var summary = _mergeService.GetMergeSummary(result);

                return summary;
            },
            _logger,
            "SemanticMerge",
            cancellationToken
        );
    }

    /// <summary>
    /// Получить статус индексации для semantic merge.
    /// </summary>
    [McpServerTool]
    [Description("Get indexing statistics for semantic merge analysis")]
    public Task<string> GetSemanticMergeInfo(CancellationToken cancellationToken = default)
    {
        return ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            () =>
            {
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
UltrasharpTool_SemanticMerge(
    baseDirectory: '/path/to/base',
    branchADirectory: '/path/to/branchA',
    branchBDirectory: '/path/to/branchB',
    filePatterns: '*.cs,*.json'
)
";

                return Task.FromResult(info);
            },
            _logger,
            "GetSemanticMergeInfo",
            cancellationToken
        );
    }
}

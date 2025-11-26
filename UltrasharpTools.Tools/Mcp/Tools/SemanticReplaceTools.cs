using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP Tools для Semantic Replace - batch поиск и замена с полным контекстом.
/// </summary>
[McpServerToolType]
public sealed class SemanticReplaceTools
{
    private readonly ISemanticReplaceService _replaceService;
    private readonly ILogger<SemanticReplaceTools> _logger;

    public SemanticReplaceTools(
        ISemanticReplaceService replaceService,
        ILogger<SemanticReplaceTools> logger)
    {
        _replaceService = replaceService;
        _logger = logger;
    }

    /// <summary>
    /// Batch find and replace with full context extraction.
    /// </summary>
    [McpServerTool]
    [Description(@"Batch find and replace with full context extraction.

PREVIEW MODE (default):
- pattern: regex, FQN, or natural language query
- searchMode: 'regex' | 'roslyn' | 'semantic'
- scope: 'statement' | 'block' | 'member' | 'type' | 'file'
- Returns list of matches with full container code

APPLY MODE:
- replacements: JSON array of [{matchId, newCode}]
- applyMode: 'AllOrNothing' | 'BestEffort'

Examples:
1. Preview Console.WriteLine usages:
   semantic_replace(pattern: 'Console\\.WriteLine', scope: 'member')

2. Manual replace (after preview):
   semantic_replace(apply: true, replacements: '[{""matchId"":""sr-xxx"",""newCode"":""...""}]')")]
    public async Task<string> SemanticReplace(
        [Description("Pattern to search (regex, FQN, or natural language). Required for preview mode.")]
        string? pattern = null,

        [Description("Search mode: regex (text), roslyn (FQN/symbol), semantic (embeddings). Default: regex")]
        string searchMode = "regex",

        [Description("Context scope: statement, block, member, type, file. Default: member")]
        string scope = "member",

        [Description("File pattern filter (glob), e.g. '**/*.cs', 'Services/*.cs'")]
        string? filePattern = null,

        [Description("Namespace filter, e.g. 'MyApp.Services.*'")]
        string? namespaceFilter = null,

        [Description("Max results to return. Default: 100")]
        int limit = 100,

        [Description("JSON array of replacements: [{\"matchId\": \"sr-xxx\", \"newCode\": \"...\"}]")]
        string? replacements = null,

        [Description("Apply changes (false = preview only). Default: false")]
        bool apply = false,

        [Description("Apply mode: AllOrNothing (rollback on any error), BestEffort (apply successful). Default: AllOrNothing")]
        string applyMode = "AllOrNothing",

        [Description("Git commit message (optional, only used when apply=true)")]
        string? commitMessage = null,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                // Parse enums
                if (!Enum.TryParse<SearchMode>(searchMode, ignoreCase: true, out var searchModeEnum))
                    throw new ArgumentException($"Invalid searchMode: {searchMode}. Valid values: regex, roslyn, semantic");

                if (!Enum.TryParse<ReplaceScope>(scope, ignoreCase: true, out var scopeEnum))
                    throw new ArgumentException($"Invalid scope: {scope}. Valid values: statement, block, member, type, file");

                if (!Enum.TryParse<ApplyMode>(applyMode, ignoreCase: true, out var applyModeEnum))
                    throw new ArgumentException($"Invalid applyMode: {applyMode}. Valid values: AllOrNothing, BestEffort");

                // APPLY MODE
                if (apply)
                {
                    if (string.IsNullOrEmpty(replacements))
                        throw new ArgumentException("For apply mode, 'replacements' parameter is required");

                    var replacementList = JsonSerializer.Deserialize<List<CodeReplacement>>(replacements, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? throw new ArgumentException("Invalid replacements JSON");

                    var result = await _replaceService.ApplyAsync(
                        replacementList,
                        applyModeEnum,
                        commitMessage,
                        cancellationToken);

                    return FormatApplyResult(result);
                }

                // PREVIEW MODE
                if (string.IsNullOrEmpty(pattern))
                    throw new ArgumentException("For preview mode, 'pattern' parameter is required");

                var preview = await _replaceService.PreviewAsync(
                    pattern,
                    searchModeEnum,
                    scopeEnum,
                    filePattern,
                    namespaceFilter,
                    limit,
                    offset: 0,
                    cancellationToken);

                return FormatPreviewResult(preview);
            },
            _logger,
            "semantic_replace",
            cancellationToken);
    }

    /// <summary>
    /// Get information about semantic replace capabilities.
    /// </summary>
    [McpServerTool]
    [Description("Get information about semantic_replace capabilities and usage examples")]
    public Task<string> GetSemanticReplaceInfo(CancellationToken cancellationToken = default)
    {
        return ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            () =>
            {
                var info = @"
=== Semantic Replace Info ===

Batch find and replace with full context extraction.
Returns complete code containers (methods, classes) for each match.

## Search Modes

1. **regex** (default) - Text pattern matching
   - pattern: 'Console\\.WriteLine\\('
   - Finds all text matches

2. **roslyn** - Semantic symbol search
   - pattern: 'MyClass.MyMethod' (FQN)
   - Uses Roslyn for accurate symbol resolution

3. **semantic** - AI-powered similarity search
   - pattern: 'logging statements' (natural language)
   - Requires semantic mode enabled

## Scope Levels

- **statement** - Only the matched statement
- **block** - Containing block (if/while/try)
- **member** (default) - Full method/property
- **type** - Full class/struct/interface
- **file** - Entire file

## Workflow

### Step 1: Preview
```
semantic_replace(
  pattern: 'Console\\.WriteLine',
  scope: 'member',
  filePattern: '**/*.cs'
)
```
Returns list of matches with IDs and full code.

### Step 2: Apply
```
semantic_replace(
  apply: true,
  replacements: '[{""matchId"":""sr-abc123"",""newCode"":""public void Method() { _logger.LogInfo(...); }""}]',
  commitMessage: 'Replace Console.WriteLine with ILogger'
)
```

## Apply Modes

- **AllOrNothing** (default) - Rollback all changes if any fails
- **BestEffort** - Apply successful changes, skip failed

## Tips

1. Use `scope: 'member'` to see full method context
2. Preview first, then selectively apply changes
3. Use `filePattern` to limit search scope
4. Match IDs persist between preview and apply
";
                return Task.FromResult(info);
            },
            _logger,
            "GetSemanticReplaceInfo",
            cancellationToken);
    }

    private static string FormatPreviewResult(SemanticReplacePreviewResult preview)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Found {preview.TotalMatches} matches");
        sb.AppendLine();

        foreach (var match in preview.Matches)
        {
            sb.AppendLine($"### `{match.Id}`");
            sb.AppendLine($"- **FQN**: `{match.ContainerFqn}`");
            sb.AppendLine($"- **File**: `{match.FilePath}:{match.MatchLine}`");
            sb.AppendLine($"- **Match**: `{match.MatchFragment}`");
            sb.AppendLine($"- **Type**: {match.ContainerType}");

            if (match.SemanticSimilarity.HasValue)
            {
                sb.AppendLine($"- **Similarity**: {match.SemanticSimilarity:P0}");
            }

            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine(match.FullCode);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (preview.HasMore)
        {
            sb.AppendLine($"*Showing {preview.Matches.Count} of {preview.TotalMatches}. Use offset/limit for pagination.*");
        }

        sb.AppendLine();
        sb.AppendLine("**To apply changes:**");
        sb.AppendLine("```");
        sb.AppendLine("semantic_replace(");
        sb.AppendLine("  apply: true,");
        sb.AppendLine("  replacements: '[{\"matchId\":\"sr-xxx\",\"newCode\":\"...\"}]',");
        sb.AppendLine("  commitMessage: 'Your commit message'");
        sb.AppendLine(")");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string FormatApplyResult(ReplaceResult result)
    {
        var sb = new StringBuilder();

        if (result.Success)
        {
            sb.AppendLine($"## ✅ Successfully applied {result.Applied} changes");
        }
        else
        {
            sb.AppendLine($"## ❌ Failed: {result.Error}");
        }

        if (result.Failed > 0)
        {
            sb.AppendLine($"⚠️ {result.Failed} changes failed");
        }

        sb.AppendLine();

        // Applied changes
        if (result.Changes.Count > 0)
        {
            sb.AppendLine("### Applied:");
            foreach (var change in result.Changes.Take(10))
            {
                sb.AppendLine($"- ✅ `{change.MatchId}` in `{change.FilePath}`");
                if (!string.IsNullOrEmpty(change.Description))
                {
                    sb.AppendLine($"  {change.Description}");
                }
            }
            if (result.Changes.Count > 10)
            {
                sb.AppendLine($"  ... and {result.Changes.Count - 10} more");
            }
        }

        // Failed changes
        if (result.Failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Failed:");
            foreach (var failure in result.Failures.Take(10))
            {
                sb.AppendLine($"- ❌ `{failure.MatchId}`: {failure.Error}");
            }
        }

        // Conflicts
        if (result.Conflicts?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Conflicts:");
            foreach (var conflict in result.Conflicts)
            {
                sb.AppendLine($"- ⚠️ {conflict.Message}");
                sb.AppendLine($"  Matches: {string.Join(", ", conflict.MatchIds)}");
            }
        }

        // Compilation errors
        if (result.CompilationErrors?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Compilation Errors (rolled back):");
            foreach (var error in result.CompilationErrors.Take(5))
            {
                sb.AppendLine($"- {error}");
            }
        }

        return sb.ToString();
    }
}

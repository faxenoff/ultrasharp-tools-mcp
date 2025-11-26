using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Models;

namespace UltrasharpTools.Tools.Replace.Services;

/// <summary>
/// Главный сервис для semantic replace операций.
/// Объединяет поиск, извлечение контекста и применение изменений.
/// </summary>
public sealed partial class SemanticReplaceService : ISemanticReplaceService
{
    private readonly IPatternMatcherService _patternMatcher;
    private readonly IContextExtractorService _contextExtractor;
    private readonly IBatchReplacerService _batchReplacer;
    private readonly ILogger<SemanticReplaceService> _logger;

    public SemanticReplaceService(
        IPatternMatcherService patternMatcher,
        IContextExtractorService contextExtractor,
        IBatchReplacerService batchReplacer,
        ILogger<SemanticReplaceService>? logger = null)
    {
        _patternMatcher = patternMatcher;
        _contextExtractor = contextExtractor;
        _batchReplacer = batchReplacer;
        _logger = logger ?? NullLogger<SemanticReplaceService>.Instance;
    }

    /// <summary>
    /// Semantic transform requires LLM client which is not implemented yet.
    /// </summary>
    public bool IsSemanticTransformAvailable => false;

    public async Task<SemanticReplacePreviewResult> PreviewAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string? filePattern = null,
        string? namespaceFilter = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        LogPreviewStarted(pattern, searchMode.ToString(), scope.ToString());

        // 1. Find pattern matches
        var patternMatches = await _patternMatcher.FindMatchesAsync(
            pattern,
            searchMode,
            filePattern,
            namespaceFilter,
            limit + offset, // Get extra for offset
            ct);

        // 2. Apply offset
        var matchesToProcess = patternMatches
            .Skip(offset)
            .Take(limit)
            .ToList();

        // 3. Extract context for each match
        var codeMatches = await _contextExtractor.ExtractContextBatchAsync(
            matchesToProcess,
            scope,
            ct);

        // 4. Register matches for later apply
        _batchReplacer.RegisterMatches(codeMatches);

        LogPreviewCompleted(codeMatches.Count, patternMatches.Count);

        return new SemanticReplacePreviewResult
        {
            Matches = codeMatches,
            TotalMatches = patternMatches.Count,
            NextOffset = offset + codeMatches.Count
        };
    }

    public async Task<ReplaceResult> ApplyAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode = ApplyMode.AllOrNothing,
        string? commitMessage = null,
        CancellationToken ct = default)
    {
        LogApplyStarted(replacements.Count, mode.ToString());

        var result = await _batchReplacer.ApplyReplacementsAsync(
            replacements,
            mode,
            commitMessage,
            ct);

        if (result.Success)
        {
            LogApplyCompleted(result.Applied);
        }
        else
        {
            LogApplyFailed(result.Error ?? "Unknown error");
        }

        return result;
    }

    public Task<ReplaceResult> TransformAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string transformation,
        string? filePattern = null,
        string? namespaceFilter = null,
        string? commitMessage = null,
        CancellationToken ct = default)
    {
        // Semantic transform requires LLM integration
        // This is Phase 6 (optional) - not implemented yet
        throw new NotImplementedException(
            "Semantic transformation requires LLM client. " +
            "Use PreviewAsync + ApplyAsync for manual transformations.");
    }

    // Logging
    [LoggerMessage(Level = LogLevel.Information, Message = "Preview started: pattern='{Pattern}', mode={Mode}, scope={Scope}")]
    private partial void LogPreviewStarted(string pattern, string mode, string scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "Preview completed: {MatchCount} matches extracted from {TotalFound} found")]
    private partial void LogPreviewCompleted(int matchCount, int totalFound);

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply started: {Count} replacements, mode={Mode}")]
    private partial void LogApplyStarted(int count, string mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply completed: {Applied} changes applied")]
    private partial void LogApplyCompleted(int applied);

    [LoggerMessage(Level = LogLevel.Error, Message = "Apply failed: {Error}")]
    private partial void LogApplyFailed(string error);
}

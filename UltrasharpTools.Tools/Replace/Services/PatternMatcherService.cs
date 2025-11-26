using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Replace.Services;

/// <summary>
/// Сервис для поиска паттернов в коде.
/// Поддерживает три режима: Regex, Roslyn (FQN), Semantic (embeddings).
/// </summary>
public sealed partial class PatternMatcherService : IPatternMatcherService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ISemanticSimilarityService? _semanticSimilarityService;
    private readonly ISemanticSearchService? _semanticSearchService;
    private readonly ILogger<PatternMatcherService> _logger;

    public PatternMatcherService(
        ISolutionManager solutionManager,
        ISemanticSimilarityService? semanticSimilarityService = null,
        ISemanticSearchService? semanticSearchService = null,
        ILogger<PatternMatcherService>? logger = null)
    {
        _solutionManager = solutionManager;
        _semanticSimilarityService = semanticSimilarityService;
        _semanticSearchService = semanticSearchService;
        _logger = logger ?? NullLogger<PatternMatcherService>.Instance;
    }

    public async Task<IReadOnlyList<PatternMatch>> FindMatchesAsync(
        string pattern,
        SearchMode mode,
        string? filePattern = null,
        string? namespaceFilter = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        LogSearchStarted(pattern, mode.ToString(), limit);

        var result = mode switch
        {
            SearchMode.Regex => await FindRegexMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            SearchMode.Roslyn => await FindRoslynMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            SearchMode.Semantic => await FindSemanticMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown search mode")
        };

        LogSearchCompleted(result.Count, mode.ToString());
        return result;
    }

    private async Task<IReadOnlyList<PatternMatch>> FindRegexMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline);
        var matches = new List<PatternMatch>();

        var solution = _solutionManager.CurrentSolution
            ?? throw new InvalidOperationException("No solution loaded");

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                ct.ThrowIfCancellationRequested();

                if (document.FilePath == null)
                    continue;

                if (!MatchesFilePattern(document.FilePath, filePattern))
                    continue;

                var text = await document.GetTextAsync(ct);
                var content = text.ToString();

                foreach (Match regexMatch in regex.Matches(content))
                {
                    var linePosition = text.Lines.GetLinePosition(regexMatch.Index);

                    // Namespace filter check
                    if (!string.IsNullOrEmpty(namespaceFilter))
                    {
                        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                        if (syntaxTree != null)
                        {
                            var root = await syntaxTree.GetRootAsync(ct);
                            var token = root.FindToken(regexMatch.Index);
                            if (!IsInNamespace(token.Parent, namespaceFilter))
                                continue;
                        }
                    }

                    matches.Add(new PatternMatch
                    {
                        DocumentId = document.Id,
                        FilePath = document.FilePath,
                        StartPosition = regexMatch.Index,
                        Length = regexMatch.Length,
                        Line = linePosition.Line + 1,
                        Column = linePosition.Character + 1,
                        MatchedText = regexMatch.Value
                    });

                    if (matches.Count >= limit)
                        return matches;
                }
            }
        }

        return matches;
    }

    private async Task<IReadOnlyList<PatternMatch>> FindRoslynMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        var matches = new List<PatternMatch>();

        var solution = _solutionManager.CurrentSolution
            ?? throw new InvalidOperationException("No solution loaded");

        // Try to find symbol by FQN
        var symbol = await _solutionManager.FindRoslynSymbolAsync(pattern, ct);
        if (symbol == null)
        {
            LogSymbolNotFound(pattern);
            return matches;
        }

        // Apply namespace filter
        if (!string.IsNullOrEmpty(namespaceFilter))
        {
            var symbolNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            if (!symbolNamespace.StartsWith(namespaceFilter.TrimEnd('*', '.')))
                return matches;
        }

        foreach (var location in symbol.Locations)
        {
            ct.ThrowIfCancellationRequested();

            if (!location.IsInSource || location.SourceTree == null)
                continue;

            var filePath = location.SourceTree.FilePath;
            if (!MatchesFilePattern(filePath, filePattern))
                continue;

            var document = solution.GetDocument(location.SourceTree);
            if (document == null)
                continue;

            var span = location.GetLineSpan();

            matches.Add(new PatternMatch
            {
                DocumentId = document.Id,
                FilePath = filePath,
                StartPosition = location.SourceSpan.Start,
                Length = location.SourceSpan.Length,
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                MatchedText = symbol.Name,
                Symbol = symbol
            });

            if (matches.Count >= limit)
                return matches;
        }

        return matches;
    }

    private async Task<IReadOnlyList<PatternMatch>> FindSemanticMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        // Check if semantic search is available
        if (_semanticSearchService == null || !_semanticSearchService.IsAvailable)
        {
            throw new InvalidOperationException(
                "Semantic search mode requires SemanticSearchService to be enabled. " +
                "Configure semantic embedding provider (TEI/Ollama) or use 'regex'/'roslyn' search mode instead.");
        }

        var matches = new List<PatternMatch>();
        var solution = _solutionManager.CurrentSolution
            ?? throw new InvalidOperationException("No solution loaded");

        // Auto-index if not indexed yet
        if (!_semanticSearchService.IsIndexed())
        {
            _logger.LogInformation("Solution not indexed for semantic search, indexing now...");
            await _semanticSearchService.IndexCurrentSolutionAsync(ct);
        }

        // Use semantic search to find similar code
        var semanticMatches = await _semanticSearchService.FindSimilarCodeAsync(
            pattern,
            topK: limit * 2, // Get more to filter
            minSimilarity: 0.5f,
            ct);

        foreach (var semanticMatch in semanticMatches)
        {
            ct.ThrowIfCancellationRequested();

            // Extract FQN from semantic match (format: "method:Namespace.Class::Method" or "class:Namespace.Class")
            var fqn = semanticMatch.FullyQualifiedName ?? semanticMatch.Id;
            if (string.IsNullOrEmpty(fqn))
                continue;

            // Parse FQN to extract actual symbol name (remove "method:" or "class:" prefix)
            var cleanFqn = fqn;
            if (fqn.StartsWith("method:", StringComparison.OrdinalIgnoreCase))
                cleanFqn = fqn.Substring(7).Replace("::", ".");
            else if (fqn.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
                cleanFqn = fqn.Substring(6);

            // Apply namespace filter
            if (!string.IsNullOrEmpty(namespaceFilter))
            {
                var filterPrefix = namespaceFilter.TrimEnd('*', '.');
                if (!cleanFqn.StartsWith(filterPrefix, StringComparison.Ordinal))
                    continue;
            }

            // Find symbol by FQN to get actual file location
            var symbol = await _solutionManager.FindRoslynSymbolAsync(cleanFqn, ct);
            if (symbol == null)
            {
                // Try without last segment (method name) for nested types
                var lastDot = cleanFqn.LastIndexOf('.');
                if (lastDot > 0)
                    symbol = await _solutionManager.FindRoslynSymbolAsync(cleanFqn[..lastDot], ct);
            }

            if (symbol == null)
                continue;

            // Get first source location
            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
            if (location?.SourceTree == null)
                continue;

            var filePath = location.SourceTree.FilePath;

            // Apply file pattern filter
            if (!MatchesFilePattern(filePath, filePattern))
                continue;

            // Find document
            var document = solution.GetDocument(location.SourceTree);
            if (document == null)
                continue;

            var lineSpan = location.GetLineSpan();

            matches.Add(new PatternMatch
            {
                DocumentId = document.Id,
                FilePath = filePath,
                StartPosition = location.SourceSpan.Start,
                Length = location.SourceSpan.Length,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                MatchedText = semanticMatch.Code.Length > 100
                    ? semanticMatch.Code[..100] + "..."
                    : semanticMatch.Code,
                SemanticSimilarity = semanticMatch.Similarity,
                SemanticFqn = cleanFqn
            });

            if (matches.Count >= limit)
                break;
        }

        return matches;
    }

    private static bool MatchesFilePattern(string? filePath, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(filePath))
            return true;

        // Simple glob matching
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/\\\\]*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(filePath, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsInNamespace(SyntaxNode? node, string namespaceFilter)
    {
        if (node == null || string.IsNullOrEmpty(namespaceFilter))
            return true;

        var current = node;
        while (current != null)
        {
            if (current is Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax namespaceDecl)
            {
                var ns = namespaceDecl.Name.ToString();
                var filterPrefix = namespaceFilter.TrimEnd('*', '.');
                return ns.StartsWith(filterPrefix, StringComparison.Ordinal);
            }
            current = current.Parent;
        }

        return true; // No namespace = global, allow
    }

    // Logging methods
    [LoggerMessage(Level = LogLevel.Information, Message = "Pattern search started: pattern='{Pattern}', mode={Mode}, limit={Limit}")]
    private partial void LogSearchStarted(string pattern, string mode, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pattern search completed: found {Count} matches, mode={Mode}")]
    private partial void LogSearchCompleted(int count, string mode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Symbol not found by FQN: {Pattern}")]
    private partial void LogSymbolNotFound(string pattern);
}

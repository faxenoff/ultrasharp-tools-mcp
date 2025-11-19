using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using System.Text.RegularExpressions;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for advanced pattern search across codebase.
/// Sprint 11: pattern_search with 4 modes (Entity, Content, Semantic, Hybrid)
/// </summary>
public class PatternSearchToolsLogCategory { }

[McpServerToolType]
public static partial class PatternSearchTools
{
    /// <summary>
    /// Advanced pattern search with 4 modes: entity (name/type), content (method bodies), semantic (ML), hybrid (combined).
    /// </summary>
    [McpServerTool(Name = "pattern_search", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Advanced search with 4 modes: 'entity' (by name/type using regex), 'content' (inside method bodies), " +
                 "'semantic' (ML-powered similarity), 'hybrid' (combines all with intelligent ranking). " +
                 "Entity mode uses Roslyn symbols, Content mode searches code text, Semantic uses embeddings, " +
                 "Hybrid combines and ranks results by relevance.")]
    public static async Task<object> PatternSearch(
        ISolutionManager solutionManager,
        SemanticSearchService? semanticService,
        ILogger<PatternSearchToolsLogCategory> logger,

        [Description("Search pattern (regex for entity/content, natural language for semantic/hybrid)")]
        string pattern,

        [Description("Search mode: 'entity', 'content', 'semantic', or 'hybrid' (default). If semantic mode is not available, defaults to 'entity'.")]
        string mode = "hybrid",

        [Description("Entity type filter (class, interface, method, property, field, enum, etc.)")]
        string[]? entityTypes = null,

        [Description("Namespace filter (e.g., 'MyApp.Services')")]
        string? namespaceFilter = null,

        [Description("Maximum results to return (1-100, default: 20)")]
        int limit = 20,

        [Description("Minimum similarity for semantic/hybrid modes (0.0-1.0, default: 0.7)")]
        float minSimilarity = 0.7f,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            ErrorHandlingHelpers.ValidateStringParameter(pattern, nameof(pattern), logger);
            await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(PatternSearch), cancellationToken);

            if (limit < 1 || limit > 100)
            {
                throw new McpException("limit must be between 1 and 100");
            }

            if (minSimilarity < 0.0f || minSimilarity > 1.0f)
            {
                throw new McpException("minSimilarity must be between 0.0 and 1.0");
            }

            // Fallback to entity mode if hybrid/semantic requested but semantic service unavailable
            var effectiveMode = mode;
            if ((mode.Equals("hybrid", StringComparison.OrdinalIgnoreCase) ||
                 mode.Equals("semantic", StringComparison.OrdinalIgnoreCase)) &&
                semanticService == null)
            {
                effectiveMode = "entity";
                logger.LogInformation("Semantic mode not available, falling back to entity mode");
            }

            logger.LogInformation("Pattern search: mode={Mode} (requested: {RequestedMode}), pattern='{Pattern}', limit={Limit}",
                effectiveMode, mode, pattern, limit);

            var solution = solutionManager.CurrentWorkspace!.CurrentSolution;

            switch (effectiveMode.ToLowerInvariant())
            {
                case "entity":
                    return await SearchEntityMode(solution, pattern, entityTypes, namespaceFilter, limit, logger, cancellationToken);

                case "content":
                    return await SearchContentMode(solution, pattern, entityTypes, namespaceFilter, limit, logger, cancellationToken);

                case "semantic":
                    // semantic mode - semanticService guaranteed not null due to fallback logic above
                    return await SearchSemanticMode(semanticService!, pattern, entityTypes, limit, minSimilarity, logger, cancellationToken);

                case "hybrid":
                    // hybrid mode - semanticService guaranteed not null due to fallback logic above
                    return await SearchHybridMode(solution, semanticService!, pattern, entityTypes, namespaceFilter, limit, minSimilarity, logger, cancellationToken);

                default:
                    throw new McpException($"Unknown search mode: {effectiveMode}. Use 'entity', 'content', 'semantic', or 'hybrid'.");
            }

        }, logger, nameof(PatternSearch), cancellationToken);
    }

    // ==================== Entity Mode ====================

    private static async Task<object> SearchEntityMode(
        Solution solution,
        string pattern,
        string[]? entityTypes,
        string? namespaceFilter,
        int limit,
        ILogger<PatternSearchToolsLogCategory> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Entity mode search: pattern='{Pattern}'", pattern);

        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var results = new List<EntityMatch>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null) continue;

            foreach (var document in project.Documents)
            {
                if (!document.SupportsSyntaxTree) continue;

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (semanticModel == null || root == null) continue;

                // Find all named declarations
                var declarations = root.DescendantNodes()
                    .Where(n => n is BaseTypeDeclarationSyntax || n is MemberDeclarationSyntax)
                    .ToList();

                foreach (var decl in declarations)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(decl, cancellationToken);
                    if (symbol == null) continue;

                    // Filter by entity type
                    if (entityTypes != null && entityTypes.Length > 0)
                    {
                        if (!MatchesEntityType(symbol, entityTypes))
                            continue;
                    }

                    // Filter by namespace
                    if (!string.IsNullOrEmpty(namespaceFilter))
                    {
                        var containingNamespace = symbol.ContainingNamespace?.ToDisplayString();
                        if (containingNamespace == null || !containingNamespace.Contains(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // Match pattern
                    if (regex.IsMatch(symbol.Name))
                    {
                        results.Add(new EntityMatch
                        {
                            Name = symbol.Name,
                            FullyQualifiedName = symbol.ToDisplayString(),
                            Kind = symbol.Kind.ToString(),
                            FilePath = document.FilePath ?? "",
                            LineNumber = symbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0,
                            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "",
                            Relevance = CalculateEntityRelevance(symbol.Name, pattern)
                        });

                        if (results.Count >= limit * 2) break;
                    }
                }

                if (results.Count >= limit * 2) break;
            }

            if (results.Count >= limit * 2) break;
        }

        var topResults = results
            .OrderByDescending(r => r.Relevance)
            .ThenBy(r => r.Name)
            .Take(limit)
            .ToList();

        return ToolHelpers.ToJson(new
        {
            mode = "entity",
            pattern,
            totalFound = results.Count,
            returned = topResults.Count,
            results = topResults.Select(r => new
            {
                r.Name,
                r.FullyQualifiedName,
                r.Kind,
                r.FilePath,
                r.LineNumber,
                r.Namespace,
                relevance = $"{r.Relevance:F2}"
            })
        });
    }

    // ==================== Content Mode ====================

    private static async Task<object> SearchContentMode(
        Solution solution,
        string pattern,
        string[]? entityTypes,
        string? namespaceFilter,
        int limit,
        ILogger<PatternSearchToolsLogCategory> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Content mode search: pattern='{Pattern}'", pattern);

        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var results = new List<ContentMatch>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null) continue;

            foreach (var document in project.Documents)
            {
                if (!document.SupportsSyntaxTree) continue;

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (semanticModel == null || root == null) continue;

                // Search in method bodies
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                foreach (var method in methods)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
                    if (methodSymbol == null) continue;

                    // Filter by namespace
                    if (!string.IsNullOrEmpty(namespaceFilter))
                    {
                        var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString();
                        if (containingNamespace == null || !containingNamespace.Contains(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // Search in method body
                    var methodBody = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
                    var matches = regex.Matches(methodBody);

                    if (matches.Count > 0)
                    {
                        var lineSpan = method.GetLocation().GetLineSpan();
                        var matchedLine = methodBody.Split('\n').FirstOrDefault(l => regex.IsMatch(l))?.Trim() ?? "";

                        results.Add(new ContentMatch
                        {
                            MethodName = methodSymbol.Name,
                            FullyQualifiedName = methodSymbol.ToDisplayString(),
                            FilePath = document.FilePath ?? "",
                            LineNumber = lineSpan.StartLinePosition.Line,
                            MatchCount = matches.Count,
                            MatchedSnippet = TruncateSnippet(matchedLine, 100),
                            Relevance = CalculateContentRelevance(matches.Count, methodBody.Length)
                        });

                        if (results.Count >= limit * 2) break;
                    }
                }

                if (results.Count >= limit * 2) break;
            }

            if (results.Count >= limit * 2) break;
        }

        var topResults = results
            .OrderByDescending(r => r.Relevance)
            .ThenByDescending(r => r.MatchCount)
            .Take(limit)
            .ToList();

        return ToolHelpers.ToJson(new
        {
            mode = "content",
            pattern,
            totalFound = results.Count,
            returned = topResults.Count,
            results = topResults.Select(r => new
            {
                r.MethodName,
                r.FullyQualifiedName,
                r.FilePath,
                r.LineNumber,
                r.MatchCount,
                r.MatchedSnippet,
                relevance = $"{r.Relevance:F2}"
            })
        });
    }

    // ==================== Semantic Mode ====================

    private static async Task<object> SearchSemanticMode(
        SemanticSearchService semanticService,
        string pattern,
        string[]? entityTypes,
        int limit,
        float minSimilarity,
        ILogger<PatternSearchToolsLogCategory> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Semantic mode search: pattern='{Pattern}'", pattern);

        // Index if needed
        if (!semanticService.IsIndexed())
        {
            logger.LogInformation("Solution not indexed, performing semantic indexing...");
            await semanticService.IndexCurrentSolutionAsync(cancellationToken);
        }

        // Determine scope based on entity types
        string scope = "solution";
        if (entityTypes != null && entityTypes.Length > 0)
        {
            if (entityTypes.All(t => t.Equals("method", StringComparison.OrdinalIgnoreCase)))
                scope = "methods";
            else if (entityTypes.All(t => t.Equals("class", StringComparison.OrdinalIgnoreCase) ||
                                          t.Equals("interface", StringComparison.OrdinalIgnoreCase)))
                scope = "classes";
        }

        // Perform semantic search
        List<SemanticCodeMatch> semanticResults;
        if (scope == "methods")
            semanticResults = await semanticService.FindSimilarMethodsAsync(pattern, limit, minSimilarity, cancellationToken);
        else if (scope == "classes")
            semanticResults = await semanticService.FindSimilarClassesAsync(pattern, limit, minSimilarity, cancellationToken);
        else
            semanticResults = await semanticService.FindSimilarCodeAsync(pattern, limit, minSimilarity, cancellationToken);

        return ToolHelpers.ToJson(new
        {
            mode = "semantic",
            pattern,
            scope,
            totalFound = semanticResults.Count,
            returned = semanticResults.Count,
            results = semanticResults.Select(r => new
            {
                r.FullyQualifiedName,
                r.FilePath,
                r.LineNumber,
                type = r.Type.ToString(),
                similarity = $"{r.Similarity:F3}",
                rank = r.Rank,
                codePreview = TruncateSnippet(r.Code, 200)
            })
        });
    }

    // ==================== Hybrid Mode ====================

    private static async Task<object> SearchHybridMode(
        Solution solution,
        SemanticSearchService semanticService,
        string pattern,
        string[]? entityTypes,
        string? namespaceFilter,
        int limit,
        float minSimilarity,
        ILogger<PatternSearchToolsLogCategory> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Hybrid mode search: pattern='{Pattern}'", pattern);

        // Run all search modes in parallel
        var entityTask = SearchEntityMode(solution, pattern, entityTypes, namespaceFilter, limit, logger, cancellationToken);
        var contentTask = SearchContentMode(solution, pattern, entityTypes, namespaceFilter, limit, logger, cancellationToken);
        var semanticTask = SearchSemanticMode(semanticService, pattern, entityTypes, limit, minSimilarity, logger, cancellationToken);

        await Task.WhenAll(entityTask, contentTask, semanticTask);

        // Extract results
        var entityResult = (dynamic)entityTask.Result;
        var contentResult = (dynamic)contentTask.Result;
        var semanticResult = (dynamic)semanticTask.Result;

        // Combine and rank results
        var combinedResults = new List<HybridMatch>();

        // Add entity matches
        foreach (var item in entityResult.results)
        {
            combinedResults.Add(new HybridMatch
            {
                Name = item.Name,
                FullyQualifiedName = item.FullyQualifiedName,
                FilePath = item.FilePath,
                LineNumber = item.LineNumber,
                Source = "entity",
                Score = float.Parse(item.relevance) * 0.3f, // Weight: 30%
                Details = $"Kind: {item.Kind}, Namespace: {item.Namespace}"
            });
        }

        // Add content matches
        foreach (var item in contentResult.results)
        {
            var existing = combinedResults.FirstOrDefault(r =>
                r.FullyQualifiedName == item.FullyQualifiedName);

            if (existing != null)
            {
                existing.Score += float.Parse(item.relevance) * 0.3f; // Weight: 30%
                existing.Source += "+content";
                existing.Details += $" | Matches: {item.MatchCount}";
            }
            else
            {
                combinedResults.Add(new HybridMatch
                {
                    Name = item.MethodName,
                    FullyQualifiedName = item.FullyQualifiedName,
                    FilePath = item.FilePath,
                    LineNumber = item.LineNumber,
                    Source = "content",
                    Score = float.Parse(item.relevance) * 0.3f,
                    Details = $"Matches: {item.MatchCount}, Snippet: {item.MatchedSnippet}"
                });
            }
        }

        // Add semantic matches
        foreach (var item in semanticResult.results)
        {
            var existing = combinedResults.FirstOrDefault(r =>
                r.FullyQualifiedName == item.FullyQualifiedName);

            if (existing != null)
            {
                existing.Score += float.Parse(item.similarity) * 0.4f; // Weight: 40% (highest)
                existing.Source += "+semantic";
                existing.Details += $" | Similarity: {item.similarity}";
            }
            else
            {
                combinedResults.Add(new HybridMatch
                {
                    Name = item.FullyQualifiedName.Split('.').Last().Split('(').First(),
                    FullyQualifiedName = item.FullyQualifiedName,
                    FilePath = item.FilePath,
                    LineNumber = item.LineNumber,
                    Source = "semantic",
                    Score = float.Parse(item.similarity) * 0.4f,
                    Details = $"Type: {item.type}, Similarity: {item.similarity}"
                });
            }
        }

        // Sort by combined score
        var rankedResults = combinedResults
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Source.Count(c => c == '+')) // Prefer multi-source matches
            .Take(limit)
            .ToList();

        return ToolHelpers.ToJson(new
        {
            mode = "hybrid",
            pattern,
            totalCombined = combinedResults.Count,
            returned = rankedResults.Count,
            breakdown = new
            {
                entityMatches = entityResult.totalFound,
                contentMatches = contentResult.totalFound,
                semanticMatches = semanticResult.totalFound
            },
            results = rankedResults.Select((r, idx) => new
            {
                rank = idx + 1,
                r.Name,
                r.FullyQualifiedName,
                r.FilePath,
                r.LineNumber,
                r.Source,
                score = $"{r.Score:F3}",
                r.Details
            })
        });
    }

    // ==================== Helper Methods ====================

    private static bool MatchesEntityType(ISymbol symbol, string[] entityTypes)
    {
        var symbolKind = symbol.Kind.ToString().ToLowerInvariant();

        foreach (var type in entityTypes)
        {
            var typeLower = type.ToLowerInvariant();
            if (symbolKind == typeLower) return true;

            // Special cases
            if (typeLower == "class" && symbol is INamedTypeSymbol { TypeKind: TypeKind.Class }) return true;
            if (typeLower == "interface" && symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface }) return true;
            if (typeLower == "enum" && symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum }) return true;
            if (typeLower == "struct" && symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct }) return true;
        }

        return false;
    }

    private static float CalculateEntityRelevance(string symbolName, string pattern)
    {
        // Exact match = 1.0
        if (symbolName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            return 1.0f;

        // Starts with pattern = 0.8
        if (symbolName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            return 0.8f;

        // Contains pattern = 0.6
        if (symbolName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            return 0.6f;

        // Regex match = 0.4
        return 0.4f;
    }

    private static float CalculateContentRelevance(int matchCount, int totalLength)
    {
        // More matches in shorter code = higher relevance
        var density = (float)matchCount / Math.Max(totalLength / 100, 1);
        return Math.Min(density, 1.0f);
    }

    private static string TruncateSnippet(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }

    // ==================== Models ====================

    private class EntityMatch
    {
        public required string Name { get; init; }
        public required string FullyQualifiedName { get; init; }
        public required string Kind { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string Namespace { get; init; }
        public required float Relevance { get; init; }
    }

    private class ContentMatch
    {
        public required string MethodName { get; init; }
        public required string FullyQualifiedName { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required int MatchCount { get; init; }
        public required string MatchedSnippet { get; init; }
        public required float Relevance { get; init; }
    }

    private class HybridMatch
    {
        public required string Name { get; init; }
        public required string FullyQualifiedName { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string Source { get; set; }
        public required float Score { get; set; }
        public required string Details { get; set; }
    }
}

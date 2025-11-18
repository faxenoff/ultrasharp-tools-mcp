using ModelContextProtocol;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for semantic code analysis using vector embeddings (TEI/Ollama/Memory).
/// Sprint 1: semantic_search, semantic_diff, detect_code_clones
/// </summary>
public class SemanticAnalysisToolsLogCategory { }

[McpServerToolType]
public static partial class SemanticAnalysisTools
{
    /// <summary>
    /// Search for semantically similar code using natural language or code snippets.
    /// Uses TEI/Ollama/Memory embeddings for ML-powered search.
    /// </summary>
    [McpServerTool(Name = "semantic_search", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Find semantically similar code using natural language keywords or code snippets. " +
                 "Searches methods and classes based on meaning, not just text matching. " +
                 "Useful for discovering similar implementations, patterns, or functionality across the codebase.")]
    public static async Task<object> SemanticSearch(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ILogger<SemanticAnalysisToolsLogCategory> logger,

        [Description("Natural language query (e.g., 'validate email address') or code snippet to search for")]
        string query,

        [Description("Search scope: 'solution' (all projects), 'project:<name>', or 'methods'/'classes' to filter by type")]
        string scope = "solution",

        [Description("Number of results to return (1-50)")]
        int topK = 10,

        [Description("Minimum similarity threshold (0.0-1.0). Higher values return only very similar matches.")]
        float minSimilarity = 0.7f,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            // Validation
            ErrorHandlingHelpers.ValidateStringParameter(query, nameof(query), logger);
            await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(SemanticSearch), cancellationToken);

            if (topK < 1 || topK > 50)
            {
                throw new McpException("topK must be between 1 and 50");
            }

            if (minSimilarity < 0.0f || minSimilarity > 1.0f)
            {
                throw new McpException("minSimilarity must be between 0.0 and 1.0");
            }

            logger.LogInformation("Executing {Tool} for query: '{Query}', scope: {Scope}, topK: {TopK}, minSimilarity: {MinSimilarity}",
                nameof(SemanticSearch), query, scope, topK, minSimilarity);

            // Index solution if not already indexed
            if (!searchService.IsIndexed())
            {
                logger.LogInformation("Solution not indexed, performing initial indexing...");
                await searchService.IndexCurrentSolutionAsync(cancellationToken);
                logger.LogInformation("Indexing complete");
            }

            // Perform search based on scope
            List<SemanticCodeMatch> results;

            if (scope.Equals("methods", StringComparison.OrdinalIgnoreCase))
            {
                results = await searchService.FindSimilarMethodsAsync(query, topK, minSimilarity, cancellationToken);
            }
            else if (scope.Equals("classes", StringComparison.OrdinalIgnoreCase))
            {
                results = await searchService.FindSimilarClassesAsync(query, topK, minSimilarity, cancellationToken);
            }
            else if (scope.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
            {
                var projectName = scope.Substring("project:".Length);
                await searchService.ReindexProjectAsync(projectName, cancellationToken);
                results = await searchService.FindSimilarCodeAsync(query, topK, minSimilarity, cancellationToken);
            }
            else // "solution" or default
            {
                results = await searchService.FindSimilarCodeAsync(query, topK, minSimilarity, cancellationToken);
            }

            // Get indexer metrics
            var metrics = searchService.GetIndexerMetrics();

            // Format results
            return ToolHelpers.ToJson(new
            {
                query,
                scope,
                resultsCount = results.Count,
                indexMetrics = new
                {
                    indexedMethods = metrics.IndexedMethods,
                    indexedClasses = metrics.IndexedClasses,
                    indexedDocuments = metrics.IndexedDocuments,
                    embeddingCacheHitRate = Math.Round(metrics.EmbeddingMetrics.CacheHitRate * 100, 2) + "%",
                    avgEmbedTimeMs = Math.Round(metrics.EmbeddingMetrics.AverageEmbedTimeMs, 2)
                },
                results = results.Select(r => new
                {
                    fqn = r.FullyQualifiedName ?? r.Id,
                    similarity = Math.Round(r.Similarity * 100, 2) + "%",
                    filePath = r.FilePath,
                    line = r.LineNumber,
                    type = r.Type.ToString().ToLowerInvariant(),
                    rank = r.Rank,
                    codePreview = r.Code.Length > 200 ? r.Code.Substring(0, 200) + "..." : r.Code
                }).ToList()
            });

        }, logger, nameof(SemanticSearch), cancellationToken);
    }

    /// <summary>
    /// Compare two code entities semantically to detect behavior changes.
    /// Returns similarity score and categorization of changes.
    /// </summary>
    [McpServerTool(Name = "semantic_diff", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Compare two code entities (methods/classes) semantically to determine if their behavior changed. " +
                 "Uses vector embeddings to compare code meaning, not just text diff. " +
                 "Useful for verifying refactorings, code reviews, and detecting breaking changes.")]
    public static async Task<object> SemanticDiff(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ICodeAnalysisService codeAnalysisService,
        ILogger<SemanticAnalysisToolsLogCategory> logger,

        [Description("Fully qualified name of first entity (before changes)")]
        string beforeFqn,

        [Description("Fully qualified name of second entity (after changes). Can be same as beforeFqn if modified in place.")]
        string afterFqn,

        [Description("Include implementation details in comparison (default: false, focuses on high-level behavior)")]
        bool includeImplementationDetails = false,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            // Validation
            ErrorHandlingHelpers.ValidateStringParameter(beforeFqn, nameof(beforeFqn), logger);
            ErrorHandlingHelpers.ValidateStringParameter(afterFqn, nameof(afterFqn), logger);
            await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(SemanticDiff), cancellationToken);

            logger.LogInformation("Executing {Tool}: comparing '{Before}' vs '{After}'",
                nameof(SemanticDiff), beforeFqn, afterFqn);

            // Get symbols
            var beforeSymbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(solutionManager, beforeFqn, cancellationToken);
            var afterSymbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(solutionManager, afterFqn, cancellationToken);

            // Extract code
            var beforeCode = await GetSymbolCodeAsync(beforeSymbol, solutionManager, cancellationToken);
            var afterCode = await GetSymbolCodeAsync(afterSymbol, solutionManager, cancellationToken);

            // Index if needed
            if (!searchService.IsIndexed())
            {
                logger.LogInformation("Indexing solution for semantic comparison...");
                await searchService.IndexCurrentSolutionAsync(cancellationToken);
            }

            // Compute semantic similarity using vector embeddings
            var similarity = await ComputeSemanticSimilarityAsync(searchService, beforeCode, afterCode, cancellationToken);

            // Categorize change
            var changeCategory = CategorizeSemanticChange(similarity);
            var behaviorPreserved = similarity > 0.95f;

            // Calculate code size changes if requested
            object? codeMetrics = null;
            if (includeImplementationDetails)
            {
                var beforeLines = beforeCode.Split('\n').Length;
                var afterLines = afterCode.Split('\n').Length;

                codeMetrics = new
                {
                    beforeLines,
                    afterLines,
                    lineDelta = afterLines - beforeLines,
                    sizeDeltaPercent = beforeLines > 0 ? Math.Round((afterLines - beforeLines) * 100.0 / beforeLines, 2) : 0
                };
            }

            return ToolHelpers.ToJson(new
            {
                beforeFqn,
                afterFqn,
                semanticSimilarity = Math.Round(similarity * 100, 2) + "%",
                behaviorPreserved,
                changeCategory,
                analysis = new
                {
                    description = GetChangeDescription(changeCategory, similarity),
                    risks = GetChangeRisks(changeCategory, similarity),
                    recommendations = GetChangeRecommendations(changeCategory, similarity)
                },
                codeMetrics,
                metadata = new
                {
                    beforeType = beforeSymbol.Kind.ToString(),
                    afterType = afterSymbol.Kind.ToString(),
                    beforeLocation = $"{beforeSymbol.Locations.FirstOrDefault()?.GetLineSpan().Path}:{beforeSymbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line}",
                    afterLocation = $"{afterSymbol.Locations.FirstOrDefault()?.GetLineSpan().Path}:{afterSymbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line}"
                }
            });

        }, logger, nameof(SemanticDiff), cancellationToken);
    }

    /// <summary>
    /// Detect code clones (duplicate or similar code) across the codebase.
    /// Groups similar methods/classes for refactoring opportunities.
    /// </summary>
    [McpServerTool(Name = "detect_code_clones", Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Find duplicate or similar code blocks across the codebase using semantic analysis. " +
                 "Groups similar methods/classes together and suggests refactoring opportunities. " +
                 "Useful for identifying technical debt, copy-paste code, and consolidation candidates.")]
    public static async Task<object> DetectCodeClones(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ILogger<SemanticAnalysisToolsLogCategory> logger,

        [Description("Minimum similarity threshold (0.0-1.0) to consider code as clones. 0.85 recommended for duplicates, 0.7 for similar patterns.")]
        float minSimilarity = 0.85f,

        [Description("Clone detection mode: 'semantic' (ML-based, default), 'exact' (identical code), or 'similar' (flexible matching)")]
        string mode = "semantic",

        [Description("Only analyze methods and properties, skip classes (default: true for faster analysis)")]
        bool membersOnly = true,

        [Description("Maximum number of clone groups to return (default: 20)")]
        int maxGroups = 20,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            // Validation
            await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(DetectCodeClones), cancellationToken);

            if (minSimilarity < 0.5f || minSimilarity > 1.0f)
            {
                throw new McpException("minSimilarity must be between 0.5 and 1.0");
            }

            if (maxGroups < 1 || maxGroups > 100)
            {
                throw new McpException("maxGroups must be between 1 and 100");
            }

            logger.LogInformation("Executing {Tool}: mode={Mode}, minSimilarity={MinSimilarity}, membersOnly={MembersOnly}, maxGroups={MaxGroups}",
                nameof(DetectCodeClones), mode, minSimilarity, membersOnly, maxGroups);

            // Index solution
            if (!searchService.IsIndexed())
            {
                logger.LogInformation("Indexing solution for clone detection...");
                await searchService.IndexCurrentSolutionAsync(cancellationToken);
                logger.LogInformation("Indexing complete");
            }

            // Get all code entities
            var entities = await GetAllCodeEntitiesAsync(solutionManager, membersOnly, cancellationToken);
            logger.LogInformation("Analyzing {Count} entities for clones", entities.Count);

            // Find clone groups
            var cloneGroups = await FindCloneGroupsAsync(searchService, entities, minSimilarity, maxGroups, cancellationToken);

            // Calculate statistics
            var totalDuplicates = cloneGroups.Sum(g => g.Members.Count);
            var totalLines = cloneGroups.Sum(g => g.EstimatedLinesOfCode * g.Members.Count);

            return ToolHelpers.ToJson(new
            {
                totalCloneGroups = cloneGroups.Count,
                totalDuplicateEntities = totalDuplicates,
                estimatedDuplicateLines = totalLines,
                analysisMode = mode,
                similarityThreshold = minSimilarity,
                groups = cloneGroups.Select((g, index) => new
                {
                    groupId = g.Id,
                    cloneType = g.CloneType,
                    avgSimilarity = Math.Round(g.AvgSimilarity * 100, 2) + "%",
                    memberCount = g.Members.Count,
                    estimatedLinesOfCode = g.EstimatedLinesOfCode,
                    members = g.Members.Select(m => new
                    {
                        fqn = m.Fqn,
                        filePath = m.FilePath,
                        line = m.LineNumber,
                        similarity = Math.Round(m.Similarity * 100, 2) + "%"
                    }).ToList(),
                    refactoringRecommendation = g.RefactoringRecommendation,
                    priority = g.Priority
                }).ToList()
            });

        }, logger, nameof(DetectCodeClones), cancellationToken);
    }

    // ==================== Private Helper Methods ====================

    private static async Task<string> GetSymbolCodeAsync(ISymbol symbol, ISolutionManager solutionManager, CancellationToken cancellationToken)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            throw new McpException($"Symbol '{symbol.ToDisplayString()}' has no syntax reference");
        }

        var syntaxNode = await syntaxRef.GetSyntaxAsync(cancellationToken);
        return syntaxNode.ToFullString();
    }

    private static async Task<float> ComputeSemanticSimilarityAsync(
        SemanticSearchService searchService,
        string code1,
        string code2,
        CancellationToken cancellationToken)
    {
        // Search for code2 using code1 as query
        var matches = await searchService.FindSimilarCodeAsync(code1, limit: 1, minSimilarity: 0.0f, cancellationToken);

        // If we find exact match, return perfect similarity
        if (matches.Any() && matches[0].Code.Trim() == code2.Trim())
        {
            return matches[0].Similarity;
        }

        // Otherwise search in reverse
        var reverseMatches = await searchService.FindSimilarCodeAsync(code2, limit: 1, minSimilarity: 0.0f, cancellationToken);

        return matches.Any() && reverseMatches.Any()
            ? (matches[0].Similarity + reverseMatches[0].Similarity) / 2.0f
            : 0.0f;
    }

    private static string CategorizeSemanticChange(float similarity)
    {
        return similarity switch
        {
            >= 0.98f => "identical",
            >= 0.95f => "refactoring",
            >= 0.85f => "minor_change",
            >= 0.70f => "moderate_change",
            >= 0.50f => "major_change",
            _ => "breaking_change"
        };
    }

    private static string GetChangeDescription(string category, float similarity)
    {
        return category switch
        {
            "identical" => "Code is semantically identical",
            "refactoring" => "Likely a refactoring - behavior appears preserved",
            "minor_change" => "Minor behavioral changes detected",
            "moderate_change" => "Moderate behavioral changes - review carefully",
            "major_change" => "Significant behavioral changes detected",
            _ => "Breaking change - functionality appears completely different"
        };
    }

    private static string[] GetChangeRisks(string category, float similarity)
    {
        return category switch
        {
            "identical" or "refactoring" => Array.Empty<string>(),
            "minor_change" => new[] { "Review for unintended side effects" },
            "moderate_change" => new[] { "Requires thorough testing", "Check for breaking changes in callers" },
            "major_change" => new[] { "High risk of breaking changes", "Requires comprehensive testing", "Update documentation" },
            _ => new[] { "Critical: Complete behavioral change", "All callers must be reviewed", "Consider versioning strategy" }
        };
    }

    private static string[] GetChangeRecommendations(string category, float similarity)
    {
        return category switch
        {
            "identical" => new[] { "No changes needed" },
            "refactoring" => new[] { "Run existing tests to verify behavior", "Consider adding comments explaining refactoring" },
            "minor_change" => new[] { "Add regression tests for changed behavior", "Update XML documentation" },
            "moderate_change" => new[] { "Create before/after test cases", "Document behavioral changes", "Review all call sites" },
            "major_change" or _ => new[] { "Consider creating new method instead of modifying", "Deprecate old version if possible", "Create migration guide" }
        };
    }

    private static async Task<List<CodeEntity>> GetAllCodeEntitiesAsync(
        ISolutionManager solutionManager,
        bool membersOnly,
        CancellationToken cancellationToken)
    {
        var entities = new List<CodeEntity>();
        var solution = solutionManager.CurrentWorkspace?.CurrentSolution;

        if (solution == null)
        {
            return entities;
        }

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

                if (syntaxRoot == null || semanticModel == null)
                {
                    continue;
                }

                // Extract methods
                var methods = syntaxRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => !m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AbstractKeyword)))
                    .ToList();

                foreach (var method in methods)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
                    if (symbol != null)
                    {
                        entities.Add(new CodeEntity
                        {
                            Fqn = symbol.ToDisplayString(),
                            FilePath = document.FilePath ?? "",
                            LineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Code = method.ToFullString(),
                            Type = "method"
                        });
                    }
                }

                // Extract classes if not membersOnly
                if (!membersOnly)
                {
                    var classes = syntaxRoot.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .ToList();

                    foreach (var cls in classes)
                    {
                        var symbol = semanticModel.GetDeclaredSymbol(cls, cancellationToken);
                        if (symbol != null)
                        {
                            entities.Add(new CodeEntity
                            {
                                Fqn = symbol.ToDisplayString(),
                                FilePath = document.FilePath ?? "",
                                LineNumber = cls.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Code = cls.ToFullString(),
                                Type = "class"
                            });
                        }
                    }
                }
            }
        }

        return entities;
    }

    private static async Task<List<CloneGroup>> FindCloneGroupsAsync(
        SemanticSearchService searchService,
        List<CodeEntity> entities,
        float minSimilarity,
        int maxGroups,
        CancellationToken cancellationToken)
    {
        var cloneGroups = new List<CloneGroup>();
        var processed = new HashSet<string>();

        foreach (var entity in entities)
        {
            if (processed.Contains(entity.Fqn))
            {
                continue;
            }

            // Find similar entities
            var similarEntities = await searchService.FindSimilarCodeAsync(
                entity.Code,
                limit: 50,
                minSimilarity,
                cancellationToken);

            // Filter to entities we're analyzing
            var clones = similarEntities
                .Where(s => entities.Any(e => e.Fqn == s.FullyQualifiedName))
                .Where(s => !processed.Contains(s.FullyQualifiedName ?? ""))
                .ToList();

            if (clones.Count >= 2) // At least 2 clones (including original)
            {
                var members = clones.Select(c =>
                {
                    var e = entities.First(ent => ent.Fqn == c.FullyQualifiedName);
                    return new CloneMember
                    {
                        Fqn = e.Fqn,
                        FilePath = e.FilePath,
                        LineNumber = e.LineNumber,
                        Similarity = c.Similarity
                    };
                }).ToList();

                var group = new CloneGroup
                {
                    Id = $"clone-{cloneGroups.Count + 1}",
                    CloneType = DetermineCloneType(clones.Average(c => c.Similarity)),
                    AvgSimilarity = clones.Average(c => c.Similarity),
                    Members = members,
                    EstimatedLinesOfCode = entity.Code.Split('\n').Length,
                    RefactoringRecommendation = GenerateRefactoringRecommendation(members.Count, entity.Type),
                    Priority = CalculateRefactoringPriority(members.Count, clones.Average(c => c.Similarity))
                };

                cloneGroups.Add(group);

                // Mark as processed
                foreach (var clone in clones)
                {
                    processed.Add(clone.FullyQualifiedName ?? "");
                }
            }

            if (cloneGroups.Count >= maxGroups)
            {
                break;
            }
        }

        return cloneGroups.OrderByDescending(g => g.Priority).ToList();
    }

    private static string DetermineCloneType(float avgSimilarity)
    {
        return avgSimilarity switch
        {
            >= 0.98f => "exact",
            >= 0.90f => "very_similar",
            >= 0.80f => "similar",
            _ => "conceptually_similar"
        };
    }

    private static string GenerateRefactoringRecommendation(int cloneCount, string entityType)
    {
        if (cloneCount >= 5)
        {
            return $"Extract to shared utility {entityType} - {cloneCount} duplicates found";
        }
        else if (cloneCount >= 3)
        {
            return $"Consider consolidating into single {entityType}";
        }
        else
        {
            return $"Review for potential consolidation";
        }
    }

    private static string CalculateRefactoringPriority(int cloneCount, float avgSimilarity)
    {
        var score = cloneCount * avgSimilarity;
        return score switch
        {
            >= 4.0f => "critical",
            >= 2.5f => "high",
            >= 1.5f => "medium",
            _ => "low"
        };
    }

    // ==================== Data Models ====================

    private class CodeEntity
    {
        public required string Fqn { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string Code { get; init; }
        public required string Type { get; init; }
    }

    private class CloneGroup
    {
        public required string Id { get; init; }
        public required string CloneType { get; init; }
        public required float AvgSimilarity { get; init; }
        public required List<CloneMember> Members { get; init; }
        public required int EstimatedLinesOfCode { get; init; }
        public required string RefactoringRecommendation { get; init; }
        public required string Priority { get; init; }
    }

    private class CloneMember
    {
        public required string Fqn { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required float Similarity { get; init; }
    }

    /// <summary>
    /// Incrementally reindex changed files for semantic search.
    /// </summary>
    [McpServerTool(Name = "reindex_changed_files", Idempotent = false, ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Incrementally reindex modified files in the semantic search index. " +
                 "Use this after editing files to update the search index without full reindexing. " +
                 "More efficient than reindexing the entire solution.")]
    public static async Task<object> ReindexChangedFiles(
        SemanticSearchService searchService,
        ISolutionManager solutionManager,
        ILogger<SemanticAnalysisToolsLogCategory> logger,

        [Description("Array of absolute file paths to reindex")]
        string[] filePaths,

        CancellationToken cancellationToken = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                throw new McpException("No file paths provided");
            }

            await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(solutionManager, logger, nameof(ReindexChangedFiles), cancellationToken);

            logger.LogInformation("Reindexing {Count} changed files", filePaths.Length);

            await searchService.ReindexChangedFilesAsync(filePaths, cancellationToken);

            var metrics = searchService.GetIndexerMetrics();

            return ToolHelpers.ToJson(new
            {
                success = true,
                filesReindexed = filePaths.Length,
                filePaths,
                metrics = new
                {
                    indexedMethods = metrics.IndexedMethods,
                    indexedClasses = metrics.IndexedClasses,
                    indexedDocuments = metrics.IndexedDocuments,
                    embeddingProvider = metrics.EmbeddingMetrics.ProviderInfo.Name
                },
                message = $"Successfully reindexed {filePaths.Length} files"
            });

        }, logger, nameof(ReindexChangedFiles), cancellationToken);
    }
}

using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Overlord.Models.Responses;
using UltrasharpTools.Overlord.Serialization;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp;
using UltrasharpTools.Tools.Mcp.Tools;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация MCP proxy - выполняет MCP tools на стороне сервера.
/// Uses source-generated JSON serialization for AOT compatibility.
/// </summary>
public sealed partial class McpProxyService : IMcpProxyService {
    private static readonly string[] validScopes = new[] { "method", "class", "project" };
    private readonly ILogger<McpProxyService> _logger;
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeAnalysisService _analysisService;
    private readonly ICodeModificationService _modificationService;
    private readonly IMultiProjectVectorStoreService _vectorStore;
    private readonly ISymbolResolutionService _symbolResolution;
    private readonly IServiceProvider _serviceProvider;

    public McpProxyService(
        ILogger<McpProxyService> logger,
        ISolutionManager solutionManager,
        ICodeAnalysisService analysisService,
        ICodeModificationService modificationService,
        IMultiProjectVectorStoreService vectorStore,
        ISymbolResolutionService symbolResolution,
        IServiceProvider serviceProvider
    ) {
        _logger = logger;
        _solutionManager = solutionManager;
        _analysisService = analysisService;
        _modificationService = modificationService;
        _vectorStore = vectorStore;
        _symbolResolution = symbolResolution;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteToolCallAsync(
        string toolName,
        string argumentsJson,
        string? projectContext = null,
        CancellationToken cancellationToken = default
    ) {
        LogExecutingTool(toolName);

        try {
            var result = toolName switch {
                "load_solution" => await ExecuteLoadSolution(argumentsJson, cancellationToken),
                "find_duplicates" => await ExecuteFindDuplicates(argumentsJson, projectContext, cancellationToken),
                "view_definition" => await ExecuteViewDefinition(argumentsJson, cancellationToken),
                "find_references" => await ExecuteFindReferences(argumentsJson, cancellationToken),
                "modify_code" => await ExecuteModifyCode(argumentsJson, cancellationToken),
                "analyze_complexity" => await ExecuteAnalyzeComplexity(argumentsJson, cancellationToken),
                "format_code" => await ExecuteFormatCode(argumentsJson, cancellationToken),
                "reindex_changed_files" => await ExecuteReindexChangedFiles(argumentsJson, projectContext, cancellationToken),
                "semantic_search" => await ExecuteSemanticSearch(argumentsJson, projectContext, cancellationToken),
                "semantic_diff" => await ExecuteSemanticDiff(argumentsJson, cancellationToken),
                "detect_code_clones" => await ExecuteDetectCodeClones(argumentsJson, projectContext, cancellationToken),
                "pattern_search" => await ExecutePatternSearch(argumentsJson, projectContext, cancellationToken),
                _ => $"Unknown tool: {toolName}",
            };

            LogToolExecuted(toolName);
            return result;
        } catch (Exception ex) {
            LogToolExecutionFailed(ex, toolName);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message, toolName), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    public Task<List<ToolInfo>> GetAvailableToolsAsync(CancellationToken cancellationToken = default) {
        var tools = new List<ToolInfo>
        {
            new() { Name = "load_solution", Description = "Load a C# solution for analysis" },
            new() { Name = "find_duplicates", Description = "Find duplicate code across all projects (requires vector)" },
            new() { Name = "view_definition", Description = "View code definition of a symbol" },
            new() { Name = "find_references", Description = "Find all references to a symbol" },
            new() { Name = "modify_code", Description = "Modify code of a member" },
            new() { Name = "analyze_complexity", Description = "Analyze code complexity" },
            new() { Name = "format_code", Description = "Format code using CSharpier" },
            new() { Name = "reindex_changed_files", Description = "Reindex changed files in vector store" },
            new() { Name = "semantic_search", Description = "Search code by natural language query (requires embedding)" },
            new() { Name = "semantic_diff", Description = "Compare two code fragments semantically" },
            new() { Name = "detect_code_clones", Description = "Detect code clones across projects" },
            new() { Name = "pattern_search", Description = "Advanced pattern search with semantic mode" },
        };

        return Task.FromResult(tools);
    }

    private async Task<string> ExecuteLoadSolution(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.LoadSolutionArgs);
        if (args == null || string.IsNullOrEmpty(args.SolutionPath)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments"), OverlordJsonContext.Default.ErrorResponse);
        }

        await _solutionManager.LoadSolutionAsync(args.SolutionPath, cancellationToken);

        return JsonSerializer.Serialize(
            new LoadSolutionResponse(true, args.SolutionPath, "Solution loaded successfully on server"),
            OverlordJsonContext.Default.LoadSolutionResponse
        );
    }

    private async Task<string> ExecuteFindDuplicates(string argumentsJson, string? projectContext, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.FindDuplicatesArgs);
        if (args == null) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments"), OverlordJsonContext.Default.ErrorResponse);
        }

        float[] queryVector;

        if (args.TargetVector != null && args.TargetVector.Length > 0) {
            queryVector = args.TargetVector;
            LogUsingPrecomputedVector(queryVector.Length);
        } else if (!string.IsNullOrEmpty(args.TargetCode)) {
            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();

            if (embeddingService == null) {
                return JsonSerializer.Serialize(
                    new ErrorWithHintResponse(
                        "TargetVector required or embedding service must be configured.",
                        "Either provide pre-computed vector or start Overlord with --embedding-url parameter."
                    ),
                    OverlordJsonContext.Default.ErrorWithHintResponse
                );
            }

            LogComputingEmbeddingForTargetCode(args.TargetCode.Length);
            var embedding = await embeddingService.GetEmbeddingAsync(args.TargetCode, cancellationToken);

            if (embedding == null || embedding.Length == 0) {
                return JsonSerializer.Serialize(
                    new ErrorWithHintResponse(
                        "Failed to compute embedding for TargetCode",
                        "Embedding service may be unavailable or returned empty result."
                    ),
                    OverlordJsonContext.Default.ErrorWithHintResponse
                );
            }

            queryVector = embedding;
            LogComputedEmbedding(queryVector.Length);
        } else {
            return JsonSerializer.Serialize(new ErrorResponse("Either TargetCode or TargetVector is required"), OverlordJsonContext.Default.ErrorResponse);
        }

        var matches = await _vectorStore.SearchAcrossProjectsAsync(
            queryVector: queryVector,
            threshold: args.Threshold,
            limit: args.Limit,
            projects: args.Scope == "current_project" && !string.IsNullOrEmpty(projectContext) ? new[] { projectContext } : null,
            cancellationToken: cancellationToken
        );

        var matchResults = matches.Select(m => new CodeMatchResult(
            m.Project,
            m.Branch,
            m.FilePath,
            m.Line,
            Math.Round(m.Similarity, 4),
            m.Code?.Length > 200 ? string.Concat(m.Code.AsSpan(0, 200), "...") : m.Code
        ));

        return JsonSerializer.Serialize(
            new FindDuplicatesResponse(queryVector.Length, args.Scope, args.Threshold, matches.Count, matchResults),
            OverlordJsonContext.Default.FindDuplicatesResponse
        );
    }

    private async Task<string> ExecuteViewDefinition(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.ViewDefinitionArgs);
        if (args == null || string.IsNullOrEmpty(args.Fqn)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments: FQN required"), OverlordJsonContext.Default.ErrorResponse);
        }

        if (!_symbolResolution.IsSolutionLoaded) {
            return JsonSerializer.Serialize(new ErrorResponse("No solution loaded on server"), OverlordJsonContext.Default.ErrorResponse);
        }

        try {
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null) {
                return JsonSerializer.Serialize(new ErrorResponse($"Symbol '{args.Fqn}' not found in loaded solution"), OverlordJsonContext.Default.ErrorResponse);
            }

            var sourceResolutionService = _serviceProvider.GetRequiredService<ISourceResolutionService>();
            var solution = _solutionManager.CurrentSolution;
            var locations = symbol.Locations.Where(l => l.IsInSource).ToList();

            if (locations.Count == 0) {
                var sourceResult = await sourceResolutionService.ResolveSourceAsync(symbol, cancellationToken);
                if (sourceResult != null) {
                    return JsonSerializer.Serialize(
                        new ViewDefinitionResponse(args.Fqn, sourceResult.FilePath, sourceResult.Source, null, sourceResult.ResolutionMethod),
                        OverlordJsonContext.Default.ViewDefinitionResponse
                    );
                }

                return JsonSerializer.Serialize(new ErrorResponse($"No source definition found for '{args.Fqn}'"), OverlordJsonContext.Default.ErrorResponse);
            }

            var location = locations.First();
            if (location.SourceTree == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Symbol location has no source tree"), OverlordJsonContext.Default.ErrorResponse);
            }

#pragma warning disable CS8602
            var document = solution.GetDocument(location.SourceTree!);
#pragma warning restore CS8602
            if (document == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Could not find document for symbol"), OverlordJsonContext.Default.ErrorResponse);
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Could not get syntax tree"), OverlordJsonContext.Default.ErrorResponse);
            }

            var syntaxNode = syntaxTree.GetRoot(cancellationToken).FindNode(location.SourceSpan);
            var sourceText = syntaxNode.ToFullString();

            return JsonSerializer.Serialize(
                new ViewDefinitionResponse(
                    args.Fqn,
                    document.FilePath ?? location.SourceTree?.FilePath,
                    sourceText,
                    location.GetLineSpan().StartLinePosition.Line + 1,
                    "Roslyn"
                ),
                OverlordJsonContext.Default.ViewDefinitionResponse
            );
        } catch (Exception ex) {
            LogViewDefinitionFailed(ex, args.Fqn);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteFindReferences(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.FindReferencesArgs);
        if (args == null || string.IsNullOrEmpty(args.Fqn)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments: FQN required"), OverlordJsonContext.Default.ErrorResponse);
        }

        if (!_symbolResolution.IsSolutionLoaded) {
            return JsonSerializer.Serialize(new ErrorResponse("No solution loaded on server"), OverlordJsonContext.Default.ErrorResponse);
        }

        try {
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null) {
                return JsonSerializer.Serialize(new ErrorResponse($"Symbol '{args.Fqn}' not found in loaded solution"), OverlordJsonContext.Default.ErrorResponse);
            }

            var referencedSymbols = await _analysisService.FindReferencesAsync(symbol, cancellationToken);

            var references = new List<SymbolReferenceLocation>();
            foreach (var referencedSymbol in referencedSymbols) {
                foreach (var location in referencedSymbol.Locations) {
                    var lineSpan = location.Location.GetLineSpan();
                    references.Add(new SymbolReferenceLocation(
                        lineSpan.Path,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1
                    ));
                }
            }

            return JsonSerializer.Serialize(
                new FindReferencesResponse(args.Fqn, references.Count, references),
                OverlordJsonContext.Default.FindReferencesResponse
            );
        } catch (Exception ex) {
            LogFindReferencesFailed(ex, args.Fqn);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteModifyCode(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.ModifyCodeArgs);
        if (args == null || string.IsNullOrEmpty(args.Fqn) || string.IsNullOrEmpty(args.NewCode)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments: FQN and NewCode required"), OverlordJsonContext.Default.ErrorResponse);
        }

        if (!_symbolResolution.IsSolutionLoaded) {
            return JsonSerializer.Serialize(new ErrorResponse("No solution loaded on server"), OverlordJsonContext.Default.ErrorResponse);
        }

        try {
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null) {
                return JsonSerializer.Serialize(new ErrorResponse($"Symbol '{args.Fqn}' not found in loaded solution"), OverlordJsonContext.Default.ErrorResponse);
            }

            if (!symbol.DeclaringSyntaxReferences.Any()) {
                return JsonSerializer.Serialize(new ErrorResponse($"Symbol '{args.Fqn}' has no declaring syntax references"), OverlordJsonContext.Default.ErrorResponse);
            }

            var syntaxRef = symbol.DeclaringSyntaxReferences.First();
            var oldNode = await syntaxRef.GetSyntaxAsync(cancellationToken);

            var newNode = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseMemberDeclaration(args.NewCode);
            if (newNode == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Failed to parse new code as member declaration"), OverlordJsonContext.Default.ErrorResponse);
            }

            var location = symbol.Locations.First();
            if (location.SourceTree == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Symbol location has no source tree"), OverlordJsonContext.Default.ErrorResponse);
            }

#pragma warning disable CS8602
            var document = _solutionManager.CurrentSolution.GetDocument(location.SourceTree!);
#pragma warning restore CS8602
            if (document == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Could not find document for symbol"), OverlordJsonContext.Default.ErrorResponse);
            }

            var newSolution = await _modificationService.ReplaceNodeAsync(document.Id, oldNode, newNode, cancellationToken);
            var lintingResult = await _modificationService.ApplyChangesAsync(newSolution, cancellationToken, $"Modified {symbol.Kind} '{args.Fqn}' via MCP proxy");

            var success = lintingResult.After == null || lintingResult.After.ErrorCount == 0;
            return JsonSerializer.Serialize(
                new ModifyCodeResponse(
                    success,
                    args.Fqn,
                    $"Successfully modified {symbol.Kind} '{args.Fqn}'",
                    lintingResult.ChangedFiles,
                    lintingResult.After?.ErrorCount ?? 0,
                    lintingResult.After?.WarningCount ?? 0
                ),
                OverlordJsonContext.Default.ModifyCodeResponse
            );
        } catch (Exception ex) {
            LogModifyCodeFailed(ex, args.Fqn);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteAnalyzeComplexity(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.AnalyzeComplexityArgs);
        if (args == null || string.IsNullOrEmpty(args.Scope) || string.IsNullOrEmpty(args.Target)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments: Scope and Target required"), OverlordJsonContext.Default.ErrorResponse);
        }

        if (!_symbolResolution.IsSolutionLoaded) {
            return JsonSerializer.Serialize(new ErrorResponse("No solution loaded on server"), OverlordJsonContext.Default.ErrorResponse);
        }

        try {
            var scope = args.Scope.ToLower();
            if (!validScopes.Contains(scope)) {
                return JsonSerializer.Serialize(new ErrorResponse($"Invalid scope '{args.Scope}'. Must be 'method', 'class', or 'project'."), OverlordJsonContext.Default.ErrorResponse);
            }

            var complexityService = _serviceProvider.GetRequiredService<IComplexityAnalysisService>();
            var metrics = new Dictionary<string, object>();
            var recommendations = new List<string>();

            switch (scope) {
                case "method":
                    var methodSymbol = await _symbolResolution.FindSymbolAsync(args.Target, cancellationToken) as IMethodSymbol;
                    if (methodSymbol == null) {
                        return JsonSerializer.Serialize(new ErrorResponse($"Target '{args.Target}' is not a method."), OverlordJsonContext.Default.ErrorResponse);
                    }
                    await complexityService.AnalyzeMethodAsync(methodSymbol, metrics, recommendations, cancellationToken);
                    break;

                case "class":
                    var typeSymbol = await _symbolResolution.FindNamedTypeSymbolAsync(args.Target, cancellationToken);
                    if (typeSymbol == null) {
                        return JsonSerializer.Serialize(new ErrorResponse($"Target '{args.Target}' is not a class or interface."), OverlordJsonContext.Default.ErrorResponse);
                    }
                    await complexityService.AnalyzeTypeAsync(typeSymbol, metrics, recommendations, false, cancellationToken);
                    break;

                case "project":
                    var project = _solutionManager.GetProjectByName(args.Target);
                    if (project == null) {
                        return JsonSerializer.Serialize(new ErrorResponse($"Project '{args.Target}' not found."), OverlordJsonContext.Default.ErrorResponse);
                    }
                    await complexityService.AnalyzeProjectAsync(project, metrics, recommendations, false, cancellationToken);
                    break;
            }

            return JsonSerializer.Serialize(
                new AnalyzeComplexityResponse(args.Scope, args.Target, metrics, recommendations.Distinct().OrderBy(r => r).ToList()),
                OverlordJsonContext.Default.AnalyzeComplexityResponse
            );
        } catch (Exception ex) {
            LogAnalyzeComplexityFailed(ex, args.Target);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteFormatCode(string argumentsJson, CancellationToken cancellationToken) {
        var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.FormatCodeArgs);
        if (args == null || string.IsNullOrEmpty(args.Path)) {
            return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments: Path required"), OverlordJsonContext.Default.ErrorResponse);
        }

        try {
            if (!Path.IsPathFullyQualified(args.Path)) {
                return JsonSerializer.Serialize(new ErrorResponse($"Path must be absolute: {args.Path}"), OverlordJsonContext.Default.ErrorResponse);
            }

            if (!File.Exists(args.Path) && !Directory.Exists(args.Path)) {
                return JsonSerializer.Serialize(new ErrorResponse($"Path does not exist: {args.Path}"), OverlordJsonContext.Default.ErrorResponse);
            }

            var formattingService = _serviceProvider.GetRequiredService<IFormattingService>();
            var result = await formattingService.FormatAsync(args.Path, args.CheckOnly, cancellationToken);

            var message = args.CheckOnly
                ? $"Check completed: {result.FilesNeedingFormatting.Count} files need formatting"
                : $"Formatted {result.FilesFormatted.Count} files successfully";

            return JsonSerializer.Serialize(
                new FormatCodeResponse(
                    result.TotalFilesChecked,
                    result.FilesNeedingFormatting.Count,
                    args.CheckOnly ? 0 : result.FilesFormatted.Count,
                    args.CheckOnly,
                    result.FilesNeedingFormatting.Take(50).ToList(),
                    message
                ),
                OverlordJsonContext.Default.FormatCodeResponse
            );
        } catch (Exception ex) {
            LogFormatCodeFailed(ex, args.Path);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteReindexChangedFiles(string argumentsJson, string? projectContext, CancellationToken cancellationToken) {
        try {
            var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.ReindexChangedFilesArgs);
            if (args == null || args.Files == null || args.Files.Length == 0) {
                return JsonSerializer.Serialize(new ErrorResponse("No files provided for reindexing"), OverlordJsonContext.Default.ErrorResponse);
            }

            var project = args.Project ?? projectContext ?? "unknown";
            var branch = args.Branch ?? "main";
            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            LogReindexing(args.Files.Length, project, branch);

            foreach (var fileData in args.Files) {
                try {
                    if (fileData.Vector == null || fileData.Vector.Length == 0) {
                        errors.Add($"{fileData.FilePath}: Missing vector");
                        errorCount++;
                        continue;
                    }

                    await _vectorStore.StoreVectorsAsync(
                        project: project,
                        branch: branch,
                        filePath: fileData.FilePath,
                        vectors: fileData.Vector,
                        content: fileData.Content,
                        symbols: fileData.Symbols,
                        cancellationToken: cancellationToken
                    );

                    successCount++;
                } catch (Exception ex) {
                    LogReindexFileFailed(ex, fileData.FilePath);
                    errors.Add($"{fileData.FilePath}: {ex.Message}");
                    errorCount++;
                }
            }

            return JsonSerializer.Serialize(
                new ReindexChangedFilesResponse(
                    project,
                    branch,
                    args.Files.Length,
                    successCount,
                    errorCount,
                    errors.Take(10).ToList(),
                    $"Reindexed {successCount} of {args.Files.Length} files successfully"
                ),
                OverlordJsonContext.Default.ReindexChangedFilesResponse
            );
        } catch (Exception ex) {
            LogReindexFailed(ex);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteSemanticSearch(string argumentsJson, string? projectContext, CancellationToken cancellationToken) {
        try {
            var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.SemanticSearchArgs);
            if (args == null || string.IsNullOrEmpty(args.Query)) {
                return JsonSerializer.Serialize(new ErrorResponse("Query is required"), OverlordJsonContext.Default.ErrorResponse);
            }

            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();
            if (embeddingService == null) {
                return JsonSerializer.Serialize(
                    new ErrorWithHintResponse("Embedding service not configured", "Start Overlord with --embedding-url parameter to enable semantic search"),
                    OverlordJsonContext.Default.ErrorWithHintResponse
                );
            }

            LogComputingEmbeddingForQuery(args.Query);
            var queryVector = await embeddingService.GetEmbeddingAsync(args.Query, cancellationToken);

            if (queryVector == null || queryVector.Length == 0) {
                return JsonSerializer.Serialize(
                    new ErrorWithHintResponse("Failed to compute embedding for query", "Embedding service may be unavailable"),
                    OverlordJsonContext.Default.ErrorWithHintResponse
                );
            }

            var matches = await _vectorStore.SearchAcrossProjectsAsync(
                queryVector: queryVector,
                threshold: args.MinSimilarity,
                limit: args.TopK,
                projects: args.Scope == "current_project" && !string.IsNullOrEmpty(projectContext) ? new[] { projectContext } : null,
                cancellationToken: cancellationToken
            );

            var results = matches.Select(m => new CodeMatchResult(
                m.Project,
                m.Branch,
                m.FilePath,
                m.Line,
                Math.Round(m.Similarity, 4),
                m.Code?.Length > 200 ? string.Concat(m.Code.AsSpan(0, 200), "...") : m.Code
            ));

            return JsonSerializer.Serialize(
                new SemanticSearchResponse(args.Query, args.Scope, args.TopK, args.MinSimilarity, matches.Count, results),
                OverlordJsonContext.Default.SemanticSearchResponse
            );
        } catch (Exception ex) {
            LogSemanticSearchFailed(ex);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteSemanticDiff(string argumentsJson, CancellationToken cancellationToken) {
        try {
            var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.SemanticDiffArgs);
            if (args == null || string.IsNullOrEmpty(args.Code1) || string.IsNullOrEmpty(args.Code2)) {
                return JsonSerializer.Serialize(new ErrorResponse("Both Code1 and Code2 are required"), OverlordJsonContext.Default.ErrorResponse);
            }

            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();
            if (embeddingService == null) {
                return JsonSerializer.Serialize(
                    new ErrorWithHintResponse("Embedding service not configured", "Start Overlord with --embedding-url parameter"),
                    OverlordJsonContext.Default.ErrorWithHintResponse
                );
            }

            LogComputingEmbeddingsForDiff();
            var vector1Task = embeddingService.GetEmbeddingAsync(args.Code1, cancellationToken);
            var vector2Task = embeddingService.GetEmbeddingAsync(args.Code2, cancellationToken);

            await Task.WhenAll(vector1Task, vector2Task);

            var vector1 = vector1Task.Result;
            var vector2 = vector2Task.Result;

            if (vector1 == null || vector2 == null || vector1.Length == 0 || vector2.Length == 0) {
                return JsonSerializer.Serialize(new ErrorResponse("Failed to compute embeddings"), OverlordJsonContext.Default.ErrorResponse);
            }

            var similarity = CosineSimilarity(vector1, vector2);
            var interpretation = similarity switch {
                >= 0.9 => "Very similar (likely same functionality)",
                >= 0.7 => "Similar (related functionality)",
                >= 0.5 => "Somewhat similar",
                _ => "Different",
            };

            return JsonSerializer.Serialize(
                new SemanticDiffResponse(args.Code1.Length, args.Code2.Length, Math.Round(similarity, 4), interpretation),
                OverlordJsonContext.Default.SemanticDiffResponse
            );
        } catch (Exception ex) {
            LogSemanticDiffFailed(ex);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecuteDetectCodeClones(string argumentsJson, string? projectContext, CancellationToken cancellationToken) {
        try {
            var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.DetectCodeClonesArgs);
            if (args == null) {
                return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments"), OverlordJsonContext.Default.ErrorResponse);
            }

            LogDetectCodeClonesRedirect(args.MinSimilarity, args.Mode, args.MembersOnly, args.MaxGroups);

            return JsonSerializer.Serialize(
                new DetectCodeClonesResponse(
                    "detect_code_clones requires local execution with loaded solution",
                    "LOCAL",
                    "This tool performs batch analysis on entire codebase (resource-intensive)",
                    new DetectCodeClonesExplanation(
                        "Scan ALL code entities, group similar code, provide refactoring recommendations",
                        "Full Roslyn semantic model and SemanticSearchService with indexed solution",
                        "Use find_duplicates for targeted duplicate search across projects (works on Overlord)"
                    ),
                    "Ensure Droid is running in Hybrid mode with loaded solution, ToolRouter will handle routing"
                ),
                OverlordJsonContext.Default.DetectCodeClonesResponse
            );
        } catch (Exception ex) {
            LogDetectCodeClonesFailed(ex);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private async Task<string> ExecutePatternSearch(string argumentsJson, string? projectContext, CancellationToken cancellationToken) {
        try {
            var args = JsonSerializer.Deserialize(argumentsJson, OverlordJsonContext.Default.PatternSearchArgs);
            if (args == null || string.IsNullOrEmpty(args.Pattern)) {
                return JsonSerializer.Serialize(new ErrorResponse("Pattern is required"), OverlordJsonContext.Default.ErrorResponse);
            }

            if (args.Mode == "semantic" || args.Mode == "hybrid") {
                var semanticArgs = new SemanticSearchArgs {
                    Query = args.Pattern,
                    Scope = args.Scope ?? "solution",
                    TopK = args.Limit,
                    MinSimilarity = 0.7,
                };

                return await ExecuteSemanticSearch(
                    JsonSerializer.Serialize(semanticArgs, OverlordJsonContext.Default.SemanticSearchArgs),
                    projectContext,
                    cancellationToken
                );
            }

            return JsonSerializer.Serialize(
                new PatternSearchResponse(
                    "pattern_search in entity/content mode requires local Roslyn access",
                    "Use semantic mode for cross-project pattern search via Overlord",
                    "semantic"
                ),
                OverlordJsonContext.Default.PatternSearchResponse
            );
        } catch (Exception ex) {
            LogPatternSearchFailed(ex);
            return JsonSerializer.Serialize(new ErrorResponse(ex.Message), OverlordJsonContext.Default.ErrorResponse);
        }
    }

    private static double CosineSimilarity(float[] vector1, float[] vector2) {
        if (vector1.Length != vector2.Length) {
            throw new ArgumentException("Vectors must have same dimension");
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++) {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0) {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }
}

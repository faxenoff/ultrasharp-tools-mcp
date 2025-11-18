using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Mcp;
using UltrasharpTools.Tools.Mcp.Tools;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация MCP proxy - выполняет MCP tools на стороне сервера
/// </summary>
public sealed class McpProxyService : IMcpProxyService
{
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
        IServiceProvider serviceProvider)
    {
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
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing proxied tool: {ToolName}", toolName);

        try
        {
            var result = toolName switch
            {
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
                _ => $"Unknown tool: {toolName}"
            };

            _logger.LogDebug("Tool {ToolName} executed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message, toolName });
        }
    }

    public Task<List<ToolInfo>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
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
            new() { Name = "pattern_search", Description = "Advanced pattern search with semantic mode" }
        };

        return Task.FromResult(tools);
    }

    private async Task<string> ExecuteLoadSolution(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<LoadSolutionArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.SolutionPath))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments" });
        }

        // Используем SolutionManager напрямую
        await _solutionManager.LoadSolutionAsync(args.SolutionPath, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            solutionPath = args.SolutionPath,
            message = "Solution loaded successfully on server"
        });
    }

    private async Task<string> ExecuteFindDuplicates(
        string argumentsJson,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<FindDuplicatesArgs>(argumentsJson);
        if (args == null)
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments" });
        }

        // Определяем query vector
        float[] queryVector;

        if (args.TargetVector != null && args.TargetVector.Length > 0)
        {
            // Vector передан явно (hybrid mode: Droid computed embedding locally)
            queryVector = args.TargetVector;
            _logger.LogDebug("Using pre-computed vector (length: {Length})", queryVector.Length);
        }
        else if (!string.IsNullOrEmpty(args.TargetCode))
        {
            // Попытка использовать IEmbeddingService если доступен
            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();

            if (embeddingService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "TargetVector required or embedding service must be configured.",
                    hint = "Either provide pre-computed vector or start Overlord with --embedding-url parameter."
                });
            }

            _logger.LogInformation("Computing embedding for TargetCode (length: {Length})", args.TargetCode.Length);
            var embedding = await embeddingService.GetEmbeddingAsync(args.TargetCode, cancellationToken);

            if (embedding == null || embedding.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Failed to compute embedding for TargetCode",
                    hint = "Embedding service may be unavailable or returned empty result."
                });
            }

            queryVector = embedding;
            _logger.LogDebug("Computed embedding via EmbeddingService (dimensions: {Dim})", queryVector.Length);
        }
        else
        {
            return JsonSerializer.Serialize(new { error = "Either TargetCode or TargetVector is required" });
        }

        // Поиск по MultiProjectVectorStore
        var matches = await _vectorStore.SearchAcrossProjectsAsync(
            queryVector: queryVector,
            threshold: args.Threshold,
            limit: args.Limit,
            projects: args.Scope == "current_project" && !string.IsNullOrEmpty(projectContext)
                ? new[] { projectContext }
                : null,
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            targetVectorDimension = queryVector.Length,
            scope = args.Scope,
            threshold = args.Threshold,
            matchCount = matches.Count,
            matches = matches.Select(m => new
            {
                project = m.Project,
                branch = m.Branch,
                file = m.FilePath,
                line = m.Line,
                similarity = Math.Round(m.Similarity, 4),
                code = m.Code?.Length > 200 ? m.Code.Substring(0, 200) + "..." : m.Code
            })
        });
    }

    private async Task<string> ExecuteViewDefinition(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ViewDefinitionArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.Fqn))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments: FQN required" });
        }

        if (!_symbolResolution.IsSolutionLoaded)
        {
            return JsonSerializer.Serialize(new { error = "No solution loaded on server" });
        }

        try
        {
            // Резолвим FQN → ISymbol
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null)
            {
                return JsonSerializer.Serialize(new { error = $"Symbol '{args.Fqn}' not found in loaded solution" });
            }

            // Получаем необходимые сервисы
            var sourceResolutionService = _serviceProvider.GetRequiredService<ISourceResolutionService>();
            var logger = _serviceProvider.GetRequiredService<ILogger<AnalysisToolsLogCategory>>();

            // Получаем source code символа
            var solution = _solutionManager.CurrentSolution;
            var locations = symbol.Locations.Where(l => l.IsInSource).ToList();

            if (!locations.Any())
            {
                // Try external source resolution
                var sourceResult = await sourceResolutionService.ResolveSourceAsync(symbol, cancellationToken);
                if (sourceResult != null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        fqn = args.Fqn,
                        filePath = sourceResult.FilePath,
                        source = sourceResult.Source,
                        resolutionMethod = sourceResult.ResolutionMethod
                    });
                }

                return JsonSerializer.Serialize(new { error = $"No source definition found for '{args.Fqn}'" });
            }

            // Получаем source из первой локации
            var location = locations.First();
            if (location.SourceTree == null)
            {
                return JsonSerializer.Serialize(new { error = "Symbol location has no source tree" });
            }

            var document = solution.GetDocument(location.SourceTree);
            if (document == null)
            {
                return JsonSerializer.Serialize(new { error = "Could not find document for symbol" });
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree == null)
            {
                return JsonSerializer.Serialize(new { error = "Could not get syntax tree" });
            }

            var syntaxNode = syntaxTree.GetRoot(cancellationToken).FindNode(location.SourceSpan);
            var sourceText = syntaxNode.ToFullString();

            return JsonSerializer.Serialize(new
            {
                fqn = args.Fqn,
                filePath = document.FilePath ?? location.SourceTree?.FilePath,
                source = sourceText,
                line = location.GetLineSpan().StartLinePosition.Line + 1,
                resolutionMethod = "Roslyn"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute view_definition for {Fqn}", args.Fqn);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteFindReferences(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<FindReferencesArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.Fqn))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments: FQN required" });
        }

        if (!_symbolResolution.IsSolutionLoaded)
        {
            return JsonSerializer.Serialize(new { error = "No solution loaded on server" });
        }

        try
        {
            // Резолвим FQN → ISymbol
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null)
            {
                return JsonSerializer.Serialize(new { error = $"Symbol '{args.Fqn}' not found in loaded solution" });
            }

            // Находим все ссылки на символ
            var referencedSymbols = await _analysisService.FindReferencesAsync(symbol, cancellationToken);

            var references = new List<object>();
            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    var lineSpan = location.Location.GetLineSpan();
                    references.Add(new
                    {
                        filePath = lineSpan.Path,
                        line = lineSpan.StartLinePosition.Line + 1,
                        column = lineSpan.StartLinePosition.Character + 1
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                fqn = args.Fqn,
                referenceCount = references.Count,
                references
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute find_references for {Fqn}", args.Fqn);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteModifyCode(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ModifyCodeArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.Fqn) || string.IsNullOrEmpty(args.NewCode))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments: FQN and NewCode required" });
        }

        if (!_symbolResolution.IsSolutionLoaded)
        {
            return JsonSerializer.Serialize(new { error = "No solution loaded on server" });
        }

        try
        {
            // Резолвим FQN → ISymbol
            var symbol = await _symbolResolution.FindSymbolAsync(args.Fqn, cancellationToken);
            if (symbol == null)
            {
                return JsonSerializer.Serialize(new { error = $"Symbol '{args.Fqn}' not found in loaded solution" });
            }

            // Получаем syntax node для символа
            if (!symbol.DeclaringSyntaxReferences.Any())
            {
                return JsonSerializer.Serialize(new { error = $"Symbol '{args.Fqn}' has no declaring syntax references" });
            }

            var syntaxRef = symbol.DeclaringSyntaxReferences.First();
            var oldNode = await syntaxRef.GetSyntaxAsync(cancellationToken);

            // Парсим новый код
            var newNode = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseMemberDeclaration(args.NewCode);
            if (newNode == null)
            {
                return JsonSerializer.Serialize(new { error = "Failed to parse new code as member declaration" });
            }

            // Получаем document
            var location = symbol.Locations.First();
            var document = _solutionManager.CurrentSolution.GetDocument(location.SourceTree);
            if (document == null)
            {
                return JsonSerializer.Serialize(new { error = "Could not find document for symbol" });
            }

            // Выполняем замену
            var newSolution = await _modificationService.ReplaceNodeAsync(document.Id, oldNode, newNode, cancellationToken);

            // Применяем изменения (simplified - не проходит через Git)
            var lintingResult = await _modificationService.ApplyChangesAsync(
                newSolution,
                cancellationToken,
                $"Modified {symbol.Kind} '{args.Fqn}' via MCP proxy");

            var success = lintingResult.After == null || lintingResult.After.ErrorCount == 0;
            return JsonSerializer.Serialize(new
            {
                success,
                fqn = args.Fqn,
                message = $"Successfully modified {symbol.Kind} '{args.Fqn}'",
                changedFiles = lintingResult.ChangedFiles,
                errors = lintingResult.After?.ErrorCount ?? 0,
                warnings = lintingResult.After?.WarningCount ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute modify_code for {Fqn}", args.Fqn);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteAnalyzeComplexity(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<AnalyzeComplexityArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.Scope) || string.IsNullOrEmpty(args.Target))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments: Scope and Target required" });
        }

        if (!_symbolResolution.IsSolutionLoaded)
        {
            return JsonSerializer.Serialize(new { error = "No solution loaded on server" });
        }

        try
        {
            var scope = args.Scope.ToLower();
            if (!new[] { "method", "class", "project" }.Contains(scope))
            {
                return JsonSerializer.Serialize(new { error = $"Invalid scope '{args.Scope}'. Must be 'method', 'class', or 'project'." });
            }

            // Получаем IComplexityAnalysisService
            var complexityService = _serviceProvider.GetRequiredService<IComplexityAnalysisService>();

            var metrics = new Dictionary<string, object>();
            var recommendations = new List<string>();

            switch (scope)
            {
                case "method":
                    var methodSymbol = await _symbolResolution.FindSymbolAsync(args.Target, cancellationToken) as IMethodSymbol;
                    if (methodSymbol == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Target '{args.Target}' is not a method." });
                    }
                    await complexityService.AnalyzeMethodAsync(methodSymbol, metrics, recommendations, cancellationToken);
                    break;

                case "class":
                    var typeSymbol = await _symbolResolution.FindNamedTypeSymbolAsync(args.Target, cancellationToken);
                    if (typeSymbol == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Target '{args.Target}' is not a class or interface." });
                    }
                    await complexityService.AnalyzeTypeAsync(typeSymbol, metrics, recommendations, false, cancellationToken);
                    break;

                case "project":
                    var project = _solutionManager.GetProjectByName(args.Target);
                    if (project == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Project '{args.Target}' not found." });
                    }
                    await complexityService.AnalyzeProjectAsync(project, metrics, recommendations, false, cancellationToken);
                    break;
            }

            return JsonSerializer.Serialize(new
            {
                scope = args.Scope,
                target = args.Target,
                metrics,
                recommendations = recommendations.Distinct().OrderBy(r => r).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute analyze_complexity for {Target}", args.Target);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteFormatCode(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<FormatCodeArgs>(argumentsJson);
        if (args == null || string.IsNullOrEmpty(args.Path))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments: Path required" });
        }

        try
        {
            // Проверяем путь
            if (!Path.IsPathFullyQualified(args.Path))
            {
                return JsonSerializer.Serialize(new { error = $"Path must be absolute: {args.Path}" });
            }

            if (!File.Exists(args.Path) && !Directory.Exists(args.Path))
            {
                return JsonSerializer.Serialize(new { error = $"Path does not exist: {args.Path}" });
            }

            // Получаем IFormattingService
            var formattingService = _serviceProvider.GetRequiredService<IFormattingService>();

            // Выполняем форматирование
            var result = await formattingService.FormatAsync(args.Path, args.CheckOnly, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                totalFilesChecked = result.TotalFilesChecked,
                filesNeedingFormatting = result.FilesNeedingFormatting.Count,
                filesFormatted = args.CheckOnly ? 0 : result.FilesFormatted.Count,
                checkOnly = args.CheckOnly,
                filesNeedingFormattingList = result.FilesNeedingFormatting.Take(50).ToList(),
                message = args.CheckOnly
                    ? $"Check completed: {result.FilesNeedingFormatting.Count} files need formatting"
                    : $"Formatted {result.FilesFormatted.Count} files successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute format_code for {Path}", args.Path);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteReindexChangedFiles(
        string argumentsJson,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ReindexChangedFilesArgs>(argumentsJson);
            if (args == null || args.Files == null || args.Files.Length == 0)
            {
                return JsonSerializer.Serialize(new { error = "No files provided for reindexing" });
            }

            var project = args.Project ?? projectContext ?? "unknown";
            var branch = args.Branch ?? "main";
            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            _logger.LogInformation(
                "Reindexing {FileCount} files for {Project}/{Branch}",
                args.Files.Length, project, branch);

            foreach (var fileData in args.Files)
            {
                try
                {
                    if (fileData.Vector == null || fileData.Vector.Length == 0)
                    {
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
                        cancellationToken: cancellationToken);

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reindex {File}", fileData.FilePath);
                    errors.Add($"{fileData.FilePath}: {ex.Message}");
                    errorCount++;
                }
            }

            return JsonSerializer.Serialize(new
            {
                project,
                branch,
                totalFiles = args.Files.Length,
                successCount,
                errorCount,
                errors = errors.Take(10).ToList(),  // Limit errors in response
                message = $"Reindexed {successCount} of {args.Files.Length} files successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute reindex_changed_files");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteSemanticSearch(
        string argumentsJson,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonSerializer.Deserialize<SemanticSearchArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Query))
            {
                return JsonSerializer.Serialize(new { error = "Query is required" });
            }

            // Получаем embedding сервис
            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();
            if (embeddingService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Embedding service not configured",
                    hint = "Start Overlord with --embedding-url parameter to enable semantic search"
                });
            }

            // Векторизуем запрос
            _logger.LogInformation("Computing embedding for query: {Query}", args.Query);
            var queryVector = await embeddingService.GetEmbeddingAsync(args.Query, cancellationToken);

            if (queryVector == null || queryVector.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Failed to compute embedding for query",
                    hint = "Embedding service may be unavailable"
                });
            }

            // Поиск по векторному хранилищу
            var matches = await _vectorStore.SearchAcrossProjectsAsync(
                queryVector: queryVector,
                threshold: args.MinSimilarity,
                limit: args.TopK,
                projects: args.Scope == "current_project" && !string.IsNullOrEmpty(projectContext)
                    ? new[] { projectContext }
                    : null,
                cancellationToken: cancellationToken);

            return JsonSerializer.Serialize(new
            {
                query = args.Query,
                scope = args.Scope,
                topK = args.TopK,
                minSimilarity = args.MinSimilarity,
                resultsCount = matches.Count,
                results = matches.Select(m => new
                {
                    project = m.Project,
                    branch = m.Branch,
                    file = m.FilePath,
                    line = m.Line,
                    similarity = Math.Round(m.Similarity, 4),
                    code = m.Code?.Length > 200 ? m.Code.Substring(0, 200) + "..." : m.Code
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute semantic_search");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteSemanticDiff(
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonSerializer.Deserialize<SemanticDiffArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Code1) || string.IsNullOrEmpty(args.Code2))
            {
                return JsonSerializer.Serialize(new { error = "Both Code1 and Code2 are required" });
            }

            // Получаем embedding сервис
            var embeddingService = _serviceProvider.GetService<IEmbeddingService>();
            if (embeddingService == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Embedding service not configured",
                    hint = "Start Overlord with --embedding-url parameter"
                });
            }

            // Векторизуем оба фрагмента кода
            _logger.LogInformation("Computing embeddings for semantic diff");
            var vector1Task = embeddingService.GetEmbeddingAsync(args.Code1, cancellationToken);
            var vector2Task = embeddingService.GetEmbeddingAsync(args.Code2, cancellationToken);

            await Task.WhenAll(vector1Task, vector2Task);

            var vector1 = vector1Task.Result;
            var vector2 = vector2Task.Result;

            if (vector1 == null || vector2 == null || vector1.Length == 0 || vector2.Length == 0)
            {
                return JsonSerializer.Serialize(new { error = "Failed to compute embeddings" });
            }

            // Вычисляем косинусное сходство
            var similarity = CosineSimilarity(vector1, vector2);

            return JsonSerializer.Serialize(new
            {
                code1Length = args.Code1.Length,
                code2Length = args.Code2.Length,
                semanticSimilarity = Math.Round(similarity, 4),
                interpretation = similarity switch
                {
                    >= 0.9 => "Very similar (likely same functionality)",
                    >= 0.7 => "Similar (related functionality)",
                    >= 0.5 => "Somewhat similar",
                    _ => "Different"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute semantic_diff");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteDetectCodeClones(
        string argumentsJson,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonSerializer.Deserialize<DetectCodeClonesArgs>(argumentsJson);
            if (args == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid arguments" });
            }

            // detect_code_clones - это BATCH ANALYSIS tool (сканирует весь codebase)
            // Overlord не должен выполнять resource-intensive full scans
            // Этот tool должен быть выполнен ЛОКАЛЬНО на Droid с loaded solution
            // ToolRouter автоматически перенаправит на LOCAL execution

            _logger.LogInformation(
                "detect_code_clones called on Overlord - this should be routed to LOCAL. " +
                "minSimilarity={MinSimilarity}, mode={Mode}, membersOnly={MembersOnly}, maxGroups={MaxGroups}",
                args.MinSimilarity, args.Mode, args.MembersOnly, args.MaxGroups);

            return JsonSerializer.Serialize(new
            {
                error = "detect_code_clones requires local execution with loaded solution",
                toolType = "LOCAL",
                hint = "This tool performs batch analysis on entire codebase (resource-intensive)",
                explanation = new
                {
                    toolPurpose = "Scan ALL code entities, group similar code, provide refactoring recommendations",
                    requiresLocal = "Full Roslyn semantic model and SemanticSearchService with indexed solution",
                    alternative = "Use find_duplicates for targeted duplicate search across projects (works on Overlord)"
                },
                recommendation = "Ensure Droid is running in Hybrid mode with loaded solution, ToolRouter will handle routing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute detect_code_clones");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecutePatternSearch(
        string argumentsJson,
        string? projectContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonSerializer.Deserialize<PatternSearchArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Pattern))
            {
                return JsonSerializer.Serialize(new { error = "Pattern is required" });
            }

            // Если mode = "semantic" или "hybrid", используем semantic search
            if (args.Mode == "semantic" || args.Mode == "hybrid")
            {
                // Делегируем на semantic_search
                var semanticArgs = new SemanticSearchArgs
                {
                    Query = args.Pattern,
                    Scope = args.Scope ?? "solution",
                    TopK = args.Limit,
                    MinSimilarity = 0.7
                };

                return await ExecuteSemanticSearch(
                    JsonSerializer.Serialize(semanticArgs),
                    projectContext,
                    cancellationToken);
            }

            // Для entity/content режимов требуется Roslyn
            return JsonSerializer.Serialize(new
            {
                message = "pattern_search in entity/content mode requires local Roslyn access",
                hint = "Use semantic mode for cross-project pattern search via Overlord",
                supportedMode = "semantic"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute pattern_search");
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // Helper method для косинусного сходства
    private static double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            throw new ArgumentException("Vectors must have same dimension");
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    // DTOs для десериализации аргументов
    private sealed class LoadSolutionArgs
    {
        public string? SolutionPath { get; set; }
        public string? BuildConfiguration { get; set; }
    }

    private sealed class FindDuplicatesArgs
    {
        public string? TargetCode { get; set; }
        public float[]? TargetVector { get; set; }  // For hybrid mode: Droid sends pre-computed vector
        public double Threshold { get; set; } = 0.7;
        public string Scope { get; set; } = "current_project";
        public int Limit { get; set; } = 10;
    }

    private sealed class ViewDefinitionArgs
    {
        public string? Fqn { get; set; }
    }

    private sealed class FindReferencesArgs
    {
        public string? Fqn { get; set; }
    }

    private sealed class ModifyCodeArgs
    {
        public string? Fqn { get; set; }
        public string? NewCode { get; set; }
    }

    private sealed class AnalyzeComplexityArgs
    {
        public string? Scope { get; set; }  // "method", "class", or "project"
        public string? Target { get; set; }  // FQN or project name
    }

    private sealed class FormatCodeArgs
    {
        public string? Path { get; set; }
        public bool CheckOnly { get; set; } = true;
    }

    private sealed class ReindexChangedFilesArgs
    {
        public string? Project { get; set; }
        public string? Branch { get; set; }
        public FileVectorData[]? Files { get; set; }
    }

    private sealed class FileVectorData
    {
        public required string FilePath { get; set; }
        public float[]? Vector { get; set; }
        public string? Content { get; set; }
        public UltrasharpTools.Overlord.Models.Agent.SymbolInfoDto[]? Symbols { get; set; }
    }

    private sealed class SemanticSearchArgs
    {
        public string? Query { get; set; }
        public string Scope { get; set; } = "solution";
        public int TopK { get; set; } = 10;
        public double MinSimilarity { get; set; } = 0.7;
    }

    private sealed class SemanticDiffArgs
    {
        public string? Code1 { get; set; }
        public string? Code2 { get; set; }
    }

    private sealed class DetectCodeClonesArgs
    {
        public float MinSimilarity { get; set; } = 0.85f;
        public string Mode { get; set; } = "semantic";
        public bool MembersOnly { get; set; } = true;
        public int MaxGroups { get; set; } = 20;
    }

    private sealed class PatternSearchArgs
    {
        public string? Pattern { get; set; }
        public string? Mode { get; set; } = "semantic";  // entity, content, semantic, hybrid
        public string? Scope { get; set; }
        public int Limit { get; set; } = 10;
    }
}

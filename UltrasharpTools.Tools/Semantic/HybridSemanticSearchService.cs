using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Ipc;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Hybrid implementation of SemanticSearchService that delegates to VectorDBClient via IPC.
/// Used in IPC mode (Comm → Droid → Indexer) instead of local semantic processing.
/// </summary>
public sealed class HybridSemanticSearchService : ISemanticSearchService {
    private readonly VectorDBClient _vectorDBClient;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<HybridSemanticSearchService> _logger;
    private readonly SemanticSearchServiceConfig _config;

    private bool _isIndexed;

    /// <summary>
    /// Indicates whether semantic search is available (has valid indexer client).
    /// </summary>
    public bool IsAvailable => _vectorDBClient != null;

    public HybridSemanticSearchService(
        VectorDBClient vectorDBClient,
        ISolutionManager solutionManager,
        SemanticSearchServiceConfig? config = null,
        ILogger<HybridSemanticSearchService>? logger = null
    ) {
        _vectorDBClient = vectorDBClient;
        _solutionManager = solutionManager;
        _config = config ?? SemanticSearchServiceConfig.Default;
        _logger = logger ?? NullLogger<HybridSemanticSearchService>.Instance;
    }
    /// <summary>
    /// Индексировать текущий solution через VectorDB процесс.
    /// Читает код через Roslyn в Droid и отправляет в VectorDB для векторизации.
    /// </summary>
    public async Task IndexCurrentSolutionAsync(CancellationToken ct = default) {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null) {
            throw new InvalidOperationException(
                "No solution loaded. Call LoadSolutionAsync first."
            );
        }

        // Проверяем статус VectorDB - если уже есть документы, пропускаем индексацию
        try {
            await _vectorDBClient.ConnectAsync(ct);
            var status = await _vectorDBClient.GetStatusAsync(ct);
            if (status != null && status.IndexedCount > 0) {
                _logger.LogInformation(
                    "[Hybrid] VectorDB already has {Count} indexed documents, skipping re-indexing",
                    status.IndexedCount
                );
                _isIndexed = true;
                return;
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "[Hybrid] Could not check VectorDB status, proceeding with indexing");
        }

        var solution = _solutionManager.CurrentWorkspace.CurrentSolution;
        _logger.LogInformation(
            "[Hybrid] Starting semantic indexing of solution: {SolutionPath}",
            solution.FilePath ?? "(in-memory)"
        );

        var totalDocuments = 0;

        foreach (var project in solution.Projects) {
            _logger.LogDebug("[Hybrid] Indexing project: {ProjectName}", project.Name);

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null) {
                _logger.LogWarning(
                    "[Hybrid] Failed to get compilation for project: {ProjectName}",
                    project.Name
                );
                continue;
            }

            foreach (var document in project.Documents) {
                if (!document.SupportsSyntaxTree)
                    continue;

                try {
                    await IndexDocumentAsync(document, compilation, ct);
                    totalDocuments++;
                } catch (Exception ex) {
                    _logger.LogError(
                        ex,
                        "[Hybrid] Error indexing document: {DocumentPath}",
                        document.FilePath
                    );
                }
            }
        }

        _isIndexed = true;

        _logger.LogInformation(
            "[Hybrid] Indexing complete: {Documents} documents indexed via VectorDB",
            totalDocuments
        );
    }    /// <summary>
         /// Индексирует один документ: извлекает методы и классы, отправляет в VectorDB
         /// </summary>
    private async Task IndexDocumentAsync(
        Document document,
        Compilation compilation,
        CancellationToken ct) {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree == null)
            return;

        var root = await syntaxTree.GetRootAsync(ct);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Извлечь методы
        var methodNodes = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .ToList();

        foreach (var methodNode in methodNodes) {
            try {
                await IndexMethodAsync(methodNode, semanticModel, document, ct);
            } catch (Exception ex) {
                _logger.LogWarning(
                    ex,
                    "[Hybrid] Failed to index method in {DocumentPath}",
                    document.FilePath
                );
            }
        }

        // Извлечь классы
        var classNodes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .ToList();

        foreach (var classNode in classNodes) {
            try {
                await IndexClassAsync(classNode, semanticModel, document, ct);
            } catch (Exception ex) {
                _logger.LogWarning(
                    ex,
                    "[Hybrid] Failed to index class in {DocumentPath}",
                    document.FilePath
                );
            }
        }

        _logger.LogDebug(
            "[Hybrid] Indexed document {DocumentPath}: {MethodCount} methods, {ClassCount} classes",
            document.FilePath,
            methodNodes.Count,
            classNodes.Count
        );
    }

    /// <summary>
    /// Индексирует один метод: извлекает текст и отправляет в VectorDB
    /// </summary>
    private async Task IndexMethodAsync(
        MethodDeclarationSyntax methodNode,
        SemanticModel semanticModel,
        Document document,
        CancellationToken ct) {
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode, cancellationToken: ct);
        if (methodSymbol == null)
            return;

        // Получить текст метода (сигнатура + body)
        var methodText = GetMethodText(methodNode);
        if (string.IsNullOrWhiteSpace(methodText))
            return;

        // Создать unique ID и metadata
        var id = $"method:{methodSymbol.ContainingType.ToDisplayString()}::{methodSymbol.Name}";
        var metadata = CreateMethodMetadata(methodSymbol, document);

        // Отправить в VectorDB через IPC
        await _vectorDBClient.IndexCodeAsync(
            code: methodText,
            documentPath: id,
            metadata: metadata,
            cancellationToken: ct
        );
    }

    /// <summary>
    /// Индексирует один класс: извлекает текст и отправляет в VectorDB
    /// </summary>
    private async Task IndexClassAsync(
        ClassDeclarationSyntax classNode,
        SemanticModel semanticModel,
        Document document,
        CancellationToken ct) {
        var classSymbol = semanticModel.GetDeclaredSymbol(classNode, cancellationToken: ct);
        if (classSymbol == null)
            return;

        // Получить текст класса (declaration + members signatures)
        var classText = GetClassText(classNode);
        if (string.IsNullOrWhiteSpace(classText))
            return;

        // Создать unique ID и metadata
        var id = $"class:{classSymbol.ToDisplayString()}";
        var metadata = CreateClassMetadata(classSymbol, document);

        // Отправить в VectorDB через IPC
        await _vectorDBClient.IndexCodeAsync(
            code: classText,
            documentPath: id,
            metadata: metadata,
            cancellationToken: ct
        );
    }

    /// <summary>
    /// Извлекает текст метода для embedding (сигнатура + тело)
    /// </summary>
    private static string GetMethodText(MethodDeclarationSyntax methodNode) {
        // Signature (return type, name, parameters)
        var signature = $"{methodNode.ReturnType} {methodNode.Identifier}{methodNode.ParameterList}";

        // Body (если есть)
        var body = methodNode.Body?.ToString() ?? methodNode.ExpressionBody?.ToString() ?? "";

        return $"{signature}\n{body}";
    }

    /// <summary>
    /// Извлекает текст класса для embedding (declaration + member signatures)
    /// </summary>
    private static string GetClassText(ClassDeclarationSyntax classNode) {
        var builder = new System.Text.StringBuilder();

        // Class declaration (modifiers, name, base types)
        builder.Append(classNode.Modifiers);
        builder.Append($" class {classNode.Identifier}");

        if (classNode.BaseList != null) {
            builder.Append($" {classNode.BaseList}");
        }

        builder.AppendLine("\n{");

        // Member signatures (fields, properties, methods)
        foreach (var member in classNode.Members) {
            if (member is FieldDeclarationSyntax field) {
                builder.AppendLine($"    {field.Declaration};");
            } else if (member is PropertyDeclarationSyntax property) {
                builder.AppendLine($"    {property.Type} {property.Identifier} {{ get; set; }}");
            } else if (member is MethodDeclarationSyntax method) {
                builder.AppendLine(
                    $"    {method.ReturnType} {method.Identifier}{method.ParameterList};"
                );
            }
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Создает JSON metadata для метода
    /// </summary>
    private static string CreateMethodMetadata(IMethodSymbol methodSymbol, Document document) {
        var location = methodSymbol.Locations.FirstOrDefault();
        var lineSpan = location?.GetLineSpan();

        var metadata = new {
            type = "method",
            name = methodSymbol.Name,
            fullyQualifiedName = methodSymbol.ToDisplayString(),
            containingType = methodSymbol.ContainingType?.ToDisplayString(),
            namespace_ = methodSymbol.ContainingNamespace?.ToDisplayString(),
            filePath = document.FilePath,
            lineNumber = lineSpan?.StartLinePosition.Line ?? 0,
            accessibility = methodSymbol.DeclaredAccessibility.ToString(),
            isAsync = methodSymbol.IsAsync,
            isStatic = methodSymbol.IsStatic,
        };

        return System.Text.Json.JsonSerializer.Serialize(metadata);
    }

    /// <summary>
    /// Создает JSON metadata для класса
    /// </summary>
    private static string CreateClassMetadata(INamedTypeSymbol classSymbol, Document document) {
        var location = classSymbol.Locations.FirstOrDefault();
        var lineSpan = location?.GetLineSpan();

        var metadata = new {
            type = "class",
            name = classSymbol.Name,
            fullyQualifiedName = classSymbol.ToDisplayString(),
            namespace_ = classSymbol.ContainingNamespace?.ToDisplayString(),
            filePath = document.FilePath,
            lineNumber = lineSpan?.StartLinePosition.Line ?? 0,
            accessibility = classSymbol.DeclaredAccessibility.ToString(),
            isAbstract = classSymbol.IsAbstract,
            isSealed = classSymbol.IsSealed,
            baseType = classSymbol.BaseType?.ToDisplayString(),
            interfaces = classSymbol.Interfaces.Select(i => i.ToDisplayString()).ToArray(),
        };

        return System.Text.Json.JsonSerializer.Serialize(metadata);
    }
    /// <summary>
    /// Переиндексировать конкретный project.
    /// </summary>
    public async Task ReindexProjectAsync(string projectName, CancellationToken ct = default) {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null) {
            throw new InvalidOperationException("No solution loaded. Call LoadSolutionAsync first.");
        }

        var solution = _solutionManager.CurrentWorkspace.CurrentSolution;
        var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);

        if (project == null) {
            _logger.LogWarning("[Hybrid] Project not found: {ProjectName}", projectName);
            return;
        }

        _logger.LogInformation("[Hybrid] Reindexing project {ProjectName} via VectorDB", projectName);
        await _vectorDBClient.ConnectAsync(ct);

        var compilation = await project.GetCompilationAsync(ct);
        if (compilation == null) {
            _logger.LogWarning("[Hybrid] Failed to get compilation for project: {ProjectName}", projectName);
            return;
        }

        var documentsIndexed = 0;
        foreach (var document in project.Documents) {
            if (!document.SupportsSyntaxTree)
                continue;

            try {
                await IndexDocumentAsync(document, compilation, ct);
                documentsIndexed++;
            } catch (Exception ex) {
                _logger.LogError(ex, "[Hybrid] Error reindexing document: {DocumentPath}", document.FilePath);
            }
        }

        _logger.LogInformation("[Hybrid] Reindexed {Count} documents in project {ProjectName}", documentsIndexed, projectName);
    }

    /// <summary>
    /// Инкрементально переиндексировать изменённые файлы.
    /// </summary>
    public async Task ReindexChangedFilesAsync(string[] filePaths, CancellationToken ct = default) {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null) {
            throw new InvalidOperationException("No solution loaded. Call LoadSolutionAsync first.");
        }

        _logger.LogInformation("[Hybrid] Incrementally reindexing {Count} changed files via VectorDB", filePaths.Length);
        await _vectorDBClient.ConnectAsync(ct);

        var solution = _solutionManager.CurrentWorkspace.CurrentSolution;
        var filesIndexed = 0;

        foreach (var filePath in filePaths) {
            // Найти документ по пути во всех проектах
            Document? document = null;
            Compilation? compilation = null;

            foreach (var project in solution.Projects) {
                document = project.Documents.FirstOrDefault(d =>
                    string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

                if (document != null) {
                    compilation = await project.GetCompilationAsync(ct);
                    break;
                }
            }

            if (document == null || compilation == null) {
                _logger.LogWarning("[Hybrid] Document not found in solution: {FilePath}", filePath);
                continue;
            }

            if (!document.SupportsSyntaxTree)
                continue;

            try {
                await IndexDocumentAsync(document, compilation, ct);
                filesIndexed++;
            } catch (Exception ex) {
                _logger.LogError(ex, "[Hybrid] Error reindexing file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("[Hybrid] Reindexed {Count} changed files", filesIndexed);
    }

    /// <summary>
    /// Удалить индекс для конкретных файлов.
    /// </summary>
    public async Task DeleteFileIndexesAsync(string[] filePaths, CancellationToken ct = default) {
        _logger.LogInformation("[Hybrid] Delete request for {Count} files", filePaths.Length);

        // NOTE: Explicit deletion is not required in VectorDB mode.
        // IndexCodeAsync automatically overwrites existing entries by ID (documentPath).
        // If files are deleted from disk, they will remain in vector store until next full reindex
        // or until ClearAsync() is called to clear the entire index.

        await Task.CompletedTask;
    }

    /// <summary>
    /// Проверить индексирован ли solution.
    /// </summary>
    public bool IsIndexed() => _isIndexed;

    /// <summary>
    /// Найти семантически похожий код (delegates to Indexer).
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarCodeAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    ) {
        _logger.LogDebug(
            "[Hybrid] Searching for similar code via Indexer: {Query}, topK={TopK}, minSimilarity={MinSimilarity}",
            query,
            topK,
            minSimilarity
        );

        // Делегируем в VectorDBClient
        var matches = await _vectorDBClient.SearchSimilarAsync(query, topK, minSimilarity, ct);

        // Конвертируем SimilarityMatch → SemanticCodeMatch
        var results = matches
            .Select((m, index) => new SemanticCodeMatch {
                Id = m.Id,
                FullyQualifiedName = m.Id, // В Indexer ID = documentPath или FQN
                Code = m.Content,
                Similarity = m.Similarity,
                Rank = index + 1,
                FilePath = m.Id, // Assuming ID is file path
                LineNumber = 0, // Not available from Indexer
                Type = CodeMatchType.Unknown, // Not available from Indexer
            })
            .ToList();

        _logger.LogDebug("[Hybrid] Found {Count} similar matches via Indexer", results.Count);

        return results;
    }

    /// <summary>
    /// Найти семантически похожие методы.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    ) {
        // В hybrid режиме делаем общий поиск
        // Фильтрация по типу (methods) пока не поддерживается
        _logger.LogDebug(
            "[Hybrid] Searching for similar methods via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Найти семантически похожие классы.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarClassesAsync(
        string query,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    ) {
        // В hybrid режиме делаем общий поиск
        _logger.LogDebug(
            "[Hybrid] Searching for similar classes via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Найти семантически похожие методы в конкретном файле.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsInFileAsync(
        string query,
        string filePath,
        int topK = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    ) {
        // В hybrid режиме делаем общий поиск
        _logger.LogDebug(
            "[Hybrid] Searching for similar methods in file via Indexer (fallback to general search)"
        );
        return await FindSimilarCodeAsync(query, topK, minSimilarity, ct);
    }

    /// <summary>
    /// Получить метрики индексатора.
    /// </summary>
    public CodeSemanticIndexerMetrics GetIndexerMetrics() {
        // Получаем метрики из Indexer
        // TODO: Использовать VectorDBClient.GetStatusAsync()
        return new CodeSemanticIndexerMetrics {
            IndexedMethods = 0,
            IndexedClasses = 0,
            IndexedDocuments = 0,
            EmbeddingMetrics = new EmbeddingMetrics {
                TotalRequests = 0,
                CacheHits = 0,
                CacheMisses = 0,
                CacheHitRate = 0,
                CacheSize = 0,
                CacheCapacity = 0,
                TotalEmbedTimeMs = 0,
                AverageEmbedTimeMs = 0,
                ProviderInfo = new Models.ProviderInfo {
                    Name = "indexer-hybrid",
                    Model = "delegated-to-indexer",
                    Dimension = 384,
                    MaxTokens = 0,
                    IsLocal = false,
                    Version = "3.0.6",
                },
            },
        };
    }

    public async ValueTask DisposeAsync() {
        _vectorDBClient?.Dispose();
        await Task.CompletedTask;
    }
}

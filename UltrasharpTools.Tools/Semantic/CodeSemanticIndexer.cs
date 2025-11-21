using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Индексатор для C# кода - создаёт semantic embeddings для методов и классов.
/// Интегрируется с VectorStore и EmbeddingGenerator для RAG поиска.
/// </summary>
public sealed class CodeSemanticIndexer : IAsyncDisposable
{
    private readonly VectorStore _vectorStore;
    private readonly EmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<CodeSemanticIndexer> _logger;
    private readonly CodeSemanticIndexerConfig _config;

    private long _indexedMethods;
    private long _indexedClasses;
    private long _indexedDocuments;

    public CodeSemanticIndexer(
        VectorStore vectorStore,
        EmbeddingGenerator embeddingGenerator,
        CodeSemanticIndexerConfig? config = null,
        ILogger<CodeSemanticIndexer>? logger = null
    )
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _config = config ?? CodeSemanticIndexerConfig.Default;
        _logger = logger ?? NullLogger<CodeSemanticIndexer>.Instance;
    }

    /// <summary>
    /// Индексировать весь Solution.
    /// </summary>
    public async Task IndexSolutionAsync(Solution solution, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting solution indexing: {SolutionPath}", solution.FilePath);

        var projects = solution.Projects.ToList();
        _logger.LogInformation("Found {ProjectCount} projects", projects.Count);

        foreach (var project in projects)
        {
            await IndexProjectAsync(project, ct);
        }

        _logger.LogInformation(
            "Solution indexing complete: {Methods} methods, {Classes} classes, {Documents} documents",
            _indexedMethods,
            _indexedClasses,
            _indexedDocuments
        );
    }

    /// <summary>
    /// Индексировать один Project.
    /// </summary>
    public async Task IndexProjectAsync(Project project, CancellationToken ct = default)
    {
        _logger.LogInformation("Indexing project: {ProjectName}", project.Name);

        var compilation = await project.GetCompilationAsync(ct);
        if (compilation == null)
        {
            _logger.LogWarning("Could not get compilation for project {ProjectName}", project.Name);
            return;
        }

        var documents = project
            .Documents.Where(d => d.SupportsSyntaxTree && d.FilePath != null)
            .ToList();

        // Batch processing для оптимизации
        for (int i = 0; i < documents.Count; i += _config.BatchSize)
        {
            var batch = documents.Skip(i).Take(_config.BatchSize).ToList();
            await IndexDocumentBatchAsync(batch, compilation, ct);
        }

        _logger.LogInformation("Project {ProjectName} indexed", project.Name);
    }

    /// <summary>
    /// Индексировать один Document.
    /// </summary>
    public async Task IndexDocumentAsync(
        Document document,
        Compilation compilation,
        CancellationToken ct = default
    )
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree == null)
        {
            return;
        }

        var root = await syntaxTree.GetRootAsync(ct);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Собрать все методы и классы
        var methodNodes = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

        var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        // Генерировать embeddings
        var embeddings = new List<VectorEmbedding>();

        // Методы
        if (_config.IndexMethods)
        {
            foreach (var methodNode in methodNodes)
            {
                var embedding = await CreateMethodEmbeddingAsync(
                    methodNode,
                    semanticModel,
                    document,
                    ct
                );

                if (embedding != null)
                {
                    embeddings.Add(embedding);
                }
            }
        }

        // Классы
        if (_config.IndexClasses)
        {
            foreach (var classNode in classNodes)
            {
                var embedding = await CreateClassEmbeddingAsync(
                    classNode,
                    semanticModel,
                    document,
                    ct
                );

                if (embedding != null)
                {
                    embeddings.Add(embedding);
                }
            }
        }

        // Batch insert
        if (embeddings.Count > 0)
        {
            await _vectorStore.InsertBatchAsync(embeddings, ct);
            Interlocked.Add(ref _indexedMethods, methodNodes.Count);
            Interlocked.Add(ref _indexedClasses, classNodes.Count);
            Interlocked.Increment(ref _indexedDocuments);
        }

        _logger.LogDebug(
            "Indexed document {DocumentPath}: {MethodCount} methods, {ClassCount} classes",
            document.FilePath,
            methodNodes.Count,
            classNodes.Count
        );
    }

    /// <summary>
    /// Переиндексировать один Document (удалить старый индекс + создать новый).
    /// Используется для инкрементальных обновлений.
    /// </summary>
    public async Task ReindexDocumentAsync(
        Document document,
        Compilation compilation,
        CancellationToken ct = default
    )
    {
        _logger.LogDebug("Reindexing document: {DocumentPath}", document.FilePath);

        // Удалить старый индекс для этого файла
        await DeleteDocumentIndexAsync(document.FilePath ?? "", ct);

        // Создать новый индекс
        await IndexDocumentAsync(document, compilation, ct);

        _logger.LogDebug("Reindexed document: {DocumentPath}", document.FilePath);
    }

    /// <summary>
    /// Переиндексировать несколько документов (batch).
    /// </summary>
    public async Task ReindexDocumentsAsync(
        IEnumerable<Document> documents,
        Compilation compilation,
        CancellationToken ct = default
    )
    {
        var documentList = documents.ToList();
        _logger.LogInformation("Batch reindexing {Count} documents", documentList.Count);

        foreach (var document in documentList)
        {
            await ReindexDocumentAsync(document, compilation, ct);
        }

        _logger.LogInformation("Batch reindex complete: {Count} documents", documentList.Count);
    }

    /// <summary>
    /// Удалить индекс для конкретного файла.
    /// </summary>
    public async Task DeleteDocumentIndexAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        _logger.LogDebug("Deleting index for file: {FilePath}", filePath);

        // VectorStore не поддерживает bulk delete по metadata
        // Временное решение: при переиндексации старые embeddings заменятся
        // благодаря тому, что ID включает FQN (который не меняется при редактировании)
        // TODO: Добавить DeleteByMetadataAsync в VectorStore для эффективного удаления
    }

    /// <summary>
    /// Поиск похожих методов по коду.
    /// </summary>
    public async Task<List<SimilarityResult>> SearchSimilarMethodsAsync(
        string code,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        // Генерировать embedding для query
        var queryVector = await _embeddingGenerator.EmbedAsync(code, ct);

        // Поиск через VectorStore
        var results = await _vectorStore.SearchAsync(queryVector, limit, minSimilarity, ct);

        return results.Where(r => r.Metadata?.Contains("\"type\":\"method\"") == true).ToList();
    }

    /// <summary>
    /// Поиск похожих классов по коду.
    /// </summary>
    public async Task<List<SimilarityResult>> SearchSimilarClassesAsync(
        string code,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        // Генерировать embedding для query
        var queryVector = await _embeddingGenerator.EmbedAsync(code, ct);

        // Поиск через VectorStore
        var results = await _vectorStore.SearchAsync(queryVector, limit, minSimilarity, ct);

        return results.Where(r => r.Metadata?.Contains("\"type\":\"class\"") == true).ToList();
    }

    /// <summary>
    /// Получить метрики индексации.
    /// </summary>
    public CodeSemanticIndexerMetrics GetMetrics()
    {
        return new CodeSemanticIndexerMetrics
        {
            IndexedMethods = Interlocked.Read(ref _indexedMethods),
            IndexedClasses = Interlocked.Read(ref _indexedClasses),
            IndexedDocuments = Interlocked.Read(ref _indexedDocuments),
            EmbeddingMetrics = _embeddingGenerator.GetMetrics(),
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _vectorStore.DisposeAsync();
        await _embeddingGenerator.DisposeAsync();
    }

    // Private helpers

    private async Task IndexDocumentBatchAsync(
        List<Document> documents,
        Compilation compilation,
        CancellationToken ct
    )
    {
        foreach (var document in documents)
        {
            await IndexDocumentAsync(document, compilation, ct);
        }
    }

    private async Task<VectorEmbedding?> CreateMethodEmbeddingAsync(
        MethodDeclarationSyntax methodNode,
        SemanticModel semanticModel,
        Document document,
        CancellationToken ct
    )
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);
        if (methodSymbol == null)
        {
            return null;
        }

        // Получить текст метода для embedding
        var methodText = GetMethodText(methodNode);

        // Генерировать embedding
        var vector = await _embeddingGenerator.EmbedAsync(methodText, ct);

        // Создать metadata
        var metadata = CreateMethodMetadata(methodSymbol, document);

        // Создать unique ID
        var id = $"method:{methodSymbol.ContainingType.ToDisplayString()}::{methodSymbol.Name}";

        return new VectorEmbedding
        {
            Id = id,
            Content = methodText,
            Vector = vector,
            Dimension = vector.Length,
            Metadata = metadata,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Provider = _embeddingGenerator.GetMetrics().ProviderInfo.Name,
        };
    }

    private async Task<VectorEmbedding?> CreateClassEmbeddingAsync(
        ClassDeclarationSyntax classNode,
        SemanticModel semanticModel,
        Document document,
        CancellationToken ct
    )
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
        if (classSymbol == null)
        {
            return null;
        }

        // Получить текст класса для embedding
        var classText = GetClassText(classNode);

        // Генерировать embedding
        var vector = await _embeddingGenerator.EmbedAsync(classText, ct);

        // Создать metadata
        var metadata = CreateClassMetadata(classSymbol, document);

        // Создать unique ID
        var id = $"class:{classSymbol.ToDisplayString()}";

        return new VectorEmbedding
        {
            Id = id,
            Content = classText,
            Vector = vector,
            Dimension = vector.Length,
            Metadata = metadata,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Provider = _embeddingGenerator.GetMetrics().ProviderInfo.Name,
        };
    }

    private string GetMethodText(MethodDeclarationSyntax methodNode)
    {
        // Для embedding используем:
        // 1. Signature (return type, name, parameters)
        // 2. XML doc comments (если есть)
        // 3. Method body (опционально)

        var parts = new List<string>();

        // XML doc
        var trivia = methodNode
            .GetLeadingTrivia()
            .Where(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            )
            .Select(t => t.ToString())
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(trivia))
        {
            parts.Add(trivia);
        }

        // Signature
        var signature =
            $"{methodNode.ReturnType} {methodNode.Identifier}{methodNode.ParameterList}";
        parts.Add(signature);

        // Body (если включено)
        if (_config.IncludeMethodBody && methodNode.Body != null)
        {
            parts.Add(methodNode.Body.ToString());
        }

        return string.Join("\n", parts);
    }

    private string GetClassText(ClassDeclarationSyntax classNode)
    {
        // Для embedding используем:
        // 1. Class signature
        // 2. XML doc comments
        // 3. Member signatures (не тела методов)

        var parts = new List<string>();

        // XML doc
        var trivia = classNode
            .GetLeadingTrivia()
            .Where(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            )
            .Select(t => t.ToString())
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(trivia))
        {
            parts.Add(trivia);
        }

        // Class signature
        var signature =
            $"{classNode.Modifiers} class {classNode.Identifier}{classNode.TypeParameterList}";
        if (classNode.BaseList != null)
        {
            signature += $" {classNode.BaseList}";
        }
        parts.Add(signature);

        // Member signatures
        if (_config.IncludeClassMembers)
        {
            foreach (var member in classNode.Members)
            {
                if (member is MethodDeclarationSyntax method)
                {
                    parts.Add($"{method.ReturnType} {method.Identifier}{method.ParameterList}");
                }
                else if (member is PropertyDeclarationSyntax property)
                {
                    parts.Add($"{property.Type} {property.Identifier}");
                }
            }
        }

        return string.Join("\n", parts);
    }

    private static string CreateMethodMetadata(IMethodSymbol methodSymbol, Document document)
    {
        // JSON metadata для фильтрации
        return $"{{\"type\":\"method\",\"name\":\"{methodSymbol.Name}\","
            + $"\"fqn\":\"{methodSymbol.ToDisplayString()}\","
            + $"\"file\":\"{document.FilePath}\","
            + $"\"line\":{methodSymbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0}}}";
    }

    private static string CreateClassMetadata(INamedTypeSymbol classSymbol, Document document)
    {
        // JSON metadata для фильтрации
        return $"{{\"type\":\"class\",\"name\":\"{classSymbol.Name}\","
            + $"\"fqn\":\"{classSymbol.ToDisplayString()}\","
            + $"\"file\":\"{document.FilePath}\","
            + $"\"line\":{classSymbol.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0}}}";
    }
}

/// <summary>
/// Конфигурация для CodeSemanticIndexer.
/// </summary>
public sealed record CodeSemanticIndexerConfig
{
    /// <summary>
    /// Индексировать методы.
    /// </summary>
    public bool IndexMethods { get; init; } = true;

    /// <summary>
    /// Индексировать классы.
    /// </summary>
    public bool IndexClasses { get; init; } = true;

    /// <summary>
    /// Включать тело метода в embedding (увеличивает точность, но замедляет).
    /// </summary>
    public bool IncludeMethodBody { get; init; } = true;

    /// <summary>
    /// Включать member signatures класса в embedding.
    /// </summary>
    public bool IncludeClassMembers { get; init; } = true;

    /// <summary>
    /// Размер batch для параллельной индексации.
    /// </summary>
    public int BatchSize { get; init; } = 10;

    public static CodeSemanticIndexerConfig Default => new();

    /// <summary>
    /// Конфигурация для быстрой индексации (только signatures).
    /// </summary>
    public static CodeSemanticIndexerConfig Fast =>
        new()
        {
            IncludeMethodBody = false,
            IncludeClassMembers = false,
            BatchSize = 20,
        };

    /// <summary>
    /// Конфигурация для детальной индексации (включая тела).
    /// </summary>
    public static CodeSemanticIndexerConfig Detailed =>
        new()
        {
            IncludeMethodBody = true,
            IncludeClassMembers = true,
            BatchSize = 5,
        };
}

/// <summary>
/// Метрики индексации.
/// </summary>
public sealed record CodeSemanticIndexerMetrics
{
    public required long IndexedMethods { get; init; }
    public required long IndexedClasses { get; init; }
    public required long IndexedDocuments { get; init; }
    public required EmbeddingMetrics EmbeddingMetrics { get; init; }
}

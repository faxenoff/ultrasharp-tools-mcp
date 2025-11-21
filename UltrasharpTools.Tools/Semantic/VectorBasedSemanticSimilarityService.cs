
using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Адаптер для ISemanticSimilarityService, использующий vector embeddings
/// вместо feature-based подхода.
///
/// Позволяет использовать CodeSemanticIndexer через существующий интерфейс.
/// </summary>
public sealed class VectorBasedSemanticSimilarityService : ISemanticSimilarityService
{
    private readonly SemanticSearchService _searchService;
    private readonly ILogger<VectorBasedSemanticSimilarityService> _logger;
    private readonly VectorBasedSemanticSimilarityConfig _config;

    public VectorBasedSemanticSimilarityService(
    SemanticSearchService searchService,
    VectorBasedSemanticSimilarityConfig? config = null,
    ILogger<VectorBasedSemanticSimilarityService>? logger = null)
    {
        _searchService = searchService;
        _config = config ?? VectorBasedSemanticSimilarityConfig.Default;
        _logger = logger ?? NullLogger<VectorBasedSemanticSimilarityService>.Instance;
    }

    /// <summary>
    /// Найти похожие методы во всём solution.
    /// </summary>
    public async Task<List<MethodSimilarityResult>> FindSimilarMethodsAsync(
    double similarityThreshold,
    CancellationToken cancellationToken)
    {
        // Убедиться что solution проиндексирован
        if (!_searchService.IsIndexed())
        {
            _logger.LogInformation("Solution not indexed, indexing now...");
            await _searchService.IndexCurrentSolutionAsync(cancellationToken);
        }

        // Для поиска "всех похожих методов" нужен базовый запрос
        // Возьмём пустой query или используем все методы
        // Но это не очень эффективно - вернём пустой результат
        // Этот метод предназначен для legacy API

        _logger.LogWarning(
        "FindSimilarMethodsAsync without query is not supported in vector-based approach. " +
        "Use SemanticSearchService.FindSimilarMethodsAsync with specific code instead.");

        return new List<MethodSimilarityResult>();
    }

    /// <summary>
    /// Найти похожие классы во всём solution.
    /// </summary>
    public async Task<List<ClassSimilarityResult>> FindSimilarClassesAsync(
    double similarityThreshold,
    CancellationToken cancellationToken)
    {
        // Аналогично методам - без конкретного query не можем искать

        _logger.LogWarning(
        "FindSimilarClassesAsync without query is not supported in vector-based approach. " +
        "Use SemanticSearchService.FindSimilarClassesAsync with specific code instead.");

        return new List<ClassSimilarityResult>();
    }

    /// <summary>
    /// Найти методы, похожие на данный код (расширенный метод).
    /// </summary>
    public async Task<List<MethodSemanticFeatures>> FindSimilarMethodsByCodeAsync(
    string code,
    double similarityThreshold,
    int limit,
    CancellationToken cancellationToken)
    {
        if (!_searchService.IsIndexed())
        {
            _logger.LogInformation("Solution not indexed, indexing now...");
            await _searchService.IndexCurrentSolutionAsync(cancellationToken);
        }

        var matches = await _searchService.FindSimilarMethodsAsync(
        code,
        limit,
        (float)similarityThreshold,
        cancellationToken);

        // Конвертировать в MethodSemanticFeatures
        return matches.Select(m => new MethodSemanticFeatures(
        fullyQualifiedMethodName: m.FullyQualifiedName ?? m.Id,
        filePath: m.FilePath ?? "",
        startLine: m.LineNumber,
        methodName: ExtractMethodName(m.FullyQualifiedName ?? m.Id),
        returnTypeName: "unknown", // Не доступно в vector-based подходе
        parameterTypeNames: new List<string>(),
        invokedMethodSignatures: new HashSet<string>(),
        basicBlockCount: 0,
        conditionalBranchCount: 0,
        loopCount: 0,
        cyclomaticComplexity: 0,
        operationCounts: new Dictionary<string, int>(),
        distinctAccessedMemberTypes: new HashSet<string>()
        )).ToList();
    }

    /// <summary>
    /// Найти классы, похожие на данный код (расширенный метод).
    /// </summary>
    public async Task<List<ClassSemanticFeatures>> FindSimilarClassesByCodeAsync(
    string code,
    double similarityThreshold,
    int limit,
    CancellationToken cancellationToken)
    {
        if (!_searchService.IsIndexed())
        {
            _logger.LogInformation("Solution not indexed, indexing now...");
            await _searchService.IndexCurrentSolutionAsync(cancellationToken);
        }

        var matches = await _searchService.FindSimilarClassesAsync(
        code,
        limit,
        (float)similarityThreshold,
        cancellationToken);

        // Конвертировать в ClassSemanticFeatures
        return matches.Select(m => new ClassSemanticFeatures(
        FullyQualifiedClassName: m.FullyQualifiedName ?? m.Id,
        FilePath: m.FilePath ?? "",
        StartLine: m.LineNumber,
        ClassName: ExtractMethodName(m.FullyQualifiedName ?? m.Id),
        BaseClassName: null,
        ImplementedInterfaceNames: new List<string>(),
        PublicMethodCount: 0,
        ProtectedMethodCount: 0,
        PrivateMethodCount: 0,
        StaticMethodCount: 0,
        AbstractMethodCount: 0,
        VirtualMethodCount: 0,
        PropertyCount: 0,
        ReadOnlyPropertyCount: 0,
        StaticPropertyCount: 0,
        FieldCount: 0,
        StaticFieldCount: 0,
        ReadonlyFieldCount: 0,
        ConstFieldCount: 0,
        EventCount: 0,
        NestedClassCount: 0,
        NestedStructCount: 0,
        NestedEnumCount: 0,
        NestedInterfaceCount: 0,
        AverageMethodComplexity: 0.0,
        DistinctReferencedExternalTypeFqns: new HashSet<string>(),
        DistinctUsedNamespaceFqns: new HashSet<string>(),
        TotalLinesOfCode: 0,
        MethodFeatures: new List<MethodSemanticFeatures>()
        )).ToList();
    }

    // Helper methods

    private static string ExtractMethodName(string fqn)
    {
        // Extract method name from FQN like "Namespace.Class::MethodName"
        var parts = fqn.Split(new[] { "::", "." }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : fqn;
    }
}

/// <summary>
/// Конфигурация для VectorBasedSemanticSimilarityService.
/// </summary>
public sealed record VectorBasedSemanticSimilarityConfig
{
    /// <summary>
    /// Similarity threshold по умолчанию.
    /// </summary>
    public double DefaultSimilarityThreshold { get; init; } = 0.7;

    /// <summary>
    /// Limit результатов по умолчанию.
    /// </summary>
    public int DefaultLimit { get; init; } = 10;

    public static VectorBasedSemanticSimilarityConfig Default => new();
}

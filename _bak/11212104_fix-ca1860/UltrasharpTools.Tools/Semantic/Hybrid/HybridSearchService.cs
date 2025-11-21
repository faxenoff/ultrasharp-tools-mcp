using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Semantic.Hybrid;

/// <summary>
/// Hybrid search service, объединяющий vector-based и feature-based поиск
/// через Reciprocal Rank Fusion (RRF).
///
/// Pipeline:
/// 1. Vector search (SemanticSearchService) - семантический поиск через embeddings
/// 2. Feature search (структурный анализ) - CFG, operations, signatures
/// 3. RRF fusion - объединение результатов с весами
/// </summary>
public sealed class HybridSearchService
{
    private readonly SemanticSearchService _vectorSearch;
    private readonly ISolutionManager _solutionManager;
    private readonly QueryFeatureExtractor _featureExtractor;
    private readonly ILogger<HybridSearchService> _logger;
    private readonly HybridSearchConfig _config;

    public HybridSearchService(
        SemanticSearchService vectorSearch,
        ISolutionManager solutionManager,
        QueryFeatureExtractor featureExtractor,
        HybridSearchConfig? config = null,
        ILogger<HybridSearchService>? logger = null
    )
    {
        _vectorSearch = vectorSearch;
        _solutionManager = solutionManager;
        _featureExtractor = featureExtractor;
        _config = config ?? HybridSearchConfig.Default;
        _logger = logger ?? NullLogger<HybridSearchService>.Instance;
    }

    /// <summary>
    /// Hybrid search для методов (vector + features).
    /// </summary>
    public async Task<List<HybridSearchResult>> SearchSimilarMethodsAsync(
        string queryCode,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        _logger.LogInformation("Starting hybrid search for methods (mode: {Mode})", _config.Mode);

        // 1. Vector search (если включен)
        List<SemanticCodeMatch>? vectorResults = null;
        if (_config.Mode is HybridSearchMode.VectorOnly or HybridSearchMode.Hybrid)
        {
            _logger.LogDebug("Running vector search...");
            vectorResults = await _vectorSearch.FindSimilarMethodsAsync(
                queryCode,
                _config.VectorSearchLimit,
                minSimilarity,
                ct
            );
            _logger.LogInformation("Vector search returned {Count} results", vectorResults.Count);
        }

        // 2. Feature search (если включен)
        List<MethodFeatureMatch>? featureResults = null;
        if (_config.Mode is HybridSearchMode.FeaturesOnly or HybridSearchMode.Hybrid)
        {
            _logger.LogDebug("Running feature search...");
            featureResults = await FeatureSearchMethodsAsync(
                queryCode,
                _config.FeatureSearchLimit,
                ct
            );
            _logger.LogInformation(
                "Feature search returned {Count} results",
                featureResults?.Count ?? 0
            );
        }

        // 3. Объединить результаты
        var hybridResults = _config.Mode switch
        {
            HybridSearchMode.VectorOnly => ConvertVectorResults(vectorResults!),
            HybridSearchMode.FeaturesOnly => ConvertFeatureResults(featureResults!),
            HybridSearchMode.Hybrid => FuseResults(vectorResults!, featureResults!),
            _ => throw new ArgumentOutOfRangeException(),
        };

        // 4. Ограничить до limit
        var finalResults = hybridResults.Take(limit).ToList();

        _logger.LogInformation("Hybrid search complete: {Count} results", finalResults.Count);

        return finalResults;
    }

    /// <summary>
    /// Hybrid search для классов (vector + features).
    /// </summary>
    public async Task<List<HybridSearchResult>> SearchSimilarClassesAsync(
        string queryCode,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        _logger.LogInformation("Starting hybrid search for classes (mode: {Mode})", _config.Mode);

        // Vector search
        List<SemanticCodeMatch>? vectorResults = null;
        if (_config.Mode is HybridSearchMode.VectorOnly or HybridSearchMode.Hybrid)
        {
            _logger.LogDebug("Running vector search...");
            vectorResults = await _vectorSearch.FindSimilarClassesAsync(
                queryCode,
                _config.VectorSearchLimit,
                minSimilarity,
                ct
            );
            _logger.LogInformation("Vector search returned {Count} results", vectorResults.Count);
        }

        // Feature search для классов пока не реализуем (оставим для будущего)
        // т.к. это более сложная задача

        // Возвращаем только vector results
        var results = ConvertVectorResults(vectorResults ?? new List<SemanticCodeMatch>())
            .Take(limit)
            .ToList();

        _logger.LogInformation("Hybrid search complete: {Count} results", results.Count);

        return results;
    }

    // Private implementation

    private async Task<List<MethodFeatureMatch>?> FeatureSearchMethodsAsync(
        string queryCode,
        int limit,
        CancellationToken ct
    )
    {
        try
        {
            // Извлечь features из query
            var queryFeatures = await _featureExtractor.ExtractMethodFeaturesAsync(queryCode, ct);
            if (queryFeatures == null)
            {
                _logger.LogWarning("Could not extract features from query code");
                return null;
            }

            // Получить все методы из solution
            var allMethodFeatures = await ExtractAllMethodFeaturesAsync(ct);
            if (allMethodFeatures.Count == 0)
            {
                _logger.LogWarning("No methods found in solution for feature comparison");
                return null;
            }

            _logger.LogInformation(
                "Comparing query with {MethodCount} methods from solution",
                allMethodFeatures.Count
            );

            // Вычислить similarity для каждого метода
            var similarities = allMethodFeatures
                .Select(method => new
                {
                    Method = method,
                    Similarity = CalculateFeatureSimilarity(queryFeatures, method),
                })
                .Where(x => x.Similarity >= _config.MinFeatureSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Take(limit)
                .Select(
                    (x, index) =>
                        new MethodFeatureMatch
                        {
                            Features = x.Method,
                            Similarity = x.Similarity,
                            Rank = index,
                        }
                )
                .ToList();

            return similarities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feature search failed");
            return null;
        }
    }

    private async Task<List<MethodSemanticFeatures>> ExtractAllMethodFeaturesAsync(
        CancellationToken ct
    )
    {
        var allFeatures = new List<MethodSemanticFeatures>();

        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null)
        {
            return allFeatures;
        }

        foreach (var project in _solutionManager.CurrentWorkspace.CurrentSolution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation == null)
                continue;

            foreach (var document in project.Documents.Where(d => d.SupportsSyntaxTree))
            {
                var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                if (syntaxTree == null)
                    continue;

                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);

                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

                foreach (var methodDecl in methods)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, ct);
                    if (methodSymbol == null || methodSymbol.IsAbstract)
                        continue;

                    // Извлечь features (упрощенная версия без сложного анализа)
                    var features = CreateBasicMethodFeatures(methodSymbol, methodDecl, document);
                    if (features != null)
                    {
                        allFeatures.Add(features);
                    }
                }
            }
        }

        return allFeatures;
    }

    private static MethodSemanticFeatures? CreateBasicMethodFeatures(
        IMethodSymbol methodSymbol,
        MethodDeclarationSyntax methodDecl,
        Document document
    )
    {
        return new MethodSemanticFeatures(
            fullyQualifiedMethodName: methodSymbol.ToDisplayString(),
            filePath: document.FilePath ?? "unknown",
            startLine: methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line,
            methodName: methodSymbol.Name,
            returnTypeName: methodSymbol.ReturnType.ToDisplayString(),
            parameterTypeNames: methodSymbol
                .Parameters.Select(p => p.Type.ToDisplayString())
                .ToList(),
            invokedMethodSignatures: new HashSet<string>(),
            basicBlockCount: 0,
            conditionalBranchCount: 0,
            loopCount: 0,
            cyclomaticComplexity: 1,
            operationCounts: new Dictionary<string, int>(),
            distinctAccessedMemberTypes: new HashSet<string>()
        );
    }

    private double CalculateFeatureSimilarity(
        MethodSemanticFeatures query,
        MethodSemanticFeatures candidate
    )
    {
        // Используем тот же алгоритм что и SemanticSimilarityService

        // Return type
        double returnTypeSimilarity =
            (query.ReturnTypeName == candidate.ReturnTypeName) ? 1.0 : 0.0;

        // Parameters
        double paramCountSimilarity =
            (query.ParameterTypeNames.Count == candidate.ParameterTypeNames.Count) ? 1.0 : 0.0;
        double paramTypeSimilarity = 0.0;
        if (query.ParameterTypeNames.Count == candidate.ParameterTypeNames.Count
            && query.ParameterTypeNames.Count() > 0)
        {
            int matchingParams = query
                .ParameterTypeNames.Zip(candidate.ParameterTypeNames, (a, b) => a == b ? 1 : 0)
                .Sum();
            paramTypeSimilarity = (double)matchingParams / query.ParameterTypeNames.Count;
        }
        else if (query.ParameterTypeNames.Count == 0 && candidate.ParameterTypeNames.Count == 0)
        {
            paramTypeSimilarity = 1.0;
        }

        // Invoked methods (Jaccard)
        double invokedSimilarity = 1.0;
        if (query.InvokedMethodSignatures.Any() || candidate.InvokedMethodSignatures.Count() > 0)
        {
            var intersection = query
                .InvokedMethodSignatures.Intersect(candidate.InvokedMethodSignatures)
                .Count();
            var union = query
                .InvokedMethodSignatures.Union(candidate.InvokedMethodSignatures)
                .Count();
            invokedSimilarity = union > 0 ? (double)intersection / union : 0.0;
        }

        // CFG features (normalized difference)
        const int maxBasicBlocks = 60;
        const int maxBranches = 25;
        const int maxLoops = 8;
        const int maxComplexity = 30;

        double basicBlockSimilarity =
            1.0
            - NormalizedDifference(
                query.BasicBlockCount,
                candidate.BasicBlockCount,
                maxBasicBlocks
            );
        double branchSimilarity =
            1.0
            - NormalizedDifference(
                query.ConditionalBranchCount,
                candidate.ConditionalBranchCount,
                maxBranches
            );
        double loopSimilarity =
            1.0 - NormalizedDifference(query.LoopCount, candidate.LoopCount, maxLoops);
        double complexitySimilarity =
            1.0
            - NormalizedDifference(
                query.CyclomaticComplexity,
                candidate.CyclomaticComplexity,
                maxComplexity
            );

        // Operation counts (cosine similarity)
        double operationSimilarity = CalculateCosineSimilarity(
            query.OperationCounts,
            candidate.OperationCounts
        );

        // Accessed types (Jaccard)
        double accessedTypesSimilarity = 1.0;
        if (query.DistinctAccessedMemberTypes.Any() || candidate.DistinctAccessedMemberTypes.Count() > 0)
        {
            var intersection = query
                .DistinctAccessedMemberTypes.Intersect(candidate.DistinctAccessedMemberTypes)
                .Count();
            var union = query
                .DistinctAccessedMemberTypes.Union(candidate.DistinctAccessedMemberTypes)
                .Count();
            accessedTypesSimilarity = union > 0 ? (double)intersection / union : 0.0;
        }

        // Weighted combination (те же веса что в SemanticSimilarityService)
        const double wReturnType = 0.05;
        const double wParamCount = 0.025;
        const double wParamTypes = 0.10;
        const double wInvoked = 0.25;
        const double wBasicBlocks = 0.05;
        const double wBranches = 0.05;
        const double wLoops = 0.05;
        const double wComplexity = 0.075;
        const double wOperations = 0.20;
        const double wAccessedTypes = 0.15;

        double totalScore =
            returnTypeSimilarity * wReturnType
            + paramCountSimilarity * wParamCount
            + paramTypeSimilarity * wParamTypes
            + invokedSimilarity * wInvoked
            + basicBlockSimilarity * wBasicBlocks
            + branchSimilarity * wBranches
            + loopSimilarity * wLoops
            + complexitySimilarity * wComplexity
            + operationSimilarity * wOperations
            + accessedTypesSimilarity * wAccessedTypes;

        const double totalWeight =
            wReturnType
            + wParamCount
            + wParamTypes
            + wInvoked
            + wBasicBlocks
            + wBranches
            + wLoops
            + wComplexity
            + wOperations
            + wAccessedTypes;

        return totalScore / totalWeight;
    }

    private static double NormalizedDifference(int val1, int val2, int maxValue)
    {
        if (maxValue == 0)
            return val1 == val2 ? 0.0 : 1.0;
        return Math.Abs(val1 - val2) / (double)maxValue;
    }

    private static double CalculateCosineSimilarity(
        Dictionary<string, int> vec1,
        Dictionary<string, int> vec2
    )
    {
        if (vec1.Count() == 0 && vec2.Count() == 0)
            return 1.0;
        if (vec1.Count() == 0 || vec2.Count() == 0)
            return 0.0;

        var allKeys = vec1.Keys.Union(vec2.Keys).ToList();

        long dotProduct = 0;
        long magnitude1Squared = 0;
        long magnitude2Squared = 0;

        foreach (var key in allKeys)
        {
            int val1 = vec1.GetValueOrDefault(key, 0);
            int val2 = vec2.GetValueOrDefault(key, 0);

            dotProduct += (long)val1 * val2;
            magnitude1Squared += (long)val1 * val1;
            magnitude2Squared += (long)val2 * val2;
        }

        if (magnitude1Squared == 0 || magnitude2Squared == 0)
            return 0.0;

        double magnitude1 = Math.Sqrt(magnitude1Squared);
        double magnitude2 = Math.Sqrt(magnitude2Squared);

        return dotProduct / (magnitude1 * magnitude2);
    }

    private List<HybridSearchResult> FuseResults(
        List<SemanticCodeMatch> vectorResults,
        List<MethodFeatureMatch> featureResults
    )
    {
        _logger.LogDebug(
            "Fusing {VectorCount} vector + {FeatureCount} feature results with RRF",
            vectorResults.Count,
            featureResults.Count
        );

        // Конвертировать в общий формат для RRF
        var vectorList = vectorResults
            .Select(v => (FQN: v.FullyQualifiedName ?? v.Id, Match: (object)v))
            .ToList();

        var featureList = featureResults
            .Select(f => (FQN: f.Features.FullyQualifiedMethodName, Match: (object)f))
            .ToList();

        // Применить RRF
        var rrfResults = RankFusion.WeightedReciprocalRankFusion(
            new[] { (vectorList, _config.VectorWeight), (featureList, _config.FeatureWeight) },
            item => item.FQN,
            _config.RrfConstant
        );

        // Конвертировать в HybridSearchResult
        return rrfResults
            .Select(rrf =>
            {
                // Найти исходные scores
                var vectorMatch = vectorResults.FirstOrDefault(v =>
                    (v.FullyQualifiedName ?? v.Id) == rrf.Id
                );
                var featureMatch = featureResults.FirstOrDefault(f =>
                    f.Features.FullyQualifiedMethodName == rrf.Id
                );

                return new HybridSearchResult
                {
                    Id = rrf.Id,
                    Code = vectorMatch?.Code ?? featureMatch?.Features.MethodName ?? "",
                    HybridScore = (float)rrf.RrfScore,
                    VectorScore = vectorMatch?.Similarity,
                    FeatureScore = featureMatch != null ? (float)featureMatch.Similarity : null,
                    Rank = rrf.FinalRank,
                    FilePath = vectorMatch?.FilePath ?? featureMatch?.Features.FilePath,
                    LineNumber = vectorMatch?.LineNumber ?? featureMatch?.Features.StartLine ?? 0,
                    FullyQualifiedName = rrf.Id,
                    Type = CodeMatchType.Method,
                    AppearanceCount = rrf.AppearanceCount,
                };
            })
            .ToList();
    }

    private static List<HybridSearchResult> ConvertVectorResults(List<SemanticCodeMatch> results)
    {
        return results
            .Select(
                (r, index) =>
                    new HybridSearchResult
                    {
                        Id = r.Id,
                        Code = r.Code,
                        HybridScore = r.Similarity,
                        VectorScore = r.Similarity,
                        FeatureScore = null,
                        Rank = index,
                        FilePath = r.FilePath,
                        LineNumber = r.LineNumber,
                        FullyQualifiedName = r.FullyQualifiedName ?? r.Id,
                        Type = r.Type,
                        AppearanceCount = 1,
                    }
            )
            .ToList();
    }

    private static List<HybridSearchResult> ConvertFeatureResults(List<MethodFeatureMatch> results)
    {
        return results
            .Select(
                (r, index) =>
                    new HybridSearchResult
                    {
                        Id = r.Features.FullyQualifiedMethodName,
                        Code = r.Features.MethodName,
                        HybridScore = (float)r.Similarity,
                        VectorScore = null,
                        FeatureScore = (float)r.Similarity,
                        Rank = index,
                        FilePath = r.Features.FilePath,
                        LineNumber = r.Features.StartLine,
                        FullyQualifiedName = r.Features.FullyQualifiedMethodName,
                        Type = CodeMatchType.Method,
                        AppearanceCount = 1,
                    }
            )
            .ToList();
    }
}

/// <summary>
/// Результат feature-based search для метода.
/// </summary>
internal sealed record MethodFeatureMatch
{
    public required MethodSemanticFeatures Features { get; init; }
    public required double Similarity { get; init; }
    public required int Rank { get; init; }
}

/// <summary>
/// Конфигурация для HybridSearchService.
/// </summary>
public sealed record HybridSearchConfig
{
    /// <summary>
    /// Режим поиска.
    /// </summary>
    public HybridSearchMode Mode { get; init; } = HybridSearchMode.Hybrid;

    /// <summary>
    /// Вес для vector search в RRF (0.0-1.0).
    /// </summary>
    public double VectorWeight { get; init; } = 0.7;

    /// <summary>
    /// Вес для feature search в RRF (0.0-1.0).
    /// </summary>
    public double FeatureWeight { get; init; } = 0.3;

    /// <summary>
    /// RRF константа (обычно 60).
    /// </summary>
    public int RrfConstant { get; init; } = 60;

    /// <summary>
    /// Limit для vector search (до RRF).
    /// </summary>
    public int VectorSearchLimit { get; init; } = 50;

    /// <summary>
    /// Limit для feature search (до RRF).
    /// </summary>
    public int FeatureSearchLimit { get; init; } = 50;

    /// <summary>
    /// Минимальная feature similarity для включения в результаты.
    /// </summary>
    public double MinFeatureSimilarity { get; init; } = 0.3;

    public static HybridSearchConfig Default => new();

    /// <summary>
    /// Приоритет векторному поиску (быстрее, семантически точнее).
    /// </summary>
    public static HybridSearchConfig VectorPriority =>
        new()
        {
            Mode = HybridSearchMode.Hybrid,
            VectorWeight = 0.8,
            FeatureWeight = 0.2,
        };

    /// <summary>
    /// Приоритет структурному поиску (точнее для рефакторинга).
    /// </summary>
    public static HybridSearchConfig FeaturePriority =>
        new()
        {
            Mode = HybridSearchMode.Hybrid,
            VectorWeight = 0.3,
            FeatureWeight = 0.7,
        };

    /// <summary>
    /// Сбалансированный режим.
    /// </summary>
    public static HybridSearchConfig Balanced =>
        new()
        {
            Mode = HybridSearchMode.Hybrid,
            VectorWeight = 0.5,
            FeatureWeight = 0.5,
        };
}

/// <summary>
/// Режим hybrid search.
/// </summary>
public enum HybridSearchMode
{
    /// <summary>
    /// Только vector-based search.
    /// </summary>
    VectorOnly,

    /// <summary>
    /// Только feature-based search.
    /// </summary>
    FeaturesOnly,

    /// <summary>
    /// Hybrid (vector + features с RRF).
    /// </summary>
    Hybrid,
}

/// <summary>
/// Результат hybrid search.
/// </summary>
public sealed record HybridSearchResult
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required float HybridScore { get; init; }
    public float? VectorScore { get; init; }
    public float? FeatureScore { get; init; }
    public required int Rank { get; init; }
    public string? FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string FullyQualifiedName { get; init; }
    public required CodeMatchType Type { get; init; }
    public int AppearanceCount { get; init; }
}

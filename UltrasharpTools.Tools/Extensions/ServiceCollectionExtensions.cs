using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Services;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Semantic.Embedding;
using UltrasharpTools.Tools.Semantic.Hybrid;
using UltrasharpTools.Tools.Semantic.GPU;
using UltrasharpTools.Tools.Merge;
using UltrasharpTools.Tools.Merge.Parsing;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Matching;
using UltrasharpTools.Tools.Merge.Engine;
using UltrasharpTools.Tools.Merge.Analysis;
using UltrasharpTools.Tools.Layered;
using System.Reflection;

namespace UltrasharpTools.Tools.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to register SharpTools services.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds all SharpTools services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithUltrasharpToolsServices(this IServiceCollection services, bool enableGit = true, string? buildConfiguration = null, GitOptions? gitOptions = null, SolutionReloadOptions? reloadOptions = null, SymbolCacheOptions? symbolCacheOptions = null) {
        services.AddSingleton<IFuzzyFqnLookupService, FuzzyFqnLookupService>();
        services.AddSingleton<ISolutionManager>(sp =>
            new SolutionManager(
                sp.GetRequiredService<ILogger<SolutionManager>>(),
                sp.GetRequiredService<IFuzzyFqnLookupService>(),
                buildConfiguration,
                reloadOptions,
                symbolCacheOptions,
                sp.GetService<LazyVectorStoreInitializer>(), // Optional: null if Semantic RAG not registered
                sp.GetService<LayeredIndexingOptions>(), // Optional: null if Layered Indexing not enabled
                sp.GetService<IGitService>() // Optional: null if Git not enabled
            )
        );
        // Register AnalysisCacheService (optional, for performance)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AnalysisCacheService>>();
            return new AnalysisCacheService(logger);
        });

        services.AddSingleton<ICodeAnalysisService>(sp =>
        {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetRequiredService<ILogger<CodeAnalysisService>>();
            var cacheService = sp.GetRequiredService<AnalysisCacheService>();
            return new CodeAnalysisService(solutionManager, logger, cacheService);
        });

        // Register GitOptions
        services.AddSingleton(gitOptions ?? new GitOptions());

        if (enableGit) {
            services.AddSingleton<IGitService, GitService>();
        } else {
            services.AddSingleton<IGitService, NoOpGitService>();
        }
        services.AddSingleton<ICodeModificationService, CodeModificationService>();
        services.AddSingleton<IEditorConfigProvider, EditorConfigProvider>();
        services.AddSingleton<IDocumentOperationsService, DocumentOperationsService>();
        services.AddSingleton<IComplexityAnalysisService, ComplexityAnalysisService>();
        services.AddSingleton<ISemanticSimilarityService, SemanticSimilarityService>();
        services.AddSingleton<ISourceResolutionService, SourceResolutionService>();
        services.AddSingleton<IExecutionTraceService, ExecutionTraceService>();
        services.AddSingleton<ICallGraphCacheService, CallGraphCacheService>();
        services.AddSingleton<IPdbSymbolResolver, PdbSymbolResolver>();
        services.AddSingleton<Z3ConstraintSolver>();
        services.AddSingleton<ISymbolicExecutionService, SymbolicExecutionService>();
        services.AddSingleton<IBacktraceService, BacktraceService>();
        services.AddSingleton<ILogAnalysisService, LogAnalysisService>();

        // Quality tools services (CSharpier formatting, Roslyn analyzers, code fixes)
        services.AddSingleton<IFormattingService, FormattingService>();
        services.AddSingleton<IDiagnosticService, DiagnosticService>();
        services.AddSingleton<ICodeFixService, CodeFixService>();
        services.AddSingleton<IQuickLintService, QuickLintService>();

        return services;
    }

    /// <summary>
    /// Adds all SharpTools services and tools to the MCP service builder.
    /// </summary>
    /// <param name="builder">The MCP service builder.</param>
    /// <returns>The MCP service builder for chaining.</returns>
    public static IMcpServerBuilder WithUltrasharpTools(this IMcpServerBuilder builder) {
        var toolAssembly = Assembly.Load("UltrasharpTools.Tools");

        return builder
            .WithToolsFromAssembly(toolAssembly);
    }

    /// <summary>
    /// Adds GPU detection and embedding services with auto-configuration.
    /// Automatically selects optimal provider based on GPU capabilities:
    /// - TEI (8192 tokens) for RTX 30xx+ (Compute Capability 8.0+)
    /// - Ollama (512 tokens) for older GPUs or non-NVIDIA
    /// - Memory (no ML) as fallback
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for embedding options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithEmbeddingServices(
        this IServiceCollection services,
        Action<EmbeddingOptions>? configure = null)
    {
        // Register embedding options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<EmbeddingOptions>()
                .BindConfiguration("Embedding");
        }

        // Register HttpClientFactory for TEI and Ollama
        services.AddHttpClient();

        // Register GPU detection service
        services.AddSingleton<IGPUDetectionService, GPUDetectionService>();

        // Register embedding provider factory
        services.AddSingleton<EmbeddingProviderFactory>();

        // Register IEmbeddingProvider (lazily created via factory)
        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var factory = sp.GetRequiredService<EmbeddingProviderFactory>();
            // Create provider synchronously (in production use IHostedService)
            return factory.CreateAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Adds Semantic RAG (Retrieval-Augmented Generation) services for code search.
    /// Provides vector-based semantic search using embeddings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Path to SQLite database for vector storage (default: in-memory).</param>
    /// <param name="dimension">Vector dimension (384, 768, or 1024) - must match embedding model.</param>
    /// <param name="configureEmbedding">Optional embedding provider configuration.</param>
    /// <param name="indexerConfig">Optional code indexer configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithSemanticRag(
        this IServiceCollection services,
        string? databasePath = null,
        int dimension = 768,
        Action<EmbeddingOptions>? configureEmbedding = null,
        CodeSemanticIndexerConfig? indexerConfig = null)
    {
        // Register VectorStore configuration
        // NOTE: databasePath будет resolved lazily через LazyVectorStoreInitializer
        // чтобы можно было использовать ProjectPathHelper с solutionPath
        var vectorStoreConfig = databasePath != null
            ? VectorStoreConfig.ForProduction(databasePath, dimension)
            : VectorStoreConfig.Default with { Dimension = dimension };

        services.AddSingleton(vectorStoreConfig);

        // Register VectorStore (not initialized yet)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<VectorStore>>();
            var config = sp.GetRequiredService<VectorStoreConfig>();
            return new VectorStore(config, logger);
        });

        // Register lazy initializer
        services.AddSingleton<LazyVectorStoreInitializer>();

        // Register embedding services (GPU detection, provider factory, provider)
        services.WithEmbeddingServices(configureEmbedding);

        // Register EmbeddingGenerator configuration
        services.AddSingleton(EmbeddingGeneratorConfig.Default);

        // Register EmbeddingGenerator
        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<IEmbeddingProvider>();
            var config = sp.GetRequiredService<EmbeddingGeneratorConfig>();
            var logger = sp.GetService<ILogger<EmbeddingGenerator>>();
            return new EmbeddingGenerator(provider, config, logger);
        });

        // Register CodeSemanticIndexer configuration
        services.AddSingleton(indexerConfig ?? CodeSemanticIndexerConfig.Default);

        // Register CodeSemanticIndexer
        services.AddSingleton(sp =>
        {
            var vectorStore = sp.GetRequiredService<VectorStore>();
            var embeddingGenerator = sp.GetRequiredService<EmbeddingGenerator>();
            var config = sp.GetRequiredService<CodeSemanticIndexerConfig>();
            var logger = sp.GetService<ILogger<CodeSemanticIndexer>>();
            return new CodeSemanticIndexer(vectorStore, embeddingGenerator, config, logger);
        });

        // Register SemanticSearchService
        services.AddSingleton(sp =>
        {
            var indexer = sp.GetRequiredService<CodeSemanticIndexer>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetService<ILogger<SemanticSearchService>>();
            return new SemanticSearchService(indexer, solutionManager, null, logger);
        });

        // Register QueryFeatureExtractor (для Hybrid Search)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<QueryFeatureExtractor>>();
            return new QueryFeatureExtractor(logger);
        });

        // Register HybridSearchService (Phase 4: Hybrid Search)
        services.AddSingleton(sp =>
        {
            var vectorSearch = sp.GetRequiredService<SemanticSearchService>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var featureExtractor = sp.GetRequiredService<QueryFeatureExtractor>();
            var logger = sp.GetService<ILogger<HybridSearchService>>();
            return new HybridSearchService(vectorSearch, solutionManager, featureExtractor, null, logger);
        });

        // Optionally: Replace ISemanticSimilarityService with vector-based implementation
        // services.AddSingleton<ISemanticSimilarityService>(sp =>
        // {
        //     var searchService = sp.GetRequiredService<SemanticSearchService>();
        //     var logger = sp.GetService<ILogger<VectorBasedSemanticSimilarityService>>();
        //     return new VectorBasedSemanticSimilarityService(searchService, null, logger);
        // });

        return services;
    }

    /// <summary>
    /// Adds Semantic Merge services for AI-powered 3-way merge.
    /// Requires Semantic RAG services (WithSemanticRag) to be registered first.
    /// Provides intelligent code merging using hybrid Fast Path (90%) + Slow Path (10%) matching.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithSemanticMerge(this IServiceCollection services)
    {
        // Parsing services
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<ContentNormalizer>>();
            return new ContentNormalizer(null, logger);
        });

        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<StructuralFingerprint>>();
            return new StructuralFingerprint(logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<CSharpParser>>();
            return new CSharpParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<JsonParser>>();
            return new JsonParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<XmlParser>>();
            return new XmlParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<YamlParser>>();
            return new YamlParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<PowerShellParser>>();
            return new PowerShellParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<ShellParser>>();
            return new ShellParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp =>
        {
            var csharpParser = sp.GetRequiredService<CSharpParser>();
            var jsonParser = sp.GetRequiredService<JsonParser>();
            var xmlParser = sp.GetRequiredService<XmlParser>();
            var yamlParser = sp.GetRequiredService<YamlParser>();
            var powershellParser = sp.GetRequiredService<PowerShellParser>();
            var shellParser = sp.GetRequiredService<ShellParser>();
            var logger = sp.GetService<ILogger<CodeUnitExtractor>>();
            return new CodeUnitExtractor(csharpParser, jsonParser, xmlParser, yamlParser, powershellParser, shellParser, logger);
        });

        // Indexing services
        services.AddSingleton(sp =>
        {
            var extractor = sp.GetRequiredService<CodeUnitExtractor>();
            var logger = sp.GetService<ILogger<MultiVersionIndexer>>();
            return new MultiVersionIndexer(extractor, logger);
        });

        services.AddSingleton(sp =>
        {
            var embeddingGenerator = sp.GetRequiredService<EmbeddingGenerator>();
            var logger = sp.GetService<ILogger<LazyEmbeddingGenerator>>();
            return new LazyEmbeddingGenerator(embeddingGenerator, logger);
        });

        // Matching services
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<FastPathMatcher>>();
            return new FastPathMatcher(logger);
        });

        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<SemanticMatcher>>();
            return new SemanticMatcher(logger);
        });

        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<MovementDetector>>();
            return new MovementDetector(logger);
        });

        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<StructuralAligner>>();
            return new StructuralAligner(logger);
        });

        // Merge engine
        services.AddSingleton(sp =>
        {
            var fastPathMatcher = sp.GetRequiredService<FastPathMatcher>();
            var semanticMatcher = sp.GetRequiredService<SemanticMatcher>();
            var movementDetector = sp.GetRequiredService<MovementDetector>();
            var embeddingGenerator = sp.GetRequiredService<LazyEmbeddingGenerator>();
            var logger = sp.GetService<ILogger<ThreeWayMerger>>();
            return new ThreeWayMerger(
                fastPathMatcher,
                semanticMatcher,
                movementDetector,
                embeddingGenerator,
                logger);
        });

        // Analysis services
        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<IntentClassifier>>();
            return new IntentClassifier(logger);
        });

        // Main Semantic Merge service
        services.AddSingleton(sp =>
        {
            var indexer = sp.GetRequiredService<MultiVersionIndexer>();
            var merger = sp.GetRequiredService<ThreeWayMerger>();
            var logger = sp.GetService<ILogger<SemanticMergeService>>();
            return new SemanticMergeService(indexer, merger, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds Layered Indexing services for branch-aware and multi-client symbol indexing.
    /// Provides three-layer architecture: Base (main branch), Branch Deltas, Working Directory (uncommitted).
    ///
    /// This is an opt-in feature that extends FastSymbolIndex with:
    /// - Branch isolation (different branches see different symbols)
    /// - Multi-client isolation (different clients have isolated uncommitted changes)
    /// - Incremental updates (changes visible in 100-500ms instead of 33s rebuild)
    /// - Persistent branch deltas (SQLite cache per branch)
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxBranchDeltas">Maximum number of branch deltas to keep in memory (LRU eviction). Default: 20.</param>
    /// <param name="enablePersistence">Enable persistent SQLite cache for branch deltas. Default: true.</param>
    /// <param name="deltaCompactionThreshold">Compact deltas larger than this number of changes. Default: 1000.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithLayeredIndexing(
        this IServiceCollection services,
        int maxBranchDeltas = 20,
        bool enablePersistence = true,
        int deltaCompactionThreshold = 1000)
    {
        // Register layered indexing configuration
        services.AddSingleton(new LayeredIndexingOptions
        {
            MaxBranchDeltas = maxBranchDeltas,
            EnablePersistence = enablePersistence,
            DeltaCompactionThreshold = deltaCompactionThreshold
        });

        // Register LayeredSymbolIndex (Phase 1.1 + 1.2)
        services.AddSingleton<ILayeredIndex>(sp =>
        {
            var baseIndex = sp.GetRequiredService<FastSymbolIndex>();
            var options = sp.GetRequiredService<LayeredIndexingOptions>();
            var gitService = sp.GetService<IGitService>(); // Optional: null if not registered
            var logger = sp.GetService<ILogger<LayeredSymbolIndex>>();
            return new LayeredSymbolIndex(baseIndex, options, gitService, logger);
        });

        // Note: Phase 6 services (DeltaCompactionService, OrphanedDeltaCleanupService,
        // BackgroundCleanupScheduler) require ILayeredIndex which is only available
        // after solution loads. These services are created manually after solution
        // loads, not registered in DI container.
        // Tests can create them directly with required parameters.

        return services;
    }
}
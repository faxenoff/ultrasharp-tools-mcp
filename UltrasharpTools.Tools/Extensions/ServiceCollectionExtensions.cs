using Microsoft.Extensions.Options;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Infrastructure.Cache;
using UltrasharpTools.Tools.Ipc;
using UltrasharpTools.Tools.Layered;
using UltrasharpTools.Tools.Merge;
using UltrasharpTools.Tools.Merge.Analysis;
using UltrasharpTools.Tools.Merge.Engine;
using UltrasharpTools.Tools.Merge.Git;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Matching;
using UltrasharpTools.Tools.Merge.Parsing;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Semantic.Embedding;
using UltrasharpTools.Tools.Semantic.GPU;
using UltrasharpTools.Tools.Replace.Interfaces;
using UltrasharpTools.Tools.Replace.Services;
using UltrasharpTools.Tools.Semantic.Hybrid;

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
    public static IServiceCollection WithUltrasharpToolsServices(
        this IServiceCollection services,
        bool enableGit = true,
        string? buildConfiguration = null,
        GitOptions? gitOptions = null,
        SolutionReloadOptions? reloadOptions = null,
        SymbolCacheOptions? symbolCacheOptions = null,
        bool lowMemoryMode = false
    ) {
        services.AddSingleton<IFuzzyFqnLookupService, FuzzyFqnLookupService>();
        services.AddSingleton<ISolutionManager>(sp => new SolutionManager(
            sp.GetRequiredService<ILogger<SolutionManager>>(),
            sp.GetRequiredService<IFuzzyFqnLookupService>(),
            buildConfiguration,
            reloadOptions,
            symbolCacheOptions,
            sp.GetService<LazyVectorStoreInitializer>(), // Optional: null if Semantic RAG not registered
            sp.GetService<LayeredIndexingOptions>(), // Optional: null if Layered Indexing not enabled
            sp.GetService<IGitService>(), // Optional: null if Git not enabled
            lowMemoryMode
        ));
        // Register AnalysisCacheService (optional, for performance)
        services.AddSingleton(sp => {
            var logger = sp.GetRequiredService<ILogger<AnalysisCacheService>>();
            return new AnalysisCacheService(logger);
        });

        services.AddSingleton<ICodeAnalysisService>(sp => {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetRequiredService<ILogger<CodeAnalysisService>>();
            var cacheService = sp.GetRequiredService<AnalysisCacheService>();
            return new CodeAnalysisService(solutionManager, logger, cacheService);
        });

        // Register CallGraphIndexer for background call graph indexing
        services.AddSingleton<CallGraphIndexer>(sp => {
            var codeAnalysisService = sp.GetRequiredService<ICodeAnalysisService>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetRequiredService<ILogger<CallGraphIndexer>>();
            return new CallGraphIndexer(codeAnalysisService, solutionManager, logger);
        });

        // Register GitOptions
        services.AddSingleton(gitOptions ?? new GitOptions());

        if (enableGit) {
            services.AddSingleton<IGitService, GitCliService>();
        } else {
            services.AddSingleton<IGitService, NoOpGitService>();
        }
        services.AddSingleton<ICodeModificationService>(sp => {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var gitService = sp.GetRequiredService<IGitService>();
            var quickLintService = sp.GetRequiredService<IQuickLintService>();
            var semanticSearchService = sp.GetService<ISemanticSearchService>(); // Optional - for auto-reindex
            var logger = sp.GetRequiredService<ILogger<CodeModificationService>>();
            return new CodeModificationService(solutionManager, gitService, quickLintService, semanticSearchService, logger);
        });
        services.AddSingleton<IEditorConfigProvider, EditorConfigProvider>();
        services.AddSingleton<ILoadingOrchestrator, LoadingOrchestrator>();
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

        // NuGet HTTP service (lightweight replacement for NuGet.Protocol)
        services.AddHttpClient("NuGetApi");
        services.AddSingleton<NuGetHttpService>();

        // Quality tools services (Roslyn formatting, analyzers, code fixes)
        services.AddSingleton<IFormattingService, FormattingService>();

        // Semantic enrichment services (Phase 1 + Phase 2) - registered before DiagnosticService
        services.AddSingleton<ISemanticDiagnosticEnricher>(sp => {
            var semanticModeProvider = sp.GetService<ISemanticModeProvider>(); // Nullable
            var logger = sp.GetRequiredService<ILogger<SemanticDiagnosticEnricher>>();
            return new SemanticDiagnosticEnricher(semanticModeProvider, logger);
        });

        services.AddSingleton<IEditorConfigGenerator>(sp => {
            var logger = sp.GetRequiredService<ILogger<EditorConfigGenerator>>();
            return new EditorConfigGenerator(logger);
        });

        services.AddSingleton<IDiagnosticService>(sp => {
            var logger = sp.GetRequiredService<ILogger<DiagnosticService>>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var semanticEnricher = sp.GetService<ISemanticDiagnosticEnricher>(); // Nullable
            var editorConfigGenerator = sp.GetService<IEditorConfigGenerator>(); // Nullable
            return new DiagnosticService(
                logger,
                solutionManager,
                semanticEnricher,
                editorConfigGenerator
            );
        });

        services.AddSingleton<ICodeFixService, CodeFixService>();
        services.AddSingleton<IQuickLintService, QuickLintService>();

        // Preview manager for code modification previews
        services.AddSingleton<UltrasharpTools.Tools.Preview.PreviewManager>();

        // Version manager for code snapshots
        services.AddSingleton<UltrasharpTools.Tools.Versioning.VersionManager>();

        // Import update service for auto-import management
        services.AddSingleton<ImportUpdateService>();

        return services;
    }

    /// <summary>
    /// Adds all SharpTools services and tools to the MCP service builder.
    /// </summary>
    /// <param name="builder">The MCP service builder.</param>
    /// <returns>The MCP service builder for chaining.</returns>
    public static IMcpServerBuilder WithUltrasharpTools(this IMcpServerBuilder builder) {
        var toolAssembly = Assembly.Load("UltrasharpTools.Tools");

        return builder.WithToolsFromAssembly(toolAssembly);
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
        Action<EmbeddingOptions>? configure = null
    ) {
        // Register embedding options
        if (configure != null) {
            services.Configure(configure);
        } else {
            services.AddOptions<EmbeddingOptions>().BindConfiguration("Embedding");
        }

        // Register HttpClientFactory for TEI and Ollama
        services.AddHttpClient();

        // Register GPU detection service
        services.AddSingleton<IGPUDetectionService, GPUDetectionService>();

        // Register embedding provider factory
        services.AddSingleton<EmbeddingProviderFactory>();

        // Register IEmbeddingProvider (lazily created via factory)
        services.AddSingleton<IEmbeddingProvider>(sp => {
            var factory = sp.GetRequiredService<EmbeddingProviderFactory>();
            // Create provider synchronously (in production use IHostedService)
            return factory.CreateAsync().GetAwaiter().GetResult();
        });

        // Register EmbeddingGenerator (wrapper with caching, used by SemanticMerge)
        services.AddSingleton<Semantic.EmbeddingGenerator>(sp => {
            var provider = sp.GetRequiredService<IEmbeddingProvider>();
            var logger = sp.GetService<ILogger<Semantic.EmbeddingGenerator>>();
            return new Semantic.EmbeddingGenerator(provider, null, logger);
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
        CodeSemanticIndexerConfig? indexerConfig = null
    ) {
        // Register VectorStore configuration
        // NOTE: databasePath будет resolved lazily через LazyVectorStoreInitializer
        // чтобы можно было использовать ProjectPathHelper с solutionPath
        var vectorStoreConfig =
            databasePath != null
                ? VectorStoreConfig.ForProduction(databasePath, dimension)
                : VectorStoreConfig.Default with {
                    Dimension = dimension,
                };

        services.AddSingleton(vectorStoreConfig);

        // Register VectorStore (not initialized yet)
        services.AddSingleton(sp => {
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
        services.AddSingleton(sp => {
            var provider = sp.GetRequiredService<IEmbeddingProvider>();
            var config = sp.GetRequiredService<EmbeddingGeneratorConfig>();
            var logger = sp.GetService<ILogger<EmbeddingGenerator>>();
            return new EmbeddingGenerator(provider, config, logger);
        });

        // Register CodeSemanticIndexer configuration
        services.AddSingleton(indexerConfig ?? CodeSemanticIndexerConfig.Default);

        // Register CodeSemanticIndexer
        services.AddSingleton(sp => {
            var vectorStore = sp.GetRequiredService<VectorStore>();
            var embeddingGenerator = sp.GetRequiredService<EmbeddingGenerator>();
            var config = sp.GetRequiredService<CodeSemanticIndexerConfig>();
            var logger = sp.GetService<ILogger<CodeSemanticIndexer>>();
            return new CodeSemanticIndexer(vectorStore, embeddingGenerator, config, logger);
        });

        // Register SemanticSearchService (both interface and concrete type)
        services.AddSingleton<SemanticSearchService>(sp => {
            var indexer = sp.GetRequiredService<CodeSemanticIndexer>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetService<ILogger<SemanticSearchService>>();
            return new SemanticSearchService(indexer, solutionManager, null, logger);
        });
        services.AddSingleton<ISemanticSearchService>(sp =>
            sp.GetRequiredService<SemanticSearchService>()
        );

        // Register QueryFeatureExtractor (для Hybrid Search)
        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<QueryFeatureExtractor>>();
            return new QueryFeatureExtractor(logger);
        });

        // Register HybridSearchService (Phase 4: Hybrid Search)
        services.AddSingleton(sp => {
            var vectorSearch = sp.GetRequiredService<SemanticSearchService>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var featureExtractor = sp.GetRequiredService<QueryFeatureExtractor>();
            var logger = sp.GetService<ILogger<HybridSearchService>>();
            return new HybridSearchService(
                vectorSearch,
                solutionManager,
                featureExtractor,
                null,
                logger
            );
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
    /// Adds Semantic RAG services using external Indexer process via IPC.
    /// This is a lightweight alternative to WithSemanticRag that delegates all semantic operations
    /// to a separate UltraSharpTools.Indexer.exe process via Named Pipe communication.
    /// 
    /// Benefits:
    /// - Lower memory usage in Droid process
    /// - AOT-compiled Indexer for better performance
    /// - Process isolation - Indexer crash doesn't affect Droid
    /// - Shared Indexer across multiple Droid instances
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="indexerPath">Optional custom path to Indexer executable. If null, uses default location.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithSemanticRagIndexer(
        this IServiceCollection services,
        string? indexerPath = null
    ) {
        // Register VectorDBClient for IPC communication
        services.AddSingleton(sp => {
            var logger = sp.GetRequiredService<ILogger<VectorDBClient>>();
            return new VectorDBClient(logger);
        });

        // Register HybridSemanticSearchService as the primary ISemanticSearchService implementation
        services.AddSingleton<ISemanticSearchService>(sp => {
            var indexerClient = sp.GetRequiredService<VectorDBClient>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetService<ILogger<HybridSemanticSearchService>>();
            return new HybridSemanticSearchService(
                indexerClient,
                solutionManager,
                config: null,
                logger
            );
        });

        // Also register as concrete type for direct access if needed
        services.AddSingleton(sp =>
            (HybridSemanticSearchService)sp.GetRequiredService<ISemanticSearchService>()
        );

        return services;
    }
    /// <summary>
    /// Adds Semantic Merge services for AI-powered 3-way merge.
    /// Requires Semantic RAG services (WithSemanticRag) to be registered first.
    /// Provides intelligent code merging using hybrid Fast Path (90%) + Slow Path (10%) matching.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithSemanticMerge(this IServiceCollection services) {
        // Parsing services
        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<ContentNormalizer>>();
            return new ContentNormalizer(null, logger);
        });

        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<StructuralFingerprint>>();
            return new StructuralFingerprint(logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<CSharpParser>>();
            return new CSharpParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<JsonParser>>();
            return new JsonParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<XmlParser>>();
            return new XmlParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<YamlParser>>();
            return new YamlParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<PowerShellParser>>();
            return new PowerShellParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var fingerprint = sp.GetRequiredService<StructuralFingerprint>();
            var normalizer = sp.GetRequiredService<ContentNormalizer>();
            var logger = sp.GetService<ILogger<ShellParser>>();
            return new ShellParser(fingerprint, normalizer, logger);
        });

        services.AddSingleton(sp => {
            var csharpParser = sp.GetRequiredService<CSharpParser>();
            var jsonParser = sp.GetRequiredService<JsonParser>();
            var xmlParser = sp.GetRequiredService<XmlParser>();
            var yamlParser = sp.GetRequiredService<YamlParser>();
            var powershellParser = sp.GetRequiredService<PowerShellParser>();
            var shellParser = sp.GetRequiredService<ShellParser>();
            var logger = sp.GetService<ILogger<CodeUnitExtractor>>();
            return new CodeUnitExtractor(
                csharpParser,
                jsonParser,
                xmlParser,
                yamlParser,
                powershellParser,
                shellParser,
                logger
            );
        });

        // Indexing services
        // CacheIntegrationService - фабрика для ленивого создания когда solution загружен
        services.AddSingleton<Func<CacheIntegrationService?>>(sp => () => {
            var solutionManager = sp.GetService<ISolutionManager>();
            var solutionPath = solutionManager?.CurrentSolution?.FilePath;
            if (!string.IsNullOrEmpty(solutionPath))
            {
                var cacheLogger = sp.GetService<ILogger<CacheIntegrationService>>();
                return new CacheIntegrationService(solutionPath, null, cacheLogger);
            }
            return null;
        });

        services.AddSingleton(sp => {
            var extractor = sp.GetRequiredService<CodeUnitExtractor>();
            var logger = sp.GetService<ILogger<MultiVersionIndexer>>();
            // CacheIntegrationService создаётся лениво через фабрику внутри MultiVersionIndexer
            var cacheFactory = sp.GetService<Func<CacheIntegrationService?>>();
            return new MultiVersionIndexer(extractor, logger, cacheFactory);
        });

        services.AddSingleton(sp => {
            // EmbeddingGenerator is optional - may not be available if Semantic RAG is not configured
            var embeddingGenerator = sp.GetService<EmbeddingGenerator>();
            var logger = sp.GetService<ILogger<LazyEmbeddingGenerator>>();
            return new LazyEmbeddingGenerator(embeddingGenerator!, logger);
        });

        // Matching services
        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<FastPathMatcher>>();
            return new FastPathMatcher(logger);
        });

        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<SemanticMatcher>>();
            return new SemanticMatcher(logger);
        });

        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<MovementDetector>>();
            return new MovementDetector(logger);
        });

        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<StructuralAligner>>();
            return new StructuralAligner(logger);
        });

        // Semantic Conflict Resolver
        services.AddSingleton(sp => {
            var embeddingGenerator = sp.GetRequiredService<LazyEmbeddingGenerator>();
            var logger = sp.GetService<ILogger<SemanticConflictResolver>>();
            return new SemanticConflictResolver(embeddingGenerator, logger);
        });

        // Merge engine
        services.AddSingleton(sp => {
            var fastPathMatcher = sp.GetRequiredService<FastPathMatcher>();
            var semanticMatcher = sp.GetRequiredService<SemanticMatcher>();
            var movementDetector = sp.GetRequiredService<MovementDetector>();
            var embeddingGenerator = sp.GetRequiredService<LazyEmbeddingGenerator>();
            var conflictResolver = sp.GetRequiredService<SemanticConflictResolver>();
            var logger = sp.GetService<ILogger<ThreeWayMerger>>();
            return new ThreeWayMerger(
                fastPathMatcher,
                semanticMatcher,
                movementDetector,
                embeddingGenerator,
                conflictResolver,
                logger
            );
        });

        // Analysis services
        services.AddSingleton(sp => {
            var logger = sp.GetService<ILogger<IntentClassifier>>();
            return new IntentClassifier(logger);
        });

        // Main Semantic Merge service
        services.AddSingleton(sp => {
            var indexer = sp.GetRequiredService<MultiVersionIndexer>();
            var merger = sp.GetRequiredService<ThreeWayMerger>();
            var logger = sp.GetService<ILogger<SemanticMergeService>>();
            return new SemanticMergeService(indexer, merger, logger);
        });

        // Branch Merge service (simplified API for merging git branches)
        services.AddSingleton(sp => {
            var mergeService = sp.GetRequiredService<SemanticMergeService>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var logger = sp.GetService<ILogger<BranchMergeService>>();
            return new BranchMergeService(mergeService, loggerFactory, logger);
        });

        // MCP Tools for Semantic Merge (explicit registration for nullable dependency support)
        services.AddSingleton(sp => {
            var branchMergeService = sp.GetService<BranchMergeService>();
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var loadingOrchestrator = sp.GetRequiredService<ILoadingOrchestrator>();
            var logger = sp.GetRequiredService<ILogger<Mcp.Tools.SemanticMergeTools>>();
            return new Mcp.Tools.SemanticMergeTools(branchMergeService, solutionManager, loadingOrchestrator, logger);
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
        int deltaCompactionThreshold = 1000
    ) {
        // Register layered indexing configuration
        services.AddSingleton(
            new LayeredIndexingOptions {
                MaxBranchDeltas = maxBranchDeltas,
                EnablePersistence = enablePersistence,
                DeltaCompactionThreshold = deltaCompactionThreshold,
            }
        );

        // Register LayeredSymbolIndex (Phase 1.1 + 1.2)
        services.AddSingleton<ILayeredIndex>(sp => {
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

    /// <summary>
    /// Adds Semantic Replace services for batch code modifications with full context extraction.
    /// Provides pattern matching, context extraction, and atomic batch replacements.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection WithSemanticReplace(this IServiceCollection services)
    {
        // Pattern Matcher Service
        services.AddSingleton<IPatternMatcherService>(sp => {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var semanticSimilarityService = sp.GetService<ISemanticSimilarityService>(); // Optional
            var semanticSearchService = sp.GetService<ISemanticSearchService>(); // Optional - for semantic mode
            var logger = sp.GetService<ILogger<PatternMatcherService>>();
            return new PatternMatcherService(solutionManager, semanticSimilarityService, semanticSearchService, logger);
        });

        // Context Extractor Service
        services.AddSingleton<IContextExtractorService>(sp => {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var logger = sp.GetService<ILogger<ContextExtractorService>>();
            return new ContextExtractorService(solutionManager, logger);
        });

        // Batch Replacer Service
        services.AddSingleton<IBatchReplacerService>(sp => {
            var solutionManager = sp.GetRequiredService<ISolutionManager>();
            var modificationService = sp.GetRequiredService<ICodeModificationService>();
            var formattingService = sp.GetRequiredService<IFormattingService>();
            var versionManager = sp.GetRequiredService<UltrasharpTools.Tools.Versioning.VersionManager>();
            var gitService = sp.GetService<IGitService>(); // Optional
            var logger = sp.GetService<ILogger<BatchReplacerService>>();
            return new BatchReplacerService(
                solutionManager,
                modificationService,
                formattingService,
                versionManager,
                gitService,
                logger);
        });

        // Main Semantic Replace Service
        services.AddSingleton<ISemanticReplaceService>(sp => {
            var patternMatcher = sp.GetRequiredService<IPatternMatcherService>();
            var contextExtractor = sp.GetRequiredService<IContextExtractorService>();
            var batchReplacer = sp.GetRequiredService<IBatchReplacerService>();
            var logger = sp.GetService<ILogger<SemanticReplaceService>>();
            return new SemanticReplaceService(
                patternMatcher,
                contextExtractor,
                batchReplacer,
                logger);
        });

        return services;
    }
}

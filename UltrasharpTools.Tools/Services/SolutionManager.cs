// Base MSBuildWorkspace integration derived from RoslynMCP by Christopher Arquiza
// https://github.com/carquiza/RoslynMCP
// Original code licensed under MIT License
// Significant modifications and additions:
// - FastSymbolIndex with Bloom filter (355k+ symbols indexed in 21.7s)
// - FrozenDictionary for 20-30% faster reflection cache lookups
// - MemoryCache with LRU policy for Compilation and SemanticModel caching
// - FileSystemWatcher for cache invalidation
// - Performance Phases 1-4 optimizations

using System.Collections.Frozen;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ModelContextProtocol;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Layered;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Models;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace UltrasharpTools.Tools.Services;

public sealed partial class SolutionManager : ISolutionManager {
    private readonly ILogger<SolutionManager> _logger;
    private readonly IFuzzyFqnLookupService _fuzzyFqnLookupService;
    private MSBuildWorkspace? _workspace;
    private Solution? _currentSolution;
    private MetadataLoadContext? _metadataLoadContext;
    private PathAssemblyResolver? _pathAssemblyResolver;
    private HashSet<string> _assemblyPathsForReflection = new();

    // Fast symbol index for 10-100x faster lookups
    private readonly FastSymbolIndex _symbolIndex;
    private readonly SymbolCacheManager? _symbolCacheManager;
    public FastSymbolIndex SymbolIndex => _symbolIndex;

    // Layered symbol index for branch-aware queries (Phase 1+, optional)
    private ILayeredIndex? _layeredIndex;
    public ILayeredIndex? LayeredIndex => _layeredIndex;

    // Git workflow service for coordinating git operations with layered indexing (Phase 5, optional)
    private GitWorkflowService? _gitWorkflowService;
    public GitWorkflowService? GitWorkflowService => _gitWorkflowService;

    // Incremental update queue for automatic symbol index updates (Phase 3)
    private IncrementalUpdateQueue? _incrementalUpdateQueue;

    // Lazy initializer for VectorStore (optional, null if Semantic RAG not registered)
    private readonly LazyVectorStoreInitializer? _vectorStoreInitializer;

    // MemoryCache с LRU политикой для управления памятью
    private readonly MemoryCache _compilationCache;
    private readonly MemoryCache _semanticModelCache;

    // FrozenDictionary for 20-30% faster lookups (built once after solution load)
    // Used only when LowMemoryMode is disabled
    private FrozenDictionary<string, Type> _allLoadedReflectionTypesCache = FrozenDictionary<
        string,
        Type
    >.Empty;

    // SQLite-based reflection type index for low memory mode
    private SqliteReflectionTypeIndex? _sqliteReflectionTypeIndex;
    private readonly bool _lowMemoryMode;

    // File-based cache invalidation support
    private FileSystemWatcher? _fileWatcher;
    private readonly ConcurrentDictionary<string, DocumentId> _filePathToDocumentId = new();
    private readonly ConcurrentDictionary<DocumentId, string> _documentIdToFilePath = new();

    // Auto-reload support for .csproj/.sln files
    private FileSystemWatcher? _projectFileWatcher;
    private readonly SolutionReloadOptions _reloadOptions;
    private System.Threading.Timer? _reloadDebounceTimer;
    private string? _currentSolutionPath;

    // Protection against concurrent solution loading
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadingLocks = new();

    // Cache statistics
    private long _compilationCacheHits;
    private long _compilationCacheMisses;
    private long _semanticModelCacheHits;
    private long _semanticModelCacheMisses;

    [MemberNotNullWhen(true, nameof(_workspace), nameof(_currentSolution))]
    public bool IsSolutionLoaded => _workspace != null && _currentSolution != null;
    public MSBuildWorkspace? CurrentWorkspace => _workspace;
    public Solution? CurrentSolution => _currentSolution;
    private readonly string? _buildConfiguration;
    public SolutionManager(
    ILogger<SolutionManager> logger,
    IFuzzyFqnLookupService fuzzyFqnLookupService,
    string? buildConfiguration = null,
    SolutionReloadOptions? reloadOptions = null,
    SymbolCacheOptions? symbolCacheOptions = null,
    LazyVectorStoreInitializer? vectorStoreInitializer = null,
    LayeredIndexingOptions? layeredIndexingOptions = null,
    IGitService? gitService = null,
    bool lowMemoryMode = false
    ) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fuzzyFqnLookupService =
        fuzzyFqnLookupService ?? throw new ArgumentNullException(nameof(fuzzyFqnLookupService));
        _buildConfiguration = buildConfiguration;
        _reloadOptions = reloadOptions ?? new SolutionReloadOptions();
        _vectorStoreInitializer = vectorStoreInitializer; // Optional dependency
        _lowMemoryMode = lowMemoryMode;

        if (_lowMemoryMode) {
            LogLowMemoryModeEnabled();
        }

        // Initialize symbol cache manager (or null if disabled)
        symbolCacheOptions ??= new SymbolCacheOptions();
        _symbolCacheManager = symbolCacheOptions.Enabled
        ? new SymbolCacheManager(_logger, symbolCacheOptions.CacheDirectory)
        : null;

        // Clear cache if requested
        if (symbolCacheOptions.ClearOnStartup && _symbolCacheManager != null) {
            LogClearingSymbolCache();
            _symbolCacheManager.ClearAllCaches();
        }

        // Initialize fast symbol index for optimized lookups
        _symbolIndex = new FastSymbolIndex(_logger, _symbolCacheManager);

        // Initialize layered symbol index if enabled (Phase 1+)
        if (layeredIndexingOptions != null) {
            _layeredIndex = new LayeredSymbolIndex(
            _symbolIndex,
            layeredIndexingOptions,
            gitService,
            null
            );
            LogLayeredIndexEnabled(layeredIndexingOptions.MaxBranchDeltas);
        }

        // Initialize git workflow service if layered indexing and git service are available (Phase 5)
        if (gitService != null && _layeredIndex != null) {
            _gitWorkflowService = new GitWorkflowService(gitService, _layeredIndex, null, null);
            LogGitWorkflowServiceEnabled();
        }

        // Initialize incremental update queue (Phase 3)
        _incrementalUpdateQueue = new IncrementalUpdateQueue(_symbolIndex, null);

        // Инициализация MemoryCache с ОПТИМИЗИРОВАННЫМИ ограничениями
        // Compilation кэш: ~2-4 проекта одновременно, каждая компиляция ~50-100 MB
        _compilationCache = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 150 * 1024 * 1024, // 150 MB лимит (было 500 MB)
            ExpirationScanFrequency = TimeSpan.FromMinutes(2),
            CompactionPercentage = 0.30, // Удалить 30% при достижении лимита
        }
        );

        // SemanticModel кэш: ~20-30 документов, каждая модель ~10-20 MB
        _semanticModelCache = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 250 * 1024 * 1024, // 250 MB лимит (было 1 GB!)
            ExpirationScanFrequency = TimeSpan.FromMinutes(2),
            CompactionPercentage = 0.30,
        }
        );

        LogMemoryCacheInitialized(150, 250);
    }
    public async Task LoadSolutionAsync(string solutionPath, CancellationToken cancellationToken) {
        if (!File.Exists(solutionPath)) {
            LogSolutionFileNotFound(solutionPath);
            throw new FileNotFoundException("Solution file not found.", solutionPath);
        }

        // Normalize solution path to prevent duplicate loading from different path formats
        var normalizedPath = Path.GetFullPath(solutionPath);

        // Get or create semaphore for this solution path
        var loadLock = _loadingLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock - if already loading, fail immediately
        if (!await loadLock.WaitAsync(0, cancellationToken)) {
            LogSolutionAlreadyLoading(normalizedPath);
            throw new McpException(
                $"Solution '{normalizedPath}' is already being loaded. Please wait for the current loading operation to complete."
            );
        }

        try {
            UnloadSolution(); // Clears previous state including _allLoadedReflectionTypesCache

            // Store solution path for auto-reload
            _currentSolutionPath = solutionPath;

            try {
                LogCreatingWorkspace();
                var properties = new Dictionary<string, string> { { "DesignTimeBuild", "true" } };

                if (!string.IsNullOrEmpty(_buildConfiguration)) {
                    properties.Add("Configuration", _buildConfiguration);
                }

                _workspace = MSBuildWorkspace.Create(properties, MefHostServices.DefaultHost);
                _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailedHandler);
                LogLoadingSolution(solutionPath);

                LogMemoryUsage("Before Solution Load");

                // Check if this is a .slnx file (new XML-based solution format)
                var isSlnx = solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

                // Create extended timeout for solution loading
                // .slnx files load projects individually, so need longer timeout for large solutions
                var timeout = isSlnx ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3);
                using var extendedTimeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    extendedTimeoutCts.Token
                );

                if (isSlnx) {
                    _currentSolution = await LoadSlnxAsync(solutionPath, linkedCts.Token);
                } else {
                    _currentSolution = await _workspace.OpenSolutionAsync(
                        solutionPath,
                        new ProgressReporter(this),
                        linkedCts.Token
                    );
                }
                LogSolutionLoaded(_currentSolution.Projects.Count());

                LogMemoryUsage("After Solution Load");

                InitializeMetadataContextAndReflectionCache(_currentSolution, cancellationToken);

                LogMemoryUsage("After Metadata Cache Init");

                // Build fast symbol index for optimized lookups
                await _symbolIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);

                LogMemoryUsage("After Symbol Index Build");

                // Build layered index if enabled (Phase 1+)
                if (_layeredIndex != null) {
                    await _layeredIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);
                    LogLayeredIndexBuilt();
                    LogMemoryUsage("After Layered Index Build");
                }

                // Initialize VectorStore if Semantic RAG is registered
                if (_vectorStoreInitializer != null) {
                    try {
                        LogInitializingVectorStore(solutionPath);
                        await _vectorStoreInitializer.InitializeAsync(
                            solutionPath,
                            cancellationToken
                        );
                        LogMemoryUsage("After VectorStore Init");
                    } catch (Exception ex) {
                        LogVectorStoreInitFailed(ex);
                        // Non-critical: continue without semantic search
                    }
                }

                // Initialize FileSystemWatcher for automatic cache invalidation
                var solutionDirectory = Path.GetDirectoryName(solutionPath);
                if (!string.IsNullOrEmpty(solutionDirectory)) {
                    InitializeFileSystemWatcher(solutionDirectory);

                    // Initialize project file watcher for auto-reload if enabled
                    if (_reloadOptions.AutoReloadEnabled) {
                        InitializeProjectFileWatcher(solutionDirectory);
                    }
                }

                // Subscribe to workspace events for incremental updates (Phase 3)
                if (_workspace != null) {
                    _workspace.RegisterWorkspaceChangedHandler(OnWorkspaceChanged);
                    LogSubscribedToWorkspaceEvents();
                }
            } catch (Exception ex) {
                LogSolutionLoadFailed(ex, solutionPath);
                UnloadSolution();
                throw;
            }
        } finally {
            // Release the loading lock
            loadLock.Release();

            // Clean up completed locks to prevent memory leak
            if (_loadingLocks.TryRemove(normalizedPath, out var removedLock)) {
                removedLock.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads a .slnx file (new XML-based solution format) using Microsoft.VisualStudio.SolutionPersistence.
    /// Projects are loaded in parallel (up to 8 concurrent) into the MSBuildWorkspace.
    /// </summary>
    private async Task<Solution> LoadSlnxAsync(string slnxPath, CancellationToken cancellationToken) {
        LogLoadingSlnxFile(slnxPath);

        // Parse the .slnx file using SolutionPersistence
        var solutionModel = await SolutionSerializers.SlnXml.OpenAsync(slnxPath, cancellationToken);
        var solutionDir = Path.GetDirectoryName(slnxPath)!;

        var loadedProjects = 0;
        var skippedProjects = 0;

        // Filter and prepare project paths
        var projectPaths = new List<(string RelativePath, string FullPath)>();
        foreach (var project in solutionModel.SolutionProjects) {
            var projectPath = Path.GetFullPath(Path.Combine(solutionDir, project.FilePath));
            var extension = Path.GetExtension(projectPath).ToLowerInvariant();

            // Skip VB.NET and F# projects (not supported by C#-only workspace)
            if (extension is ".vbproj" or ".fsproj") {
                LogSlnxProjectSkippedUnsupportedLanguage(project.FilePath);
                Interlocked.Increment(ref skippedProjects);
                continue;
            }

            if (!File.Exists(projectPath)) {
                LogSlnxProjectNotFound(project.FilePath);
                Interlocked.Increment(ref skippedProjects);
                continue;
            }

            projectPaths.Add((project.FilePath, projectPath));
        }

        LogSlnxProjectsToLoad(projectPaths.Count);

        // Load projects in parallel (max 8 concurrent) with per-project timeout
        using var throttle = new SemaphoreSlim(8, 8);
        var tasks = projectPaths.Select(async p => {
            await throttle.WaitAsync(cancellationToken);
            try {
                using var projectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                projectCts.CancelAfter(TimeSpan.FromSeconds(60)); // 60s per project timeout

                await _workspace!.OpenProjectAsync(p.FullPath, cancellationToken: projectCts.Token);
                Interlocked.Increment(ref loadedProjects);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                LogSlnxProjectLoadFailed(p.RelativePath, "Timeout (>60s)");
                Interlocked.Increment(ref skippedProjects);
            } catch (Exception ex) {
                LogSlnxProjectLoadFailed(p.RelativePath, ex.Message);
                Interlocked.Increment(ref skippedProjects);
            } finally {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);

        LogSlnxLoadComplete(loadedProjects, skippedProjects);
        return _workspace!.CurrentSolution;
    }

    /// <summary>
    /// Attempts to automatically discover and load a solution or project file.
    /// Searches up the directory tree from current directory for .sln, then .csproj files.
    /// </summary>
    /// <returns>True if a solution/project was found and loaded, false otherwise</returns>
    public async Task<bool> TryAutoLoadSolutionAsync(CancellationToken cancellationToken) {
        if (IsSolutionLoaded) {
            LogSolutionAlreadyLoaded();
            return true;
        }

        var currentDir = Environment.CurrentDirectory;
        LogStartingAutoDiscovery(currentDir);

        // First, search for .sln files up the directory tree
        var solutionPath = SearchForFileUpwards(currentDir, "*.sln");
        if (solutionPath != null) {
            LogAutoDiscoveredSolution(solutionPath);
            try {
                await LoadSolutionAsync(solutionPath, cancellationToken);
                return true;
            } catch (Exception ex) {
                LogAutoLoadSolutionFailed(ex, solutionPath);
            }
        }

        // Search for .slnx files (new XML-based solution format)
        var slnxPath = SearchForFileUpwards(currentDir, "*.slnx");
        if (slnxPath != null) {
            LogAutoDiscoveredSolution(slnxPath);
            try {
                await LoadSolutionAsync(slnxPath, cancellationToken);
                return true;
            } catch (Exception ex) {
                LogAutoLoadSolutionFailed(ex, slnxPath);
            }
        }

        // If no .sln/.slnx found, search for .csproj files
        var projectPath = SearchForFileUpwards(currentDir, "*.csproj");
        if (projectPath != null) {
            LogAutoDiscoveredProject(projectPath);
            try {
                await LoadProjectAsync(projectPath, cancellationToken);
                return true;
            } catch (Exception ex) {
                LogAutoLoadProjectFailed(ex, projectPath);
            }
        }

        LogNoSolutionOrProjectFound(currentDir);
        return false;
    }

    /// <summary>
    /// Searches for a file matching the pattern up the directory tree.
    /// </summary>
    /// <param name="startDirectory">Directory to start searching from</param>
    /// <param name="pattern">File pattern to search for (e.g., "*.sln")</param>
    /// <returns>Full path to the first matching file, or null if not found</returns>
    private string? SearchForFileUpwards(string startDirectory, string pattern) {
        try {
            var currentDir = new DirectoryInfo(startDirectory);

            while (currentDir != null) {
                var files = currentDir.GetFiles(pattern);
                if (files.Length > 0) {
                    LogFoundFilePattern(pattern, currentDir.FullName);
                    return files[0].FullName;
                }

                currentDir = currentDir.Parent;
            }

            LogNoFilePatternFound(pattern);
        } catch (Exception ex) {
            LogFileSearchError(ex, pattern);
        }

        return null;
    }

    /// <summary>
    /// Loads a single project file (.csproj) as a standalone solution.
    /// </summary>
    private async Task LoadProjectAsync(string projectPath, CancellationToken cancellationToken) {
        if (!File.Exists(projectPath)) {
            LogProjectFileNotFound(projectPath);
            throw new FileNotFoundException("Project file not found.", projectPath);
        }

        UnloadSolution();

        try {
            LogCreatingWorkspaceForProject();
            var properties = new Dictionary<string, string> { { "DesignTimeBuild", "true" } };

            if (!string.IsNullOrEmpty(_buildConfiguration)) {
                properties.Add("Configuration", _buildConfiguration);
            }

            _workspace = MSBuildWorkspace.Create(properties, MefHostServices.DefaultHost);
            _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailedHandler);
            LogLoadingProject(projectPath);

            LogMemoryUsage("Before Project Load");

            var project = await _workspace.OpenProjectAsync(
                projectPath,
                new ProgressReporter(this),
                cancellationToken
            );
            _currentSolution = project.Solution;
            LogProjectLoaded(project.Name);

            LogMemoryUsage("After Project Load");

            InitializeMetadataContextAndReflectionCache(_currentSolution, cancellationToken);

            LogMemoryUsage("After Metadata Cache Init");

            // Build fast symbol index for optimized lookups
            await _symbolIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);

            LogMemoryUsage("After Symbol Index Build");

            // Build layered index if enabled (Phase 1+)
            if (_layeredIndex != null) {
                await _layeredIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);
                LogLayeredIndexBuiltProject();
                LogMemoryUsage("After Layered Index Build (Project)");
            }

            // Initialize FileSystemWatcher for automatic cache invalidation
            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrEmpty(projectDirectory)) {
                InitializeFileSystemWatcher(projectDirectory);
            }
        } catch (Exception ex) {
            LogProjectLoadFailed(ex, projectPath);
            UnloadSolution();
            throw;
        }
    }

    // Фильтр релевантных assemblies для ускорения инициализации
    private static readonly FrozenSet<string> RelevantAssemblyPrefixes = FrozenSet.ToFrozenSet(
        [
            "System.",
            "Microsoft.",
            "mscorlib",
            "netstandard",
            "System",
            "Microsoft", // Без точки для захвата базовых assemblies
        ],
        StringComparer.OrdinalIgnoreCase
    );

    private static bool IsRelevantAssembly(string assemblyPath) {
        var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
        return RelevantAssemblyPrefixes.Any(prefix =>
            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );
    }

    // Мониторинг использования памяти
    private void LogMemoryUsage(string context) {
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        var totalMemoryMB = gcMemoryInfo.HeapSizeBytes / (1024.0 * 1024.0);
        var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        LogMemoryUsageInternal(context, totalMemoryMB, workingSetMB, gen0Collections, gen1Collections, gen2Collections);
    }

    private void InitializeMetadataContextAndReflectionCache(
        Solution solution,
        CancellationToken cancellationToken = default
    ) {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        _assemblyPathsForReflection.Clear();

        // Add runtime assemblies с фильтрацией для оптимизации
        string[] allRuntimeAssemblies = Directory.GetFiles(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "*.dll"
        );
        LogFoundRuntimeAssemblies(allRuntimeAssemblies.Length);

        string[] runtimeAssemblies = allRuntimeAssemblies.Where(IsRelevantAssembly).ToArray();

        LogFilteredAssemblies(
            runtimeAssemblies.Length,
            100.0 * (allRuntimeAssemblies.Length - runtimeAssemblies.Length) / allRuntimeAssemblies.Length
        );

        foreach (var assemblyPath in runtimeAssemblies) {
            // Check cancellation periodically
            cancellationToken.ThrowIfCancellationRequested();
            if (!_assemblyPathsForReflection.Contains(assemblyPath)) {
                _assemblyPathsForReflection.Add(assemblyPath);
            }
        }

        // Load NuGet package assemblies from global cache instead of output directories
        var nugetAssemblies = GetNuGetAssemblyPaths(solution, cancellationToken);
        foreach (var assemblyPath in nugetAssemblies) {
            cancellationToken.ThrowIfCancellationRequested();
            _assemblyPathsForReflection.Add(assemblyPath);
        }

        // Check cancellation before cleanup operations
        cancellationToken.ThrowIfCancellationRequested();

        // Remove mscorlib.dll from the list of assemblies as it is loaded by default
        _assemblyPathsForReflection.RemoveWhere(p =>
            p.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase)
        );

        // Remove duplicate files regardless of path
        _assemblyPathsForReflection = _assemblyPathsForReflection
            .DistinctBy(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check cancellation before creating context
        cancellationToken.ThrowIfCancellationRequested();

        _pathAssemblyResolver = new PathAssemblyResolver(_assemblyPathsForReflection);
        _metadataLoadContext = new MetadataLoadContext(_pathAssemblyResolver);
        LogMetadataContextInitialized(_assemblyPathsForReflection.Count);

        // Check cancellation before populating cache
        cancellationToken.ThrowIfCancellationRequested();

        PopulateReflectionCache(_assemblyPathsForReflection, cancellationToken);
    }

    private void PopulateReflectionCache(
        IEnumerable<string> assemblyPathsToInspect,
        CancellationToken cancellationToken = default
    ) {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null) {
            LogMetadataContextNotInitialized();
            return;
        }

        // Low memory mode: use SQLite-based index instead of in-memory FrozenDictionary
        if (_lowMemoryMode) {
            PopulateReflectionCacheSqlite(assemblyPathsToInspect, cancellationToken);
            return;
        }

        // _allLoadedReflectionTypesCache is cleared in UnloadSolution
        LogStartingReflectionCacheParallel();
        int typesCachedCount = 0;

        // Convert to list to avoid multiple enumeration and enable progress tracking
        var pathsList = assemblyPathsToInspect.ToList();
        int totalPaths = pathsList.Count;
        int processedPaths = 0;
        const int progressCheckInterval = 10; // Report progress and check cancellation every 10 assemblies

        // Use temporary ConcurrentDictionary for parallel collection, then build FrozenDictionary once
        var tempCache = new ConcurrentDictionary<string, Type>();

        // Parallel processing for 4-5x speedup
        var parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        Parallel.ForEach(
            pathsList,
            parallelOptions,
            assemblyPath => {
                var loadedCount = LoadTypesFromAssembly(assemblyPath, tempCache, cancellationToken);
                Interlocked.Add(ref typesCachedCount, loadedCount);

                // Thread-safe progress tracking
                var currentProcessed = Interlocked.Increment(ref processedPaths);
                if (currentProcessed % progressCheckInterval == 0) {
                    LogReflectionCacheProgress(
                        (int)((float)currentProcessed / totalPaths * 100),
                        currentProcessed,
                        totalPaths
                    );
                }
            }
        );

        // Build FrozenDictionary once after parallel collection (20-30% faster reads than Dictionary)
        _allLoadedReflectionTypesCache = tempCache.ToFrozenDictionary();

        LogReflectionCacheComplete(typesCachedCount, pathsList.Count);
    }

    private void PopulateReflectionCacheSqlite(
        IEnumerable<string> assemblyPathsToInspect,
        CancellationToken cancellationToken = default
    ) {
        LogStartingReflectionCacheSqlite();

        // Initialize SQLite index if not already done
        if (_sqliteReflectionTypeIndex == null) {
            _sqliteReflectionTypeIndex = new SqliteReflectionTypeIndex(_logger);
            _sqliteReflectionTypeIndex.InitializeAsync(_metadataLoadContext!, cancellationToken)
                .GetAwaiter().GetResult();
        }

        // Compute solution hash for cache invalidation
        var solutionHash = _currentSolutionPath != null
            ? $"{Path.GetFileName(_currentSolutionPath)}:{_assemblyPathsForReflection.Count}:{DateTime.UtcNow:yyyyMMdd}"
            : $"unknown:{_assemblyPathsForReflection.Count}";

        // Populate from assemblies
        _sqliteReflectionTypeIndex.PopulateFromAssembliesAsync(
            assemblyPathsToInspect,
            solutionHash,
            cancellationToken
        ).GetAwaiter().GetResult();

        LogSqliteReflectionCachePopulated(_sqliteReflectionTypeIndex.TotalTypes);
    }

    private int LoadTypesFromAssembly(
        string assemblyPath,
        ConcurrentDictionary<string, Type> targetCache,
        CancellationToken cancellationToken = default
    ) {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (
            _metadataLoadContext == null
            || string.IsNullOrEmpty(assemblyPath)
            || !File.Exists(assemblyPath)
        ) {
            if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath)) {
                LogInvalidAssemblyPath(assemblyPath);
            }
            return 0;
        }

        int loadedTypesCount = 0;

        try {
            var assembly = _metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

            // For large assemblies, check cancellation periodically during type collection
            // We can't check during GetTypes() directly since it's atomic, but we can after
            cancellationToken.ThrowIfCancellationRequested();

            var types = assembly.GetTypes();
            int processedTypes = 0;
            const int typeCheckInterval = 50; // Check cancellation every 50 types

            foreach (var type in types) {
                // Check cancellation periodically when processing many types
                if (++processedTypes % typeCheckInterval == 0) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (type?.FullName != null && targetCache.TryAdd(type.FullName, type)) {
                    loadedTypesCount++;
                }
            }
        } catch (ReflectionTypeLoadException rtlex) {
            LogPartialTypeLoad(assemblyPath, rtlex.LoaderExceptions.Length);
            foreach (var loaderEx in rtlex.LoaderExceptions.Where(e => e != null)) {
                LogLoaderException(loaderEx!.Message);
            }

            // For partial load errors, still process the types that did load
            int processedTypes = 0;
            const int typeCheckInterval = 20; // Check cancellation more frequently when dealing with problematic assemblies

            foreach (var type in rtlex.Types.Where(t => t != null)) {
                // Check cancellation periodically
                if (++processedTypes % typeCheckInterval == 0) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (type!.FullName != null && targetCache.TryAdd(type.FullName, type)) {
                    loadedTypesCount++;
                }
            }
        } catch (FileNotFoundException) { // Should be rare due to File.Exists check, but MLC might have its own resolution logic
            LogAssemblyFileNotFound(assemblyPath);
        } catch (BadImageFormatException) {
            LogBadImageFormat(assemblyPath);
        } catch (Exception ex) {
            LogTypeLoadError(ex, assemblyPath);
        }

        return loadedTypesCount;
    }

    public void UnloadSolution() {
        LogUnloadingSolution();

        LogMemoryUsage("Before Unload");

        // Stop FileSystemWatcher
        if (_fileWatcher != null) {
            _fileWatcher.EnableRaisingEvents = false;
        }

        // Stop project file watcher
        if (_projectFileWatcher != null) {
            _projectFileWatcher.EnableRaisingEvents = false;
        }

        // Stop debounce timer
        _reloadDebounceTimer?.Dispose();
        _reloadDebounceTimer = null;

        // Clear mappings
        _filePathToDocumentId.Clear();
        _documentIdToFilePath.Clear();

        // Clear solution path
        _currentSolutionPath = null;

        // Очищаем MemoryCache - удаляем 100% записей (полная выгрузка решения)
        _compilationCache.Compact(1.0);
        _semanticModelCache.Compact(1.0);

        // Reset to empty FrozenDictionary (FrozenDictionary doesn't have Clear method)
        _allLoadedReflectionTypesCache = FrozenDictionary<string, Type>.Empty;

        // Dispose SQLite reflection type index if in low memory mode
        if (_sqliteReflectionTypeIndex != null) {
            _sqliteReflectionTypeIndex.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _sqliteReflectionTypeIndex = null;
        }

        if (_workspace != null) {
            _workspace.CloseSolution();
            _workspace.Dispose();
            _workspace = null;
        }
        _metadataLoadContext?.Dispose();
        _metadataLoadContext = null;
        _pathAssemblyResolver = null; // PathAssemblyResolver doesn't implement IDisposable
        _assemblyPathsForReflection.Clear();

        // Принудительный GC для освобождения памяти
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();

        LogMemoryUsage("After Unload + GC");

        LogSolutionUnloaded();
    }

    public void RefreshCurrentSolution() {
        if (_workspace == null) {
            LogRefreshWorkspaceNull();
            return;
        }
        if (_workspace.CurrentSolution == null) {
            LogRefreshNoSolution();
            return;
        }
        _currentSolution = _workspace.CurrentSolution;

        // No longer compacting caches - file-based invalidation handles stale entries automatically
        // FileSystemWatcher will invalidate modified files
        LogSolutionRefreshed();
    }

    public async Task ReloadSolutionFromDiskAsync(CancellationToken cancellationToken) {
        if (_workspace == null) {
            LogReloadWorkspaceNull();
            return;
        }
        if (_workspace.CurrentSolution == null) {
            LogReloadNoSolution();
            return;
        }
        await LoadSolutionAsync(_workspace.CurrentSolution.FilePath!, cancellationToken);
        LogSolutionReloaded();
    }

    private void OnWorkspaceFailedHandler(WorkspaceDiagnosticEventArgs e) {
        var diagnostic = e.Diagnostic;
        if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure) {
            LogWorkspaceDiagnosticError(diagnostic.Kind, diagnostic.Message);
        } else {
            LogWorkspaceDiagnosticWarning(diagnostic.Kind, diagnostic.Message);
        }
    }

    /// <summary>
    /// Handles workspace change events for incremental symbol index updates (Phase 3).
    /// </summary>
    private void OnWorkspaceChanged(WorkspaceChangeEventArgs e) {
        if (_incrementalUpdateQueue == null || e.NewSolution == null) {
            return;
        }

        try {
            // Enqueue updates based on change kind
            switch (e.Kind) {
                case WorkspaceChangeKind.DocumentAdded:
                    if (e.DocumentId != null) {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Added,
                                e.NewSolution
                            )
                        );
                        LogDocumentAdded(e.DocumentId);
                    }
                    break;

                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentReloaded:
                    if (e.DocumentId != null) {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Modified,
                                e.NewSolution
                            )
                        );
                        LogDocumentChanged(e.DocumentId);
                    }
                    break;

                case WorkspaceChangeKind.DocumentRemoved:
                    if (e.DocumentId != null) {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Removed,
                                e.NewSolution
                            )
                        );
                        LogDocumentRemoved(e.DocumentId);
                    }
                    break;

                // Ignore other change kinds for now (project-level changes, solution changes, etc.)
                default:
                    break;
            }

            // Update current solution reference
            _currentSolution = e.NewSolution;
        } catch (Exception ex) {
            LogWorkspaceChangeError(ex, e.Kind);
        }
    }

    public async Task<INamedTypeSymbol?> FindRoslynNamedTypeSymbolAsync(
        string fullyQualifiedTypeName,
        CancellationToken cancellationToken
    ) {
        if (!IsSolutionLoaded) {
            LogCannotFindSymbolNoSolution();
            return null;
        }
        // Check cancellation before starting lookup
        cancellationToken.ThrowIfCancellationRequested();
        // Use fuzzy FQN lookup service
        var matches = await _fuzzyFqnLookupService.FindMatchesAsync(
            fullyQualifiedTypeName,
            this,
            cancellationToken
        );
        var matchList = matches.Where(m => m.Symbol is INamedTypeSymbol).ToList();
        // Check cancellation after initial matching
        cancellationToken.ThrowIfCancellationRequested();
        if (matchList.Count == 1) {
            var match = matchList.First();
            LogNamedTypeSymbolFound(match.CanonicalFqn, match.Score, match.MatchReason);
            return (INamedTypeSymbol)match.Symbol;
        }
        if (matchList.Count > 1) {
            LogMultipleMatchesFound(fullyQualifiedTypeName);
            throw new McpException(
                $"FQN was ambiguous, did you mean one of these?\n{string.Join("\n", matchList.Select(m => m.CanonicalFqn))}"
            );
        }
        // Direct lookup as fallback
        if (CurrentSolution == null) {
            LogCannotDirectLookupNoSolution();
            return null;
        }
        foreach (var project in CurrentSolution.Projects) {
            // Check cancellation before each project
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await GetCompilationAsync(project.Id, cancellationToken);
            if (compilation == null) {
                continue;
            }
            var symbol = compilation.GetTypeByMetadataName(fullyQualifiedTypeName);
            if (symbol != null) {
                LogNamedTypeSymbolFoundDirect(fullyQualifiedTypeName, project.Name);
                return symbol;
            }
        }
        // Check cancellation before nested type check
        cancellationToken.ThrowIfCancellationRequested();
        // Check for nested type with dot notation as last resort
        var lastDotIndex = fullyQualifiedTypeName.LastIndexOf('.');
        if (lastDotIndex > 0) {
            var parentTypeName = fullyQualifiedTypeName.Substring(0, lastDotIndex);
            var nestedTypeName = fullyQualifiedTypeName.Substring(lastDotIndex + 1);
            foreach (var project in CurrentSolution.Projects) {
                // Check cancellation before each project
                cancellationToken.ThrowIfCancellationRequested();
                var compilation = await GetCompilationAsync(project.Id, cancellationToken);
                if (compilation == null) {
                    continue;
                }
                var parentSymbol = compilation.GetTypeByMetadataName(parentTypeName);
                if (parentSymbol != null) {
                    // Check if there's a nested type with this name
                    var nestedType = parentSymbol.GetTypeMembers(nestedTypeName).FirstOrDefault();
                    if (nestedType != null) {
                        var correctName = $"{parentTypeName}+{nestedTypeName}";
                        LogNestedTypeHint(fullyQualifiedTypeName, correctName);
                        throw new McpException(
                            $"Type not found: '{fullyQualifiedTypeName}'. This appears to be a nested type - use '{correctName}' instead (use + instead of . for nested types)"
                        );
                    }
                }
            }
        }
        LogNamedTypeSymbolNotFound(fullyQualifiedTypeName);
        return null;
    }

    public async Task<ISymbol?> FindRoslynSymbolAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken
    ) {
        if (!IsSolutionLoaded) {
            LogCannotFindSymbolNoSolution();
            return null;
        }

        // Check cancellation before starting lookup
        cancellationToken.ThrowIfCancellationRequested();

        // Use fuzzy FQN lookup service
        var matches = await _fuzzyFqnLookupService.FindMatchesAsync(
            fullyQualifiedName,
            this,
            cancellationToken
        );
        var matchList = matches.ToList();

        // Check cancellation after initial matching
        cancellationToken.ThrowIfCancellationRequested();

        if (matchList.Count == 1) {
            var match = matchList.First();
            LogSymbolFound(match.CanonicalFqn, match.Score, match.MatchReason);
            return match.Symbol;
        }

        if (matchList.Count > 1) {
            LogMultipleMatchesFound(fullyQualifiedName);
            throw new McpException(
                $"FQN was ambiguous, did you mean one of these?\n{string.Join("\n", matchList.Select(m => m.CanonicalFqn))}"
            );
        }

        // Check cancellation before fallback lookup
        cancellationToken.ThrowIfCancellationRequested();

        // Fall back to type lookup
        var typeSymbol = await FindRoslynNamedTypeSymbolAsync(
            fullyQualifiedName,
            cancellationToken
        );
        if (typeSymbol != null) {
            return typeSymbol;
        }

        // Check cancellation before member lookup
        cancellationToken.ThrowIfCancellationRequested();

        // Check for member of a type as fallback
        var lastDotIndex = fullyQualifiedName.LastIndexOf('.');
        if (lastDotIndex > 0 && lastDotIndex < fullyQualifiedName.Length - 1) {
            var typeName = fullyQualifiedName.Substring(0, lastDotIndex);
            var memberName = fullyQualifiedName.Substring(lastDotIndex + 1);

            var parentTypeSymbol = await FindRoslynNamedTypeSymbolAsync(
                typeName,
                cancellationToken
            );
            if (parentTypeSymbol != null) {
                // Check cancellation before final lookup step
                cancellationToken.ThrowIfCancellationRequested();

                var members = parentTypeSymbol.GetMembers(memberName);
                if (members.Length > 0) {
                    // TODO: Handle overloads if necessary, for now, take the first.
                    var memberSymbol = members.First();
                    LogMemberSymbolFound(fullyQualifiedName);
                    return memberSymbol;
                }
            }
        }

        LogSymbolNotFound(fullyQualifiedName);
        return null;
    }

    public async Task<Type?> FindReflectionTypeAsync(
        string fullyQualifiedTypeName,
        CancellationToken cancellationToken
    ) {
        // Check cancellation at the beginning of the method
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null) {
            LogCannotFindReflectionType();
            return null;
        }

        // Low memory mode: use SQLite index
        if (_lowMemoryMode && _sqliteReflectionTypeIndex != null) {
            var type = await _sqliteReflectionTypeIndex.FindTypeAsync(fullyQualifiedTypeName, cancellationToken);
            if (type != null) {
                LogReflectionTypeFoundSqlite(fullyQualifiedTypeName);
            }
            return type;
        }

        // Standard mode: use in-memory cache
        if (_allLoadedReflectionTypesCache.TryGetValue(fullyQualifiedTypeName, out var cachedType)) {
            LogReflectionTypeFoundCache(fullyQualifiedTypeName);
            return cachedType;
        }
        LogReflectionTypeNotFound(fullyQualifiedTypeName);
        return null;
    }

    public async Task<IEnumerable<Type>> SearchReflectionTypesAsync(
        string regexPattern,
        CancellationToken cancellationToken
    ) {
        // Check cancellation at the method entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null) {
            LogCannotSearchReflectionTypes();
            return [];
        }

        // Low memory mode: use SQLite index
        if (_lowMemoryMode && _sqliteReflectionTypeIndex != null) {
            var searchResults = await _sqliteReflectionTypeIndex.SearchTypesAsync(
                regexPattern, limit: 200, cancellationToken);

            // Lazy load actual Type objects from search results
            var matchedTypes = new List<Type>();
            foreach (var result in searchResults) {
                cancellationToken.ThrowIfCancellationRequested();
                var type = _sqliteReflectionTypeIndex.LoadType(result);
                if (type != null) {
                    matchedTypes.Add(type);
                }
            }

            LogReflectionTypesFoundSqlite(matchedTypes.Count, regexPattern);
            return matchedTypes.Distinct();
        }

        // Standard mode: use in-memory cache
        if (_allLoadedReflectionTypesCache.Count == 0) {
            LogReflectionCacheEmpty();
            return [];
        }

        // Check cancellation before regex compilation
        cancellationToken.ThrowIfCancellationRequested();

        var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var results = new List<Type>();

        // Consider batching in chunks to check cancellation more frequently on large type caches
        int processedCount = 0;
        const int batchSize = 100; // Check cancellation every 100 types

        foreach (var typeEntry in _allLoadedReflectionTypesCache) { // Iterate KeyValuePair to access FQN directly
            if (++processedCount % batchSize == 0) {
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Key is type.FullName which should not be null for cached types
            if (regex.IsMatch(typeEntry.Key)) { // Search FQN
                results.Add(typeEntry.Value);
            } else if (regex.IsMatch(typeEntry.Value.Name)) { // Search simple name
                results.Add(typeEntry.Value);
            }
        }

        // Check cancellation before returning results
        cancellationToken.ThrowIfCancellationRequested();

        LogReflectionTypesFound(results.Count, regexPattern);
        return results.Distinct();
    }

    public IEnumerable<Project> GetProjects() {
        return CurrentSolution?.Projects ?? [];
    }

    public Project? GetProjectByName(string projectName) {
        if (!IsSolutionLoaded) {
            LogCannotGetProjectNoSolution();
            return null;
        }
        var project = CurrentSolution?.Projects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)
        );
        if (project == null) {
            LogProjectNotFound(projectName);
        }
        return project;
    }

    public ValueTask<SemanticModel?> GetSemanticModelAsync(
        DocumentId documentId,
        CancellationToken cancellationToken
    ) {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSolutionLoaded) {
            LogCannotGetSemanticModelNoSolution();
            return ValueTask.FromResult<SemanticModel?>(null);
        }

        // Fast path: check cache first with file-based validation
        if (_semanticModelCache.TryGetValue(documentId, out CacheEntry<SemanticModel>? cacheEntry)) {
            // Validate cache entry based on file modification time
            if (cacheEntry!.IsValid()) {
                Interlocked.Increment(ref _semanticModelCacheHits);
                LogSemanticModelCacheHit(documentId);
                return ValueTask.FromResult<SemanticModel?>(cacheEntry.Value);
            } else {
                // File has been modified - invalidate cache entry
                _semanticModelCache.Remove(documentId);
                LogSemanticModelCacheInvalidated(cacheEntry.FilePath);
            }
        }

        Interlocked.Increment(ref _semanticModelCacheMisses);

        // Slow path: load asynchronously
        return LoadSemanticModelAsync(documentId, cancellationToken);
    }

    private async ValueTask<SemanticModel?> LoadSemanticModelAsync(
        DocumentId documentId,
        CancellationToken cancellationToken
    ) {
        // Check cancellation before document lookup
        cancellationToken.ThrowIfCancellationRequested();

        if (CurrentSolution == null) {
            LogCannotGetSemanticModelNoSolution();
            return null;
        }

        var document = CurrentSolution.GetDocument(documentId);
        if (document == null) {
            LogDocumentNotFound(documentId);
            return null;
        }

        LogRequestingSemanticModel(document.FilePath);

        // Check cancellation before expensive GetSemanticModelAsync call
        cancellationToken.ThrowIfCancellationRequested();

        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (model != null && document.FilePath != null) {
            // Get file LastWriteTime for invalidation
            DateTime lastWriteTime;
            try {
                lastWriteTime = File.GetLastWriteTimeUtc(document.FilePath);
            } catch {
                lastWriteTime = DateTime.UtcNow;
                LogLastWriteTimeFailed(document.FilePath);
            }

            // Create cache entry with file metadata
            var cacheEntry = new CacheEntry<SemanticModel> {
                Value = model,
                FilePath = document.FilePath,
                LastWriteTimeUtc = lastWriteTime,
            };

            // Store with expiration and size settings (OPTIMIZED for lower memory)
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(15 * 1024 * 1024) // Estimate: ~15 MB per semantic model
                .SetSlidingExpiration(TimeSpan.FromMinutes(10)) // Reduced from 30 min
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)) // Reduced from 2 hours
                .RegisterPostEvictionCallback(
                    (key, value, reason, state) => {
                        LogSemanticModelEvicted(key?.ToString() ?? "null", reason);
                    }
                );

            _semanticModelCache.Set(documentId, cacheEntry, cacheEntryOptions);

            // Maintain FilePath → DocumentId mapping for FileSystemWatcher
            _documentIdToFilePath[documentId] = document.FilePath;
            _filePathToDocumentId[document.FilePath] = documentId;

            LogSemanticModelCached(document.FilePath, lastWriteTime);
        } else {
            LogSemanticModelFailed(document.FilePath);
        }
        return model;
    }

    public ValueTask<Compilation?> GetCompilationAsync(
        ProjectId projectId,
        CancellationToken cancellationToken
    ) {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSolutionLoaded) {
            LogCannotGetCompilationNoSolution();
            return ValueTask.FromResult<Compilation?>(null);
        }

        // Fast path: check cache first with file-based validation
        if (_compilationCache.TryGetValue(projectId, out ProjectCacheEntry? cacheEntry)) {
            // Validate cache entry based on project file modification time
            if (cacheEntry!.IsValid()) {
                Interlocked.Increment(ref _compilationCacheHits);
                LogCompilationCacheHit(projectId);
                return ValueTask.FromResult<Compilation?>(cacheEntry.Value);
            } else {
                // Project file has been modified - invalidate compilation and all related documents
                InvalidateCompilationInternal(projectId, cacheEntry);
                LogCompilationCacheInvalidated(cacheEntry.ProjectFilePath);
            }
        }

        Interlocked.Increment(ref _compilationCacheMisses);

        // Slow path: load asynchronously
        return LoadCompilationAsync(projectId, cancellationToken);
    }

    private async ValueTask<Compilation?> LoadCompilationAsync(
        ProjectId projectId,
        CancellationToken cancellationToken
    ) {
        // Check cancellation before project lookup
        cancellationToken.ThrowIfCancellationRequested();

        if (CurrentSolution == null) {
            LogCannotGetCompilationNoSolution();
            return null;
        }

        var project = CurrentSolution.GetProject(projectId);
        if (project == null) {
            LogProjectNotFoundById(projectId);
            return null;
        }

        LogRequestingCompilation(project.Name);

        // Check cancellation before expensive GetCompilationAsync call
        cancellationToken.ThrowIfCancellationRequested();

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation != null && project.FilePath != null) {
            // Get project file LastWriteTime for invalidation
            DateTime lastWriteTime;
            try {
                lastWriteTime = File.GetLastWriteTimeUtc(project.FilePath);
            } catch {
                lastWriteTime = DateTime.UtcNow;
                LogLastWriteTimeFailed(project.FilePath);
            }

            // Collect all document IDs for cascade invalidation
            var documentIds = project.DocumentIds.ToHashSet();

            // Create project cache entry with metadata
            var cacheEntry = new ProjectCacheEntry {
                Value = compilation,
                ProjectFilePath = project.FilePath,
                LastWriteTimeUtc = lastWriteTime,
                DocumentIds = documentIds,
            };

            // Store with expiration and size settings (OPTIMIZED for lower memory)
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(100 * 1024 * 1024) // Estimate: ~100 MB per compilation
                .SetSlidingExpiration(TimeSpan.FromMinutes(20)) // Reduced from 60 min
                .SetAbsoluteExpiration(TimeSpan.FromHours(1)) // Reduced from 4 hours
                .RegisterPostEvictionCallback(
                    (key, value, reason, state) => {
                        LogCompilationEvicted(key?.ToString() ?? "null", reason);
                    }
                );

            _compilationCache.Set(projectId, cacheEntry, cacheEntryOptions);

            LogCompilationCached(project.Name, lastWriteTime, documentIds.Count);
        } else {
            LogCompilationFailed(project.Name);
        }
        return compilation;
    }

    /// <summary>
    /// Invalidate semantic model cache for a specific document (granular invalidation)
    /// </summary>
    public void InvalidateSemanticModel(DocumentId documentId) {
        _semanticModelCache.Remove(documentId);
        LogSemanticModelInvalidated(documentId);

        // Remove from mapping
        if (_documentIdToFilePath.TryRemove(documentId, out var filePath)) {
            _filePathToDocumentId.TryRemove(filePath, out _);
        }
    }

    /// <summary>
    /// Invalidate compilation and all related semantic models (cascade invalidation)
    /// </summary>
    public void InvalidateCompilation(ProjectId projectId) {
        if (_compilationCache.TryGetValue(projectId, out ProjectCacheEntry? cacheEntry)) {
            InvalidateCompilationInternal(projectId, cacheEntry!);
        }
    }

    private void InvalidateCompilationInternal(ProjectId projectId, ProjectCacheEntry cacheEntry) {
        // Remove compilation from cache
        _compilationCache.Remove(projectId);
        LogCompilationInvalidated(projectId);

        // Cascade: invalidate all semantic models for documents in this project
        foreach (var documentId in cacheEntry.DocumentIds) {
            InvalidateSemanticModel(documentId);
        }
        LogCascadeInvalidation(cacheEntry.DocumentIds.Count);
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public CacheStatistics GetCacheStatistics() {
        var totalMemory = GC.GetTotalMemory(forceFullCollection: false);

        return new CacheStatistics {
            CompilationCacheSize = _compilationCache.Count,
            SemanticModelCacheSize = _semanticModelCache.Count,
            ReflectionTypesCacheSize = _allLoadedReflectionTypesCache.Count,
            CompilationCacheHits = _compilationCacheHits,
            CompilationCacheMisses = _compilationCacheMisses,
            SemanticModelCacheHits = _semanticModelCacheHits,
            SemanticModelCacheMisses = _semanticModelCacheMisses,
            TotalMemoryBytes = totalMemory,
        };
    }

    /// <summary>
    /// Initialize FileSystemWatcher for automatic cache invalidation
    /// </summary>
    private void InitializeFileSystemWatcher(string solutionDirectory) {
        try {
            _fileWatcher = new FileSystemWatcher(solutionDirectory) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.cs",
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;

            // Debounce mechanism to avoid multiple events for same file
            _fileWatcher.EnableRaisingEvents = true;

            LogFileWatcherInitialized(solutionDirectory);
        } catch (Exception ex) {
            LogFileWatcherInitFailed(ex, solutionDirectory);
            _fileWatcher = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) {
        // Find DocumentId by file path and invalidate
        if (_filePathToDocumentId.TryGetValue(e.FullPath, out var documentId)) {
            LogFileChanged(e.FullPath);
            InvalidateSemanticModel(documentId);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e) {
        // File deleted - invalidate cache
        if (_filePathToDocumentId.TryGetValue(e.FullPath, out var documentId)) {
            LogFileDeleted(e.FullPath);
            InvalidateSemanticModel(documentId);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e) {
        // Old file path - invalidate
        if (_filePathToDocumentId.TryGetValue(e.OldFullPath, out var documentId)) {
            LogFileRenamed(e.OldFullPath, e.FullPath);
            InvalidateSemanticModel(documentId);

            // Update mapping with new path
            if (CurrentSolution != null) {
                var document = CurrentSolution.GetDocument(documentId);
                if (document != null && document.FilePath == e.FullPath) {
                    _documentIdToFilePath[documentId] = e.FullPath;
                    _filePathToDocumentId[e.FullPath] = documentId;
                }
            }
        }
    }

    /// <summary>
    /// Initialize FileSystemWatcher for project files (.csproj, .sln) to trigger automatic reload
    /// </summary>
    private void InitializeProjectFileWatcher(string solutionDirectory) {
        try {
            _projectFileWatcher = new FileSystemWatcher(solutionDirectory) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };

            // Watch all configured extensions
            if (_reloadOptions.WatchedExtensions?.Length > 0) {
                // FileSystemWatcher doesn't support multiple filters directly, so we'll filter in the event handler
                _projectFileWatcher.Filter = "*.*";
            }

            _projectFileWatcher.Changed += OnProjectFileChanged;

            _projectFileWatcher.EnableRaisingEvents = true;

            LogProjectFileWatcherInitialized(
                string.Join(", ", _reloadOptions.WatchedExtensions ?? Array.Empty<string>()),
                _reloadOptions.DebounceDelayMs
            );
        } catch (Exception ex) {
            LogProjectFileWatcherInitFailed(ex, solutionDirectory);
            _projectFileWatcher = null;
        }
    }

    private void OnProjectFileChanged(object sender, FileSystemEventArgs e) {
        // Check if the file extension is in the watched list
        var extension = Path.GetExtension(e.FullPath);
        if (
            _reloadOptions.WatchedExtensions == null
            || !_reloadOptions.WatchedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase
            )
        ) {
            return;
        }

        LogProjectFileChangeDetected(e.FullPath, _reloadOptions.DebounceDelayMs);

        // Reset debounce timer - if multiple files change, we only reload once after the last change
        _reloadDebounceTimer?.Dispose();
        _reloadDebounceTimer = new System.Threading.Timer(
            async _ => await TriggerAutoReloadAsync(),
            null,
            _reloadOptions.DebounceDelayMs,
            Timeout.Infinite
        );
    }

    private async Task TriggerAutoReloadAsync() {
        try {
            if (string.IsNullOrEmpty(_currentSolutionPath)) {
                LogCannotAutoReloadNoPath();
                return;
            }

            LogAutoReloading(_currentSolutionPath);

            await ReloadSolutionFromDiskAsync(CancellationToken.None);

            LogAutoReloadComplete();
        } catch (Exception ex) {
            LogAutoReloadError(ex);
        } finally {
            // Dispose the timer after it fires
            _reloadDebounceTimer?.Dispose();
            _reloadDebounceTimer = null;
        }
    }

    public void Dispose() {
        // Note: Workspace.RegisterWorkspaceChangedHandler doesn't provide unsubscribe mechanism
        // The handler will be disposed when workspace is disposed in UnloadSolution

        UnloadSolution();

        // Dispose IncrementalUpdateQueue (Phase 3)
        _incrementalUpdateQueue?.Dispose();
        _incrementalUpdateQueue = null;

        // Dispose FileSystemWatcher
        if (_fileWatcher != null) {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Deleted -= OnFileDeleted;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        // Dispose project file watcher
        if (_projectFileWatcher != null) {
            _projectFileWatcher.EnableRaisingEvents = false;
            _projectFileWatcher.Changed -= OnProjectFileChanged;
            _projectFileWatcher.Dispose();
            _projectFileWatcher = null;
        }

        // Dispose debounce timer
        _reloadDebounceTimer?.Dispose();
        _reloadDebounceTimer = null;

        // Освобождаем MemoryCache ресурсы
        _compilationCache?.Dispose();
        _semanticModelCache?.Dispose();

        GC.SuppressFinalize(this);
    }

    private class ProgressReporter : IProgress<ProjectLoadProgress> {
        private readonly SolutionManager _manager;

        public ProgressReporter(SolutionManager manager) {
            _manager = manager;
        }

        public void Report(ProjectLoadProgress loadProgress) {
            var projectDisplay = Path.GetFileName(loadProgress.FilePath);
            _manager.LogProjectLoadProgress(projectDisplay, loadProgress.Operation.ToString(), loadProgress.ElapsedTime);
        }
    }

    private HashSet<string> GetNuGetAssemblyPaths(
        Solution solution,
        CancellationToken cancellationToken = default
    ) {
        var nugetAssemblyPaths = new ConcurrentBag<string>();
        var nugetCacheDir = GetNuGetGlobalPackagesFolder();

        if (string.IsNullOrEmpty(nugetCacheDir) || !Directory.Exists(nugetCacheDir)) {
            LogNuGetFolderNotFound(nugetCacheDir);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Parallel processing of projects for 3-4x speedup
        var parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = 4, // I/O bound operations
            CancellationToken = cancellationToken,
        };

        Parallel.ForEach(
            solution.Projects,
            parallelOptions,
            project => {
                if (string.IsNullOrEmpty(project.FilePath)) {
                    return;
                }

                var packageReferences = LegacyNuGetPackageReader.GetAllPackageReferences(
                    project.FilePath
                );
                var projectTargetFramework = SolutionTools.ExtractTargetFrameworkFromProjectFile(
                    project.FilePath
                );

                foreach (var package in packageReferences) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var packageDir = Path.Combine(
                        nugetCacheDir,
                        package.PackageId.ToLowerInvariant(),
                        package.Version
                    );
                    if (!Directory.Exists(packageDir)) {
                        LogPackageDirectoryNotFound(packageDir);
                        continue;
                    }

                    var libDir = Path.Combine(packageDir, "lib");
                    if (!Directory.Exists(libDir)) {
                        LogNoLibDirectory(package.PackageId, package.Version);
                        continue;
                    }

                    // Find assemblies using the project's target framework
                    var assemblyPaths = GetAssembliesForTargetFramework(
                        libDir,
                        package.TargetFramework ?? projectTargetFramework,
                        package.PackageId,
                        package.Version
                    );
                    foreach (var assemblyPath in assemblyPaths) {
                        nugetAssemblyPaths.Add(assemblyPath);
                    }
                }
            }
        );

        var resultSet = nugetAssemblyPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        LogNuGetAssembliesFound(resultSet.Count);
        return resultSet;
    }

    private static string GetNuGetGlobalPackagesFolder() {
        // Check environment variable first
        var globalPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(globalPackagesPath)) {
            return globalPackagesPath;
        }

        // Default location based on OS
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".nuget", "packages");
    }

    private List<string> GetAssembliesForTargetFramework(
        string libDir,
        string targetFramework,
        string packageId,
        string version
    ) {
        var assemblies = new List<string>();

        if (!Directory.Exists(libDir)) {
            return assemblies;
        }

        // First try exact target framework match
        var exactFrameworkDir = Path.Combine(libDir, targetFramework);
        if (Directory.Exists(exactFrameworkDir)) {
            var exactAssemblies = Directory.GetFiles(
                exactFrameworkDir,
                "*.dll",
                SearchOption.TopDirectoryOnly
            );
            assemblies.AddRange(exactAssemblies);
            LogExactFrameworkMatch(exactAssemblies.Length, targetFramework, packageId, version);
            return assemblies;
        }

        // Try compatible frameworks in order of preference
        var compatibleFrameworks = GetCompatibleFrameworks(targetFramework);

        foreach (var framework in compatibleFrameworks) {
            var frameworkDir = Path.Combine(libDir, framework);
            if (Directory.Exists(frameworkDir)) {
                var frameworkAssemblies = Directory.GetFiles(
                    frameworkDir,
                    "*.dll",
                    SearchOption.TopDirectoryOnly
                );
                assemblies.AddRange(frameworkAssemblies);
                LogCompatibleFrameworkMatch(frameworkAssemblies.Length, framework, packageId, version);
                return assemblies; // Take the first compatible framework found
            }
        }

        // Fallback: check if there are any DLLs directly in lib directory
        if (assemblies.Count == 0) {
            var libAssemblies = Directory.GetFiles(libDir, "*.dll", SearchOption.TopDirectoryOnly);
            assemblies.AddRange(libAssemblies);
            if (libAssemblies.Length > 0) {
                LogLibRootAssemblies(libAssemblies.Length, packageId, version);
            }
        }

        return assemblies;
    }

    private static string[] GetCompatibleFrameworks(string targetFramework) {
        // Return frameworks in order of compatibility preference
        return targetFramework.ToLowerInvariant() switch {
            "net10.0" =>
            [
                "net10.0",
                "net9.0",
                "net8.0",
                "net7.0",
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "net9.0" =>
            [
                "net9.0",
                "net8.0",
                "net7.0",
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "net8.0" =>
            [
                "net8.0",
                "net7.0",
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "net7.0" =>
            [
                "net7.0",
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "net6.0" =>
            [
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "net5.0" =>
            [
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "netcoreapp3.1" =>
            [
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "netcoreapp3.0" =>
            [
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
            "netcoreapp2.1" => ["netcoreapp2.1", "netstandard2.0", "netstandard1.6"],
            "netstandard2.1" => ["netstandard2.1", "netstandard2.0", "netstandard1.6"],
            "netstandard2.0" => ["netstandard2.0", "netstandard1.6"],
            _ =>
            [
                "net8.0",
                "net7.0",
                "net6.0",
                "net5.0",
                "netcoreapp3.1",
                "netcoreapp3.0",
                "netcoreapp2.1",
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
            ],
        };
    }
}

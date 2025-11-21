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
using ModelContextProtocol;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Layered;
using UltrasharpTools.Tools.Mcp.Tools;
using UltrasharpTools.Tools.Models;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace UltrasharpTools.Tools.Services;

public sealed class SolutionManager : ISolutionManager
{
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
    private FrozenDictionary<string, Type> _allLoadedReflectionTypesCache = FrozenDictionary<
        string,
        Type
    >.Empty;

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
        IGitService? gitService = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fuzzyFqnLookupService =
            fuzzyFqnLookupService ?? throw new ArgumentNullException(nameof(fuzzyFqnLookupService));
        _buildConfiguration = buildConfiguration;
        _reloadOptions = reloadOptions ?? new SolutionReloadOptions();
        _vectorStoreInitializer = vectorStoreInitializer; // Optional dependency

        // Initialize symbol cache manager (or null if disabled)
        symbolCacheOptions ??= new SymbolCacheOptions();
        _symbolCacheManager = symbolCacheOptions.Enabled
            ? new SymbolCacheManager(_logger, symbolCacheOptions.CacheDirectory)
            : null;

        // Clear cache if requested
        if (symbolCacheOptions.ClearOnStartup && _symbolCacheManager != null)
        {
            _logger.LogInformation("Clearing symbol cache on startup");
            _symbolCacheManager.ClearAllCaches();
        }

        // Initialize fast symbol index for optimized lookups
        _symbolIndex = new FastSymbolIndex(_logger, _symbolCacheManager);

        // Initialize layered symbol index if enabled (Phase 1+)
        if (layeredIndexingOptions != null)
        {
            _layeredIndex = new LayeredSymbolIndex(
                _symbolIndex,
                layeredIndexingOptions,
                gitService,
                null
            );
            _logger.LogInformation(
                "LayeredSymbolIndex enabled with {MaxBranchDeltas} max branch deltas",
                layeredIndexingOptions.MaxBranchDeltas
            );
        }

        // Initialize git workflow service if layered indexing and git service are available (Phase 5)
        if (gitService != null && _layeredIndex != null)
        {
            _gitWorkflowService = new GitWorkflowService(gitService, _layeredIndex, null, null);
            _logger.LogInformation("GitWorkflowService enabled for git workflow integration");
        }

        // Initialize incremental update queue (Phase 3)
        _incrementalUpdateQueue = new IncrementalUpdateQueue(_symbolIndex, null);

        // Инициализация MemoryCache с ограничениями
        // Compilation кэш: ~10-15 проектов, каждая компиляция ~50-100 MB
        _compilationCache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = 500 * 1024 * 1024, // 500 MB лимит
                ExpirationScanFrequency = TimeSpan.FromMinutes(5),
                CompactionPercentage = 0.25, // Удалить 25% при достижении лимита
            }
        );

        // SemanticModel кэш: ~50-100 документов, каждая модель ~10-20 MB
        _semanticModelCache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = 1024 * 1024 * 1024, // 1 GB лимит
                ExpirationScanFrequency = TimeSpan.FromMinutes(5),
                CompactionPercentage = 0.25,
            }
        );
    }

    public async Task LoadSolutionAsync(string solutionPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(solutionPath))
        {
            _logger.LogError("Solution file not found: {SolutionPath}", solutionPath);
            throw new FileNotFoundException("Solution file not found.", solutionPath);
        }

        // Normalize solution path to prevent duplicate loading from different path formats
        var normalizedPath = Path.GetFullPath(solutionPath);

        // Get or create semaphore for this solution path
        var loadLock = _loadingLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        // Try to acquire lock - if already loading, fail immediately
        if (!await loadLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Solution is already being loaded: {SolutionPath}", normalizedPath);
            throw new McpException(
                $"Solution '{normalizedPath}' is already being loaded. Please wait for the current loading operation to complete."
            );
        }

        try
        {
            UnloadSolution(); // Clears previous state including _allLoadedReflectionTypesCache

            // Store solution path for auto-reload
            _currentSolutionPath = solutionPath;

            try
            {
                _logger.LogInformation("Creating MSBuildWorkspace...");
                var properties = new Dictionary<string, string> { { "DesignTimeBuild", "true" } };

                if (!string.IsNullOrEmpty(_buildConfiguration))
                {
                    properties.Add("Configuration", _buildConfiguration);
                }

                _workspace = MSBuildWorkspace.Create(properties, MefHostServices.DefaultHost);
                _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailedHandler);
                _logger.LogInformation("Loading solution: {SolutionPath}", solutionPath);

                LogMemoryUsage("Before Solution Load");

                // Create extended timeout for solution loading (3 minutes)
                // Large solutions with many dependencies can take time to load through BuildHost
                using var extendedTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    extendedTimeoutCts.Token
                );

                _currentSolution = await _workspace.OpenSolutionAsync(
                    solutionPath,
                    new ProgressReporter(_logger),
                    linkedCts.Token
                );
                _logger.LogInformation(
                    "Solution loaded successfully with {ProjectCount} projects.",
                    _currentSolution.Projects.Count()
                );

                LogMemoryUsage("After Solution Load");

                InitializeMetadataContextAndReflectionCache(_currentSolution, cancellationToken);

                LogMemoryUsage("After Metadata Cache Init");

                // Build fast symbol index for optimized lookups
                await _symbolIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);

                LogMemoryUsage("After Symbol Index Build");

                // Build layered index if enabled (Phase 1+)
                if (_layeredIndex != null)
                {
                    await _layeredIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);
                    _logger.LogInformation("LayeredSymbolIndex built successfully");
                    LogMemoryUsage("After Layered Index Build");
                }

                // Initialize VectorStore if Semantic RAG is registered
                if (_vectorStoreInitializer != null)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Initializing VectorStore with solution path: {SolutionPath}",
                            solutionPath
                        );
                        await _vectorStoreInitializer.InitializeAsync(
                            solutionPath,
                            cancellationToken
                        );
                        LogMemoryUsage("After VectorStore Init");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to initialize VectorStore, semantic search may not be available"
                        );
                        // Non-critical: continue without semantic search
                    }
                }

                // Initialize FileSystemWatcher for automatic cache invalidation
                var solutionDirectory = Path.GetDirectoryName(solutionPath);
                if (!string.IsNullOrEmpty(solutionDirectory))
                {
                    InitializeFileSystemWatcher(solutionDirectory);

                    // Initialize project file watcher for auto-reload if enabled
                    if (_reloadOptions.AutoReloadEnabled)
                    {
                        InitializeProjectFileWatcher(solutionDirectory);
                    }
                }

                // Subscribe to workspace events for incremental updates (Phase 3)
                if (_workspace != null)
                {
                    _workspace.RegisterWorkspaceChangedHandler(OnWorkspaceChanged);
                    _logger.LogInformation(
                        "Subscribed to workspace change events for incremental updates"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load solution: {SolutionPath}", solutionPath);
                UnloadSolution();
                throw;
            }
        }
        finally
        {
            // Release the loading lock
            loadLock.Release();

            // Clean up completed locks to prevent memory leak
            if (_loadingLocks.TryRemove(normalizedPath, out var removedLock))
            {
                removedLock.Dispose();
            }
        }
    }

    /// <summary>
    /// Attempts to automatically discover and load a solution or project file.
    /// Searches up the directory tree from current directory for .sln, then .csproj files.
    /// </summary>
    /// <returns>True if a solution/project was found and loaded, false otherwise</returns>
    public async Task<bool> TryAutoLoadSolutionAsync(CancellationToken cancellationToken)
    {
        if (IsSolutionLoaded)
        {
            _logger.LogDebug("Solution already loaded, skipping auto-discovery");
            return true;
        }

        var currentDir = Environment.CurrentDirectory;
        _logger.LogInformation(
            "Starting auto-discovery of solution/project from: {Directory}",
            currentDir
        );

        // First, search for .sln files up the directory tree
        var solutionPath = SearchForFileUpwards(currentDir, "*.sln");
        if (solutionPath != null)
        {
            _logger.LogInformation("Auto-discovered solution file: {Path}", solutionPath);
            try
            {
                await LoadSolutionAsync(solutionPath, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to auto-load discovered solution: {Path}",
                    solutionPath
                );
            }
        }

        // If no .sln found, search for .csproj files
        var projectPath = SearchForFileUpwards(currentDir, "*.csproj");
        if (projectPath != null)
        {
            _logger.LogInformation("Auto-discovered project file: {Path}", projectPath);
            try
            {
                await LoadProjectAsync(projectPath, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to auto-load discovered project: {Path}",
                    projectPath
                );
            }
        }

        _logger.LogWarning(
            "No solution or project file found in directory tree starting from: {Directory}",
            currentDir
        );
        return false;
    }

    /// <summary>
    /// Searches for a file matching the pattern up the directory tree.
    /// </summary>
    /// <param name="startDirectory">Directory to start searching from</param>
    /// <param name="pattern">File pattern to search for (e.g., "*.sln")</param>
    /// <returns>Full path to the first matching file, or null if not found</returns>
    private string? SearchForFileUpwards(string startDirectory, string pattern)
    {
        try
        {
            var currentDir = new DirectoryInfo(startDirectory);

            while (currentDir != null)
            {
                var files = currentDir.GetFiles(pattern);
                if (files.Length > 0)
                {
                    _logger.LogDebug(
                        "Found {Pattern} file in: {Directory}",
                        pattern,
                        currentDir.FullName
                    );
                    return files[0].FullName;
                }

                currentDir = currentDir.Parent;
            }

            _logger.LogDebug("No {Pattern} file found in directory tree", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for {Pattern} files", pattern);
        }

        return null;
    }

    /// <summary>
    /// Loads a single project file (.csproj) as a standalone solution.
    /// </summary>
    private async Task LoadProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(projectPath))
        {
            _logger.LogError("Project file not found: {ProjectPath}", projectPath);
            throw new FileNotFoundException("Project file not found.", projectPath);
        }

        UnloadSolution();

        try
        {
            _logger.LogInformation("Creating MSBuildWorkspace for single project...");
            var properties = new Dictionary<string, string> { { "DesignTimeBuild", "true" } };

            if (!string.IsNullOrEmpty(_buildConfiguration))
            {
                properties.Add("Configuration", _buildConfiguration);
            }

            _workspace = MSBuildWorkspace.Create(properties, MefHostServices.DefaultHost);
            _workspace.RegisterWorkspaceFailedHandler(OnWorkspaceFailedHandler);
            _logger.LogInformation("Loading project: {ProjectPath}", projectPath);

            LogMemoryUsage("Before Project Load");

            var project = await _workspace.OpenProjectAsync(
                projectPath,
                new ProgressReporter(_logger),
                cancellationToken
            );
            _currentSolution = project.Solution;
            _logger.LogInformation("Project loaded successfully: {ProjectName}", project.Name);

            LogMemoryUsage("After Project Load");

            InitializeMetadataContextAndReflectionCache(_currentSolution, cancellationToken);

            LogMemoryUsage("After Metadata Cache Init");

            // Build fast symbol index for optimized lookups
            await _symbolIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);

            LogMemoryUsage("After Symbol Index Build");

            // Build layered index if enabled (Phase 1+)
            if (_layeredIndex != null)
            {
                await _layeredIndex.BuildFromSolutionAsync(_currentSolution, cancellationToken);
                _logger.LogInformation("LayeredSymbolIndex built successfully (Project)");
                LogMemoryUsage("After Layered Index Build (Project)");
            }

            // Initialize FileSystemWatcher for automatic cache invalidation
            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrEmpty(projectDirectory))
            {
                InitializeFileSystemWatcher(projectDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project: {ProjectPath}", projectPath);
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

    private static bool IsRelevantAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
        return RelevantAssemblyPrefixes.Any(prefix =>
            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );
    }

    // Мониторинг использования памяти
    private void LogMemoryUsage(string context)
    {
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        var totalMemoryMB = gcMemoryInfo.HeapSizeBytes / (1024.0 * 1024.0);
        var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        _logger.LogInformation(
            "[Memory] {Context} | Heap: {HeapMB:F1} MB | Working Set: {WorkingSetMB:F1} MB | GC: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}",
            context,
            totalMemoryMB,
            workingSetMB,
            gen0Collections,
            gen1Collections,
            gen2Collections
        );
    }

    private void InitializeMetadataContextAndReflectionCache(
        Solution solution,
        CancellationToken cancellationToken = default
    )
    {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        _assemblyPathsForReflection.Clear();

        // Add runtime assemblies с фильтрацией для оптимизации
        string[] allRuntimeAssemblies = Directory.GetFiles(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "*.dll"
        );
        _logger.LogInformation(
            "Found {TotalCount} runtime assemblies. Filtering relevant ones...",
            allRuntimeAssemblies.Length
        );

        string[] runtimeAssemblies = allRuntimeAssemblies.Where(IsRelevantAssembly).ToArray();

        _logger.LogInformation(
            "Filtered to {FilteredCount} relevant assemblies ({Percentage:F1}% reduction)",
            runtimeAssemblies.Length,
            100.0
                * (allRuntimeAssemblies.Length - runtimeAssemblies.Length)
                / allRuntimeAssemblies.Length
        );

        foreach (var assemblyPath in runtimeAssemblies)
        {
            // Check cancellation periodically
            cancellationToken.ThrowIfCancellationRequested();
            if (!_assemblyPathsForReflection.Contains(assemblyPath))
            {
                _assemblyPathsForReflection.Add(assemblyPath);
            }
        }

        // Load NuGet package assemblies from global cache instead of output directories
        var nugetAssemblies = GetNuGetAssemblyPaths(solution, cancellationToken);
        foreach (var assemblyPath in nugetAssemblies)
        {
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
        _logger.LogInformation(
            "MetadataLoadContext initialized with {PathCount} distinct search paths.",
            _assemblyPathsForReflection.Count
        );

        // Check cancellation before populating cache
        cancellationToken.ThrowIfCancellationRequested();

        PopulateReflectionCache(_assemblyPathsForReflection, cancellationToken);
    }

    private void PopulateReflectionCache(
        IEnumerable<string> assemblyPathsToInspect,
        CancellationToken cancellationToken = default
    )
    {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null)
        {
            _logger.LogWarning(
                "Cannot populate reflection cache: MetadataLoadContext not initialized."
            );
            return;
        }
        // _allLoadedReflectionTypesCache is cleared in UnloadSolution
        _logger.LogInformation("Starting population of reflection type cache (parallel mode)...");
        int typesCachedCount = 0;

        // Convert to list to avoid multiple enumeration and enable progress tracking
        var pathsList = assemblyPathsToInspect.ToList();
        int totalPaths = pathsList.Count;
        int processedPaths = 0;
        const int progressCheckInterval = 10; // Report progress and check cancellation every 10 assemblies

        // Use temporary ConcurrentDictionary for parallel collection, then build FrozenDictionary once
        var tempCache = new ConcurrentDictionary<string, Type>();

        // Parallel processing for 4-5x speedup
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        Parallel.ForEach(
            pathsList,
            parallelOptions,
            assemblyPath =>
            {
                var loadedCount = LoadTypesFromAssembly(assemblyPath, tempCache, cancellationToken);
                Interlocked.Add(ref typesCachedCount, loadedCount);

                // Thread-safe progress tracking
                var currentProcessed = Interlocked.Increment(ref processedPaths);
                if (currentProcessed % progressCheckInterval == 0)
                {
                    _logger.LogTrace(
                        "Reflection cache population progress: {Progress}% ({Current}/{Total})",
                        (int)((float)currentProcessed / totalPaths * 100),
                        currentProcessed,
                        totalPaths
                    );
                }
            }
        );

        // Build FrozenDictionary once after parallel collection (20-30% faster reads than Dictionary)
        _allLoadedReflectionTypesCache = tempCache.ToFrozenDictionary();

        _logger.LogInformation(
            "Reflection type cache population complete (parallel). Cached {Count} types from {AssemblyCount} unique assembly paths processed.",
            typesCachedCount,
            pathsList.Count
        );
    }

    private int LoadTypesFromAssembly(
        string assemblyPath,
        ConcurrentDictionary<string, Type> targetCache,
        CancellationToken cancellationToken = default
    )
    {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (
            _metadataLoadContext == null
            || string.IsNullOrEmpty(assemblyPath)
            || !File.Exists(assemblyPath)
        )
        {
            if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            {
                _logger.LogTrace(
                    "Assembly path is invalid or file does not exist, skipping for reflection cache: {Path}",
                    assemblyPath
                );
            }
            return 0;
        }

        int loadedTypesCount = 0;

        try
        {
            var assembly = _metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

            // For large assemblies, check cancellation periodically during type collection
            // We can't check during GetTypes() directly since it's atomic, but we can after
            cancellationToken.ThrowIfCancellationRequested();

            var types = assembly.GetTypes();
            int processedTypes = 0;
            const int typeCheckInterval = 50; // Check cancellation every 50 types

            foreach (var type in types)
            {
                // Check cancellation periodically when processing many types
                if (++processedTypes % typeCheckInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (type?.FullName != null && targetCache.TryAdd(type.FullName, type))
                {
                    loadedTypesCount++;
                }
            }
        }
        catch (ReflectionTypeLoadException rtlex)
        {
            _logger.LogWarning(
                "Could not load all types from assembly {Path} for reflection cache. LoaderExceptions: {Count}",
                assemblyPath,
                rtlex.LoaderExceptions.Length
            );
            foreach (var loaderEx in rtlex.LoaderExceptions.Where(e => e != null))
            {
                _logger.LogTrace("LoaderException: {Message}", loaderEx!.Message);
            }

            // For partial load errors, still process the types that did load
            int processedTypes = 0;
            const int typeCheckInterval = 20; // Check cancellation more frequently when dealing with problematic assemblies

            foreach (var type in rtlex.Types.Where(t => t != null))
            {
                // Check cancellation periodically
                if (++processedTypes % typeCheckInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (type!.FullName != null && targetCache.TryAdd(type.FullName, type))
                {
                    loadedTypesCount++;
                }
            }
        }
        catch (FileNotFoundException)
        { // Should be rare due to File.Exists check, but MLC might have its own resolution logic
            _logger.LogTrace("Assembly file not found by MetadataLoadContext: {Path}", assemblyPath);
        }
        catch (BadImageFormatException)
        {
            _logger.LogTrace("Bad image format for assembly file: {Path}", assemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error loading types from assembly {Path} for reflection cache.",
                assemblyPath
            );
        }

        return loadedTypesCount;
    }

    public void UnloadSolution()
    {
        _logger.LogInformation("Unloading current solution and workspace.");

        LogMemoryUsage("Before Unload");

        // Stop FileSystemWatcher
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
        }

        // Stop project file watcher
        if (_projectFileWatcher != null)
        {
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

        if (_workspace != null)
        {
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

        _logger.LogInformation("Solution unloaded. Caches compacted.");
    }

    public void RefreshCurrentSolution()
    {
        if (_workspace == null)
        {
            _logger.LogWarning("Cannot refresh solution: Workspace is null.");
            return;
        }
        if (_workspace.CurrentSolution == null)
        {
            _logger.LogWarning("Cannot refresh solution: No solution loaded.");
            return;
        }
        _currentSolution = _workspace.CurrentSolution;

        // No longer compacting caches - file-based invalidation handles stale entries automatically
        // FileSystemWatcher will invalidate modified files
        _logger.LogDebug(
            "Current solution state has been refreshed from workspace. Caches preserved with file-based invalidation."
        );
    }

    public async Task ReloadSolutionFromDiskAsync(CancellationToken cancellationToken)
    {
        if (_workspace == null)
        {
            _logger.LogWarning("Cannot reload solution: Workspace is null.");
            return;
        }
        if (_workspace.CurrentSolution == null)
        {
            _logger.LogWarning("Cannot reload solution: No solution loaded.");
            return;
        }
        await LoadSolutionAsync(_workspace.CurrentSolution.FilePath!, cancellationToken);
        _logger.LogDebug("Current solution state has been refreshed from workspace.");
    }

    private void OnWorkspaceFailedHandler(WorkspaceDiagnosticEventArgs e)
    {
        var diagnostic = e.Diagnostic;
        var level =
            diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                ? MsLogLevel.Error
                : MsLogLevel.Warning;
        _logger.Log(
            level,
            "Workspace diagnostic ({Kind}): {Message}",
            diagnostic.Kind,
            diagnostic.Message
        );
    }

    /// <summary>
    /// Handles workspace change events for incremental symbol index updates (Phase 3).
    /// </summary>
    private void OnWorkspaceChanged(WorkspaceChangeEventArgs e)
    {
        if (_incrementalUpdateQueue == null || e.NewSolution == null)
        {
            return;
        }

        try
        {
            // Enqueue updates based on change kind
            switch (e.Kind)
            {
                case WorkspaceChangeKind.DocumentAdded:
                    if (e.DocumentId != null)
                    {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Added,
                                e.NewSolution
                            )
                        );
                        _logger.LogDebug("Document added: {DocumentId}", e.DocumentId);
                    }
                    break;

                case WorkspaceChangeKind.DocumentChanged:
                case WorkspaceChangeKind.DocumentReloaded:
                    if (e.DocumentId != null)
                    {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Modified,
                                e.NewSolution
                            )
                        );
                        _logger.LogDebug("Document changed: {DocumentId}", e.DocumentId);
                    }
                    break;

                case WorkspaceChangeKind.DocumentRemoved:
                    if (e.DocumentId != null)
                    {
                        _ = _incrementalUpdateQueue.EnqueueAsync(
                            new DocumentUpdate(
                                e.DocumentId,
                                DocumentChangeKind.Removed,
                                e.NewSolution
                            )
                        );
                        _logger.LogDebug("Document removed: {DocumentId}", e.DocumentId);
                    }
                    break;

                // Ignore other change kinds for now (project-level changes, solution changes, etc.)
                default:
                    break;
            }

            // Update current solution reference
            _currentSolution = e.NewSolution;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling workspace change event: {Kind}", e.Kind);
        }
    }

    public async Task<INamedTypeSymbol?> FindRoslynNamedTypeSymbolAsync(
        string fullyQualifiedTypeName,
        CancellationToken cancellationToken
    )
    {
        if (!IsSolutionLoaded)
        {
            _logger.LogWarning("Cannot find Roslyn symbol: No solution loaded.");
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
        if (matchList.Count == 1)
        {
            var match = matchList.First();
            _logger.LogDebug(
                "Roslyn named type symbol found: {FullyQualifiedTypeName} (score: {Score}, reason: {Reason})",
                match.CanonicalFqn,
                match.Score,
                match.MatchReason
            );
            return (INamedTypeSymbol)match.Symbol;
        }
        if (matchList.Count > 1)
        {
            _logger.LogWarning(
                "Multiple matches found for {FullyQualifiedTypeName}",
                fullyQualifiedTypeName
            );
            throw new McpException(
                $"FQN was ambiguous, did you mean one of these?\n{string.Join("\n", matchList.Select(m => m.CanonicalFqn))}"
            );
        }
        // Direct lookup as fallback
        if (CurrentSolution == null)
        {
            _logger.LogWarning("Cannot perform direct lookup: No solution loaded.");
            return null;
        }
        foreach (var project in CurrentSolution.Projects)
        {
            // Check cancellation before each project
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await GetCompilationAsync(project.Id, cancellationToken);
            if (compilation == null)
            {
                continue;
            }
            var symbol = compilation.GetTypeByMetadataName(fullyQualifiedTypeName);
            if (symbol != null)
            {
                _logger.LogDebug(
                    "Roslyn named type symbol found via direct lookup: {FullyQualifiedTypeName} in project {ProjectName}",
                    fullyQualifiedTypeName,
                    project.Name
                );
                return symbol;
            }
        }
        // Check cancellation before nested type check
        cancellationToken.ThrowIfCancellationRequested();
        // Check for nested type with dot notation as last resort
        var lastDotIndex = fullyQualifiedTypeName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            var parentTypeName = fullyQualifiedTypeName.Substring(0, lastDotIndex);
            var nestedTypeName = fullyQualifiedTypeName.Substring(lastDotIndex + 1);
            foreach (var project in CurrentSolution.Projects)
            {
                // Check cancellation before each project
                cancellationToken.ThrowIfCancellationRequested();
                var compilation = await GetCompilationAsync(project.Id, cancellationToken);
                if (compilation == null)
                {
                    continue;
                }
                var parentSymbol = compilation.GetTypeByMetadataName(parentTypeName);
                if (parentSymbol != null)
                {
                    // Check if there's a nested type with this name
                    var nestedType = parentSymbol.GetTypeMembers(nestedTypeName).FirstOrDefault();
                    if (nestedType != null)
                    {
                        var correctName = $"{parentTypeName}+{nestedTypeName}";
                        _logger.LogWarning(
                            "Type not found: '{FullyQualifiedTypeName}'. This appears to be a nested type - use '{CorrectName}' instead (use + instead of . for nested types)",
                            fullyQualifiedTypeName,
                            correctName
                        );
                        throw new McpException(
                            $"Type not found: '{fullyQualifiedTypeName}'. This appears to be a nested type - use '{correctName}' instead (use + instead of . for nested types)"
                        );
                    }
                }
            }
        }
        _logger.LogDebug(
            "Roslyn named type symbol not found: {FullyQualifiedTypeName}",
            fullyQualifiedTypeName
        );
        return null;
    }

    public async Task<ISymbol?> FindRoslynSymbolAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken
    )
    {
        if (!IsSolutionLoaded)
        {
            _logger.LogWarning("Cannot find Roslyn symbol: No solution loaded.");
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

        if (matchList.Count == 1)
        {
            var match = matchList.First();
            _logger.LogDebug(
                "Roslyn symbol found: {FullyQualifiedName} (score: {Score}, reason: {Reason})",
                match.CanonicalFqn,
                match.Score,
                match.MatchReason
            );
            return match.Symbol;
        }

        if (matchList.Count > 1)
        {
            _logger.LogWarning(
                "Multiple matches found for {FullyQualifiedName}",
                fullyQualifiedName
            );
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
        if (typeSymbol != null)
        {
            return typeSymbol;
        }

        // Check cancellation before member lookup
        cancellationToken.ThrowIfCancellationRequested();

        // Check for member of a type as fallback
        var lastDotIndex = fullyQualifiedName.LastIndexOf('.');
        if (lastDotIndex > 0 && lastDotIndex < fullyQualifiedName.Length - 1)
        {
            var typeName = fullyQualifiedName.Substring(0, lastDotIndex);
            var memberName = fullyQualifiedName.Substring(lastDotIndex + 1);

            var parentTypeSymbol = await FindRoslynNamedTypeSymbolAsync(
                typeName,
                cancellationToken
            );
            if (parentTypeSymbol != null)
            {
                // Check cancellation before final lookup step
                cancellationToken.ThrowIfCancellationRequested();

                var members = parentTypeSymbol.GetMembers(memberName);
                if (members.Count() > 0)
                {
                    // TODO: Handle overloads if necessary, for now, take the first.
                    var memberSymbol = members.First();
                    _logger.LogDebug(
                        "Roslyn member symbol found: {FullyQualifiedName}",
                        fullyQualifiedName
                    );
                    return memberSymbol;
                }
            }
        }

        _logger.LogDebug("Roslyn symbol not found: {FullyQualifiedName}", fullyQualifiedName);
        return null;
    }

    public Task<Type?> FindReflectionTypeAsync(
        string fullyQualifiedTypeName,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation at the beginning of the method
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null)
        {
            _logger.LogWarning("Cannot find reflection type: MetadataLoadContext not initialized.");
            return Task.FromResult<Type?>(null);
        }
        if (_allLoadedReflectionTypesCache.TryGetValue(fullyQualifiedTypeName, out var type))
        {
            _logger.LogDebug("Reflection type found in cache: {Name}", fullyQualifiedTypeName);
            return Task.FromResult<Type?>(type);
        }
        _logger.LogDebug(
            "Reflection type '{FullyQualifiedTypeName}' not found in cache. It might not exist in the loaded solution's dependencies or was not loadable.",
            fullyQualifiedTypeName
        );
        return Task.FromResult<Type?>(null);
    }

    public Task<IEnumerable<Type>> SearchReflectionTypesAsync(
        string regexPattern,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation at the method entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (_metadataLoadContext == null)
        {
            _logger.LogWarning(
                "Cannot search reflection types: MetadataLoadContext not initialized."
            );
            return Task.FromResult<IEnumerable<Type>>([]);
        }
        if (_allLoadedReflectionTypesCache.Count() == 0)
        {
            _logger.LogInformation("Reflection type cache is empty. Search will yield no results.");
            return Task.FromResult<IEnumerable<Type>>([]);
        }

        // Check cancellation before regex compilation
        cancellationToken.ThrowIfCancellationRequested();

        var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var matchedTypes = new List<Type>();

        // Consider batching in chunks to check cancellation more frequently on large type caches
        int processedCount = 0;
        const int batchSize = 100; // Check cancellation every 100 types

        foreach (var typeEntry in _allLoadedReflectionTypesCache)
        { // Iterate KeyValuePair to access FQN directly
            if (++processedCount % batchSize == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Key is type.FullName which should not be null for cached types
            if (regex.IsMatch(typeEntry.Key))
            { // Search FQN
                matchedTypes.Add(typeEntry.Value);
            }
            else if (regex.IsMatch(typeEntry.Value.Name))
            { // Search simple name
                matchedTypes.Add(typeEntry.Value);
            }
        }

        // Check cancellation before returning results
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug(
            "Found {Count} reflection types matching pattern '{Pattern}'.",
            matchedTypes.Count,
            regexPattern
        );
        return Task.FromResult<IEnumerable<Type>>(matchedTypes.Distinct());
    }

    public IEnumerable<Project> GetProjects()
    {
        return CurrentSolution?.Projects ?? [];
    }

    public Project? GetProjectByName(string projectName)
    {
        if (!IsSolutionLoaded)
        {
            _logger.LogWarning("Cannot get project by name: No solution loaded.");
            return null;
        }
        var project = CurrentSolution?.Projects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)
        );
        if (project == null)
        {
            _logger.LogWarning("Project not found: {ProjectName}", projectName);
        }
        return project;
    }

    public ValueTask<SemanticModel?> GetSemanticModelAsync(
        DocumentId documentId,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSolutionLoaded)
        {
            _logger.LogWarning("Cannot get semantic model: No solution loaded.");
            return ValueTask.FromResult<SemanticModel?>(null);
        }

        // Fast path: check cache first with file-based validation
        if (_semanticModelCache.TryGetValue(documentId, out CacheEntry<SemanticModel>? cacheEntry))
        {
            // Validate cache entry based on file modification time
            if (cacheEntry!.IsValid())
            {
                Interlocked.Increment(ref _semanticModelCacheHits);
                _logger.LogTrace(
                    "Returning valid cached semantic model for document ID: {DocumentId}",
                    documentId
                );
                return ValueTask.FromResult<SemanticModel?>(cacheEntry.Value);
            }
            else
            {
                // File has been modified - invalidate cache entry
                _semanticModelCache.Remove(documentId);
                _logger.LogDebug(
                    "Semantic model cache invalidated (file modified): {FilePath}",
                    cacheEntry.FilePath
                );
            }
        }

        Interlocked.Increment(ref _semanticModelCacheMisses);

        // Slow path: load asynchronously
        return LoadSemanticModelAsync(documentId, cancellationToken);
    }

    private async ValueTask<SemanticModel?> LoadSemanticModelAsync(
        DocumentId documentId,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation before document lookup
        cancellationToken.ThrowIfCancellationRequested();

        if (CurrentSolution == null)
        {
            _logger.LogWarning("Cannot get semantic model: No solution loaded.");
            return null;
        }

        var document = CurrentSolution.GetDocument(documentId);
        if (document == null)
        {
            _logger.LogWarning("Document not found for ID: {DocumentId}", documentId);
            return null;
        }

        _logger.LogTrace(
            "Requesting semantic model for document: {DocumentFilePath}",
            document.FilePath
        );

        // Check cancellation before expensive GetSemanticModelAsync call
        cancellationToken.ThrowIfCancellationRequested();

        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (model != null && document.FilePath != null)
        {
            // Get file LastWriteTime for invalidation
            DateTime lastWriteTime;
            try
            {
                lastWriteTime = File.GetLastWriteTimeUtc(document.FilePath);
            }
            catch
            {
                lastWriteTime = DateTime.UtcNow;
                _logger.LogWarning(
                    "Could not get LastWriteTime for {FilePath}, using current time",
                    document.FilePath
                );
            }

            // Create cache entry with file metadata
            var cacheEntry = new CacheEntry<SemanticModel>
            {
                Value = model,
                FilePath = document.FilePath,
                LastWriteTimeUtc = lastWriteTime,
            };

            // Store with expiration and size settings
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(15 * 1024 * 1024) // Estimate: ~15 MB per semantic model
                .SetSlidingExpiration(TimeSpan.FromMinutes(30)) // Extend on use (increased from 10)
                .SetAbsoluteExpiration(TimeSpan.FromHours(2)) // Max 2 hours (increased from 30 min)
                .RegisterPostEvictionCallback(
                    (key, value, reason, state) =>
                    {
                        _logger.LogDebug(
                            "SemanticModel evicted from cache. DocumentId: {DocumentId}, Reason: {Reason}",
                            key,
                            reason
                        );
                    }
                );

            _semanticModelCache.Set(documentId, cacheEntry, cacheEntryOptions);

            // Maintain FilePath → DocumentId mapping for FileSystemWatcher
            _documentIdToFilePath[documentId] = document.FilePath;
            _filePathToDocumentId[document.FilePath] = documentId;

            _logger.LogTrace(
                "Cached semantic model for document: {DocumentFilePath} (LastWriteTime: {LastWriteTime})",
                document.FilePath,
                lastWriteTime
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to get semantic model for document: {DocumentFilePath}",
                document.FilePath
            );
        }
        return model;
    }

    public ValueTask<Compilation?> GetCompilationAsync(
        ProjectId projectId,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation at entry point
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSolutionLoaded)
        {
            _logger.LogWarning("Cannot get compilation: No solution loaded.");
            return ValueTask.FromResult<Compilation?>(null);
        }

        // Fast path: check cache first with file-based validation
        if (_compilationCache.TryGetValue(projectId, out ProjectCacheEntry? cacheEntry))
        {
            // Validate cache entry based on project file modification time
            if (cacheEntry!.IsValid())
            {
                Interlocked.Increment(ref _compilationCacheHits);
                _logger.LogTrace(
                    "Returning valid cached compilation for project ID: {ProjectId}",
                    projectId
                );
                return ValueTask.FromResult<Compilation?>(cacheEntry.Value);
            }
            else
            {
                // Project file has been modified - invalidate compilation and all related documents
                InvalidateCompilationInternal(projectId, cacheEntry);
                _logger.LogDebug(
                    "Compilation cache invalidated (project file modified): {ProjectPath}",
                    cacheEntry.ProjectFilePath
                );
            }
        }

        Interlocked.Increment(ref _compilationCacheMisses);

        // Slow path: load asynchronously
        return LoadCompilationAsync(projectId, cancellationToken);
    }

    private async ValueTask<Compilation?> LoadCompilationAsync(
        ProjectId projectId,
        CancellationToken cancellationToken
    )
    {
        // Check cancellation before project lookup
        cancellationToken.ThrowIfCancellationRequested();

        if (CurrentSolution == null)
        {
            _logger.LogWarning("Cannot get compilation: No solution loaded.");
            return null;
        }

        var project = CurrentSolution.GetProject(projectId);
        if (project == null)
        {
            _logger.LogWarning("Project not found for ID: {ProjectId}", projectId);
            return null;
        }

        _logger.LogTrace("Requesting compilation for project: {ProjectName}", project.Name);

        // Check cancellation before expensive GetCompilationAsync call
        cancellationToken.ThrowIfCancellationRequested();

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation != null && project.FilePath != null)
        {
            // Get project file LastWriteTime for invalidation
            DateTime lastWriteTime;
            try
            {
                lastWriteTime = File.GetLastWriteTimeUtc(project.FilePath);
            }
            catch
            {
                lastWriteTime = DateTime.UtcNow;
                _logger.LogWarning(
                    "Could not get LastWriteTime for {FilePath}, using current time",
                    project.FilePath
                );
            }

            // Collect all document IDs for cascade invalidation
            var documentIds = project.DocumentIds.ToHashSet();

            // Create project cache entry with metadata
            var cacheEntry = new ProjectCacheEntry
            {
                Value = compilation,
                ProjectFilePath = project.FilePath,
                LastWriteTimeUtc = lastWriteTime,
                DocumentIds = documentIds,
            };

            // Store with expiration and size settings
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(100 * 1024 * 1024) // Estimate: ~100 MB per compilation
                .SetSlidingExpiration(TimeSpan.FromMinutes(60)) // Extend on use (increased from 15)
                .SetAbsoluteExpiration(TimeSpan.FromHours(4)) // Max 4 hours (increased from 60 min)
                .RegisterPostEvictionCallback(
                    (key, value, reason, state) =>
                    {
                        _logger.LogDebug(
                            "Compilation evicted from cache. ProjectId: {ProjectId}, Reason: {Reason}",
                            key,
                            reason
                        );
                    }
                );

            _compilationCache.Set(projectId, cacheEntry, cacheEntryOptions);

            _logger.LogTrace(
                "Cached compilation for project: {ProjectName} (LastWriteTime: {LastWriteTime}, Documents: {DocumentCount})",
                project.Name,
                lastWriteTime,
                documentIds.Count
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to get compilation for project: {ProjectName}",
                project.Name
            );
        }
        return compilation;
    }

    /// <summary>
    /// Invalidate semantic model cache for a specific document (granular invalidation)
    /// </summary>
    public void InvalidateSemanticModel(DocumentId documentId)
    {
        _semanticModelCache.Remove(documentId);
        _logger.LogDebug(
            "Invalidated semantic model cache for DocumentId: {DocumentId}",
            documentId
        );

        // Remove from mapping
        if (_documentIdToFilePath.TryRemove(documentId, out var filePath))
        {
            _filePathToDocumentId.TryRemove(filePath, out _);
        }
    }

    /// <summary>
    /// Invalidate compilation and all related semantic models (cascade invalidation)
    /// </summary>
    public void InvalidateCompilation(ProjectId projectId)
    {
        if (_compilationCache.TryGetValue(projectId, out ProjectCacheEntry? cacheEntry))
        {
            InvalidateCompilationInternal(projectId, cacheEntry!);
        }
    }

    private void InvalidateCompilationInternal(ProjectId projectId, ProjectCacheEntry cacheEntry)
    {
        // Remove compilation from cache
        _compilationCache.Remove(projectId);
        _logger.LogDebug("Invalidated compilation cache for ProjectId: {ProjectId}", projectId);

        // Cascade: invalidate all semantic models for documents in this project
        foreach (var documentId in cacheEntry.DocumentIds)
        {
            InvalidateSemanticModel(documentId);
        }
        _logger.LogDebug(
            "Cascade invalidation: removed {Count} document semantic models",
            cacheEntry.DocumentIds.Count
        );
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        var totalMemory = GC.GetTotalMemory(forceFullCollection: false);

        return new CacheStatistics
        {
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
    private void InitializeFileSystemWatcher(string solutionDirectory)
    {
        try
        {
            _fileWatcher = new FileSystemWatcher(solutionDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.cs",
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;

            // Debounce mechanism to avoid multiple events for same file
            _fileWatcher.EnableRaisingEvents = true;

            _logger.LogInformation(
                "FileSystemWatcher initialized for directory: {Directory}",
                solutionDirectory
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize FileSystemWatcher for {Directory}. Automatic cache invalidation disabled.",
                solutionDirectory
            );
            _fileWatcher = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Find DocumentId by file path and invalidate
        if (_filePathToDocumentId.TryGetValue(e.FullPath, out var documentId))
        {
            _logger.LogDebug("File changed detected by FileSystemWatcher: {FilePath}", e.FullPath);
            InvalidateSemanticModel(documentId);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        // File deleted - invalidate cache
        if (_filePathToDocumentId.TryGetValue(e.FullPath, out var documentId))
        {
            _logger.LogDebug("File deleted detected by FileSystemWatcher: {FilePath}", e.FullPath);
            InvalidateSemanticModel(documentId);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Old file path - invalidate
        if (_filePathToDocumentId.TryGetValue(e.OldFullPath, out var documentId))
        {
            _logger.LogDebug(
                "File renamed detected by FileSystemWatcher: {OldPath} -> {NewPath}",
                e.OldFullPath,
                e.FullPath
            );
            InvalidateSemanticModel(documentId);

            // Update mapping with new path
            if (CurrentSolution != null)
            {
                var document = CurrentSolution.GetDocument(documentId);
                if (document != null && document.FilePath == e.FullPath)
                {
                    _documentIdToFilePath[documentId] = e.FullPath;
                    _filePathToDocumentId[e.FullPath] = documentId;
                }
            }
        }
    }

    /// <summary>
    /// Initialize FileSystemWatcher for project files (.csproj, .sln) to trigger automatic reload
    /// </summary>
    private void InitializeProjectFileWatcher(string solutionDirectory)
    {
        try
        {
            _projectFileWatcher = new FileSystemWatcher(solutionDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };

            // Watch all configured extensions
            if (_reloadOptions.WatchedExtensions?.Length > 0)
            {
                // FileSystemWatcher doesn't support multiple filters directly, so we'll filter in the event handler
                _projectFileWatcher.Filter = "*.*";
            }

            _projectFileWatcher.Changed += OnProjectFileChanged;

            _projectFileWatcher.EnableRaisingEvents = true;

            _logger.LogInformation(
                "Project file watcher initialized for auto-reload. Watching: {Extensions}, Debounce: {DebounceMs}ms",
                string.Join(", ", _reloadOptions.WatchedExtensions ?? Array.Empty<string>()),
                _reloadOptions.DebounceDelayMs
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize project file watcher for {Directory}. Auto-reload disabled.",
                solutionDirectory
            );
            _projectFileWatcher = null;
        }
    }

    private void OnProjectFileChanged(object sender, FileSystemEventArgs e)
    {
        // Check if the file extension is in the watched list
        var extension = Path.GetExtension(e.FullPath);
        if (
            _reloadOptions.WatchedExtensions == null
            || !_reloadOptions.WatchedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        _logger.LogInformation(
            "Project file change detected: {FilePath}. Scheduling reload with {DebounceMs}ms debounce.",
            e.FullPath,
            _reloadOptions.DebounceDelayMs
        );

        // Reset debounce timer - if multiple files change, we only reload once after the last change
        _reloadDebounceTimer?.Dispose();
        _reloadDebounceTimer = new System.Threading.Timer(
            async _ => await TriggerAutoReloadAsync(),
            null,
            _reloadOptions.DebounceDelayMs,
            Timeout.Infinite
        );
    }

    private async Task TriggerAutoReloadAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentSolutionPath))
            {
                _logger.LogWarning("Cannot auto-reload: Solution path not stored");
                return;
            }

            _logger.LogInformation("Auto-reloading solution: {SolutionPath}", _currentSolutionPath);

            await ReloadSolutionFromDiskAsync(CancellationToken.None);

            _logger.LogInformation("Auto-reload completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-reload");
        }
        finally
        {
            // Dispose the timer after it fires
            _reloadDebounceTimer?.Dispose();
            _reloadDebounceTimer = null;
        }
    }

    public void Dispose()
    {
        // Note: Workspace.RegisterWorkspaceChangedHandler doesn't provide unsubscribe mechanism
        // The handler will be disposed when workspace is disposed in UnloadSolution

        UnloadSolution();

        // Dispose IncrementalUpdateQueue (Phase 3)
        _incrementalUpdateQueue?.Dispose();
        _incrementalUpdateQueue = null;

        // Dispose FileSystemWatcher
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Deleted -= OnFileDeleted;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        // Dispose project file watcher
        if (_projectFileWatcher != null)
        {
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

    private class ProgressReporter : IProgress<ProjectLoadProgress>
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public ProgressReporter(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void Report(ProjectLoadProgress loadProgress)
        {
            var projectDisplay = Path.GetFileName(loadProgress.FilePath);
            _logger.LogTrace(
                "Project Load Progress: {ProjectDisplayName}, Operation: {Operation}, Time: {TimeElapsed}",
                projectDisplay,
                loadProgress.Operation,
                loadProgress.ElapsedTime
            );
        }
    }

    private HashSet<string> GetNuGetAssemblyPaths(
        Solution solution,
        CancellationToken cancellationToken = default
    )
    {
        var nugetAssemblyPaths = new ConcurrentBag<string>();
        var nugetCacheDir = GetNuGetGlobalPackagesFolder();

        if (string.IsNullOrEmpty(nugetCacheDir) || !Directory.Exists(nugetCacheDir))
        {
            _logger.LogWarning(
                "NuGet global packages folder not found or inaccessible: {NuGetCacheDir}",
                nugetCacheDir
            );
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Parallel processing of projects for 3-4x speedup
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4, // I/O bound operations
            CancellationToken = cancellationToken,
        };

        Parallel.ForEach(
            solution.Projects,
            parallelOptions,
            project =>
            {
                if (string.IsNullOrEmpty(project.FilePath))
                {
                    return;
                }

                var packageReferences = LegacyNuGetPackageReader.GetAllPackageReferences(
                    project.FilePath
                );
                var projectTargetFramework = SolutionTools.ExtractTargetFrameworkFromProjectFile(
                    project.FilePath
                );

                foreach (var package in packageReferences)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var packageDir = Path.Combine(
                        nugetCacheDir,
                        package.PackageId.ToLowerInvariant(),
                        package.Version
                    );
                    if (!Directory.Exists(packageDir))
                    {
                        _logger.LogTrace("Package directory not found: {PackageDir}", packageDir);
                        continue;
                    }

                    var libDir = Path.Combine(packageDir, "lib");
                    if (!Directory.Exists(libDir))
                    {
                        _logger.LogTrace(
                            "No lib directory found for package {PackageId} {Version}",
                            package.PackageId,
                            package.Version
                        );
                        continue;
                    }

                    // Find assemblies using the project's target framework
                    var assemblyPaths = GetAssembliesForTargetFramework(
                        libDir,
                        package.TargetFramework ?? projectTargetFramework,
                        package.PackageId,
                        package.Version
                    );
                    foreach (var assemblyPath in assemblyPaths)
                    {
                        nugetAssemblyPaths.Add(assemblyPath);
                    }
                }
            }
        );

        var resultSet = nugetAssemblyPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation(
            "Found {AssemblyCount} NuGet assemblies from global packages cache (parallel mode)",
            resultSet.Count
        );
        return resultSet;
    }

    private static string GetNuGetGlobalPackagesFolder()
    {
        // Check environment variable first
        var globalPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(globalPackagesPath))
        {
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
    )
    {
        var assemblies = new List<string>();

        if (!Directory.Exists(libDir))
        {
            return assemblies;
        }

        // First try exact target framework match
        var exactFrameworkDir = Path.Combine(libDir, targetFramework);
        if (Directory.Exists(exactFrameworkDir))
        {
            var exactAssemblies = Directory.GetFiles(
                exactFrameworkDir,
                "*.dll",
                SearchOption.TopDirectoryOnly
            );
            assemblies.AddRange(exactAssemblies);
            _logger.LogTrace(
                "Found {AssemblyCount} assemblies in exact framework match {Framework} for {PackageId} {Version}",
                exactAssemblies.Length,
                targetFramework,
                packageId,
                version
            );
            return assemblies;
        }

        // Try compatible frameworks in order of preference
        var compatibleFrameworks = GetCompatibleFrameworks(targetFramework);

        foreach (var framework in compatibleFrameworks)
        {
            var frameworkDir = Path.Combine(libDir, framework);
            if (Directory.Exists(frameworkDir))
            {
                var frameworkAssemblies = Directory.GetFiles(
                    frameworkDir,
                    "*.dll",
                    SearchOption.TopDirectoryOnly
                );
                assemblies.AddRange(frameworkAssemblies);
                _logger.LogTrace(
                    "Found {AssemblyCount} assemblies in compatible framework {Framework} for {PackageId} {Version}",
                    frameworkAssemblies.Length,
                    framework,
                    packageId,
                    version
                );
                return assemblies; // Take the first compatible framework found
            }
        }

        // Fallback: check if there are any DLLs directly in lib directory
        if (assemblies.Count == 0)
        {
            var libAssemblies = Directory.GetFiles(libDir, "*.dll", SearchOption.TopDirectoryOnly);
            assemblies.AddRange(libAssemblies);
            if (libAssemblies.Length > 0)
            {
                _logger.LogTrace(
                    "Found {AssemblyCount} assemblies in lib root for {PackageId} {Version}",
                    libAssemblies.Length,
                    packageId,
                    version
                );
            }
        }

        return assemblies;
    }

    private static string[] GetCompatibleFrameworks(string targetFramework)
    {
        // Return frameworks in order of compatibility preference
        return targetFramework.ToLowerInvariant() switch
        {
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

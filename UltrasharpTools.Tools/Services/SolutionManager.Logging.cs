using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public sealed partial class SolutionManager
{
    // Constructor and initialization (4100-4110)
    [LoggerMessage(EventId = 4100, Level = LogLevel.Information,
        Message = "Low memory mode enabled - using SQLite-based reflection type index")]
    private partial void LogLowMemoryModeEnabled();

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information,
        Message = "Clearing symbol cache on startup")]
    private partial void LogClearingSymbolCache();

    [LoggerMessage(EventId = 4102, Level = LogLevel.Information,
        Message = "LayeredSymbolIndex enabled with {MaxBranchDeltas} max branch deltas")]
    private partial void LogLayeredIndexEnabled(int maxBranchDeltas);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Information,
        Message = "GitWorkflowService enabled for git workflow integration")]
    private partial void LogGitWorkflowServiceEnabled();

    [LoggerMessage(EventId = 4104, Level = LogLevel.Information,
        Message = "MemoryCache initialized: Compilation={CompilationMB}MB, SemanticModel={SemanticMB}MB")]
    private partial void LogMemoryCacheInitialized(int compilationMB, int semanticMB);

    // Solution loading (4110-4130)
    [LoggerMessage(EventId = 4110, Level = LogLevel.Error,
        Message = "Solution file not found: {SolutionPath}")]
    private partial void LogSolutionFileNotFound(string solutionPath);

    [LoggerMessage(EventId = 4111, Level = LogLevel.Warning,
        Message = "Solution is already being loaded: {SolutionPath}")]
    private partial void LogSolutionAlreadyLoading(string solutionPath);

    [LoggerMessage(EventId = 4112, Level = LogLevel.Information,
        Message = "Creating MSBuildWorkspace...")]
    private partial void LogCreatingWorkspace();

    [LoggerMessage(EventId = 4113, Level = LogLevel.Information,
        Message = "Loading solution: {SolutionPath}")]
    private partial void LogLoadingSolution(string solutionPath);

    [LoggerMessage(EventId = 4114, Level = LogLevel.Information,
        Message = "Solution loaded successfully with {ProjectCount} projects.")]
    private partial void LogSolutionLoaded(int projectCount);

    [LoggerMessage(EventId = 4115, Level = LogLevel.Information,
        Message = "LayeredSymbolIndex built successfully")]
    private partial void LogLayeredIndexBuilt();

    [LoggerMessage(EventId = 4116, Level = LogLevel.Information,
        Message = "Initializing VectorStore with solution path: {SolutionPath}")]
    private partial void LogInitializingVectorStore(string solutionPath);

    [LoggerMessage(EventId = 4117, Level = LogLevel.Warning,
        Message = "Failed to initialize VectorStore, semantic search may not be available")]
    private partial void LogVectorStoreInitFailed(Exception exception);

    [LoggerMessage(EventId = 4118, Level = LogLevel.Information,
        Message = "Subscribed to workspace change events for incremental updates")]
    private partial void LogSubscribedToWorkspaceEvents();

    [LoggerMessage(EventId = 4119, Level = LogLevel.Error,
        Message = "Failed to load solution: {SolutionPath}")]
    private partial void LogSolutionLoadFailed(Exception exception, string solutionPath);

    // Auto-discovery (4130-4140)
    [LoggerMessage(EventId = 4130, Level = LogLevel.Debug,
        Message = "Solution already loaded, skipping auto-discovery")]
    private partial void LogSolutionAlreadyLoaded();

    [LoggerMessage(EventId = 4131, Level = LogLevel.Information,
        Message = "Starting auto-discovery of solution/project from: {Directory}")]
    private partial void LogStartingAutoDiscovery(string directory);

    [LoggerMessage(EventId = 4132, Level = LogLevel.Information,
        Message = "Auto-discovered solution file: {Path}")]
    private partial void LogAutoDiscoveredSolution(string path);

    [LoggerMessage(EventId = 4133, Level = LogLevel.Warning,
        Message = "Failed to auto-load discovered solution: {Path}")]
    private partial void LogAutoLoadSolutionFailed(Exception exception, string path);

    [LoggerMessage(EventId = 4134, Level = LogLevel.Information,
        Message = "Auto-discovered project file: {Path}")]
    private partial void LogAutoDiscoveredProject(string path);

    [LoggerMessage(EventId = 4135, Level = LogLevel.Warning,
        Message = "Failed to auto-load discovered project: {Path}")]
    private partial void LogAutoLoadProjectFailed(Exception exception, string path);

    [LoggerMessage(EventId = 4136, Level = LogLevel.Warning,
        Message = "No solution or project file found in directory tree starting from: {Directory}")]
    private partial void LogNoSolutionOrProjectFound(string directory);

    [LoggerMessage(EventId = 4137, Level = LogLevel.Debug,
        Message = "Found {Pattern} file in: {Directory}")]
    private partial void LogFoundFilePattern(string pattern, string directory);

    [LoggerMessage(EventId = 4138, Level = LogLevel.Debug,
        Message = "No {Pattern} file found in directory tree")]
    private partial void LogNoFilePatternFound(string pattern);

    [LoggerMessage(EventId = 4139, Level = LogLevel.Warning,
        Message = "Error searching for {Pattern} files")]
    private partial void LogFileSearchError(Exception exception, string pattern);

    // Project loading (4140-4150)
    [LoggerMessage(EventId = 4140, Level = LogLevel.Error,
        Message = "Project file not found: {ProjectPath}")]
    private partial void LogProjectFileNotFound(string projectPath);

    [LoggerMessage(EventId = 4141, Level = LogLevel.Information,
        Message = "Creating MSBuildWorkspace for single project...")]
    private partial void LogCreatingWorkspaceForProject();

    [LoggerMessage(EventId = 4142, Level = LogLevel.Information,
        Message = "Loading project: {ProjectPath}")]
    private partial void LogLoadingProject(string projectPath);

    [LoggerMessage(EventId = 4143, Level = LogLevel.Information,
        Message = "Project loaded successfully: {ProjectName}")]
    private partial void LogProjectLoaded(string projectName);

    [LoggerMessage(EventId = 4144, Level = LogLevel.Information,
        Message = "LayeredSymbolIndex built successfully (Project)")]
    private partial void LogLayeredIndexBuiltProject();

    [LoggerMessage(EventId = 4145, Level = LogLevel.Error,
        Message = "Failed to load project: {ProjectPath}")]
    private partial void LogProjectLoadFailed(Exception exception, string projectPath);

    // Memory logging (4150-4160)
    [LoggerMessage(EventId = 4150, Level = LogLevel.Information,
        Message = "[Memory] {Context} | Heap: {HeapMB:F1} MB | Working Set: {WorkingSetMB:F1} MB | GC: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}")]
    private partial void LogMemoryUsageInternal(string context, double heapMB, double workingSetMB, int gen0, int gen1, int gen2);

    // Metadata and reflection cache (4160-4180)
    [LoggerMessage(EventId = 4160, Level = LogLevel.Information,
        Message = "Found {TotalCount} runtime assemblies. Filtering relevant ones...")]
    private partial void LogFoundRuntimeAssemblies(int totalCount);

    [LoggerMessage(EventId = 4161, Level = LogLevel.Information,
        Message = "Filtered to {FilteredCount} relevant assemblies ({Percentage:F1}% reduction)")]
    private partial void LogFilteredAssemblies(int filteredCount, double percentage);

    [LoggerMessage(EventId = 4162, Level = LogLevel.Information,
        Message = "MetadataLoadContext initialized with {PathCount} distinct search paths.")]
    private partial void LogMetadataContextInitialized(int pathCount);

    [LoggerMessage(EventId = 4163, Level = LogLevel.Warning,
        Message = "Cannot populate reflection cache: MetadataLoadContext not initialized.")]
    private partial void LogMetadataContextNotInitialized();

    [LoggerMessage(EventId = 4164, Level = LogLevel.Information,
        Message = "Starting population of reflection type cache (parallel mode)...")]
    private partial void LogStartingReflectionCacheParallel();

    [LoggerMessage(EventId = 4165, Level = LogLevel.Trace,
        Message = "Reflection cache population progress: {Progress}% ({Current}/{Total})")]
    private partial void LogReflectionCacheProgress(int progress, int current, int total);

    [LoggerMessage(EventId = 4166, Level = LogLevel.Information,
        Message = "Reflection type cache population complete (parallel). Cached {Count} types from {AssemblyCount} unique assembly paths processed.")]
    private partial void LogReflectionCacheComplete(int count, int assemblyCount);

    [LoggerMessage(EventId = 4167, Level = LogLevel.Information,
        Message = "Starting population of reflection type cache (SQLite low-memory mode)...")]
    private partial void LogStartingReflectionCacheSqlite();

    [LoggerMessage(EventId = 4168, Level = LogLevel.Information,
        Message = "SQLite reflection type cache populated with {Count} types")]
    private partial void LogSqliteReflectionCachePopulated(int count);

    [LoggerMessage(EventId = 4169, Level = LogLevel.Trace,
        Message = "Assembly path is invalid or file does not exist, skipping for reflection cache: {Path}")]
    private partial void LogInvalidAssemblyPath(string? path);

    [LoggerMessage(EventId = 4170, Level = LogLevel.Warning,
        Message = "Could not load all types from assembly {Path} for reflection cache. LoaderExceptions: {Count}")]
    private partial void LogPartialTypeLoad(string path, int count);

    [LoggerMessage(EventId = 4171, Level = LogLevel.Trace,
        Message = "LoaderException: {Message}")]
    private partial void LogLoaderException(string message);

    [LoggerMessage(EventId = 4172, Level = LogLevel.Trace,
        Message = "Assembly file not found by MetadataLoadContext: {Path}")]
    private partial void LogAssemblyFileNotFound(string path);

    [LoggerMessage(EventId = 4173, Level = LogLevel.Trace,
        Message = "Bad image format for assembly file: {Path}")]
    private partial void LogBadImageFormat(string path);

    [LoggerMessage(EventId = 4174, Level = LogLevel.Warning,
        Message = "Error loading types from assembly {Path} for reflection cache.")]
    private partial void LogTypeLoadError(Exception exception, string path);

    // Solution unload and refresh (4180-4190)
    [LoggerMessage(EventId = 4180, Level = LogLevel.Information,
        Message = "Unloading current solution and workspace.")]
    private partial void LogUnloadingSolution();

    [LoggerMessage(EventId = 4181, Level = LogLevel.Information,
        Message = "Solution unloaded. Caches compacted.")]
    private partial void LogSolutionUnloaded();

    [LoggerMessage(EventId = 4182, Level = LogLevel.Warning,
        Message = "Cannot refresh solution: Workspace is null.")]
    private partial void LogRefreshWorkspaceNull();

    [LoggerMessage(EventId = 4183, Level = LogLevel.Warning,
        Message = "Cannot refresh solution: No solution loaded.")]
    private partial void LogRefreshNoSolution();

    [LoggerMessage(EventId = 4184, Level = LogLevel.Debug,
        Message = "Current solution state has been refreshed from workspace. Caches preserved with file-based invalidation.")]
    private partial void LogSolutionRefreshed();

    [LoggerMessage(EventId = 4185, Level = LogLevel.Warning,
        Message = "Cannot reload solution: Workspace is null.")]
    private partial void LogReloadWorkspaceNull();

    [LoggerMessage(EventId = 4186, Level = LogLevel.Warning,
        Message = "Cannot reload solution: No solution loaded.")]
    private partial void LogReloadNoSolution();

    [LoggerMessage(EventId = 4187, Level = LogLevel.Debug,
        Message = "Current solution state has been refreshed from workspace.")]
    private partial void LogSolutionReloaded();

    // Workspace events (4190-4200)
    [LoggerMessage(EventId = 4190, Level = LogLevel.Debug,
        Message = "Document added: {DocumentId}")]
    private partial void LogDocumentAdded(DocumentId documentId);

    [LoggerMessage(EventId = 4191, Level = LogLevel.Debug,
        Message = "Document changed: {DocumentId}")]
    private partial void LogDocumentChanged(DocumentId documentId);

    [LoggerMessage(EventId = 4192, Level = LogLevel.Debug,
        Message = "Document removed: {DocumentId}")]
    private partial void LogDocumentRemoved(DocumentId documentId);

    [LoggerMessage(EventId = 4193, Level = LogLevel.Warning,
        Message = "Error handling workspace change event: {Kind}")]
    private partial void LogWorkspaceChangeError(Exception exception, WorkspaceChangeKind kind);

    // Symbol lookup (4200-4220)
    [LoggerMessage(EventId = 4200, Level = LogLevel.Warning,
        Message = "Cannot find Roslyn symbol: No solution loaded.")]
    private partial void LogCannotFindSymbolNoSolution();

    [LoggerMessage(EventId = 4201, Level = LogLevel.Debug,
        Message = "Roslyn named type symbol found: {FullyQualifiedTypeName} (score: {Score}, reason: {Reason})")]
    private partial void LogNamedTypeSymbolFound(string fullyQualifiedTypeName, double score, string reason);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Warning,
        Message = "Multiple matches found for {FullyQualifiedTypeName}")]
    private partial void LogMultipleMatchesFound(string fullyQualifiedTypeName);

    [LoggerMessage(EventId = 4203, Level = LogLevel.Warning,
        Message = "Cannot perform direct lookup: No solution loaded.")]
    private partial void LogCannotDirectLookupNoSolution();

    [LoggerMessage(EventId = 4204, Level = LogLevel.Debug,
        Message = "Roslyn named type symbol found via direct lookup: {FullyQualifiedTypeName} in project {ProjectName}")]
    private partial void LogNamedTypeSymbolFoundDirect(string fullyQualifiedTypeName, string projectName);

    [LoggerMessage(EventId = 4205, Level = LogLevel.Warning,
        Message = "Type not found: '{FullyQualifiedTypeName}'. This appears to be a nested type - use '{CorrectName}' instead (use + instead of . for nested types)")]
    private partial void LogNestedTypeHint(string fullyQualifiedTypeName, string correctName);

    [LoggerMessage(EventId = 4206, Level = LogLevel.Debug,
        Message = "Roslyn named type symbol not found: {FullyQualifiedTypeName}")]
    private partial void LogNamedTypeSymbolNotFound(string fullyQualifiedTypeName);

    [LoggerMessage(EventId = 4207, Level = LogLevel.Debug,
        Message = "Roslyn symbol found: {FullyQualifiedName} (score: {Score}, reason: {Reason})")]
    private partial void LogSymbolFound(string fullyQualifiedName, double score, string reason);

    [LoggerMessage(EventId = 4208, Level = LogLevel.Debug,
        Message = "Roslyn member symbol found: {FullyQualifiedName}")]
    private partial void LogMemberSymbolFound(string fullyQualifiedName);

    [LoggerMessage(EventId = 4209, Level = LogLevel.Debug,
        Message = "Roslyn symbol not found: {FullyQualifiedName}")]
    private partial void LogSymbolNotFound(string fullyQualifiedName);

    // Reflection type lookup (4220-4235)
    [LoggerMessage(EventId = 4220, Level = LogLevel.Warning,
        Message = "Cannot find reflection type: MetadataLoadContext not initialized.")]
    private partial void LogCannotFindReflectionType();

    [LoggerMessage(EventId = 4221, Level = LogLevel.Debug,
        Message = "Reflection type found in SQLite index: {Name}")]
    private partial void LogReflectionTypeFoundSqlite(string name);

    [LoggerMessage(EventId = 4222, Level = LogLevel.Debug,
        Message = "Reflection type found in cache: {Name}")]
    private partial void LogReflectionTypeFoundCache(string name);

    [LoggerMessage(EventId = 4223, Level = LogLevel.Debug,
        Message = "Reflection type '{FullyQualifiedTypeName}' not found in cache. It might not exist in the loaded solution's dependencies or was not loadable.")]
    private partial void LogReflectionTypeNotFound(string fullyQualifiedTypeName);

    [LoggerMessage(EventId = 4224, Level = LogLevel.Warning,
        Message = "Cannot search reflection types: MetadataLoadContext not initialized.")]
    private partial void LogCannotSearchReflectionTypes();

    [LoggerMessage(EventId = 4225, Level = LogLevel.Debug,
        Message = "Found {Count} reflection types matching pattern '{Pattern}' (SQLite mode).")]
    private partial void LogReflectionTypesFoundSqlite(int count, string pattern);

    [LoggerMessage(EventId = 4226, Level = LogLevel.Information,
        Message = "Reflection type cache is empty. Search will yield no results.")]
    private partial void LogReflectionCacheEmpty();

    [LoggerMessage(EventId = 4227, Level = LogLevel.Debug,
        Message = "Found {Count} reflection types matching pattern '{Pattern}'.")]
    private partial void LogReflectionTypesFound(int count, string pattern);

    // Project access (4235-4240)
    [LoggerMessage(EventId = 4235, Level = LogLevel.Warning,
        Message = "Cannot get project by name: No solution loaded.")]
    private partial void LogCannotGetProjectNoSolution();

    [LoggerMessage(EventId = 4236, Level = LogLevel.Warning,
        Message = "Project not found: {ProjectName}")]
    private partial void LogProjectNotFound(string projectName);

    // Semantic model cache (4240-4260)
    [LoggerMessage(EventId = 4240, Level = LogLevel.Warning,
        Message = "Cannot get semantic model: No solution loaded.")]
    private partial void LogCannotGetSemanticModelNoSolution();

    [LoggerMessage(EventId = 4241, Level = LogLevel.Trace,
        Message = "Returning valid cached semantic model for document ID: {DocumentId}")]
    private partial void LogSemanticModelCacheHit(DocumentId documentId);

    [LoggerMessage(EventId = 4242, Level = LogLevel.Debug,
        Message = "Semantic model cache invalidated (file modified): {FilePath}")]
    private partial void LogSemanticModelCacheInvalidated(string? filePath);

    [LoggerMessage(EventId = 4243, Level = LogLevel.Warning,
        Message = "Document not found for ID: {DocumentId}")]
    private partial void LogDocumentNotFound(DocumentId documentId);

    [LoggerMessage(EventId = 4244, Level = LogLevel.Trace,
        Message = "Requesting semantic model for document: {DocumentFilePath}")]
    private partial void LogRequestingSemanticModel(string? documentFilePath);

    [LoggerMessage(EventId = 4245, Level = LogLevel.Warning,
        Message = "Could not get LastWriteTime for {FilePath}, using current time")]
    private partial void LogLastWriteTimeFailed(string? filePath);

    [LoggerMessage(EventId = 4246, Level = LogLevel.Debug,
        Message = "SemanticModel evicted from cache. DocumentId: {DocumentId}, Reason: {Reason}")]
    private partial void LogSemanticModelEvicted(string documentId, EvictionReason reason);

    [LoggerMessage(EventId = 4247, Level = LogLevel.Trace,
        Message = "Cached semantic model for document: {DocumentFilePath} (LastWriteTime: {LastWriteTime})")]
    private partial void LogSemanticModelCached(string? documentFilePath, DateTime lastWriteTime);

    [LoggerMessage(EventId = 4248, Level = LogLevel.Warning,
        Message = "Failed to get semantic model for document: {DocumentFilePath}")]
    private partial void LogSemanticModelFailed(string? documentFilePath);

    // Compilation cache (4260-4280)
    [LoggerMessage(EventId = 4260, Level = LogLevel.Warning,
        Message = "Cannot get compilation: No solution loaded.")]
    private partial void LogCannotGetCompilationNoSolution();

    [LoggerMessage(EventId = 4261, Level = LogLevel.Trace,
        Message = "Returning valid cached compilation for project ID: {ProjectId}")]
    private partial void LogCompilationCacheHit(ProjectId projectId);

    [LoggerMessage(EventId = 4262, Level = LogLevel.Debug,
        Message = "Compilation cache invalidated (project file modified): {ProjectPath}")]
    private partial void LogCompilationCacheInvalidated(string? projectPath);

    [LoggerMessage(EventId = 4263, Level = LogLevel.Warning,
        Message = "Project not found for ID: {ProjectId}")]
    private partial void LogProjectNotFoundById(ProjectId projectId);

    [LoggerMessage(EventId = 4264, Level = LogLevel.Trace,
        Message = "Requesting compilation for project: {ProjectName}")]
    private partial void LogRequestingCompilation(string projectName);

    [LoggerMessage(EventId = 4265, Level = LogLevel.Debug,
        Message = "Compilation evicted from cache. ProjectId: {ProjectId}, Reason: {Reason}")]
    private partial void LogCompilationEvicted(string projectId, EvictionReason reason);

    [LoggerMessage(EventId = 4266, Level = LogLevel.Trace,
        Message = "Cached compilation for project: {ProjectName} (LastWriteTime: {LastWriteTime}, Documents: {DocumentCount})")]
    private partial void LogCompilationCached(string projectName, DateTime lastWriteTime, int documentCount);

    [LoggerMessage(EventId = 4267, Level = LogLevel.Warning,
        Message = "Failed to get compilation for project: {ProjectName}")]
    private partial void LogCompilationFailed(string? projectName);

    // Cache invalidation (4280-4290)
    [LoggerMessage(EventId = 4280, Level = LogLevel.Debug,
        Message = "Invalidated semantic model cache for DocumentId: {DocumentId}")]
    private partial void LogSemanticModelInvalidated(DocumentId documentId);

    [LoggerMessage(EventId = 4281, Level = LogLevel.Debug,
        Message = "Invalidated compilation cache for ProjectId: {ProjectId}")]
    private partial void LogCompilationInvalidated(ProjectId projectId);

    [LoggerMessage(EventId = 4282, Level = LogLevel.Debug,
        Message = "Cascade invalidation: removed {Count} document semantic models")]
    private partial void LogCascadeInvalidation(int count);

    // FileSystemWatcher (4290-4310)
    [LoggerMessage(EventId = 4290, Level = LogLevel.Information,
        Message = "FileSystemWatcher initialized for directory: {Directory}")]
    private partial void LogFileWatcherInitialized(string directory);

    [LoggerMessage(EventId = 4291, Level = LogLevel.Warning,
        Message = "Failed to initialize FileSystemWatcher for {Directory}. Automatic cache invalidation disabled.")]
    private partial void LogFileWatcherInitFailed(Exception exception, string directory);

    [LoggerMessage(EventId = 4292, Level = LogLevel.Debug,
        Message = "File changed detected by FileSystemWatcher: {FilePath}")]
    private partial void LogFileChanged(string filePath);

    [LoggerMessage(EventId = 4293, Level = LogLevel.Debug,
        Message = "File deleted detected by FileSystemWatcher: {FilePath}")]
    private partial void LogFileDeleted(string filePath);

    [LoggerMessage(EventId = 4294, Level = LogLevel.Debug,
        Message = "File renamed detected by FileSystemWatcher: {OldPath} -> {NewPath}")]
    private partial void LogFileRenamed(string oldPath, string newPath);

    // Project file watcher and auto-reload (4310-4325)
    [LoggerMessage(EventId = 4310, Level = LogLevel.Information,
        Message = "Project file watcher initialized for auto-reload. Watching: {Extensions}, Debounce: {DebounceMs}ms")]
    private partial void LogProjectFileWatcherInitialized(string extensions, int debounceMs);

    [LoggerMessage(EventId = 4311, Level = LogLevel.Warning,
        Message = "Failed to initialize project file watcher for {Directory}. Auto-reload disabled.")]
    private partial void LogProjectFileWatcherInitFailed(Exception exception, string directory);

    [LoggerMessage(EventId = 4312, Level = LogLevel.Information,
        Message = "Project file change detected: {FilePath}. Scheduling reload with {DebounceMs}ms debounce.")]
    private partial void LogProjectFileChangeDetected(string filePath, int debounceMs);

    [LoggerMessage(EventId = 4313, Level = LogLevel.Warning,
        Message = "Cannot auto-reload: Solution path not stored")]
    private partial void LogCannotAutoReloadNoPath();

    [LoggerMessage(EventId = 4314, Level = LogLevel.Information,
        Message = "Auto-reloading solution: {SolutionPath}")]
    private partial void LogAutoReloading(string solutionPath);

    [LoggerMessage(EventId = 4315, Level = LogLevel.Information,
        Message = "Auto-reload completed successfully")]
    private partial void LogAutoReloadComplete();

    [LoggerMessage(EventId = 4316, Level = LogLevel.Error,
        Message = "Error during auto-reload")]
    private partial void LogAutoReloadError(Exception exception);

    // Progress reporting (4320)
    [LoggerMessage(EventId = 4320, Level = LogLevel.Trace,
        Message = "Project Load Progress: {ProjectDisplayName}, Operation: {Operation}, Time: {TimeElapsed}")]
    private partial void LogProjectLoadProgress(string projectDisplayName, string operation, TimeSpan timeElapsed);

    // NuGet assemblies (4330-4345)
    [LoggerMessage(EventId = 4330, Level = LogLevel.Warning,
        Message = "NuGet global packages folder not found or inaccessible: {NuGetCacheDir}")]
    private partial void LogNuGetFolderNotFound(string? nugetCacheDir);

    [LoggerMessage(EventId = 4331, Level = LogLevel.Trace,
        Message = "Package directory not found: {PackageDir}")]
    private partial void LogPackageDirectoryNotFound(string packageDir);

    [LoggerMessage(EventId = 4332, Level = LogLevel.Trace,
        Message = "No lib directory found for package {PackageId} {Version}")]
    private partial void LogNoLibDirectory(string packageId, string version);

    [LoggerMessage(EventId = 4333, Level = LogLevel.Information,
        Message = "Found {AssemblyCount} NuGet assemblies from global packages cache (parallel mode)")]
    private partial void LogNuGetAssembliesFound(int assemblyCount);

    [LoggerMessage(EventId = 4334, Level = LogLevel.Trace,
        Message = "Found {AssemblyCount} assemblies in exact framework match {Framework} for {PackageId} {Version}")]
    private partial void LogExactFrameworkMatch(int assemblyCount, string framework, string packageId, string version);

    [LoggerMessage(EventId = 4335, Level = LogLevel.Trace,
        Message = "Found {AssemblyCount} assemblies in compatible framework {Framework} for {PackageId} {Version}")]
    private partial void LogCompatibleFrameworkMatch(int assemblyCount, string framework, string packageId, string version);

    [LoggerMessage(EventId = 4336, Level = LogLevel.Trace,
        Message = "Found {AssemblyCount} assemblies in lib root for {PackageId} {Version}")]
    private partial void LogLibRootAssemblies(int assemblyCount, string packageId, string version);

    // Workspace diagnostics (4340-4341)
    [LoggerMessage(EventId = 4340, Level = LogLevel.Error,
        Message = "Workspace diagnostic ({Kind}): {Message}")]
    private partial void LogWorkspaceDiagnosticError(WorkspaceDiagnosticKind kind, string message);

    [LoggerMessage(EventId = 4341, Level = LogLevel.Warning,
        Message = "Workspace diagnostic ({Kind}): {Message}")]
    private partial void LogWorkspaceDiagnosticWarning(WorkspaceDiagnosticKind kind, string message);

    // SLNX support (4350-4355)
    [LoggerMessage(EventId = 4350, Level = LogLevel.Information,
        Message = "Loading .slnx file: {SlnxPath}")]
    private partial void LogLoadingSlnxFile(string slnxPath);

    [LoggerMessage(EventId = 4351, Level = LogLevel.Warning,
        Message = "Project not found in .slnx: {ProjectPath}")]
    private partial void LogSlnxProjectNotFound(string projectPath);

    [LoggerMessage(EventId = 4352, Level = LogLevel.Warning,
        Message = "Failed to load project from .slnx: {ProjectPath} - {ErrorMessage}")]
    private partial void LogSlnxProjectLoadFailed(string projectPath, string errorMessage);

    [LoggerMessage(EventId = 4353, Level = LogLevel.Information,
        Message = ".slnx load complete: {LoadedCount} projects loaded, {SkippedCount} skipped")]
    private partial void LogSlnxLoadComplete(int loadedCount, int skippedCount);

    [LoggerMessage(EventId = 4354, Level = LogLevel.Debug,
        Message = "Skipping unsupported language project: {ProjectPath}")]
    private partial void LogSlnxProjectSkippedUnsupportedLanguage(string projectPath);

    [LoggerMessage(EventId = 4355, Level = LogLevel.Information,
        Message = ".slnx: {ProjectCount} C# projects to load")]
    private partial void LogSlnxProjectsToLoad(int projectCount);
}

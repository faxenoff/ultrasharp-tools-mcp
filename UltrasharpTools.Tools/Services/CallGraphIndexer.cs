using System.Collections.Concurrent;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Background service for pre-indexing call graph to populate cache.
/// Runs after LoadProject to make subsequent view_definition calls instant.
/// </summary>
public partial class CallGraphIndexer : IDisposable {
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger _logger;
    private CancellationTokenSource? _indexingCts;
    private Task? _indexingTask;
    private readonly SemaphoreSlim _indexingLock = new(1, 1);

    // Progress tracking
    private int _totalMethods;
    private int _indexedMethods;
    private DateTime _indexingStartTime;

    public bool IsIndexing { get; private set; }
    public double ProgressPercentage => _totalMethods > 0 ? (_indexedMethods * 100.0 / _totalMethods) : 0;
    public TimeSpan ElapsedTime => IsIndexing ? DateTime.UtcNow - _indexingStartTime : TimeSpan.Zero;

    public CallGraphIndexer(
        ICodeAnalysisService codeAnalysisService,
        ISolutionManager solutionManager,
        ILogger<CallGraphIndexer> logger) {
        _codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start background indexing of call graph for all methods in the solution.
    /// This populates the cache so subsequent view_definition calls are instant.
    /// </summary>
    public Task StartBackgroundIndexingAsync() {
        return StartBackgroundIndexingAsync(CancellationToken.None);
    }

    /// <summary>
    /// Start background indexing of call graph for all methods in the solution.
    /// This populates the cache so subsequent view_definition calls are instant.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the indexing operation.</param>
    public async Task StartBackgroundIndexingAsync(CancellationToken cancellationToken) {
        await _indexingLock.WaitAsync(cancellationToken);
        try {
            if (IsIndexing) {
                LogIndexingInProgress();
                return;
            }

            if (!_solutionManager.IsSolutionLoaded) {
                LogNoSolutionForIndexing();
                return;
            }

            // Cancel any previous indexing
            _indexingCts?.Cancel();
            _indexingCts?.Dispose();
            _indexingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            IsIndexing = true;
            _indexingStartTime = DateTime.UtcNow;
            _indexedMethods = 0;

            LogStartingIndexing();

            // Start indexing task in background
            _indexingTask = Task.Run(() => IndexCallGraphAsync(_indexingCts.Token), _indexingCts.Token);
        } finally {
            _indexingLock.Release();
        }
    }

    /// <summary>
    /// Stop background indexing if it's running.
    /// </summary>
    public async Task StopIndexingAsync() {
        await _indexingLock.WaitAsync();
        try {
            if (!IsIndexing) {
                return;
            }

            LogStoppingIndexing();
            _indexingCts?.Cancel();

            if (_indexingTask != null) {
                try {
                    await _indexingTask;
                } catch (OperationCanceledException) {
                    // Expected
                }
            }

            IsIndexing = false;
            LogIndexingStopped();
        } finally {
            _indexingLock.Release();
        }
    }
    private async Task IndexCallGraphAsync(CancellationToken cancellationToken) {
        try {
            var solution = _solutionManager.CurrentSolution;
            if (solution == null) {
                LogSolutionNotLoadedForIndex();
                return;
            }

            // Collect all methods from all projects in parallel
            var allMethods = new ConcurrentBag<IMethodSymbol>();

            LogCollectingMethods();

            // Parallel collection from projects
            var projects = solution.Projects.ToList();
            var collectOptions = new ParallelOptions {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
                CancellationToken = cancellationToken
            };

            var skippedProjects = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(projects, collectOptions, async (project, ct) => {
                ct.ThrowIfCancellationRequested();

                // Check for unresolved analyzer references that cause hanging during FindReferences
                // UnresolvedAnalyzerReference is internal, so check by type name
                var hasUnresolvedAnalyzers = project.AnalyzerReferences
                    .Any(ar => ar.GetType().Name == "UnresolvedAnalyzerReference");

                if (hasUnresolvedAnalyzers) {
                    skippedProjects.Add(project.Name);
                    LogSkippingProjectWithAnalyzerIssues(project.Name, 1);
                    return;
                }

                var compilation = await project.GetCompilationAsync(ct);
                if (compilation == null) {
                    LogCompilationFailed(project.Name);
                    return;
                }

                // Visit all types and collect methods
                var visitor = new MethodCollectorVisitor();
                visitor.Visit(compilation.Assembly.GlobalNamespace);

                foreach (var method in visitor.Methods) {
                    allMethods.Add(method);
                }
            });

            if (skippedProjects.Count > 0) {
                LogProjectsSkippedDueToAnalyzerIssues(skippedProjects.Count, string.Join(", ", skippedProjects.Take(5)));
            }

            _totalMethods = allMethods.Count;
            LogMethodsFound(_totalMethods);

            // Index methods in parallel with throttling
            var parallelOptions = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(allMethods, parallelOptions, async (method, ct) => {
                try {
                    // Use timeout to prevent hanging on problematic methods
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                    // Index callers
                    await _codeAnalysisService.FindCallersAsync(method, timeoutCts.Token);

                    // Index outgoing calls
                    await _codeAnalysisService.FindOutgoingCallsAsync(method, timeoutCts.Token);

                    var indexed = Interlocked.Increment(ref _indexedMethods);

                    // Log progress every 100 methods
                    if (indexed % 100 == 0) {
                        LogIndexingProgress(indexed, _totalMethods, ProgressPercentage);
                    }
                } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                    throw; // Re-throw only if main token was cancelled
                } catch (OperationCanceledException) {
                    // Timeout - skip this method
                    LogMethodIndexTimeout(method.ToDisplayString());
                    Interlocked.Increment(ref _indexedMethods);
                } catch (Exception ex) {
                    LogMethodIndexError(ex, method.ToDisplayString());
                    Interlocked.Increment(ref _indexedMethods);
                }
            });

            var elapsed = DateTime.UtcNow - _indexingStartTime;
            LogIndexingCompleted(_totalMethods, elapsed.TotalSeconds);
        } catch (OperationCanceledException) {
            LogIndexingCancelled();
        } catch (Exception ex) {
            LogIndexingError(ex);
        } finally {
            IsIndexing = false;
        }
    }
    /// <summary>
    /// Visitor to collect all methods from a namespace.
    /// </summary>
    private sealed class MethodCollectorVisitor : SymbolVisitor {
        public List<IMethodSymbol> Methods { get; } = new();

        public override void VisitNamespace(INamespaceSymbol symbol) {
            foreach (var member in symbol.GetMembers()) {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol) {
            // Skip compiler-generated and special types
            if (symbol.IsImplicitlyDeclared || symbol.TypeKind == TypeKind.Error) {
                return;
            }

            foreach (var member in symbol.GetMembers()) {
                member.Accept(this);
            }
        }

        public override void VisitMethod(IMethodSymbol symbol) {
            // Skip compiler-generated methods, property accessors, etc.
            if (symbol.IsImplicitlyDeclared ||
                symbol.MethodKind == MethodKind.PropertyGet ||
                symbol.MethodKind == MethodKind.PropertySet ||
                symbol.MethodKind == MethodKind.EventAdd ||
                symbol.MethodKind == MethodKind.EventRemove) {
                return;
            }

            // Only index methods with source code
            if (symbol.DeclaringSyntaxReferences.Length > 0) {
                Methods.Add(symbol);
            }
        }
    }

    public void Dispose() {
        _indexingCts?.Cancel();
        _indexingCts?.Dispose();
        _indexingLock?.Dispose();
    }
}

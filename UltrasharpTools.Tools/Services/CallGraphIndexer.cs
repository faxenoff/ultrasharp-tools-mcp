using System.Collections.Concurrent;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Background service for pre-indexing call graph to populate cache.
/// Runs after LoadProject to make subsequent view_definition calls instant.
/// </summary>
public class CallGraphIndexer : IDisposable
{
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
        ILogger<CallGraphIndexer> logger)
    {
        _codeAnalysisService = codeAnalysisService ?? throw new ArgumentNullException(nameof(codeAnalysisService));
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start background indexing of call graph for all methods in the solution.
    /// This populates the cache so subsequent view_definition calls are instant.
    /// </summary>
    public async Task StartBackgroundIndexingAsync(CancellationToken cancellationToken = default)
    {
        await _indexingLock.WaitAsync(cancellationToken);
        try
        {
            if (IsIndexing)
            {
                _logger.LogInformation("Call graph indexing is already in progress");
                return;
            }

            if (!_solutionManager.IsSolutionLoaded)
            {
                _logger.LogWarning("Cannot start call graph indexing: no solution loaded");
                return;
            }

            // Cancel any previous indexing
            _indexingCts?.Cancel();
            _indexingCts?.Dispose();
            _indexingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            IsIndexing = true;
            _indexingStartTime = DateTime.UtcNow;
            _indexedMethods = 0;

            _logger.LogInformation("Starting background call graph indexing...");

            // Start indexing task in background
            _indexingTask = Task.Run(() => IndexCallGraphAsync(_indexingCts.Token), _indexingCts.Token);
        }
        finally
        {
            _indexingLock.Release();
        }
    }

    /// <summary>
    /// Stop background indexing if it's running.
    /// </summary>
    public async Task StopIndexingAsync()
    {
        await _indexingLock.WaitAsync();
        try
        {
            if (!IsIndexing)
            {
                return;
            }

            _logger.LogInformation("Stopping call graph indexing...");
            _indexingCts?.Cancel();

            if (_indexingTask != null)
            {
                try
                {
                    await _indexingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            IsIndexing = false;
            _logger.LogInformation("Call graph indexing stopped");
        }
        finally
        {
            _indexingLock.Release();
        }
    }

    private async Task IndexCallGraphAsync(CancellationToken cancellationToken)
    {
        try
        {
            var solution = _solutionManager.CurrentSolution;
            if (solution == null)
            {
                _logger.LogWarning("Cannot index call graph: solution is not loaded");
                return;
            }

            // Collect all methods from all projects
            var allMethods = new ConcurrentBag<IMethodSymbol>();

            _logger.LogInformation("Collecting methods from solution...");

            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                {
                    _logger.LogWarning("Failed to get compilation for project {ProjectName}", project.Name);
                    continue;
                }

                // Visit all types and collect methods
                var visitor = new MethodCollectorVisitor();
                visitor.Visit(compilation.Assembly.GlobalNamespace);

                foreach (var method in visitor.Methods)
                {
                    allMethods.Add(method);
                }
            }

            _totalMethods = allMethods.Count;
            _logger.LogInformation("Found {MethodCount} methods to index", _totalMethods);

            // Index methods in parallel with throttling
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(allMethods, parallelOptions, async (method, ct) =>
            {
                try
                {
                    // Index callers
                    await _codeAnalysisService.FindCallersAsync(method, ct);

                    // Index outgoing calls
                    await _codeAnalysisService.FindOutgoingCallsAsync(method, ct);

                    var indexed = Interlocked.Increment(ref _indexedMethods);

                    // Log progress every 100 methods
                    if (indexed % 100 == 0)
                    {
                        _logger.LogInformation(
                            "Call graph indexing progress: {Indexed}/{Total} methods ({Percentage:F1}%)",
                            indexed,
                            _totalMethods,
                            ProgressPercentage
                        );
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogWarning(
                        ex,
                        "Error indexing method {MethodName}",
                        method.ToDisplayString()
                    );
                }
            });

            var elapsed = DateTime.UtcNow - _indexingStartTime;
            _logger.LogInformation(
                "Call graph indexing completed: {MethodCount} methods in {Elapsed:F1} seconds",
                _totalMethods,
                elapsed.TotalSeconds
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Call graph indexing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during call graph indexing");
        }
        finally
        {
            IsIndexing = false;
        }
    }

    /// <summary>
    /// Visitor to collect all methods from a namespace.
    /// </summary>
    private class MethodCollectorVisitor : SymbolVisitor
    {
        public List<IMethodSymbol> Methods { get; } = new();

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            // Skip compiler-generated and special types
            if (symbol.IsImplicitlyDeclared || symbol.TypeKind == TypeKind.Error)
            {
                return;
            }

            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitMethod(IMethodSymbol symbol)
        {
            // Skip compiler-generated methods, property accessors, etc.
            if (symbol.IsImplicitlyDeclared ||
                symbol.MethodKind == MethodKind.PropertyGet ||
                symbol.MethodKind == MethodKind.PropertySet ||
                symbol.MethodKind == MethodKind.EventAdd ||
                symbol.MethodKind == MethodKind.EventRemove)
            {
                return;
            }

            // Only index methods with source code
            if (symbol.DeclaringSyntaxReferences.Length > 0)
            {
                Methods.Add(symbol);
            }
        }
    }

    public void Dispose()
    {
        _indexingCts?.Cancel();
        _indexingCts?.Dispose();
        _indexingLock?.Dispose();
    }
}

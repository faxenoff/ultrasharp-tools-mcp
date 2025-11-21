

using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Service for backtracing from a crash/failure point to potential entry points.
/// </summary>
public sealed class BacktraceService(
ISolutionManager solutionManager,
ICallGraphCacheService callGraphCache,
IPdbSymbolResolver pdbResolver
) : IBacktraceService
{
    private readonly ISolutionManager _solutionManager = solutionManager;
    private readonly ICallGraphCacheService _callGraphCache = callGraphCache;
    private readonly IPdbSymbolResolver _pdbResolver = pdbResolver;

    private sealed class BacktraceContext
    {
        public string? StartPointFqn { get; set; }
        public List<string>? StackTraceHints { get; set; }
        public int MaxDepth { get; set; }
        public int MaxPaths { get; set; }
        public bool IncludeExternalCallers { get; set; }
        public int MaxDepthReached { get; set; }
        public bool StartPointReached { get; set; }
        public List<CallPath> CompletedPaths { get; } = new();
    }

    public async Task<BacktraceResult> BacktraceFromCrashAsync(
    string crashPointFqn,
    string? startPointFqn = null,
    List<string>? stackTraceHints = null,
    int maxDepth = 15,
    int maxPaths = 5,
    bool includeExternalCallers = false,
    CancellationToken cancellationToken = default
    )
    {
        var context = new BacktraceContext
        {
            StartPointFqn = startPointFqn,
            StackTraceHints = stackTraceHints,
            MaxDepth = maxDepth,
            MaxPaths = maxPaths,
            IncludeExternalCallers = includeExternalCallers
        };

        string? errorMessage = null;

        try
        {
            // Find crash point symbol
            var crashSymbol = await FindMethodSymbolAsync(crashPointFqn, cancellationToken);
            if (crashSymbol == null)
            {
                return new BacktraceResult
                {
                    CrashPointFqn = crashPointFqn,
                    StartPointFqn = startPointFqn,
                    StackTraceHints = stackTraceHints,
                    CallPaths = new List<CallPath>(),
                    MaxDepthReached = 0,
                    StartPointReached = false,
                    ErrorMessage = $"Crash point method not found: {crashPointFqn}"
                };
            }

            // Start backtracing from crash point
            var initialFrame = new CallFrame
            {
                FrameNumber = 0,
                MethodFqn = crashSymbol.ToDisplayString(),
                Description = $"CRASH: {crashSymbol.Name}",
                SourceLocation = GetSourceLocation(crashSymbol),
                Parameters = GetMethodParameters(crashSymbol),
                CallSite = null,
                MatchesStackTrace = MatchesStackTrace(crashSymbol, stackTraceHints),
                StackTraceConfidence = FuzzyStackTraceMatcher.MatchConfidence(crashSymbol, stackTraceHints)
            };

            // Build call paths backwards
            await BuildCallPathsAsync(
            crashSymbol,
            new List<CallFrame> { initialFrame },
            context,
            currentDepth: 0,
            cancellationToken
            );

            // Rank paths by confidence
            RankPathsByConfidence(context.CompletedPaths, stackTraceHints);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error during backtrace: {ex.Message}";
        }

        return new BacktraceResult
        {
            CrashPointFqn = crashPointFqn,
            StartPointFqn = startPointFqn,
            StackTraceHints = stackTraceHints,
            CallPaths = context.CompletedPaths,
            MaxDepthReached = context.MaxDepthReached,
            StartPointReached = context.StartPointReached,
            ErrorMessage = errorMessage
        };
    }

    private async Task BuildCallPathsAsync(
    IMethodSymbol currentMethod,
    List<CallFrame> currentPath,
    BacktraceContext context,
    int currentDepth,
    CancellationToken cancellationToken
    )
    {
        // Check limits
        if (currentDepth >= context.MaxDepth || context.CompletedPaths.Count >= context.MaxPaths)
        {
            // Save current path as completed
            SaveCompletedPath(currentPath, context, currentDepth, reachedEntryPoint: false);
            return;
        }

        context.MaxDepthReached = Math.Max(context.MaxDepthReached, currentDepth);

        // Check if we reached the start point
        var currentFqn = currentMethod.ToDisplayString();
        if (context.StartPointFqn != null &&
        string.Equals(currentFqn, context.StartPointFqn, StringComparison.OrdinalIgnoreCase))
        {
            context.StartPointReached = true;
            SaveCompletedPath(currentPath, context, currentDepth, reachedEntryPoint: true);
            return;
        }

        // Find all callers of this method
        var callers = await FindCallersAsync(currentMethod, cancellationToken);

        // Filter external callers if needed
        if (!context.IncludeExternalCallers)
        {
            callers = callers.Where(c => IsInSolution(c.CallingSymbol)).ToList();
        }

        // If no callers found, this is an entry point
        if (callers.Count == 0)
        {
            SaveCompletedPath(currentPath, context, currentDepth, reachedEntryPoint: true);
            return;
        }

        // For each caller, create a new path branch
        foreach (var caller in callers)
        {
            if (context.CompletedPaths.Count >= context.MaxPaths)
            {
                break;
            }

            var callingMethod = caller.CallingSymbol as IMethodSymbol;
            if (callingMethod == null)
            {
                continue;
            }

            // Create new frame for this caller
            var newFrame = new CallFrame
            {
                FrameNumber = currentPath.Count,
                MethodFqn = callingMethod.ToDisplayString(),
                Description = $"CALLED FROM: {callingMethod.Name}",
                SourceLocation = GetSourceLocation(callingMethod),
                Parameters = GetMethodParameters(callingMethod),
                CallSite = new CallSiteInfo
                {
                    CallingMethodFqn = callingMethod.ToDisplayString(),
                    SourceLocation = GetCallSiteLocation(caller),
                    CallExpression = GetCallExpression(caller)
                },
                MatchesStackTrace = MatchesStackTrace(callingMethod, context.StackTraceHints),
                StackTraceConfidence = FuzzyStackTraceMatcher.MatchConfidence(callingMethod, context.StackTraceHints)
            };

            // Create new path with this frame added
            var newPath = new List<CallFrame>(currentPath) { newFrame };

            // Recursively trace this caller
            await BuildCallPathsAsync(
            callingMethod,
            newPath,
            context,
            currentDepth + 1,
            cancellationToken
            );
        }
    }

    private void SaveCompletedPath(
    List<CallFrame> frames,
    BacktraceContext context,
    int depth,
    bool reachedEntryPoint
    )
    {
        if (context.CompletedPaths.Count >= context.MaxPaths)
        {
            return;
        }

        // Reverse frames so they go from entry point to crash point
        var reversedFrames = new List<CallFrame>(frames);
        reversedFrames.Reverse();

        // Renumber frames
        for (int i = 0; i < reversedFrames.Count; i++)
        {
            reversedFrames[i] = reversedFrames[i] with { FrameNumber = i };
        }

        context.CompletedPaths.Add(new CallPath
        {
            PathId = context.CompletedPaths.Count + 1,
            Frames = reversedFrames,
            ReachedEntryPoint = reachedEntryPoint,
            Confidence = 0.5 // Will be calculated later
        });
    }

    private async Task<List<CachedCallerInfo>> FindCallersAsync(
    IMethodSymbol method,
    CancellationToken cancellationToken
    )
    {
        if (!_solutionManager.IsSolutionLoaded || _solutionManager.CurrentSolution == null)
        {
            return new List<CachedCallerInfo>();
        }

        var methodFqn = method.ToDisplayString();
        var solutionHash = ComputeSolutionHash();

        // Try FULL cache first (includes Location data) - 5-10x faster
        var cachedCallersFull = await _callGraphCache.GetCallersFullAsync(methodFqn, solutionHash, cancellationToken);
        if (cachedCallersFull != null)
        {
            // Cache HIT - convert SerializableCallerInfo to CachedCallerInfo
            var resolvedCallers = new List<CachedCallerInfo>();

            foreach (var serializable in cachedCallersFull)
            {
                // Resolve calling symbol from FQN
                var callingSymbol = await _solutionManager.FindRoslynSymbolAsync(
                serializable.CallingSymbolFqn,
                cancellationToken
                );

                if (callingSymbol != null)
                {
                    resolvedCallers.Add(new CachedCallerInfo(serializable, callingSymbol));
                }
            }

            return resolvedCallers;
        }

        // Cache MISS - call expensive SymbolFinder
        var callers = await SymbolFinder.FindCallersAsync(
        method,
        _solutionManager.CurrentSolution,
        cancellationToken
        );

        var callersList = callers.ToList();

        // Store FULL caller info in cache (including Locations)
        var serializableCallers = callersList
        .Select(CallerInfoConverter.ToSerializable)
        .ToList();
        await _callGraphCache.SetCallersFullAsync(methodFqn, serializableCallers, solutionHash, cancellationToken);

        // Wrap SymbolCallerInfo in CachedCallerInfo for unified interface
        return callersList.Select(c => new CachedCallerInfo(c)).ToList();
    }

    private string ComputeSolutionHash()
    {
        if (_solutionManager.CurrentSolution == null)
        {
            return string.Empty;
        }

        // Use solution version as hash (lightweight)
        // In production, could use file hashes for better accuracy
        return _solutionManager.CurrentSolution.Version.ToString();
    }

    private bool IsInSolution(ISymbol symbol)
    {
        if (!_solutionManager.IsSolutionLoaded || _solutionManager.CurrentSolution == null)
        {
            return false;
        }

        // Check if symbol is declared in any project of the solution
        var syntaxRefs = symbol.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
        {
            return false;
        }

        var syntaxTree = syntaxRefs[0].SyntaxTree;
        return _solutionManager.CurrentSolution.Projects
        .Any(p => p.Documents.Any(d => d.FilePath == syntaxTree.FilePath));
    }

    private void RankPathsByConfidence(List<CallPath> paths, List<string>? stackTraceHints)
    {
        if (stackTraceHints == null || stackTraceHints.Count == 0)
        {
            // No stack trace hints, use simple heuristics
            foreach (var path in paths)
            {
                // Prefer shorter paths (more direct)
                var depthScore = 1.0 / (1.0 + path.Depth * 0.1);

                // Prefer paths that reach entry point
                var entryPointScore = path.ReachedEntryPoint ? 1.0 : 0.7;

                path.GetType().GetProperty(nameof(CallPath.Confidence))!
                .SetValue(path, depthScore * entryPointScore);
            }
        }
        else
        {
            // Use fuzzy stack trace matching for confidence
            foreach (var path in paths)
            {
                // Average fuzzy confidence across all frames
                var avgConfidence = path.Frames.Count > 0
                ? path.Frames.Average(f => f.StackTraceConfidence)
                : 0.0;

                // Bonus for frames with high confidence (>0.8)
                var highConfidenceFrames = path.Frames.Count(f => f.StackTraceConfidence > 0.8);
                var highConfidenceBonus = highConfidenceFrames > 0
                ? 0.1 * Math.Min(1.0, highConfidenceFrames / 3.0)
                : 0.0;

                // Entry point bonus
                var entryPointScore = path.ReachedEntryPoint ? 1.0 : 0.85;

                // Depth penalty (prefer shorter paths)
                var depthPenalty = 1.0 / (1.0 + path.Depth * 0.05);

                var finalConfidence = (avgConfidence + highConfidenceBonus) * entryPointScore * depthPenalty;

                path.GetType().GetProperty(nameof(CallPath.Confidence))!
                .SetValue(path, Math.Clamp(finalConfidence, 0.0, 1.0));
            }
        }

        // Sort by confidence descending
        paths.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
    }

    private bool MatchesStackTrace(IMethodSymbol method, List<string>? stackTraceHints)
    {
        if (stackTraceHints == null || stackTraceHints.Count == 0)
        {
            return false;
        }

        var methodName = method.Name;
        var typeName = method.ContainingType?.Name;
        var namespaceName = method.ContainingNamespace?.ToDisplayString();

        return stackTraceHints.Any(hint =>
        hint.Contains(methodName, StringComparison.OrdinalIgnoreCase) ||
        (typeName != null && hint.Contains(typeName, StringComparison.OrdinalIgnoreCase)) ||
        (namespaceName != null && hint.Contains(namespaceName, StringComparison.OrdinalIgnoreCase))
        );
    }

    private List<VariableInfo> GetMethodParameters(IMethodSymbol method)
    {
        return method.Parameters.Select(
        p =>
        new VariableInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(),
            Operation = "parameter",
            Scope = "parameter"
        }
        ).ToList();
    }

    private string? GetSourceLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        if (location == null || !location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return $"{Path.GetFileName(lineSpan.Path)}:{lineSpan.StartLinePosition.Line + 1}";
    }

    private string? GetCallSiteLocation(CachedCallerInfo caller)
    {
        var location = caller.Locations.FirstOrDefault();
        if (location == null || !location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return $"{Path.GetFileName(lineSpan.Path)}:{lineSpan.StartLinePosition.Line + 1}";
    }

    private string? GetCallExpression(CachedCallerInfo caller)
    {
        // Try to get the actual call syntax
        var location = caller.Locations.FirstOrDefault();
        if (location?.SourceTree != null)
        {
            var root = location.SourceTree.GetRoot();
            var node = root.FindNode(location.SourceSpan);
            return node?.ToString().Trim();
        }

        return null;
    }

    private async Task<IMethodSymbol?> FindMethodSymbolAsync(
    string fqn,
    CancellationToken cancellationToken
    )
    {
        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, cancellationToken);
        return symbol as IMethodSymbol;
    }
}

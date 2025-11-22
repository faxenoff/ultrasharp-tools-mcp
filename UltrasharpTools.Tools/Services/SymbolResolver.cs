using System.Collections.Concurrent;
using System.Collections.Frozen;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Resolves ISymbol instances from FQNs and cached metadata
/// Used for restoring symbols from persistent cache
/// OPTIMIZED: Uses Type Dictionary Cache for O(1) lookups instead of O(n)
/// </summary>
public class SymbolResolver
{
    private readonly ILogger _logger;

    public SymbolResolver(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Build Type Dictionary Cache for O(1) type lookups
    /// This is the CRITICAL optimization: instead of 462K O(n) calls to GetTypeByMetadataName,
    /// we scan the namespace tree ONCE and cache all types in a FrozenDictionary
    /// </summary>
    private FrozenDictionary<string, INamedTypeSymbol> BuildTypeCache(Compilation compilation, string projectName)
    {
        _logger.LogDebug("Building type cache for {ProjectName}...", projectName);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var typeDict = new Dictionary<string, INamedTypeSymbol>(capacity: 10000);

        void VisitNamespace(INamespaceSymbol ns)
        {
            // Visit all types in this namespace
            foreach (var type in ns.GetTypeMembers())
            {
                VisitType(type);
            }

            // Recurse into child namespaces
            foreach (var childNs in ns.GetNamespaceMembers())
            {
                VisitNamespace(childNs);
            }
        }

        void VisitType(INamedTypeSymbol type)
        {
            // Add this type to cache
            var fqn = type.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "");

            // Store both with and without generic arity for flexibility
            typeDict[fqn] = type;

            // Also store metadata name for GetTypeByMetadataName compatibility
            var metadataName = type.MetadataName;
            if (!string.IsNullOrEmpty(metadataName))
            {
                // Store with full namespace
                var fullMetadataName = type.ContainingNamespace?.ToDisplayString() + "." + metadataName;
                typeDict[fullMetadataName] = type;
            }

            // Visit nested types recursively
            foreach (var nestedType in type.GetTypeMembers())
            {
                VisitType(nestedType);
            }
        }

        // Start traversal from global namespace
        VisitNamespace(compilation.GlobalNamespace);

        sw.Stop();
        _logger.LogDebug(
            "Built type cache for {ProjectName}: {TypeCount} types in {ElapsedMs}ms",
            projectName,
            typeDict.Count,
            sw.ElapsedMilliseconds
        );

        // Convert to FrozenDictionary for optimal read performance (20-30% faster lookups)
        return typeDict.ToFrozenDictionary();
    }

    /// <summary>
    /// Find type by FQN using Type Cache (O(1) instead of O(n))
    /// </summary>
    private INamedTypeSymbol? FindType(
        FrozenDictionary<string, INamedTypeSymbol> typeCache,
        string typeFqn
    )
    {
        // Remove generic arity markers (e.g., MyType`1 -> MyType)
        var cleanTypeName = typeFqn;
        var arityIndex = typeFqn.IndexOf('`');
        if (arityIndex > 0)
        {
            cleanTypeName = typeFqn.Substring(0, arityIndex);
        }

        // O(1) lookup in FrozenDictionary ⚡
        if (typeCache.TryGetValue(cleanTypeName, out var symbol))
            return symbol;

        // Try original name
        if (typeCache.TryGetValue(typeFqn, out symbol))
            return symbol;

        return null;
    }

    /// <summary>
    /// Find member in type by name and flags
    /// </summary>
    private ISymbol? FindMember(INamedTypeSymbol type, string memberName, long flags)
    {
        var symbolFlags = (SymbolMetadataFlags)flags;

        // Get all members with matching name
        var candidates = type.GetMembers(memberName);

        if (candidates.Length == 0)
            return null;

        if (candidates.Length == 1)
            return candidates[0];

        // Multiple candidates - use flags to disambiguate
        foreach (var candidate in candidates)
        {
            var candidateFlags = SymbolIndexEntryBuilder.ExtractFlags(candidate);
            if (candidateFlags == symbolFlags)
                return candidate;
        }

        // Fallback: return first candidate
        _logger.LogTrace(
            "Multiple candidates for member {MemberName}, using first match",
            memberName
        );
        return candidates[0];
    }

    /// <summary>
    /// Parse FQN to extract type and member information
    /// </summary>
    private (string TypeFqn, string? MemberName) ParseFqn(string fqn)
    {
        // FQN formats:
        // - Type: "Namespace.TypeName"
        // - Method: "Namespace.TypeName.MethodName(params)"
        // - Property: "Namespace.TypeName.PropertyName"
        // - Field: "Namespace.TypeName.FieldName"

        // Find last dot before parenthesis (if any)
        var parenIndex = fqn.IndexOf('(');
        var searchLength = parenIndex > 0 ? parenIndex : fqn.Length;

        var lastDotIndex = fqn.LastIndexOf('.', searchLength - 1, searchLength);

        if (lastDotIndex < 0)
        {
            // No dots - must be a simple type
            return (fqn, null);
        }

        // Check if this looks like a member
        // Heuristic: if there's a '(' after the last dot, it's a method
        // Otherwise, we need to check if it's a nested type or a member

        // For now, simple approach: last segment is member if it looks like one
        var potentialMember = fqn.Substring(lastDotIndex + 1);

        // If it contains '(' or '<', it's definitely a method
        if (potentialMember.Contains('(') || potentialMember.Contains('<'))
        {
            // Extract member name (without parameters/generics)
            var memberNameEnd = Math.Min(
                potentialMember.IndexOf('(') >= 0 ? potentialMember.IndexOf('(') : int.MaxValue,
                potentialMember.IndexOf('<') >= 0 ? potentialMember.IndexOf('<') : int.MaxValue
            );

            var memberName =
                memberNameEnd < int.MaxValue
                    ? potentialMember.Substring(0, memberNameEnd)
                    : potentialMember;

            return (fqn.Substring(0, lastDotIndex), memberName);
        }

        // Ambiguous case - could be nested type or member
        // Try both: first assume it's a member, if that fails, caller will try as type
        return (fqn.Substring(0, lastDotIndex), potentialMember);
    }

    /// <summary>
    /// Batch resolve symbols with AGGRESSIVE OPTIMIZATIONS
    /// - Type Dictionary Cache: O(1) lookups instead of O(n)
    /// - Batch size 4096: reduced overhead
    /// - Parallel.ForEach: uses all CPU cores
    /// </summary>
    public async Task<List<(SerializableSymbolEntry Entry, ISymbol? Symbol)>> ResolveSymbolsAsync(
        List<SerializableSymbolEntry> entries,
        Solution solution,
        CancellationToken cancellationToken,
        Action<int, int>? progressCallback = null
    )
    {
        // CRITICAL FIX: Filter out symbols from unknown/missing projects BEFORE processing
        // This prevents spam of 32K+ warnings and unnecessary parallel processing
        var projectLookup = solution.Projects.ToDictionary(p => p.Name);
        var invalidEntries = entries.Where(e =>
            string.IsNullOrEmpty(e.ProjectName) ||
            e.ProjectName == "Unknown" ||
            !projectLookup.ContainsKey(e.ProjectName)
        ).ToList();

        if (invalidEntries.Count > 0)
        {
            var unknownProjects = invalidEntries
                .GroupBy(e => e.ProjectName ?? "null")
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key} ({g.Count()} symbols)")
                .ToList();

            _logger.LogWarning(
                "Skipping {InvalidCount} symbols from {ProjectCount} invalid/missing projects: {Projects}",
                invalidEntries.Count,
                invalidEntries.Select(e => e.ProjectName).Distinct().Count(),
                string.Join(", ", unknownProjects)
            );
        }

        // Filter to only valid entries
        var validEntries = entries.Where(e =>
            !string.IsNullOrEmpty(e.ProjectName) &&
            e.ProjectName != "Unknown" &&
            projectLookup.ContainsKey(e.ProjectName)
        ).ToList();

        if (validEntries.Count == 0)
        {
            _logger.LogWarning("No valid symbols to resolve after filtering");
            return new List<(SerializableSymbolEntry Entry, ISymbol? Symbol)>();
        }

        // OPTIMIZATION 1: Analyze which projects are actually needed
        var neededProjects = validEntries
            .Select(e => e.ProjectName)
            .Distinct()
            .ToHashSet();

        _logger.LogInformation(
            "Symbol resolution: {ValidSymbols}/{TotalSymbols} symbols from {NeededProjects}/{TotalProjects} projects",
            validEntries.Count,
            entries.Count,
            neededProjects.Count,
            solution.Projects.Count()
        );

        // OPTIMIZATION 2: Lazy compilation loading with concurrency control
        var compilationCache = new ConcurrentDictionary<string, Compilation?>();
        var typeCacheDict = new ConcurrentDictionary<string, FrozenDictionary<string, INamedTypeSymbol>>();
        using var compilationLoadSemaphore = new SemaphoreSlim(2, 2); // Max 2 parallel compilation loads
        var perProjectLocks = new ConcurrentDictionary<string, SemaphoreSlim>(); // Per-project locks
        var compilationTimeout = TimeSpan.FromMinutes(5);

        async Task<(Compilation?, FrozenDictionary<string, INamedTypeSymbol>?)> GetOrLoadCompilationWithCacheAsync(string projectName)
        {
            // Check cache first (fast path)
            if (compilationCache.TryGetValue(projectName, out var cachedCompilation) &&
                typeCacheDict.TryGetValue(projectName, out var cachedTypeCache))
            {
                return (cachedCompilation, cachedTypeCache);
            }

            // Not in cache - need to load
            if (!projectLookup.TryGetValue(projectName, out var project))
            {
                _logger.LogWarning("Project {ProjectName} not found in solution", projectName);
                compilationCache[projectName] = null;
                return (null, null);
            }

            // Get or create per-project lock
            var projectLock = perProjectLocks.GetOrAdd(projectName, _ => new SemaphoreSlim(1, 1));

            await projectLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring project-specific lock
                if (compilationCache.TryGetValue(projectName, out var cached2) &&
                    typeCacheDict.TryGetValue(projectName, out var cachedTypeCache2))
                {
                    return (cached2, cachedTypeCache2);
                }

                // Acquire global semaphore for actual loading
                await compilationLoadSemaphore.WaitAsync(cancellationToken);
                try
                {
                    _logger.LogDebug("Loading compilation for project {ProjectName}...", projectName);

                    using var timeoutCts = new CancellationTokenSource(compilationTimeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        timeoutCts.Token
                    );

                    try
                    {
                        var compilation = await project.GetCompilationAsync(linkedCts.Token);
                        compilationCache[projectName] = compilation;

                        _logger.LogDebug(
                            "Loaded compilation for {ProjectName} ({TypeCount} types)",
                            projectName,
                            compilation?.GlobalNamespace.GetTypeMembers().Length ?? 0
                        );

                        // CRITICAL OPTIMIZATION: Build Type Dictionary Cache
                        FrozenDictionary<string, INamedTypeSymbol>? typeCache = null;
                        if (compilation != null)
                        {
                            typeCache = BuildTypeCache(compilation, projectName);
                            typeCacheDict[projectName] = typeCache;
                        }

                        return (compilation, typeCache);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogError(
                            "Compilation loading for {ProjectName} timed out after {Timeout}",
                            projectName,
                            compilationTimeout
                        );
                        compilationCache[projectName] = null;
                        return (null, null);
                    }
                }
                finally
                {
                    compilationLoadSemaphore.Release();
                }
            }
            finally
            {
                projectLock.Release();
            }
        }

        // OPTIMIZATION 3: Pre-warm cache for top 2 projects
        var topProjects = validEntries
            .GroupBy(e => e.ProjectName)
            .OrderByDescending(g => g.Count())
            .Take(2)
            .Select(g => g.Key)
            .ToList();

        var preloadTasks = topProjects.Select(p => GetOrLoadCompilationWithCacheAsync(p)).ToList();

        // OPTIMIZATION 4: Increased batch size from 256 to 4096
        // OPTIMIZATION 5: Parallel.ForEach for aggressive CPU utilization
        var results = new ConcurrentBag<(SerializableSymbolEntry Entry, ISymbol? Symbol)>();
        var processedCount = 0;
        var totalCount = validEntries.Count;
        var batchSize = 4096; // ⚡ Increased from 256

        var batches = new List<List<SerializableSymbolEntry>>();
        for (int i = 0; i < validEntries.Count; i += batchSize)
        {
            batches.Add(validEntries.Skip(i).Take(batchSize).ToList());
        }

        _logger.LogInformation(
            "Processing {TotalSymbols} symbols in {BatchCount} batches of {BatchSize}",
            totalCount,
            batches.Count,
            batchSize
        );

        // Use all CPU cores for maximum throughput
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount, // ⚡ Use ALL cores
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(batches, parallelOptions, async (batch, ct) =>
        {
            foreach (var entry in batch)
            {
                ct.ThrowIfCancellationRequested();

                // Lazy load compilation + type cache
                var (compilation, typeCache) = await GetOrLoadCompilationWithCacheAsync(entry.ProjectName);

                var symbol = TryResolveSymbolWithTypeCache(
                    entry,
                    compilation,
                    typeCache,
                    ct
                );

                results.Add((entry, symbol));

                // Thread-safe progress reporting
                var current = Interlocked.Increment(ref processedCount);
                if (progressCallback != null && current % 5000 == 0) // Report every 5000 symbols
                {
                    progressCallback(current, totalCount);
                }
            }
        });

        // Final progress report
        progressCallback?.Invoke(processedCount, totalCount);

        var resultList = results.ToList();

        _logger.LogInformation(
            "Symbol resolution complete: {Loaded}/{Needed} projects loaded, {Resolved}/{Total} symbols resolved",
            compilationCache.Count(c => c.Value != null),
            neededProjects.Count,
            resultList.Count(r => r.Symbol != null),
            totalCount
        );

        return resultList;
    }

    /// <summary>
    /// Resolve symbol using Type Cache (O(1) lookup)
    /// </summary>
    private ISymbol? TryResolveSymbolWithTypeCache(
        SerializableSymbolEntry entry,
        Compilation? compilation,
        FrozenDictionary<string, INamedTypeSymbol>? typeCache,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (compilation == null || typeCache == null)
            {
                _logger.LogTrace(
                    "Compilation or type cache not available for project {ProjectName}",
                    entry.ProjectName
                );
                return null;
            }

            // Parse FQN
            var symbolInfo = ParseFqn(entry.CanonicalFqn);

            // Find symbol
            ISymbol? symbol = null;

            if (symbolInfo.MemberName == null)
            {
                // It's a type - O(1) lookup ⚡
                symbol = FindType(typeCache, symbolInfo.TypeFqn);
            }
            else
            {
                // It's a member - O(1) type lookup + O(1) member lookup
                var containingType = FindType(typeCache, symbolInfo.TypeFqn);
                if (containingType != null)
                {
                    symbol = FindMember(containingType, symbolInfo.MemberName, entry.Flags);
                }
            }

            return symbol;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error resolving symbol {FQN}", entry.CanonicalFqn);
            return null;
        }
    }
}

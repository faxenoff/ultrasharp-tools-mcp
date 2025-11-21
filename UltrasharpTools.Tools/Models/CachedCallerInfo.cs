namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Wrapper for caller information that can come from either cache or SymbolFinder
/// Provides unified interface for BacktraceService
/// </summary>
internal sealed class CachedCallerInfo
{
    private readonly SymbolCallerInfo? _roslynInfo;
    private readonly SerializableCallerInfo? _cachedInfo;
    private readonly ISymbol? _cachedSymbol;

    /// <summary>
    /// Create from Roslyn SymbolCallerInfo (cache MISS path)
    /// </summary>
    public CachedCallerInfo(SymbolCallerInfo roslynInfo)
    {
        _roslynInfo = roslynInfo;
        _cachedInfo = null;
        _cachedSymbol = null;
    }

    /// <summary>
    /// Create from cached SerializableCallerInfo (cache HIT path)
    /// </summary>
    public CachedCallerInfo(SerializableCallerInfo cachedInfo, ISymbol callingSymbol)
    {
        _roslynInfo = null;
        _cachedInfo = cachedInfo;
        _cachedSymbol = callingSymbol;
    }

    /// <summary>
    /// Get calling symbol (either from Roslyn or resolved from cache)
    /// </summary>
    public ISymbol CallingSymbol => _roslynInfo?.CallingSymbol ?? _cachedSymbol!;

    /// <summary>
    /// Get locations where this symbol is called
    /// </summary>
    public IEnumerable<Location> Locations
    {
        get
        {
            if (_roslynInfo != null)
            {
                return _roslynInfo.Value.Locations;
            }

            // Convert SerializableLocation to Location
            // For now, return empty - actual location reconstruction requires Solution context
            // which we'll handle separately in BacktraceService
            return Enumerable.Empty<Location>();
        }
    }

    /// <summary>
    /// Whether this is a direct call
    /// </summary>
    public bool IsDirect => _roslynInfo?.IsDirect ?? _cachedInfo?.IsDirect ?? true;

    /// <summary>
    /// Get serializable form (for caching)
    /// </summary>
    public SerializableCallerInfo ToSerializable()
    {
        if (_cachedInfo != null)
        {
            return _cachedInfo;
        }

        if (_roslynInfo != null)
        {
            return CallerInfoConverter.ToSerializable(_roslynInfo.Value);
        }

        throw new InvalidOperationException("CachedCallerInfo has no data");
    }

    /// <summary>
    /// Get cached source locations (if available from cache)
    /// </summary>
    public List<SerializableLocation>? GetCachedLocations()
    {
        return _cachedInfo?.CallSiteLocations;
    }
}

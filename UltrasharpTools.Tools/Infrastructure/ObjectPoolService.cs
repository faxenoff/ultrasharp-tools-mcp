using Microsoft.Extensions.ObjectPool;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// DI-friendly service для работы с ObjectPools
/// Provides scoped access к pools с automatic tracking и metrics
/// </summary>
public sealed class ObjectPoolService
{
    private readonly ILogger<ObjectPoolService> _logger;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly ObjectPool<List<ISymbol>> _symbolListPool;
    private readonly ObjectPool<List<string>> _stringListPool;

    // Metrics
    private long _stringBuilderGets;
    private long _stringBuilderReturns;
    private long _symbolListGets;
    private long _symbolListReturns;
    private long _stringListGets;
    private long _stringListReturns;

    public ObjectPoolService(ILogger<ObjectPoolService> logger)
    {
        _logger = logger;

        var provider = new DefaultObjectPoolProvider();

        _stringBuilderPool = provider.Create(
            new PooledStringBuilderPolicy(initialCapacity: 1024, maxCapacity: 16 * 1024)
        );

        _symbolListPool = provider.Create(
            new PooledSymbolListPolicy(initialCapacity: 128, maxCapacity: 2048)
        );

        _stringListPool = provider.Create(
            new PooledStringListPolicy(initialCapacity: 64, maxCapacity: 1024)
        );

        _logger.LogInformation("ObjectPoolService initialized with 3 pools");
    }

    /// <summary>
    /// Rent StringBuilder from pool
    /// </summary>
    public StringBuilder RentStringBuilder()
    {
        Interlocked.Increment(ref _stringBuilderGets);
        return _stringBuilderPool.Get();
    }

    /// <summary>
    /// Return StringBuilder to pool
    /// </summary>
    public void ReturnStringBuilder(StringBuilder sb)
    {
        Interlocked.Increment(ref _stringBuilderReturns);
        _stringBuilderPool.Return(sb);
    }

    /// <summary>
    /// Rent List&lt;ISymbol&gt; from pool
    /// </summary>
    public List<ISymbol> RentSymbolList()
    {
        Interlocked.Increment(ref _symbolListGets);
        return _symbolListPool.Get();
    }

    /// <summary>
    /// Return List&lt;ISymbol&gt; to pool
    /// </summary>
    public void ReturnSymbolList(List<ISymbol> list)
    {
        Interlocked.Increment(ref _symbolListReturns);
        _symbolListPool.Return(list);
    }

    /// <summary>
    /// Rent List&lt;string&gt; from pool
    /// </summary>
    public List<string> RentStringList()
    {
        Interlocked.Increment(ref _stringListGets);
        return _stringListPool.Get();
    }

    /// <summary>
    /// Return List&lt;string&gt; to pool
    /// </summary>
    public void ReturnStringList(List<string> list)
    {
        Interlocked.Increment(ref _stringListReturns);
        _stringListPool.Return(list);
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    public ObjectPoolStats GetStats()
    {
        return new ObjectPoolStats
        {
            StringBuilderGets = Interlocked.Read(ref _stringBuilderGets),
            StringBuilderReturns = Interlocked.Read(ref _stringBuilderReturns),
            SymbolListGets = Interlocked.Read(ref _symbolListGets),
            SymbolListReturns = Interlocked.Read(ref _symbolListReturns),
            StringListGets = Interlocked.Read(ref _stringListGets),
            StringListReturns = Interlocked.Read(ref _stringListReturns),
        };
    }

    /// <summary>
    /// Log pool statistics
    /// </summary>
    public void LogStats()
    {
        var stats = GetStats();
        _logger.LogInformation(
            "ObjectPool Stats: StringBuilder({SbGets}/{SbRets}), SymbolList({SlGets}/{SlRets}), StringList({StlGets}/{StlRets})",
            stats.StringBuilderGets,
            stats.StringBuilderReturns,
            stats.SymbolListGets,
            stats.SymbolListReturns,
            stats.StringListGets,
            stats.StringListReturns
        );
    }
}

/// <summary>
/// ObjectPool statistics
/// </summary>
public sealed class ObjectPoolStats
{
    public long StringBuilderGets { get; init; }
    public long StringBuilderReturns { get; init; }
    public long SymbolListGets { get; init; }
    public long SymbolListReturns { get; init; }
    public long StringListGets { get; init; }
    public long StringListReturns { get; init; }

    public double StringBuilderReturnRate =>
        StringBuilderGets > 0 ? (double)StringBuilderReturns / StringBuilderGets * 100.0 : 0.0;

    public double SymbolListReturnRate =>
        SymbolListGets > 0 ? (double)SymbolListReturns / SymbolListGets * 100.0 : 0.0;

    public double StringListReturnRate =>
        StringListGets > 0 ? (double)StringListReturns / StringListGets * 100.0 : 0.0;
}

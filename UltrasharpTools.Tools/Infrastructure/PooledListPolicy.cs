using Microsoft.Extensions.ObjectPool;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// ObjectPool policy для List&lt;ISymbol&gt;
/// Используется в горячих путях symbol search results
/// </summary>
public sealed class PooledSymbolListPolicy : IPooledObjectPolicy<List<ISymbol>>
{
    private readonly int _initialCapacity;
    private readonly int _maxCapacity;

    public PooledSymbolListPolicy(int initialCapacity = 128, int maxCapacity = 2048)
    {
        _initialCapacity = initialCapacity;
        _maxCapacity = maxCapacity;
    }

    public List<ISymbol> Create()
    {
        return new List<ISymbol>(_initialCapacity);
    }

    public bool Return(List<ISymbol> obj)
    {
        // Не возвращаем в pool слишком большие списки
        if (obj.Capacity > _maxCapacity)
        {
            return false;
        }

        // Очищаем перед возвратом в pool
        obj.Clear();
        return true;
    }
}

/// <summary>
/// ObjectPool policy для List&lt;string&gt;
/// Используется в горячих путях для FQN lists, path lists и т.д.
/// </summary>
public sealed class PooledStringListPolicy : IPooledObjectPolicy<List<string>>
{
    private readonly int _initialCapacity;
    private readonly int _maxCapacity;

    public PooledStringListPolicy(int initialCapacity = 64, int maxCapacity = 1024)
    {
        _initialCapacity = initialCapacity;
        _maxCapacity = maxCapacity;
    }

    public List<string> Create()
    {
        return new List<string>(_initialCapacity);
    }

    public bool Return(List<string> obj)
    {
        // Не возвращаем в pool слишком большие списки
        if (obj.Capacity > _maxCapacity)
        {
            return false;
        }

        // Очищаем перед возвратом в pool
        obj.Clear();
        return true;
    }
}

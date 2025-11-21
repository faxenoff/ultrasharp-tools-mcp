using Microsoft.Extensions.ObjectPool;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// ObjectPool policy для StringBuilder
/// Используется в горячих путях code generation и formatting
/// </summary>
public sealed class PooledStringBuilderPolicy : IPooledObjectPolicy<StringBuilder>
{
    private readonly int _initialCapacity;
    private readonly int _maxCapacity;

    public PooledStringBuilderPolicy(int initialCapacity = 1024, int maxCapacity = 16 * 1024)
    {
        _initialCapacity = initialCapacity;
        _maxCapacity = maxCapacity;
    }

    public StringBuilder Create()
    {
        return new StringBuilder(_initialCapacity);
    }

    public bool Return(StringBuilder obj)
    {
        // Не возвращаем в pool слишком большие StringBuilder
        if (obj.Capacity > _maxCapacity)
        {
            return false;
        }

        // Очищаем перед возвратом в pool
        obj.Clear();
        return true;
    }
}

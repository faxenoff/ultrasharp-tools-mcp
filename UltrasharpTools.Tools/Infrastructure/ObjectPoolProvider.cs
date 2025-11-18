using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.ObjectPool;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Singleton provider для ObjectPools
/// Обеспечивает centralized access к всем pools в application
/// </summary>
public sealed class ObjectPoolProvider
{
    private static ObjectPoolProvider? _instance;
    private static readonly object _lock = new();

    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly ObjectPool<List<ISymbol>> _symbolListPool;
    private readonly ObjectPool<List<string>> _stringListPool;
    private readonly ObjectPool<byte[]> _bufferPool;

    private ObjectPoolProvider()
    {
        var provider = new DefaultObjectPoolProvider();

        // StringBuilder pool: для code generation, formatting
        _stringBuilderPool = provider.Create(new PooledStringBuilderPolicy(
            initialCapacity: 1024,
            maxCapacity: 16 * 1024));

        // List<ISymbol> pool: для symbol search results
        _symbolListPool = provider.Create(new PooledSymbolListPolicy(
            initialCapacity: 128,
            maxCapacity: 2048));

        // List<string> pool: для FQN lists, path lists
        _stringListPool = provider.Create(new PooledStringListPolicy(
            initialCapacity: 64,
            maxCapacity: 1024));

        // Byte array pool: для file I/O, HTTP buffers
        _bufferPool = provider.Create(new PooledByteArrayPolicy(
            bufferSize: 4096,
            maxBuffersPerBucket: 50));
    }

    public static ObjectPoolProvider Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ObjectPoolProvider();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Получить StringBuilder из pool
    /// </summary>
    public StringBuilder GetStringBuilder() => _stringBuilderPool.Get();

    /// <summary>
    /// Вернуть StringBuilder в pool
    /// </summary>
    public void ReturnStringBuilder(StringBuilder sb) => _stringBuilderPool.Return(sb);

    /// <summary>
    /// Получить List&lt;ISymbol&gt; из pool
    /// </summary>
    public List<ISymbol> GetSymbolList() => _symbolListPool.Get();

    /// <summary>
    /// Вернуть List&lt;ISymbol&gt; в pool
    /// </summary>
    public void ReturnSymbolList(List<ISymbol> list) => _symbolListPool.Return(list);

    /// <summary>
    /// Получить List&lt;string&gt; из pool
    /// </summary>
    public List<string> GetStringList() => _stringListPool.Get();

    /// <summary>
    /// Вернуть List&lt;string&gt; в pool
    /// </summary>
    public void ReturnStringList(List<string> list) => _stringListPool.Return(list);

    /// <summary>
    /// Получить byte[] buffer из pool (4KB)
    /// </summary>
    public byte[] GetBuffer() => _bufferPool.Get();

    /// <summary>
    /// Вернуть byte[] buffer в pool
    /// </summary>
    public void ReturnBuffer(byte[] buffer) => _bufferPool.Return(buffer);
}

/// <summary>
/// ObjectPool policy для byte arrays (HTTP buffers, file I/O)
/// </summary>
internal sealed class PooledByteArrayPolicy : IPooledObjectPolicy<byte[]>
{
    private readonly int _bufferSize;

    public PooledByteArrayPolicy(int bufferSize, int maxBuffersPerBucket)
    {
        _bufferSize = bufferSize;
    }

    public byte[] Create()
    {
        return new byte[_bufferSize];
    }

    public bool Return(byte[] obj)
    {
        // Проверяем размер буфера
        if (obj.Length != _bufferSize)
        {
            return false;
        }

        // Очищаем перед возвратом (опционально, для security)
        Array.Clear(obj, 0, obj.Length);
        return true;
    }
}

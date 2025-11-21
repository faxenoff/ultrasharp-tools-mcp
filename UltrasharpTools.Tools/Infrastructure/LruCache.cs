namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Thread-safe Least Recently Used (LRU) cache with eviction callback.
/// </summary>
/// <typeparam name="TKey">Cache key type</typeparam>
/// <typeparam name="TValue">Cache value type</typeparam>
public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _maxSize;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _dictionary;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();

    /// <summary>
    /// Event fired when item is evicted from cache.
    /// </summary>
    public event Action<TKey, TValue>? OnEvict;

    public LruCache(int maxSize)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive");
        }

        _maxSize = maxSize;
        _dictionary = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// Try to get value from cache.
    /// Updates LRU position if found.
    /// </summary>
    public bool TryGet(TKey key, out TValue value)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            lock (_lock)
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }

            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Add or update value in cache.
    /// Evicts least recently used item if cache is full.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {
            // Remove existing if present
            if (_dictionary.TryRemove(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
            }

            // Add new item to front
            var cacheItem = new CacheItem(key, value);
            var newNode = _lruList.AddFirst(cacheItem);
            _dictionary[key] = newNode;

            // Evict LRU if over capacity
            if (_dictionary.Count > _maxSize)
            {
                var lruNode = _lruList.Last;
                if (lruNode != null)
                {
                    _lruList.RemoveLast();
                    _dictionary.TryRemove(lruNode.Value.Key, out _);

                    // Fire eviction callback
                    OnEvict?.Invoke(lruNode.Value.Key, lruNode.Value.Value);
                }
            }
        }
    }

    /// <summary>
    /// Remove item from cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_dictionary.TryRemove(key, out var node))
            {
                _lruList.Remove(node);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Clear all items from cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _dictionary.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Current number of items in cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _dictionary.Count;
            }
        }
    }

    /// <summary>
    /// Maximum cache size.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Get all keys in cache (snapshot).
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            lock (_lock)
            {
                return _dictionary.Keys.ToList();
            }
        }
    }

    private sealed record CacheItem(TKey Key, TValue Value);
}

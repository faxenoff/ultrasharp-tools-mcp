
using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Semantic.Models;
using UltrasharpTools.Tools.Semantic.Embedding;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Генератор embeddings с LRU кэшем и batch processing.
/// Thread-safe, поддерживает метрики производительности.
/// </summary>
public sealed class EmbeddingGenerator : IAsyncDisposable
{
    private readonly IEmbeddingProvider _provider;
    private readonly LruCache<string, float[]> _cache;
    private readonly ILogger<EmbeddingGenerator> _logger;
    private readonly EmbeddingGeneratorConfig _config;

    // Метрики
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalEmbedTimeMs;

    public EmbeddingGenerator(
    IEmbeddingProvider provider,
    EmbeddingGeneratorConfig? config = null,
    ILogger<EmbeddingGenerator>? logger = null)
    {
        _provider = provider;
        _config = config ?? EmbeddingGeneratorConfig.Default;
        _cache = new LruCache<string, float[]>(_config.CacheSize);
        _logger = logger ?? NullLogger<EmbeddingGenerator>.Instance;
    }

    /// <summary>
    /// Генерировать embedding для одного текста (с кэшем).
    /// </summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or whitespace", nameof(text));
        }

        Interlocked.Increment(ref _totalRequests);

        // Проверить кэш
        var cacheKey = GetCacheKey(text);
        if (_cache.TryGet(cacheKey, out var cachedVector))
        {
            Interlocked.Increment(ref _cacheHits);
            _logger.LogDebug("Cache hit for text (length={Length})", text.Length);
            return cachedVector;
        }

        Interlocked.Increment(ref _cacheMisses);

        // Генерировать embedding
        var sw = Stopwatch.StartNew();
        var vector = await _provider.EmbedAsync(text, ct);
        sw.Stop();

        Interlocked.Add(ref _totalEmbedTimeMs, sw.ElapsedMilliseconds);

        // Добавить в кэш
        _cache.Add(cacheKey, vector);

        _logger.LogDebug(
        "Generated embedding for text (length={Length}) in {ElapsedMs}ms",
        text.Length, sw.ElapsedMilliseconds);

        return vector;
    }

    /// <summary>
    /// Генерировать embeddings для batch текстов (оптимизировано).
    /// Использует кэш и минимизирует количество API вызовов.
    /// </summary>
    public async Task<float[][]> EmbedBatchAsync(string[] texts, CancellationToken ct = default)
    {
        if (texts.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        Interlocked.Add(ref _totalRequests, texts.Length);

        // Разделить на cached и uncached
        var results = new float[texts.Length][];
        var uncachedIndices = new List<int>();
        var uncachedTexts = new List<string>();

        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            var cacheKey = GetCacheKey(text);

            if (_cache.TryGet(cacheKey, out var cachedVector))
            {
                Interlocked.Increment(ref _cacheHits);
                results[i] = cachedVector;
            }
            else
            {
                Interlocked.Increment(ref _cacheMisses);
                uncachedIndices.Add(i);
                uncachedTexts.Add(text);
            }
        }

        // Если все в кэше - вернуть результат
        if (uncachedTexts.Count == 0)
        {
            _logger.LogDebug("All {Count} texts found in cache", texts.Length);
            return results;
        }

        // Генерировать embeddings для uncached текстов
        _logger.LogDebug(
        "Generating embeddings for {UncachedCount}/{TotalCount} texts (cache hit rate: {HitRate:P1})",
        uncachedTexts.Count, texts.Length,
        (double)_cacheHits / _totalRequests);

        var sw = Stopwatch.StartNew();
        var newVectors = await _provider.EmbedBatchAsync(uncachedTexts.ToArray(), ct);
        sw.Stop();

        Interlocked.Add(ref _totalEmbedTimeMs, sw.ElapsedMilliseconds);

        // Добавить в результаты и кэш
        for (int i = 0; i < uncachedIndices.Count; i++)
        {
            var index = uncachedIndices[i];
            var vector = newVectors[i];
            var text = uncachedTexts[i];

            results[index] = vector;
            _cache.Add(GetCacheKey(text), vector);
        }

        _logger.LogDebug(
        "Generated {Count} embeddings in {ElapsedMs}ms ({AvgMs:F2}ms/embedding)",
        uncachedTexts.Count, sw.ElapsedMilliseconds,
        (double)sw.ElapsedMilliseconds / uncachedTexts.Count);

        return results;
    }

    /// <summary>
    /// Очистить кэш.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Embedding cache cleared");
    }

    /// <summary>
    /// Получить метрики производительности.
    /// </summary>
    public EmbeddingMetrics GetMetrics()
    {
        var totalRequests = Interlocked.Read(ref _totalRequests);
        var cacheHits = Interlocked.Read(ref _cacheHits);
        var cacheMisses = Interlocked.Read(ref _cacheMisses);
        var totalEmbedTimeMs = Interlocked.Read(ref _totalEmbedTimeMs);

        return new EmbeddingMetrics
        {
            TotalRequests = totalRequests,
            CacheHits = cacheHits,
            CacheMisses = cacheMisses,
            CacheHitRate = totalRequests > 0 ? (double)cacheHits / totalRequests : 0,
            CacheSize = _cache.Count,
            CacheCapacity = _config.CacheSize,
            TotalEmbedTimeMs = totalEmbedTimeMs,
            AverageEmbedTimeMs = cacheMisses > 0 ? (double)totalEmbedTimeMs / cacheMisses : 0,
            ProviderInfo = _provider.Info
        };
    }

    /// <summary>
    /// Сбросить метрики.
    /// </summary>
    public void ResetMetrics()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
        Interlocked.Exchange(ref _totalEmbedTimeMs, 0);

        _logger.LogInformation("Embedding metrics reset");
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    // Private helpers

    private string GetCacheKey(string text)
    {
        // Простой cache key: нормализованный текст
        // В production можно использовать hash для экономии памяти
        if (!_config.NormalizeTextForCache)
            return text;

        // 1. Trim whitespace
        text = text.Trim();

        // 2. Normalize line endings (CR/LF → LF)
        text = Infrastructure.FileNormalizer.NormalizeLineEndings(text);

        // 3. Normalize to lowercase (опционально)
        if (_config.NormalizeCaseForCache)
        {
            text = text.ToLowerInvariant();
        }

        return text;
    }
}

/// <summary>
/// Конфигурация для EmbeddingGenerator.
/// </summary>
public sealed record EmbeddingGeneratorConfig
{
    /// <summary>
    /// Размер LRU кэша (количество embeddings).
    /// Default: 10,000 (достаточно для большинства проектов).
    /// </summary>
    public int CacheSize { get; init; } = 10_000;

    /// <summary>
    /// Нормализовать текст для кэша (trim + line endings + optional lowercase).
    /// Default: true (повышает cache hit rate).
    /// </summary>
    public bool NormalizeTextForCache { get; init; } = true;

    /// <summary>
    /// Нормализовать case (lowercase) для кэша.
    /// Default: true (для code similarity это обычно полезно).
    /// </summary>
    public bool NormalizeCaseForCache { get; init; } = true;

    public static EmbeddingGeneratorConfig Default => new();

    /// <summary>
    /// Конфигурация для больших проектов.
    /// </summary>
    public static EmbeddingGeneratorConfig ForLargeProjects => new()
    {
        CacheSize = 50_000
    };

    /// <summary>
    /// Конфигурация для малых проектов.
    /// </summary>
    public static EmbeddingGeneratorConfig ForSmallProjects => new()
    {
        CacheSize = 5_000
    };

    /// <summary>
    /// Конфигурация без кэша (для тестирования).
    /// </summary>
    public static EmbeddingGeneratorConfig NoCache => new()
    {
        CacheSize = 0
    };
}

/// <summary>
/// Метрики производительности EmbeddingGenerator.
/// </summary>
public sealed record EmbeddingMetrics
{
    public required long TotalRequests { get; init; }
    public required long CacheHits { get; init; }
    public required long CacheMisses { get; init; }
    public required double CacheHitRate { get; init; }
    public required int CacheSize { get; init; }
    public required int CacheCapacity { get; init; }
    public required long TotalEmbedTimeMs { get; init; }
    public required double AverageEmbedTimeMs { get; init; }
    public required ProviderInfo ProviderInfo { get; init; }
}

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache.
/// </summary>
internal sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();

    public int Count => _cache.Count;

    public LruCache(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentException("Capacity must be non-negative", nameof(capacity));
        }

        _capacity = capacity;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            // Переместить в начало (most recently used)
            lock (_lock)
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }

            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        if (_capacity == 0)
        {
            return; // Cache disabled
        }

        lock (_lock)
        {
            // Если ключ уже есть - обновить
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _cache.TryRemove(key, out _);
            }

            // Удалить LRU элемент если превышена capacity
            while (_cache.Count >= _capacity && _lruList.Last != null)
            {
                var lruNode = _lruList.Last;
                _lruList.RemoveLast();
                _cache.TryRemove(lruNode.Value.Key, out _);
            }

            // Добавить новый элемент
            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            _lruList.AddFirst(newNode);
            _cache[key] = newNode;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    private sealed record CacheItem(TKey Key, TValue Value);
}

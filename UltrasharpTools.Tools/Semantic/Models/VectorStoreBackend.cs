namespace UltrasharpTools.Tools.Semantic.Models;

/// <summary>
/// Vector store backend type для хранения и поиска векторных embeddings.
/// </summary>
public enum VectorStoreBackendType
{
    /// <summary>
    /// Microsoft SqliteVec - brute-force SIMD search.
    /// Оптимально для малых кодовых баз (&lt;10K векторов).
    /// Преимущества: 100% точность, быстрая индексация, простая интеграция через NuGet.
    /// </summary>
    SqliteVec,

    /// <summary>
    /// Vectorlite HNSW (Hierarchical Navigable Small World) - approximate nearest neighbors.
    /// Оптимально для больших кодовых баз (&gt;10K векторов).
    /// Преимущества: 3x-100x быстрее чем brute-force, 99.9%+ recall, масштабируется до 100K+ векторов.
    /// Недостатки: медленнее индексация, требует native .dll.
    /// </summary>
    Vectorlite,

    /// <summary>
    /// Автоматический выбор backend на основе количества векторов:
    /// - Если &lt;= 10,000 векторов → SqliteVec (простота + точность)
    /// - Если &gt; 10,000 векторов → Vectorlite (производительность)
    /// Порог настраивается через конфигурацию.
    /// </summary>
    Auto,
}

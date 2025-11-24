# RAG Integration Design Document

**Project:** SharpToolsMCP
**Version:** 1.0.0
**Date:** 2025-01-14
**Status:** Design Phase
**Author:** AI Assistant (Claude Sonnet 4.5)

---

## 📋 Executive Summary

This document describes the integration of **Retrieval-Augmented Generation (RAG)** capabilities into SharpToolsMCP using **vector embeddings** and **semantic search**. The solution will provide:

- **Hybrid Search**: Combining Roslyn's structural analysis with semantic vector search
- **Adaptive Vector Backend**: SqliteVec (brute-force) for small codebases, Vectorlite (HNSW ANN) for large codebases
- **Local Inference**: Ollama and TEI (Text Embeddings Inference) via Docker
- **IBM Granite Models**: Three embedding model tiers (30M, 125M, 278M parameters)
- **Graceful Fallback**: Memory-based deterministic embeddings using xxHash
- **Production Ready**: SIMD-optimized or HNSW-indexed vector search, LRU caching, batch processing

**Key Metrics (Expected):**

**Small Codebases (<10K symbols) - SqliteVec:**
- Semantic search: <10ms for <1K vectors, <100ms for <10K vectors
- 100% accuracy (brute-force SIMD search)
- Fast indexing: ~100-200 symbols/sec

**Large Codebases (>10K symbols) - Vectorlite:**
- Semantic search: <50ms for 10K-100K vectors (**3x-100x faster** than brute-force)
- 99.9%+ recall (HNSW approximate nearest neighbors)
- Slower indexing: ~50-100 symbols/sec (HNSW index building)

**Common:**
- Hybrid search: <300ms (Roslyn + Vector fusion)
- Memory overhead: +200-500MB (vectors + cache + HNSW index if used)
- Auto-switching at 10K vectors threshold (configurable)

---

## 🎯 Goals and Non-Goals

### Goals

✅ **Enable Semantic Code Search**
- Find code by natural language queries ("authentication logic", "database connection handling")
- Discover semantically similar code blocks (potential duplicates, code clones)
- Improve FindSimilarMethods with actual semantic understanding

✅ **Hybrid Search Architecture**
- Combine Roslyn's precise structural analysis (100% accurate for C# syntax)
- With vector-based semantic search (understanding intent and meaning)
- Reciprocal Rank Fusion (RRF) for result merging

✅ **Local, Privacy-Preserving Inference**
- No cloud API dependencies (all models run locally)
- Support Ollama (easy setup) and TEI (production-grade performance)
- Fallback to deterministic xxHash embeddings (no ML required)

✅ **Three-Tier Model Support**
- **ibm-granite/granite-3.0-embedding-30m** (384 dims, fastest, multilingual)
- **ibm-granite/granite-3.0-embedding-125m** (768 dims, balanced, multilingual)
- **ibm/granite-embedding:278m** (768 dims, best quality, Ollama only)

✅ **Production Performance**
- SIMD-optimized cosine similarity (already implemented in Performance Phase 5)
- Hybrid two-stage search for >10K vectors
- LRU cache with TTL for embeddings
- Batch processing (8-16 symbols at a time)

### Non-Goals

❌ **Multi-Language AST Parsing**
- SharpTools will remain C#-focused (Roslyn)
- No tree-sitter integration for other languages

❌ **Cloud-Based Embeddings**
- No OpenAI, HuggingFace API, or other cloud providers (privacy-first approach)
- Exception: May add in future as opt-in for users who prefer cloud

❌ **Full-Text Search Engine**
- Not replacing Roslyn or FastSymbolIndex
- Vector search complements, not replaces structural search

❌ **Real-Time Incremental Indexing**
- Initial version: manual reindexing via MCP tool
- Future: Git hook integration for auto-reindexing

---

## 🏗️ System Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         MCP Client (Claude)                      │
└───────────────────────┬─────────────────────────────────────────┘
                        │ JSON-RPC (stdio)
┌───────────────────────▼─────────────────────────────────────────┐
│                      UltrasharpTools MCP Server                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              New RAG Components                          │   │
│  │  ┌────────────────┐  ┌─────────────────┐               │   │
│  │  │ HybridSearch   │  │ CodeSemantic    │               │   │
│  │  │ Engine         │  │ VectorDB         │               │   │
│  │  └────────┬───────┘  └────────┬────────┘               │   │
│  │           │                    │                         │   │
│  │  ┌────────▼────────┐  ┌───────▼────────┐               │   │
│  │  │ VectorStore     │  │ Embedding      │               │   │
│  │  │ (Adaptive)      │  │ Generator      │               │   │
│  │  └────────┬────────┘  └───────┬────────┘               │   │
│  │           │                    │                         │   │
│  │  ┌────────▼─────────────────┐ │                         │   │
│  │  │  Backend Selector        │ │                         │   │
│  │  │  (<10K → SqliteVec)      │ │                         │   │
│  │  │  (>10K → Vectorlite)     │ │                         │   │
│  │  └────────┬─────────────────┘ │                         │   │
│  │           │                    │                         │   │
│  │  ┌────────┼────────────────┐  │                         │   │
│  │  │        │                │  │                         │   │
│  │  │ ┌──────▼───────┐ ┌──────▼─────┐                     │   │
│  │  │ │ SqliteVec    │ │ Vectorlite │                     │   │
│  │  │ │ Backend      │ │ Backend    │                     │   │
│  │  │ │ (Brute-force)│ │ (HNSW ANN) │                     │   │
│  │  │ └──────────────┘ └────────────┘                     │   │
│  │  └─────────────────────────────────                     │   │
│  │                                │                         │   │
│  │                       ┌────────▼────────┐               │   │
│  │                       │ Provider        │               │   │
│  │                       │ Factory         │               │   │
│  │                       └────────┬────────┘               │   │
│  │                                │                         │   │
│  │           ┌────────────────────┼────────────────┐       │   │
│  │           │                    │                │       │   │
│  │  ┌────────▼────────┐ ┌────────▼──────┐ ┌───────▼─────┐│   │
│  │  │ OllamaProvider  │ │ TEIProvider   │ │ Memory      ││   │
│  │  │ (port 11434)    │ │ (port 8080)   │ │ Provider    ││   │
│  │  └─────────────────┘ └───────────────┘ └─────────────┘│   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │           Existing SharpTools Components                 │   │
│  │  ┌─────────────────┐  ┌──────────────┐                  │   │
│  │  │ FastSymbolIndex │  │ Roslyn       │                  │   │
│  │  │ (Bloom+Frozen)  │  │ Semantic API │                  │   │
│  │  └─────────────────┘  └──────────────┘                  │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────────────────────┬─────────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────────┐
│                   External Services (Docker)                     │
│  ┌─────────────────────────┐  ┌─────────────────────────────┐  │
│  │ Ollama                  │  │ TEI Container               │  │
│  │ - granite-embedding:278m│  │ - granite-30m or granite-125m│ │
│  │ Port: 11434             │  │ Port: 8080                  │  │
│  └─────────────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

#### **1. VectorStore** (`UltrasharpTools.Tools/Semantic/VectorStore.cs`)

**Responsibilities:**
- Store and retrieve vector embeddings in SQLite
- SIMD-optimized cosine similarity search OR ANN index (vectorlite)
- Hybrid two-stage search for large datasets (>1000 vectors)
- Prepared statement caching for performance
- **Adaptive backend selection** (sqlite-vec for small, vectorlite for large)

**Key Operations:**
```csharp
Task InsertAsync(VectorEmbedding embedding);
Task InsertBatchAsync(IEnumerable<VectorEmbedding> embeddings);
Task<List<SimilarityResult>> SearchAsync(ReadOnlySpan<float> queryVector, int limit);
Task<int> GetCountAsync();
VectorStoreBackend GetCurrentBackend(); // "SqliteVec" | "Vectorlite"
```

---

### 🔬 **Vector Backend Selection: vectorlite vs sqlite-vec**

После детального исследования, для SharpTools рекомендуется **гибридный подход** с автоматическим выбором backend на основе размера кодовой базы.

#### **Сравнительный анализ**

| Характеристика | **sqlite-vec** | **vectorlite** |
|---------------|----------------|----------------|
| **Алгоритм поиска** | Brute-force (100% точность) | HNSW ANN (99.9%+ recall) |
| **Скорость запросов (3K векторов)** | Baseline | **3x-15x быстрее** (128d) <br> **6x-26x быстрее** (512d) <br> **7x-30x быстрее** (1536d) |
| **Скорость запросов (20K векторов)** | Disproportionally long | **3x-100x быстрее** |
| **Скорость вставки** | **6x-16x быстрее** | Медленнее (построение HNSW индекса) |
| **Масштабируемость** | ❌ Не масштабируется (O(n)) | ✅ Отлично (O(log n)) |
| **Точность (Recall)** | 100% (brute-force) | 99.90-100% (настраиваемо) |
| **Интеграция с .NET** | ✅ **Microsoft.SemanticKernel.Connectors.SqliteVec** (NuGet) | ⚠️ LoadExtension() (извлечение .dll из Python wheel) |
| **Стабильность** | ✅ Стабильный релиз | ⚠️ Beta (возможны breaking changes) |
| **SIMD ускорение** | ✅ Встроено | ✅ HNSW + внутренняя оптимизация |
| **Настройка параметров** | - | ✅ M, efConstruction, efSearch |

#### **Рекомендация для SharpTools**

**Стратегия: Автоматический выбор backend на основе размера датасета**

```csharp
public enum VectorStoreBackend
{
    /// <summary>Microsoft SqliteVec - brute force, для малых кодовых баз (<10K символов)</summary>
    SqliteVec,

    /// <summary>Vectorlite HNSW - ANN, для больших кодовых баз (>10K символов)</summary>
    Vectorlite,

    /// <summary>Автоматический выбор на основе количества векторов</summary>
    Auto
}

// Логика выбора:
// - Если векторов <= 10,000 → SqliteVec (простота + точность)
// - Если векторов > 10,000 → Vectorlite (производительность)
// - Пользователь может явно переопределить через конфигурацию
```

**Обоснование:**

1. **Для малых/средних проектов (1K-10K символов):**
   - sqlite-vec обеспечивает достаточную производительность
   - 100% точность (brute-force)
   - Простая интеграция через Microsoft NuGet
   - Быстрая индексация (редактирование кода)

2. **Для больших проектов (>10K символов):**
   - vectorlite обеспечивает **3x-100x ускорение** при запросах
   - 99.9%+ recall (практически как brute-force)
   - Критично для корпоративных кодовых баз (50K-500K символов)
   - Медленная индексация приемлема (выполняется редко)

3. **Гибридная архитектура:**
   - Оба backend реализуют `IVectorStoreBackend`
   - Автоматическое переключение при превышении порога
   - Миграция данных между backend при необходимости

#### **Параметры Vectorlite HNSW (для >10K векторов)**

```json
{
  "Vectorlite": {
    "M": 16,                    // Количество двунаправленных связей (default: 16)
                                // Больше M = выше recall, но больше памяти

    "efConstruction": 100,      // Размер динамического списка при построении (default: 200)
                                // Больше ef = лучше качество индекса, медленнее построение

    "efSearch": 50,             // Размер динамического списка при поиске (default: 50)
                                // Больше ef = выше recall, медленнее поиск

    "MaxElements": 100000       // Максимальное количество элементов в индексе
  }
}
```

**Рекомендуемые настройки для кодовых баз:**
- **Малые (10K-50K символов):** M=16, efConstruction=100, efSearch=50 (быстро, 99.5%+ recall)
- **Средние (50K-200K символов):** M=24, efConstruction=150, efSearch=75 (баланс)
- **Большие (>200K символов):** M=32, efConstruction=200, efSearch=100 (максимальное качество)

---

**Database Schema:**
```sql
-- Main table (одинаковая для обоих backend)
CREATE TABLE doc_embeddings (
    id TEXT PRIMARY KEY,
    content TEXT NOT NULL,
    vector BLOB NOT NULL,       -- float32 array serialized
    metadata TEXT,               -- JSON: { type, name, filePath, lineNumber }
    created_at INTEGER,
    dimension INTEGER,
    provider TEXT,               -- "ollama:granite-278m", "tei:granite-125m", "memory"
    backend TEXT                 -- "SqliteVec" | "Vectorlite" (для трекинга)
);

-- Indexes для metadata поиска
CREATE INDEX idx_embeddings_created ON doc_embeddings(created_at);
CREATE INDEX idx_embeddings_provider ON doc_embeddings(provider);
CREATE INDEX idx_embeddings_backend ON doc_embeddings(backend);

-- Для vectorlite: HNSW индекс создается через расширение
-- CREATE INDEX idx_vec_hnsw ON doc_embeddings USING hnsw(vector) WITH (m=16, ef_construction=100);
```

**Performance Characteristics:**

**SqliteVec Backend (<10K vectors):**
- Full scan search (<1K): <10ms
- Full scan search (1K-10K): 10-100ms
- Insert batch (100 embeddings): ~30ms (быстро)
- Memory: ~1KB per embedding (768 dims * 4 bytes + metadata)

**Vectorlite Backend (>10K vectors):**
- HNSW search (10K-100K): 10-50ms (3x-100x быстрее brute-force)
- Insert batch (100 embeddings): ~200ms (медленнее, но приемлемо)
- Memory: ~1.2KB per embedding (HNSW индекс + данные)
- Index build time: ~2-5 seconds на 10K векторов

---

#### **2. EmbeddingGenerator** (`UltrasharpTools.Tools/Semantic/EmbeddingGenerator.cs`)

**Responsibilities:**
- Abstract away embedding provider details
- LRU cache with TTL (5 minutes)
- Batch processing support (8-16 at a time)
- Text normalization and preprocessing

**Key Operations:**
```csharp
Task<float[]> GenerateAsync(string text, CancellationToken ct = default);
Task<float[][]> GenerateBatchAsync(string[] texts, CancellationToken ct = default);
void ClearCache();
EmbeddingCacheStats GetCacheStats();
```

**Caching Strategy:**
```csharp
// Cache key format:
"{provider}:{model}:{xxHash(normalizedText)}"

// Example:
"ollama:granite-278m:a3f2c1b4d5e6"

// TTL: 5 minutes (300 seconds)
// Max entries: 1000
// Eviction: LRU (Least Recently Used)
```

---

#### **3. Provider Abstraction** (`UltrasharpTools.Tools/Semantic/Providers/`)

**IEmbeddingProvider Interface:**
```csharp
public interface IEmbeddingProvider
{
    /// <summary>Generate embedding for single text</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>Generate embeddings for batch of texts</summary>
    Task<float[][]> EmbedBatchAsync(string[] texts, CancellationToken ct = default);

    /// <summary>Vector dimension (384, 768, etc.)</summary>
    int Dimension { get; }

    /// <summary>Provider metadata</summary>
    ProviderInfo Info { get; }

    /// <summary>Initialize provider (load model, check health)</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Cleanup resources</summary>
    Task DisposeAsync();
}

public record ProviderInfo
{
    public required string Name { get; init; }        // "ollama", "tei", "memory"
    public required string Model { get; init; }       // "granite-278m", "granite-125m", etc.
    public required int Dimension { get; init; }
    public required int MaxTokens { get; init; }
    public required bool IsLocal { get; init; }
    public required string Version { get; init; }
}
```

**Provider Implementations:**

##### **3.1. OllamaProvider** (`Providers/OllamaProvider.cs`)

**Configuration:**
```json
{
  "BaseUrl": "http://127.0.0.1:11434",
  "Model": "ibm/granite-embedding:278m",
  "Timeout": "30s",
  "MaxRetries": 3,
  "RetryDelayMs": 1000
}
```

**HTTP API:**
```csharp
// Ollama embeddings endpoint:
POST http://127.0.0.1:11434/api/embed
{
  "model": "ibm/granite-embedding:278m",
  "input": "public class MyClass { }"
}

// Response:
{
  "model": "ibm/granite-embedding:278m",
  "embeddings": [[0.123, 0.456, ...]], // 768 floats
  "total_duration": 45000000,           // nanoseconds
  "load_duration": 5000000
}
```

**Features:**
- ✅ Easy setup (single Docker command or native install)
- ✅ Model caching (first request loads model into memory)
- ✅ Batch support (up to 16 texts at once)
- ✅ Automatic retry on transient failures

**Performance:**
- Cold start (model load): ~2-5 seconds
- Warm inference: ~50-100ms per embedding
- Memory: ~500MB-1GB (model in RAM)

##### **3.2. TEIProvider** (`Providers/TEIProvider.cs`)

**Configuration:**
```json
{
  "BaseUrl": "http://127.0.0.1:8080",
  "Model": "ibm-granite/granite-3.0-embedding-125m",
  "Timeout": "10s",
  "MaxBatchSize": 16
}
```

**HTTP API:**
```csharp
// TEI embeddings endpoint:
POST http://127.0.0.1:8080/embed
{
  "inputs": ["public class MyClass { }"]
}

// Response:
[
  [0.123, 0.456, ...] // 768 floats
]
```

**Docker Deployment:**
```bash
# Granite 30M (fastest, 384 dims):
docker run -d \
  --name tei-granite-30m \
  -p 8080:80 \
  -v $PWD/data:/data \
  ghcr.io/huggingface/text-embeddings-inference:cpu-1.5 \
  --model-id ibm-granite/granite-3.0-embedding-30m \
  --max-client-batch-size 16

# Granite 125M (balanced, 768 dims):
docker run -d \
  --name tei-granite-125m \
  -p 8080:80 \
  -v $PWD/data:/data \
  ghcr.io/huggingface/text-embeddings-inference:cpu-1.5 \
  --model-id ibm-granite/granite-3.0-embedding-125m \
  --max-client-batch-size 16
```

**Features:**
- ✅ Production-grade performance (Rust-based, optimized)
- ✅ Lower latency than Ollama (~20-30ms per embedding)
- ✅ Batch processing (up to 32 texts)
- ✅ Health check endpoint for readiness probe

**Performance:**
- Cold start (first request): ~1-2 seconds
- Warm inference (30M): ~20ms per embedding
- Warm inference (125M): ~40ms per embedding
- Memory (30M): ~200MB
- Memory (125M): ~600MB

##### **3.3. MemoryProvider** (`Providers/MemoryProvider.cs`)

**Purpose:** Zero-dependency fallback when no ML models available

**Algorithm:**
```csharp
// Deterministic pseudo-embeddings using xxHash:
public float[] GenerateEmbedding(string text, int dimension = 768)
{
    var normalized = NormalizeText(text);
    var embedding = new float[dimension];

    // Generate deterministic hash-based "embedding":
    for (int i = 0; i < dimension; i++)
    {
        var seed = (ulong)(i + 1);
        var hash = XxHash64.HashToUInt64(
            Encoding.UTF8.GetBytes(normalized + seed.ToString())
        );

        // Normalize to [-1, 1]:
        embedding[i] = (float)((hash / (double)ulong.MaxValue) * 2.0 - 1.0);
    }

    return embedding;
}
```

**Characteristics:**
- ✅ Zero dependencies (no network, no Docker)
- ✅ Deterministic (same input → same output)
- ✅ Extremely fast (~1-2μs per embedding)
- ⚠️ No semantic understanding (purely syntactic hashing)
- ⚠️ Lower quality than ML models (but better than nothing)

**Use Cases:**
- CI/CD environments without GPU/ML infrastructure
- Offline development
- Quick prototyping
- Graceful degradation

---

#### **4. EmbeddingProviderFactory** (`UltrasharpTools.Tools/Semantic/EmbeddingProviderFactory.cs`)

**Responsibilities:**
- Auto-detect available providers (Ollama → TEI → Memory)
- Create provider instances based on configuration
- Health check and fallback logic

**Auto-Detection Algorithm:**
```csharp
public static async Task<IEmbeddingProvider> CreateAsync(
    EmbeddingConfig? config = null,
    CancellationToken ct = default)
{
    // Priority order:
    // 1. Explicit configuration
    // 2. TEI (best performance)
    // 3. Ollama (easy setup)
    // 4. Memory (fallback)

    if (config?.Provider != "auto")
    {
        return CreateSpecificProvider(config);
    }

    // Auto-detect:
    var available = await DetectAvailableProvidersAsync(ct);

    if (available.Contains("tei"))
    {
        return new TEIProvider(new TEIConfig
        {
            BaseUrl = "http://127.0.0.1:8080",
            Model = await DetectTEIModelAsync(ct)  // granite-30m or 125m
        });
    }

    if (available.Contains("ollama"))
    {
        return new OllamaProvider(new OllamaConfig
        {
            BaseUrl = "http://127.0.0.1:11434",
            Model = "ibm/granite-embedding:278m"
        });
    }

    // Fallback:
    return new MemoryProvider(dimension: 768);
}

private static async Task<List<string>> DetectAvailableProvidersAsync(
    CancellationToken ct)
{
    var available = new List<string>();

    // Check TEI:
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await client.GetAsync("http://127.0.0.1:8080/health", ct);
        if (response.IsSuccessStatusCode)
        {
            available.Add("tei");
        }
    }
    catch { /* Not available */ }

    // Check Ollama:
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await client.GetAsync("http://127.0.0.1:11434/api/tags", ct);
        if (response.IsSuccessStatusCode)
        {
            // Check if granite model is pulled:
            var content = await response.Content.ReadAsStringAsync(ct);
            if (content.Contains("granite-embedding"))
            {
                available.Add("ollama");
            }
        }
    }
    catch { /* Not available */ }

    return available;
}
```

---

#### **5. CodeSemanticIndexer** (`UltrasharpTools.Tools/Semantic/CodeSemanticIndexer.cs`)

**Responsibilities:**
- Extract code snippets from Roslyn syntax trees
- Format code for embedding generation
- Batch processing for efficiency
- Progress tracking and cancellation support

**Indexing Algorithm:**
```csharp
public async Task IndexSolutionAsync(
    IndexingOptions? options = null,
    IProgress<IndexingProgress>? progress = null,
    CancellationToken ct = default)
{
    options ??= new IndexingOptions
    {
        BatchSize = 8,
        IncludeTypes = true,
        IncludeMethods = true,
        IncludeProperties = true,
        MinCodeLength = 20,      // Skip trivial snippets
        MaxCodeLength = 2000     // Truncate very long code
    };

    // 1. Get all symbols from FastSymbolIndex:
    var allSymbols = _fastSymbolIndex.GetAllSymbols()
        .Where(s => ShouldIndex(s, options))
        .ToList();

    progress?.Report(new IndexingProgress
    {
        Phase = "Extraction",
        Total = allSymbols.Count,
        Current = 0
    });

    // 2. Extract code snippets:
    var snippets = new List<CodeSnippet>();

    foreach (var symbol in allSymbols)
    {
        ct.ThrowIfCancellationRequested();

        var snippet = await ExtractCodeSnippetAsync(symbol, ct);
        if (snippet != null)
        {
            snippets.Add(snippet);
        }
    }

    progress?.Report(new IndexingProgress
    {
        Phase = "Embedding Generation",
        Total = snippets.Count,
        Current = 0
    });

    // 3. Batch embedding generation:
    for (int i = 0; i < snippets.Count; i += options.BatchSize)
    {
        ct.ThrowIfCancellationRequested();

        var batch = snippets.Skip(i).Take(options.BatchSize).ToArray();
        var texts = batch.Select(s => FormatForEmbedding(s)).ToArray();

        var embeddings = await _embeddingGen.GenerateBatchAsync(texts, ct);

        // 4. Store vectors:
        var vectorEmbeddings = batch.Zip(embeddings, (snippet, embedding) =>
            new VectorEmbedding
            {
                Id = snippet.Id,
                Content = snippet.Code,
                Vector = embedding,
                Dimension = embedding.Length,
                Provider = _embeddingGen.GetProviderInfo().Name,
                Metadata = new Dictionary<string, object>
                {
                    ["type"] = snippet.Type,
                    ["name"] = snippet.Name,
                    ["filePath"] = snippet.FilePath,
                    ["lineNumber"] = snippet.LineNumber,
                    ["complexity"] = snippet.Complexity
                }
            });

        await _vectorStore.InsertBatchAsync(vectorEmbeddings, ct);

        progress?.Report(new IndexingProgress
        {
            Phase = "Embedding Generation",
            Total = snippets.Count,
            Current = i + batch.Length
        });
    }
}

// Format code snippet for embedding:
private string FormatForEmbedding(CodeSnippet snippet)
{
    var sb = new StringBuilder();

    // Structured format for better embeddings:
    sb.AppendLine($"Type: {snippet.Type}");
    sb.AppendLine($"Name: {snippet.Name}");

    if (!string.IsNullOrEmpty(snippet.Documentation))
    {
        sb.AppendLine($"Summary: {snippet.Documentation}");
    }

    if (snippet.Parameters.Any())
    {
        sb.AppendLine($"Parameters: {string.Join(", ", snippet.Parameters)}");
    }

    sb.AppendLine("Code:");
    sb.AppendLine(snippet.Code);

    return sb.ToString();
}
```

**Code Snippet Extraction:**
```csharp
private async Task<CodeSnippet?> ExtractCodeSnippetAsync(
    SymbolInfo symbol,
    CancellationToken ct)
{
    // Get syntax node from Roslyn:
    var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
    if (syntaxRef == null) return null;

    var node = await syntaxRef.GetSyntaxAsync(ct);
    var semanticModel = await symbol.Document.GetSemanticModelAsync(ct);

    // Extract code text:
    var code = node.ToFullString();
    if (code.Length < 20 || code.Length > 2000)
    {
        return null;  // Skip trivial or too long
    }

    // Extract documentation:
    var documentation = symbol.GetDocumentationCommentXml();
    var summary = ExtractSummary(documentation);

    // Calculate complexity:
    var complexity = CalculateComplexity(node);

    return new CodeSnippet
    {
        Id = symbol.Id,
        Type = symbol.Kind.ToString(),
        Name = symbol.Name,
        FilePath = symbol.FilePath,
        LineNumber = symbol.Location.StartLine,
        Code = code,
        Documentation = summary,
        Complexity = complexity,
        Parameters = ExtractParameters(node, semanticModel)
    };
}
```

---

#### **6. HybridSearchEngine** (`UltrasharpTools.Tools/Semantic/HybridSearchEngine.cs`)

**Responsibilities:**
- Combine Roslyn structural search with vector semantic search
- Reciprocal Rank Fusion (RRF) for result merging
- Configurable weights for structural vs semantic

**RRF Algorithm:**
```csharp
public async Task<List<HybridSearchResult>> SearchAsync(
    string query,
    HybridSearchOptions? options = null,
    CancellationToken ct = default)
{
    options ??= new HybridSearchOptions
    {
        Limit = 10,
        StructuralWeight = 0.6,  // 60% structural
        SemanticWeight = 0.4,    // 40% semantic
        K = 60                    // RRF constant
    };

    // 1. Generate embedding for semantic search:
    var queryEmbedding = await _embeddingGen.GenerateAsync(query, ct);

    // 2. Parallel execution:
    var (structural, semantic) = await (
        PerformStructuralSearchAsync(query, options.Limit * 2, ct),
        _vectorStore.SearchAsync(queryEmbedding, options.Limit * 2, ct)
    );

    // 3. RRF Fusion:
    return FuseResults(structural, semantic, options);
}

// Reciprocal Rank Fusion:
private List<HybridSearchResult> FuseResults(
    List<StructuralResult> structural,
    List<SimilarityResult> semantic,
    HybridSearchOptions options)
{
    var scores = new Dictionary<string, (double Score, object Data)>();

    // Structural RRF scoring:
    // Score = weight / (k + rank + 1)
    for (int rank = 0; rank < structural.Count; rank++)
    {
        var id = structural[rank].Id;
        var rrfScore = options.StructuralWeight / (options.K + rank + 1);

        if (scores.ContainsKey(id))
        {
            scores[id] = (scores[id].Score + rrfScore, structural[rank]);
        }
        else
        {
            scores[id] = (rrfScore, structural[rank]);
        }
    }

    // Semantic RRF scoring:
    for (int rank = 0; rank < semantic.Count; rank++)
    {
        var id = semantic[rank].Id;
        var rrfScore = options.SemanticWeight / (options.K + rank + 1);

        if (scores.ContainsKey(id))
        {
            scores[id] = (scores[id].Score + rrfScore, semantic[rank]);
        }
        else
        {
            scores[id] = (rrfScore, semantic[rank]);
        }
    }

    // Sort by combined score:
    return scores
        .OrderByDescending(kvp => kvp.Value.Score)
        .Take(options.Limit)
        .Select(kvp => new HybridSearchResult
        {
            Id = kvp.Key,
            Score = kvp.Value.Score,
            Data = kvp.Value.Data
        })
        .ToList();
}
```

**Structural Search (Roslyn + FastSymbolIndex):**
```csharp
private async Task<List<StructuralResult>> PerformStructuralSearchAsync(
    string query,
    int limit,
    CancellationToken ct)
{
    // Use existing FastSymbolIndex:
    var candidates = _fastSymbolIndex
        .SearchByName(query)
        .Concat(_fastSymbolIndex.SearchByPattern(query))
        .DistinctBy(s => s.Id)
        .ToList();

    // Score by relevance:
    var scored = new List<StructuralResult>();

    foreach (var candidate in candidates)
    {
        var score = CalculateStructuralScore(candidate, query);

        scored.Add(new StructuralResult
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Kind = candidate.Kind,
            FilePath = candidate.FilePath,
            Score = score
        });
    }

    return scored
        .OrderByDescending(r => r.Score)
        .Take(limit)
        .ToList();
}

private double CalculateStructuralScore(SymbolInfo symbol, string query)
{
    var score = 0.0;

    // Exact name match:
    if (symbol.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
    {
        score += 10.0;
    }

    // Starts with:
    if (symbol.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
    {
        score += 5.0;
    }

    // Contains:
    if (symbol.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
    {
        score += 2.0;
    }

    // Pattern match (regex):
    if (Regex.IsMatch(symbol.Name, query, RegexOptions.IgnoreCase))
    {
        score += 1.0;
    }

    return score;
}
```

---

## 🐳 Docker Deployment

### Docker Compose Configuration

```yaml
# docker-compose.yml
version: '3.8'

services:
  # Option 1: TEI with Granite 30M (fastest, 384 dims)
  tei-granite-30m:
    image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.5
    container_name: tei-granite-30m
    ports:
      - "8080:80"
    volumes:
      - ./data/tei-models:/data
    command: >
      --model-id ibm-granite/granite-3.0-embedding-30m
      --max-client-batch-size 16
      --max-batch-tokens 32768
    environment:
      - HUGGING_FACE_HUB_TOKEN=${HF_TOKEN}  # Optional, only if model is gated
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  # Option 2: TEI with Granite 125M (balanced, 768 dims)
  tei-granite-125m:
    image: ghcr.io/huggingface/text-embeddings-inference:cpu-1.5
    container_name: tei-granite-125m
    ports:
      - "8081:80"
    volumes:
      - ./data/tei-models:/data
    command: >
      --model-id ibm-granite/granite-3.0-embedding-125m
      --max-client-batch-size 16
      --max-batch-tokens 32768
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:80/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    profiles:
      - balanced  # Only start with: docker-compose --profile balanced up

  # Option 3: Ollama with Granite 278M (best quality, 768 dims)
  ollama:
    image: ollama/ollama:latest
    container_name: ollama-sharptools
    ports:
      - "11434:11434"
    volumes:
      - ./data/ollama:/root/.ollama
    environment:
      - OLLAMA_MODELS=/root/.ollama/models
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
```

### Startup Scripts

**Windows (PowerShell):**
```powershell
# setup-embeddings.ps1

param(
    [ValidateSet("tei-30m", "tei-125m", "ollama", "all")]
    [string]$Provider = "tei-30m"
)

Write-Host "🚀 Setting up SharpTools RAG Embeddings..." -ForegroundColor Cyan

# Create data directories:
New-Item -ItemType Directory -Force -Path ".\data\tei-models" | Out-Null
New-Item -ItemType Directory -Force -Path ".\data\ollama" | Out-Null
New-Item -ItemType Directory -Force -Path ".\data\vectors" | Out-Null

switch ($Provider) {
    "tei-30m" {
        Write-Host "Starting TEI with Granite 30M (384 dims)..." -ForegroundColor Green
        docker-compose up -d tei-granite-30m
    }
    "tei-125m" {
        Write-Host "Starting TEI with Granite 125M (768 dims)..." -ForegroundColor Green
        docker-compose --profile balanced up -d tei-granite-125m
    }
    "ollama" {
        Write-Host "Starting Ollama..." -ForegroundColor Green
        docker-compose up -d ollama

        # Pull granite model:
        Write-Host "Pulling granite-embedding:278m model (this may take a few minutes)..." -ForegroundColor Yellow
        docker exec ollama-sharptools ollama pull ibm/granite-embedding:278m
    }
    "all" {
        Write-Host "Starting all providers..." -ForegroundColor Green
        docker-compose --profile balanced up -d

        # Pull Ollama model:
        Start-Sleep -Seconds 5
        docker exec ollama-sharptools ollama pull ibm/granite-embedding:278m
    }
}

Write-Host ""
Write-Host "✅ Embeddings providers are starting..." -ForegroundColor Green
Write-Host ""
Write-Host "Health check URLs:" -ForegroundColor Cyan
Write-Host "  - TEI 30M:  http://127.0.0.1:8080/health"
Write-Host "  - TEI 125M: http://127.0.0.1:8081/health"
Write-Host "  - Ollama:   http://127.0.0.1:11434/api/tags"
Write-Host ""
Write-Host "To stop: docker-compose down" -ForegroundColor Yellow
```

**Linux/macOS (Bash):**
```bash
#!/bin/bash
# setup-embeddings.sh

PROVIDER=${1:-"tei-30m"}

echo "🚀 Setting up SharpTools RAG Embeddings..."

# Create data directories:
mkdir -p ./data/tei-models
mkdir -p ./data/ollama
mkdir -p ./data/vectors

case $PROVIDER in
    tei-30m)
        echo "Starting TEI with Granite 30M (384 dims)..."
        docker-compose up -d tei-granite-30m
        ;;
    tei-125m)
        echo "Starting TEI with Granite 125M (768 dims)..."
        docker-compose --profile balanced up -d tei-granite-125m
        ;;
    ollama)
        echo "Starting Ollama..."
        docker-compose up -d ollama

        # Pull granite model:
        echo "Pulling granite-embedding:278m model..."
        docker exec ollama-sharptools ollama pull ibm/granite-embedding:278m
        ;;
    all)
        echo "Starting all providers..."
        docker-compose --profile balanced up -d

        # Pull Ollama model:
        sleep 5
        docker exec ollama-sharptools ollama pull ibm/granite-embedding:278m
        ;;
esac

echo ""
echo "✅ Embeddings providers are starting..."
echo ""
echo "Health check URLs:"
echo "  - TEI 30M:  http://127.0.0.1:8080/health"
echo "  - TEI 125M: http://127.0.0.1:8081/health"
echo "  - Ollama:   http://127.0.0.1:11434/api/tags"
echo ""
echo "To stop: docker-compose down"
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Semantic": {
    "Enabled": true,

    "EmbeddingProvider": {
      "Type": "auto",  // "auto", "tei", "ollama", "memory"

      "TEI": {
        "BaseUrl": "http://127.0.0.1:8080",
        "Model": "ibm-granite/granite-3.0-embedding-30m",
        "Dimension": 384,
        "Timeout": "10s",
        "MaxRetries": 3,
        "HealthCheckInterval": "30s"
      },

      "Ollama": {
        "BaseUrl": "http://127.0.0.1:11434",
        "Model": "ibm/granite-embedding:278m",
        "Dimension": 768,
        "Timeout": "30s",
        "MaxRetries": 3,
        "HealthCheckInterval": "60s"
      },

      "Memory": {
        "Dimension": 768,
        "Algorithm": "xxHash64"
      }
    },

    "VectorStore": {
      "DatabasePath": "data/vectors/embeddings.db",
      "CacheSizeMB": 256,
      "WalMode": true,
      "HybridSearchThreshold": 1000,
      "MaxVectorsInMemory": 10000,

      // Backend selection: "auto", "sqlitevec", "vectorlite"
      "Backend": "auto",

      // Auto-switching threshold (количество векторов)
      "BackendSwitchThreshold": 10000,

      // SqliteVec settings (brute-force, для малых кодовых баз)
      "SqliteVec": {
        "Enabled": true,
        // Использует SIMD оптимизации из Microsoft.SemanticKernel
      },

      // Vectorlite settings (HNSW ANN, для больших кодовых баз)
      "Vectorlite": {
        "Enabled": true,
        "DllPath": "Native/vectorlite/vectorlite.dll",  // Относительный путь

        // HNSW параметры (влияют на скорость vs точность)
        "HNSW": {
          "M": 16,                  // Количество двунаправленных связей
                                    // Малые: M=16, Средние: M=24, Большие: M=32

          "efConstruction": 100,    // Размер списка при построении индекса
                                    // Малые: 100, Средние: 150, Большие: 200

          "efSearch": 50,           // Размер списка при поиске
                                    // Малые: 50, Средние: 75, Большие: 100

          "MaxElements": 100000     // Максимум векторов в индексе
        }
      }
    },

    "EmbeddingGenerator": {
      "BatchSize": 8,
      "CacheTTLSeconds": 300,
      "MaxCacheEntries": 1000,
      "TextNormalization": {
        "MaxLength": 2000,
        "MinLength": 20,
        "Lowercase": false,
        "RemoveComments": false
      }
    },

    "CodeIndexing": {
      "AutoIndexOnLoad": false,
      "IndexTypes": true,
      "IndexMethods": true,
      "IndexProperties": true,
      "IndexFields": false,
      "MinComplexity": 1,
      "MaxCodeLength": 2000
    },

    "HybridSearch": {
      "DefaultStructuralWeight": 0.6,
      "DefaultSemanticWeight": 0.4,
      "RrfConstant": 60,
      "MinStructuralMatches": 0,
      "MinSemanticSimilarity": 0.3
    }
  }
}
```

---

## 🧪 Testing Strategy

### Unit Tests

```csharp
// UltrasharpTools.Tests/Semantic/VectorStoreTests.cs
public class VectorStoreTests
{
    [Fact]
    public async Task InsertAndSearch_ShouldReturnSimilarVectors()
    {
        // Arrange
        var store = new VectorStore(":memory:");
        await store.InitializeAsync();

        var embedding1 = new VectorEmbedding
        {
            Id = "test1",
            Content = "public class MyClass { }",
            Vector = new float[] { 0.1f, 0.2f, 0.3f },
            Dimension = 3
        };

        await store.InsertAsync(embedding1);

        // Act
        var results = await store.SearchAsync(
            new float[] { 0.1f, 0.2f, 0.3f },
            limit: 10
        );

        // Assert
        Assert.Single(results);
        Assert.Equal("test1", results[0].Id);
        Assert.True(results[0].Similarity > 0.99f);
    }

    [Fact]
    public async Task HybridSearch_WithLargeDataset_ShouldUseTwoStage()
    {
        // Arrange
        var store = new VectorStore(":memory:");
        await store.InitializeAsync();

        // Insert 2000 vectors:
        for (int i = 0; i < 2000; i++)
        {
            await store.InsertAsync(new VectorEmbedding
            {
                Id = $"vec{i}",
                Content = $"Code snippet {i}",
                Vector = GenerateRandomVector(768),
                Dimension = 768
            });
        }

        // Act
        var sw = Stopwatch.StartNew();
        var results = await store.SearchAsync(
            GenerateRandomVector(768),
            limit: 10
        );
        sw.Stop();

        // Assert
        Assert.Equal(10, results.Count);
        Assert.True(sw.ElapsedMilliseconds < 200); // Should be fast
    }
}
```

### Integration Tests

```csharp
// UltrasharpTools.Tests/Semantic/IntegrationTests.cs
public class SemanticIntegrationTests
{
    [Fact]
    public async Task FullPipeline_IndexAndSearch_ShouldWork()
    {
        // Arrange
        var provider = new MemoryProvider(dimension: 768);
        var embeddingGen = new EmbeddingGenerator(provider);
        var vectorStore = new VectorStore(":memory:");
        await vectorStore.InitializeAsync();

        var vectordb = new CodeSemanticIndexer(
            solutionManager,
            embeddingGen,
            vectorStore
        );

        // Act: Index a small solution
        await vectordb.IndexSolutionAsync();

        // Act: Search
        var hybridSearch = new HybridSearchEngine(
            fastSymbolIndex,
            vectorStore,
            embeddingGen
        );

        var results = await hybridSearch.SearchAsync(
            "authentication logic",
            new HybridSearchOptions { Limit = 5 }
        );

        // Assert
        Assert.NotEmpty(results);
    }
}
```

### Performance Benchmarks

```csharp
// UltrasharpTools.Benchmarks/SemanticBenchmarks.cs
[MemoryDiagnoser]
public class SemanticBenchmarks
{
    private VectorStore _store;
    private EmbeddingGenerator _embeddingGen;

    [GlobalSetup]
    public async Task Setup()
    {
        _store = new VectorStore(":memory:");
        await _store.InitializeAsync();

        // Pre-populate with 10K vectors:
        for (int i = 0; i < 10000; i++)
        {
            await _store.InsertAsync(new VectorEmbedding
            {
                Id = $"vec{i}",
                Content = $"Code {i}",
                Vector = GenerateRandomVector(768),
                Dimension = 768
            });
        }
    }

    [Benchmark]
    public async Task VectorSearch_10K_Limit10()
    {
        var query = GenerateRandomVector(768);
        var results = await _store.SearchAsync(query, 10);
    }

    [Benchmark]
    public async Task HybridSearch_WithFusion()
    {
        var results = await _hybridSearch.SearchAsync(
            "authentication",
            new HybridSearchOptions { Limit = 10 }
        );
    }
}
```

---

## 📊 Performance Characteristics

### Expected Benchmarks

| Operation | Dataset Size | Latency (p50) | Latency (p99) | Memory |
|-----------|-------------|---------------|---------------|--------|
| **Embedding (TEI 30M)** | Single | 20ms | 50ms | - |
| **Embedding (TEI 125M)** | Single | 40ms | 100ms | - |
| **Embedding (Ollama 278M)** | Single | 80ms | 200ms | - |
| **Embedding (Memory)** | Single | 0.002ms | 0.01ms | - |
| **Vector Search** | <1K vectors | 5ms | 15ms | ~1MB |
| **Vector Search** | 1K-10K vectors | 30ms | 80ms | ~10MB |
| **Vector Search** | >10K vectors | 80ms | 150ms | ~50MB |
| **Hybrid Search** | 10K symbols | 150ms | 300ms | - |
| **Indexing** | 1000 symbols | 60s (TEI) | - | - |
| **Indexing** | 1000 symbols | 120s (Ollama) | - | - |

### Memory Usage

```
Base SharpTools:         ~200MB
+ VectorStore (10K):     +40MB (vectors)
+ EmbeddingGen cache:    +50MB (1000 cached)
+ TEI Docker (30M):      +200MB
+ TEI Docker (125M):     +600MB
+ Ollama Docker (278M):  +1GB

Total (TEI 30M):         ~500MB
Total (TEI 125M):        ~900MB
Total (Ollama 278M):     ~1.3GB
```

---

## 🚀 Implementation Plan

### Phase 1: Foundation (Week 1-2)

**Goal:** Core vector infrastructure with **гибридным backend** (SqliteVec + Vectorlite)

**Tasks:**

**1. ✅ Create project structure:**
   ```
   UltrasharpTools.Tools/Semantic/
   ├── VectorStore.cs               (главный интерфейс)
   ├── IVectorStoreBackend.cs       (абстракция backend)
   ├── Backends/
   │   ├── SqliteVecBackend.cs      (brute-force, <10K)
   │   ├── VectorliteBackend.cs     (HNSW ANN, >10K)
   │   └── BackendSelector.cs       (автовыбор)
   ├── IEmbeddingProvider.cs
   ├── EmbeddingGenerator.cs
   ├── Models/
   │   ├── VectorEmbedding.cs
   │   ├── SimilarityResult.cs
   │   ├── VectorStoreBackend.cs    (enum: SqliteVec | Vectorlite | Auto)
   │   └── ProviderInfo.cs
   └── Providers/
       └── MemoryProvider.cs
   ```

**2. ✅ Добавить NuGet зависимости:**
   ```xml
   <!-- UltrasharpTools.Tools.csproj -->
   <ItemGroup>
     <!-- SqliteVec: официальная поддержка Microsoft -->
     <PackageReference Include="Microsoft.SemanticKernel.Connectors.SqliteVec" Version="1.32.1-preview" />

     <!-- Базовая работа с SQLite -->
     <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
   </ItemGroup>
   ```

**3. ✅ Получить Vectorlite native библиотеку:**
   - Скачать Python wheel: `pip download vectorlite-py` (или с PyPI)
   - Извлечь `vectorlite.dll` (Windows x64) из wheel (это zip архив)
   - Разместить в `UltrasharpTools.Tools/Native/vectorlite/`
   - Настроить копирование в output directory:
     ```xml
     <ItemGroup>
       <None Include="Native\vectorlite\vectorlite.dll" CopyToOutputDirectory="PreserveNewest" />
     </ItemGroup>
     ```

**4. ✅ Implement IVectorStoreBackend:**
   ```csharp
   public interface IVectorStoreBackend : IAsyncDisposable
   {
       Task InitializeAsync(string connectionString, int dimension);
       Task InsertAsync(VectorEmbedding embedding);
       Task InsertBatchAsync(IEnumerable<VectorEmbedding> embeddings);
       Task<List<SimilarityResult>> SearchAsync(ReadOnlySpan<float> queryVector, int limit);
       Task<int> GetCountAsync();
       VectorStoreBackendType BackendType { get; }
   }
   ```

**5. ✅ Implement SqliteVecBackend:**
   - Использовать `Microsoft.SemanticKernel.Connectors.SqliteVec`
   - Brute-force SIMD search (уже оптимизирован в библиотеке)
   - Простая интеграция через NuGet

**6. ✅ Implement VectorliteBackend:**
   - Загрузить расширение через `SqliteConnection.LoadExtension("vectorlite.dll")`
   - HNSW индекс: `CREATE INDEX ... USING hnsw(vector)`
   - Настраиваемые параметры M, efConstruction, efSearch

**7. ✅ Implement BackendSelector:**
   - Auto-detection: count <= 10,000 → SqliteVec, иначе → Vectorlite
   - Поддержка явного выбора через конфигурацию
   - Миграция между backend при необходимости

**8. ✅ Implement VectorStore (facade):**
   - Делегирование вызовов к выбранному backend
   - Прозрачное переключение при превышении порога
   - Единый API для клиентов

**9. ✅ Implement MemoryProvider:**
   - xxHash-based deterministic embeddings
   - Fast, zero-dependency fallback

**10. ✅ Unit tests:**
   - SqliteVecBackend insert/search
   - VectorliteBackend insert/search (требует vectorlite.dll)
   - BackendSelector auto-switching logic
   - Performance comparison (<10K vs >10K)
   - SIMD vs HNSW accuracy comparison

**Deliverables:**
- ✅ Working VectorStore с двумя backend
- ✅ Auto-switching logic (SqliteVec ↔ Vectorlite)
- ✅ MemoryProvider as fallback
- ✅ Full test coverage

**Success Criteria:**
- VectorStore can insert and search vectors
- **SqliteVec:** Search <10ms for <1K vectors, <100ms for <10K vectors
- **Vectorlite:** Search <50ms for >10K vectors (3x-100x faster than brute-force)
- Auto-switching works correctly at 10K threshold
- Recall rate: SqliteVec 100%, Vectorlite 99.9%+
- All tests passing

**Dependencies:**
- ✅ Microsoft.SemanticKernel.Connectors.SqliteVec (NuGet)
- ✅ Microsoft.Data.Sqlite (NuGet)
- ⚠️ vectorlite.dll (manual extraction from Python wheel)
  - Download: https://github.com/1yefuwang1/vectorlite/releases
  - Or: `pip download vectorlite-py` → extract .dll from .whl (zip)

---

### Phase 2: Provider Integration (Week 2-3)

**Goal:** Add Ollama and TEI providers

**Tasks:**
1. ✅ Implement TEIProvider:
   - HTTP client with retry logic
   - Health check endpoint
   - Batch processing support
   - Model detection (30M vs 125M)

2. ✅ Implement OllamaProvider:
   - HTTP client for /api/embed
   - Model loading detection
   - Graceful handling of cold starts

3. ✅ Implement EmbeddingProviderFactory:
   - Auto-detection logic
   - Priority: TEI → Ollama → Memory
   - Configuration parsing

4. ✅ Implement EmbeddingGenerator:
   - LRU cache with TTL
   - Batch processing
   - Provider abstraction

5. ✅ Docker setup:
   - docker-compose.yml
   - setup-embeddings scripts
   - Documentation

**Deliverables:**
- Working TEI and Ollama providers
- Auto-detection factory
- Docker deployment guide

**Success Criteria:**
- Can generate embeddings via TEI
- Can generate embeddings via Ollama
- Auto-detection works correctly
- Docker containers start successfully

---

### Phase 3: Roslyn Integration (Week 3-4)

**Goal:** Index C# code and enable semantic search

**Tasks:**
1. ✅ Implement CodeSemanticIndexer:
   - Extract code snippets from Roslyn
   - Format code for embedding
   - Batch processing
   - Progress tracking

2. ✅ Integration with FastSymbolIndex:
   - Read symbols from existing index
   - Avoid duplicate work
   - Incremental indexing support

3. ✅ MCP Tools:
   - `semantic_index_solution`
   - `semantic_search`
   - `semantic_get_stats`

4. ✅ Testing:
   - Index SharpTools.sln itself
   - Measure indexing performance
   - Validate search quality

**Deliverables:**
- CodeSemanticIndexer working
- Can index SharpTools solution
- Semantic search via MCP

**Success Criteria:**
- Can index 1000 symbols in <2 minutes
- Search returns relevant results
- No performance regression on existing features

---

### Phase 4: Hybrid Search (Week 4-5)

**Goal:** Combine structural and semantic search with RRF

**Tasks:**
1. ✅ Implement HybridSearchEngine:
   - Parallel structural + semantic search
   - RRF fusion algorithm
   - Configurable weights

2. ✅ MCP Tools:
   - `hybrid_search`
   - `detect_semantic_duplicates`
   - `find_similar_code`

3. ✅ Benchmarking:
   - Compare structural-only vs hybrid
   - Measure fusion overhead
   - Tune default weights

4. ✅ Documentation:
   - User guide for hybrid search
   - Best practices
   - Configuration tuning

**Deliverables:**
- Working hybrid search
- RRF fusion
- Comprehensive benchmarks

**Success Criteria:**
- Hybrid search <300ms on 10K symbols
- Better results than structural-only
- Configurable fusion weights

---

### Phase 5: Polish & Production (Week 5-6)

**Goal:** Production-ready release

**Tasks:**
1. ✅ Performance tuning:
   - Cache optimization
   - Batch size tuning
   - Memory management

2. ✅ Error handling:
   - Graceful degradation
   - Retry logic
   - User-friendly errors

3. ✅ Documentation:
   - Complete user guide
   - API reference
   - Troubleshooting guide

4. ✅ Examples:
   - Sample queries
   - Configuration templates
   - Integration examples

5. ✅ Release:
   - Changelog
   - Migration guide
   - Docker images

**Deliverables:**
- Production-ready RAG system
- Complete documentation
- Release artifacts

**Success Criteria:**
- All performance benchmarks met
- Documentation complete
- Zero critical bugs

---

## 📚 API Reference

### MCP Tools

#### `semantic_index_solution`

**Description:** Index the loaded solution for semantic search

**Parameters:**
```json
{
  "batchSize": 8,
  "includeTypes": true,
  "includeMethods": true,
  "includeProperties": true,
  "minCodeLength": 20,
  "maxCodeLength": 2000
}
```

**Example:**
```json
{
  "method": "semantic_index_solution",
  "params": {
    "batchSize": 8
  }
}
```

**Response:**
```json
{
  "indexed": 1523,
  "skipped": 47,
  "durationMs": 78340,
  "provider": "tei:granite-30m",
  "dimension": 384
}
```

---

#### `semantic_search`

**Description:** Search code by semantic meaning

**Parameters:**
```json
{
  "query": "authentication logic",
  "limit": 10,
  "minSimilarity": 0.3
}
```

**Example:**
```json
{
  "method": "semantic_search",
  "params": {
    "query": "database connection handling",
    "limit": 5
  }
}
```

**Response:**
```json
{
  "results": [
    {
      "id": "a3f2c1b4d5e6",
      "name": "DatabaseConnectionManager",
      "type": "Class",
      "filePath": "Services/DatabaseConnectionManager.cs",
      "similarity": 0.87,
      "code": "public class DatabaseConnectionManager { ... }"
    }
  ],
  "total": 5,
  "durationMs": 45
}
```

---

#### `hybrid_search`

**Description:** Combine structural and semantic search

**Parameters:**
```json
{
  "query": "user authentication",
  "limit": 10,
  "structuralWeight": 0.6,
  "semanticWeight": 0.4
}
```

**Example:**
```json
{
  "method": "hybrid_search",
  "params": {
    "query": "login validation",
    "limit": 5,
    "structuralWeight": 0.5,
    "semanticWeight": 0.5
  }
}
```

**Response:**
```json
{
  "results": [
    {
      "id": "x7y8z9",
      "name": "ValidateLoginAsync",
      "type": "Method",
      "score": 0.82,
      "structuralScore": 0.45,
      "semanticScore": 0.37
    }
  ],
  "total": 5,
  "durationMs": 123
}
```

---

#### `detect_semantic_duplicates`

**Description:** Find semantically similar code (potential duplicates)

**Parameters:**
```json
{
  "threshold": 0.85,
  "minCodeLines": 5,
  "maxResults": 20
}
```

**Example:**
```json
{
  "method": "detect_semantic_duplicates",
  "params": {
    "threshold": 0.9
  }
}
```

**Response:**
```json
{
  "groups": [
    {
      "original": {
        "id": "abc123",
        "name": "CalculateTotal",
        "filePath": "Services/OrderService.cs"
      },
      "duplicates": [
        {
          "id": "def456",
          "name": "ComputeSum",
          "filePath": "Services/InvoiceService.cs",
          "similarity": 0.92
        }
      ]
    }
  ],
  "total": 1
}
```

---

## 🔒 Security & Privacy

### Data Privacy

✅ **All processing is local:**
- No code sent to cloud APIs
- Models run in Docker containers on localhost
- Vector database stored locally

✅ **No telemetry:**
- No usage data collected
- No model training on user code
- No external network calls (except model download)

### Security Considerations

⚠️ **Docker container access:**
- TEI and Ollama containers have no network access by default
- Only expose localhost ports
- Use Docker volumes for data isolation

⚠️ **SQLite database:**
- Store vectors.db in user data directory
- No encryption by default (add if needed)
- Regular backups recommended

---

## 🐛 Troubleshooting

### Common Issues

**Issue:** "No embedding provider available"

**Solution:**
1. Check if TEI/Ollama containers are running:
   ```bash
   docker ps
   ```
2. Check health endpoints:
   ```bash
   curl http://127.0.0.1:8080/health  # TEI
   curl http://127.0.0.1:11434/api/tags  # Ollama
   ```
3. Fallback to Memory provider (auto-enabled)

---

**Issue:** "Embedding generation is slow"

**Solution:**
1. Use TEI instead of Ollama (2-3x faster)
2. Reduce batch size in config
3. Check Docker container resources
4. Use Granite 30M instead of 125M/278M

---

**Issue:** "Out of memory during indexing"

**Solution:**
1. Reduce batch size to 4 or 2
2. Increase Docker container memory limit
3. Index in smaller chunks
4. Clear embedding cache periodically

---

## 📖 References

### IBM Granite Embedding Models

- **granite-3.0-embedding-30m**: https://huggingface.co/ibm-granite/granite-3.0-embedding-30m
- **granite-3.0-embedding-125m**: https://huggingface.co/ibm-granite/granite-3.0-embedding-125m
- **granite-embedding:278m (Ollama)**: https://ollama.com/ibm/granite-embedding

### Technologies

- **Text Embeddings Inference**: https://github.com/huggingface/text-embeddings-inference
- **Ollama**: https://ollama.com/
- **vectorlite** (HNSW ANN для больших кодовых баз): https://github.com/1yefuwang1/vectorlite
  - PyPI (для извлечения .dll): https://pypi.org/project/vectorlite/
  - Документация: https://1yefuwang1.github.io/vectorlite/
- **sqlite-vec** (brute-force для малых кодовых баз): https://github.com/asg017/sqlite-vec
  - Microsoft Semantic Kernel connector: https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/sqlite-connector
- **Reciprocal Rank Fusion**: https://plg.uwaterloo.ca/~gvcormac/cormacksigir09-rrf.pdf
- **HNSW algorithm**: https://arxiv.org/abs/1603.09320

### Inspiration

- **code-graph-rag-mcp**: D:\_mcp\code-graph-rag-mcp

---

## 📝 Changelog

### v1.1.0 (2025-01-14) - Hybrid Backend Design
- ✅ **Исследование vectorlite vs sqlite-vec**
  - Детальный performance анализ (3x-100x ускорение на больших датасетах)
  - Анализ accuracy (99.9%+ recall у vectorlite HNSW)
  - Сравнение интеграции с .NET
- ✅ **Гибридный подход с автоматическим выбором backend**
  - SqliteVec (brute-force) для малых кодовых баз (<10K символов)
  - Vectorlite (HNSW ANN) для больших кодовых баз (>10K символов)
  - Автоматическое переключение при превышении порога
- ✅ **Обновлен Implementation Plan Phase 1**
  - Добавлен IVectorStoreBackend abstraction
  - SqliteVecBackend + VectorliteBackend implementations
  - BackendSelector для auto-switching logic
- ✅ **Обновлена конфигурация**
  - VectorStore.Backend: "auto" | "sqlitevec" | "vectorlite"
  - VectorStore.BackendSwitchThreshold: 10000
  - Vectorlite.HNSW параметры (M, efConstruction, efSearch)
- ✅ **Добавлены References**
  - vectorlite GitHub, PyPI, docs
  - sqlite-vec + Microsoft Semantic Kernel connector
  - HNSW algorithm paper

### v1.0.0 (2025-01-14) - Initial Design
- Initial design document
- Architecture defined
- Implementation plan created
- Three IBM Granite models specified
- Docker deployment configuration
- 5-phase implementation plan (6 weeks)

---

**Status:** ✅ Design Complete with Hybrid Backend Strategy, Ready for Implementation

**Рекомендация:** Начать реализацию с Phase 1, используя гибридный подход. Это обеспечит:
- Простую интеграцию для малых проектов (SqliteVec через NuGet)
- Отличную масштабируемость для больших проектов (Vectorlite HNSW)
- Гибкость для пользователей (явный выбор backend)
**Next Steps:** Begin Phase 1 implementation

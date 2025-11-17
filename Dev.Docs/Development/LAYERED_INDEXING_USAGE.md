# Layered Indexing - Руководство по использованию

## 📋 Обзор

Layered Indexing - это система многослойной индексации символов с поддержкой:
- **Веточной изоляции** (branch awareness)
- **Инкрементальных обновлений** (100-500ms вместо 33s)
- **Git интеграции** (автоматическое вычисление deltas)
- **Персистентности** (SQLite хранилище)
- **Multi-client изоляции** (uncommitted changes per client)

## 🏗️ Архитектура

### Трёхслойная модель

```
┌─────────────────────────────────────┐
│  Layer 2: Working Directory         │  ← Per-client uncommitted changes
│  (WorkingDelta per clientId)        │
├─────────────────────────────────────┤
│  Layer 1: Branch Deltas             │  ← Per-branch committed changes
│  (BranchDelta per branch, cached)   │
├─────────────────────────────────────┤
│  Layer 0: Base Index                │  ← Main branch (shared)
│  (FastSymbolIndex, immutable)       │
└─────────────────────────────────────┘
```

**Query Flow:**
```
FindAsync(clientId, branch, term)
  ↓
  1. Query Layer 0 (Base) → baseResults
  2. Apply Layer 1 (Branch Delta) → branchResults
  3. Apply Layer 2 (Working Delta) → finalResults
  ↓
  Return merged results
```

## ⚙️ Конфигурация и регистрация

### Вариант 1: С LayeredIndexing (рекомендуется)

```csharp
services
    .WithUltrasharpToolsServices(enableGit: true)
    .WithLayeredIndexing(
        maxBranchDeltas: 20,          // LRU cache размер
        enablePersistence: true,       // SQLite хранилище
        deltaCompactionThreshold: 1000 // Compaction порог
    );
```

### Вариант 2: Только FastSymbolIndex (классика)

```csharp
services.WithUltrasharpToolsServices(enableGit: false);
```

### LayeredIndexingOptions

```csharp
public class LayeredIndexingOptions
{
    // LRU cache: max количество branch deltas в памяти
    public int MaxBranchDeltas { get; set; } = 20;

    // Включить SQLite персистентность (.ultrasharp/layered/deltas.db)
    public bool EnablePersistence { get; set; } = true;

    // Порог для compaction (объединение множественных изменений)
    public int DeltaCompactionThreshold { get; set; } = 1000;

    // Presets
    public static LayeredIndexingOptions Default { get; } = new() { };
    public static LayeredIndexingOptions Development { get; } = new()
    {
        MaxBranchDeltas = 10,
        EnablePersistence = false
    };
    public static LayeredIndexingOptions Production { get; } = new()
    {
        MaxBranchDeltas = 50,
        EnablePersistence = true
    };
}
```

## 🚀 Использование

### 1. Базовый поиск (без branch awareness)

```csharp
// Через FastSymbolIndex (Layer 0 only)
var results = _solutionManager.SymbolIndex.Find("MyClass");
```

### 2. Branch-aware поиск

```csharp
// Через LayeredSymbolIndex (все 3 слоя)
if (_solutionManager.LayeredIndex != null)
{
    var results = await _solutionManager.LayeredIndex.FindAsync(
        clientId: "user-123",      // Идентификатор клиента
        branch: "feature/new-api", // Текущая ветка
        searchTerm: "MyClass",     // Поисковый запрос
        cancellationToken);
}
```

### 3. Working Directory Operations (Layer 2)

```csharp
var layeredIndex = _solutionManager.LayeredIndex;

// Обновить uncommitted changes для клиента
await layeredIndex.UpdateWorkingDeltaAsync(
    clientId: "user-123",
    branch: "feature/new-api",
    documentId: changedDocId,
    cancellationToken);

// Очистить uncommitted changes
await layeredIndex.ClearWorkingDeltaAsync(
    clientId: "user-123",
    branch: "feature/new-api",
    cancellationToken);

// Продвинуть working delta в branch delta (после git commit)
await layeredIndex.PromoteWorkingToBranchAsync(
    clientId: "user-123",
    branch: "feature/new-api",
    newCommitSha: "abc123...",
    cancellationToken);
```

### 4. Branch Delta Operations (Layer 1)

```csharp
// Загрузить/создать branch delta
await layeredIndex.EnsureBranchDeltaAsync(
    branch: "feature/new-api",
    cancellationToken);

// Delta автоматически вычисляется из git diff при первом запросе
```

## 🔄 Инкрементальные обновления

Система автоматически отслеживает изменения workspace:

```csharp
// Автоматически подписывается при LoadSolutionAsync
workspace.RegisterWorkspaceChangedHandler(OnWorkspaceChanged);

// Обрабатывает:
// - DocumentAdded → AddDocumentAsync (100-200ms)
// - DocumentChanged → UpdateDocumentAsync (100-500ms)
// - DocumentRemoved → RemoveDocumentAsync (50-100ms)
```

**Batching & Throttling:**
- Batch window: 300ms
- Max batch size: 50 документов
- Deduplication: только последнее изменение для каждого файла

## 📂 Структура файлов

```
.ultrasharp/
├── layered/
│   └── deltas.db          # SQLite: branch deltas storage
├── vectors/
│   └── embeddings.db      # SQLite: vector embeddings
└── cache/
    └── symbols/           # Symbol cache files
```

### SQLite Schema (deltas.db)

```sql
CREATE TABLE BranchDeltas (
    BranchName TEXT PRIMARY KEY,
    BaseCommitSha TEXT NOT NULL,
    LastModified INTEGER NOT NULL,
    AddedSymbols TEXT NOT NULL,      -- JSON
    ModifiedSymbols TEXT NOT NULL,   -- JSON
    DeletedSymbolIds TEXT NOT NULL   -- JSON
);
```

## 🎯 Производительность

### Метрики

| Операция | FastSymbolIndex | LayeredSymbolIndex |
|----------|----------------|-------------------|
| Initial build | 33s | 33s + delta computation |
| Document change | 33s rebuild | 100-500ms incremental |
| Branch switch | 33s rebuild | <100ms (LRU hit) / 1-2s (miss) |
| Query (same branch) | 10-50ms | 12-60ms (+20% overhead) |
| Query (cross-branch) | N/A | 15-80ms |

### Память

| Component | Memory Usage |
|-----------|--------------|
| Base Index (Layer 0) | ~500-800 MB |
| Branch Delta (Layer 1) | ~10-50 MB per branch |
| Working Delta (Layer 2) | ~5-20 MB per client |
| LRU Cache (20 branches) | ~200-1000 MB total |

## 🔍 Git Integration

### Автоматическое вычисление Branch Delta

```csharp
// При первом запросе для ветки:
1. GetCurrentBranchAsync() → "feature/new-api"
2. GetMergeBaseCommitAsync("feature/new-api", "main") → "abc123..."
3. GetChangedFilesAsync("abc123...", "HEAD") → [file1.cs, file2.cs, ...]
4. ExtractSymbolsFromDocumentAsync() для каждого файла
5. Categorize: Added/Modified/Deleted
6. Save to SQLite (.ultrasharp/layered/deltas.db)
```

### Git Workflow

```csharp
// Git Commit: Promote Layer 2 → Layer 1
await layeredIndex.PromoteWorkingToBranchAsync(...);

// Git Pull: Preserve Layer 2, recompute Layer 1
var workingBackup = workingDelta.Clone();
// ... git pull ...
await layeredIndex.EnsureBranchDeltaAsync(branch); // recompute
layeredIndex.SetWorkingDelta(clientId, branch, workingBackup);

// Git Checkout: Clear Layer 2, load new Layer 1
await layeredIndex.ClearWorkingDeltaAsync(clientId, oldBranch);
await layeredIndex.EnsureBranchDeltaAsync(newBranch);
```

## 🐛 Отладка

### Логирование

```csharp
// Включить детальные логи
builder.Logging.AddFilter("UltrasharpTools.Tools.Layered", LogLevel.Debug);
```

### Типичные логи

```
[INFO] LayeredSymbolIndex initialized with max 20 branch deltas, persistence: True, git integration: True
[INFO] Base index built with 355847 symbols
[INFO] LayeredSymbolIndex built successfully
[INFO] Computing branch delta from git diff: feature/new-api
[INFO] Branch delta loaded: feature/new-api (127 added, 43 modified, 8 deleted)
[DEBUG] Branch delta cache hit: feature/new-api
[INFO] Batch completed: 15 succeeded, 0 failed in 287ms
```

### Performance Monitoring

```csharp
// Incremental Update Queue
var pendingCount = _incrementalUpdateQueue.PendingCount;
_logger.LogInformation("Pending updates: {Count}", pendingCount);

// Symbol Index Stats
_logger.LogInformation("Total symbols: {Count}", _symbolIndex.TotalSymbols);
_logger.LogInformation("Update counter: {Counter}", _symbolIndex.UpdateCounter);
```

## ⚠️ Ограничения

1. **Git required**: LayeredIndexing требует Git repository
2. **Memory overhead**: +20% памяти для branch deltas
3. **Query latency**: +20% на запросах (layer merging)
4. **SQLite locking**: Concurrent writes могут блокироваться
5. **C# only**: Поддержка только .cs файлов

## 🔮 Roadmap (Фазы 4-8)

- ✅ **Phase 0-1**: Base + Branch Deltas
- ✅ **Phase 3**: Incremental Updates
- ⏭️ **Phase 4**: Vector Integration (embeddings per layer)
- ⏭️ **Phase 5**: Git Workflow Integration (commit/pull/switch hooks)
- ⏭️ **Phase 6**: Optimization (compaction, delta merging)
- ⏭️ **Phase 7**: Testing & Validation
- ⏭️ **Phase 8**: Documentation & Deployment

## 📚 Примеры

### Пример 1: MCP Server с LayeredIndexing

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .WithUltrasharpToolsServices(enableGit: true)
    .WithLayeredIndexing(maxBranchDeltas: 20, enablePersistence: true);

var host = builder.Build();
var solutionManager = host.Services.GetRequiredService<ISolutionManager>();

// Load solution
await solutionManager.LoadSolutionAsync(@"C:\Projects\MyProject\MyProject.sln");

// Query with branch awareness
var results = await solutionManager.LayeredIndex?.FindAsync(
    clientId: "mcp-client-1",
    branch: "feature/new-api",
    searchTerm: "OrderService");

// Results include symbols from:
// - main branch (Layer 0)
// - feature/new-api commits (Layer 1)
// - uncommitted changes (Layer 2)
```

### Пример 2: Multi-client scenario

```csharp
// Client 1: working on feature/auth
var client1Results = await layeredIndex.FindAsync(
    clientId: "client-1",
    branch: "feature/auth",
    searchTerm: "UserController");

// Client 2: working on feature/payments
var client2Results = await layeredIndex.FindAsync(
    clientId: "client-2",
    branch: "feature/payments",
    searchTerm: "UserController");

// Каждый клиент видит СВОЮ версию кода!
```

### Пример 3: Development preset

```csharp
builder.Services.AddSingleton(LayeredIndexingOptions.Development);
// → MaxBranchDeltas: 10
// → EnablePersistence: false
// → Ideal for local development/testing
```

## 📞 Support

Issues: https://github.com/your-repo/issues
Docs: `Dev.Docs/Development/UNIFIED_IMPLEMENTATION_PLAN.md`

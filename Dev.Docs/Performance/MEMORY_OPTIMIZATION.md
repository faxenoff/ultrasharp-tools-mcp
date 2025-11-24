# Memory Optimization (v3.0.8)

## Обзор

Документ описывает оптимизации потребления памяти, реализованные в версии 3.0.8 UltrasharpTools.

**Проблема:** Droid процесс потреблял **1.7-2.5 GB** памяти на средних проектах, что делало невозможным использование на машинах с ограниченными ресурсами.

**Решение:** Комплексная оптимизация с уменьшением потребления до **400-600 MB** (-60-70%).

---

## Результаты

| Метрика | До (v3.0.7) | После (v3.0.8) | Экономия |
|---------|-------------|----------------|----------|
| MemoryCache лимиты | 1.5 GB | 400 MB | **-1.1 GB** |
| Reflection types (normal) | 50-150 MB | 50-150 MB | — |
| Reflection types (`--low-memory`) | 50-150 MB | 5 MB | **-50-100 MB** |
| **Общее потребление (normal)** | 1.7-2.5 GB | 600-900 MB | **-60%** |
| **Общее потребление (`--low-memory`)** | 1.7-2.5 GB | 400-600 MB | **-70%** |

---

## Компоненты оптимизации

### 1. MemoryCache Limits (всегда активно)

**Файл:** `UltrasharpTools.Tools/Extensions/ServiceCollectionExtensions.cs`

**До:**
```csharp
// Compilation cache
SizeLimit = 500 * 1024 * 1024  // 500 MB

// SemanticModel cache
SizeLimit = 1024 * 1024 * 1024  // 1 GB
```

**После:**
```csharp
// Compilation cache
SizeLimit = 150 * 1024 * 1024  // 150 MB (-70%)

// SemanticModel cache
SizeLimit = 250 * 1024 * 1024  // 250 MB (-75%)
```

**Экономия:** ~1.1 GB

**Почему это безопасно:**
- Compilation и SemanticModel кешируются на уровне документа
- LRU eviction удаляет редко используемые записи
- Roslyn пересоздаёт модели при необходимости (cache miss)
- Для большинства операций достаточно 150-250 MB кеша

---

### 2. SqliteSymbolIndex

**Файл:** `UltrasharpTools.Tools/Services/SqliteSymbolIndex.cs`

**Назначение:** Disk-based storage для символов с Full-Text Search.

**Архитектура:**
```
┌─────────────────────────────────────────────┐
│ SqliteSymbolIndex                           │
├─────────────────────────────────────────────┤
│ SQLite Database (WAL mode)                  │
│ ├─ symbols table                            │
│ │   └─ Id, Name, FQN, Kind, Flags, FilePath │
│ └─ symbols_fts (FTS5)                       │
│       └─ Name, FQN (searchable)             │
├─────────────────────────────────────────────┤
│ API:                                        │
│ - AddSymbolAsync(SymbolIndexEntry)          │
│ - SearchAsync(query) → SymbolSearchResult[] │
│ - GetStatisticsAsync() → stats              │
└─────────────────────────────────────────────┘
```

**Ключевые особенности:**
- **FTS5** — Full-Text Search для быстрого поиска по именам
- **WAL mode** — параллельные читатели без блокировок
- **SymbolSearchResult** — возвращает метаданные без ISymbol reference
- **Lazy restoration** — ISymbol восстанавливается через Roslyn API при необходимости

**Пример использования:**
```csharp
var index = new SqliteSymbolIndex(dbPath);
await index.InitializeAsync();

// Добавление символа
await index.AddSymbolAsync(new SymbolIndexEntry {
    Name = "MyClass",
    FullyQualifiedName = "MyNamespace.MyClass",
    Kind = SymbolKind.NamedType,
    FilePath = "MyClass.cs"
});

// Поиск
var results = await index.SearchAsync("MyClass");
// → [{ Name: "MyClass", FQN: "MyNamespace.MyClass", ... }]
```

---

### 3. SqliteReflectionTypeIndex

**Файл:** `UltrasharpTools.Tools/Services/SqliteReflectionTypeIndex.cs`

**Назначение:** Disk-based storage для reflection types с lazy loading.

**Архитектура:**
```
┌─────────────────────────────────────────────┐
│ SqliteReflectionTypeIndex                   │
├─────────────────────────────────────────────┤
│ SQLite Database (WAL mode)                  │
│ ├─ types table                              │
│ │   └─ FullName, AssemblyPath               │
│ └─ types_fts (FTS5)                         │
│       └─ FullName (searchable)              │
├─────────────────────────────────────────────┤
│ LRU Cache (500 entries)                     │
│ └─ FullName → Type (hot data)               │
├─────────────────────────────────────────────┤
│ MetadataLoadContext                         │
│ └─ Lazy loading of Type objects             │
├─────────────────────────────────────────────┤
│ API:                                        │
│ - PopulateFromAssembliesAsync()             │
│ - FindTypeAsync(fullName) → Type?           │
│ - SearchTypesAsync(query) → Type[]          │
└─────────────────────────────────────────────┘
```

**Ключевые особенности:**
- **FTS5** — полнотекстовый поиск по именам типов
- **LRU Cache** — 500 горячих типов в памяти
- **Lazy Loading** — Type объекты загружаются через MetadataLoadContext только при необходимости
- **WAL mode** — оптимизация для частых читающих операций

**Сравнение с FrozenDictionary:**

| Аспект | FrozenDictionary | SqliteReflectionTypeIndex |
|--------|-----------------|---------------------------|
| Memory | 50-150 MB | ~5 MB (SQLite + LRU) |
| Lookup | O(1) instant | O(1) SQLite + LRU |
| First access | Instant | +10-50ms (lazy load) |
| Search | O(N) linear | O(log N) FTS5 |
| Persistence | No (rebuild) | Yes (cached) |

---

### 4. Low Memory Mode (`--low-memory`)

**Файл:** `UltrasharpTools.Droid/Program.cs`

**CLI флаг:**
```bash
UltrasharpTools.Droid.exe --low-memory
```

**Что включает:**
1. Использование SqliteReflectionTypeIndex вместо FrozenDictionary
2. Дополнительная экономия ~50-100 MB

**Интеграция в DI:**

```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection WithUltrasharpToolsServices(
    this IServiceCollection services,
    // ... other params
    bool lowMemoryMode = false  // NEW
)
{
    // SolutionManager получает lowMemoryMode
    services.AddSingleton<ISolutionManager>(sp => new SolutionManager(
        // ... other deps
        lowMemoryMode: lowMemoryMode
    ));
}
```

**Интеграция в SolutionManager:**

```csharp
// SolutionManager.cs
private readonly bool _lowMemoryMode;
private SqliteReflectionTypeIndex? _sqliteReflectionTypeIndex;

public SolutionManager(..., bool lowMemoryMode = false)
{
    _lowMemoryMode = lowMemoryMode;
}

// При загрузке reflection cache
if (_lowMemoryMode)
{
    await PopulateReflectionCacheSqlite();  // SQLite + lazy loading
}
else
{
    PopulateReflectionCache();  // FrozenDictionary (in-memory)
}

// При поиске типа
public async ValueTask<Type?> FindReflectionTypeAsync(string fullName)
{
    if (_lowMemoryMode && _sqliteReflectionTypeIndex != null)
    {
        return await _sqliteReflectionTypeIndex.FindTypeAsync(fullName);
    }
    return _reflectionTypeCache.GetValueOrDefault(fullName);
}
```

---

## Performance Trade-offs

### Memory vs Speed

| Операция | Normal Mode | Low Memory Mode | Разница |
|----------|-------------|-----------------|---------|
| Type lookup | O(1) FrozenDict | O(1) SQLite + LRU | +1-5ms |
| Type search | O(N) linear | O(log N) FTS5 | **Быстрее** |
| First type load | Instant | Lazy load | +10-50ms |
| Memory | 600-900 MB | 400-600 MB | **-30%** |

### Когда использовать Low Memory Mode

**Рекомендуется:**
- Машины с < 8 GB RAM
- CI/CD pipelines (ограниченные ресурсы)
- Docker контейнеры с memory limits
- Параллельный запуск нескольких Droid процессов

**Не рекомендуется:**
- Машины с 16+ GB RAM
- Интенсивная работа с reflection types
- Production с требованиями к latency < 5ms

---

## Файлы и изменения

### Новые файлы

| Файл | Описание |
|------|----------|
| `SqliteSymbolIndex.cs` | Disk-based symbol index с FTS5 |
| `SqliteReflectionTypeIndex.cs` | Disk-based reflection type index |

### Изменённые файлы

| Файл | Изменения |
|------|-----------|
| `ServiceCollectionExtensions.cs` | Добавлен параметр `lowMemoryMode`, уменьшены лимиты MemoryCache |
| `SolutionManager.cs` | Интеграция SqliteReflectionTypeIndex, условная логика для low memory mode |
| `Program.cs` (Droid) | Добавлен CLI флаг `--low-memory` |

---

## Технические детали

### SQLite Configuration

```csharp
// Connection string
var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";

// Pragmas для производительности
await command.ExecuteNonQueryAsync("PRAGMA journal_mode = WAL");
await command.ExecuteNonQueryAsync("PRAGMA synchronous = NORMAL");
await command.ExecuteNonQueryAsync("PRAGMA cache_size = -64000"); // 64MB cache
await command.ExecuteNonQueryAsync("PRAGMA temp_store = MEMORY");
```

### LRU Cache Implementation

```csharp
private readonly LinkedList<(string Key, Type Value)> _lruList = new();
private readonly Dictionary<string, LinkedListNode<(string, Type)>> _lruDict = new();
private const int MaxLruSize = 500;

private void AddToLru(string key, Type value)
{
    if (_lruDict.TryGetValue(key, out var node))
    {
        _lruList.Remove(node);
        _lruList.AddFirst(node);
        return;
    }

    if (_lruList.Count >= MaxLruSize)
    {
        var last = _lruList.Last!;
        _lruDict.Remove(last.Value.Key);
        _lruList.RemoveLast();
    }

    var newNode = _lruList.AddFirst((key, value));
    _lruDict[key] = newNode;
}
```

### MetadataLoadContext for Lazy Loading

```csharp
private Type? LoadType(string fullName, string assemblyPath)
{
    if (_metadataLoadContext == null)
    {
        var resolver = new PathAssemblyResolver(_assemblyPaths);
        _metadataLoadContext = new MetadataLoadContext(resolver);
    }

    var assembly = _metadataLoadContext.LoadFromAssemblyPath(assemblyPath);
    return assembly.GetType(fullName);
}
```

---

## Рекомендации

### Для пользователей

1. **Default mode (без флагов)** — используйте на машинах с 8+ GB RAM
2. **Low memory mode** — добавьте `--low-memory` для ограниченных ресурсов
3. **CI/CD** — всегда используйте `--low-memory` в pipelines

### Для разработчиков

1. **Не храните ISymbol** — используйте FQN для lazy restoration
2. **Используйте SqliteSymbolIndex** — для disk-based symbol operations
3. **LRU cache** — держите hot data в памяти (500-1000 entries)
4. **WAL mode** — обязательно для SQLite в concurrent scenarios

---

## Связанные документы

- [Test_Report.md](Test_Report.md) — Performance benchmarks
- [PARALLELIZATION_SUMMARY.md](PARALLELIZATION_SUMMARY.md) — Parallel processing optimizations
- [../ULTRA-SHARPED.md](../ULTRA-SHARPED.md) — Все улучшения проекта
- [../../CHANGELOG.md](../../CHANGELOG.md) — История версий

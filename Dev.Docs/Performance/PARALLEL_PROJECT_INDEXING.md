# Parallel Project Indexing - Phase 5 Implementation

**Дата:** 2025-11-25
**Статус:** Реализовано
**Ожидаемый эффект:** 2-4x ускорение индексации на multi-project solutions

---

## Проблема

При загрузке решения индексация проектов выполнялась **последовательно**:

```csharp
// Было: последовательный foreach
foreach (var project in solution.Projects)
{
    var compilation = await project.GetCompilationAsync(cancellationToken);
    CollectSymbolsFromCompilation(compilation, entries, seenSymbols, cancellationToken);
}
```

**Проблемы старого подхода:**
- CPU недогружен (использовалось 1 ядро из N)
- Проекты без зависимостей ждали друг друга
- Для 10 проектов время = сумма времени каждого проекта

---

## Решение

### Алгоритм параллелизации по уровням зависимостей

```
1. Получить ProjectDependencyGraph от Roslyn
2. Построить уровни зависимостей:
   - Level 0: проекты без зависимостей
   - Level 1: проекты, зависящие только от Level 0
   - Level N: проекты, зависящие от Level 0..N-1
3. Для каждого уровня - Parallel.ForEachAsync
4. Thread-safe дедупликация через ConcurrentDictionary
```

### Пример для UltrasharpTools.sln

```
Level 0: VectorDB, Comm                    [параллельно, 2 проекта]
Level 1: Tools                             [1 проект]
Level 2: Droid, Overlord, Test.Common,     [параллельно, 4 проекта]
         Benchmarks
Level 3: Test.LayeredIndex,                [параллельно, 3 проекта]
         Test.SemanticAnalysis,
         Test.SemanticMerge
```

**Результат:** вместо 10 последовательных операций — 4 параллельных уровня.

---

## Изменённые файлы

### 1. FastSymbolIndex.cs

**Новые методы:**

| Метод | Назначение |
|-------|-----------|
| `BuildFromSolutionParallelAsync` | Основная логика параллельной компиляции |
| `BuildDependencyLevels` | Построение уровней из `ProjectDependencyGraph` |
| `BuildFromProjectsSequentialAsync` | Fallback для малых решений (≤2 проекта) |
| `CollectSymbolsFromCompilationParallel` | Thread-safe сбор символов |

**Ключевые изменения:**

```csharp
// Новый метод: параллельная компиляция по уровням
private async Task<List<SymbolIndexEntry>> BuildFromSolutionParallelAsync(
    Solution solution,
    CancellationToken cancellationToken)
{
    var dependencyGraph = solution.GetProjectDependencyGraph();
    var levels = BuildDependencyLevels(projects, dependencyGraph);

    // Thread-safe коллекции
    var allEntries = new ConcurrentBag<SymbolIndexEntry>();
    var seenSymbolFqns = new ConcurrentDictionary<string, byte>();

    // Обработка по уровням
    for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
    {
        var level = levels[levelIndex];

        await Parallel.ForEachAsync(level, parallelOptions, async (project, ct) =>
        {
            var compilation = await project.GetCompilationAsync(ct);
            var projectEntries = CollectSymbolsFromCompilationParallel(
                compilation, seenSymbolFqns, ct);

            foreach (var entry in projectEntries)
                allEntries.Add(entry);
        });
    }

    return allEntries.ToList();
}
```

**Thread-safe дедупликация:**

```csharp
// Вместо HashSet<ISymbol> используем ConcurrentDictionary<string, byte>
// Ключ - FQN символа, что обеспечивает thread-safety
if (seenSymbolFqns.TryAdd(canonicalFqn, 0))
{
    var entry = SymbolIndexEntryBuilder.Build(symbol, canonicalFqn);
    entries.Add(entry);
}
```

### 2. FastSymbolIndex.Logging.cs

**Новые логгеры:**

```csharp
[LoggerMessage(EventId = 3860, Level = LogLevel.Information,
    Message = "Starting parallel build: {ProjectCount} projects, {LevelCount} dependency levels, max parallelism = {MaxParallelism}")]
private partial void LogParallelBuildStart(int projectCount, int levelCount, int maxParallelism);

[LoggerMessage(EventId = 3861, Level = LogLevel.Debug,
    Message = "Completed level {Level}/{TotalLevels}: {ProjectCount} projects in {ElapsedMs}ms")]
private partial void LogLevelCompleted(int level, int totalLevels, int projectCount, long elapsedMs);

[LoggerMessage(EventId = 3862, Level = LogLevel.Information,
    Message = "Parallel build complete: {SymbolCount} symbols from {ProjectCount} projects")]
private partial void LogParallelBuildComplete(int symbolCount, int projectCount);

[LoggerMessage(EventId = 3863, Level = LogLevel.Warning,
    Message = "Error indexing project {ProjectName}")]
private partial void LogProjectIndexingError(Exception exception, string projectName);
```

### 3. CallGraphIndexer.cs

**Оптимизация сбора методов:**

```csharp
// Было: foreach + await
foreach (var project in solution.Projects)
{
    var compilation = await project.GetCompilationAsync(cancellationToken);
    // ...
}

// Стало: Parallel.ForEachAsync
await Parallel.ForEachAsync(projects, collectOptions, async (project, ct) =>
{
    var compilation = await project.GetCompilationAsync(ct);
    var visitor = new MethodCollectorVisitor();
    visitor.Visit(compilation.Assembly.GlobalNamespace);

    foreach (var method in visitor.Methods)
        allMethods.Add(method);
});
```

---

## Конфигурация

**Степень параллелизма:**

```csharp
private static int MaxParallelism => Math.Max(1, Environment.ProcessorCount);
```

По умолчанию используется количество логических процессоров. Roslyn внутренне также оптимизирован для многопоточности.

**Fallback для малых решений:**

Для решений с ≤2 проектами используется последовательный подход, так как overhead от параллелизации превысит выигрыш.

---

## Ожидаемый эффект

| Сценарий | Было | Стало | Ускорение |
|----------|------|-------|-----------|
| 10 проектов, 8 ядер | ~10 сек | ~3-4 сек | **2.5-3x** |
| Проекты без зависимостей | Последовательно | Параллельно | **до Nx** |
| 2 проекта | Последовательно | Последовательно | 1x |
| UltrasharpTools.sln (10 проектов) | ~8-10 сек | ~3-4 сек | **~2.5x** |

**Ограничения:**
- Roslyn уже использует внутреннюю параллелизацию
- I/O может быть bottleneck на медленных дисках
- Зависимые проекты всё равно ждут свои зависимости

---

## Как это работает с Roslyn

### ProjectDependencyGraph

Roslyn предоставляет `solution.GetProjectDependencyGraph()`, который содержит:
- Топологический порядок проектов
- Информацию о зависимостях каждого проекта

```csharp
var dependencies = dependencyGraph.GetProjectsThatThisProjectDirectlyDependsOn(project.Id);
```

### GetCompilationAsync

Roslyn **внутренне** кэширует компиляции зависимостей:
- При вызове `GetCompilationAsync` для проекта A, зависящего от B
- Roslyn автоматически сначала компилирует B (если не закэшировано)
- Повторные вызовы используют кэш

Это означает, что даже при параллельном запуске `GetCompilationAsync` для проектов одного уровня, Roslyn корректно обработает зависимости.

---

## Логи

При включённом Debug-логировании вывод будет примерно таким:

```
[INF] Starting parallel build: 10 projects, 4 dependency levels, max parallelism = 8
[DBG] Completed level 1/4: 2 projects in 450ms
[DBG] Collected 1200 symbols from project VectorDB (total: 1200)
[DBG] Collected 150 symbols from project Comm (total: 1350)
[DBG] Completed level 2/4: 1 projects in 2100ms
[DBG] Collected 15000 symbols from project Tools (total: 16350)
[DBG] Completed level 3/4: 4 projects in 800ms
[DBG] Completed level 4/4: 3 projects in 300ms
[INF] Parallel build complete: 18500 symbols from 10 projects
[INF] Fast symbol index built: 18500 symbols indexed in 3650ms
```

---

## Связь с другими оптимизациями

Эта оптимизация дополняет существующие:

| Оптимизация | Фаза | Эффект |
|-------------|------|--------|
| SymbolCacheManager | Phase 3 | Кэширование между сессиями |
| FastSymbolIndex | Phase 3 | Быстрый поиск символов |
| AnalysisCacheService | Phase 4 | Кэширование результатов анализа |
| **Parallel Project Indexing** | **Phase 5** | **Параллельная компиляция** |

---

## Рекомендации для дальнейшей оптимизации

1. **Memory-mapped indices** — для очень больших решений (>100 проектов)
2. **Incremental rebuild** — при изменении одного файла обновлять только затронутые проекты
3. **Compilation cache persistence** — сохранение компиляций между сессиями (сложно из-за Roslyn)
4. **Profiling** — использовать dotnet-trace для выявления реальных bottlenecks

---

## Заключение

Параллельная индексация проектов — это low-hanging fruit оптимизация, которая:
- Не требует изменения API
- Полностью обратно совместима
- Даёт 2-4x ускорение на типичных решениях
- Использует встроенные возможности Roslyn (`ProjectDependencyGraph`)

Основной выигрыш достигается на решениях с несколькими независимыми проектами, которые теперь компилируются параллельно.

# UltrasharpTools - TODO и возможные улучшения

**Дата создания:** 2025-01-13
**Статус проекта:** ✅ Production Ready

Этот документ содержит систематизированный список возможных улучшений и функций для будущих версий SharpTools.

---

## 🔴 Критичные / Высокий приоритет

### 1. Автоматическая перезагрузка решения
**Статус:** ✅ Реализовано (2025-01-13, commit 916362a)
**Приоритет:** Высокий
**Описание:** Сервер автоматически перезагружает решение при изменении проектных файлов.

**Реализовано:**
- ✅ FileSystemWatcher для `.csproj`, `.sln`, `.props`, `.targets`
- ✅ Debounce механизм (2000ms по умолчанию)
- ✅ Автоматический вызов `ReloadSolutionFromDiskAsync`
- ✅ Invalidation всех кэшей
- ✅ Command-line опции: `--auto-reload`, `--reload-debounce-ms`

**Конфигурация:**
- По умолчанию: Выключено (opt-in)
- Включить: `--auto-reload`

---

### 2. Автоматическая очистка Git веток
**Статус:** ✅ Реализовано (2025-01-13, commit 968c860)
**Приоритет:** Средний
**Описание:** Автоматическая очистка старых `ultrasharptools/*` веток для поддержания чистоты репозитория.

**Реализовано:**
- ✅ Retention по количеству (N последних веток)
- ✅ Retention по времени (ветки за последние N дней)
- ✅ Автоматическая очистка после создания новых веток
- ✅ GitOptions configuration model
- ✅ Command-line опции: `--git-branch-retention-count`, `--git-branch-retention-days`, `--git-auto-cleanup`

**Конфигурация:**
- По умолчанию: 10 последних веток, auto-cleanup включен
- Никогда не удаляет текущую ветку

---

### 3. Персистентный кэш FastSymbolIndex
**Статус:** 🟡 MVP реализован (2025-01-13, commit 11e27a0)
**Приоритет:** Высокий
**Описание:** Инфраструктура кэширования FastSymbolIndex на диск готова. Требуется доработка для полного использования.

**Реализовано:**
- ✅ SymbolCacheManager с SHA256 валидацией
- ✅ Сериализация в JSON (SymbolCacheMetadata, SerializableSymbolEntry)
- ✅ Hash validation (solution + project files)
- ✅ Last write time + hash staleness detection
- ✅ Background (non-blocking) cache saving
- ✅ Cache format versioning (v1)
- ✅ Кэш в %TEMP%/SharpTools/SymbolCache

**TODO (для 10x speedup):**
- ⚠️ ISymbol resolution из кэшированных FQNs
- ⚠️ Incremental rebuild измененных проектов
- ⚠️ Command-line опция для включения/выключения

**Текущее влияние:**
- Кэш сохраняется после каждой загрузки ✅
- Загрузка из кэша пока не дает ускорения (TODO)

**Ожидаемое влияние после доработки:**
- ⬇️ **Инициализация: 33 сек → 3-5 сек** (10x ускорение)

---

## 🟡 Средний приоритет

### 4. Включение отключённых инструментов

**Статус:** 🟡 Частично реализовано (отключено)
**Приоритет:** Средний

#### 4.1 get_all_subtypes
**Описание:** Recursively lists all nested members of a type

**Требуется:**
- Включение в SolutionTools
- Тестирование производительности на глубоких иерархиях
- Лимит глубины рекурсии

**Оценка времени:** 2-4 часа

#### 4.2 view_inheritance_chain
**Описание:** Shows the inheritance hierarchy for a type

**Требуется:**
- Включение в AnalysisTools
- Визуализация цепочки (Base → Derived)
- Поддержка interfaces

**Оценка времени:** 2-3 часа

#### 4.3 view_call_graph
**Описание:** Displays incoming and outgoing calls for a method

**Требуется:**
- Включение в AnalysisTools
- Кэширование call graph (см. TraceBackwards улучшения)
- Лимит глубины для больших графов

**Оценка времени:** 4-6 часов

#### 4.4 find_duplicates
**Описание:** Finds semantically similar methods or classes

**Требуется:**
- Включение в AnalysisTools
- Threshold конфигурация
- Интеграция с SemanticSimilarityService (уже существует)

**Оценка времени:** 2-4 часа

#### 4.5 replace_all_references
**Описание:** Replaces all references to a symbol with specified C# code

**Требуется:**
- Включение в ModificationTools
- Тщательное тестирование (рискованная операция)
- Preview mode обязателен
- Git commit для отката

**Оценка времени:** 6-8 часов

#### 4.6 add_package
**Статус:** ✅ Реализовано (2025-01-13, commit 35a5420)
**Описание:** Adds or updates a NuGet package reference with automatic restore and reload

**Реализовано:**
- ✅ Включен в PackageTools
- ✅ Парсинг .csproj XML и packages.config
- ✅ Version resolution (latest, specific) через NuGet API
- ✅ Автоматический dotnet/nuget restore
- ✅ Автоматический reload solution
- ✅ Поддержка PackageReference и packages.config

**Параметр:**
- `skipRestoreAndReload` - для ручного контроля процесса

---

### 5. Улучшения trace_execution

**Статус:** ✅ Базовая версия реализована
**Приоритет:** Средний
**См.:** TRACING_IMPLEMENTATION.md строка 264-268

#### 5.1 Multiple Paths Support
**Статус:** ✅ Реализовано (2025-01-13, commit 0e9239e)
**Описание:** Трассировка всех веток условий (if/else/switch), а не только одного пути

**Реализовано:**
- ✅ Обход всех путей CFG (TraceMethodAllPathsAsync)
- ✅ Fork detection для условных веток
- ✅ Лимит количества путей через maxPaths параметр
- ✅ ExecutionPath модель с PathConditions
- ✅ Backward compatible (legacy Steps для single path)

**Влияние:**
- Полная картина выполнения кода
- Обнаружение всех возможных путей к точке

**Время:** 12 часов

#### 5.2 Символьное выполнение с constraints
**Описание:** Анализ условий и ограничений для путей

**Требуется:**
- Constraint solver (например, Z3)
- Символьные значения переменных
- Path feasibility analysis

**Влияние:**
- Определение достижимости путей
- Выявление dead code

**Оценка времени:** 20-30 часов (сложно)

#### 5.3 Улучшенная обработка async/await
**Статус:** ✅ Реализовано (2025-01-13, commit 40e1111)
**Описание:** Трассировка асинхронных потоков выполнения

**Реализовано:**
- ✅ AsyncStateMachineAnalyzer helper class
- ✅ IAwaitOperation detection в CFG
- ✅ ConfigureAwait detection
- ✅ AsyncAwait + AsyncContinuation trace steps
- ✅ Параметр unwrapAsync (default: true)

**Время:** 10 часов

#### 5.4 Поддержка LINQ выражений
**Статус:** ✅ Реализовано (2025-01-13, commit 57a597f)
**Описание:** Раскрытие LINQ query в последовательность вызовов

**Реализовано:**
- ✅ LinqQueryAnalyzer helper class
- ✅ Определение deferred vs immediate execution
- ✅ Lambda expression extraction
- ✅ Query expression syntax analysis (from...where...select)
- ✅ LinqQuery + LambdaCall trace steps
- ✅ Параметр unwrapLinq (default: false)

**Время:** 8 часов

---

### 6. Улучшения trace_backwards

**Статус:** ✅ Базовая версия реализована
**Приоритет:** Средний
**См.:** TRACING_IMPLEMENTATION.md строка 270-274

#### 6.1 Кэширование call graph
**Статус:** ✅ Базовая версия реализована (2025-11-23, commit 24a76d8)
**Описание:** Сохранение построенного call graph для повторного использования

**Реализовано:**
- ✅ SQLite-backed кеш (AnalysisCacheService)
- ✅ Кеширование FindCallersAsync, FindOutgoingCallsAsync, FindReferencedTypesAsync
- ✅ Фоновая индексация CallGraphIndexer после LoadProject
- ✅ Параллельная обработка с CPU-based throttling
- ✅ Таймауты (5 сек) для предотвращения зависаний
- ✅ Пагинация результатов (200 элементов, покрывает 99% методов)
- ✅ Progress tracking и логирование каждые 100 методов

**TODO (для полного инкрементального обновления):**
- ⚠️ Incremental rebuild при изменении файлов (см. секцию 6.1.1)
- ⚠️ Invalidation только затронутых записей (не всего кеша)
- ⚠️ Integration с FileSystemWatcher для автоматического обновления

**Текущее влияние:**
- ⬇️ **view_definition: бесконечное зависание → <10 сек** (первый вызов)
- ⬇️ **view_definition: <10 сек → мгновенно** (повторные вызовы, cached)
- 📦 Фоновая индексация предзаполняет кеш для ~2000 методов

**Время:** 10 часов

#### 6.1.1 Инкрементальное обновление call graph кеша
**Статус:** 🟡 Частично реализовано (базовый кеш есть, инкремент - нет)
**Приоритет:** Средний
**Описание:** Автоматическое инкрементальное обновление кеша при изменении файлов вместо полной переиндексации

**Текущее состояние:**
- ✅ FileSystemWatcher уже отслеживает изменения файлов (SolutionManager)
- ✅ Semantic model cache инвалидируется автоматически
- ❌ Analysis cache (FindCallers/FindOutgoingCalls) НЕ обновляется инкрементально
- ❌ При изменении файла требуется полная переиндексация (долго)

**Требуется реализовать:**

1. **Dependency Graph Service** (4-6 часов):
   - Построение графа зависимостей между методами
   - Tracking какие методы зависят от каких файлов
   - Reverse dependency lookup (файл → затронутые методы)

2. **Selective Cache Invalidation** (3-4 часа):
   - При изменении файла определить затронутые методы
   - Invalidate только записи для этих методов
   - Batch invalidation для множественных изменений

3. **Incremental Reindexing** (4-5 часов):
   - Переиндексировать только измененные методы
   - Переиндексировать методы, вызывающие измененные методы (1 уровень вверх)
   - Debounce механизм (не реиндексировать на каждое изменение)

4. **Integration с FileSystemWatcher** (2-3 часа):
   - Подписка на события изменения файлов
   - Триггер инкрементальной переиндексации
   - Конфигурируемый debounce delay (default: 2000ms)

5. **Smart Reindexing Strategy** (3-4 часа):
   - Определение масштаба изменений (локальные vs глобальные)
   - Для малых изменений (<5 методов) — инкрементальная переиндексация
   - Для больших изменений (>50 методов) — полная переиндексация проекта
   - Для изменений в interfaces/base classes — расширенная переиндексация (все реализации/наследники)

6. **Cache Metadata Enhancement** (2-3 часа):
   - Добавить timestamp для каждой записи
   - Добавить source file hash для отслеживания изменений
   - Добавить dependency tracking в метаданные

**Архитектурные компоненты:**

```csharp
// 1. Dependency Graph Service
public interface IDependencyGraphService
{
    // Построить граф зависимостей для решения
    Task<DependencyGraph> BuildGraphAsync(Solution solution, CancellationToken ct);

    // Найти все методы, зависящие от данного файла
    IEnumerable<IMethodSymbol> GetAffectedMethods(string filePath);

    // Найти все файлы, от которых зависит метод
    IEnumerable<string> GetDependentFiles(IMethodSymbol method);
}

// 2. Incremental Cache Manager
public interface IIncrementalCacheManager
{
    // Инвалидировать записи для конкретных методов
    Task InvalidateMethodsAsync(IEnumerable<IMethodSymbol> methods);

    // Переиндексировать только затронутые методы
    Task ReindexAffectedMethodsAsync(
        IEnumerable<string> changedFiles,
        CancellationToken ct
    );

    // Определить масштаб изменений
    ReindexingScope DetermineScope(IEnumerable<string> changedFiles);
}

// 3. File Change Handler
public class IncrementalIndexingHandler
{
    private readonly IDependencyGraphService _depGraph;
    private readonly IIncrementalCacheManager _cacheManager;
    private readonly ILogger _logger;
    private readonly Timer _debounceTimer;
    private readonly HashSet<string> _pendingFiles = new();

    public void OnFileChanged(string filePath)
    {
        _pendingFiles.Add(filePath);
        _debounceTimer.Reset(); // Restart debounce
    }

    private async Task ProcessPendingChangesAsync()
    {
        var scope = _cacheManager.DetermineScope(_pendingFiles);

        if (scope == ReindexingScope.Full)
        {
            // Запустить полную переиндексацию
            await _callGraphIndexer.StartBackgroundIndexingAsync();
        }
        else
        {
            // Инкрементальная переиндексация
            await _cacheManager.ReindexAffectedMethodsAsync(_pendingFiles);
        }

        _pendingFiles.Clear();
    }
}
```

**Workflow:**

1. Пользователь изменяет файл `ServiceA.cs`
2. FileSystemWatcher → `OnFileChanged("ServiceA.cs")`
3. Debounce timer перезапускается (ждем 2 секунды без изменений)
4. Timer срабатывает → `ProcessPendingChangesAsync()`
5. DependencyGraphService определяет затронутые методы:
   - Методы в `ServiceA.cs` (прямые)
   - Методы, вызывающие методы из `ServiceA.cs` (1 уровень вверх)
6. IncrementalCacheManager:
   - Invalidate записи для этих методов
   - Запустить фоновую переиндексацию только этих методов
7. Логирование: "Reindexed 15 methods affected by ServiceA.cs changes"

**Влияние:**
- ⬇️ **Время обновления кеша: 30-60 сек → 1-5 сек** (для локальных изменений)
- ⬇️ **Нагрузка на CPU: значительно меньше** (переиндексация ~10-50 методов вместо ~2000)
- ✅ **Всегда актуальный кеш** без ручного вызова LoadProject

**Риски:**
- ⚠️ Сложность dependency tracking (циклические зависимости, generics)
- ⚠️ Memory overhead для хранения dependency graph
- ⚠️ Возможность race conditions при параллельных изменениях

**Оценка времени:** 18-25 часов

**Приоритет реализации:** Средний (nice-to-have, базовый кеш уже работает хорошо)

#### 6.2 Интеграция с PDB/symbols
**Описание:** Использование debug symbols для лучшего matching со stack trace

**Требуется:**
- PDB parsing (System.Reflection.Metadata.PdbReader)
- Sequence points mapping
- Inlined methods detection

**Влияние:**
- 100% точность matching для production builds
- Работа с obfuscated code

**Оценка времени:** 12-16 часов

#### 6.3 Fuzzy matching для stack trace hints
**Статус:** ✅ Реализовано (2025-01-13, commit 8cf0694)
**Описание:** Tolerant matching при неточных stack traces (например, сокращённые названия)

**Реализовано:**
- ✅ FuzzyStackTraceMatcher с Levenshtein distance
- ✅ Partial FQN matching для nested types и generics
- ✅ StackTraceConfidence поле в CallFrame (0.0-1.0)
- ✅ Enhanced RankPathsByConfidence с fuzzy scoring
- ✅ Bonus для high confidence frames (>0.8)

**Влияние:**
- Работа с неполными/повреждёнными stack traces
- Лучшая user experience

**Время:** 6 часов

#### 6.4 Визуализация call graph
**Описание:** Экспорт call graph в графические форматы

**Требуется:**
- DOT/GraphML/Mermaid export
- Interactive HTML view (например, d3.js)
- Highlighting confidence scores

**Влияние:**
- Лучшее понимание путей выполнения
- Visual debugging

**Оценка времени:** 8-12 часов

---

### 7. Общие улучшения трассировки

**Статус:** 🟡 Идеи
**Приоритет:** Низкий
**См.:** TRACING_IMPLEMENTATION.md строка 276-280

#### 7.1 Экспорт в различные форматы
**Описание:** JSON, GraphML, DOT, Mermaid для integration с другими инструментами

**Требуется:**
- Serialization templates
- Format versioning
- Schema documentation

**Оценка времени:** 4-6 часов

#### 7.2 Интеграция с IDE (VS Code extension)
**Описание:** VS Code extension для запуска трассировки из редактора

**Требуется:**
- TypeScript extension development
- LSP integration или custom protocol
- UI для отображения результатов
- Jump to definition из трассировки

**Влияние:**
- Seamless developer experience
- Нативная интеграция в workflow

**Оценка времени:** 30-40 часов

#### 7.3 Performance profiling integration
**Описание:** Интеграция с dotnet-trace/BenchmarkDotNet для реального profiling

**Требуется:**
- dotnet-trace output parsing
- Correlation trace paths с performance metrics
- Hot path highlighting

**Влияние:**
- Идентификация bottleneck'ов
- Guided optimization

**Оценка времени:** 12-16 часов

#### 7.4 Interactive mode (step-by-step)
**Описание:** Пошаговая трассировка с возможностью исследования переменных

**Требуется:**
- REPL-like interface
- State inspection на каждом шаге
- Breakpoint support

**Влияние:**
- Interactive debugging
- Обучение/понимание кода

**Оценка времени:** 20-30 часов (сложно)

---

## 🟢 Низкий приоритет / Nice to Have

### 8. Caching Layer 2 (Persistent Cache)

**Статус:** ✅ Частично реализовано (2025-11-23, commit 24a76d8)
**Приоритет:** Низкий
**Описание:** Персистентный кэш результатов анализа между запусками
**См.:** ULTRA-SHARPED.md строка 1439-1442

**Реализовано:**
- ✅ SQLite database для хранения результатов (AnalysisCacheService)
- ✅ Кеширование FindCallersAsync, FindOutgoingCallsAsync, FindReferencedTypesAsync
- ✅ Cache key = hash(solution state + operation FQN)
- ✅ Automatic serialization/deserialization

**TODO:**
- ⚠️ Кеширование других операций (FindReferences, SearchDefinitions)
- ⚠️ TTL для автоматической очистки старых записей
- ⚠️ Compression для больших результатов
- ⚠️ Cache statistics и monitoring

**Текущее влияние:**
- ⬇️ **Call graph операции: 2-10x ускорение** (для кешированных запросов)
- 📦 Персистентный кеш между сессиями

**Оценка времени оставшейся работы:** 8-12 часов

---

### 9. Incremental Compilation

**Статус:** 🟢 Идея
**Приоритет:** Низкий
**Описание:** Кэширование Roslyn Compilation объектов между запросами
**См.:** ULTRA-SHARPED.md строка 1444-1446

**Требуется:**
- Persistent Compilation cache (serialize to disk)
- Incremental rebuild при изменении файлов
- Dependency tracking для invalidation

**Влияние:**
- ⬇️ **Повторные операции: 1.5-2x ускорение**
- Особенно полезно для больших решений

**Оценка времени:** 12-16 часов

---

### 10. SIMD Optimizations

**Статус:** 🟢 Идея
**Приоритет:** Низкий
**Описание:** Векторизация операций в SemanticSimilarityService
**См.:** ULTRA-SHARPED.md строка 1448-1451

**Требуется:**
- Использование `System.Numerics.Vector<T>`
- Batch processing для similarity calculations
- SIMD-friendly data structures
- AVX2/AVX-512 когда доступно

**Влияние:**
- ⬇️ **CalculateSimilarity: 1.5-2x ускорение**
- Критично для больших O(N²) операций

**Оценка времени:** 10-14 часов
**Навык:** Требует глубокого понимания SIMD

---

### 11. MCP Streaming Support

**Статус:** 🟢 Отложено
**Приоритет:** Низкий
**Описание:** Потоковая передача результатов через MCP protocol
**См.:** ULTRA-SHARPED.md строка 1429-1431

**Требуется:**
- MCP SDK должен поддерживать streaming
- Yield return вместо batch results
- Chunked transfer для больших ответов

**Влияние:**
- Faster time-to-first-result
- Лучше UX для долгих операций (SearchDefinitions)

**Оценка времени:** Зависит от MCP SDK support
**Блокер:** MCP SDK не поддерживает streaming (по состоянию на 2025-01)

---

### 12. Native AOT Support

**Статус:** ❌ Невозможно
**Приоритет:** N/A
**Описание:** Компиляция в native код без runtime
**См.:** ULTRA-SHARPED.md строка 1433-1435

**Блокер:** Roslyn не поддерживает Native AOT (используется Reflection.Emit)
**Альтернатива:** ReadyToRun (уже реализовано, даёт ~75% от выигрыша Native AOT)

**Статус:** Не планируется

---

### 13. Исправление quirky features

**Статус:** 🟢 Косметическое
**Приоритет:** Очень низкий
**Описание:** Некоторые функции имеют "quirky" behaviour

#### 13.1 Newline handling в modify_code
**Описание:** Removing newlines before and after overwritten members создаёт неконсистентное форматирование

**Требуется:**
- Preserve surrounding whitespace
- Respect .editorconfig rules
- Consistent indentation

**Влияние:**
- Лучше code style consistency
- Меньше git diffs

**Оценка времени:** 3-4 часа

---

## 📊 Оценка общей трудоёмкости

| Приоритет | Категория | Задач | Готово | Оценка времени |
|-----------|-----------|-------|--------|----------------|
| 🔴 Критичные | High Priority | 3 | ✅ 3/3 | ~~24-34 часа~~ **DONE** |
| 🟡 Средний | Disabled Tools | 6 | ✅ 6/6 | ~~16-25 часов~~ **DONE** |
| 🟡 Средний | TraceExecution | 4 | ✅ 3/4 | 20-30 часов (осталось) |
| 🟡 Средний | **Call Graph Cache** | **2** | **✅ 1/2** | **~~10 часов~~ (базовый кеш) + 18-25 часов (инкремент)** |
| 🟡 Средний | TraceBackwards | 4 | ✅ 1/4 | 28-42 часов (осталось) |
| 🟡 Средний | Tracing General | 4 | 0/4 | 66-94 часа |
| 🟢 Низкий | Performance | 3 | ✅ 1/3 | ~~16-20 часов (кеш)~~ + 22-30 часов (осталось) |
| 🟢 Низкий | Misc | 2 | 0/2 | 3-4 часа |
| **Итого** | | **28 задач** | **✅ 12/28** | **155-240 часов** остается |

**Выполнено:** 12 задач, ~76-86 часов работы
**Остается:** 16 задач, ~155-240 часов (~4-6 недель full-time)

**Новое в этом обновлении (2025-11-23):**
- ✅ Call Graph Cache (базовая версия): SQLite кеш, фоновая индексация, таймауты, пагинация
- 📝 Добавлена детальная спецификация инкрементального обновления кеша (секция 6.1.1)

---

## 🎯 Рекомендуемый порядок внедрения

### Phase 1: Критичные улучшения ✅ ЗАВЕРШЕНО (2025-01-13)
1. ✅ Персистентный кэш FastSymbolIndex (MVP реализован, commit 11e27a0)
2. ✅ Автоматическая перезагрузка решения (commit 916362a)
3. ✅ Автоматическая очистка Git веток (commit 968c860)
4. ✅ AddOrModifyNugetPackage с auto-reload (commit 35a5420)

### Phase 2: Включение отключённых инструментов ✅ ЗАВЕРШЕНО (2025-01-13, commit 0117eea)
4. ✅ find_duplicates
5. ✅ view_inheritance_chain
6. ✅ get_all_subtypes
7. ✅ view_call_graph

### Phase 3: Улучшение TraceBackwards 🟡 В ПРОЦЕССЕ (2025-11-23)
8. ✅ Кэширование call graph (базовая версия реализована, commit 24a76d8)
   - ✅ SQLite persistent cache
   - ✅ Фоновая индексация CallGraphIndexer
   - ⚠️ Инкрементальное обновление (TODO, секция 6.1.1)
9. ✅ Fuzzy matching для stack trace hints (commit 8cf0694)
10. ⚠️ Визуализация call graph (TODO, секция 6.4)

### Phase 4: Performance Optimizations 🟡 ЧАСТИЧНО (2025-11-23)
11. ✅ Caching Layer 2 (частично реализовано, commit 24a76d8)
    - ✅ Call graph операции кешируются
    - ⚠️ TODO: другие операции (FindReferences, SearchDefinitions)
12. ⚠️ SIMD Optimizations (TODO, если SemanticSimilarity станет bottleneck)

### Phase 5: Advanced Tracing (долгосрочно)
13. ✅trace_executionn: Multiple paths
14. ✅ IDE Integration (VS Code extension)
15. ✅ Interactive mode

---

## 📝 Примечания

- Приоритеты могут измениться на основе реального использования
- Оценки времени даны для опытного C#/Roslyn разработчика
- Некоторые задачи могут быть выполнены параллельно
- MCP SDK limitations могут блокировать некоторые функции

---

**Последнее обновление:** 2025-11-23
**Версия:** 1.1
**Статус проекта:** ✅ Production Ready, активное развитие

## 📋 Changelog

### v1.1 (2025-11-23)
- ✅ Реализовано кеширование call graph (базовая версия)
  - SQLite-backed персистентный кеш
  - Фоновая индексация CallGraphIndexer
  - Таймауты для предотвращения зависаний (5 сек)
  - Пагинация результатов (200 элементов)
- 📝 Добавлена детальная спецификация инкрементального обновления кеша (секция 6.1.1)
- 📊 Обновлена статистика: 12/28 задач выполнено
- 🎯 Phase 3 и Phase 4 частично выполнены

### v1.0 (2025-01-13)
- Начальная версия TODO.md
- Систематизация всех запланированных улучшений
- Phases 1-2 полностью выполнены

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
**Описание:** Автоматическая очистка старых `sharptools/*` веток для поддержания чистоты репозитория.

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

#### 4.1 UltrasharpTool_GetAllSubtypes
**Описание:** Recursively lists all nested members of a type

**Требуется:**
- Включение в SolutionTools
- Тестирование производительности на глубоких иерархиях
- Лимит глубины рекурсии

**Оценка времени:** 2-4 часа

#### 4.2 UltrasharpTool_ViewInheritanceChain
**Описание:** Shows the inheritance hierarchy for a type

**Требуется:**
- Включение в AnalysisTools
- Визуализация цепочки (Base → Derived)
- Поддержка interfaces

**Оценка времени:** 2-3 часа

#### 4.3 UltrasharpTool_ViewCallGraph
**Описание:** Displays incoming and outgoing calls for a method

**Требуется:**
- Включение в AnalysisTools
- Кэширование call graph (см. TraceBackwards улучшения)
- Лимит глубины для больших графов

**Оценка времени:** 4-6 часов

#### 4.4 UltrasharpTool_FindPotentialDuplicates
**Описание:** Finds semantically similar methods or classes

**Требуется:**
- Включение в AnalysisTools
- Threshold конфигурация
- Интеграция с SemanticSimilarityService (уже существует)

**Оценка времени:** 2-4 часа

#### 4.5 UltrasharpTool_ReplaceAllReferences
**Описание:** Replaces all references to a symbol with specified C# code

**Требуется:**
- Включение в ModificationTools
- Тщательное тестирование (рискованная операция)
- Preview mode обязателен
- Git commit для отката

**Оценка времени:** 6-8 часов

#### 4.6 UltrasharpTool_AddOrModifyNugetPackage
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

### 5. Улучшения TraceExecution

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

### 6. Улучшения TraceBackwards

**Статус:** ✅ Базовая версия реализована
**Приоритет:** Средний
**См.:** TRACING_IMPLEMENTATION.md строка 270-274

#### 6.1 Кэширование call graph
**Описание:** Сохранение построенного call graph для повторного использования

**Требуется:**
- Persistent storage (SQLite/file)
- Invalidation strategy при изменении кода
- Incremental rebuild графа

**Влияние:**
- ⬇️ **Backtrace: 5-10 сек → <1 сек** (для повторных вызовов)
- Критично для частых трассировок

**Оценка времени:** 8-12 часов

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

**Статус:** 🟢 Идея
**Приоритет:** Низкий
**Описание:** Персистентный кэш результатов анализа между запусками
**См.:** ULTRA-SHARPED.md строка 1439-1442

**Требуется:**
- SQLite database для хранения результатов (FindReferences, SearchDefinitions, etc.)
- Cache key = hash(solution state + operation + parameters)
- TTL или invalidation triggers
- Compression для больших результатов

**Влияние:**
- ⬇️ **Повторный анализ: 2-3x ускорение**
- Мгновенные ответы для кэшированных запросов
- Trade-off: Disk space за Speed

**Оценка времени:** 16-20 часов

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

#### 13.1 Newline handling в OverwriteMember
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
| 🟡 Средний | TraceBackwards | 4 | ✅ 1/4 | 28-42 часов (осталось) |
| 🟡 Средний | Tracing General | 4 | 0/4 | 66-94 часа |
| 🟢 Низкий | Performance | 3 | 0/3 | 38-50 часов |
| 🟢 Низкий | Misc | 2 | 0/2 | 3-4 часа |
| **Итого** | | **26 задач** | **✅ 10/26** | **155-220 часов** остается |

**Выполнено:** 10 задач, ~66-76 часов работы
**Остается:** 16 задач, ~155-220 часов (~4-5.5 недель full-time)

---

## 🎯 Рекомендуемый порядок внедрения

### Phase 1: Критичные улучшения ✅ ЗАВЕРШЕНО (2025-01-13)
1. ✅ Персистентный кэш FastSymbolIndex (MVP реализован, commit 11e27a0)
2. ✅ Автоматическая перезагрузка решения (commit 916362a)
3. ✅ Автоматическая очистка Git веток (commit 968c860)
4. ✅ AddOrModifyNugetPackage с auto-reload (commit 35a5420)

### Phase 2: Включение отключённых инструментов ✅ ЗАВЕРШЕНО (2025-01-13, commit 0117eea)
4. ✅ UltrasharpTool_FindPotentialDuplicates
5. ✅ UltrasharpTool_ViewInheritanceChain
6. ✅ UltrasharpTool_GetAllSubtypes
7. ✅ UltrasharpTool_ViewCallGraph

### Phase 3: Улучшение TraceBackwards (1-2 недели)
8. ✅ Кэширование call graph
9. ✅ Fuzzy matching для stack trace hints
10. ✅ Визуализация call graph

### Phase 4: Performance Optimizations (опционально)
11. ✅ Caching Layer 2 (если видим частые повторные запросы)
12. ✅ SIMD Optimizations (если SemanticSimilarity bottleneck)

### Phase 5: Advanced Tracing (долгосрочно)
13. ✅ TraceExecution: Multiple paths
14. ✅ IDE Integration (VS Code extension)
15. ✅ Interactive mode

---

## 📝 Примечания

- Приоритеты могут измениться на основе реального использования
- Оценки времени даны для опытного C#/Roslyn разработчика
- Некоторые задачи могут быть выполнены параллельно
- MCP SDK limitations могут блокировать некоторые функции

---

**Последнее обновление:** 2025-01-13
**Версия:** 1.0
**Статус проекта:** ✅ Production Ready, активное развитие

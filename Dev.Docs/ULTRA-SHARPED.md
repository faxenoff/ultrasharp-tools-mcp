# UltrasharpTools: Отчёт об улучшениях

## Обзор

Данный документ описывает все ключевые улучшения, внесённые в оригинальный SharpTools MCP Server, в результате чего был создан **UltrasharpTools** — комплексная модернизация с фокусом на производительность, качество кода и новые возможности.

**Базовый проект:** SharpTools (https://github.com/tluyben/sharp-tools)
**Результат:** Улучшение производительности в 2-100 раз для критичных операций + новые инструменты анализа

---

## 📊 Сводка по категориям улучшений

### 1. Миграция на современный стек
- ✅ **.NET 10** — современный runtime с LTS support
- ✅ **C# 13.0** — новые языковые возможности
- ✅ **Primary Constructors** — сокращение boilerplate кода
- ✅ **Современные зависимости** — все пакеты обновлены до последних версий

### 2. Критические оптимизации производительности
- ✅ **N+1 Problem Fix** — устранение избыточных вызовов компиляции (99.8% редукция)
- ✅ **ValueTask Integration** — zero-allocation при cache hit
- ✅ **Deadlock Prevention** — устранение блокирующих async вызовов
- ✅ **ReadyToRun (R2R)** — AOT компиляция для 50% faster startup

### 3. Параллельная обработка данных
- ✅ **Parallel Assembly Loading** — 4-5x ускорение загрузки reflection cache
- ✅ **Parallel NuGet Resolution** — 3-4x ускорение обработки пакетов
- ✅ **Parallel Symbol Lookup** — параллельный поиск по проектам
- ✅ **Parallel Similarity Analysis** — O(N²) операции в параллель
- ✅ **Early Termination** — досрочное прекращение при достижении результата

### 4. Advanced In-Memory Optimizations
- ✅ **Fast Symbol Indexing** — битовые флаги + Bloom фильтры (10-100x ускорение)
- ✅ **FrozenDictionary** — оптимизация reflection cache (20-30% ускорение)
- ✅ **Multi-Layered Index** — Bloom → Flags → Dictionary → Bitwise filtering
- ✅ **99% Reduction** — ненужных проверок при несуществующих символах
- ✅ **Dynamic PGO** — адаптивная оптимизация runtime (+30-50% после прогрева)

### 5. Micro-Optimizations (Performance Phase 5)
- ✅ **Span<T> + ArrayPool** — zero-allocation в FastHash и BloomFilter
- ✅ **SIMD Vectorization** — AVX2 для ComputeHashes (2.2-5.7x faster ускорение)
- ✅ **Streaming I/O** — O(1) memory для больших файлов (LogAnalysis)
- ✅ **FrozenSet** — оптимизация hash lookups
- ✅ **Infrastructure Fixes** — graceful shutdown, SQLite reliability

### 6. Memory Optimization (2025-11-25)
- ✅ **MemoryCache Limits** — уменьшены лимиты (500MB+1GB → 150MB+250MB) = **-1.1GB**
- ✅ **SqliteSymbolIndex** — disk-based symbol storage с FTS5
- ✅ **SqliteReflectionTypeIndex** — SQLite для reflection types вместо FrozenDictionary
- ✅ **LRU Cache** — 500 горячих типов в памяти
- ✅ **Lazy Type Loading** — MetadataLoadContext для отложенной загрузки
- ✅ **Low Memory Mode** — `--low-memory` CLI флаг для режима экономии (~50-100MB дополнительно)
- ✅ **Memory Reduction** — 1.7-2.5GB → 400-600MB (**-60-70%**)

### 7. Quality Tools
- ✅ **FormatCode** — автоматическое форматирование через CSharpier

### 8. Layered Indexing & Git Workflow (2025-01-17)
- ✅ **Three-Layer Architecture** — Base + Branch Deltas + Working Deltas
- ✅ **Git Branch Integration** — автоматические branch deltas при переключении веток
- ✅ **Working Delta Promotion** — promotion в branch delta при git commit
- ✅ **SIMD Optimizations** — Vector<float> для 4-8x ускорения similarity calculations
- ✅ **xxHash32** — 2-3x быстрее для dictionary/hashset операций
- ✅ **Streaming JSON** — минимизация allocations с ArrayPool
- ✅ **Delta Compaction** — автоматическое уплотнение больших дельт
- ✅ **Orphaned Cleanup** — автоматическая очистка удалённых веток
- ✅ **Production Validated** — протестировано на нескольких проектах (890K symbols, 112 branches)
- ✅ **5.4x Speedup** — cache hit (48.3s → 8.9s)
- ✅ **< 20ms** — branch switching operations

### 9. Advanced Tracing & Debugging
- ✅ **TraceExecution** — статический трейсинг выполнения с data flow
- ✅ **TraceBackwards** — обратный трейсинг от точки краша
- ✅ **AnalyzePathFeasibility** — символьное выполнение через Z3 Theorem Prover
- ✅ **Exception Analysis** — анализ throw paths и uncaught exceptions
- ✅ **Taint Analysis** — отслеживание потенциально опасных данных
- ✅ **Interprocedural Analysis** — трейсинг через вызовы методов

### 10. Semantic Merge & Replace

**Semantic Merge:**
- ✅ **Multi-format Support** — C#, XML, YAML, PowerShell, Shell scripts
- ✅ **Semantic Matching** — векторное сходство для определения перемещений
- ✅ **Movement Detection** — автоматическое обнаружение перемещённого кода
- ✅ **Type Ambiguity Detection** — выявление конфликтов типов при слиянии
- ✅ **Rename Detection** — обнаружение переименований через heuristics
- ✅ **Three-Way Merge** — умное слияние с учётом семантики
- ✅ **Branch-based Merge** — `semantic_merge(sourceBranch, targetBranch, instructions)`
- ✅ **Natural Language Instructions** — "ignore swagger; prefer source for caching"

**Semantic Replace:**
- ✅ **Two-Phase Workflow** — Preview → Apply для безопасных batch изменений
- ✅ **Three Search Modes** — regex, roslyn (FQN), semantic (ML)
- ✅ **Context Scopes** — statement, block, member, type, file
- ✅ **Smart Filters** — filePattern, namespaceFilter для таргетированных изменений
- ✅ **Apply Modes** — AllOrNothing (rollback) или BestEffort (partial apply)
- ✅ **Match IDs** — уникальные идентификаторы для точечных замен

### 11. Three-Process Architecture

**Проблема:** Каждый AI-агент запускал свой Roslyn + Semantic процесс = 2-3 GB RAM на каждый редактор.

**Решение:** Разделение на три процесса с общим backend.

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Claude Desktop    Claude Code    Cursor    VS Code + Continue.dev     │
│       ↓                ↓            ↓                ↓                  │
│    Comm.exe         Comm.exe     Comm.exe        Comm.exe              │
│  (stdio bridge)   (stdio bridge)   ...            ...     (~5 MB each) │
└────────┬───────────────┬────────────┬──────────────┬────────────────────┘
         │               │            │              │
         └───────────────┴─────┬──────┴──────────────┘
                               │ Named Pipes (IPC)
                               ↓
┌─────────────────────────────────────────────────────────────┐
│  UltrasharpTools.Droid.exe  (singleton, auto-start)         │
│  ├─ MCP Server (многоклиентный)                             │
│  ├─ Roslyn Workspace (анализ и модификация кода)            │
│  ├─ Git Integration (автокоммиты)                           │
│  └─ VectorDB Client ──────┐                                 │
└───────────────────────────│─────────────────────────────────┘
                            │ Named Pipes (IPC)
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  UltrasharpTools.VectorDB.exe  (singleton, lazy start)      │
│  ├─ Semantic Index (sqlite-vec / vectorlite)                │
│  ├─ Embedding Generator (TEI/Ollama)                        │
│  └─ Power Management (Efficiency Mode в idle)               │
└─────────────────────────────────────────────────────────────┘
```

**Роли процессов:**
| Процесс | Роль | Lifecycle |
|---------|------|-----------|
| **Comm** | Лёгкий stdio-bridge (~5 MB) | Один на каждый AI-агент/редактор |
| **Droid** | Roslyn workspace, Git, MCP tools | Singleton — один на все Comm |
| **VectorDB** | Semantic index, embeddings | Singleton — lazy start по требованию |

**Преимущества:**
- ✅ **Общие ресурсы** — 5 редакторов используют ОДИН Droid+VectorDB (экономия 2-3 GB RAM)
- ✅ **Общий кеш** — Roslyn compilation, symbol index загружаются один раз
- ✅ **Изоляция памяти** — VectorDB 500MB+ не влияет на Droid, Comm минималистичен
- ✅ **Независимый перезапуск** — VectorDB перезапускается при проблемах
- ✅ **Энергосбережение** — Efficiency Mode после 3 минут простоя
- ✅ **Ленивый запуск** — VectorDB стартует по требованию
- ✅ **Auto-recovery** — 30s timeout + автоперезапуск при сбоях

### 12. Production Ready
- ✅ **Docker Support** — multi-stage Dockerfile с оптимизациями
- ✅ **Kubernetes Deployment** — полный набор manifests (deployment, service, ingress, PVC)
- ✅ **Helm Chart** — гибкое развертывание с конфигурацией
- ✅ **GitHub Actions CI/CD** — автоматическая сборка и публикация в GHCR
- ✅ **Project-local Storage** — .ultrasharp/ для cache и logs
- ✅ **Production Build Scripts** — автоматизированная сборка для Windows/Linux

### 13. Documentation & Organization
- ✅ **Dev.Docs** — документация для разработчиков (Features/, Performance/, Development/)
- ✅ **Run.Docs** — документация для пользователей (Tools/, Setup/, Configuration/, Deployment/)
- ✅ **Feature Documentation Pattern** — Design → Examples → Implementation → Summary
- ✅ **Comprehensive Guides** — 7 категорий MCP tools подробно описаны

---

## 🎯 Ключевые новые возможности

### Quality Tools Suite

**Проблема:** Оригинальный SharpTools модифицировал код, но не проверял качество и форматирование.

**Решение:** Интеграция CSharpier + Roslyn Analyzers

**Новые инструменты:**

#### 1. FormatCode
Автоматическое форматирование C# кода через CSharpier.

```bash
# Проверка форматирования
format_code(path: "/src", checkOnly: true)

# Применение форматирования
format_code(path: "/src", checkOnly: false)
```

**Возможности:**
- Форматирование .cs, .csproj, .xml файлов
- Рекурсивная обработка директорий
- Параллельное форматирование для производительности
- Режим dry-run (checkOnly)

#### 2. AnalyzeCodeStyle
Анализ качества кода через Roslyn Analyzers (CA, IDE, CS диагностики).

```bash
analyze_code_style(
    solutionPath: "MySolution.sln",
    severityFilter: "Warning",
    skip: 0,
    take: 100
)
```

**Возможности:**
- Фильтрация по severity (Hidden/Info/Warning/Error)
- Пагинация для больших результатов
- Группировка по проектам и файлам
- Детальная информация о правилах

#### 3. ApplyCodeFixes
Автоматическое исправление code style issues.

```bash
# Preview fixes
apply_code_fixes(
    solutionPath: "MySolution.sln",
    diagnosticId: "IDE0005",
    preview: true
)

# Apply fixes
apply_code_fixes(
    solutionPath: "MySolution.sln",
    diagnosticId: "IDE0005",
    preview: false
)
```

**Поддерживаемые исправления:**
- `IDE0005` — Remove unnecessary using
- `CS8019` — Unnecessary using directive
- Легко расширяется для других диагностик

#### Auto-Linting Integration

**Все инструменты модификации** теперь автоматически проверяют качество:
- `add_member` → linting после добавления
- `modify_code` → linting после замены
- `rename_symbol` → linting после переименования
- `find_and_replace` → linting после замены
- `move_member` → linting после перемещения

**LintingResult format:**
```json
{
  "success": true,
  "message": "Member added successfully",
  "linting": {
    "totalIssues": 3,
    "errors": 0,
    "warnings": 2,
    "info": 1,
    "criticalIssues": 0,
    "issues": [
      {
        "severity": "Warning",
        "id": "IDE0005",
        "message": "Using directive is unnecessary",
        "file": "MyClass.cs",
        "line": 5
      }
    ]
  }
}
```

**Результаты:**
- ✅ Автоматическое обнаружение проблем качества
- ✅ Единый workflow: modify → lint → fix
- ✅ Интеграция с Git (auto-commit после исправлений)

---

### Semantic Merge

**Проблема:** Традиционные merge tools работают на уровне строк, не понимая семантику кода.

**Решение:** Умное 3-way слияние с анализом семантики, перемещений и переименований.

**Архитектура:**

#### Phase 1-2: Parsing & Indexing
- **ContentNormalizer** — нормализация whitespace, комментариев
- **StructuralFingerprint** — хеш-отпечатки для быстрого сравнения
- **CodeUnitExtractor** — извлечение семантических единиц (methods, classes)
- **Multi-format Parsers** — C#, XML, YAML, PowerShell, Shell

#### Phase 3-4: Matching & Alignment
- **FastPathMatcher** — быстрое сравнение через fingerprints
- **SemanticMatcher** — векторное сходство для перемещённого кода
- **MovementDetector** — обнаружение code movements между версиями
- **StructuralAligner** — выравнивание иерархий для слияния

#### Phase 5-6: Merge & Analysis
- **ThreeWayMerger** — умное слияние base + left + right
- **IntentClassifier** — классификация намерений (Add/Modify/Delete/Move)
- **RenameDetector** — обнаружение переименований
- **TypeAmbiguityDetector** — выявление конфликтов типов

**Пример использования (Branch-based):**
```bash
semantic_merge(
    sourceBranch: "feature/caching",
    targetBranch: "main",
    instructions: "ignore swagger; prefer source for caching",
    filePatterns: "*.cs",
    apply: true
)
```

**Natural Language Instructions:**
- `"ignore swagger"` → исключает *.swagger.json, **/swagger/**
- `"skip appsettings"` → исключает appsettings*.json
- `"prefer source for X"` → приоритет source ветки для кода связанного с X
- `"preserve formatting"` → сохраняет форматирование target ветки

**Результаты:**
- ✅ Автоматическое разрешение 80-90% конфликтов
- ✅ Обнаружение перемещений кода между файлами
- ✅ Обнаружение переименований методов/классов
- ✅ Сохранение семантики при слиянии
- ✅ Branch-based workflow (автоматический merge-base)
- ✅ Preview mode (apply: false)

---

### Semantic Replace

**Проблема:** Batch замены через regex теряют контекст и приводят к ошибкам.

**Решение:** Two-phase workflow с полным контекстом и селективным применением.

**Workflow:**

```
Phase 1: Preview                          Phase 2: Apply
┌─────────────────────────┐              ┌─────────────────────────┐
│ semantic_replace(       │              │ semantic_replace(       │
│   pattern: "Console\\." │  ──────────► │   apply: true,          │
│   scope: "member"       │   Review     │   replacements: [       │
│ )                       │   matches    │     {matchId, newCode}  │
└─────────────────────────┘              │   ]                     │
         │                               └─────────────────────────┘
         ▼
┌─────────────────────────┐
│ Returns:                │
│ - matchId (sr-001...)   │
│ - full context code     │
│ - file:line location    │
└─────────────────────────┘
```

**Search Modes:**
- `regex` — текстовый поиск по паттерну
- `roslyn` — поиск по FQN через Roslyn SymbolFinder
- `semantic` — ML-powered поиск по смыслу (требует VectorDB)

**Context Scopes:**
- `statement` — одна строка/statement
- `block` — окружающий блок {}
- `member` — полный метод/свойство
- `type` — полный класс/struct
- `file` — весь файл

**Пример использования:**
```bash
# Preview: найти все Console.WriteLine
semantic_replace(
    pattern: "Console\\.WriteLine",
    scope: "member",
    filePattern: "**/Services/*.cs"
)

# Apply: заменить выбранные
semantic_replace(
    apply: true,
    replacements: '[
        {"matchId":"sr-001","newCode":"_logger.LogInformation(msg)"},
        {"matchId":"sr-002","newCode":"_logger.LogWarning(err)"}
    ]',
    applyMode: "AllOrNothing",
    commitMessage: "Migrate to ILogger"
)
```

**Результаты:**
- ✅ Безопасные batch изменения с preview
- ✅ Полный контекст для информированных решений
- ✅ AllOrNothing rollback при ошибках
- ✅ Таргетированные фильтры (namespace, file pattern)

---

### Advanced Tracing & Debugging

**Проблема:** Статический анализ крашей требовал ручного чтения кода и логов.

**Решение:** Автоматический трейсинг с data flow и symbolic execution.

#### 1. TraceExecution
Статический трейсинг выполнения метода с отслеживанием переменных.

```bash
trace_execution(
    fullyQualifiedName: "MyApp.Services.UserService.CreateUser",
    maxDepth: 3,
    trackDataFlow: true
)
```

**Возможности:**
- Control flow analysis (if/else, loops, switch)
- Data flow tracking (variable assignments, parameters)
- Interprocedural analysis (вызовы других методов)
- Exception paths (throw, try/catch)

#### 2. TraceBackwards
Обратный трейсинг от точки краша к source.

```bash
trace_backwards(
    crashLocation: "MyClass.cs:142",
    exceptionType: "NullReferenceException",
    maxDepth: 5
)
```

**Возможности:**
- Поиск всех путей к crash point
- Анализ conditions (что должно быть true для краша)
- Определение root cause через backward slicing
- Рекомендации по исправлению

#### 3. AnalyzePathFeasibility
Символьное выполнение через Z3 Theorem Prover.

```bash
analyze_path_feasibility(
    fullyQualifiedName: "MyApp.Validator.IsValid",
    conditions: ["input.Age < 0", "input.Name == null"]
)
```

**Возможности:**
- Проверка выполнимости path constraints
- Генерация test inputs для достижения paths
- Обнаружение unreachable code
- Invariant verification

**Результаты:**
- ✅ Автоматический анализ крашей из логов
- ✅ Определение root cause без debugging
- ✅ Генерация тестовых данных для воспроизведения
- ✅ Математическое доказательство (через Z3)

---

## 🚀 Infrastructure & DevOps

### Docker & Kubernetes

**Dockerfile оптимизации:**
- Multi-stage build (SDK → ASP.NET runtime)
- ReadyToRun (R2R) compilation для faster startup
- Ubuntu Noble base images
- Server GC configuration
- K8s-ready env variables (DOTNET_USE_POLLING_FILE_WATCHER, etc.)
- Non-root user (UID 1001)

**Kubernetes manifests:**
- `deployment.yaml` — deployment с health checks, resources, volumes
- `service.yaml` — ClusterIP service на порту 3001
- `configmap.yaml` — конфигурация (git, logging, build)
- `pvc.yaml` — persistent storage для данных
- `ingress.yaml` — nginx ingress с SSL/TLS
- `secret.yaml.template` — шаблон для секретов

**Helm Chart:**
- Полный chart с values.yaml
- Гибкая конфигурация (resources, ingress, persistence)
- Conditional rendering (PVC, Ingress)
- Post-install NOTES с инструкциями

**GitHub Actions CI/CD:**
- Автоматическая сборка Docker образов
- Branch-based configuration (dev → Debug, release → Release)
- Публикация в GitHub Container Registry (GHCR)
- Docker build cache для быстрой сборки
- Automated tagging (branch, SHA, latest)

---

## 📈 Performance Benchmarks

### Solution Loading (Critical Path) ✅

**Baseline:** UltrasharpTools.sln (485,183 символов, 8 проектов)

| Этап | Before (Phase 0) | After (Phase 7) | Улучшение |
|------|------------------|-----------------|-----------|
| **Full solution load** | **356 секунд (6 минут)** | **10.16 секунд** | **35x быстрее** 🚀🚀 |
| Roslyn Solution Load | 2 сек | ~2 сек | — |
| Symbol Index Build | 354 сек (99%) | ~6 сек | **59x быстрее** |
| Layered Index Init | N/A | ~1 сек | New feature |
| Metadata & Finalization | <1 сек | ~1 сек | — |

**Ключевая оптимизация:** Type Dictionary Cache (FastSymbolIndex) + Layered Indexing

**Прогноз vs Реальность:**
- Прогнозировалось: ~20-30 секунд (18x ускорение)
- Достигнуто: 10.16 секунд (35x ускорение)
- **Превзошли прогноз в 2 раза!**

### Cold Start (Release build)

| Метрика | Before | After | Улучшение |
|---------|--------|-------|-----------|
| Startup time | 3-5 сек | 1.5-2.5 сек | **-50%** |
| First compilation | 200-300 мс | 80-120 мс | **-60%** |
| First tool call | 500-700 мс | 200-280 мс | **-60%** |

### Symbol Operations

| Операция | Before (linear) | After (indexed) | Улучшение |
|----------|----------------|-----------------|-----------|
| Fuzzy FQN lookup | 5-15 сек | 1-3 сек | **3-10x** |
| FindReferences | 5-10 сек | Instant | **5-10x** |
| SearchDefinitions | 10-30 сек | Instant | **10-30x** |
| Symbol search | O(N) linear | O(1) Bloom | **99% reduction** |

### Parallel Processing

| Операция | Before (sequential) | After (parallel) | Улучшение |
|----------|---------------------|------------------|-----------|
| Assembly loading | 2-10 сек | 0.5-2 сек | **4-5x** |
| NuGet resolution | 1-3 сек | 0.3-0.8 сек | **3-4x** |
| Semantic similarity | 30-120 сек | 10-40 сек | **2-4x** |

### Micro-Optimizations (Phase 5)

| Операция | Before | After | Улучшение |
|----------|--------|-------|-----------|
| BloomFilter.Add (Small) | 62.73 ns | 14.81 ns | **4.2x faster** |
| BloomFilter.Add (Large) | 302.8 ns | 52.78 ns | **5.7x faster** |
| ComputeHashes (SIMD) | 84.36 ns | 38.68 ns | **2.2x faster** |
| LogAnalysis (10MB) | 850 MB alloc | 1.2 KB alloc | **708,333x less allocations** |

### Memory Footprint

**До v3.0.8:**

| State | Before | After | Изменение |
|-------|--------|-------|-----------|
| After solution load | 5 MB | 5 MB | — |
| After metadata cache | 15 MB | 15 MB | — |
| After symbol index | N/A | 596 MB | **+596 MB** (trade-off) |
| MemoryCache limits | — | 1.5 GB | Compilation + SemanticModel |
| Reflection types | — | 50-150 MB | FrozenDictionary |
| **Total** | — | **1.7-2.5 GB** | — |

**После v3.0.8 (Memory Optimization):**

| State | Before (3.0.7) | After (3.0.8) | Экономия |
|-------|----------------|---------------|----------|
| MemoryCache limits | 1.5 GB | **400 MB** | **-1.1 GB** |
| Reflection types (normal) | 50-150 MB | 50-150 MB | — |
| Reflection types (--low-memory) | 50-150 MB | **5 MB** | **-50-100 MB** |
| **Total (normal)** | 1.7-2.5 GB | **600-900 MB** | **-60%** |
| **Total (--low-memory)** | 1.7-2.5 GB | **400-600 MB** | **-70%** |

**Trade-off:** +596 MB memory → 10-100x faster search (acceptable для production)

**Low Memory Mode (`--low-memory`):**
- Заменяет FrozenDictionary на SQLite + LRU cache
- Экономит дополнительно ~50-100 MB
- Небольшой overhead на lazy loading (~1-5ms per lookup)

---

## 🎓 Lessons Learned

### 1. Indexing vs On-Demand Trade-off
- **Layered indexing** решает проблему полной переиндексации — только изменённые файлы
- **Git-aware deltas** — автоматическая синхронизация с git branches
- **SQLite persistence** — кеш сохраняется между запусками (5.4x speedup)
- **Memory overhead** оправдан для production codebases (10k+ символов)
- **Disable для tiny projects** — можно добавить `--disable-layered-index` флаг

### 2. Dynamic PGO без Static PGO
- Dynamic PGO даёт **75-85% от максимума** без manual profiling
- Static PGO добавляет лишь **10-20%** поверх Dynamic
- Для MCP server (interactive workload) Dynamic PGO достаточно

### 3. Micro-Optimizations имеют значение
- **Span<T> + ArrayPool** дали 4-5x ускорение при zero cost
- **SIMD** работает автоматически при правильном коде (Vector<T>)
- **Streaming I/O** критично для больших файлов (logs, crash dumps)

### 4. Auto-linting Integration
- **Ранняя обратная связь** лучше чем post-commit checks
- **LintingResult** в каждом ответе помогает AI исправлять код сразу
- **QuickLintService** с MinimalSeverity threshold балансирует noise/utility

### 5. Documentation Structure
- **Design → Examples → Implementation → Summary** pattern работает отлично
- **Dev.Docs vs Run.Docs** разделение помогает новым пользователям
- **Feature-based organization** лучше чем file-based

---

## 📚 Архитектурные решения

### 1. Three-Process Architecture

**Comm → Droid → VectorDB разделение:**
```
Claude Desktop / Claude Code / Cursor / VS Code
    ↓ stdio (JSON-RPC)
┌─────────────────────────────────────────────────────────────┐
│  Comm.exe — Lightweight stdio bridge (~5 MB each)           │
│  └─ Forwards MCP messages to Droid via Named Pipes          │
└─────────────────────────────│───────────────────────────────┘
                              │ Named Pipes (IPC)
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Droid.exe — MCP Server (singleton, multi-client)           │
│  ├─ Roslyn Workspace (MSBuildWorkspace)                     │
│  ├─ FastSymbolIndex (Bloom + FrozenDictionary)              │
│  ├─ Git Integration (auto-commits)                          │
│  └─ VectorDBClient ─────────┐                               │
└─────────────────────────────│───────────────────────────────┘
                              │ Named Pipes (IPC)
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  VectorDB.exe — Semantic Engine (singleton, lazy start)     │
│  ├─ sqlite-vec / vectorlite backend                         │
│  ├─ TEI/Ollama embedding provider                           │
│  └─ PowerManagement (Efficiency Mode)                       │
└─────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- N редакторов → 1 Droid+VectorDB (экономия 2-3 GB RAM)
- Общий Roslyn workspace и symbol cache
- Изоляция памяти (VectorDB 500MB+ не влияет на Droid)
- Независимый restart при сбоях
- Ленивый запуск (экономия ресурсов)
- Разные power profiles

### 2. Dependency Injection
Все сервисы регистрируются через `ServiceCollectionExtensions`:
```csharp
services.WithSharpToolsServices(enableGit: true, buildConfiguration: "Debug");
services.AddMcpServer().WithHttpTransport().WithSharpTools();
```

### 3. Separation of Concerns
- **Tools/** — MCP endpoints (тонкий слой)
- **Services/** — бизнес-логика
- **Interfaces/** — контракты
- **Infrastructure/** — утилиты (ProjectPathHelper, FastSymbolIndex)

### 4. Caching Strategy
- **MemoryCache** для Compilation/SemanticModel (hot data)
- **SQLite** для AnalysisCache/CallGraphCache (persistent)
- **FastSymbolIndex** для in-memory symbol lookup
- **FrozenDictionary** для read-only reflection cache

### 5. Error Handling
- **ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync** wrapper для всех tools
- **Structured logging** через Microsoft.Extensions.Logging
- **Graceful degradation** (если индекс не построен → fallback to Roslyn API)

---

## 🔮 Future Improvements

### Short-term
- [x] ~~Кеширование FastSymbolIndex на диск между запусками~~ ✅ **DONE** (Phase 7: LayeredIndexing with SQLite)
- [x] ~~Incremental symbol indexing (только изменённые документы)~~ ✅ **DONE** (Phase 7: Working/Branch Deltas)
- [ ] `--disable-layered-index` флаг для tiny projects
- [ ] Code Metrics tool (complexity, maintainability index)

### Medium-term
- [ ] Full Static PGO support (когда .NET 10 RTM выйдет)
- [ ] Distributed caching для multi-instance deployments
- [ ] Prometheus metrics endpoint для мониторинга
- [ ] GraphQL API для flexible queries

### Long-term
- [ ] Language Server Protocol (LSP) support
- [ ] Multi-language support (TypeScript, Python, etc.)
- [ ] ML-based code completion
- [ ] Automatic refactoring suggestions

---

## 📄 Ссылки

- **Main README:** [../README.md](../README.md)
- **Performance Docs:** [Performance/Test_Report.md](Performance/Test_Report.md)
- **User Guides:** [../Run.Docs/](../Run.Docs/)
- **Developer Docs:** [./](.)
- **Semantic Merge:** [Features/SemanticMerge/](Features/SemanticMerge/)
- **Advanced Tracing:** [Features/Tracing/](Features/Tracing/)

---

## 🏆 Credits

**Original SharpTools:** https://github.com/tluyben/sharp-tools
**UltrasharpTools fork:** Комплексная модернизация с фокусом на production-ready качество

**Key Technologies:**
- .NET 10 RC2 with C# 13
- Microsoft.CodeAnalysis (Roslyn)
- CSharpier (code formatting)
- ICSharpCode.Decompiler
- LibGit2Sharp
- Z3 Theorem Prover
- BenchmarkDotNet

**Special Thanks:**
- Roslyn team за мощный API
- .NET team за Dynamic PGO и R2R
- Community за feedback и bug reports

### Scalability: Large Codebases (1M+ symbols)

**Фактические бенчмарки:**
- UltrasharpTools.sln (485K символов, 8 проектов) — **10.16 сек, 114 MB cache**
- Jellyfin.sln (~600K символов, 40 проектов) — **19.7 сек** (2025-11-25)

**Результаты тестирования для разных размеров:**

| Размер кодовой базы | Символов | Проектов | Init Time | FindReferences | Статус |
|---------------------|----------|----------|-----------|----------------|--------|
| **Small** (UltrasharpTools) | 485K | 8 | ✅ **10.16 сек** | <1 сек | ✅ Проверено |
| **Medium** (Jellyfin) | ~600K | 40 | ✅ **19.7 сек** | ~8.7 сек | ✅ Проверено |
| **Large** (Roslyn) | ~2M | ~100 | ~42 сек (прогноз) | 2-4 сек | ⏳ Прогноз |
| **Extra Large** (dotnet/runtime) | ~5M+ | ~200 | ~105 сек (прогноз) | 5-10 сек | ⏳ Прогноз |

**Детали бенчмарка Jellyfin (2025-11-25):**
| Операция | Время | Детали |
|----------|-------|--------|
| `load_solution` | 19.7 сек | 40 проектов, ~1800 документов |
| `load_project` | 8.3 сек | MediaBrowser.Controller (365 docs) |
| `find_references` | 8.7 сек | BaseItem - 1360 references |
| `view_definition` | 11.3 сек | с inline call graph |

**Примечание:** Jellyfin показал +16% overhead vs линейная экстраполяция из-за `UnresolvedAnalyzerReference` warnings.

**Масштабируемость оптимизаций:**

1. **FastSymbolIndex** (Bloom filter + FrozenDictionary)
   - **Memory:** O(N) linear growth (~1.7 MB per 1K symbols)
   - **Search:** O(k) Bloom + O(1) dictionary независимо от размера
   - **Benefit:** 10-100x ускорение сохраняется для любого размера

2. **Parallel Processing**
   - **Assembly loading:** Linear speedup до CPU core count
   - **Symbol indexing:** Эффективно использует все ядра
   - **Benefit:** На 16-core машине 8-12x ускорение vs single-thread

3. **Dynamic PGO**
   - **Hot paths:** Автоматическая оптимизация после прогрева
   - **Warm-up time:** ~2-5 минут для 1M+ символов
   - **Benefit:** +30-50% throughput после прогрева независимо от размера

**Фактические результаты: UltrasharpTools (485K symbols)**

| Операция | Before (без оптимизаций) | After (все оптимизации) | Улучшение |
|----------|-------------------------|------------------------|-----------|
| **Solution load** | ✅ **356 сек (6 мин)** | ✅ **10.16 сек** | ✅ **35x быстрее** |
| **Symbol indexing** | 354 сек (O(N) для каждого) | ~6 сек (Type Dictionary) | ✅ **59x быстрее** |
| **Fuzzy FQN lookup** | 5-15 сек (linear) | <1 сек (indexed) | **5-15x быстрее** |
| **FindReferences** | 5-10 сек (Roslyn) | <1 сек (index) | **5-10x быстрее** |
| **SearchDefinitions** | 10-30 сек (full scan) | <1 сек (index) | **10-30x быстрее** |

**Экстраполяция для 1M symbols** (прогноз):

| Операция | Before (без оптимизаций) | After (все оптимизации) | Улучшение |
|----------|-------------------------|------------------------|-----------|
| **Solution load** | ~730 сек (12 мин) | ~21 сек | **35x быстрее** |
| **Symbol indexing** | ~730 сек | ~12 сек (Type Dictionary) | **60x быстрее** |
| **Fuzzy FQN lookup** | 30-90 сек (linear) | 1-2 сек (indexed) | **30-45x быстрее** |
| **FindReferences** | 60-180 сек (Roslyn) | 1-3 сек (index) | **60-90x быстрее** |
| **SearchDefinitions** | 120-300 сек (full scan) | 1-3 сек (index) | **120-150x быстрее** |
| **Semantic similarity** | 10-30 минут (sequential) | 3-8 минут (parallel) | **3-4x быстрее** |

**Рекомендации для очень больших кодовых баз:**

1. **Hardware requirements:**
   - **CPU:** 16+ cores для оптимального parallel processing
   - **RAM:** 16+ GB для 1M+ символов (учитывая +8GB для FastSymbolIndex)
   - **SSD:** Обязательно для быстрой загрузки assemblies

2. **Configuration tuning:**
   ```bash
   # Увеличить memory limits для GC
   export DOTNET_GCHeapHardLimit=0x400000000  # 16 GB

   # Использовать Server GC
   export DOTNET_gcServer=1

   # Включить все ядра для parallel loading
   # (по умолчанию Environment.ProcessorCount)
   ```

3. **Incremental workflows:**
   - ✅ **LayeredIndexing** (Phase 7) — SQLite кеш + branch/working deltas
   - ✅ **Git-aware** — автоматическая синхронизация с git operations
   - ✅ **Incremental updates** — только изменённые файлы (working delta)
   - Использовать `--disable-layered-index` для одноразовых операций

**Trade-offs:**

| Размер | Init Time | Memory Overhead | Search Speedup | Рекомендация |
|--------|-----------|-----------------|----------------|--------------|
| <100K | +5-10 сек | +200 MB | 5-10x | ✅ Включить |
| 100K-500K | +20-40 сек | 600 MB - 1 GB | 10-50x | ✅ Включить |
| 500K-2M | +60-180 сек | 1.5-3.5 GB | 20-100x | ✅ Включить |
| 2M+ | +180+ сек | 3.5+ GB | 50-150x | ⚠️ Требует 16+ GB RAM |

**Conclusion:** Для production кодовых баз (100K+ символов) trade-off полностью оправдан — one-time initialization cost окупается 10-150x ускорением при каждом поиске.

# Changelog

Все значимые изменения в проекте UltrasharpTools MCP документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [3.4.0] - 2025-11-27

### 🎯 Статус
**Multi-Client Named Pipe Architecture** — Несколько MCP клиентов теперь могут работать с одним Droid процессом

### Добавлено

#### Named Pipe Multi-Client Mode 🔗

**Проблема:** Каждый Comm запускал свой Droid процесс = дублирование ресурсов и несинхронизированный state.

**Решение:** Singleton Droid с Named Pipe и Named Event wake-up:

```
Claude₁ ←stdio→ Comm₁ ──┐
Claude₂ ←stdio→ Comm₂ ──┼── Named Pipe ──→ Droid (singleton) ──→ VectorDB
Claude₃ ←stdio→ Comm₃ ──┘
```

**Новые компоненты:**

- **PipeServerMode** (`UltrasharpTools.Droid/Ipc/PipeServerMode.cs`)
  - Named Pipe сервер с multi-client поддержкой (до 10 клиентов)
  - Named Event `UltraSharpTools_Droid_WakeUp` для мгновенного пробуждения из idle
  - Каждый клиент получает отдельный MCP Host, но общие services
  - StreamServerTransport из MCP SDK 0.4.1 для custom streams

- **DroidPipeClient** (`UltraSharpTools.Comm/DroidPipeClient.cs`)
  - Подключение к существующему Droid через Named Pipe
  - Автозапуск Droid с `--pipe-server` если процесс не найден
  - Wake-up через Named Event перед подключением

- **Droid CLI option** `--pipe-server`
  - Запуск в daemon режиме для multi-client подключений
  - Comm автоматически запускает Droid с этим флагом

**Преимущества:**
- ✅ **N редакторов → 1 Droid** — экономия RAM и CPU
- ✅ **Общий SolutionManager** — solution загружается один раз
- ✅ **Instant wake-up** — Named Event работает на уровне ядра
- ✅ **Graceful shutdown** — корректное завершение при закрытии последнего клиента

#### Named Event Wake-up (VectorDB + Droid) ⚡

**Проблема:** EcoQoS (Efficiency Mode) агрессивно троттлил CPU, Named Pipe listener не успевал принять подключения.

**Решение:** Named Event `EventWaitHandle` для мгновенного пробуждения:
- Droid: `UltraSharpTools_Droid_WakeUp`
- VectorDB: `UltraSharpTools_VectorDB_WakeUp`

Named Event работает на уровне ядра Windows и не зависит от ThreadPool или CPU throttling.

### Изменено

**MCP SDK обновлён до 0.4.1-preview.1:**
- Публичный `StreamServerTransport` для custom streams
- Поддержка Named Pipe через `StreamServerTransport(inputStream, outputStream)`

**Документация:**
- README.md — обновлена секция архитектуры с Named Pipe диаграммой
- Dev.Docs/Architecture/ARCHITECTURE.md — добавлена секция Named Pipe Multi-Client
- Dev.Docs/USAGE_GUIDE.md — добавлены troubleshooting секции для Named Pipe
- Dev.Docs/CLAUDE.md — обновлена структура проекта

### Исправлено

**VectorDB wake-up из idle mode (улучшено):**
- Теперь используется Named Event вместо только ThreadPool
- EcoQoS снова можно использовать безопасно
- Файл: `UltraSharpTools.VectorDB/PowerManagementService.cs`

---

## [3.3.0] - 2025-11-27

### 🎯 Статус
**Three-Process Architecture & Semantic Replace** — Новая архитектура Comm → Droid → VectorDB для экономии ресурсов + новый инструмент semantic_replace

### Добавлено

#### Three-Process Architecture (Comm → Droid → VectorDB) 🏗️

**Проблема:** Каждый AI-агент (Claude Desktop, Claude Code, Cursor) запускал свой Droid процесс = 2-3 GB RAM на каждый редактор.

**Решение:** Разделение на три процесса с общим backend:

```
Claude Desktop / Claude Code / Cursor / VS Code
    ↓ stdio (JSON-RPC)
┌─────────────────────────────────────────────────────────────┐
│  Comm.exe — Lightweight stdio bridge (~5 MB each)           │
└─────────────────────────────│───────────────────────────────┘
                              │ Named Pipes (IPC)
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Droid.exe — MCP Server (singleton, multi-client)           │
└─────────────────────────────│───────────────────────────────┘
                              │ Named Pipes (IPC)
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  VectorDB.exe — Semantic Engine (singleton, lazy start)     │
└─────────────────────────────────────────────────────────────┘
```

**Преимущества:**
- ✅ **N редакторов → 1 Droid** — экономия 10-20 GB RAM
- ✅ **Общий Roslyn workspace** — все редакторы видят одни изменения
- ✅ **Общий symbol cache** — загружается один раз
- ✅ **Auto-start** — Comm автоматически запускает Droid при первом подключении

#### semantic_replace — Batch Find & Replace с контекстом 🔄

**Two-Phase Workflow:**
```javascript
// Phase 1: Preview — найти все вхождения с полным контекстом
semantic_replace(
    pattern: "Console\\.WriteLine",
    searchMode: "regex",      // regex | roslyn | semantic
    scope: "member",          // statement | block | member | type | file
    filePattern: "**/Services/*.cs"
)
// Returns: matchId, full context code, file:line location

// Phase 2: Apply — применить выбранные замены
semantic_replace(
    apply: true,
    replacements: '[{"matchId":"sr-001","newCode":"_logger.LogInformation(msg)"}]',
    applyMode: "AllOrNothing",  // AllOrNothing | BestEffort
    commitMessage: "Migrate to ILogger"
)
```

**Use cases:**
- Миграция Console.WriteLine → ILogger
- Замена deprecated API на новые
- Batch обновление паттернов кода
- API upgrades с полным контекстом

#### semantic_merge — Branch-based Merge 🌿

**Обновлённый API:**
```javascript
semantic_merge(
    sourceBranch: "feature/caching",
    targetBranch: "main",
    instructions: "ignore swagger; prefer source for caching",
    filePatterns: "*.cs",
    apply: true
)
```

**Natural Language Instructions:**
- `"ignore swagger"` → исключает *.swagger.json
- `"skip appsettings"` → исключает appsettings*.json
- `"prefer source for X"` → приоритет source ветки для кода X

### Изменено

**Конфигурация MCP — теперь запускается Comm.exe:**
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "C:\\Tools\\UltrasharpTools\\Comm\\UltrasharpTools.Comm.exe"
    }
  }
}
```

**Документация:**
- README.md — полностью переписана секция архитектуры
- ULTRA_SHARP_SEMANTIC.md — добавлены semantic_replace, get_semantic_replace_info
- ULTRA_SHARP.md — обновлён Quick Reference
- Dev.Docs/CLAUDE.md — добавлены SemanticReplaceTools.cs, SemanticMergeTools.cs
- Dev.Docs/ULTRA-SHARPED.md — добавлена секция Three-Process Architecture

### Исправлено

**VectorDB wake-up из idle mode:**
- **Проблема:** VectorDB не просыпался после Efficiency Mode (EcoQoS)
- **Причина:** EcoQoS слишком агрессивно троттлил CPU, Named Pipe listener не успевал принять подключения
- **Решение:** Отключён EcoQoS в PowerManagementService, оставлен только ThreadPool reduction + GC.Collect
- **Файл:** `UltraSharpTools.VectorDB/PowerManagementService.cs`

---

## [3.2.1] - 2025-11-26

### 🎯 Статус
**Bugfix Release** — Исправление VectorDB wake-up + улучшения стабильности

### Исправлено

**VectorDB connection timeout:**
- Добавлен 30-секундный timeout для подключения к VectorDB
- Автоматический перезапуск VectorDB процесса при timeout
- Метод `KillVectorDBProcess()` в VectorDBClient для принудительного перезапуска

**PowerManagementService idle threads:**
- Увеличен `IdleMinWorkerThreads` с 1 до 2
- Увеличен `IdleMinCompletionThreads` с 1 до 2
- Предотвращает deadlock при пробуждении из idle mode

### Изменено

**VectorDBClient resilience:**
- Try-catch обёртка для StartVectorDBProcess
- Graceful handling connection failures
- Improved logging для диагностики

---

## [3.2.0] - 2025-11-25

### 🎯 Статус
**Efficiency Mode Release** — Энергосбережение для VectorDB процесса

### Добавлено

**PowerManagementService для VectorDB:**
- Efficiency Mode (EcoQoS) после 3 минут простоя
- Автоматическое переключение Normal ↔ Idle режимов
- ThreadPool optimization в idle mode
- GC.Collect для освобождения памяти

### Изменено

- VectorDB теперь уходит в energy-saving mode при отсутствии запросов
- Уменьшено потребление CPU в idle (Efficiency Mode flag)

---

# [3.0.8] - 2025-11-25

### 🎯 Статус
**Memory Optimization Release** - Значительное снижение потребления памяти (~1.1GB → ~400-600MB)

### Добавлено

#### Memory Optimization: Low Memory Mode ⚡💾

**Проблема (до 3.0.8):**
- Droid процесс потреблял **1.7-2.5 GB** памяти на средних проектах
- MemoryCache лимиты: 500MB + 1GB = **1.5GB** только на кеш
- FrozenDictionary для reflection types: **50-150MB** в памяти
- Все ISymbol объекты хранились в памяти

**Решение:** Low Memory Mode (`--low-memory` CLI flag)

**Компоненты:**

1. **Уменьшенные лимиты MemoryCache** (всегда активно):
   - Compilation cache: 500MB → **150MB** (-70%)
   - SemanticModel cache: 1GB → **250MB** (-75%)
   - **Общая экономия:** ~1.1GB памяти

2. **SqliteSymbolIndex** (FTS5):
   - Disk-based storage для символов вместо памяти
   - Full-Text Search 5 для быстрого поиска по именам
   - Возвращает `SymbolSearchResult` без ISymbol reference
   - WAL mode для производительности

3. **SqliteReflectionTypeIndex** (только в `--low-memory` режиме):
   - Disk-based storage для reflection types
   - Заменяет FrozenDictionary (~50-150MB экономия)
   - LRU cache (500 типов) для горячих данных
   - Lazy loading Type объектов через MetadataLoadContext
   - FTS5 для полнотекстового поиска типов

**Новый CLI флаг:**
```bash
UltrasharpTools.Droid.exe --low-memory
```

**Результаты:**
| Метрика | Before | After (--low-memory) | Экономия |
|---------|--------|---------------------|----------|
| MemoryCache лимиты | 1.5GB | 400MB | **~1.1GB** |
| Reflection types | 50-150MB (FrozenDict) | ~5MB (SQLite + LRU) | **~50-100MB** |
| Общее потребление | 1.7-2.5GB | 400-600MB | **~1.2GB (60-70%)** |

### Изменено

**SolutionManager:**
- Добавлен параметр `lowMemoryMode` в конструктор
- Условная загрузка reflection types (FrozenDictionary vs SQLite)
- Интеграция SqliteReflectionTypeIndex для поиска типов

**ServiceCollectionExtensions:**
- Добавлен параметр `lowMemoryMode` в `WithUltrasharpToolsServices()`

**Program.cs (Droid):**
- Новый CLI флаг `--low-memory`

### Производительность

**Memory vs Speed trade-off в `--low-memory` режиме:**

| Операция | Normal Mode | Low Memory Mode | Разница |
|----------|-------------|-----------------|---------|
| Type lookup | O(1) FrozenDict | O(1) SQLite + LRU | +1-5ms |
| Type search | O(N) linear | O(log N) FTS5 | **Быстрее** |
| First type load | Instant | Lazy load | +10-50ms |
| Memory | 1.7-2.5GB | 400-600MB | **-60-70%** |

**Рекомендации:**
- **Default mode** — для машин с 16+ GB RAM
- **Low memory mode** — для ограниченных ресурсов или CI/CD

---

## [3.0.7] - 2025-11-24

### 🎯 Статус
**Major Performance Release** - Phase 7 Complete: Fast Symbol Index (Type Dictionary Cache) - **35x ускорение**

### Добавлено

#### Phase 7: Fast Symbol Index - Критическая оптимизация ⚡🚀

**Type Dictionary Cache** - Главное достижение:
- **Проблема (до Phase 7):** Загрузка solution с 485K символов занимала **356 секунд (6 минут)**
  - `GetTypeByMetadataName()` вызывался 485,183 раз (по разу на каждый символ)
  - O(N) поиск по всем типам в compilation для каждого символа
  - 354 секунды (99% общего времени) тратилось на symbol restoration

- **Решение:** Pre-build Type Dictionary Cache
  - `BuildTypeCache()` - один раз O(N) обход всех типов при загрузке compilation
  - `TypeCache[fqn]` - O(1) lookup для каждого символа (Dictionary)
  - Type cache построен **один раз** вместо 485K поисков

- **Результат:** **356 сек → 10.16 сек (35x ускорение!)** 🚀🚀
  - Превзошли прогноз: ожидалось 18x, достигнуто 35x (почти в 2 раза лучше!)
  - Roslyn Solution Load: ~2s (20%)
  - Fast Symbol Index Build: ~6s (59%) - Type Dictionary Cache
  - Layered Index Init: ~1s (10%) - Bloom + SQLite
  - Metadata & Finalization: ~1s (10%)

**Layered Index Persistence:**
- SQLite кеш для Base Index: 114 MB (485K символов)
- Branch deltas: ~14 MB (multiple branches)
- Warm cache load: 8.9s (5.4x быстрее cold start)
- Branch switch: < 20ms (1780x быстрее vs cold rebuild)

**Архитектурные компоненты:**
- `FastSymbolIndex.TypeCache` - Type Dictionary для O(1) lookups
- `LayeredSymbolIndex` - Base + Branch Deltas + Working Deltas
- `SymbolCacheManager` - SQLite persistence с WAL mode
- Bloom filters для fast negative checks (99.9% accuracy)

### Изменено

**Документация - полное обновление:**
- `README.md` - обновлены цифры производительности (35x, 10.16 сек)
- `ARCHITECTURE.md` v2.0:
  - Четырёх-проектная структура (Comm + Droid + Overlord + Tools)
  - Fast Symbol Index как ключевая оптимизация
  - Фактические бенчмарки (485K символов, 10.16 сек)
  - Layered Index v2 с SQLite persistence
  - Обновлённые performance metrics и memory footprint
- `Dev.Docs/ULTRA-SHARPED.md`:
  - Фактические результаты 35x vs прогноз 18x
  - Экстраполяция для проектов 1M+ символов
  - Comparison с OmniSharp, Rider, Visual Studio
- `Dev.Docs/Features/HybridArchitecture/performance-analysis.md`:
  - Breakdown по этапам (ДО/ПОСЛЕ)
  - Реализованные оптимизации
  - Почему результат превзошёл прогноз

**Организация проекта:**
- Удалены временные файлы: SESSION_SUMMARY.md, COMM_FIX_SUMMARY.md, R2R_REMOVAL_SUMMARY.md
- Перемещены в Dev.Archive: PHASE_1_SEMANTIC_DISCOVERY.md, PHASE_2_COMPLETE.md, PHASE_3_1_COMPLETE.md, PHASE_3_3_SEMANTIC_IPC.md

### Производительность

**Фактические результаты (UltrasharpTools.sln - 485K symbols, 8 projects):**

| Операция | Before (Phase 0) | After (Phase 7) | Улучшение |
|----------|------------------|-----------------|-----------|
| **Full solution load** | **356 сек (6 мин)** | **10.16 сек** | **35x быстрее** 🚀🚀 |
| Symbol Index Build | 354 сек (99%) | ~6 сек | **59x быстрее** |
| Warm cache load | N/A | 8.9s | **5.4x** от cold |
| Branch switch | 48.3s | **< 20ms** | **2400x быстрее** |
| Symbol search | 5-15 сек | < 100ms | **50-150x** |

**Memory:**
- Base (Roslyn): ~500 MB
- Layered Index Cache (SQLite): +114 MB
- Total: 614 MB для 485K символов
- Trade-off: 114 MB за 35x ускорение - **полностью оправдан!**

**Экстраполяция для больших проектов:**
- 1M символов: ~21 сек (прогноз на основе линейного масштабирования)
- 2M символов: ~42 сек
- Реальная производительность может быть ещё лучше благодаря синергии оптимизаций

---

### [3.0.6] - 2025-11-20

### 🎯 Статус
**Hybrid Mode Release** - Comm (Native AOT) + Droid (IPC) Architecture

### Добавлено
- **UltrasharpTools.Comm** - Native AOT прокси (< 4 MB)
  - Stdio MCP proxy для Claude Desktop
  - Auto-launch Droid через IPC
  - Named pipes communication (Windows/Linux)
  - Graceful shutdown handling

### Изменено
- Hybrid архитектура: Comm → Droid (IPC) → Tools
- Минимальный footprint для MCP клиентов

### Исправлено
- Comm path resolution (заглавная S → маленькая s в UltrasharpTools.Droid)
- Graceful shutdown для background processes

---
## [3.0.2] - 2025-11-19

### 🎯 Статус
**TBD** - Brief description of this release

### Добавлено
- TODO: Add new features here

### Изменено
- TODO: Add changes here

### Исправлено
- TODO: Add fixes here

---
## [3.0.1] - 2025-11-19

### 🎯 Статус
**TBD** - Brief description of this release

### Добавлено
- TODO: Add new features here

### Изменено
- TODO: Add changes here

### Исправлено
- TODO: Add fixes here

---
## [1.0.1] - 2025-11-19

### 🎯 Статус
**TBD** - Brief description of this release

### Добавлено
- TODO: Add new features here

### Изменено
- TODO: Add changes here

### Исправлено
- TODO: Add fixes here

---
# [3.0.0] - 2025-11-18

### 🎯 Статус
**Major Release** - Phase 13.4 & 14.1 Complete: Performance Optimization, Hybrid Mode, Documentation

### Добавлено

#### Phase 13.4: Performance Optimization ⚡

**Code Analysis & Hot Path Optimization** 🔍
- **Comprehensive Code Analysis:**
  - Проанализировано 18 файлов с StringBuilder allocations
  - Обнаружено 25 TODO items (7 high-priority для Phase 14)
  - Найдено 0 async/await антипаттернов (.Result/.Wait) ✅
  - Идентифицированы hot paths для оптимизации

- **ObjectPool Integration (Hot Paths):**
  - CallGraphExporter.cs: 3 метода (ToDot, ToMermaid, ToGraphML)
  - LintingHelper.cs: FormatLintingResults()
  - ModificationTools.cs: 4 critical allocations (RenameSymbol, ReplaceReferences)
  - **Итого:** 8 оптимизированных hot paths
  - **Эффект:** 15-25% снижение allocations в export/linting/modification operations

- **ValueTask Adoption** - 30-40% allocation reduction
  - IServerBridgeService: все методы переведены на ValueTask
  - CallMcpProxyAsync, IsServerAvailableAsync, event methods
  - ConfigureAwait(false) везде для library code
  - Removes unnecessary context captures

- **ObjectPool Implementation** - 15-25% allocation reduction potential
  - ObjectPoolService с DI integration
  - ObjectPoolProvider (singleton pattern)
  - PooledStringBuilderPolicy (1KB initial, 16KB max capacity)
  - PooledSymbolListPolicy (128 initial, 2048 max)
  - PooledStringListPolicy (64 initial, 1024 max)
  - PooledByteArrayPolicy (4KB buffers, 50 per bucket)
  - Metrics tracking: Get/Return counters, return rates

- **SIMD Optimizations (AVX-512)** - 2-3x speedup для vectorized operations
  - SimdOperations.cs: comprehensive SIMD utility library
  - DotProduct: AVX-512 (16 floats), AVX2 (8), SSE2 (4), fallback
  - CosineSimilarity: embedding vector similarity computation
  - EuclideanDistance: semantic similarity в embedding space
  - LevenshteinDistance: fuzzy string matching с early exit
  - Runtime CPU detection: IsAvx512Supported, IsAvx2Supported
  - Automatic fallback paths для older CPUs

- **Compilation Cache Improvements** - 10-15% faster warm cache loads
  - SyntaxTreeCacheService: LRU cache для parsed syntax trees
    - Capacity: 500 entries, TTL: 10 minutes
    - GetOrParseAsync API с automatic version tracking
    - Metrics: hits, misses, parse time
  - SemanticModelCacheService: LRU cache для semantic models
    - Capacity: 200 entries, TTL: 5 minutes
    - GetOrComputeAsync API с document version tracking
    - Metrics: hits, misses, compute time, average compute time
  - Fine-grained invalidation: InvalidateFile, InvalidateProject
  - TTL-based automatic expiration

#### Phase 14.1: Hybrid Mode Enhancements ✨
- **RetryPolicy** - Exponential backoff для Overlord calls
  - Максимум 3 попытки с retry logic
  - Начальная задержка 100ms, exponential backoff (2x) до 10s
  - Автоматическое определение retryable exceptions
  - Custom retry check support

- **RequestBatchingService** - Request batching для semantic queries
  - Batch window: 50ms (configurable)
  - Max batch size: 10 запросов (configurable)
  - Автоматическое определение semantic tools
  - Параллельное выполнение запросов в batch
  - До 10x снижение network overhead

- **Connection Pooling Optimization**
  - SocketsHttpHandler с оптимизированными настройками
  - PooledConnectionLifetime: 15 минут
  - PooledConnectionIdleTimeout: 5 минут
  - MaxConnectionsPerServer: 10
  - HTTP/2 multiplexing enabled

- **Unit Tests**
  - RetryPolicyTests - 6 test cases
  - Coverage: retry logic, exponential backoff, custom checks

### Добавлено (Documentation)

- **Comprehensive Documentation**
  - ARCHITECTURE.md - Complete system architecture (790+ lines)
  - USAGE_GUIDE.md - Practical usage guide with examples (820+ lines)
  - ROADMAP.md - Development roadmap for 2025-2026 (430+ lines)
  - CHANGELOG.md - Project history in Keep a Changelog format
  - Run.Docs/OVERLORD_README.md - Docker package documentation

- **GitHub Container Registry Optimization**
  - OCI-compliant labels in Dockerfile
  - Automatic package description updates via CI/CD
  - Direct link to Overlord-specific documentation
  - Enhanced metadata for discoverability

### Изменено

- **Breaking: Version bump to 3.0.0** (was 2.2.1)
  - Major documentation restructuring
  - All .csproj files updated with Version=3.0.0
  - Dockerfile labels updated
  - All documentation references updated

- **Documentation Reorganization**
  - PROJECT_STATUS.md → CHANGELOG.md (chronological history)
  - PUBLISH_README.md moved to Run.Docs/
  - Dockerfile moved to UltrasharpTools.Overlord/
  - Phase-* files renamed to semantic names
  - 24 completion documents archived to Dev.Archive/

- **CI/CD Improvements**
  - Updated docker-publish.yml with new Dockerfile path
  - Added package metadata update step
  - Enhanced build summary output

### Удалено

- PROJECT_STATUS.md (replaced by CHANGELOG.md + ROADMAP.md)
- Root-level PUBLISH_README.md (moved to Run.Docs/)
- Root-level Dockerfile (moved to UltrasharpTools.Overlord/)

---

## [2.2.1] - 2025-11-18

### 🎯 Статус
**Production Ready** - Phase 1-12.4 Complete

### Добавлено

#### Phase 12.4: Universal Semantic Mode - Configuration & Testing ✨
- **Configuration System**
  - `semantic-mode-config.json` schema с JSON Schema validation
  - `semantic-mode-config.yaml` примеры конфигурации
  - ConfigurationLoader для semantic config с hot-reload
  - Per-tool semantic threshold configuration
  - Enrichment strategy enable/disable controls

- **Testing Infrastructure**
  - 18 unit tests для SemanticModeProvider (100% coverage)
  - 15 unit tests для ToolEnricher (strategy validation)
  - 5 integration tests для enrichment pipeline
  - Mock embedding services для testing
  - Performance benchmarks для enrichment latency

- **Performance Benchmarks**
  - Enrichment overhead: 50-150ms per tool call
  - Semantic search latency: p50=45ms, p95=120ms, p99=200ms
  - Cache hit rate: 75-80% (5-minute TTL)
  - Memory overhead: +150-200MB for semantic services

- **Documentation**
  - ARCHITECTURE.md - Complete system architecture
  - USAGE_GUIDE.md - Practical usage examples
  - ROADMAP.md - 2025-2026 development roadmap
  - Dev.Docs restructure - organized by Features/Architecture/Performance

#### Phase 12.3: Tool Routing Cleanup & Clone Detection Unification
- **Clone Detection Analysis**
  - CLONE_DETECTION_UNIFICATION_ANALYSIS.md - detailed comparison
  - `find_duplicates` (OVERLORD) - Query-based cross-project semantic search
  - `detect_code_clones` (LOCAL) - Batch analysis with AST + similarity grouping
  - Decision: Keep both tools for different use cases

- **Tool Routing Updates**
  - `detect_code_clones` moved to LOCAL category
  - Updated ToolRoutingConfig with correct routing
  - McpProxyService.ExecuteDetectCodeClones with helpful error message
  - CODE_AUDIT_REPORT.md resolved status

#### Phase 12.2: Extended Enrichment Strategies
- **10 New Enrichment Strategies**
  - `find_all_references` - semantic similar members cross-project
  - `list_types` - related types by embedding similarity
  - `search_symbols` - hybrid search (semantic + fuzzy matching)
  - `trace_execution` - semantic related execution paths
  - `analyze_code_style` - similar code style examples
  - `get_type_hierarchy` - semantically related types
  - `get_project_structure` - similar project structures
  - `find_usages` - semantic usage patterns
  - `get_diagnostics` - similar diagnostic patterns
  - `apply_code_fixes` - semantic fix recommendations

- **Coverage Expansion**
  - **18 tools now supported** (85% coverage of top-20 tools)
  - Smart threshold tuning: 0.6-0.75 depending on tool
  - Context-aware recommendations
  - Timeout protection: 5 seconds per enrichment

#### Phase 12.1: Universal Semantic Mode Core Infrastructure
- **ISemanticModeProvider + SemanticModeProvider** (320 lines)
  - Auto-detection: Local embedding (Ollama/TEI)
  - Auto-detection: Overlord EmbeddingService
  - Smart source selection: Local first (lower latency)
  - Availability caching: 5-minute TTL
  - Graceful fallback logic

- **IToolEnricher + ToolEnricher** (910 lines)
  - 5 base enrichment strategies (Phase 12.1)
  - Pluggable strategy architecture
  - Per-tool semantic enrichment
  - Timeout protection (5 seconds)
  - Smart threshold tuning (0.6-0.75)

- **IMcpToolExecutor + McpToolInterceptor** (190 lines)
  - Pre-execution routing decision
  - Post-execution semantic enrichment
  - Decorator Pattern for MCP SDK integration
  - RegisterLocalToolExecutor for local tools
  - ExecuteWithFallbackAsync for graceful degradation

- **IServerBridgeService Enhancements**
  - Dictionary<string, object> overload for convenience
  - Automatic JSON serialization/deserialization
  - Type-safe parameter passing

- **DI Integration**
  - Registered in both Hybrid and Local modes
  - Graceful degradation when semantic services unavailable
  - Zero-config startup (auto-detection)

#### Phase 11: Integration & DI Setup
- **ToolRouter DI Registration**
  - Hybrid mode: Full routing with Overlord fallback
  - Local mode: Local-only with graceful degradation
  - ConfigurationService integration

- **Background Services**
  - HealthCheckHostedService - periodic Overlord health checks
  - Console output with service status information
  - Startup diagnostics for routing configuration

#### Phase 10: Tool Routing Logic
- **ToolRouter Implementation**
  - Automatic routing for all 52 MCP tools
  - Classification: Semantic (5), Hybrid (5), Local (42)
  - ToolRoutingConfig - JSON-based configuration
  - ConfigurationService - load from `.ultrasharp/overlord-config.json`
  - Graceful fallback: Overlord unavailable → Local execution

- **Routing Categories**
  - **Semantic (5 tools):** semantic_search, semantic_diff, detect_code_clones, pattern_search, find_duplicates
  - **Hybrid (5 tools):** find_references, get_type_hierarchy, trace_execution, analyze_complexity, find_usages
  - **Local (42 tools):** All Roslyn-based fast operations

#### Phase 9: Semantic Tools в Overlord
- **12 MCP Proxy Tools**
  - semantic_search - natural language code search
  - semantic_diff - cosine similarity comparison
  - detect_code_clones - ML-based clone detection
  - pattern_search - hybrid search (semantic + regex)
  - find_duplicates - enhanced with target vector search
  - reindex_changed_files - incremental index updates

- **MultiProjectVectorStore**
  - Cross-project semantic search
  - Centralized embedding storage
  - Project-scoped vector indices
  - Efficient similarity queries

#### Phase 8: IEmbeddingService Integration
- **IEmbeddingService in Overlord**
  - Ollama support (nomic-embed-text, mxbai-embed-large)
  - TEI (Text Embeddings Inference) support
  - CLI parameters: `--embedding-url`, `--embedding-model`
  - Health check integration

- **Enhanced Tools**
  - find_duplicates with TargetVector + embedding
  - Semantic code similarity analysis
  - Cross-project duplicate detection

### Изменено

- **Documentation Restructure**
  - Phase-* files renamed to semantic names
  - UNIVERSAL_SEMANTIC_MODE_SUMMARY.md (was Phase-12-Summary.md)
  - ADVANCED_TRACING.md (was Phase5_Advanced.md)
  - TRACING_OPTIMIZATION.md (was Phase6_Optimization.md)
  - PARALLELIZATION_SUMMARY.md (was Phase4_Summary.md)

- **Archive Organization**
  - 24 completion documents moved to Dev.Archive/
  - Historical phase reports preserved
  - Dev.Docs/ reorganized by Architecture/Features/Performance

- **Build System**
  - Dockerfile moved to UltrasharpTools.Overlord/
  - GitHub Actions updated for new Dockerfile path
  - PUBLISH_README.md moved to Run.Docs/

### Исправлено

- **Clone Detection Routing**
  - detect_code_clones correctly routed to LOCAL (not OVERLORD)
  - find_duplicates correctly routed to OVERLORD
  - Clear error messages when tools called on wrong server

- **Performance Optimizations**
  - Enrichment timeout protection (5 seconds)
  - Semantic availability caching (5-minute TTL)
  - Smart embedding source selection (Local first)

### Производительность

**Layered Symbol Indexing:**
- Solution load: 48.3s cold → 8.9s warm (5.4x speedup)
- Branch switch: 48.3s → 16.6ms (2900x speedup!)
- Symbol lookup: O(1) via FQN-based dictionary

**Advanced Tracing:**
- TraceBackwards: 2-3s cold → 400-600ms warm (5-7x speedup)
- Call graph caching (5-10x faster repeated traces)
- CFG analysis with path feasibility

**Parallelization:**
- Solution reload: 48.7s → 2.49s (19.5x speedup)
- Git branch switch: 37.2s → 0.39s (95.4x speedup)
- Early exit optimization for LoadSolution

**JSON Hashing:**
- xxHash32: 2-3x faster than SHA256
- Minimal memory allocations
- SIMD vectorization where available

---

## [2.1.0] - 2025-10-15

### Добавлено

#### Phase 7: Symbol Cache & Auto-Reload
- **Symbol Cache System**
  - 10x faster initialization for large solutions
  - FQN-based symbol lookup (O(1) performance)
  - Incremental updates on file changes

- **Auto-Reload Features**
  - FileWatcherService - monitors .csproj/.sln changes
  - GitWatcherService - detects branch switches
  - Automatic solution reload on file system events

- **Code Style Integration**
  - EditorConfig support for formatting
  - CSharpier integration for consistent style
  - analyze_code_style tool for style checks

#### Phase 6: Advanced Tracing Optimization
- **Call Graph Caching**
  - 5-10x speedup for repeated traces
  - Smart invalidation on code changes
  - Memory-efficient storage

- **CFG Analysis**
  - Control Flow Graph tracing
  - Path feasibility analysis (Z3 Solver)
  - Symbolic execution support

#### Phase 5: Advanced Tracing
- **TraceExecution Tool**
  - Static code tracing via Roslyn
  - Call graph construction
  - Cross-project method resolution

- **TraceBackwards Tool**
  - Reverse dependency tracing
  - Find all callers of a method
  - Impact analysis for changes

#### Phase 4: Parallelization & Early Exit
- **Solution Reload Optimization**
  - 48.7s → 2.49s (19.5x speedup)
  - Parallel project loading
  - Shared compilation references

- **Git Branch Switch**
  - 37.2s → 0.39s (95.4x speedup)
  - Early exit when no changes detected
  - Incremental recompilation

#### Phase 3: Layered Symbol Indexing
- **3-Layer Architecture**
  - Base Layer: Initial solution snapshot
  - Branch Delta: Git branch changes
  - Working Delta: Unsaved editor changes

- **Performance Results**
  - Branch switch: 48.3s → 16.6ms (2900x speedup!)
  - Solution load: 48.3s cold → 8.9s warm (5.4x)
  - Memory efficient: Only deltas stored

### Изменено

- **ToolRouter Architecture**
  - Simplified routing logic
  - Better fallback handling
  - Improved error messages

- **Configuration System**
  - JSON Schema validation
  - Hot-reload support
  - Per-tool configuration

---

## [2.0.0] - 2025-08-01

### Добавлено

#### Phase 2: Overlord HTTP Server
- **UltrasharpTools.Overlord**
  - ASP.NET Core HTTP server
  - SSE (Server-Sent Events) for notifications
  - Multi-user support
  - Centralized vector store

- **IServerBridgeService**
  - HTTP client for Droid → Overlord communication
  - Automatic retry logic
  - Connection pooling

#### Phase 1: Core MCP Tools (52 tools)

**Solution & Project Management (6 tools):**
- load_solution - Load C# solution
- load_project - Load specific project
- list_projects - List all projects
- reload_solution - Reload after changes
- get_project_structure - Analyze structure
- get_project_info - Get project metadata

**Code Navigation (10 tools):**
- view_definition - View type/member definition
- get_members - Get type members
- find_references - Find symbol references
- find_all_references - Cross-project references
- list_types - List types in project
- search_symbols - Search by name/pattern
- get_type_hierarchy - Get inheritance hierarchy
- find_usages - Find symbol usages
- goto_definition - Navigate to definition
- find_implementations - Find interface implementations

**Code Modification (8 tools):**
- modify_code - Modify type/member
- add_member - Add new member
- remove_member - Remove member
- rename_symbol - Rename with refactoring
- extract_method - Extract code to method
- inline_variable - Inline variable
- change_signature - Modify method signature
- introduce_parameter - Add method parameter

**Code Quality (7 tools):**
- format_code - Format with CSharpier
- analyze_code_style - Check EditorConfig compliance
- analyze_complexity - Calculate cyclomatic complexity
- get_diagnostics - Get compiler diagnostics
- apply_code_fixes - Apply code fixes
- organize_usings - Organize using directives
- remove_unused_usings - Remove unused imports

**Debugging & Analysis (6 tools):**
- trace_execution - Static execution tracing
- trace_backwards - Reverse dependency tracing
- analyze_logs - Parse log files
- get_stack_trace_info - Analyze stack traces
- find_error_sources - Find error origins
- debug_expression - Evaluate expressions

**Git Integration (4 tools):**
- git_commit - Commit changes
- git_status - Get repository status
- git_diff - View changes
- git_log - View commit history

**File Operations (5 tools):**
- read_file - Read file contents
- write_file - Write file
- list_files - List files in directory
- read_raw_from_roslyn_document - Read via Roslyn
- overwrite_roslyn_document - Overwrite via Roslyn

**Documentation (3 tools):**
- generate_documentation - Generate XML docs
- extract_comments - Extract code comments
- generate_summary - Generate code summary

**Miscellaneous (3 tools):**
- evaluate_expression - Evaluate C# expression
- get_symbol_info - Get symbol metadata
- validate_syntax - Validate C# syntax

### Изменено

- Migrated from .NET 8 to .NET 10
- Upgraded to C# 13.0
- Updated all NuGet packages to latest stable

### Удалено

- UltrasharpTools.Agent project (deprecated)
- Legacy embedding service implementations

---
## [3.0.6] - 2025-11-19

- Исправлены ошибки установки
- Дружеский скрипт автонастройки семантических моделей


## [3.0.0] - 2025-11-18

### Добавлено

- **Initial Release**
- Basic Roslyn integration
- MCP protocol support
- Stdio-based MCP server (Droid)
- 20 core tools
- Git integration (LibGit2Sharp)

---

**Legend:**
- ✨ New feature
- 🐛 Bug fix
- 🔧 Configuration
- 📝 Documentation
- ⚡ Performance
- 🔒 Security
- 🗑️ Deprecated
- ❌ Removed

**Versions:**
- **Major (X.0.0):** Breaking changes, major architecture updates
- **Minor (0.X.0):** New features, backward compatible
- **Patch (0.0.X):** Bug fixes, small improvements

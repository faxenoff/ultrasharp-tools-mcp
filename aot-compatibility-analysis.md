# AOT Compatibility Analysis - UltrasharpTools MCP Инструменты

## Критерии разделения

### ❌ НЕ совместимо с AOT
Инструменты требующие:
- **Roslyn** (Workspace, Compilation, Symbols, SyntaxTree)
- **Reflection.Emit** (динамическая генерация кода)
- **Dynamic assembly loading** (MetadataLoadContext)
- **MethodInfo/Type manipulation** в runtime

### ✅ Совместимо с AOT
Инструменты использующие только:
- **Файловые операции** (System.IO)
- **HTTP клиенты** (HttpClient, gRPC)
- **Текстовый парсинг** (Regex, string operations)
- **SQLite** (Microsoft.Data.Sqlite)
- **JSON сериализация** (System.Text.Json source generators)
- **Subprocess execution** (Process.Start)

---

## Разделение инструментов

### 🔴 Core Process (НЕ AOT) - Требуют Roslyn

Все инструменты использующие `ISolutionManager`, `ICodeAnalysisService`, `ICodeModificationService` и другие Roslyn-зависимые сервисы.

#### 1. **SolutionTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `ICodeAnalysisService`

**Инструменты**:
- `load_solution` - загрузка Roslyn Workspace
- `load_project` - загрузка проектов и символов

**Причина**: Roslyn Workspace, Compilation, MetadataLoadContext

---

#### 2. **AnalysisTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `ICodeAnalysisService`, `IComplexityAnalysisService`

**Инструменты**:
- `view_definition` - получение определения символа через Roslyn
- `get_members` - получение членов типа через Roslyn
- `list_implementations` - поиск реализаций интерфейсов
- `find_references` - поиск ссылок на символы
- `view_call_graph` - анализ графа вызовов
- `view_inheritance_chain` - цепочка наследования
- `get_all_subtypes` - вложенные типы
- `search_definitions` - поиск определений (Roslyn + Reflection)
- `analyze_complexity` - анализ сложности через Roslyn CFG

**Причина**: Все операции требуют Roslyn Compilation и Symbol API

---

#### 3. **ModificationTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `ICodeModificationService`, `IFormattingService`, `PreviewManager`

**Инструменты**:
- `modify_code` - перезапись кода через Roslyn
- `add_member` - добавление членов через Roslyn
- `rename_symbol` - переименование символов через Roslyn
- `move_member` - перемещение членов
- `find_and_replace` - regex замена с Roslyn валидацией
- `replace_all_references` - замена всех ссылок на символ
- `replace_references_by_pattern` - batch переименование
- `manage_usings` - управление using директивами
- `manage_attributes` - управление атрибутами

**Причина**: Roslyn SyntaxTree manipulation, Code Rewriting

---

#### 4. **DocumentTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `IDocumentOperationsService`

**Инструменты**:
- `read_file` - чтение через Roslyn Document
- `create_file` - создание с auto-formatting через Roslyn
- `overwrite_file` - перезапись с валидацией через Roslyn
- `list_file_entities` - получение типов из файла через Roslyn

**Причина**: Roslyn Document model, SyntaxTree parsing

---

#### 5. **FileOperationTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `ImportUpdateService`

**Инструменты**:
- `split_file` - разделение файла с Roslyn парсингом
- `synthesize_files` - объединение файлов с Roslyn

**Причина**: Roslyn SyntaxTree parsing и rewriting

---

#### 6. **QualityTools.cs** ❌ НЕ AOT
**Сервисы**: `IFormattingService`, `IDiagnosticService`, `ICodeFixService`

**Инструменты**:
- `format_code` - форматирование через Roslyn Formatter
- `analyze_code_style` - анализ через Roslyn Analyzers
- `apply_code_fixes` - применение фиксов через Roslyn CodeFixProviders

**Причина**: Roslyn Formatting, Analyzers, CodeFixes

---

#### 7. **ValidationTools.cs** ❌ НЕ AOT
**Сервисы**: `IDiagnosticService`, `IQuickLintService`

**Инструменты**:
- `validate_file` - валидация через Roslyn Analyzers
- `validate_directory` - batch валидация
- `compare_validation` - сравнение результатов

**Причина**: Roslyn Diagnostics API

---

#### 8. **TraceTools.cs** ❌ НЕ AOT
**Сервисы**: `IExecutionTraceService`, `IBacktraceService`, `ISymbolicExecutionService`

**Инструменты**:
- `trace_execution` - трассировка через Roslyn Control Flow Graph
- `trace_backwards` - обратная трассировка
- `analyze_path_feasibility` - символьное выполнение (Roslyn + Z3)
- `export_call_graph` - экспорт графа вызовов

**Причина**: Roslyn Control Flow Graph, Symbol analysis, Z3 interop

---

#### 9. **PatternSearchTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `SemanticSearchService`, `HybridSearchService`

**Инструменты**:
- `pattern_search` - hybrid search (entity mode требует Roslyn)

**Причина**: Entity search через Roslyn Symbols (хотя semantic mode может быть AOT)

---

#### 10. **TechnologyDetectionTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`

**Инструменты**:
- `detect_technology_stack` - анализ .csproj и NuGet через Roslyn

**Причина**: Roslyn MSBuild integration, Project loading

---

#### 11. **PackageTools.cs** ❌ НЕ AOT
**Сервисы**: `ISolutionManager`, `NuGetHttpService`

**Инструменты**:
- `add_package` - добавление NuGet пакетов с Roslyn reload

**Причина**: Roslyn Solution reload после изменения .csproj

---

### 🟢 Index/AI Process (МОЖЕТ быть AOT)

Инструменты работающие с векторными эмбеддингами, но БЕЗ прямого использования Roslyn.

#### 12. **SemanticAnalysisTools.cs** ⚠️ ЧАСТИЧНО AOT
**Сервисы**: `ISolutionManager` ❌, `SemanticSearchService` ✅, `ISemanticSimilarityService` ⚠️

**Инструменты**:
- `semantic_search` - векторный поиск (✅ AOT если через HTTP)
- `semantic_diff` - семантическое сравнение (⚠️ зависит от реализации)
- `find_duplicates` - поиск дубликатов (⚠️ зависит от реализации)
- `detect_code_clones` - детекция клонов (⚠️ зависит от реализации)

**Анализ**:
- ✅ `SemanticSearchService` - если эмбеддинги через HTTP (TEI/Ollama) - AOT OK
- ❌ `ISolutionManager` - требуется для получения кода символов - НЕ AOT
- ⚠️ `ISemanticSimilarityService` - зависит от реализации

**Вывод**: **Может быть AOT** если рефакторить для работы БЕЗ ISolutionManager (индекс в SQLite)

---

#### 13. **SemanticMergeTools.cs** ✅ МОЖЕТ быть AOT
**Сервисы**: `SemanticMergeService`

**Инструменты**:
- `semantic_merge` - 3-way merge с AI

**Анализ**:
- Использует `EmbeddingGenerator` (может быть HTTP к TEI/Ollama)
- Работает с файлами напрямую (не через Roslyn)
- Парсинг C# через собственный `CSharpParser` (не Roslyn)

**Вывод**: ✅ **AOT OK** если эмбеддинги через HTTP

---

### 🟡 Utility Process (AOT)

Инструменты БЕЗ Roslyn зависимостей.

#### 14. **LogTools.cs** ✅ AOT OK
**Сервисы**: `ILogAnalysisService`

**Инструменты**:
- `analyze_logs` - парсинг логов (Regex, текстовая обработка)

**Анализ**:
- Только текстовая обработка
- Regex парсинг
- Никакого Roslyn

**Вывод**: ✅ **Полностью AOT compatible**

---

#### 15. **SnapshotTools.cs** ✅ AOT OK
**Сервисы**: `VersionManager`

**Инструменты**:
- `create_snapshot` - создание снапшотов (файловые операции + Git)
- `rollback_snapshot` - откат к снапшоту
- `list_snapshots` - список снапшотов
- `cleanup_snapshots` - очистка старых снапшотов

**Анализ**:
- Файловые операции (System.IO)
- Git через subprocess (Process.Start)
- Никакого Roslyn

**Вывод**: ✅ **Полностью AOT compatible**

---

#### 16. **SystemTools.cs** ✅ AOT OK
**Сервисы**: `ISemanticModeProvider`

**Инструменты**:
- `get_capabilities` - информация о возможностях сервера

**Анализ**:
- Только чтение конфигурации
- HTTP health checks к эмбеддинг сервисам
- Никакого Roslyn

**Вывод**: ✅ **Полностью AOT compatible**

---

#### 17. **MiscTools.cs** ✅ AOT OK (если существует)
**Инструменты**: Request new tool

**Вывод**: ✅ **AOT OK** (логирование запросов)

---

## Итоговая статистика

| Категория | Количество | Инструментов | % от общего |
|-----------|------------|--------------|-------------|
| **🔴 НЕ AOT** (Core) | 11 классов | ~60 инструментов | **~85%** |
| **🟢 МОЖЕТ AOT** (Index/AI) | 2 класса | ~6 инструментов | **~10%** |
| **🟡 AOT OK** (Utility) | 3 класса | ~8 инструментов | **~5%** |

**Вывод**: **85% инструментов требуют Roslyn** и НЕ могут быть AOT.

---

## Рекомендации по архитектуре

### Вариант 1: Три процесса

```
┌─────────────────────────────────────┐
│ Micro-Proxy (AOT, множество)        │
│ - MCP stdio transport               │
│ - 10-20 MB RAM                      │
│ - Instant startup                   │
└─────────────────────────────────────┘
         │ gRPC
         ├──────────────┬──────────────┐
         ↓              ↓              ↓
┌─────────────────┐  ┌──────────────┐  ┌──────────────┐
│ Core Process    │  │ Index/AI     │  │ Utility      │
│ (НЕ AOT)        │  │ (AOT)        │  │ (AOT)        │
├─────────────────┤  ├──────────────┤  ├──────────────┤
│ 85% инструментов│  │ 10% инстр.   │  │ 5% инстр.    │
│ • SolutionTools │  │ • Semantic   │  │ • LogTools   │
│ • AnalysisTools │  │ • SemanticMrg│  │ • Snapshots  │
│ • ModifyTools   │  │              │  │ • System     │
│ • QualityTools  │  │              │  │              │
│ • TraceTools    │  │              │  │              │
│ • etc.          │  │              │  │              │
│                 │  │              │  │              │
│ 500 MB RAM      │  │ 200 MB RAM   │  │ 50 MB RAM    │
│ 33s startup     │  │ 2s startup   │  │ <1s startup  │
└─────────────────┘  └──────────────┘  └──────────────┘
```

**Плюсы**:
- ✅ Utility и Index/AI процессы AOT (быстрый старт, маленький размер)
- ✅ Shared Core между всеми клиентами (экономия памяти)
- ✅ Можно restart Utility без влияния на Core

**Минусы**:
- ❌ Только 15% инструментов получают AOT (мало пользы)
- ❌ Сложность inter-process routing

---

### Вариант 2: Два процесса (практичнее)

```
┌─────────────────────────────────────┐
│ Micro-Proxy (AOT, множество)        │
│ - MCP stdio transport               │
│ - Routing к Core                    │
│ - 10-20 MB RAM                      │
└─────────────────────────────────────┘
         │ gRPC + Shared Memory
         ↓
┌──────────────────────────────────────┐
│ Core Process (НЕ AOT, singleton)     │
├──────────────────────────────────────┤
│ ВСЕ инструменты (100%)               │
│ • Roslyn (85%)                       │
│ • Semantic/Index (10%)               │
│ • Utility (5%)                       │
│                                      │
│ 550 MB RAM                           │
│ 33s startup (один раз)               │
└──────────────────────────────────────┘
```

**Плюсы**:
- ✅ Простая архитектура (только 2 процесса)
- ✅ Proxy AOT (главная экономия памяти)
- ✅ Все инструменты в одном месте (проще отладка)
- ✅ 3 окна Claude Code: 60 MB proxy + 550 MB core = **610 MB** вместо 1.5 GB

**Минусы**:
- ❌ Core не AOT (но это OK, один инстанс)

**Рекомендация**: **Вариант 2** - максимальная польза при минимальной сложности.

---

### Вариант 3: Гибридный (будущее)

Начать с **Варианта 2**, потом **постепенно** вынести:

**Phase 1**: Proxy + Core (2 процесса)

**Phase 2**: Вынести **Index/AI** в отдельный процесс (3 процесса)
- Причина: Semantic indexing может быть медленным
- Польза: Можно restart без Core

**Phase 3**: Вынести **Trace/Symbolic** в отдельный процесс (4 процесса)
- Причина: Z3 solver может зависать
- Польза: Kill зависших задач

**Phase 4**: Вынести **Git Service** в отдельный процесс (5 процессов)
- Причина: Git операции могут быть долгими
- Польза: Параллельное выполнение Git + Roslyn

---

## Детальная разбивка сервисов

### Core Process - Roslyn Services (НЕ AOT)

| Сервис | Причина НЕ AOT | Используется в |
|--------|----------------|----------------|
| `ISolutionManager` | Roslyn Workspace, MetadataLoadContext | 12 Tool классов |
| `ICodeAnalysisService` | Roslyn Compilation, Symbols | AnalysisTools |
| `ICodeModificationService` | Roslyn SyntaxTree rewriting | ModificationTools |
| `IComplexityAnalysisService` | Roslyn Control Flow Graph | AnalysisTools |
| `IExecutionTraceService` | Roslyn CFG | TraceTools |
| `IBacktraceService` | Roslyn Symbol analysis | TraceTools |
| `ISymbolicExecutionService` | Roslyn + Z3 interop | TraceTools |
| `IFormattingService` | Roslyn Formatter | QualityTools, ModificationTools |
| `IDiagnosticService` | Roslyn Analyzers | QualityTools, ValidationTools |
| `ICodeFixService` | Roslyn CodeFixProviders | QualityTools |
| `IQuickLintService` | Roslyn quick checks | ValidationTools |
| `ImportUpdateService` | Roslyn using directives | FileOperationTools, ModificationTools |
| `IDocumentOperationsService` | Roslyn Document model | DocumentTools |
| `PreviewManager` | Roslyn diff + formatting | ModificationTools |

**Итого**: 14 сервисов требуют Roslyn

---

### Index/AI Process - Может быть AOT

| Сервис | AOT? | Зависимости | Комментарий |
|--------|------|-------------|-------------|
| `SemanticSearchService` | ✅ Да | `CodeSemanticIndexer`, `VectorStore` | Если эмбеддинги через HTTP |
| `HybridSearchService` | ⚠️ Частично | `SemanticSearchService`, `ISolutionManager` | Нужен рефакторинг |
| `SemanticMergeService` | ✅ Да | `EmbeddingGenerator`, custom parsers | Не использует Roslyn |
| `ISemanticSimilarityService` | ⚠️ Частично | Текущая реализация базовая | Может быть AOT |
| `CodeSemanticIndexer` | ✅ Да | `VectorStore`, `EmbeddingGenerator` | AOT OK |
| `VectorStore` | ✅ Да | SQLite | AOT OK |
| `EmbeddingGenerator` | ✅ Да | HTTP к TEI/Ollama | AOT OK |

**Проблема**: `HybridSearchService` и некоторые semantic tools требуют `ISolutionManager` для получения кода.

**Решение**: Рефакторинг для использования индекса в SQLite вместо прямого обращения к Roslyn.

---

### Utility Process - Полностью AOT

| Сервис | Зависимости | Инструменты |
|--------|-------------|-------------|
| `ILogAnalysisService` | Regex, File I/O | LogTools |
| `VersionManager` | File I/O, Git subprocess | SnapshotTools |
| `ISemanticModeProvider` | HTTP health checks | SystemTools |
| `NuGetHttpService` | HTTP к nuget.org | PackageTools* |

*Примечание: `PackageTools` также требует `ISolutionManager` для reload, поэтому остается в Core.

**Итого**: 3 полностью независимых сервиса (AOT OK)

---

## Практический план

### Рекомендация: Вариант 2 (Proxy + Core)

**Причины**:
1. **85% инструментов требуют Roslyn** - разделение даст мало пользы
2. **Главная экономия** - в Proxy процессе (AOT, 10-20 MB)
3. **Простота** - только 2 процесса вместо 5
4. **Быстрый результат** - можно реализовать за 2-3 недели

### Что даст:

**Сейчас**:
- 3 окна Claude Code = **1500 MB** (3×500)

**После Proxy**:
- 3 proxy = **60 MB** (3×20)
- 1 core = **550 MB**
- **Итого: 610 MB** (~60% экономии)

**Бонус**:
- Proxy startup: **<100ms** (AOT)
- Core startup: **33s** (один раз, в фоне)
- Пользователь видит быстрый старт

### Будущие оптимизации (опционально):

Если нужно еще больше оптимизации:
1. Вынести Index/AI (10% инструментов) → +200 MB экономии при разделении
2. Вынести Z3/Trace → изоляция долгих операций
3. Lazy load проектов в Core → быстрее инициализация

---

## Вывод

**Разделение имеет смысл**, но:
- ✅ **Обязательно**: Micro-Proxy (AOT) - 95% пользы
- ⚠️ **Опционально**: Отдельные процессы для Index/Trace - 5% пользы
- ❌ **Не стоит**: Разделять все 18 классов инструментов - overengineering

**Начать с Proxy + Core**, потом по необходимости добавлять процессы.

# Архитектурный анализ: Разделение UltrasharpTools на отдельные процессы

## Executive Summary

**Проблема**:
- 3 окна Claude Code = **1.5 GB памяти** (3 инстанса по 500 MB)
- Индексация **блокирует** Roslyn операции (критично для больших проектов)
- Mono-repos: индексация **25 минут** блокирует всю работу

**Решение**: **3 процесса** (Proxy + Core + VectorDB)

**Результат**:
- 3 окна = **610 MB** (~60% экономии)
- Proxy startup **<100ms**
- **Индексация НЕ блокирует Core** (параллелизм)
- Возможность remote VectorDB с GPU (25 мин → 2 мин)

**Рекомендация**: Вариант B (3 процесса) - **обязательно для больших проектов**

**Критично**: БЕЗ разделения VectorDB большие проекты **непригодны** для работы

---

## 🔴 Обнаруженная критическая проблема

### Дублирование загрузки решения

**ПРОБЛЕМА**: В текущей архитектуре load_solution вызывается дважды одновременно:

1. **Program.cs:937** - Фоновая загрузка при запуске:
```csharp
_ = Task.Run(async () => {
    await solutionManager.LoadSolutionAsync(solutionPathCopy, CancellationToken.None);
});
```

2. **MCP инструмент** - Claude Code вызывает load_solution

**ПОСЛЕДСТВИЯ**:
- Двойная загрузка 462,263 символов
- Дублирование компиляций (каждый проект грузится 2 раза)
- Зависание на 5+ минут
- Двойное потребление памяти
- Race conditions в кешах
- Дублирование строк в логе

**СРОЧНОЕ РЕШЕНИЕ**:
1. Убрать фоновую загрузку из Program.cs (строки 934-986)
2. ИЛИ добавить блокировку/флаг в SolutionManager чтобы повторные вызовы ждали завершения первого
3. ИЛИ добавить проверку `if (!solutionManager.IsLoaded)` перед фоновой загрузкой

---

## 📊 Анализ текущей кодовой базы

### Статистика сервисов (54 total)

| Категория | Количество | % от всех |
|-----------|------------|-----------|
| **🔴 Roslyn-зависимые** | 23 | 43% |
| **🟡 Reflection.Metadata** | 3 | 6% |
| **🟢 Z3 (адаптируемо)** | 1 | 2% |
| **✅ AOT OK** | 25 | **46%** |
| **❓ Неясно** | 2 | 4% |

### Статистика инструментов (65 total)

| Категория | Инструменты | % от всех |
|-----------|-------------|-----------|
| **🔴 НЕ AOT** (требуют Roslyn) | ~55 | **85%** |
| **🟢 МОЖЕТ AOT** (Semantic/Index) | ~6 | 10% |
| **🟡 AOT OK** (Utility) | ~4 | 5% |

### Roslyn-зависимые сервисы (23 шт, НЕ AOT)

**Core Roslyn Services**:
- `SolutionManager` - Workspace, MSBuild, Compilation
- `CodeAnalysisService` - Symbols, SyntaxTree
- `CodeModificationService` - SyntaxTree rewriting
- `FormattingService` - Roslyn Formatter
- `DiagnosticService` - Roslyn Analyzers
- `CodeFixService` - CodeFixProviders
- `QuickLintService` - Quick checks
- `DocumentOperationsService` - Document model
- `ImportUpdateService` - Using directives
- `FuzzyFqnLookupService` - Symbol search
- `SymbolResolver` - Symbol resolution

**Advanced Analysis Services**:
- `ComplexityAnalysisService` - Control Flow Graph
- `ExecutionTraceService` - CFG tracing
- `BacktraceService` - Symbol + CFG analysis
- `SymbolicExecutionService` - CFG + Z3
- `AsyncStateMachineAnalyzer` - Async CFG
- `LinqQueryAnalyzer` - LINQ IOperation
- `SemanticSimilarityService` - Symbol comparison

**Caching Services**:
- `FastSymbolIndex`, `SymbolCacheManager`
- `SemanticModelCacheService`, `SyntaxTreeCacheService`, `AnalysisCacheService`

### AOT-совместимые сервисы (25 шт)

**Git / File I/O**:
- `GitCliService`, `NoOpGitService`
- `EditorConfigProvider`, `EditorConfigGenerator`

**Logging / Text Analysis**:
- `LogAnalysisService`, `FuzzyStackTraceMatcher`, `CallerInfoConverter`

**HTTP / NuGet**:
- `NuGetHttpService`, `LegacyNuGetPackageReader`*

**Semantic / Embedding** (если через HTTP):
- `SemanticServiceHealthCheck`, `EmbeddingServiceHealthChecker`
- `SemanticConfigManager`, `EmbeddingConfigValidator`, `AutoConfigurationService`

**Utilities**:
- `CodebaseSizeDetector`, `CodebaseLanguageDetector`, `CallGraphExporter`, `PathInfo`

*NuGet.Protocol может требовать проверки AOT совместимости

### Roslyn-зависимые инструменты (55 шт, 85%)

**Solution/Analysis Tools**:
- `SolutionTools` (2): load_solution, load_project
- `AnalysisTools` (9): view_definition, get_members, find_references, view_call_graph, etc.
- `TraceTools` (4): trace_execution, trace_backwards, analyze_path_feasibility, export_call_graph

**Modification Tools**:
- `ModificationTools` (9): modify_code, add_member, rename_symbol, find_and_replace, etc.
- `FileOperationTools` (2): split_file, synthesize_files

**Quality/Validation Tools**:
- `QualityTools` (3): format_code, analyze_code_style, apply_code_fixes
- `ValidationTools` (3): validate_file, validate_directory, compare_validation

**Document/Tech Tools**:
- `DocumentTools` (4): read_file, create_file, overwrite_file, list_file_entities
- `TechnologyDetectionTools` (1): detect_technology_stack
- `PackageTools` (1): add_package (требует Roslyn reload)
- `PatternSearchTools` (1): pattern_search (entity mode требует Roslyn)

### AOT-совместимые инструменты (10 шт, 15%)

**Utility Tools** (уже AOT):
- `SystemTools` (1): get_capabilities
- `LogTools` (1): analyze_logs
- `SnapshotTools` (4): create/rollback/list/cleanup snapshots

**Semantic Tools** (адаптируемо):
- `SemanticMergeTools` (2): semantic_merge, get_semantic_merge_info - уже использует свой парсер!
- `SemanticAnalysisTools` (5): semantic_search, semantic_diff, find_duplicates, detect_code_clones
  - Требует рефакторинг: убрать ISolutionManager, использовать индекс в SQLite

---

## 🎯 Рекомендуемая архитектура

### Вариант 1: Минимальный (простота > оптимизация)

**НЕ выносить** VectorDB, только Proxy + Server

```
┌─────────────────────────────────────┐
│ UltrasharpTools.Proxy (✅ AOT)      │
│ - Множество инстансов (по окну)    │
│ - MCP stdio transport               │
│ - gRPC клиент                       │
│ - Routing к Server                  │
│ - 10-20 MB RAM                      │
│ - Startup: <100ms                   │
└─────────────────────────────────────┘
         │ gRPC + Shared Memory
         ↓
┌──────────────────────────────────────┐
│ UltrasharpTools.Server (❌ НЕ AOT)  │
│ - Singleton процесс                  │
│ - gRPC server                        │
│ - Roslyn Services (23 шт)            │
│ - Semantic Services (Vector DB)      │
│ - Utility Services (25 шт)           │
│ - ВСЕ Tools (65 шт)                  │
│ - 550 MB RAM                         │
│ - Startup: 33s (один раз)            │
└──────────────────────────────────────┘
```

**Плюсы**:
- ✅ Простая архитектура (2 процесса)
- ✅ Минимальная IPC сложность
- ✅ Быстрая реализация (2-3 недели)

**Минусы**:
- ❌ Индексация блокирует Server (если медленная)
- ❌ Нельзя restart Index без Core
- ❌ Vector DB в том же процессе (+200 MB RAM)

**Структура проектов**:
```
UltrasharpTools.sln
│
├── UltrasharpTools.Contracts (✅ AOT OK)
│   ├── Grpc/
│   │   ├── roslyn_service.proto
│   │   ├── semantic_service.proto
│   │   └── tool_service.proto
│   └── Models/
│       └── Shared DTOs
│
├── UltrasharpTools.Proxy (✅ AOT)
│   ├── Program.cs
│   ├── McpStdioTransport.cs
│   ├── GrpcClients/
│   │   ├── RoslynServiceClient.cs
│   │   ├── SemanticServiceClient.cs
│   │   └── ToolServiceClient.cs
│   └── Dependencies:
│       ├── ModelContextProtocol
│       ├── Grpc.Net.Client
│       └── UltrasharpTools.Contracts
│
└── UltrasharpTools.Server (❌ НЕ AOT)
    ├── Program.cs
    ├── GrpcServices/
    │   ├── RoslynServiceImpl.cs
    │   ├── SemanticServiceImpl.cs
    │   └── ToolServiceImpl.cs
    ├── Tools/ (ВСЕ 65 инструментов)
    ├── Services/ (ВСЕ 54 сервиса)
    └── Dependencies:
        ├── Microsoft.CodeAnalysis.*
        ├── UltrasharpTools.Contracts
        └── Все существующие зависимости
```

**Результат**:
- 3 окна Claude Code: **610 MB** (3×20 + 550)
- Proxy startup: **<100ms**
- Server startup: **33s** (один раз, в фоне)
- **Экономия памяти: 60%** (1.5 GB → 610 MB)

---

### Вариант 1.5: С индексером (РЕКОМЕНДУЕТСЯ, если индексация критична)

**Вынести** Semantic Index в отдельный процесс

```
┌─────────────────────────────────────┐
│ UltrasharpTools.Proxy (✅ AOT)      │
│ - 10-20 MB RAM                      │
│ - Startup: <100ms                   │
└─────────────────────────────────────┘
         │ gRPC
         ├──────────────┬──────────────┐
         ↓              ↓              ↓
┌──────────────────┐  ┌───────────────────┐
│ Core (НЕ AOT)    │  │ VectorDB (✅ AOT*) │
│ - Roslyn (23)    │  │ - Vector DB       │
│ - Utilities (25) │  │ - Semantic Search │
│ - 350 MB RAM     │  │ - CodeIndexer     │
│ - 33s startup    │  │ - 200 MB RAM      │
└──────────────────┘  │ - 2s startup      │
                      └───────────────────┘
                              ↓ HTTP
                      ┌───────────────────┐
                      │ TEI/Ollama        │
                      │ Embedding Service │
                      └───────────────────┘
```

**Структура проектов**:
```
UltrasharpTools.sln
│
├── UltrasharpTools.Contracts (✅ AOT)
│
├── UltrasharpTools.Proxy (✅ AOT)
│
├── UltrasharpTools.Core (❌ НЕ AOT)
│   ├── Roslyn Services (23 шт)
│   ├── Utility Services (25 шт)
│   └── Roslyn Tools (55 шт)
│
└── UltrasharpTools.VectorDB (✅ AOT*)
    ├── Semantic Services
    │   ├── VectorStore
    │   ├── CodeSemanticIndexer
    │   ├── SemanticSearchService (БЕЗ ISolutionManager!)
    │   └── EmbeddingGenerator
    ├── Semantic Tools
    │   ├── semantic_search
    │   ├── semantic_diff
    │   ├── find_duplicates
    │   └── detect_code_clones
    └── Dependencies:
        ├── Microsoft.Data.Sqlite ✅
        └── HTTP клиенты ✅
```

**Рефакторинг SemanticSearchService**:

```csharp
// БЫЛО (требует ISolutionManager):
public class SemanticSearchService
{
    private readonly CodeSemanticIndexer _indexer;
    private readonly ISolutionManager _solutionManager; // ❌ Roslyn dependency

    public async Task<SearchResult> SearchAsync(string query)
    {
        var vectors = await _indexer.SearchVectorsAsync(query);

        // Получение source code через Roslyn ❌
        foreach (var result in vectors)
        {
            var symbol = await _solutionManager.GetSymbolAsync(result.Fqn);
            result.SourceCode = symbol.GetSourceCode();
        }
    }
}

// СТАЛО (БЕЗ ISolutionManager):
public class SemanticSearchService
{
    private readonly CodeSemanticIndexer _indexer;
    private readonly VectorStore _vectorStore; // Только SQLite ✅

    public async Task<SearchResult> SearchAsync(string query)
    {
        var vectors = await _indexer.SearchVectorsAsync(query);

        // Source code УЖЕ в индексе! ✅
        foreach (var result in vectors)
        {
            // VectorStore содержит:
            // - FQN
            // - Vector embedding
            // - Source code snippet (для preview)
            // - File path + line number
            result.SourceCode = await _vectorStore.GetSourceCodeAsync(result.Fqn);
        }
    }
}
```

**Изменения в индексации**:

```csharp
// При индексации СОХРАНЯТЬ source code в Vector DB
public class CodeSemanticIndexer
{
    public async Task IndexSymbolAsync(ISymbol symbol)
    {
        var sourceCode = symbol.DeclaringSyntaxReferences
            .FirstOrDefault()?.GetSyntax().ToFullString();

        var embedding = await _embeddingGenerator.GenerateAsync(sourceCode);

        await _vectorStore.InsertAsync(new VectorEntry
        {
            Fqn = symbol.ToDisplayString(),
            Embedding = embedding,
            SourceCode = sourceCode, // ✅ Сохраняем!
            FilePath = symbol.Locations.First().SourceTree.FilePath,
            LineNumber = symbol.Locations.First().GetLineSpan().StartLinePosition.Line
        });
    }
}
```

**Плюсы**:
- ✅ Индексация НЕ блокирует Core (разные процессы)
- ✅ Можно restart VectorDB без Core
- ✅ Vector DB изолирован (200 MB в отдельном процессе)
- ✅ VectorDB **МОЖЕТ быть AOT** (если убрать ISolutionManager)
- ✅ Параллельная индексация и Roslyn операции
- ✅ Можно scale VectorDB отдельно (больше RAM/CPU)

**Минусы**:
- ❌ Дополнительный процесс (3 вместо 2)
- ❌ Рефакторинг SemanticSearchService
- ❌ Source code в Vector DB (дублирование, +размер БД)
- ❌ Синхронизация между Core и VectorDB при изменениях
- ❌ Дополнительная неделя разработки

**Когда выносить VectorDB**:

✅ **ОБЯЗАТЕЛЬНО ДЕЛАТЬ** если:
- **Большие проекты**: >100 файлов, >50K LOC
- **Очень большие проекты**: >1000 файлов, >500K LOC (mono-repos)
- Индексация занимает >30s (блокирует работу)
- Vector DB >500 MB (много проектов)
- Нужна независимая индексация в фоне
- Планируется remote VectorDB (на отдельной машине)

❌ **МОЖНО НЕ ДЕЛАТЬ** если:
- Маленькие проекты: <50 файлов, <10K LOC
- Индексация быстрая (<10s)
- Vector DB <200 MB
- Только локальное использование одного проекта

**Для вашего случая** (большие + очень большие проекты):
- ✅ **Вариант B - единственный правильный выбор**
- ✅ Индексация в фоне НЕ блокирует работу
- ✅ Можно масштабировать VectorDB независимо
- ✅ В будущем: VectorDB на отдельной машине с GPU для эмбеддингов

**Результат**:
- 3 окна Claude Code: **610 MB** (3×20 + 350 + 200)
- Memory экономия: **60%** (same as Вариант 1)
- VectorDB startup: **2s** (vs 33s Core)
- VectorDB **AOT возможен** (если рефакторинг)

---

### Масштабирование для больших и очень больших проектов

#### Сценарий 1: Большие проекты (100-500 файлов, 50-250K LOC)

**Проблемы**:
- Индексация: 30-60s
- Vector DB: 200-500 MB
- Блокирует работу при полной переиндексации

**Решение**: VectorDB в отдельном процессе (Вариант B)

**Оптимизации**:
1. **Incremental indexing**: только измененные файлы
2. **Background indexing**: не блокирует Core
3. **Lazy loading**: индексация по требованию

```csharp
// Incremental indexing
public async Task OnFileChangedAsync(string filePath)
{
    // Только один файл, не весь проект
    var symbols = await ParseFileAsync(filePath);
    await _vectorStore.UpdateAsync(symbols);
}
```

#### Сценарий 2: Очень большие проекты (>1000 файлов, >500K LOC)

**Проблемы**:
- Индексация: 5-15 минут
- Vector DB: 1-5 GB
- Эмбеддинги: >100K вызовов к модели
- RAM: Core + VectorDB = 800 MB+

**Решение**: Remote VectorDB + оптимизации

**Архитектура для mono-repos**:

```
┌─────────────────┐
│ Proxy (AOT)     │  ← Локально на dev машине
│ - 10-20 MB      │
└─────────────────┘
       │ gRPC (localhost)
       ↓
┌─────────────────┐
│ Core            │  ← Локально на dev машине
│ - Roslyn        │
│ - 350 MB        │
└─────────────────┘
       │ gRPC (network)
       ↓
┌──────────────────────────────┐
│ VectorDB (Remote)             │  ← На отдельном сервере!
│ - Vector DB (5 GB)           │
│ - GPU для эмбеддингов        │
│ - 2-4 GB RAM                 │
│ - Параллельная индексация    │
└──────────────────────────────┘
```

**Оптимизации для очень больших проектов**:

1. **Distributed indexing**: несколько VectorDB процессов
```csharp
// Шардирование по проектам
Indexer1: ProjectA, ProjectB
Indexer2: ProjectC, ProjectD
```

2. **GPU acceleration**: эмбеддинги на GPU
```
TEI + NVIDIA GPU: 100-1000x быстрее CPU
Индексация: 15 минут → 1-2 минуты
```

3. **Persistent cache**: не переиндексировать неизмененные файлы
```csharp
public async Task IndexProjectAsync(Project project)
{
    var changedFiles = await GetChangedFilesAsync(project);
    // Только измененные файлы
    await IndexFilesAsync(changedFiles);
}
```

4. **Batch embeddings**: 100 файлов → 1 вызов
```csharp
// Вместо 100 вызовов
foreach (var file in files)
{
    await GetEmbeddingAsync(file); // ❌
}

// 1 batch вызов
var embeddings = await GetEmbeddingBatchAsync(files); // ✅
```

5. **Compression**: Vector DB сжатие
```sql
-- SQLite compression
PRAGMA journal_mode = WAL;
PRAGMA page_size = 8192;
VACUUM;
-- Экономия: 5 GB → 2-3 GB
```

**Бенчмарки для очень больших проектов**:

| Проект | Файлов | LOC | Индексация (локально) | Индексация (remote GPU) | Vector DB |
|--------|--------|-----|-----------------------|-------------------------|-----------|
| **Small** | 50 | 10K | 5s | 1s | 50 MB |
| **Medium** | 200 | 100K | 30s | 5s | 200 MB |
| **Large** | 500 | 250K | 2min | 15s | 500 MB |
| **Very Large** | 1000 | 500K | 5min | 30s | 1 GB |
| **Mono-repo** | 5000 | 2M | 25min | 2min | 5 GB |

**Критический порог**: >500K LOC → Remote VectorDB обязательно

---

### Вариант 2: Полное разделение Services (НЕ РЕКОМЕНДУЕТСЯ)

**Разделить** на UltrasharpTools.Core и UltrasharpTools.Roslyn

**Проблемы**:
- ❌ Большой refactoring (2-3 недели)
- ❌ Circular dependencies между Services
- ❌ 46% сервисов AOT - хорошо, но не критично
- ❌ Только 15% инструментов AOT - мало пользы
- ❌ Сложность поддержки двух кодовых баз

**Дополнительная польза**: Незначительная (Server все равно НЕ AOT из-за Roslyn)

**Вывод**: **Не стоит усилий**

---

## 🚀 План реализации

### Phase 0: Срочный фикс (1 день)

**Цель**: Исправить критическую проблему дублирования

**Задачи**:
1. Убрать фоновую загрузку из Program.cs:934-986
2. Добавить флаг в SolutionManager для однократной загрузки
3. Тестирование

**Файлы**:
- `UltrasharpTools.Droid/Program.cs`
- `UltrasharpTools.Tools/Services/SolutionManager.cs`

---

### Phase 1: gRPC Contracts (3-4 дня)

**Цель**: Определить gRPC контракты для всех операций

**Задачи**:
1. Создать проект UltrasharpTools.Contracts
2. Написать .proto файлы для всех MCP инструментов:
   - `roslyn_service.proto` - Roslyn операции (load_solution, view_definition, modify_code, etc.)
   - `semantic_service.proto` - Semantic операции (semantic_search, semantic_diff, etc.)
   - `utility_service.proto` - Utility операции (analyze_logs, snapshots, etc.)
3. Code generation (Google.Protobuf)
4. Определить стратегию для больших данных (Shared Memory keys)

**Пример proto**:
```protobuf
service RoslynService {
  rpc LoadSolution(LoadSolutionRequest) returns (LoadSolutionResponse);
  rpc ViewDefinition(ViewDefinitionRequest) returns (ViewDefinitionResponse);
  rpc FindReferences(FindReferencesRequest) returns (stream ReferenceInfo);
  rpc ModifyCode(ModifyCodeRequest) returns (ModifyCodeResponse);
  // ... все Roslyn инструменты
}

message ViewDefinitionRequest {
  string fully_qualified_name = 1;
}

message ViewDefinitionResponse {
  string source_code = 1;
  string file_path = 2;
  int32 line_number = 3;
  // Для больших результатов:
  string shared_memory_key = 4; // если source_code > 1MB
}
```

**Оптимизация для больших данных**:
```protobuf
message LargeDataResponse {
  oneof data_location {
    string inline_data = 1;          // Если < 100 KB
    string shared_memory_key = 2;    // Если > 100 KB
  }
  int32 data_size = 3;
}
```

---

### Phase 2: Server Process (5-7 дней)

**Цель**: Создать gRPC сервер с всеми существующими инструментами

**Задачи**:
1. Создать проект UltrasharpTools.Server
2. Настроить gRPC server (Kestrel + Unix Domain Sockets)
3. Реализовать gRPC service implementations:
   - `RoslynServiceImpl` - обертка над существующими Tools
   - `SemanticServiceImpl` - semantic tools
   - `UtilityServiceImpl` - utility tools
4. Добавить Shared Memory support для больших ответов
5. Миграция DI контейнера из Droid
6. Тестирование

**Структура Server**:
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Существующие DI регистрации из Droid
builder.Services.WithUltrasharpToolsServices(...);
builder.Services.WithSemanticRag(...);

// gRPC
builder.Services.AddGrpc();

var app = builder.Build();

// gRPC endpoints
app.MapGrpcService<RoslynServiceImpl>();
app.MapGrpcService<SemanticServiceImpl>();
app.MapGrpcService<UtilityServiceImpl>();

// Unix Domain Sockets для low latency
app.Run("unix:///tmp/ultrasharp-server.sock");
```

**gRPC Service Implementation**:
```csharp
public class RoslynServiceImpl : RoslynService.RoslynServiceBase
{
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeAnalysisService _codeAnalysis;
    private readonly SharedMemoryManager _sharedMemory;

    public override async Task<ViewDefinitionResponse> ViewDefinition(
        ViewDefinitionRequest request,
        ServerCallContext context)
    {
        // Вызов существующего инструмента
        var result = await AnalysisTools.ViewDefinition(
            _solutionManager,
            _codeAnalysis,
            logger: null,
            fullyQualifiedSymbolName: request.FullyQualifiedName
        );

        var sourceCode = result.ToString();

        // Если результат большой - использовать Shared Memory
        if (sourceCode.Length > 100_000)
        {
            var key = await _sharedMemory.WriteAsync(sourceCode);
            return new ViewDefinitionResponse
            {
                SharedMemoryKey = key,
                DataSize = sourceCode.Length
            };
        }

        return new ViewDefinitionResponse
        {
            InlineData = sourceCode
        };
    }
}
```

---

### Phase 3: Proxy Process (4-5 дней)

**Цель**: Создать легковесный Proxy с MCP stdio транспортом

**Задачи**:
1. Создать проект UltrasharpTools.Proxy
2. Настроить MCP stdio transport (из Droid)
3. Создать gRPC клиенты для всех сервисов
4. Реализовать маршрутизацию MCP → gRPC
5. Добавить Shared Memory чтение
6. Connection pooling и error handling
7. AOT публикация (PublishAot=true)

**Структура Proxy**:
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// gRPC клиенты
builder.Services.AddGrpcClient<RoslynService.RoslynServiceClient>(o =>
{
    o.Address = new Uri("unix:///tmp/ultrasharp-server.sock");
});

// MCP server
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithProxyTools(); // Proxy инструменты

// Enable AOT
<PublishAot>true</PublishAot>
<InvariantGlobalization>true</InvariantGlobalization>

await builder.Build().RunAsync();
```

**Proxy MCP Tool**:
```csharp
[McpServerTool(Name = "view_definition")]
public static async Task<object> ViewDefinition(
    RoslynService.RoslynServiceClient client,
    SharedMemoryManager sharedMemory,
    string fullyQualifiedSymbolName,
    CancellationToken cancellationToken = default)
{
    var request = new ViewDefinitionRequest
    {
        FullyQualifiedName = fullyQualifiedSymbolName
    };

    var response = await client.ViewDefinitionAsync(request, cancellationToken: cancellationToken);

    // Если данные в Shared Memory
    if (!string.IsNullOrEmpty(response.SharedMemoryKey))
    {
        var sourceCode = await sharedMemory.ReadAsync(response.SharedMemoryKey);
        return new { sourceCode, filePath = "", lineNumber = 0 };
    }

    return new { sourceCode = response.InlineData, filePath = "", lineNumber = 0 };
}
```

**AOT оптимизации**:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  <IlcOptimizationPreference>Speed</IlcOptimizationPreference>
  <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
</PropertyGroup>
```

---

### Phase 4: Shared Memory Manager (2-3 дня)

**Цель**: Реализовать эффективную передачу больших данных

**Задачи**:
1. Создать SharedMemoryManager
2. Write/Read API
3. TTL и auto-cleanup
4. Reference counting
5. Тестирование на всех платформах (Windows, Linux, macOS)

**Реализация**:
```csharp
public class SharedMemoryManager : IDisposable
{
    private readonly ConcurrentDictionary<string, MemoryMappedFile> _buffers = new();

    public async Task<string> WriteAsync(string data)
    {
        var key = $"ultrasharp-{Guid.NewGuid():N}";
        var bytes = Encoding.UTF8.GetBytes(data);

        var mmf = MemoryMappedFile.CreateNew(key, bytes.Length);
        using var accessor = mmf.CreateViewAccessor();
        accessor.WriteArray(0, bytes, 0, bytes.Length);

        _buffers[key] = mmf;

        // Auto-cleanup after 60 seconds
        _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ =>
        {
            if (_buffers.TryRemove(key, out var buffer))
            {
                buffer.Dispose();
            }
        });

        return key;
    }

    public async Task<string> ReadAsync(string key)
    {
        var mmf = MemoryMappedFile.OpenExisting(key);
        using var accessor = mmf.CreateViewAccessor();

        var bytes = new byte[accessor.Capacity];
        accessor.ReadArray(0, bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(bytes);
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers.Values)
        {
            buffer.Dispose();
        }
        _buffers.Clear();
    }
}
```

---

### Phase 5: Deployment & Testing (3-4 дня)

**Цель**: Упаковка, документация, тестирование

**Задачи**:
1. Single installer для Proxy + Server
2. systemd/Windows Service для Server
3. Health checks и auto-restart
4. Централизованное логирование
5. E2E тестирование
6. Performance benchmarks
7. Документация

**Deployment структура**:
```
/opt/ultrasharp/
├── bin/
│   ├── ultrasharp-proxy           # AOT binary, 5-10 MB
│   ├── ultrasharp-server           # .NET binary, 50+ MB
│   └── ...dependencies...
├── config/
│   ├── proxy.json
│   └── server.json
├── logs/
│   └── ultrasharp-server.log
└── systemd/
    └── ultrasharp-server.service
```

**systemd service**:
```ini
[Unit]
Description=UltrasharpTools Server
After=network.target

[Service]
Type=notify
ExecStart=/opt/ultrasharp/bin/ultrasharp-server
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

**Health check**:
```csharp
// Proxy проверяет Server при запуске
var channel = GrpcChannel.ForAddress("unix:///tmp/ultrasharp-server.sock");
var client = new Health.HealthClient(channel);

try
{
    var response = await client.CheckAsync(new HealthCheckRequest());
    if (response.Status != HealthCheckResponse.Types.ServingStatus.Serving)
    {
        // Start server
        Process.Start("/opt/ultrasharp/bin/ultrasharp-server");
        await Task.Delay(5000); // Wait for startup
    }
}
catch (RpcException)
{
    // Server not running, start it
    Process.Start("/opt/ultrasharp/bin/ultrasharp-server");
}
```

---

## 📈 Метрики успеха

### Базовые метрики (Вариант B: 3 процесса)

| Метрика | Сейчас | После | Улучшение |
|---------|--------|-------|-----------|
| **Memory (1 окно)** | 500 MB | 20 MB (proxy) + 350 MB (core) + 200 MB (vectordb) | N/A |
| **Memory (3 окна)** | 1500 MB | 60 MB + 350 MB + 200 MB = **610 MB** | **60%** ⬇️ |
| **Cold start (Proxy)** | 33s | **<100ms** | **330x** ⚡ |
| **Cold start (Core)** | 33s | 33s (один раз) | Same |
| **Cold start (VectorDB)** | N/A | **2s** (фон) | N/A |
| **Warm start** | 33s | <100ms | **330x** ⚡ |
| **Binary size (Proxy)** | 150 MB | **5-10 MB** (AOT) | **93%** ⬇️ |
| **Binary size (VectorDB)** | N/A | **15-20 MB** (AOT) | N/A |
| **IPC overhead** | 0 | 0.1-0.5ms | Negligible |

### Метрики индексации

| Размер проекта | Файлов | LOC | Индексация (монолит) | Индексация (VectorDB) | Блокировка Core |
|----------------|--------|-----|----------------------|----------------------|-----------------|
| **Small** | 50 | 10K | 5s (блокирует) | 5s | ❌ НЕТ |
| **Medium** | 200 | 100K | 30s (блокирует) | 30s | ❌ НЕТ |
| **Large** | 500 | 250K | 2min (блокирует) | 2min | ❌ НЕТ |
| **Very Large** | 1000 | 500K | 5min (блокирует) | 5min | ❌ НЕТ |
| **Mono-repo** | 5000 | 2M | 25min (блокирует) | 25min (или 2min с GPU) | ❌ НЕТ |

**Главное улучшение**: Roslyn операции **НЕ блокируются** индексацией!

---

## 🔬 Межпроцессное взаимодействие

### gRPC Transport

**Выбор**: Unix Domain Sockets (Linux/macOS), Named Pipes (Windows)

**Преимущества**:
- Латентность: 0.03-0.05ms (vs 0.3ms TCP localhost)
- Пропускная способность: 5 GB/s
- Нет network stack overhead
- Кросс-платформенность через абстракцию .NET

**Конфигурация**:
```csharp
// Server
app.Run("unix:///tmp/ultrasharp-server.sock");

// Proxy
var channel = GrpcChannel.ForAddress("unix:///tmp/ultrasharp-server.sock");
```

### Shared Memory

**Когда использовать**:
- ✅ Результаты > 100 KB (view_definition с большим файлом)
- ✅ find_references с 1000+ результатов
- ✅ semantic_search с большими векторами

**Когда НЕ использовать**:
- ❌ Маленькие запросы/ответы (< 10 KB)
- ❌ Редкие вызовы

**Hybrid стратегия**:
```protobuf
message Response {
  oneof data {
    string inline = 1;           // < 100 KB
    string shared_memory_key = 2; // > 100 KB
  }
}
```

---

## ⚠️ Риски и митигации

### Риск 1: IPC латентность

**Вероятность**: Средняя
**Влияние**: Низкое (0.1-0.5ms per call)

**Митигация**:
- Unix Domain Sockets вместо TCP
- Shared Memory для больших данных
- Batching для множества мелких запросов
- Connection pooling

### Риск 2: Complexity отладки

**Вероятность**: Высокая
**Влияние**: Среднее

**Митигация**:
- Centralized logging (все процессы → один лог)
- Structured logging с process ID
- Visual Studio multi-process debugging
- Health checks и metrics

### Риск 3: Deployment сложность

**Вероятность**: Средняя
**Влияние**: Среднее

**Митигация**:
- Single installer
- systemd/Windows Service автостарт
- Health checks с auto-restart
- Rollback механизм

---

## 🎯 Итоговая рекомендация

### ✅ ДЕЛАТЬ

**Phase 0** (срочно, 1 день): Фикс дублирования load_solution

**Выбор архитектуры**:

#### Вариант A: Proxy + Server (2 процесса) - Простота
**Для**: Быстрая реализация, минимальная сложность
**Время**: 2-3 недели (20 дней)

#### Вариант B: Proxy + Core + VectorDB (3 процесса) - Оптимальность
**Для**: Индексация критична, планируется масштабирование
**Время**: 3-4 недели (25 дней)
**Дополнительно**: Рефакторинг SemanticSearchService

**Рекомендация**: **Вариант B** (Proxy + Core + VectorDB)

**Почему**:
1. ✅ Индексация НЕ блокирует Roslyn операции (параллелизм)
2. ✅ Vector DB изолирован (можно restart/scale)
3. ✅ VectorDB может быть AOT (после рефакторинга)
4. ✅ Заделка на будущее (remote VectorDB)
5. ✅ Всего +5 дней разработки, но долгосрочная польза

**План реализации (Вариант B)**:

**Phase 1**: gRPC Contracts (4-5 дней)
- roslyn_service.proto
- semantic_service.proto
- utility_service.proto
- **+** indexer_service.proto

**Phase 2**: Core Process (5-7 дней)
- Roslyn Services + Tools
- gRPC server
- Unix Domain Sockets

**Phase 3**: VectorDB Process (4-5 дней)
- Рефакторинг SemanticSearchService (убрать ISolutionManager)
- Vector DB с source code storage
- gRPC server для semantic operations
- AOT публикация

**Phase 4**: Proxy Process (4-5 дней)
- MCP stdio transport
- gRPC клиенты (Core + VectorDB)
- Routing логика
- AOT публикация

**Phase 5**: Shared Memory (2-3 дня)
- SharedMemoryManager
- Integration с gRPC

**Phase 6**: Deployment (3-4 дня)
- Installer для 3 процессов
- systemd services
- Health checks
- E2E тесты

**Итого**: ~25 рабочих дней (vs 20 для Варианта A)

**Результат**:
- **60% экономии памяти** (1.5 GB → 610 MB)
- **330x быстрее старт** Proxy (<100ms)
- **Индексация изолирована** (не блокирует Core)
- **2 AOT бинарика** (Proxy + VectorDB)
- **Масштабируемость** (можно вынести VectorDB на отдельный сервер)

### ❌ НЕ ДЕЛАТЬ

1. ❌ Разделение Services на Core/Roslyn библиотеки
   - Причина: Circular dependencies, сложность, мало пользы (Server все равно НЕ AOT)

2. ❌ Разделение на >2 процессов (Index, AI Connector, Tool processes)
   - Причина: Diminishing returns, 95% пользы от Proxy

3. ❌ Roslyn subprocess wrapper
   - Причина: Server уже singleton, дополнительная сложность не нужна

4. ❌ Предкомпилированный индекс вместо runtime Roslyn
   - Причина: Сложность incremental updates, устаревание данных

### 🚀 Go/No-Go критерии

**GO** если:
- ✅ IPC overhead < 1ms для 95% операций
- ✅ Proxy binary < 15 MB (AOT)
- ✅ Proxy startup < 200ms
- ✅ Memory экономия > 50% (3 окна)
- ✅ Deployment проще или равен текущему

**NO-GO** если:
- ❌ IPC overhead > 5ms
- ❌ Разработка > 1 месяца
- ❌ Критические баги в production

---

## 📚 Дополнительные материалы

См. также:
- `aot-compatibility-analysis.md` - Детальный анализ AOT совместимости инструментов
- `aot-adaptation-roadmap.md` - Стратегии адаптации к AOT
- `services-aot-analysis.md` - Анализ сервисов и их зависимостей
- `Dev.Docs/Architecture/` - Архитектурная документация

---

## 🏁 Заключение

**Рекомендуемая архитектура**: **3 процесса** (Proxy + Core + VectorDB)

**Почему не 2**:
- Индексация критична для работы
- Vector DB изолирован (200 MB отдельно)
- Параллелизм: Roslyn + Indexing одновременно
- Заделка на будущее (remote VectorDB, scaling)
- Только +5 дней разработки (+25%)

**Почему не 4+**:
- Diminishing returns
- Over-engineering
- Сложность управления

**Ключевые преимущества**:
- ✅ **60% экономии памяти** (1.5 GB → 610 MB)
- ✅ **2 AOT процесса** (Proxy 5-10 MB, VectorDB ~20 MB)
- ✅ **Изоляция** (restart VectorDB без Core)
- ✅ **Параллелизм** (Roslyn + Indexing)
- ✅ **Масштабируемость** (remote VectorDB в будущем)

**НЕ** делать over-engineering:
- ❌ Разделение Services на библиотеки (circular dependencies)
- ❌ >3 процессов (Index, AI, Tool отдельно)
- ❌ Альтернативные парсеры вместо Roslyn

**Критический рефакторинг для VectorDB AOT**:
```csharp
// Убрать ISolutionManager из SemanticSearchService
// Сохранять source code в Vector DB при индексации
// Vector DB становится полностью самодостаточным
```

**Главный выигрыш**:
- **1.5 GB → 610 MB** память 🎉
- **Индексация изолирована** 🚀
- **2 AOT бинарика** ⚡

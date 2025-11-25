# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Обзор проекта

UltrasharpTools — это MCP-сервер, предоставляющий AI-агентам возможности анализа и модификации C# кодовых баз с использованием Roslyn. Проект состоит из пяти основных компонентов:

- **UltrasharpTools.Tools** — библиотека с MCP инструментами и сервисами для работы с C# кодом (268 файлов)
- **UltrasharpTools.Droid** — Stdio-сервер для локальной интеграции с MCP-клиентами (51 файл)
- **UltrasharpTools.Overlord** — HTTP-сервер (SSE) для удалённого доступа (30 файлов)
- **UltraSharpTools.Comm** — коммуникационная библиотека для IPC (6 файлов)
- **UltraSharpTools.VectorDB** — внешний процесс для semantic операций (Indexer) (33 файла)

**Target Framework:** .NET 10.0
**Version:** 3.2.0

## Команды разработки

### Структура проекта

```
ultrasharp-tools-mcp/
├── UltrasharpTools.Tools/      # Ядро: MCP инструменты, Roslyn сервисы
├── UltrasharpTools.Droid/      # Stdio MCP сервер
├── UltrasharpTools.Overlord/   # HTTP SSE сервер
├── UltraSharpTools.Comm/       # IPC коммуникация
├── UltraSharpTools.VectorDB/   # Внешний Indexer процесс
├── UltrasharpTools.Benchmarks/ # BenchmarkDotNet тесты
├── UltrasharpTools.Test/       # Тестовые проекты
│
├── Dev.Scripts/                # Скрипты разработки (.ps1, .py, .sh)
├── Dev.Docs/                   # Техническая документация
├── Dev.Archive/                # Архив старой документации
│
├── Run.Config/                 # Конфигурационные файлы
├── Run.Docs/                   # Документация для пользователей
├── Run.Publish/                # Артефакты сборки (git ignore)
└── Run.Logs/                   # Логи (git ignore)
```

### Dev.Scripts/ — Скрипты разработки

**Сборка:**
- `build-debug.ps1` / `build-release.ps1` — базовая сборка
- `build-all.ps1` — сборка всех проектов
- `build-hybrid.ps1` — сборка Droid + VectorDB
- `build-releases.ps1` — сборка для релиза

**Публикация:**
- `publish-mcp.ps1` — оптимизированная сборка Droid (R2R + PGO)
- `publish-vectordb.ps1` — публикация VectorDB
- `publish-hybrid.ps1` — публикация Droid + VectorDB
- `publish-comm.ps1` — публикация Comm
- `organize-publish.ps1` — организация артефактов

**Semantic Embedding:**
- `setup-tei.ps1` / `setup-tei.sh` — запуск TEI Docker контейнера
- `convert-tokenizer-to-fast.ps1/.py` — конвертация токенизаторов
- `setup-nvidia-container-toolkit.ps1` — настройка NVIDIA для Docker

**Утилиты:**
- `update-version.ps1` — обновление версии во всех файлах
- `training-client.ps1` — клиент для обучения

### Сборка

**Оптимизированная публикация Droid:**
```bash
# Windows
build-release.cmd

# Или напрямую
pwsh Dev.Scripts/publish-mcp.ps1
```

Особенности:
- ReadyToRun (R2R) + Dynamic PGO
- Удаление PDB файлов (~34 MB)
- Организация Scripts/ и Config/
- Результат: `Run.Publish/Droid/` (~103 MB)

**Hybrid публикация (Droid + VectorDB):**
```bash
pwsh Dev.Scripts/publish-hybrid.ps1
```

**Обычная сборка для разработки:**
```bash
dotnet build UltrasharpTools.sln
dotnet build UltrasharpTools.sln -c Release
```

### Запуск серверов

**Stdio Server (Droid):**
```bash
cd UltrasharpTools.Droid
dotnet run -- --log-directory ./logs --log-level Information
```

**SSE Server (Overlord):**
```bash
cd UltrasharpTools.Overlord
dotnet run -- --port 3001 --log-level Information
```

**VectorDB (Indexer):**
```bash
cd UltraSharpTools.VectorDB
dotnet run -- --port 11435
```

### Опции командной строки

Общие для Droid и Overlord:
- `--log-level <level>` — Verbose, Debug, Information, Warning, Error, Fatal
- `--load-solution <path>` — путь к .sln для загрузки при старте
- `--build-configuration <config>` — Debug/Release
- `--disable-git` — отключить Git интеграцию

## Архитектура

### Основные компоненты

```
┌─────────────────────────────────────────────────────────────┐
│                     MCP Clients                             │
│              (Claude Desktop, VS Code, etc.)                │
└──────────────────────┬──────────────────────────────────────┘
                       │
          ┌────────────┴────────────┐
          │                         │
          ▼                         ▼
┌─────────────────┐       ┌─────────────────┐
│  Droid (Stdio)  │       │ Overlord (SSE)  │
│   Local MCP     │       │   Remote MCP    │
└────────┬────────┘       └────────┬────────┘
         │                         │
         └────────────┬────────────┘
                      │
                      ▼
         ┌────────────────────────┐
         │   UltrasharpTools.Tools │
         │   (Roslyn Services)     │
         │   - CodeAnalysis        │
         │   - CodeModification    │
         │   - FastSymbolIndex     │
         │   - GitService          │
         └────────────┬────────────┘
                      │
         ┌────────────┴────────────┐
         │                         │
         ▼                         ▼
┌─────────────────┐       ┌─────────────────┐
│  UltraSharpTools│       │ MSBuildWorkspace│
│     VectorDB    │       │    (Roslyn)     │
│  (Indexer/IPC)  │       │                 │
└─────────────────┘       └─────────────────┘
```

### Слоистая структура Tools

**Уровень интерфейсов** (`Interfaces/`):
- `ISolutionManager`, `ICodeAnalysisService`, `ICodeModificationService`
- `IGitService`, `ISemanticSimilarityService`, `IFastSymbolIndex`

**Уровень сервисов** (`Services/`):
- `SolutionManager` — управление MSBuildWorkspace, загрузка .sln
- `FastSymbolIndex` — O(1) поиск символов (Bloom filter + FrozenDictionary)
- `CodeAnalysisService` — анализ кода: члены, референсы, определения
- `CodeModificationService` — модификация кода через Roslyn API
- `CallGraphIndexer` — индексация графа вызовов
- `SemanticSimilarityService` — семантический поиск (требует VectorDB)
- `GitCliService` — автоматическая работа с Git
- `FormattingService` — форматирование через CSharpier
- `DiagnosticService` — Roslyn analyzers
- `CodeFixService` — автоматическое применение code fixes

**Уровень MCP инструментов** (`Mcp/Tools/`):
| Файл | Назначение |
|------|------------|
| `SolutionTools.cs` | load_solution, load_project |
| `AnalysisTools.cs` | get_members, view_definition, find_references, view_call_graph |
| `ModificationTools.cs` | add_member, modify_code, rename_symbol, move_member |
| `PatternSearchTools.cs` | search_definitions, pattern_search, replace_references |
| `SemanticAnalysisTools.cs` | semantic_search, semantic_diff, detect_code_clones |
| `QualityTools.cs` | format_code, analyze_code_style, apply_code_fixes |
| `TraceTools.cs` | trace_execution, trace_backwards, analyze_path_feasibility |
| `LogTools.cs` | analyze_logs |
| `DocumentTools.cs` | read_file, create_file, overwrite_file |
| `FileOperationTools.cs` | split_file, synthesize_files, manage_usings |
| `ValidationTools.cs` | validate_file, validate_directory |
| `SnapshotTools.cs` | create_snapshot, rollback_snapshot, list_snapshots |
| `PackageTools.cs` | add_package |
| `TechnologyDetectionTools.cs` | detect_technology_stack |
| `SystemTools.cs` | get_capabilities, undo |
| `MiscTools.cs` | request_new_tool |

### Паттерн логирования

Все сервисы используют паттерн partial class для логирования:

```csharp
// MyService.cs
public sealed partial class MyService
{
    private readonly ILogger<MyService> _logger;

    public void DoWork()
    {
        LogOperationStarted("test");
        // ...
        LogOperationCompleted(100, 50);
    }
}

// MyService.Logging.cs
public sealed partial class MyService
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Operation started: {Name}")]
    private partial void LogOperationStarted(string name);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Operation completed: {Count} items in {ElapsedMs}ms")]
    private partial void LogOperationCompleted(int count, long elapsedMs);
}
```

### Droid Hybrid Services

**Services/Hybrid/** — сервисы для работы с внешним VectorDB:
- `VectorDBLauncher` — запуск VectorDB процесса
- `SemanticModeProvider` — управление semantic режимом
- `SemanticModeConfigurationLoader` — загрузка конфигурации
- `EmbeddingService` — работа с embeddings
- `ServerBridgeService` — мост к VectorDB
- `ToolRouter` — маршрутизация инструментов
- `ToolEnricher` — обогащение результатов
- `ConfigurationService` — конфигурация
- `FileWatcherService` / `GitWatcherService` — отслеживание изменений
- `HealthCheckHostedService` — health checks
- `NotificationClientService` — уведомления
- `RetryPolicy` — политика повторов
- `RequestBatchingService` — батчинг запросов

**Services/**:
- `PowerManagementService` — управление энергопотреблением

### VectorDB (Indexer)

Внешний процесс для semantic операций:

```
UltraSharpTools.VectorDB/
├── Semantic/
│   ├── Backends/
│   │   └── SqliteVecBackend.cs    # SQLite-vec хранилище
│   ├── Embedding/
│   │   └── Providers/
│   │       └── TEIProvider.cs     # Text Embeddings Inference
│   ├── EmbeddingGenerator.cs      # Генерация embeddings
│   ├── VectorStore.cs             # Хранилище векторов
│   └── VectorDBSemanticService.cs # Главный сервис
├── VectorDBService.cs             # IPC сервис
├── PowerManagementService.cs      # Энергосбережение
└── Program.cs                     # Entry point
```

### Поток работы

1. **Инициализация**: AI вызывает `load_solution` с путём к .sln
2. **Навигация**: `load_project` возвращает карту типов (namespaces → types)
3. **Анализ**: `view_definition`, `get_members`, `find_references` по FQN
4. **Модификация**: `add_member`, `modify_code`, `rename_symbol` — автокоммит в Git
5. **Откат**: `undo` откатывает последнее изменение

### Особенности реализации

**FQN Fuzzy Matching** (`FuzzyFqnLookupService`):
- AI может передавать неточные или неполные FQN
- Сервис находит наиболее подходящий символ через Levenshtein distance

**FastSymbolIndex** (O(1) поиск):
- Bloom filter для быстрого отклонения
- FrozenDictionary для O(1) lookup
- Параллельная индексация по уровням зависимостей (Phase 5)

**Token Efficiency**:
- Код возвращается без отступов (~10% экономия токенов)
- Навигация по FQN вместо полного чтения файлов
- Адаптивный уровень детализации

**Git Integration**:
- Автоматические ветки `sharptools/YYYYMMDD-HHMMSS`
- Автокоммиты с описанием
- `undo` откатывает последний коммит
- Отключение: `--disable-git`

### Dependency Injection

```csharp
// Регистрация сервисов
services.WithSharpToolsServices(enableGit: true, buildConfiguration: "Debug");

// Для MCP
services.AddDroid()
    .WithStdioTransport()  // или .WithHttpTransport()
    .WithSharpTools();

// Для Semantic Merge
services.WithSemanticRag(
    databasePath: "./data/vectors.db",
    dimension: 768,
    configureEmbedding: opts => { /* настройка */ }
);
services.WithSemanticMerge();
```

## Подход к разработке

### Добавление новых инструментов

1. Создать метод в соответствующем классе Tools с атрибутами `[DroidTool]` и `[Description]`
2. Использовать dependency injection для сервисов
3. Обернуть логику в `ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync`
4. Добавить logging методы в отдельный `.Logging.cs` файл

### Добавление новых сервисов

1. Создать интерфейс в `Interfaces/`
2. Реализовать сервис в `Services/`
3. Создать `.Logging.cs` partial class для логирования
4. Зарегистрировать в `ServiceCollectionExtensions`

### Тестирование

```bash
# Запуск с решением
dotnet run --project UltrasharpTools.Droid -- --load-solution ./MyProject.sln --log-level Debug

# Тесты
dotnet test UltrasharpTools.Test/
```

### Обновление версии

```bash
# Dry run (предпросмотр)
Dev.Scripts\update-version.cmd 3.3.0 --dry-run

# Применить
Dev.Scripts\update-version.cmd 3.3.0
```

Обновляются 21 место: .csproj, Dockerfile, Program.cs, Chart.yaml, README.md и др.

## Важные зависимости

**Основные:**
- **Microsoft.CodeAnalysis.Workspaces.MSBuild** (5.0.0) — загрузка .sln
- **ICSharpCode.Decompiler** (10.0.0.8079-preview1) — декомпиляция
- **ModelContextProtocol** (0.4.0-preview.3) — MCP SDK
- **Microsoft.Z3** (4.12.2) — SMT solver для path feasibility

**Quality:**
- **CSharpier.Core** — форматирование C#

**Semantic:**
- **Microsoft.Data.Sqlite** (10.0.0) — sqlite-vec хранилище
- **System.IO.Hashing** (10.0.0) — xxHash

## Производительность

| Метрика | Улучшение | Описание |
|---------|-----------|----------|
| Symbol Indexing | 10-100x | O(1) вместо O(N) через FastSymbolIndex |
| Parallel Indexing | 2-4x | Параллельная компиляция по уровням зависимостей |
| Log Analysis | 109x | SIMD + Span<T> оптимизации |
| N+1 Fix | 20-30x | Батчинг запросов |

## Ограничения

- MSBuildWorkspace требует соответствующий .NET SDK
- Сервер не перезагружает решение автоматически при изменении файлов
- GitService создаёт ветку для каждого изменения — может потребоваться очистка
- Semantic operations требуют запущенный VectorDB или TEI/Ollama

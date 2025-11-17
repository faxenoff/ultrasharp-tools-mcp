# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Обзор проекта

SharpTools — это MCP-сервер, предоставляющий AI-агентам возможности анализа и модификации C# кодовых баз с использованием Roslyn. Проект состоит из трёх основных компонентов:

- **UltrasharpTools.Tools** — библиотека с MCP инструментами и сервисами для работы с C# кодом
- **UltrasharpTools.Overlord** — HTTP-сервер (SSE) для удалённого доступа
- **UltrasharpTools.Droid** — Stdio-сервер для локальной интеграции с MCP-клиентами

## Команды разработки

### Структура проекта

```
ultrasharp-tools-mcp/
├── Dev.Scripts/           # Скрипты разработки (.ps1, .py, .sh)
├── Dev.Docs/              # Техническая документация
├── Run.Config/            # Конфигурационные файлы
├── Run.Publish/           # Артефакты сборки (git ignore)
└── Run.Logs/              # Логи (git ignore)
```

**Dev.Scripts/** содержит:
- `publish-mcp.ps1` — оптимизированная сборка Droid
- `setup-semantic-embedding.ps1` — настройка semantic embedding
- `detect-gpu-architecture.ps1` — определение GPU архитектуры
- `convert-tokenizer-to-fast.ps1/.py` — конвертация токенизаторов
- `setup-tei.ps1/.sh` — запуск TEI Docker контейнера
- `validate-semantic-config.ps1` — валидация конфигурации

**Run.Config/** содержит:
- `semantic-config.json` — рабочий конфиг (git ignore)
- `semantic-config.yaml` — пример конфига
- `validate-semantic-config.cmd` — Windows launcher

### Сборка

**Оптимизированная публикация Droid:**
```bash
# Windows
publish-droid.cmd

# Linux/macOS
pwsh Dev.Scripts/publish-mcp.ps1
```

Особенности:
- ✅ ReadyToRun (R2R) + Dynamic PGO
- ✅ Удаление PDB файлов (~34 MB)
- ✅ Удаление BuildHost директорий
- ✅ Организация Scripts/ и Config/
- 📦 Результат: `Run.Publish/Droid/` (~103 MB)

**Обычная сборка для разработки:**
```bash
dotnet build UltrasharpTools.sln
dotnet build UltrasharpTools.sln -c Release
```

### Запуск серверов

**SSE Server (HTTP):**
```bash
cd UltrasharpTools.Overlord
dotnet run -- --port 3001 --log-level Information
dotnet run -- --port 3001 --log-file ./logs/server.log --log-level Debug --build-configuration Debug
```

**Stdio Server:**
```bash
cd UltrasharpTools.Droid
dotnet run -- --log-directory ./logs --log-level Information
```

### Semantic Embedding

**Первая настройка:**
```bash
# Windows (двойной клик)
setup-semantic-embedding.cmd

# Или напрямую
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

**Валидация конфигурации:**
```bash
validate-semantic-config.cmd
# или
pwsh Dev.Scripts/validate-semantic-config.ps1
```

**Запуск TEI сервера:**
```bash
pwsh Dev.Scripts/setup-tei.ps1    # Windows
./Dev.Scripts/setup-tei.sh        # Linux
```

Опции командной строки (обоим серверам):
- `--log-level <level>` — уровень логирования (Verbose, Debug, Information, Warning, Error, Fatal)
- `--load-solution <path>` — путь к .sln файлу для загрузки при старте (опционально, лучше использовать load_solution)
- `--build-configuration <config>` — конфигурация сборки (Debug/Release)
- `--disable-git` — отключить Git интеграцию

## Архитектура

### Слоистая структура

**Уровень интерфейсов** (`UltrasharpTools.Tools/Interfaces/`):
- Определяют контракты для основных сервисов
- Ключевые интерфейсы: `ISolutionManager`, `ICodeAnalysisService`, `ICodeModificationService`, `IGitService`, `ISemanticSimilarityService`

**Уровень сервисов** (`UltrasharpTools.Tools/Services/`):
- `SolutionManager` — управление MSBuildWorkspace, загрузка .sln файлов, поиск символов
- `CodeAnalysisService` — анализ кода: поиск членов, референсов, определений
- `CodeModificationService` — модификация кода через Roslyn API
- `GitService` / `NoOpGitService` — автоматическая работа с Git (создание веток `sharptools/*`, коммиты изменений)
- `SemanticSimilarityService` — поиск семантически похожих методов/классов
- `ComplexityAnalysisService` — анализ цикломатической и когнитивной сложности
- `SourceResolutionService` — получение исходного кода из SourceLink, embedded PDB, декомпиляции
- **`FormattingService`** — форматирование кода через CSharpier
- **`DiagnosticService`** — анализ через Roslyn analyzers
- **`CodeFixService`** — автоматическое применение code fixes
- **Semantic Embedding сервисы:**
  - `SemanticConfigManager` — управление конфигурацией (global + project)
  - `AutoConfigurationService` — автоопределение платформы (Ollama/TEI/Memory)
  - `EmbeddingConfigValidator` — валидация с health checks
  - `CodebaseLanguageDetector` — определение языка кодовой базы (english/multilingual)
  - `CodebaseSizeDetector` — определение размера (small/medium/large)
  - `EmbeddingServiceHealthChecker` — проверка доступности TEI/Ollama

**Уровень MCP инструментов** (`UltrasharpTools.Tools/Mcp/Tools/`):
- `SolutionTools` — `load_solution`, `load_project`
- `AnalysisTools` — `get_members`, `view_definition`, `find_references`, `search_definitions`, `analyze_complexity`
- `ModificationTools` — `add_member`, `modify_code`, `rename_symbol`, `find_and_replace`, `move_member`, `undo`
- `DocumentTools` — `read_file`, `create_file`, `overwrite_file`
- **`QualityTools`** — `format_code`, `analyze_code_style`, `apply_code_fixes` (новое)

### Поток работы

1. **Инициализация**: AI вызывает `load_solution` с путём к .sln файлу
2. **Навигация**: `load_project` возвращает карту проекта (namespaces → types), адаптивную по сложности
3. **Анализ**: Используя FQN, AI читает определения (`view_definition`), члены типов (`get_members`), референсы (`find_references`)
4. **Модификация**: `add_member`, `modify_code`, `rename_symbol` — каждое изменение коммитится в Git
5. **Откат**: `undo` откатывает последнее изменение через Git

### Особенности реализации

**FQN Fuzzy Matching** (`FuzzyFqnLookupService`):
- AI может передавать неточные или неполные FQN
- Сервис находит наиболее подходящий символ через Levenshtein distance и Roslyn APIs

**Token Efficiency**:
- Весь код возвращается без отступов (экономия ~10% токенов)
- Навигация по FQN вместо полного чтения файлов
- Адаптивный уровень детализации в `load_project` (DetailLevel enum)

**Git Integration**:
- Каждое изменение создаёт ветку `sharptools/YYYYMMDD-HHMMSS`
- Автоматические коммиты с описанием изменения
- `undo` откатывает последний коммит
- Можно отключить через `--disable-git`

**Source Resolution**:
- Приоритет: Local files → SourceLink → Embedded PDB → ILSpy decompilation
- Поддержка legacy и SDK-style проектов
- Работа с любыми версиями .NET (Framework, Core, 5+)

### Dependency Injection

Регистрация сервисов в `ServiceCollectionExtensions.cs`:
```csharp
services.WithSharpToolsServices(enableGit: true, buildConfiguration: "Debug");
```

Для MCP:
```csharp
services.AddDroid()
    .WithHttpTransport() // или .WithDroidTransport()
    .WithSharpTools();
```

Для Semantic Merge:
```csharp
// Сначала зарегистрировать Semantic RAG сервисы (требуется для embeddings)
services.WithSemanticRag(
    databasePath: "./data/vectors.db",
    dimension: 768,
    configureEmbedding: opts => { /* настройка embedding провайдера */ }
);

// Затем зарегистрировать Semantic Merge сервисы
services.WithSemanticMerge();
```

Semantic Merge автоматически регистрирует:
- **Parsing**: ContentNormalizer, StructuralFingerprint, CSharpParser, JsonParser, CodeUnitExtractor
- **Indexing**: MultiVersionIndexer, LazyEmbeddingGenerator
- **Matching**: FastPathMatcher, SemanticMatcher, MovementDetector, StructuralAligner
- **Merge Engine**: ThreeWayMerger
- **Analysis**: IntentClassifier
- **Service**: SemanticMergeService

## Подход к разработке

### Добавление новых инструментов

1. Создать метод в соответствующем классе Tools с атрибутами `[DroidTool]` и `[Description]`
2. Использовать dependency injection для сервисов
3. Обернуть логику в `ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync`
4. Регистрировать в `ServiceCollectionExtensions.WithSharpTools()` (если нужно)

### Тестирование

- Запустить сервер с `--load-solution <path>` для ручного тестирования
- Использовать `--log-level Debug` для детальной диагностики
- Проверять Git ветки после модификаций (если Git включён)

### Важные зависимости

**Основные:**
- **Microsoft.CodeAnalysis.Workspaces.MSBuild** (5.0.0-2.final) — загрузка .sln файлов
- **ICSharpCode.Decompiler** (10.0.0.8079-preview1) — декомпиляция при отсутствии исходников
- **LibGit2Sharp** (0.31.0) — автоматизация Git
- **ModelContextProtocol** (0.4.0-preview.3) — MCP SDK

**Quality Tools:**
- **CSharpier.Core** (1.2.1) — форматирование C# кода

**Semantic Embedding:**
- **Microsoft.SemanticKernel** — векторные операции и embedding
- **Microsoft.Data.Sqlite** — хранение векторов (sqlite-vec/vectorlite)
- Поддержка провайдеров:
  - TEI (Text Embeddings Inference) — HuggingFace, Docker
  - Ollama — локальный inference (granite-embedding, mxbai-embed-large)
  - Memory — in-process embedding (экспериментально)

## Новые возможности: Quality Tools

### format_code — Форматирование кода
Использует **CSharpier** для автоматического форматирования C# кода согласно единому стилю.

**Возможности:**
- Форматирование `.cs`, `.csproj`, `.xml` файлов
- Режим проверки (`checkOnly=true`) — только анализ без изменений
- Параллельная обработка файлов для высокой производительности
- Рекурсивное форматирование директорий

**Пример использования:**
```
format_code(
    path: "D:/MyProject/src",
    checkOnly: true  // Сначала проверяем
)
// Затем применяем:
format_code(
    path: "D:/MyProject/src",
    checkOnly: false  // Применяет форматирование
)
```

### AnalyzeCodeStyle — Анализ качества кода
Использует **Roslyn Analyzers** для поиска проблем в коде: code style issues, warnings, errors.

**Возможности:**
- Запуск всех Roslyn analyzers (CA, IDE, CS диагностики)
- Фильтрация по уровню серьезности (Hidden/Info/Warning/Error)
- Пагинация для больших результатов
- Группировка результатов по severity

**Пример использования:**
```
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    severityFilter: "Warning",  // Info, Warning, Error
    skip: 0,
    take: 100
)
```

### ApplyCodeFixes — Автоматическое исправление
Автоматически применяет code fixes для распространённых проблем.

**Поддерживаемые диагностики:**
- `IDE0005` — Remove unnecessary using
- `CS8019` — Unnecessary using directive
- Легко расширяется для других диагностик

**Возможности:**
- Режим preview (`preview=true`) — показывает что будет исправлено
- Автоматический git commit при применении изменений
- Параллельная обработка проектов

**Пример использования:**
```
// Сначала preview:
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",  // или "all"
    preview: true
)
// Затем применяем:
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",
    preview: false  // Создаст git commit
)
```

### Workflow с Quality Tools

Типичный workflow для улучшения качества кода:

1. **Анализ**: `analyze_code_style` — находим проблемы
2. **Автоисправление**: `apply_code_fixes` — исправляем что возможно автоматически
3. **Форматирование**: `format_code` — приводим код к единому стилю
4. **Повторный анализ**: `analyze_code_style` — проверяем результат

Все изменения автоматически коммитятся в Git (если не отключено).

## Ограничения и особенности

- MSBuildWorkspace требует соответствующий .NET SDK для целевых проектов
- Сервер не перезагружает решение автоматически при изменении файлов (нужен `ReloadSolutionFromDiskAsync`)
- GitService создаёт новую ветку для каждого изменения — может потребоваться периодическая очистка
- EditorConfig используется для форматирования кода при модификациях

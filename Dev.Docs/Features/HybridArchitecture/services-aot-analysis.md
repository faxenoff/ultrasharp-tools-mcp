# Анализ Services - AOT совместимость

## Категоризация сервисов UltrasharpTools.Tools

### 🔴 Roslyn-зависимые (НЕ AOT)

Сервисы использующие `Microsoft.CodeAnalysis.*`:

| Сервис | Причина НЕ AOT | Использует |
|--------|----------------|------------|
| **SolutionManager** | Roslyn Workspace, MSBuild | Compilation, Project, Document |
| **CodeAnalysisService** | Roslyn Symbols, Compilation | ISymbol, SyntaxTree |
| **CodeModificationService** | SyntaxTree rewriting | SyntaxNode.ReplaceNode |
| **ComplexityAnalysisService** | Control Flow Graph | IOperation, CFG |
| **ExecutionTraceService** | Control Flow Graph | CFG, IOperation |
| **BacktraceService** | Symbol analysis, CFG | IMethodSymbol, CFG |
| **SymbolicExecutionService** | CFG + Z3 | IOperation, CFG |
| **FormattingService** | Roslyn Formatter | Formatter.FormatAsync |
| **DiagnosticService** | Roslyn Analyzers | DiagnosticAnalyzer |
| **CodeFixService** | Roslyn CodeFix | CodeFixProvider |
| **QuickLintService** | Roslyn quick checks | Compilation |
| **DocumentOperationsService** | Roslyn Document | Document, SyntaxTree |
| **ImportUpdateService** | Using directives | CompilationUnitSyntax |
| **FuzzyFqnLookupService** | Symbol search | ISymbol |
| **SymbolResolver** | Symbol resolution | Compilation.GetSymbol |
| **FastSymbolIndex** | Symbol caching | ISymbol, INamedTypeSymbol |
| **SymbolCacheManager** | Symbol serialization | ISymbol metadata |
| **SemanticModelCacheService** | SemanticModel caching | SemanticModel |
| **SyntaxTreeCacheService** | SyntaxTree caching | SyntaxTree |
| **AnalysisCacheService** | Compilation caching | Compilation |
| **AsyncStateMachineAnalyzer** | Async state machine | IOperation, CFG |
| **LinqQueryAnalyzer** | LINQ query analysis | IOperation |
| **SemanticSimilarityService** | Symbol comparison | ISymbol |

**Итого**: ~23 Roslyn-зависимых сервиса

---

### 🟡 Reflection.Metadata зависимые (НЕ AOT)

Сервисы использующие `System.Reflection.Metadata`:

| Сервис | Причина НЕ AOT | Использует |
|--------|----------------|------------|
| **PdbSymbolResolver** | PDB reading | MetadataReader |
| **SourceResolutionService** | Embedded source extraction | PortablePdbReader |
| **EmbeddedSourceReader** | PDB embedded source | MetadataReader |

**Итого**: ~3 Reflection.Metadata сервиса

---

### 🟢 Z3-зависимые (МОЖЕТ быть AOT через subprocess)

| Сервис | Причина | Обход |
|--------|---------|-------|
| **Z3ConstraintSolver** | Native P/Invoke | Subprocess wrapper |

**Итого**: 1 сервис (можно адаптировать)

---

### ✅ AOT-совместимые сервисы

Сервисы БЕЗ Roslyn/Reflection.Metadata зависимостей:

#### Файловые операции / Git
| Сервис | Тип | Зависимости |
|--------|-----|-------------|
| **GitCliService** | Git | Process.Start("git") |
| **NoOpGitService** | Mock | Нет |
| **EditorConfigProvider** | File I/O | System.IO, Regex |
| **EditorConfigGenerator** | File generation | System.IO |

#### Логирование / Анализ текста
| Сервис | Тип | Зависимости |
|--------|-----|-------------|
| **LogAnalysisService** | Log parsing | Regex, File I/O |
| **FuzzyStackTraceMatcher** | Text matching | Regex |
| **CallerInfoConverter** | Log formatting | String operations |

#### NuGet / HTTP
| Сервис | Тип | Зависимости |
|--------|-----|-------------|
| **NuGetHttpService** | HTTP | HttpClient |
| **LegacyNuGetPackageReader** | NuGet | NuGet.Protocol* |

*NuGet.Protocol может иметь проблемы с AOT, нужно проверить

#### Semantic / Embedding (если через HTTP)
| Сервис | Тип | Зависимости |
|--------|-----|-------------|
| **SemanticServiceHealthCheck** | HTTP health check | HttpClient |
| **EmbeddingServiceHealthChecker** | HTTP | HttpClient |
| **SemanticConfigManager** | Config | JSON, File I/O |
| **EmbeddingConfigValidator** | Validation | JSON schema |
| **AutoConfigurationService** | Config | File I/O |

#### Утилиты
| Сервис | Тип | Зависимости |
|--------|-----|-------------|
| **CodebaseSizeDetector** | File stats | System.IO |
| **CodebaseLanguageDetector** | File extensions | System.IO |
| **CallGraphExporter** | Graph export | Text generation (DOT, Mermaid) |
| **PathInfo** | Helpers | String operations |

#### Semantic Features (если БЕЗ Roslyn)
| Сервис | Проблема | Решение |
|--------|----------|---------|
| **ClassSemanticFeatures** | Может использовать Roslyn | Нужна проверка |
| **MethodSemanticFeatures** | Может использовать Roslyn | Нужна проверка |
| **ClassSimilarityResult** | DTO | AOT OK |
| **MethodSimilarityResult** | DTO | AOT OK |
| **SemanticDiagnosticEnricher** | Enrichment | Если HTTP эмбеддинги - AOT OK |

**Итого**: ~25 AOT-совместимых сервисов

---

## Итоговая статистика

| Категория | Количество | % от всех |
|-----------|------------|-----------|
| **🔴 Roslyn** | 23 | 43% |
| **🟡 Reflection.Metadata** | 3 | 6% |
| **🟢 Z3 (адаптируемо)** | 1 | 2% |
| **✅ AOT OK** | 25 | 46% |
| **❓ Неясно** | 2 | 4% |
| **ИТОГО** | 54 | 100% |

**Вывод**: **46% сервисов** могут быть AOT без усилий!

---

## Разделение на модули

### Предлагаемая структура

```
UltrasharpTools.sln
│
├── UltrasharpTools.Core (✅ AOT)
│   ├── Infrastructure/
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
│       ├── GitCliService
│       ├── LogAnalysisService
│       ├── NuGetHttpService
│       ├── EditorConfigProvider
│       ├── SemanticHealthCheck
│       ├── CallGraphExporter
│       └── ... (25 AOT-сервисов)
│
├── UltrasharpTools.Roslyn (❌ НЕ AOT)
│   ├── Services/
│   │   ├── SolutionManager
│   │   ├── CodeAnalysisService
│   │   ├── CodeModificationService
│   │   ├── ComplexityAnalysisService
│   │   ├── ExecutionTraceService
│   │   ├── FormattingService
│   │   ├── DiagnosticService
│   │   └── ... (26 Roslyn-сервисов)
│   └── Dependencies:
│       └── Microsoft.CodeAnalysis.*
│
├── UltrasharpTools.Tools.Core (✅ AOT)
│   ├── Mcp/Tools/
│   │   ├── SystemTools
│   │   ├── LogTools
│   │   ├── SnapshotTools
│   │   ├── SemanticMergeTools
│   │   ├── PackageTools (через dotnet CLI)
│   │   └── ... (AOT инструменты)
│   └── Dependencies:
│       └── UltrasharpTools.Core
│
├── UltrasharpTools.Tools.Roslyn (❌ НЕ AOT)
│   ├── Mcp/Tools/
│   │   ├── SolutionTools
│   │   ├── AnalysisTools
│   │   ├── ModificationTools
│   │   ├── QualityTools
│   │   ├── ValidationTools
│   │   ├── TraceTools
│   │   └── ... (Roslyn инструменты)
│   └── Dependencies:
│       └── UltrasharpTools.Roslyn
│
├── UltrasharpTools.Proxy (✅ AOT)
│   ├── Program.cs
│   ├── McpStdioTransport
│   ├── GrpcClient
│   └── Dependencies:
│       ├── ModelContextProtocol
│       ├── Grpc.Net.Client
│       └── UltrasharpTools.Tools.Core (только AOT инструменты)
│
└── UltrasharpTools.Server (❌ НЕ AOT, singleton)
    ├── Program.cs
    ├── GrpcServer
    └── Dependencies:
        ├── UltrasharpTools.Roslyn
        ├── UltrasharpTools.Core
        ├── UltrasharpTools.Tools.Roslyn
        └── UltrasharpTools.Tools.Core
```

---

## Проблемы текущей архитектуры

### Проблема 1: Circular dependencies

**Сейчас**:
```
UltrasharpTools.Tools (единый пакет)
  ├── Services (все вместе)
  └── Mcp/Tools (все вместе)
```

**Проблема**: Нельзя разделить - все в одном проекте

### Проблема 2: Shared Infrastructure

Многие сервисы используют общую инфраструктуру:
- `ISolutionManager` - нужен почти всем инструментам
- `ILogger<T>` - все
- `ErrorHandlingHelpers` - все

**Решение**: Вынести интерфейсы в UltrasharpTools.Core

---

## Рекомендации по разделению

### Вариант 1: Минимальный (рекомендуется)

**НЕ разделять** Core/Roslyn сервисы, только Proxy

```
UltrasharpTools.Proxy (✅ AOT)
  - MCP stdio transport
  - gRPC клиент
  - Только маршрутизация

UltrasharpTools.Server (❌ НЕ AOT)
  - ВСЕ сервисы (Core + Roslyn)
  - ВСЕ инструменты
  - gRPC сервер
```

**Плюсы**:
- ✅ Минимальные изменения кода
- ✅ Нет circular dependencies
- ✅ Proxy дает 95% пользы
- ✅ Простая архитектура

**Минусы**:
- ❌ Server не AOT (но это OK, singleton)

---

### Вариант 2: Полное разделение (сложно)

**Разделить** на UltrasharpTools.Core и UltrasharpTools.Roslyn

**Шаги**:
1. Создать UltrasharpTools.Core.Abstractions (интерфейсы)
2. Переместить AOT-сервисы в UltrasharpTools.Core
3. Переместить Roslyn-сервисы в UltrasharpTools.Roslyn
4. Разделить инструменты на Tools.Core и Tools.Roslyn
5. Создать Proxy с Tools.Core
6. Создать Server с Tools.Core + Tools.Roslyn

**Плюсы**:
- ✅ Чистая архитектура
- ✅ Proxy + Tools.Core полностью AOT
- ✅ Можно deploy Proxy отдельно

**Минусы**:
- ❌ Большой refactoring (2-3 недели)
- ❌ Circular dependencies нужно разруливать
- ❌ Риск сломать существующий код

---

## Вывод

**Рекомендация**: **Вариант 1** (Proxy + Server без разделения сервисов)

**Причины**:
1. **95% пользы** от Proxy (экономия памяти)
2. **Минимальный риск** - не трогаем Services
3. **Быстрая реализация** - 1-2 недели
4. **46% сервисов AOT OK** - хорошо, но не критично

**В будущем** (если нужно):
- Можно постепенно выносить AOT-сервисы в отдельный пакет
- Добавить Roslyn subprocess для remote execution

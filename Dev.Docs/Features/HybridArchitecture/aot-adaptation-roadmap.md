# AOT Adaptation Roadmap - Как адаптировать инструменты к AOT

## Анализ NuGet зависимостей

### 🔴 Критические блокеры AOT (нет альтернатив)

| Пакет | Версия | AOT? | Причина | Используется в |
|-------|--------|------|---------|----------------|
| **Microsoft.CodeAnalysis.*** | 5.0.0 | ❌ | Reflection.Emit, dynamic loading | 85% инструментов |
| **System.Reflection.MetadataLoadContext** | 10.0.0 | ❌ | Dynamic assembly loading | SolutionManager |

**Вывод**: Эти пакеты **ЖЕСТКО блокируют AOT**. Обойти можно только через subprocess.

---

### 🟡 Проблемные (есть workaround)

| Пакет | Версия | AOT? | Обход | Сложность |
|-------|--------|------|-------|-----------|
| **Microsoft.Z3** | 4.12.2 | ⚠️ | Subprocess wrapper | Средняя |
| **ICSharpCode.Decompiler** | 10.0.0 | ⚠️ | Не критично, можно убрать | Низкая |

**Microsoft.Z3**:
- Native P/Invoke к libz3.so/dll
- AOT может работать с native interop, но сложно
- **Обход**: Запустить Z3 как отдельный процесс (stdin/stdout)

**ICSharpCode.Decompiler**:
- Используется где? (нужно проверить)
- Вероятно можно удалить

---

### ✅ AOT-совместимые (нет проблем)

| Пакет | Версия | AOT? | Использование |
|-------|--------|------|---------------|
| **Microsoft.Data.Sqlite** | 10.0.0 | ✅ | Vector DB, Symbol Cache |
| **Microsoft.Extensions.*** | 10.0.0 | ✅ | DI, HTTP, Caching |
| **DiffPlex** | 1.9.0 | ✅ | Text diff (pure C#) |
| **YamlDotNet** | 16.3.0 | ✅ | YAML parsing |
| **System.IO.Hashing** | 10.0.0 | ✅ | XxHash64 |
| **ModelContextProtocol** | 0.4.0 | ✅* | MCP framework |
| **System.CommandLine** | 2.0.0 | ✅ | CLI parsing |

*ModelContextProtocol нужно проверить, но скорее всего ✅

---

## Стратегии адаптации инструментов к AOT

### Стратегия 1: Subprocess Roslyn Wrapper

**Идея**: Roslyn работает в отдельном процессе, AOT процесс общается через gRPC/stdin.

```
┌──────────────────────────┐
│ AOT Process              │
│ - Все инструменты        │
│ - gRPC клиент к Roslyn   │
└──────────────────────────┘
         ↓ gRPC
┌──────────────────────────┐
│ Roslyn Process (НЕ AOT)  │
│ - Microsoft.CodeAnalysis │
│ - Workspace              │
│ - gRPC сервер            │
└──────────────────────────┘
```

**Что можно адаптировать**:
- ✅ ВСЕ read-only инструменты (view_definition, get_members, find_references)
- ✅ ВСЕ modification инструменты (modify_code, add_member, rename_symbol)
- ✅ ВСЕ analysis инструменты (analyze_complexity, trace_execution)

**Overhead**:
- gRPC latency: 0.1-0.5ms per call
- Сериализация: добавляет 10-20% к времени выполнения

**Сложность**: Высокая (нужен gRPC контракт для всех Roslyn операций)

---

### Стратегия 2: Предкомпилированный индекс (без runtime Roslyn)

**Идея**: Загрузить решение ОДИН РАЗ, построить индекс, сериализовать в SQLite. AOT процесс читает из SQLite.

```
┌────────────────────────────┐
│ Build-time Indexer (НЕ AOT)│
│ - Roslyn Workspace         │
│ - Extract ALL symbols      │
│ - Save to SQLite           │
└────────────────────────────┘
         ↓ SQLite
┌────────────────────────────┐
│ AOT Process                │
│ - Read from SQLite         │
│ - No Roslyn at runtime     │
└────────────────────────────┘
```

**Что можно адаптировать**:
- ✅ view_definition (если сохранен source code в индексе)
- ✅ get_members (если сохранены signatures)
- ✅ find_references (если построен reference graph)
- ✅ view_call_graph (если построен call graph)
- ✅ list_implementations (если сохранена иерархия)
- ✅ search_definitions (regex по индексу)
- ❌ modify_code (нужен Roslyn SyntaxTree rewriting)
- ❌ add_member (нужен Roslyn)
- ❌ rename_symbol (нужен Roslyn для refactoring)

**Проблема**: Индекс устаревает при изменении кода

**Решение**: Incremental update через Roslyn subprocess при изменениях

**Сложность**: Средняя (нужна хорошая схема БД и incremental update)

---

### Стратегия 3: Альтернативные парсеры (без Roslyn для некоторых операций)

**Идея**: Для некоторых операций Roslyn не нужен - можно использовать regex/простой парсинг.

**Примеры**:

#### 3.1. SemanticMerge (уже НЕ использует Roslyn!)
```csharp
// UltrasharpTools.Tools/Merge/Parsing/CSharpParser.cs
// Использует свой парсер, НЕ Roslyn!
public class CSharpParser {
    // Regex-based parsing
}
```
**Статус**: ✅ Уже AOT-готов

#### 3.2. LogAnalysis (уже AOT-готов)
```csharp
// UltrasharpTools.Tools/Services/LogAnalysisService.cs
// Regex парсинг логов
```
**Статус**: ✅ Уже AOT-готов

#### 3.3. Formatting (можно заменить)
Текущий: Roslyn Formatter (НЕ AOT)
Альтернатива: CSharpier (standalone tool через subprocess)

**Адаптация**:
```csharp
public async Task<string> FormatCode(string code) {
    // Вместо Roslyn Formatter:
    var result = await Process.Start("dotnet", "csharpier --write-stdout", code);
    return result.Output;
}
```
**Сложность**: Низкая

---

## Детальная адаптация по категориям

### 📋 Категория A: Легко адаптируется (без Roslyn или через subprocess)

#### ✅ **LogTools** - УЖЕ AOT
**Зависимости**: Regex, File I/O
**Адаптация**: Не требуется

#### ✅ **SnapshotTools** - УЖЕ AOT
**Зависимости**: File I/O, Git subprocess
**Адаптация**: Не требуется

#### ✅ **SystemTools** - УЖЕ AOT
**Зависимости**: HTTP health checks
**Адаптация**: Не требуется

#### ✅ **SemanticMergeTools** - УЖЕ AOT (почти)
**Зависимости**: Custom CSharpParser (НЕ Roslyn), EmbeddingGenerator
**Проблема**: Если EmbeddingGenerator использует local model (НЕ HTTP)
**Решение**: Эмбеддинги через HTTP к TEI/Ollama
**Сложность**: Низкая

#### ✅ **SemanticAnalysisTools** - Легко адаптируется
**Текущие зависимости**: `SemanticSearchService`, `ISolutionManager`

**Проблема**: `ISolutionManager` для получения source code

**Решение 1 (предкомпилированный индекс)**:
```csharp
// Вместо:
var symbol = await solutionManager.GetSymbol(fqn);
var code = symbol.GetSourceCode();

// Использовать:
var code = await indexDb.GetSourceCode(fqn);
```

**Решение 2 (Roslyn subprocess)**:
```csharp
var code = await roslynClient.GetSourceCodeAsync(fqn);
```

**Инструменты**:
- `semantic_search` - ✅ легко (индекс в SQLite)
- `semantic_diff` - ✅ легко (если source code в индексе)
- `find_duplicates` - ✅ легко (только векторы)
- `detect_code_clones` - ✅ легко (только векторы)

**Сложность**: Низкая

---

### 📋 Категория B: Средняя сложность (нужен subprocess или индекс)

#### ⚠️ **SolutionTools**
**Инструменты**:
- `load_solution` - построить индекс один раз (НЕ AOT builder)
- `load_project` - загрузить из индекса (AOT reader)

**Адаптация**:
```
Build-time:
  dotnet run UltrasharpTools.Indexer MySolution.sln
  → создает .ultrasharp/index.db

Runtime (AOT):
  Load from .ultrasharp/index.db
```

**Сложность**: Средняя (нужен schema для индекса)

---

#### ⚠️ **AnalysisTools** (read-only операции)
**Инструменты**:
- `view_definition` ✅ легко (source code в индексе)
- `get_members` ✅ легко (signatures в индексе)
- `find_references` ✅ средне (reference graph в индексе)
- `view_call_graph` ✅ средне (call graph в индексе)
- `list_implementations` ✅ легко (type hierarchy в индексе)
- `view_inheritance_chain` ✅ легко (hierarchy в индексе)
- `get_all_subtypes` ✅ легко (hierarchy в индексе)
- `search_definitions` ✅ легко (regex по индексу)
- `analyze_complexity` ⚠️ сложно (требует CFG из Roslyn)

**Стратегия**:
1. Простые (view, get, list) - предкомпилированный индекс
2. Сложные (analyze_complexity) - Roslyn subprocess

**Сложность**: Средняя

---

#### ⚠️ **TraceTools**
**Инструменты**:
- `trace_execution` ❌ сложно (Roslyn Control Flow Graph)
- `trace_backwards` ❌ сложно (Roslyn CFG + call graph)
- `analyze_path_feasibility` ❌ очень сложно (Roslyn CFG + Z3)
- `export_call_graph` ⚠️ средне (если call graph в индексе)

**Проблема**: Control Flow Graph требует Roslyn

**Решение 1 (предкомпилированный CFG)**:
Построить CFG на build-time, сериализовать в индекс
**Сложность**: Очень высокая (CFG очень большой)

**Решение 2 (Roslyn subprocess)**:
```csharp
var trace = await roslynClient.TraceExecutionAsync(entryPoint);
```
**Сложность**: Средняя

**Решение 3 (Z3 subprocess)**:
Для `analyze_path_feasibility` запустить Z3 отдельно
**Сложность**: Низкая

**Рекомендация**: Roslyn subprocess для trace, Z3 subprocess для symbolic execution

---

### 📋 Категория C: Сложно адаптируется (модификация кода)

#### ❌ **ModificationTools**
**Инструменты**:
- `modify_code` - Roslyn SyntaxTree rewriting
- `add_member` - Roslyn SyntaxTree manipulation
- `rename_symbol` - Roslyn refactoring
- `move_member` - Roslyn code movement
- `find_and_replace` - Roslyn validation после замены
- `replace_all_references` - Roslyn symbol tracking
- `manage_usings` - Roslyn using directives
- `manage_attributes` - Roslyn attributes

**Проблема**: SyntaxTree rewriting КРИТИЧЕСКИ требует Roslyn

**Альтернативы**:
1. ❌ Regex замена - небезопасно (может сломать код)
2. ✅ Roslyn subprocess - единственный вариант
3. ⚠️ LSP server (Language Server Protocol) - альтернатива Roslyn

**Решение (Roslyn subprocess)**:
```csharp
var result = await roslynClient.ModifyCodeAsync(
    fqn: "MyNamespace.MyClass.MyMethod",
    newCode: "public void MyMethod() { ... }"
);
```

**Сложность**: Средняя (нужен gRPC контракт для всех операций)

---

#### ❌ **QualityTools**
**Инструменты**:
- `format_code` - Roslyn Formatter
- `analyze_code_style` - Roslyn Analyzers
- `apply_code_fixes` - Roslyn CodeFixProviders

**Альтернативы**:

1. **format_code** → CSharpier subprocess ✅
```bash
echo "$code" | dotnet csharpier --write-stdout
```

2. **analyze_code_style** → Roslyn Analyzers subprocess ⚠️
Можно через dotnet build + читать .editorconfig

3. **apply_code_fixes** → Roslyn subprocess ❌
Нет альтернатив

**Рекомендация**:
- format_code через CSharpier (легко)
- остальные через Roslyn subprocess

**Сложность**: Низкая (format), Средняя (остальные)

---

#### ❌ **ValidationTools**
**Инструменты**:
- `validate_file` - Roslyn Diagnostics
- `validate_directory` - Roslyn Diagnostics batch

**Альтернативы**:
1. `dotnet build` subprocess и парсинг ошибок
2. Roslyn subprocess

**Рекомендация**: `dotnet build` subprocess (проще чем Roslyn gRPC)

**Сложность**: Низкая

---

#### ❌ **DocumentTools**
**Инструменты**:
- `read_file` - через Roslyn Document
- `create_file` - с Roslyn formatting
- `overwrite_file` - с Roslyn validation
- `list_file_entities` - Roslyn SyntaxTree parsing

**Альтернативы**:
1. `read_file` → System.IO.File.ReadAllText ✅ (тривиально)
2. `create_file` → File.WriteAllText + CSharpier ✅
3. `list_file_entities` → Roslyn subprocess ⚠️

**Рекомендация**: Простые операции через File I/O, парсинг через subprocess

**Сложность**: Низкая (read/write), Средняя (list_file_entities)

---

#### ⚠️ **FileOperationTools**
**Инструменты**:
- `split_file` - Roslyn parsing + file I/O
- `synthesize_files` - Roslyn parsing + merging

**Альтернатива**: Regex-based парсинг для простых случаев

**Рекомендация**: Roslyn subprocess для надежности

**Сложность**: Средняя

---

#### ❌ **TechnologyDetectionTools**
**Инструменты**:
- `detect_technology_stack` - Roslyn MSBuild integration

**Альтернатива**: XML парсинг .csproj файлов ✅

```csharp
var doc = XDocument.Load("MyProject.csproj");
var packages = doc.Descendants("PackageReference")
    .Select(p => new {
        Name = p.Attribute("Include")?.Value,
        Version = p.Attribute("Version")?.Value
    });
```

**Рекомендация**: XML парсинг (легко, AOT OK)

**Сложность**: Низкая

---

#### ⚠️ **PackageTools**
**Инструменты**:
- `add_package` - модификация .csproj + Roslyn reload

**Альтернативы**:
1. XML manipulation .csproj ✅
2. `dotnet add package` subprocess ✅
3. Roslyn reload → не нужен если есть incremental indexing

**Рекомендация**: `dotnet add package` subprocess

**Сложность**: Низкая

---

## Итоговая таблица адаптируемости

| Категория | Инструментов | AOT без усилий | Легко адаптировать | Средняя сложность | Сложно | Невозможно |
|-----------|--------------|----------------|--------------------|--------------------|--------|------------|
| **Utility** | 8 | 8 ✅ | - | - | - | - |
| **Semantic** | 6 | 1 ✅ | 5 ✅ | - | - | - |
| **Analysis (RO)** | 15 | - | 5 ✅ | 8 ⚠️ | 2 ❌ | - |
| **Modification** | 15 | - | - | - | 15 ❌ | - |
| **Quality** | 5 | - | 1 ✅ | 2 ⚠️ | 2 ❌ | - |
| **Validation** | 3 | - | 3 ✅ | - | - | - |
| **Files** | 5 | - | 2 ✅ | 3 ⚠️ | - | - |
| **Solution** | 2 | - | - | 2 ⚠️ | - | - |
| **Trace** | 4 | - | 1 ✅ | 2 ⚠️ | 1 ❌ | - |
| **Tech/Package** | 2 | - | 2 ✅ | - | - | - |
| **ИТОГО** | **65** | **9** (14%) | **19** (29%) | **17** (26%) | **20** (31%) | **0** |

---

## Практические рекомендации

### Сценарий 1: Минимальные усилия (1-2 недели)

**Цель**: AOT для Proxy + максимум простых инструментов

**Что делать**:
1. ✅ Proxy процесс (AOT) - роутинг к Core
2. ✅ Utility инструменты (уже AOT): LogTools, SnapshotTools, SystemTools
3. ✅ Semantic инструменты: эмбеддинги через HTTP
4. ✅ PackageTools: через `dotnet add package` subprocess
5. ✅ TechnologyDetection: через XML парсинг
6. ✅ QualityTools.format_code: через CSharpier subprocess

**Результат**:
- **14 инструментов AOT** (из 65) = 22%
- Экономия памяти: 3 proxy (60 MB) + Core (550 MB) = **610 MB** вместо 1500 MB

**ROI**: 🔥 Отличный (60% экономии памяти за 2 недели)

---

### Сценарий 2: Средние усилия (4-6 недель)

**Дополнительно к Сценарию 1**:

**Что делать**:
4. ⚠️ Roslyn subprocess wrapper для:
   - `view_definition`, `get_members`, `find_references`
   - `validate_file` (или через dotnet build)
5. ⚠️ Предкомпилированный индекс для:
   - Symbol cache
   - Call graph
   - Type hierarchy

**Результат**:
- **30+ инструментов AOT** (из 65) = 46%
- Все read-only операции работают через индекс/subprocess

**ROI**: ⚠️ Средний (много работы, средняя польза)

---

### Сценарий 3: Максимальные усилия (3-4 месяца)

**Дополнительно к Сценарию 2**:

**Что делать**:
6. ❌ Roslyn gRPC server для ВСЕ modification операций
7. ❌ Incremental индексация при изменениях
8. ❌ Z3 subprocess wrapper

**Результат**:
- **50+ инструментов AOT** (из 65) = 77%
- Только самые сложные trace операции остаются НЕ AOT

**ROI**: ❌ Плохой (4 месяца работы, marginal польза)

---

## Вывод и рекомендация

### ✅ Рекомендуется: Сценарий 1 (минимальные усилия)

**Причины**:
1. **60% экономии памяти** за 2 недели работы
2. **22% инструментов** становятся AOT (самые используемые)
3. **Низкий риск** (простые изменения)

**Что получаем**:
- Proxy AOT (главная польза)
- Utility инструменты AOT
- Semantic инструменты AOT
- Форматирование через CSharpier

**Что остается НЕ AOT**:
- Core процесс (85% инструментов) - но это OK, singleton

---

### ⚠️ НЕ рекомендуется: Сценарий 2-3 (средние/максимальные усилия)

**Причины**:
1. **Diminishing returns** - 80/20 правило
2. **Сложность** Roslyn subprocess архитектуры
3. **Maintenance burden** двух кодовых баз (AOT + Roslyn)
4. **Performance overhead** gRPC сериализации

**Лучше потратить время на**:
- Оптимизацию Symbol Cache
- Lazy loading проектов
- Incremental compilation

---

## Финальная стратегия

```
Phase 1 (2 недели): Proxy + простые инструменты AOT
  ✅ 60% экономии памяти
  ✅ 22% инструментов AOT

Phase 2 (опционально, если нужно):
  Roslyn subprocess ТОЛЬКО для критичных операций

Phase 3 (не делать):
  Полная миграция на AOT - не стоит усилий
```

**Главный вывод**: **Proxy процесс дает 95% пользы**, остальное - marginal improvements.

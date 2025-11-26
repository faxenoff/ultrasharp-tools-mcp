# Semantic Replace - Design Document

## Проблема

При работе с кодовой базой часто возникают задачи **массовых однотипных изменений**:

1. **Замена паттернов** — `Console.WriteLine` → `_logger.LogInformation`
2. **API миграции** — `oldMethod()` → `newMethod(withParams)`
3. **Рефакторинг** — добавление DI, изменение сигнатур
4. **Стандартизация** — приведение к code style

### Текущие ограничения

| Инструмент | Что делает | Ограничения |
|------------|-----------|-------------|
| `replace_all_references` | Заменяет все ссылки на символ | Только по FQN, замена одинаковая |
| `find_and_replace` | Regex замена | Нет семантического понимания контекста |
| `replace_references_by_pattern` | Batch переименование | Только имена, не код |

### Чего не хватает

1. **Batch preview с полным контекстом** — найти паттерн, вернуть весь метод/класс
2. **Индивидуальная трансформация** — каждое вхождение можно изменить по-своему
3. **Семантическая трансформация** — LLM понимает контекст и делает изменения

## Решение: `semantic_replace`

Двухрежимный инструмент для массовых изменений кода:

```
┌─────────────────────────────────────────────────────────────┐
│                    semantic_replace                          │
├─────────────────────────────────────────────────────────────┤
│ Режим 1: PREVIEW (batch вывод)                              │
│   pattern + scope → List<CodeMatch>                         │
│   Возвращает JSON с полным кодом каждого вхождения          │
├─────────────────────────────────────────────────────────────┤
│ Режим 2: APPLY                                              │
│   Вариант A - Manual: принимает batch изменений             │
│   Вариант B - Semantic: LLM трансформирует автоматически    │
└─────────────────────────────────────────────────────────────┘
```

## Архитектура

### Компоненты

```
┌─────────────────────────────────────────────────────────────┐
│                  SemanticReplaceService                      │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐  │
│ │  PatternMatcher │  │ ContextExtractor│  │ BatchApplier │  │
│ │  (Roslyn+Regex) │  │ (Full CodeUnit) │  │ (Atomic Tx)  │  │
│ └────────┬────────┘  └────────┬────────┘  └──────┬───────┘  │
│          │                    │                   │          │
│          ▼                    ▼                   ▼          │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │                    CodeMatchRegistry                     │ │
│ │  - Кэш найденных вхождений                              │ │
│ │  - Mapping: matchId → CodeUnit                          │ │
│ │  - Tracking: pending/applied/failed                      │ │
│ └─────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│ Optional: SemanticTransformer (LLM-based)                   │
│   - TEI/Ollama embedding для similarity                     │
│   - LLM API для трансформации (если включен)               │
└─────────────────────────────────────────────────────────────┘
```

### Уровни контекста (Scope)

```csharp
public enum ReplaceScope
{
    Statement,   // Только statement где найден паттерн
    Block,       // Весь блок (if/while/try)
    Member,      // Весь метод/property/field
    Type,        // Весь класс/struct/interface
    File         // Весь файл
}
```

### Модель данных

```csharp
/// <summary>
/// Результат поиска паттерна с полным контекстом
/// </summary>
public sealed record CodeMatch
{
    /// <summary>Уникальный ID для референса в apply</summary>
    public required string Id { get; init; }

    /// <summary>FQN контейнера (метод, класс)</summary>
    public required string ContainerFqn { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Строка где найден паттерн</summary>
    public required int MatchLine { get; init; }

    /// <summary>Полный код контейнера (scope)</summary>
    public required string FullCode { get; init; }

    /// <summary>Фрагмент где найден паттерн (highlighted)</summary>
    public required string MatchFragment { get; init; }

    /// <summary>Тип контейнера</summary>
    public required CodeUnitType ContainerType { get; init; }

    /// <summary>Метаданные (зависимости, используемые типы)</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Изменение для apply
/// </summary>
public sealed record CodeReplacement
{
    /// <summary>ID из CodeMatch</summary>
    public required string MatchId { get; init; }

    /// <summary>Новый код (полный код контейнера)</summary>
    public required string NewCode { get; init; }

    /// <summary>Опциональное описание изменения</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Результат применения изменений
/// </summary>
public sealed record ReplaceResult
{
    public required int TotalMatches { get; init; }
    public required int Applied { get; init; }
    public required int Failed { get; init; }
    public required int Skipped { get; init; }
    public required List<AppliedChange> Changes { get; init; }
    public required List<FailedChange> Failures { get; init; }
}
```

## API Design

### MCP Tool: `semantic_replace`

```typescript
// Параметры
interface SemanticReplaceParams {
  // === ПОИСК ===
  /** Regex или semantic query для поиска */
  pattern: string;

  /** Режим поиска: 'regex' | 'semantic' | 'roslyn' */
  searchMode?: 'regex' | 'semantic' | 'roslyn';

  /** Уровень контекста: 'statement' | 'block' | 'member' | 'type' | 'file' */
  scope?: 'statement' | 'block' | 'member' | 'type' | 'file';

  /** Фильтр по файлам (glob) */
  filePattern?: string;

  /** Фильтр по namespace */
  namespaceFilter?: string;

  /** Максимум результатов */
  limit?: number;

  // === ПРИМЕНЕНИЕ ===
  /** Batch изменений (для manual apply) */
  replacements?: CodeReplacement[];

  /** Описание трансформации (для semantic apply) */
  transformation?: string;

  /** Применить изменения (false = preview only) */
  apply?: boolean;

  /** Использовать LLM для трансформации */
  useSemanticModel?: boolean;

  /** Commit message */
  commitMessage?: string;
}
```

### Workflow 1: Manual Replace

```
1. PREVIEW:
   semantic_replace(
     pattern: "Console\\.WriteLine\\(",
     scope: "member",
     searchMode: "regex"
   )

   → Response:
   {
     "matches": [
       {
         "id": "match-001",
         "containerFqn": "MyApp.Services.UserService.CreateUser",
         "filePath": "Services/UserService.cs",
         "matchLine": 45,
         "fullCode": "public async Task<User> CreateUser(...) { ... }",
         "matchFragment": "Console.WriteLine($\"Creating user {name}\");"
       },
       // ... ещё 14 matches
     ],
     "total": 15
   }

2. APPLY (manual):
   semantic_replace(
     apply: true,
     replacements: [
       { "matchId": "match-001", "newCode": "public async Task<User> CreateUser(...) { _logger.LogInformation(...); ... }" },
       { "matchId": "match-002", "newCode": "..." }
     ],
     commitMessage: "Replace Console.WriteLine with ILogger"
   )
```

### Workflow 2: Semantic Replace

```
1. SEMANTIC TRANSFORM:
   semantic_replace(
     pattern: "Console.WriteLine",
     scope: "member",
     transformation: "Replace Console.WriteLine with _logger.LogInformation.
                      If ILogger is not injected, add it to constructor.",
     useSemanticModel: true,
     apply: true
   )

   → LLM анализирует каждый метод и трансформирует
   → Автоматический apply всех изменений
```

### Workflow 3: Hybrid (Preview + Selective Apply)

```
1. PREVIEW:
   semantic_replace(pattern: "throw new Exception", scope: "member")
   → 20 matches

2. USER: Отбирает 5 критичных для изменения

3. APPLY:
   semantic_replace(
     apply: true,
     replacements: [/* только 5 выбранных */]
   )
```

## Алгоритм поиска

### Roslyn Mode (searchMode: 'roslyn')

```csharp
// Для паттернов типа "MethodName", "TypeName.Method", "namespace.Class"
1. Parse pattern как FQN или partial FQN
2. Использовать FastSymbolIndex для O(1) lookup
3. Вернуть CodeMatch с полным контекстом (scope)
```

### Regex Mode (searchMode: 'regex')

```csharp
// Для произвольных текстовых паттернов
1. Компилировать regex
2. Для каждого .cs файла:
   a. Найти все matches в тексте
   b. Для каждого match → определить containing syntax node
   c. Расширить до scope (member/type/file)
   d. Создать CodeMatch
```

### Semantic Mode (searchMode: 'semantic')

```csharp
// Для естественного языка типа "logging statements", "error handling"
1. Сгенерировать embedding для query
2. Найти похожие CodeUnits через VectorStore
3. Отфильтровать по similarity threshold (>0.7)
4. Вернуть CodeMatch с контекстом
```

## Применение изменений

### Atomic Transaction

```csharp
public async Task<ReplaceResult> ApplyReplacementsAsync(
    List<CodeReplacement> replacements,
    string? commitMessage,
    CancellationToken ct)
{
    // 1. Валидация всех изменений
    var validationErrors = ValidateReplacements(replacements);
    if (validationErrors.Any())
        return ReplaceResult.Failed(validationErrors);

    // 2. Группировка по файлам
    var byFile = replacements.GroupBy(r => GetFilePath(r.MatchId));

    // 3. Snapshot для отката
    var snapshotId = await _snapshotService.CreateAsync("before-semantic-replace");

    try
    {
        // 4. Применение по файлам
        foreach (var fileGroup in byFile)
        {
            await ApplyFileReplacementsAsync(fileGroup.Key, fileGroup.ToList(), ct);
        }

        // 5. Format code
        await _formattingService.FormatFilesAsync(byFile.Select(g => g.Key), ct);

        // 6. Validate (compile check)
        var diagnostics = await _diagnosticService.GetDiagnosticsAsync(ct);
        if (diagnostics.HasErrors)
        {
            await _snapshotService.RollbackAsync(snapshotId);
            return ReplaceResult.Failed("Compilation errors after changes");
        }

        // 7. Git commit
        if (commitMessage != null)
        {
            await _gitService.CommitAsync(commitMessage);
        }

        return ReplaceResult.Success(replacements.Count);
    }
    catch
    {
        await _snapshotService.RollbackAsync(snapshotId);
        throw;
    }
}
```

## Интеграция с существующими компонентами

### Используемые сервисы

| Сервис | Использование |
|--------|---------------|
| `FastSymbolIndex` | O(1) поиск символов по FQN |
| `CodeAnalysisService` | Получение CodeUnit по FQN |
| `CodeModificationService` | Применение изменений |
| `FormattingService` | Форматирование после изменений |
| `DiagnosticService` | Проверка на ошибки компиляции |
| `GitService` | Commit изменений |
| `SnapshotService` | Откат при ошибках |
| `EmbeddingGenerator` | Семантический поиск (опционально) |
| `VectorStore` | Хранение embeddings (опционально) |

### Новые компоненты

```
UltrasharpTools.Tools/
├─ Replace/
│  ├─ Models/
│  │  ├─ CodeMatch.cs
│  │  ├─ CodeReplacement.cs
│  │  └─ ReplaceResult.cs
│  │
│  ├─ Services/
│  │  ├─ PatternMatcherService.cs      # Поиск паттернов
│  │  ├─ ContextExtractorService.cs    # Извлечение контекста
│  │  ├─ BatchReplacerService.cs       # Применение изменений
│  │  └─ SemanticTransformerService.cs # LLM трансформация (опц.)
│  │
│  └─ SemanticReplaceService.cs        # Orchestrator
│
└─ Mcp/Tools/
   └─ SemanticReplaceTools.cs          # MCP endpoints
```

## Edge Cases

### 1. Overlapping Changes

```csharp
// Match 1: метод A содержит Console.WriteLine
// Match 2: метод B (внутри A) тоже содержит Console.WriteLine
// → Применяем от внутренних к внешним (depth-first)
```

### 2. Syntax Errors в результате

```csharp
// User предоставил невалидный newCode
// → Валидация через Roslyn перед применением
// → Откат если не компилируется
```

### 3. Partial Applies

```csharp
// 10 replacements, 3 падают
// → Применить успешные? Или all-or-nothing?
// → Конфигурируемо: AllOrNothing | BestEffort
```

### 4. Large Batches

```csharp
// 1000+ matches
// → Pagination в preview
// → Parallel apply с rate limiting
```

## Примеры использования

### Пример 1: Замена логирования

```
Pattern: Console\.WriteLine\(
Scope: member
Transformation: Replace with _logger.LogInformation(), inject ILogger if missing
```

### Пример 2: API миграция

```
Pattern: HttpClient\.GetAsync\(
Scope: block
Transformation: Wrap in retry policy, add cancellation token
```

### Пример 3: Null checks

```
Pattern: if.*==.*null.*throw
Scope: statement
Transformation: Replace with ArgumentNullException.ThrowIfNull()
```

### Пример 4: Async suffix

```
Pattern: async.*Task.*\w+(?<!Async)\(
Scope: member
SearchMode: roslyn (method declarations)
Transformation: Add Async suffix to method name
```

## Метрики успеха

| Метрика | Target |
|---------|--------|
| Preview latency | < 2 sec для 100 matches |
| Apply latency | < 5 sec для 50 replacements |
| Accuracy (semantic) | > 90% корректных трансформаций |
| Rollback reliability | 100% успешных откатов |

## Риски и митигация

| Риск | Митигация |
|------|-----------|
| LLM галлюцинации | Валидация через Roslyn, preview before apply |
| Большие batches OOM | Streaming, pagination |
| Breaking changes | Snapshot + rollback, compile check |
| Performance degradation | Caching, parallel processing |

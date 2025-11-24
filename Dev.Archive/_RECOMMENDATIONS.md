# Рекомендации по добавлению функционала из ultrascript-tools-mcp

## 🎯 Векторный поиск и семантический анализ

### ✅ Что уже есть в UltraSharp

**Инфраструктура:**
- `SemanticEmbeddingConfig.cs` - конфигурация TEI/Ollama/Memory
- `SemanticSearchService.cs` - сервис семантического поиска
- `CodeSemanticIndexer.cs` - индексация кода
- `VectorBasedSemanticSimilarityService.cs` - cosine similarity
- `setup-semantic-embedding.ps1` - автоустановка

**MCP Tools:**
- ✅ `SemanticMerge` - семантический Git merge (реализован)
- ❌ `SemanticSearch` - поиск похожего кода (ТОЛЬКО в документации)
- ❌ `SemanticDiff` - семантическое сравнение (ТОЛЬКО в документации)

---

## 🔥 ПРИОРИТЕТ 1: Реализовать недостающие MCP Tools

### 1. SemanticSearch Tool

**Что делает (из ultrascript):**
```typescript
semantic_search({
  query: "authentication logic",
  topK: 10,
  minSimilarity: 0.7
})

// Результат:
[
  { fqn: "UserService.Login", similarity: 0.92 },
  { fqn: "AuthService.Authenticate", similarity: 0.88 },
  { fqn: "TokenValidator.Verify", similarity: 0.81 }
]
```

**Реализация для C#:**
```csharp
// Создать файл: UltrasharpTools.Tools/Mcp/Tools/SemanticAnalysisTools.cs

[McpServerTool(Name = "semantic_search", Idempotent = true, ReadOnly = true)]
[Description("Find semantically similar code using natural language or code snippet")]
public static async Task<object> SemanticSearch(
    SemanticSearchService searchService,
    ISolutionManager solutionManager,
    ILogger<SolutionToolsLogCategory> logger,

    [Description("Natural language query or code snippet")]
    string query,

    [Description("Search scope: solution, project, namespace")]
    string scope = "solution",

    [Description("Number of results (1-50)")]
    int topK = 10,

    [Description("Minimum similarity threshold (0.0-1.0)")]
    float minSimilarity = 0.7f,

    CancellationToken cancellationToken)
{
    return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
    {
        // 1. Индексация если не была выполнена
        await searchService.IndexCurrentSolutionAsync(cancellationToken);

        // 2. Поиск похожего кода
        var results = await searchService.FindSimilarCodeAsync(
            query,
            topK,
            minSimilarity,
            cancellationToken);

        // 3. Форматирование результатов
        return ToolHelpers.ToJson(new {
            query,
            resultsCount = results.Count,
            results = results.Select(r => new {
                fqn = r.FullyQualifiedName,
                similarity = Math.Round(r.Similarity * 100, 2) + "%",
                filePath = r.FilePath,
                lineNumber = r.LineNumber,
                type = r.Type.ToString(),
                codePreview = r.Code.Take(200) + "..." // первые 200 символов
            })
        });
    }, logger, nameof(SemanticSearch), cancellationToken);
}
```

**Интеграция:**
- Использует существующий `SemanticSearchService`
- Работает с TEI/Ollama через готовую инфраструктуру
- Token-efficient (возвращает FQN для дальнейшей работы с `view_definition`)

---

### 2. SemanticDiff Tool

**Что делает:**
Сравнивает два метода/класса семантически (не текстуально)

**Реализация:**
```csharp
[McpServerTool(Name = "semantic_diff", Idempotent = true, ReadOnly = true)]
[Description("Compare two code entities semantically to detect behavior changes")]
public static async Task<object> SemanticDiff(
    VectorBasedSemanticSimilarityService similarityService,
    ISolutionManager solutionManager,
    ILogger<SolutionToolsLogCategory> logger,

    [Description("FQN of first entity (before)")]
    string beforeFqn,

    [Description("FQN of second entity (after)")]
    string afterFqn,

    [Description("Include implementation details")]
    bool includeImplementationDetails = false,

    CancellationToken cancellationToken)
{
    return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
    {
        // 1. Получить код обоих entities
        var beforeCode = await GetEntityCodeAsync(beforeFqn, solutionManager);
        var afterCode = await GetEntityCodeAsync(afterFqn, solutionManager);

        // 2. Вычислить embeddings
        var beforeEmbedding = await similarityService.GetEmbeddingAsync(beforeCode);
        var afterEmbedding = await similarityService.GetEmbeddingAsync(afterCode);

        // 3. Cosine similarity
        var similarity = similarityService.CosineSimilarity(beforeEmbedding, afterEmbedding);

        // 4. Анализ изменений
        var semanticChange = AnalyzeSemanticChange(beforeCode, afterCode, similarity);

        return ToolHelpers.ToJson(new {
            beforeFqn,
            afterFqn,
            semanticSimilarity = Math.Round(similarity * 100, 2) + "%",
            behaviorPreserved = similarity > 0.95,
            changeCategory = semanticChange.Category, // "refactoring", "logic_change", "breaking"
            description = semanticChange.Description,
            risks = semanticChange.Risks
        });
    }, logger, nameof(SemanticDiff), cancellationToken);
}
```

---

### 3. detect_code_clones Tool

**Что делает (из ultrascript):**
```typescript
detect_code_clones({
  minSimilarity: 0.85,
  groupBy: "semantic"  // или "exact", "structural"
})

// Результат:
[
  {
    groupId: "clone-1",
    similarity: 0.92,
    members: [
      { fqn: "UserService.ValidateEmail", file: "UserService.cs:45" },
      { fqn: "EmailValidator.IsValid", file: "EmailValidator.cs:12" },
      { fqn: "Registration.CheckEmail", file: "Registration.cs:89" }
    ],
    recommendation: "Extract to EmailValidationHelper"
  }
]
```

**Реализация:**
```csharp
[McpServerTool(Name = "detect_code_clones", Idempotent = true, ReadOnly = true)]
[Description("Find duplicate or similar code blocks using semantic analysis")]
public static async Task<object> DetectCodeClones(
    SemanticSearchService searchService,
    ISolutionManager solutionManager,
    ILogger<SolutionToolsLogCategory> logger,

    [Description("Minimum similarity threshold (0.0-1.0)")]
    float minSimilarity = 0.85f,

    [Description("Clone detection mode: semantic, structural, exact")]
    string mode = "semantic",

    [Description("Only methods/properties (skip classes)")]
    bool membersOnly = true,

    CancellationToken cancellationToken)
{
    return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () =>
    {
        // 1. Индексация
        await searchService.IndexCurrentSolutionAsync(cancellationToken);

        // 2. Получить все методы/классы
        var allEntities = await GetAllCodeEntitiesAsync(solutionManager, membersOnly);

        // 3. Группировка похожих
        var cloneGroups = await FindCloneGroupsAsync(
            allEntities,
            searchService,
            minSimilarity,
            mode);

        // 4. Рекомендации по рефакторингу
        var recommendations = GenerateRefactoringRecommendations(cloneGroups);

        return ToolHelpers.ToJson(new {
            totalCloneGroups = cloneGroups.Count,
            totalDuplicates = cloneGroups.Sum(g => g.Members.Count),
            groups = cloneGroups.Select(g => new {
                groupId = g.Id,
                avgSimilarity = Math.Round(g.AvgSimilarity * 100, 2) + "%",
                members = g.Members.Select(m => new {
                    fqn = m.Fqn,
                    file = m.FilePath + ":" + m.LineNumber
                }),
                recommendation = g.RefactoringRecommendation
            })
        });
    }, logger, nameof(DetectCodeClones), cancellationToken);
}
```

**Алгоритм FindCloneGroupsAsync:**
```csharp
private async Task<List<CloneGroup>> FindCloneGroupsAsync(
    List<CodeEntity> entities,
    SemanticSearchService searchService,
    float minSimilarity,
    string mode)
{
    var cloneGroups = new List<CloneGroup>();
    var processed = new HashSet<string>();

    foreach (var entity in entities)
    {
        if (processed.Contains(entity.Id)) continue;

        // Найти все похожие на этот entity
        var similarEntities = await searchService.FindSimilarCodeAsync(
            entity.Code,
            topK: 50, // поиск в топ-50
            minSimilarity,
            CancellationToken.None);

        if (similarEntities.Count > 1) // минимум 2 дубликата
        {
            var group = new CloneGroup
            {
                Id = $"clone-{cloneGroups.Count + 1}",
                Members = similarEntities,
                AvgSimilarity = similarEntities.Average(e => e.Similarity)
            };

            cloneGroups.Add(group);

            // Пометить все как обработанные
            foreach (var similar in similarEntities)
                processed.Add(similar.Id);
        }
    }

    return cloneGroups;
}
```

---

## 🟡 ПРИОРИТЕТ 2: Дополнительные инструменты

### 4. analyze_hotspots Tool

**Интеграция с существующим analyze_complexity:**
```csharp
[McpServerTool(Name = "analyze_hotspots", Idempotent = true, ReadOnly = true)]
[Description("Find code hotspots based on complexity, coupling, and change frequency")]
public static async Task<object> AnalyzeHotspots(
    ISolutionManager solutionManager,
    IComplexityAnalyzer complexityAnalyzer,
    SemanticSearchService searchService,
    ILogger<SolutionToolsLogCategory> logger,

    [Description("Git repository path for change history analysis")]
    string? gitRepoPath = null,

    [Description("Top N hotspots to return")]
    int topN = 10,

    CancellationToken cancellationToken)
{
    // 1. Сложность (уже есть в analyze_complexity)
    // 2. Coupling - через FindReferences
    // 3. Change frequency - через Git log (если gitRepoPath)
    // 4. Комбинированная метрика
}
```

---

### 5. Snapshot/Rollback Tools

**Улучшение существующего `undo`:**
```csharp
[McpServerTool(Name = "create_snapshot")]
[Description("Create version snapshot for rollback using git stash or .backup/")]
public static async Task<object> CreateSnapshot(...)

[McpServerTool(Name = "rollback_snapshot")]
[Description("Rollback to specific snapshot by ID")]
public static async Task<object> RollbackSnapshot(...)

[McpServerTool(Name = "list_snapshots")]
[Description("List all available snapshots")]
public static async Task<object> ListSnapshots(...)
```

---

### 6. File Operations (Token-Efficient)

```csharp
[McpServerTool(Name = "split_file")]
[Description("Split large file into modules by class/namespace")]
public static async Task<object> SplitFile(...)

[McpServerTool(Name = "synthesize_files")]
[Description("Combine multiple files into one")]
public static async Task<object> SynthesizeFiles(...)
```

---

## 📊 Сравнение: ultrascript vs ultrasharp

| Функционал | ultrascript (TS/JS) | ultrasharp (C#) | Приоритет |
|------------|---------------------|-----------------|-----------|
| **Embedding инфраструктура** | ✅ SQLite + vector store | ✅ TEI/Ollama/Memory | ✅ Готово |
| **semantic_search tool** | ✅ Реализован | ❌ Только сервис | 🔥 Высокий |
| **semantic_diff tool** | ✅ Реализован | ❌ Только сервис | 🔥 Высокий |
| **detect_code_clones** | ✅ 2 варианта (ML + JSCPD) | ❌ | 🔥 Высокий |
| **analyze_hotspots** | ✅ С Git интеграцией | ⚠️ Частично | 🟡 Средний |
| **Snapshots** | ✅ 4 tool | ⚠️ Только undo | 🟡 Средний |
| **File operations** | ✅ split/synthesize | ❌ | 🟡 Средний |

---

## 🚀 План реализации

### Фаза 1: Векторный поиск (1-2 недели)
1. ✅ Создать `SemanticAnalysisTools.cs`
2. ✅ Реализовать `semantic_search` tool
3. ✅ Реализовать `semantic_diff` tool
4. ✅ Реализовать `detect_code_clones` tool
5. ✅ Написать интеграционные тесты

### Фаза 2: Hotspots (1 неделя)
1. ✅ Расширить `analyze_complexity`
2. ✅ Добавить Git change frequency analysis
3. ✅ Реализовать `analyze_hotspots` tool

### Фаза 3: Snapshots (1 неделя)
1. ✅ Создать `SnapshotManager`
2. ✅ Реализовать snapshot tools
3. ✅ Интеграция с существующим `undo`

### Фаза 4: File Operations (опционально)
1. ⚠️ `split_file` - рефакторинг больших файлов
2. ⚠️ `synthesize_files` - объединение модулей

---

## 💡 Ключевые преимущества

### Что даст реализация:

1. **semantic_search:**
   - "Найди все валидаторы email" → мгновенный результат
   - Не нужно знать точные имена классов/методов
   - Работает на естественном языке

2. **detect_code_clones:**
   - Автоматическое обнаружение дубликатов
   - Рекомендации по рефакторингу
   - Метрики технического долга

3. **analyze_hotspots:**
   - Приоритизация рефакторинга
   - Выявление проблемных участков
   - Интеграция с Git history

4. **Snapshots:**
   - Безопасное экспериментирование
   - Множественные точки восстановления
   - Независимость от Git

---

## 🔧 Технические детали

### Использование существующей инфраструктуры:

```csharp
// Всё уже работает через DI!
services.WithEmbeddingServices(options => {
    options.Provider = "auto";  // TEI если RTX 30xx+, иначе Ollama
    options.AutoDetectGPU = true;
});

// В MCP tool:
public static async Task<object> SemanticSearch(
    SemanticSearchService searchService,  // ← DI автоматически
    ...
)
```

### Vector Store:

У вас уже есть:
- ✅ TEI provider (8192 токена контекста)
- ✅ Ollama provider (512 токенов)
- ✅ Memory provider (fallback)
- ✅ Auto-detection GPU capabilities
- ✅ Cosine similarity через SIMD (если доступно)

Можно добавить из ultrascript:
- ⚠️ SQLite vector store (sqlite-vec/vectorlite) - более эффективное хранение
- ⚠️ Adaptive backend switching (auto выбор по размеру кодовой базы)

НО это не критично - текущая реализация уже работает!

---

## 📚 Что НЕ стоит брать

### ❌ Multi-agent архитектура
**Причина:** Roslyn уже предоставляет всю информацию синхронно
- Не нужны Parser/VectorDB/Query агенты
- Roslyn Semantic Model быстрее чем Code Graph в SQLite

### ❌ Branch-aware indexing
**Причина:** Roslyn работает с текущим состоянием workspace
- Git branch switching handled by IDE
- Переиндексация не требуется при смене веток

### ❌ JSCPD clone detection
**Причина:** Semantic clone detection через embeddings лучше
- ML embeddings ловят больше дубликатов
- Уже есть инфраструктура (TEI/Ollama)

---

## 🎯 Выводы

### Что делать ПРЯМО СЕЙЧАС:

1. **Создать `SemanticAnalysisTools.cs`** с тремя tools:
   - `semantic_search`
   - `semantic_diff`
   - `detect_code_clones`

2. **Использовать существующие сервисы:**
   - `SemanticSearchService` - готов к использованию
   - `VectorBasedSemanticSimilarityService` - работает
   - `CodeSemanticIndexer` - индексирует код

3. **Минимальные изменения:**
   - Только новые MCP tools (3 метода)
   - Без изменения существующей инфраструктуры
   - Полная совместимость с текущим кодом

### Результат:

✅ **Полноценный семантический поиск** на основе TEI/Ollama
✅ **Обнаружение дубликатов кода** автоматически
✅ **Семантическое сравнение** для code review
✅ **Token-efficient** workflow через FQN

---

## 📞 Следующие шаги

Хотите, чтобы я:
1. Создал полную реализацию `SemanticAnalysisTools.cs`?
2. Написал тесты для новых tools?
3. Обновил документацию?
4. Создал PR с изменениями?

Выберите любой вариант, и начнём!

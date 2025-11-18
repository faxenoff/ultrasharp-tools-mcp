# Clone Detection Unification Analysis

**Дата:** 2025-11-18
**Версия проекта:** 2.2.0
**Анализ:** detect_code_clones vs find_duplicates

---

## 📊 Executive Summary

**Вывод:** `detect_code_clones` и `find_duplicates` - **РАЗНЫЕ ИНСТРУМЕНТЫ** с разными use cases.
**Рекомендация:** **НЕ унифицировать**. Реализовать Overlord `detect_code_clones` через MCP Proxy вызов локального Tools версии.

**Обоснование:**
- `find_duplicates` - **QUERY-based** (дай код, найди похожие)
- `detect_code_clones` - **BATCH ANALYSIS** (просканируй всё, найди все группы клонов)
- Разные входные параметры, разные алгоритмы, разные выходные данные
- Разные сценарии использования

---

## 🔍 Детальный анализ трёх реализаций

### 1. Overlord `find_duplicates` (COMPLETE)

**Файл:** `UltrasharpTools.Overlord/Services/McpProxyService.cs:121-201`

#### Сигнатура:
```csharp
private async Task<string> ExecuteFindDuplicates(
    string argumentsJson,
    string? projectContext,
    CancellationToken cancellationToken)

private sealed class FindDuplicatesArgs
{
    public string? TargetCode { get; set; }
    public float[]? TargetVector { get; set; }
    public double Threshold { get; set; } = 0.7;
    public string Scope { get; set; } = "current_project";
    public int Limit { get; set; } = 10;
}
```

#### Алгоритм:
```
1. Parse arguments: TargetCode OR TargetVector
2. IF TargetCode provided:
     queryVector = await embeddingService.GetEmbeddingAsync(TargetCode)
   ELSE:
     queryVector = TargetVector (pre-computed from Droid)
3. Search vector store:
     matches = await _vectorStore.SearchAcrossProjectsAsync(
         queryVector, threshold, limit, projects)
4. Return: { matchCount, matches }
```

#### Входные данные:
- **TargetCode** (string) - код для поиска дубликатов
- **TargetVector** (float[]) - опциональный pre-computed embedding
- **Threshold** (double) - порог схожести (default 0.7)
- **Scope** (string) - current_project / all
- **Limit** (int) - количество результатов (default 10)

#### Выходные данные:
```json
{
  "matchCount": 5,
  "matches": [
    {
      "project": "ProjectA",
      "branch": "main",
      "file": "Utils.cs",
      "line": 42,
      "fqn": "MyNamespace.Utils.Helper",
      "codeSnippet": "...",
      "similarity": 0.89
    }
  ]
}
```

#### Use Case:
**"У меня есть этот код, найди похожие в других проектах"**

**Примеры:**
- Разработчик нашёл метод, хочет найти похожие реализации в других проектах
- Code review: проверить, есть ли уже подобный код перед добавлением нового
- Refactoring: найти все места с похожей логикой для консолидации

---

### 2. Overlord `detect_code_clones` (STUB)

**Файл:** `UltrasharpTools.Overlord/Services/McpProxyService.cs:739-766`

#### Текущая реализация:
```csharp
private async Task<string> ExecuteDetectCodeClones(
    string argumentsJson,
    string? projectContext,
    CancellationToken cancellationToken)
{
    var args = JsonSerializer.Deserialize<DetectCodeClonesArgs>(argumentsJson);

    // detect_code_clones - это поиск всех дубликатов во всех проектах
    // Используем тот же механизм что и find_duplicates, но по всем файлам

    return JsonSerializer.Serialize(new
    {
        message = "detect_code_clones requires pre-indexed vector store",
        hint = "This tool analyzes all code in vector store to find clones. Use find_duplicates for specific code search.",
        suggestion = "For now, use find_duplicates with targetCode to find similar code across projects"
    });
}

private sealed class DetectCodeClonesArgs
{
    public double Threshold { get; set; } = 0.8;
    public int Limit { get; set; } = 50;
}
```

#### Статус:
**STUB** - возвращает helpful message с рекомендацией использовать `find_duplicates`.

#### Комментарии в коде:
```csharp
// detect_code_clones - это поиск всех дубликатов во всех проектах
// Используем тот же механизм что и find_duplicates, но по всем файлам
```

**Интерпретация:** Автор намеревался использовать тот же механизм (vector search), но для **всех файлов**, а не для конкретного target code.

---

### 3. Tools `detect_code_clones` (COMPLETE)

**Файл:** `UltrasharpTools.Tools/Mcp/Tools/SemanticAnalysisTools.cs:236-319`

#### Сигнатура:
```csharp
[McpServerTool(Name = "detect_code_clones")]
public static async Task<object> DetectCodeClones(
    SemanticSearchService searchService,
    ISolutionManager solutionManager,
    ILogger<SemanticAnalysisToolsLogCategory> logger,
    float minSimilarity = 0.85f,
    string mode = "semantic",
    bool membersOnly = true,
    int maxGroups = 20,
    CancellationToken cancellationToken = default)
```

#### Алгоритм:
```
1. Check if solution indexed, if not - index:
     if (!searchService.IsIndexed())
         await searchService.IndexCurrentSolutionAsync()

2. Get ALL code entities from solution:
     entities = await GetAllCodeEntitiesAsync(solutionManager, membersOnly)
     // Extracts ALL methods/classes from ALL documents

3. Find clone groups:
     cloneGroups = await FindCloneGroupsAsync(searchService, entities, minSimilarity, maxGroups)

     Algorithm:
     FOR EACH entity IN entities:
         IF entity NOT processed:
             similarEntities = searchService.FindSimilarCodeAsync(entity.Code, limit=50, minSimilarity)

             IF similarEntities.Count >= 2:
                 CREATE CloneGroup:
                     - members: all similar entities
                     - avgSimilarity: average of all similarities
                     - cloneType: exact/very_similar/similar/conceptually_similar
                     - refactoringRecommendation: based on cloneCount
                     - priority: critical/high/medium/low (score = cloneCount * avgSimilarity)

                 MARK all members as processed

4. Return clone groups sorted by priority (descending)
```

#### Входные данные:
- **minSimilarity** (float) - минимальная схожесть (default 0.85)
- **mode** (string) - semantic/exact/similar (default "semantic")
- **membersOnly** (bool) - только методы или + классы (default true)
- **maxGroups** (int) - максимум групп (default 20)

#### Выходные данные:
```json
{
  "totalCloneGroups": 5,
  "totalDuplicateEntities": 18,
  "groups": [
    {
      "groupId": "clone-1",
      "cloneType": "very_similar",
      "avgSimilarity": "92.5%",
      "members": [
        {
          "fqn": "ProjectA.Utils.Helper1",
          "filePath": "D:/code/Utils.cs",
          "line": 42
        },
        {
          "fqn": "ProjectB.Common.Helper2",
          "filePath": "D:/code/Common.cs",
          "line": 128
        }
      ],
      "estimatedLinesOfCode": 25,
      "refactoringRecommendation": "Consider consolidating into single method",
      "priority": "high"
    }
  ]
}
```

#### Use Case:
**"Просканируй весь codebase, найди все группы похожего кода для рефакторинга"**

**Примеры:**
- Technical debt analysis: найти все дублирования для рефакторинга
- Code quality improvement: систематически устранить code clones
- Codebase health check: оценить уровень дублирования
- Refactoring planning: понять, где больше всего повторений

---

## 📐 Сравнительная таблица

| Аспект | find_duplicates (Overlord) | detect_code_clones (Tools) |
|--------|---------------------------|---------------------------|
| **Тип операции** | QUERY-based search | BATCH analysis |
| **Входные данные** | TargetCode/TargetVector (конкретный код) | Parameters only (threshold, mode) |
| **Алгоритм** | SearchAcrossProjects(queryVector) | GetAllEntities() → CompareAll() → GroupSimilar() |
| **Выходные данные** | List of matches to specific code | Groups of similar code with refactoring recommendations |
| **Сложность** | O(1) query - single vector search | O(N²) worst-case - all entities compared |
| **Scope** | Cross-project vector store | Current solution/project |
| **Цель** | "Найди похожие на ЭТОТ код" | "Найди ВСЕ клоны в кодовой базе" |
| **Use Case** | Code review, find existing implementations | Technical debt analysis, refactoring planning |
| **Performance** | Fast (single search query) | Resource-intensive (full codebase scan) |
| **Results grouping** | No grouping - flat list of matches | Grouped by similarity with priorities |
| **Refactoring info** | No | Yes (recommendations, priority, estimated LOC) |

---

## 🎯 Архитектурные различия

### find_duplicates - Query Pattern:
```
INPUT: Specific Code/Vector
       ↓
[Vector Search Engine]
       ↓
OUTPUT: Matches sorted by similarity
```

**Аналогия:** Как Google search - даёшь query, получаешь результаты.

### detect_code_clones - Batch Analysis Pattern:
```
INPUT: Parameters (threshold, mode, etc.)
       ↓
[Get ALL Code Entities]
       ↓
[Compare Each Entity with All Others]
       ↓
[Group Similar Entities]
       ↓
[Calculate Priorities]
       ↓
OUTPUT: Clone groups with refactoring recommendations
```

**Аналогия:** Как antivirus full scan - сканирует всё, группирует проблемы, приоритизирует.

---

## 🔬 Функциональные различия

### 1. Входные требования

**find_duplicates:**
- ТРЕБУЕТ конкретный code/vector для поиска
- Без target code/vector не может работать
- User-driven: пользователь выбирает, что искать

**detect_code_clones:**
- НЕ требует конкретный код
- Работает с параметрами (threshold, mode)
- System-driven: система сама находит все клоны

### 2. Алгоритмическая сложность

**find_duplicates:**
```
Time: O(log N) - vector search with indexed store
Space: O(K) - K matches returned
```

**detect_code_clones:**
```
Time: O(N * M) - N entities, M = search limit (50)
      Worst-case: O(N²) if no early stopping
Space: O(G * M) - G groups, M members per group
```

### 3. Результаты

**find_duplicates:**
```json
[
  { "project": "A", "file": "X", "similarity": 0.89 },
  { "project": "B", "file": "Y", "similarity": 0.82 }
]
```
- Плоский список
- Отсортирован по similarity
- Нет группировки
- Нет рекомендаций

**detect_code_clones:**
```json
{
  "groups": [
    {
      "id": "clone-1",
      "cloneType": "very_similar",
      "avgSimilarity": 0.92,
      "members": [...],
      "refactoringRecommendation": "Extract to shared utility method - 5 duplicates found",
      "priority": "critical"
    }
  ]
}
```
- Группировка по схожести
- Классификация типов клонов
- Refactoring recommendations
- Priority scoring

---

## 💡 Use Case сценарии

### Когда использовать find_duplicates:

**Сценарий 1: Code Review**
```
Developer: "Я написал этот метод, есть ли уже похожий?"
Action: find_duplicates(targetCode: myNewMethod, scope: "all")
Result: Найдены 2 похожих метода в других проектах
```

**Сценарий 2: Refactoring**
```
Developer: "У меня есть этот utility метод, где ещё он используется?"
Action: find_duplicates(targetCode: utilityMethod, threshold: 0.8)
Result: Найдены все похожие реализации для консолидации
```

**Сценарий 3: Learning**
```
Developer: "Как обычно реализуют JWT validation в нашей компании?"
Action: find_duplicates(targetCode: myJwtValidation, scope: "all")
Result: Найдены best practices примеры из других проектов
```

### Когда использовать detect_code_clones:

**Сценарий 1: Technical Debt Analysis**
```
Tech Lead: "Сколько дублированного кода у нас в проекте?"
Action: detect_code_clones(minSimilarity: 0.85, maxGroups: 50)
Result: 15 групп клонов, 67 методов-дубликатов, prioritized список для рефакторинга
```

**Сценарий 2: Refactoring Planning**
```
Developer: "Какие методы стоит рефакторить в первую очередь?"
Action: detect_code_clones(minSimilarity: 0.9, membersOnly: true)
Result: Top 10 групп с priority: critical/high и рекомендациями
```

**Сценарий 3: Codebase Health Check**
```
Manager: "Насколько у нас чистая кодовая база?"
Action: detect_code_clones(minSimilarity: 0.8, membersOnly: false)
Result: Overall статистика дублирования, метрики качества кода
```

---

## 🔧 Текущее состояние реализаций

### ✅ Overlord find_duplicates
**Статус:** COMPLETE (100% implemented)
- Полная реализация через MultiProjectVectorStore
- Cross-project search
- Support для TargetCode и TargetVector
- Гибкая настройка Threshold/Scope/Limit

### ⚠️ Overlord detect_code_clones
**Статус:** STUB (0% implemented)
- Возвращает helpful message
- Предлагает использовать find_duplicates как альтернативу
- Комментарий: "Используем тот же механизм что и find_duplicates, но по всем файлам"
- **ПРОБЛЕМА:** Это НЕ тот же механизм - нужен batch analysis, не query search

### ✅ Tools detect_code_clones
**Статус:** COMPLETE (100% implemented)
- Полная реализация через SemanticSearchService
- Grouping algorithm с priority scoring
- Refactoring recommendations
- Clone type classification

---

## 📋 Рекомендация: НЕ унифицировать

### Причина 1: Разные паттерны операций
```
find_duplicates:    User provides target → System finds matches
detect_code_clones: User provides params → System finds ALL clones
```
**Унификация невозможна** - нужны разные входные данные и алгоритмы.

### Причина 2: Разные алгоритмические сложности
```
find_duplicates:    O(log N) - single query
detect_code_clones: O(N * M) - full scan
```
**Performance профиль** кардинально отличается.

### Причина 3: Разные результаты
```
find_duplicates:    Flat list of matches
detect_code_clones: Grouped clones with priorities
```
**Output format** несовместим.

### Причина 4: Разные use cases
```
find_duplicates:    "Найди похожие на ЭТО"
detect_code_clones: "Найди ВСЕ клоны"
```
**Пользовательские сценарии** не пересекаются.

---

## 🎯 Рекомендуемая реализация

### Решение: Overlord detect_code_clones → MCP Proxy → Tools detect_code_clones

#### Архитектура:
```
Claude → Droid (Hybrid)
         ↓
    [McpToolInterceptor]
         ↓
    [ToolRouter] → detect_code_clones = LOCAL tool
         ↓
    Execute LOCAL
         ↓
    Tools.SemanticAnalysisTools.DetectCodeClones
         ↓
    Return clone groups to Claude
```

#### Реализация в Overlord:
```csharp
private async Task<string> ExecuteDetectCodeClones(
    string argumentsJson,
    string? projectContext,
    CancellationToken cancellationToken)
{
    // detect_code_clones - batch analysis tool
    // Overlord не должен сам сканировать все проекты (resource-intensive)
    // Вместо этого - proxy to LOCAL Tools implementation

    var args = JsonSerializer.Deserialize<DetectCodeClonesArgs>(argumentsJson);

    // Call LOCAL Tools version через MCP Proxy
    var mcpProxyArgs = new Dictionary<string, object>
    {
        ["minSimilarity"] = args.MinSimilarity,
        ["mode"] = args.Mode,
        ["membersOnly"] = args.MembersOnly,
        ["maxGroups"] = args.MaxGroups
    };

    // Option 1: Proxy to Droid (if in Hybrid mode)
    if (_serverBridgeService != null)
    {
        return await _serverBridgeService.CallMcpProxyAsync(
            "detect_code_clones",
            mcpProxyArgs,
            projectContext,
            cancellationToken);
    }

    // Option 2: Return helpful message if no bridge
    return JsonSerializer.Serialize(new
    {
        error = "detect_code_clones requires local Droid with loaded solution",
        hint = "This tool performs batch analysis on entire codebase",
        recommendation = "Use find_duplicates for targeted duplicate search across projects"
    });
}

private sealed class DetectCodeClonesArgs
{
    public float MinSimilarity { get; set; } = 0.85f;
    public string Mode { get; set; } = "semantic";
    public bool MembersOnly { get; set; } = true;
    public int MaxGroups { get; set; } = 20;
}
```

#### Обоснование:
1. **Resource efficiency:** Overlord не должен сканировать все проекты (CPU/memory intensive)
2. **Separation of concerns:**
   - Overlord = cross-project semantic search (find_duplicates)
   - Tools = local codebase analysis (detect_code_clones)
3. **Reuse existing implementation:** Tools detect_code_clones уже полностью реализован
4. **Graceful degradation:** Если нет bridge - helpful error message

---

## 🔀 Альтернативное решение (NOT RECOMMENDED)

### Overlord detect_code_clones через MultiProjectVectorStore

#### Идея:
```csharp
private async Task<string> ExecuteDetectCodeClones(...)
{
    // Get ALL vectors from ALL projects
    var allProjects = await _vectorStore.GetProjectsAsync(cancellationToken);
    var cloneGroups = new List<CloneGroup>();

    foreach (var project in allProjects)
    {
        // Get all vectors for this project
        var vectors = await _vectorStore.GetAllVectorsAsync(project, cancellationToken);

        // Compare each vector with all others
        foreach (var vector in vectors)
        {
            var similar = await _vectorStore.SearchAsync(vector.Embedding, threshold, limit);
            // Group similar vectors...
        }
    }

    return JsonSerializer.Serialize(cloneGroups);
}
```

#### Почему НЕ рекомендуется:

**1. Performance issues:**
- Requires scanning ALL vectors in ALL projects
- O(N²) complexity for large codebases
- Network overhead for each vector retrieval
- Memory-intensive (all vectors in RAM)

**2. Incomplete implementation:**
- VectorStore не хранит full code snippets
- Нет Roslyn semantic model для classification
- Нет refactoring recommendation logic
- Нужно дублировать всю логику из Tools version

**3. Resource waste:**
- Overlord server становится bottleneck
- Можно одновременно только один detect_code_clones
- Блокирует ресурсы для других users

**4. Maintenance burden:**
- Нужно синхронизировать 2 реализации (Overlord + Tools)
- Duplicate code для clone detection (ironic!)

---

## 🏁 Финальная рекомендация

### ✅ Действия:

1. **НЕ унифицировать** find_duplicates и detect_code_clones
   - Разные инструменты для разных сценариев
   - Оба инструмента нужны и дополняют друг друга

2. **Реализовать Overlord detect_code_clones как MCP Proxy**
   - Перенаправлять на Tools detect_code_clones
   - Graceful error message если нет Droid connection

3. **Обновить ToolRoutingConfig**
   - detect_code_clones = LOCAL (требует loaded solution)
   - find_duplicates = OVERLORD (cross-project vector search)

4. **Обновить документацию**
   - Clarify use cases для обоих инструментов
   - Примеры когда использовать каждый из них

### 📊 Итоговая архитектура:

```
┌─────────────────────────────────────────────────┐
│              Claude / User                      │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│         Droid (Hybrid Mode)                     │
│  ┌──────────────────────────────────────────┐   │
│  │      McpToolInterceptor                  │   │
│  │             ↓                            │   │
│  │      ToolRouter                          │   │
│  └──────────────┬───────────────────────────┘   │
│                 │                               │
│        ┌────────┴────────┐                      │
│        ▼                 ▼                      │
│   [find_duplicates]  [detect_code_clones]      │
│        ↓                 ↓                      │
│   OVERLORD             LOCAL                    │
└────────┬────────────────┬────────────────────────┘
         │                │
         ▼                ▼
┌─────────────────┐  ┌──────────────────────────┐
│   Overlord      │  │  SemanticAnalysisTools   │
│                 │  │  .DetectCodeClones       │
│ find_duplicates │  │                          │
│      ↓          │  │  - GetAllCodeEntities    │
│ MultiProject    │  │  - FindCloneGroups       │
│ VectorStore     │  │  - Group by similarity   │
│      ↓          │  │  - Refactoring recom     │
│ SearchAcross    │  │  - Priority scoring      │
│ ProjectsAsync   │  │                          │
└─────────────────┘  └──────────────────────────┘

Use Cases:
find_duplicates:        detect_code_clones:
- Code review           - Technical debt analysis
- Find existing impl    - Refactoring planning
- Cross-project search  - Codebase health check
- Learning examples     - Duplicate elimination
```

---

## 📝 Summary

**Question:** Должны ли мы унифицировать detect_code_clones и find_duplicates?

**Answer:** **НЕТ**

**Reasons:**
1. ❌ Разные операции: QUERY-based vs BATCH analysis
2. ❌ Разные входные данные: TargetCode vs Parameters
3. ❌ Разные алгоритмы: Single search vs Full scan
4. ❌ Разные результаты: Flat list vs Grouped clones
5. ❌ Разные use cases: "Find similar to THIS" vs "Find ALL clones"

**Solution:**
- ✅ Оставить оба инструмента как есть
- ✅ Реализовать Overlord detect_code_clones через MCP Proxy → Tools version
- ✅ Обновить ToolRoutingConfig: detect_code_clones = LOCAL
- ✅ Документировать use cases для каждого инструмента

**Status:** Готов к реализации в Phase 12.3

---

**Дата:** 2025-11-18
**Автор:** Claude Code Analysis
**Версия документа:** 1.0

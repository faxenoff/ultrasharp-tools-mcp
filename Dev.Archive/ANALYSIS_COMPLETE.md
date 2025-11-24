# Итоговый анализ: ultrascript-tools-mcp vs ultrasharp-tools-mcp

**Дата:** 2025-01-18
**Версия анализа:** Глубокий + детальный

---

## 📊 Сводная таблица: что есть, чего нет

| Категория | Функция | ultrascript (TS) | ultrasharp (C#) | Приоритет |
|-----------|---------|------------------|-----------------|-----------|
| **Semantic Search MCP Tools** | | | | |
| | `semantic_search` tool | ✅ | ❌ (только сервис) | 🔥 Критично |
| | `semantic_diff` tool | ✅ | ❌ (только сервис) | 🔥 Критично |
| | `detect_code_clones` | ✅ (2 варианта) | ❌ | 🔥 Критично |
| | Semantic infrastructure | ✅ | ✅ **ГОТОВО** | ✅ |
| **Code Modification Safety** | | | | |
| | Preview before modify | ✅ PreviewManager | ❌ | 🟡 Высокий |
| | Impact estimation | ✅ | ❌ | 🟡 Высокий |
| | Diff generation | ✅ WASM diff-simd | ❌ | 🟡 Средний |
| **Version Control** | | | | |
| | Git auto-commits | ✅ | ✅ **ГОТОВО** | ✅ |
| | Undo last change | ✅ | ✅ **ГОТОВО** | ✅ |
| | Snapshot system | ✅ (create/rollback/list/cleanup) | ❌ | 🟡 Высокий |
| | Multiple restore points | ✅ | ❌ (только undo) | 🟡 Высокий |
| | Git + Backup fallback | ✅ | ❌ | 🟢 Средний |
| **Code Validation** | | | | |
| | Roslyn analyzers | ✅ ESLint/Pylint | ✅ **ГОТОВО** | ✅ |
| | Before/after comparison | ✅ | ❌ | 🟡 Высокий |
| | Batch validation | ✅ (concurrency) | ❌ | 🟢 Средний |
| | Auto-validation in modify | ✅ | ❌ | 🟢 Средний |
| **File Operations** | | | | |
| | Split file | ✅ | ❌ | 🟡 Средний |
| | Synthesize files | ✅ | ❌ | 🟡 Средний |
| | Copy file | ✅ (streaming) | ❌ | 🟢 Низкий |
| | Rename file + auto-imports | ✅ | ❌ | 🟡 Средний |
| | Move member | ✅ | ✅ **ГОТОВО** | ✅ |
| **Technology Detection** | | | | |
| | Detect frameworks | ✅ | ❌ | 🟢 Низкий |
| | Parse dependencies | ✅ | ❌ (есть add_package) | 🟢 Низкий |
| | Tech context for embeddings | ✅ | ❌ | 🟢 Низкий |
| **Advanced Search** | | | | |
| | Pattern search (4 modes) | ✅ | ❌ | 🟢 Средний |
| | Entity search | ✅ | ✅ (search_definitions) | ✅ |
| | Content search | ✅ | ❌ | 🟢 Низкий |
| | Semantic search | ✅ | ✅ (сервис) | ⚠️ Нет tool |
| | Hybrid search | ✅ | ❌ | 🟢 Низкий |
| **Quality & Performance** | | | | |
| | Hotspot analysis | ✅ (complexity + coupling + git) | ⚠️ Частично (analyze_complexity) | 🟡 Средний |
| | Code complexity | ✅ | ✅ **ГОТОВО** | ✅ |
| | Git change frequency | ✅ | ❌ | 🟢 Низкий |
| | Refactoring suggestions | ✅ AI-powered | ❌ | 🟢 Низкий |
| **Semantic Merge** | | | | |
| | 3-way merge | ✅ | ✅ **ГОТОВО** | ✅ |
| | Code movement detection | ✅ | ✅ **ГОТОВО** | ✅ |
| | Fast path matching | ✅ | ✅ **ГОТОВО** | ✅ |
| **Tracing & Debugging** | | | | |
| | Trace execution (CFG) | ❌ | ✅ **ГОТОВО** | ✅ |
| | Trace backwards | ❌ | ✅ **ГОТОВО** | ✅ |
| | Symbolic execution (Z3) | ❌ | ✅ **ГОТОВО** | ✅ |
| | Path feasibility | ❌ | ✅ **ГОТОВО** | ✅ |
| | Log analysis | ✅ | ✅ **ГОТОВО** | ✅ |
| **Code Graph** | | | | |
| | SQLite code graph | ✅ | ❌ (Roslyn лучше) | ❌ Не нужно |
| | Multi-agent indexing | ✅ | ❌ (Roslyn синхронный) | ❌ Не нужно |
| | Branch-aware indexing | ✅ | ❌ (Roslyn auto-updates) | ❌ Не нужно |
| | Knowledge Bus | ✅ | ❌ | ❌ Не нужно |
| **Performance Optimizations** | | | | |
| | SIMD vector ops | ✅ Loop unrolling 4x | ⚠️ (C# уже быстрый) | 🟢 Низкий |
| | xxHash | ✅ | ❌ | 🟢 Низкий |
| | LRU cache v11 | ✅ | ⚠️ Roslyn cache | ✅ |
| | Adaptive vector backend | ✅ | ❌ | 🟢 Низкий |
| | Resource Manager | ✅ Adaptive | ❌ (не нужно) | ❌ Не нужно |

---

## ✅ Что УЖЕ ОТЛИЧНО реализовано в ultrasharp

### 1. Advanced Tracing (лучше чем в ultrascript!)

**ultrasharp ПРЕВОСХОДИТ ultrascript:**

```csharp
✅ SymbolicExecutionService + Z3 SMT Solver
✅ TraceExecution через Roslyn CFG (Control Flow Graph)
✅ TraceBackwards - обратная трассировка
✅ analyze_path_feasibility - проверка достижимости
```

**ultrascript НЕ ИМЕЕТ:**
- ❌ Symbolic execution
- ❌ Z3 constraint solving
- ❌ Path feasibility analysis

**Вывод:** Трассировка в ultrasharp уникальна и мощнее!

---

### 2. Semantic Merge (лучше чем в ultrascript!)

**ultrasharp имеет:**
```csharp
✅ SemanticMergeService (hybrid fast/slow path)
✅ Structural normalization (AST hash)
✅ Content normalization (whitespace-agnostic)
✅ Signature matching (FQN + parameters)
✅ Vector embeddings для semantic similarity
```

**Преимущество над ultrascript:**
- Roslyn AST анализ глубже чем tree-sitter
- Semantic model даёт точную type information
- Integrated с Compilation

**Вывод:** Semantic Merge уже на высоком уровне!

---

### 3. Quality Tools

**ultrasharp имеет:**
```csharp
✅ CSharpier integration (format_code)
✅ Roslyn Analyzers (analyze_code_style)
✅ Auto-fixes (apply_code_fixes)
✅ EditorConfig support
```

**ultrascript имеет:**
- ESLint/Pylint integration (аналогично)

**Вывод:** Паритет достигнут

---

### 4. Git Workflow (продвинутый)

**ultrasharp имеет:**
```csharp
✅ Auto-branch: sharptools/YYYYMMDD-HHMMSS
✅ Auto-commits with descriptive messages
✅ undo (git revert)
✅ Branch retention policies (--git-branch-retention-count)
✅ Auto-cleanup старых веток
✅ Disable git flag (--disable-git)
```

**ultrascript имеет:**
- Git stash для snapshots
- Manual branch management

**Вывод:** ultrasharp Git workflow продуманнее!

---

### 5. Token Efficiency

**ultrasharp имеет:**
```csharp
✅ Code без indentation (~10% token savings)
✅ FQN-first navigation (не нужно читать файлы)
✅ Adaptive detail levels в load_project
✅ Paginated results для больших запросов
✅ Fuzzy matching FQN (typo tolerance)
```

**ultrascript имеет:**
- Похожие оптимизации

**Вывод:** Паритет

---

## 🔥 Что КРИТИЧНО добавить

### ПРИОРИТЕТ 1: Semantic MCP Tools (2 недели)

**Проблема:** Инфраструктура готова, но нет MCP tools!

**Что есть:**
- ✅ `SemanticSearchService.cs` - работает
- ✅ `CodeSemanticIndexer.cs` - индексирует
- ✅ `VectorBasedSemanticSimilarityService.cs` - вычисляет similarity
- ✅ TEI/Ollama/Memory провайдеры
- ✅ setup-semantic-embedding.ps1

**Чего НЕТ:**
- ❌ `semantic_search` MCP tool
- ❌ `semantic_diff` MCP tool
- ❌ `detect_code_clones` MCP tool

**Решение:**
Создать `SemanticAnalysisTools.cs` с 3 tools:

```csharp
// Файл: UltrasharpTools.Tools/Mcp/Tools/SemanticAnalysisTools.cs

[McpServerTool(Name = "semantic_search")]
public static async Task<object> SemanticSearch(
    SemanticSearchService searchService, ...)
{
    await searchService.IndexCurrentSolutionAsync();
    var results = await searchService.FindSimilarCodeAsync(query, topK, minSimilarity);
    return FormatResults(results);
}

[McpServerTool(Name = "semantic_diff")]
public static async Task<object> SemanticDiff(
    VectorBasedSemanticSimilarityService similarityService, ...)
{
    var similarity = await ComputeSemanticSimilarity(beforeCode, afterCode);
    return new { similarity, behaviorPreserved = similarity > 0.95 };
}

[McpServerTool(Name = "detect_code_clones")]
public static async Task<object> DetectCodeClones(
    SemanticSearchService searchService, ...)
{
    var entities = await GetAllEntitiesAsync();
    var cloneGroups = await GroupSimilarEntitiesAsync(entities, minSimilarity);
    return cloneGroups;
}
```

**Трудозатраты:** 9-13 дней
**Риск:** 🟢 Низкий (инфраструктура готова)

---

### ПРИОРИТЕТ 2: Preview System (1 неделя)

**Проблема:** Все modification tools сразу применяют изменения

**Решение из ultrascript:**
```typescript
// Preview mode (default)
modify_entity_code({ entityId, newCode, preview: true })
→ Результат: diff, impact estimation, БЕЗ изменений

// Apply mode
modify_entity_code({ entityId, newCode, preview: false })
→ Результат: изменения применены
```

**Реализация для C#:**

```csharp
// PreviewManager.cs
public class PreviewManager
{
    public async Task<DiffPreview> PreviewCodeModification(string fqn, string newCode)
    {
        // 1. Get current code
        var oldCode = await GetSymbolCodeAsync(fqn);

        // 2. Generate diff
        var diff = GenerateUnifiedDiff(oldCode, newCode);

        // 3. Estimate impact
        var impact = await EstimateImpactAsync(fqn, newCode);

        return new DiffPreview { Diff = diff, Impact = impact };
    }
}

// Integration in modify_code
[McpServerTool(Name = "modify_code")]
public static async Task<object> OverwriteMember(
    ...
    [Description("Preview mode")] bool preview = true)
{
    if (preview)
        return await previewManager.PreviewCodeModification(fqn, newCode);

    // Apply changes
    await ApplyChangesAsync(...);
}
```

**Что даёт:**
- Безопасный preview перед изменениями
- Понимание impact (сколько файлов/references затронуто)
- Unified diff для code review

**Трудозатраты:** 7 дней
**Риск:** 🟡 Средний (нужна diff generation)

---

### ПРИОРИТЕТ 3: Snapshot System (1 неделя)

**Проблема:** `undo` работает только с последним commit

**Решение из ultrascript:**
```typescript
create_snapshot("before refactoring", files)
→ snapshotId = "snapshot-20250118123045"

// Работа с кодом...

rollback_snapshot(snapshotId)
→ Восстановление к точной версии
```

**Реализация для C#:**

```csharp
// VersionManager.cs
public class VersionManager
{
    // Backend selection: Git stash или .backup/ directory
    public async Task<string> CreateSnapshot(string description, string[] files)
    {
        if (_hasGit)
            return await CreateGitSnapshot(description, files); // git stash
        else
            return await CreateBackupSnapshot(description, files); // .backup/
    }

    public async Task RollbackAsync(string snapshotId) { ... }
    public async Task<List<Snapshot>> ListSnapshotsAsync(int limit) { ... }
}

// MCP Tools
[McpServerTool(Name = "create_snapshot")]
public static async Task<object> CreateSnapshot(...) { ... }

[McpServerTool(Name = "rollback_snapshot")]
public static async Task<object> RollbackSnapshot(...) { ... }

[McpServerTool(Name = "list_snapshots")]
public static async Task<object> ListSnapshots(...) { ... }
```

**Интеграция с modify_code:**
```csharp
// Auto-snapshot before modification
var snapshotId = await versionManager.CreateSnapshot($"before-{fqn}", [filePath]);

try
{
    await ModifyCodeAsync(...);
}
catch
{
    await versionManager.RollbackAsync(snapshotId); // Auto-rollback on error
    throw;
}
```

**Что даёт:**
- Множественные restore points (не только undo)
- Работает БЕЗ git (fallback на .backup/)
- Метаданные (description, timestamp, size)

**Трудозатраты:** 7 дней
**Риск:** 🟢 Низкий

---

## 🟡 Что ЖЕЛАТЕЛЬНО добавить

### 1. Validation Tools (1 неделя)

**Что есть:**
- ✅ `analyze_code_style` - Roslyn analyzers

**Чего нет:**
- ❌ Before/after comparison
- ❌ Batch validation
- ❌ Auto-validation в modify_code

**Решение:**

```csharp
[McpServerTool(Name = "validate_file")]
public static async Task<object> ValidateFile(string filePath)
{
    var diagnostics = await GetDiagnosticsAsync(filePath);
    return FormatDiagnostics(diagnostics);
}

[McpServerTool(Name = "validate_directory")]
public static async Task<object> ValidateDirectory(string dirPath, int batchSize = 10)
{
    // Batch processing with concurrency
    var reports = await BatchValidateAsync(files, batchSize);
    return AggregateReports(reports);
}

// Integration in modify_code
var beforeValidation = await ValidateFileAsync(filePath);
await ModifyCodeAsync(...);
var afterValidation = await ValidateFileAsync(filePath);
return new { ...validationReport = CompareValidation(before, after) };
```

**Трудозатраты:** 7 дней
**Риск:** 🟢 Низкий (Roslyn Diagnostics API уже есть)

---

### 2. File Operations (1-2 недели)

**Что есть:**
- ✅ `move_member` - перемещение члена типа

**Чего нет:**
- ❌ `split_file` - разделение большого файла
- ❌ `synthesize_files` - объединение файлов
- ❌ Auto-import updates при rename/move

**Решение:**

```csharp
[McpServerTool(Name = "split_file")]
public static async Task<object> SplitFile(
    string filePath,
    string targetDir,
    bool preview = true)
{
    // 1. Parse file (Roslyn)
    var classes = await ExtractClassesAsync(filePath);

    if (preview)
        return PreviewSplit(classes, targetDir);

    // 2. Create separate files
    foreach (var cls in classes)
        await CreateFileForClassAsync(cls, targetDir);

    // 3. Update project
    await AddFilesToProjectAsync(targetDir, classes);

    return new { filesCreated = classes.Count };
}

[McpServerTool(Name = "synthesize_files")]
public static async Task<object> SynthesizeFiles(
    string[] filePaths,
    string targetFile,
    bool deleteOriginals = false)
{
    // 1. Parse all files
    var roots = await ParseFilesAsync(filePaths);

    // 2. Merge usings + classes
    var combined = MergeSyntaxRoots(roots);

    // 3. Write combined file
    await WriteFileAsync(targetFile, combined);

    if (deleteOriginals)
        DeleteFiles(filePaths);

    return new { success = true, filesRemoved = deleteOriginals ? filePaths.Length : 0 };
}
```

**Трудозатраты:** 12 дней
**Риск:** 🟡 Средний (auto-import updates сложный)

---

### 3. Pattern Search (1 неделя)

**Решение:**

```csharp
[McpServerTool(Name = "pattern_search")]
public static async Task<object> PatternSearch(
    string pattern,
    string mode = "hybrid") // entity|content|semantic|hybrid
{
    switch (mode)
    {
        case "entity": return await SearchEntitiesByNameAsync(pattern);
        case "content": return await SearchContentInMethodsAsync(pattern);
        case "semantic": return await SemanticSearchAsync(pattern);
        case "hybrid": return await HybridSearchAsync(pattern);
    }
}
```

**Трудозатраты:** 7 дней
**Риск:** 🟢 Низкий (комбинация существующих tools)

---

### 4. Hotspot Analysis (5 дней)

**Что есть:**
- ✅ `analyze_complexity` - complexity metrics

**Чего нет:**
- ❌ Git change frequency
- ❌ Coupling metrics
- ❌ Комбинированная hotspot score

**Решение:**

```csharp
[McpServerTool(Name = "analyze_hotspots")]
public static async Task<object> AnalyzeHotspots(
    string gitRepoPath,
    int topN = 10)
{
    // 1. Complexity (already have)
    var complexity = await GetComplexityMetricsAsync();

    // 2. Git change frequency
    var changeFreq = await GetGitChangeFrequencyAsync(gitRepoPath);

    // 3. Coupling (via FindReferences)
    var coupling = await GetCouplingMetricsAsync();

    // 4. Combined hotspot score
    var hotspots = CombineMetrics(complexity, changeFreq, coupling)
        .OrderByDescending(h => h.Score)
        .Take(topN);

    return hotspots;
}
```

**Трудозатраты:** 5 дней
**Риск:** 🟢 Низкий (git log parsing простой)

---

## 🟢 Что опционально (Nice to have)

### 1. Technology Detection (3-5 дней)

```csharp
[McpServerTool(Name = "detect_technology_stack")]
public static async Task<object> DetectTechnologyStack()
{
    // 1. Languages (from projects)
    var languages = DetectLanguagesFromProjects();

    // 2. Frameworks (from TargetFramework)
    var frameworks = ParseTargetFrameworks();

    // 3. Dependencies (from PackageReferences)
    var dependencies = GetNuGetPackages();

    return new { languages, frameworks, dependencies };
}
```

**Трудозатраты:** 3-5 дней
**Риск:** 🟢 Низкий (парсинг .csproj)

---

## ❌ Что НЕ стоит брать

### 1. Multi-agent архитектура
**Причина:** Roslyn уже предоставляет всю информацию синхронно
- Не нужны Parser/VectorDB/Query агенты
- Roslyn Semantic Model быстрее чем Code Graph в SQLite

### 2. Code Graph в SQLite
**Причина:** Roslyn's Compilation/SemanticModel лучше
- Roslyn уже держит граф в памяти
- Синхронизация с SQLite = overhead

### 3. Branch-aware indexing
**Причина:** Roslyn workspace автоматически обновляется
- Git checkout → IDE перечитывает → Roslyn автообновляется

### 4. Knowledge Bus
**Причина:** Нет async агентов, не нужна координация

### 5. Worker Pools
**Причина:** Roslyn уже параллелит компиляцию

### 6. SIMD vector ops
**Причина:** C# уже достаточно быстрый для cosine similarity

---

## 📝 Итоговые приоритеты

### Sprint 1: Semantic MCP Tools (2 недели) 🔥 КРИТИЧНО
1. ✅ `semantic_search` tool
2. ✅ `semantic_diff` tool
3. ✅ `detect_code_clones` tool
4. ✅ Тесты + документация

**Результат:** Полноценный semantic поиск на базе TEI/Ollama

---

### Sprint 2: Preview System (1 неделя) 🟡 ВЫСОКИЙ
1. ✅ `PreviewManager` класс
2. ✅ Интеграция preview mode во все modification tools
3. ✅ Impact estimation
4. ✅ Тесты

**Результат:** Preview перед всеми изменениями

---

### Sprint 3: Snapshot System (1 неделя) 🟡 ВЫСОКИЙ
1. ✅ `VersionManager` класс
2. ✅ 4 MCP tools (create/rollback/list/cleanup)
3. ✅ Интеграция в modify_code
4. ✅ Тесты

**Результат:** Множественные restore points

---

### Sprint 4: Validation Tools (1 неделя) 🟡 ЖЕЛАТЕЛЬНО
1. ✅ `validate_file` tool
2. ✅ `validate_directory` batch tool
3. ✅ Before/after comparison
4. ✅ Интеграция в modify_code

**Результат:** Автоматическая валидация изменений

---

### Sprint 5+: Опциональные фичи (2-3 недели) 🟢 NICE TO HAVE
1. File operations (split/synthesize)
2. Pattern search (4 режима)
3. Hotspot analysis
4. Technology detection

**Результат:** Полный feature parity (где применимо)

---

## 🎯 Рекомендация

**Начать с Sprint 1: Semantic MCP Tools**

**Обоснование:**
1. ✅ Инфраструктура УЖЕ ГОТОВА (100%)
2. ✅ Только нужно обернуть в MCP tools
3. ✅ Максимальная ценность для пользователей
4. ✅ Низкий риск (9-13 дней работы)

**Следующие шаги:**
1. Создать `SemanticAnalysisTools.cs`
2. Реализовать 3 tools
3. Написать тесты
4. Обновить `ULTRA_SHARP_SEMANTIC.md`

---

**Готово к реализации! 🚀**

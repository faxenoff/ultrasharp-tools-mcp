# План внедрения функционала из ultrascript-tools-mcp

**Дата анализа:** 2025-01-18
**Проект-источник:** D:\_mcp\ultrascript-tools-mcp
**Целевой проект:** D:\github\ultrasharp-tools-mcp

---

## 📋 Оглавление

1. [Итоги глубокого анализа](#итоги-глубокого-анализа)
2. [Что уже есть в UltraSharp](#что-уже-есть-в-ultrasharp)
3. [Приоритетные функции для добавления](#приоритетные-функции-для-добавления)
4. [Детальный план реализации](#детальный-план-реализации)
5. [Архитектурные паттерны](#архитектурные-паттерны)
6. [Оценка трудозатрат](#оценка-трудозатрат)
7. [Риски и митигация](#риски-и-митигация)

---

## 🎯 Итоги глубокого анализа

### Полный список MCP tools в ultrascript-tools-mcp

**Всего: 38 инструментов**

#### Основные (Core):
1. `index` - индексация кодовой базы
2. `list_file_entities` - список сущностей в файле
3. `list_entity_relationships` - связи сущности
4. `query` - запросы к графу
5. `get_metrics` - системные метрики
6. `get_version` - информация о версии

#### Семантический поиск (Semantic):
7. `semantic_search` - поиск по смыслу
8. `find_similar_code` - поиск похожего кода
9. `analyze_code_impact` - анализ влияния изменений
10. `detect_code_clones` - обнаружение дубликатов (ML)
11. `jscpd_detect_clones` - обнаружение дубликатов (токенизация)
12. `suggest_refactoring` - рекомендации по рефакторингу
13. `cross_language_search` - кросс-языковый поиск
14. `analyze_hotspots` - поиск проблемных зон
15. `find_related_concepts` - поиск связанных концептов
16. `analyze_state_chaos` - анализ хаоса в state management

#### Code Graph:
17. `get_graph` - получение всего графа
18. `get_graph_stats` - статистика графа
19. `reset_graph` - очистка графа
20. `clean_index` - полная переиндексация
21. `get_graph_health` - диагностика БД
22. `get_agent_metrics` - метрики агентов
23. `get_bus_stats` - статистика knowledge bus
24. `clear_bus_topic` - очистка топика

#### **🆕 PHASE 8: Code Modification & Analysis** (упущено ранее):
25. `create_snapshot` - создание снапшота
26. `rollback_snapshot` - откат к снапшоту
27. `list_snapshots` - список снапшотов
28. `cleanup_snapshots` - удаление старых снапшотов
29. `modify_entity_code` - модификация кода сущности
30. `copy_file` - копирование файла
31. `rename_file` - переименование с auto-import updates
32. `split_file` - разделение файла на модули
33. `synthesize_files` - объединение файлов
34. `validate_file` - валидация файла (ESLint/Pylint)
35. `validate_directory` - batch-валидация
36. `detect_technology_stack` - определение стека
37. `pattern_search` - продвинутый поиск (4 режима)

#### Git ветки:
38-42. `list_branches`, `switch_branch`, `get_branch_status`, `cleanup_branches`, `get_changed_files`

#### Workspace:
43. `lerna_project_graph` - граф Lerna dependencies

---

## ✅ Что уже есть в UltraSharp

### MCP Tools (31 инструмент):

#### Solution & Project:
- ✅ `load_solution` - загрузка solution
- ✅ `load_project` - загрузка проекта

#### Analysis (9 tools):
- ✅ `get_all_subtypes` - рекурсивный список типов
- ✅ `get_members` - члены типа
- ✅ `view_definition` - исходный код
- ✅ `list_implementations` - реализации интерфейса
- ✅ `find_references` - поиск ссылок
- ✅ `view_inheritance_chain` - цепочка наследования
- ✅ `view_call_graph` - граф вызовов
- ✅ `search_definitions` - поиск определений
- ✅ `manage_usings` - управление using

#### Modification (7 tools):
- ✅ `add_member` - добавление члена
- ✅ `modify_code` - изменение кода
- ✅ `rename_symbol` - переименование
- ✅ `replace_all_references` - замена всех ссылок
- ✅ `replace_references_by_pattern` - замена по паттерну
- ✅ `undo` - отмена изменений
- ✅ `find_and_replace` - поиск и замена
- ✅ `move_member` - перемещение члена

#### Quality (3 tools):
- ✅ `format_code` - форматирование
- ✅ `analyze_code_style` - анализ стиля
- ✅ `apply_code_fixes` - применение исправлений

#### Document (3 tools):
- ✅ `read_file` - чтение файла
- ✅ `create_file` - создание файла
- ✅ `overwrite_file` - перезапись файла
- ✅ `list_file_entities` - список типов в файле

#### Tracing (4 tools):
- ✅ `trace_execution` - трассировка выполнения
- ✅ `trace_backwards` - обратная трассировка
- ✅ `export_call_graph` - экспорт графа вызовов
- ✅ `analyze_path_feasibility` - анализ путей

#### Semantic (2 tools - ТОЛЬКО в документации):
- ⚠️ `SemanticSearch` - **НЕ РЕАЛИЗОВАН**
- ⚠️ `SemanticDiff` - **НЕ РЕАЛИЗОВАН**

#### Misc:
- ✅ `request_new_tool` - запрос нового инструмента
- ✅ `add_package` - добавление NuGet пакета
- ✅ `analyze_logs` - анализ логов

### Инфраструктура:

#### Semantic:
- ✅ `SemanticSearchService.cs` - сервис поиска
- ✅ `CodeSemanticIndexer.cs` - индексация
- ✅ `VectorBasedSemanticSimilarityService.cs` - similarity
- ✅ `SemanticEmbeddingConfig.cs` - конфиг (TEI/Ollama/Memory)
- ✅ `setup-semantic-embedding.ps1` - автоустановка

#### Git:
- ✅ Automatic git commits для всех модификаций
- ✅ Branch creation: `sharptools/YYYYMMDD-HHMMSS`
- ✅ Undo через git revert
- ⚠️ НЕТ snapshot system (только git undo)

---

## 🔥 Приоритетные функции для добавления

### ПРИОРИТЕТ 1: Недостающие Semantic MCP Tools (1-2 недели)

**Критично:** У вас УЖЕ есть вся инфраструктура, но нет MCP tools!

#### 1.1 `semantic_search` tool
**Сложность:** 🟢 Низкая (2-3 дня)
**Файл:** `UltrasharpTools.Tools/Mcp/Tools/SemanticAnalysisTools.cs`

```csharp
[McpServerTool(Name = "semantic_search")]
[Description("Find semantically similar code using natural language")]
public static async Task<object> SemanticSearch(
    SemanticSearchService searchService,
    ISolutionManager solutionManager,
    [Description("Natural language query")] string query,
    [Description("Search scope")] string scope = "solution",
    [Description("Top N results")] int topK = 10,
    [Description("Min similarity")] float minSimilarity = 0.7f,
    CancellationToken ct)
{
    // 1. Index if needed
    await searchService.IndexCurrentSolutionAsync(ct);

    // 2. Search
    var results = await searchService.FindSimilarCodeAsync(query, topK, minSimilarity, ct);

    // 3. Format
    return results.Select(r => new {
        fqn = r.FullyQualifiedName,
        similarity = r.Similarity,
        filePath = r.FilePath,
        lineNumber = r.LineNumber
    });
}
```

**Использует:**
- ✅ Существующий `SemanticSearchService`
- ✅ Существующий TEI/Ollama/Memory провайдер
- ✅ Roslyn для FQN resolution

**Что даёт:**
```
Запрос: "email validation logic"
Результат:
- UserService.ValidateEmail (92%)
- EmailValidator.IsValid (88%)
- Registration.CheckEmailFormat (81%)
```

---

#### 1.2 `semantic_diff` tool
**Сложность:** 🟢 Низкая (2-3 дня)

```csharp
[McpServerTool(Name = "semantic_diff")]
[Description("Compare two code entities semantically")]
public static async Task<object> SemanticDiff(
    VectorBasedSemanticSimilarityService similarityService,
    ISolutionManager solutionManager,
    [Description("FQN before")] string beforeFqn,
    [Description("FQN after")] string afterFqn,
    CancellationToken ct)
{
    // 1. Get code
    var beforeCode = await GetEntityCodeAsync(beforeFqn);
    var afterCode = await GetEntityCodeAsync(afterFqn);

    // 2. Compute embeddings
    var beforeEmb = await similarityService.GetEmbeddingAsync(beforeCode);
    var afterEmb = await similarityService.GetEmbeddingAsync(afterCode);

    // 3. Similarity
    var similarity = similarityService.CosineSimilarity(beforeEmb, afterEmb);

    return new {
        similarity = similarity * 100 + "%",
        behaviorPreserved = similarity > 0.95,
        changeCategory = CategorizeChange(similarity)
    };
}
```

**Что даёт:**
- Проверка semantic equivalence после рефакторинга
- Обнаружение breaking changes
- Code review insights

---

#### 1.3 `detect_code_clones` tool
**Сложность:** 🟡 Средняя (3-5 дней)

```csharp
[McpServerTool(Name = "detect_code_clones")]
[Description("Find duplicate or similar code using semantic analysis")]
public static async Task<object> DetectCodeClones(
    SemanticSearchService searchService,
    ISolutionManager solutionManager,
    [Description("Min similarity")] float minSimilarity = 0.85f,
    [Description("Members only")] bool membersOnly = true,
    CancellationToken ct)
{
    // 1. Index
    await searchService.IndexCurrentSolutionAsync(ct);

    // 2. Get all entities
    var entities = await GetAllCodeEntitiesAsync(solutionManager, membersOnly);

    // 3. Group by similarity
    var cloneGroups = await FindCloneGroupsAsync(entities, searchService, minSimilarity);

    return cloneGroups.Select(g => new {
        groupId = g.Id,
        avgSimilarity = g.AvgSimilarity * 100 + "%",
        members = g.Members.Select(m => m.Fqn),
        recommendation = g.RefactoringRecommendation
    });
}
```

**Алгоритм:**
1. Для каждого entity: найти похожие (topK=50, minSimilarity)
2. Группировка похожих в clone groups
3. Рекомендации по рефакторингу

**Что даёт:**
- Автоматическое обнаружение дубликатов
- Метрики технического долга
- Приоритизация рефакторинга

---

### ПРИОРИТЕТ 2: Snapshot System (1 неделя)

**Проблема:** Текущий `undo` работает только с последним git commit.

**Решение:** Snapshot system из ultrascript:
- Множественные точки восстановления
- Работает БЕЗ git (fallback на `.backup/`)
- Метаданные (описание, timestamp, size)

#### 2.1 `VersionManager` сервис
**Файл:** `UltrasharpTools.Tools/Versioning/VersionManager.cs`

```csharp
public class VersionManager
{
    private readonly string _workingDirectory;
    private readonly string _backupDir;
    private bool _hasGit;

    // Backend selection
    public async Task<string> CreateSnapshot(string description, string[] files)
    {
        if (_hasGit)
            return await CreateGitSnapshot(description, files);
        else
            return await CreateBackupSnapshot(description, files);
    }

    // Git backend: stash
    private async Task<string> CreateGitSnapshot(...)
    {
        // git stash push -m "description" -- files...
    }

    // Fallback: .backup/ directory
    private async Task<string> CreateBackupSnapshot(...)
    {
        var snapshotId = $"backup-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var backupPath = Path.Combine(_backupDir, snapshotId);

        // Copy files + metadata
        foreach (var file in files)
            await CopyFileAsync(file, backupPath);

        await SaveMetadataAsync(snapshotId, description, files);
        return snapshotId;
    }
}
```

#### 2.2 MCP Tools

```csharp
[McpServerTool(Name = "create_snapshot")]
public static async Task<object> CreateSnapshot(
    VersionManager versionManager,
    [Description("Snapshot description")] string description,
    [Description("Files to snapshot")] string[]? files = null)
{
    var snapshotId = await versionManager.CreateSnapshot(description, files);
    return new { snapshotId, description, backend = "git" };
}

[McpServerTool(Name = "rollback_snapshot")]
public static async Task<object> RollbackSnapshot(
    VersionManager versionManager,
    [Description("Snapshot ID")] string snapshotId)
{
    await versionManager.RollbackAsync(snapshotId);
    return new { success = true, snapshotId };
}

[McpServerTool(Name = "list_snapshots")]
public static async Task<object> ListSnapshots(
    VersionManager versionManager,
    [Description("Max results")] int limit = 10)
{
    var snapshots = await versionManager.ListSnapshotsAsync(limit);
    return snapshots;
}
```

**Интеграция с `modify_code`:**
```csharp
// Перед модификацией
var snapshotId = await versionManager.CreateSnapshot($"before-{fqn}", [filePath]);

try
{
    // Модификация
    await ModifyCodeAsync(...);
}
catch
{
    // Автоматический rollback
    await versionManager.RollbackAsync(snapshotId);
    throw;
}
```

**Что даёт:**
- Безопасное экспериментирование
- Множественные точки восстановления
- Работает без git

---

### ПРИОРИТЕТ 3: Preview System (1 неделя)

**Что делает ultrascript:**
```typescript
// Preview mode (default)
modify_entity_code({ entityId, newCode, preview: true })
// Результат: diff, impact estimation, НЕТ изменений

// Apply mode
modify_entity_code({ entityId, newCode, preview: false })
// Результат: изменения применены
```

**Реализация для C#:**

#### 3.1 `PreviewManager` сервис
**Файл:** `UltrasharpTools.Tools/Preview/PreviewManager.cs`

```csharp
public class PreviewManager
{
    public async Task<DiffPreview> PreviewCodeModification(string fqn, string newCode)
    {
        // 1. Get current code via Roslyn
        var symbol = await ResolveSymbolAsync(fqn);
        var oldCode = await GetSymbolCodeAsync(symbol);

        // 2. Generate diff (unified format)
        var diff = GenerateDiff(oldCode, newCode);

        // 3. Estimate impact
        var impact = await EstimateImpactAsync(symbol, newCode);

        return new DiffPreview
        {
            Operation = "code-modification",
            FilesAffected = [symbol.FilePath],
            Diff = diff,
            EstimatedImpact = impact
        };
    }
}

public class DiffPreview
{
    public string Operation { get; set; }
    public string[] FilesAffected { get; set; }
    public string Diff { get; set; } // Unified diff format
    public ImpactEstimation EstimatedImpact { get; set; }
}

public class ImpactEstimation
{
    public int EntitiesAffected { get; set; }
    public int ReferencesAffected { get; set; }
    public bool BreakingChange { get; set; }
}
```

#### 3.2 Интеграция в `modify_code`

```csharp
[McpServerTool(Name = "modify_code")]
public static async Task<object> OverwriteMember(
    ...
    [Description("Preview mode (default: true)")] bool preview = true)
{
    if (preview)
    {
        var previewResult = await previewManager.PreviewCodeModification(fqn, newCode);
        return previewResult; // Diff + impact, NO changes
    }

    // Apply changes
    var snapshot = await versionManager.CreateSnapshot(...);
    try
    {
        await ApplyCodeChangesAsync(...);
        return new { success = true, snapshotId = snapshot };
    }
    catch
    {
        await versionManager.RollbackAsync(snapshot);
        throw;
    }
}
```

**Что даёт:**
- Безопасный preview перед изменениями
- Понимание impact на другие части кода
- Unified diff для code review

---

### ПРИОРИТЕТ 4: Code Validation (1 неделя)

**Что есть:** `analyze_code_style` - Roslyn analyzers
**Чего нет:** Batch validation, before/after comparison

#### 4.1 Расширить `QualityTools.cs`

```csharp
[McpServerTool(Name = "validate_file")]
[Description("Validate code file with Roslyn analyzers")]
public static async Task<object> ValidateFile(
    ISolutionManager solutionManager,
    [Description("File path")] string filePath)
{
    var document = await GetDocumentAsync(filePath);
    var diagnostics = await GetDiagnosticsAsync(document);

    return new {
        filePath,
        problems = diagnostics.Select(d => new {
            severity = d.Severity.ToString(),
            message = d.GetMessage(),
            line = d.Location.GetLineSpan().StartLinePosition.Line,
            ruleId = d.Id
        }),
        summary = new {
            errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
            warnings = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)
        }
    };
}

[McpServerTool(Name = "validate_directory")]
[Description("Batch validate all C# files in directory")]
public static async Task<object> ValidateDirectory(
    ISolutionManager solutionManager,
    [Description("Directory path")] string dirPath,
    [Description("Concurrency limit")] int batchSize = 10)
{
    var files = Directory.GetFiles(dirPath, "*.cs", SearchOption.AllDirectories);
    var reports = new List<object>();

    // Batch processing
    for (int i = 0; i < files.Length; i += batchSize)
    {
        var batch = files.Skip(i).Take(batchSize);
        var batchReports = await Task.WhenAll(
            batch.Select(f => ValidateFileAsync(f)));
        reports.AddRange(batchReports);
    }

    return new {
        totalFiles = files.Length,
        reports,
        aggregated = AggregateStats(reports)
    };
}
```

**Интеграция в `modify_code`:**
```csharp
// Before modification
var beforeValidation = await ValidateFileAsync(filePath);

// Apply changes
await ModifyCodeAsync(...);

// After modification
var afterValidation = await ValidateFileAsync(filePath);

// Comparison
var improvement = new {
    errorsFixed = beforeValidation.errors - afterValidation.errors,
    warningsFixed = beforeValidation.warnings - afterValidation.warnings,
    netChange = (afterValidation.errors + afterValidation.warnings) -
                (beforeValidation.errors + beforeValidation.warnings)
};
```

**Что даёт:**
- Automatic validation before/after changes
- Batch validation для целых директорий
- Quality metrics

---

### ПРИОРИТЕТ 5: File Operations (1-2 недели)

**Что есть:** `move_member` (перемещение члена типа)
**Чего нет:** split/synthesize файлов, auto-import updates

#### 5.1 `split_file` - Разделение большого файла

```csharp
[McpServerTool(Name = "split_file")]
[Description("Split large file into separate files by class/namespace")]
public static async Task<object> SplitFile(
    ISolutionManager solutionManager,
    [Description("File to split")] string filePath,
    [Description("Target directory")] string targetDir,
    [Description("Preview mode")] bool preview = true)
{
    // 1. Parse file
    var document = await GetDocumentAsync(filePath);
    var root = await document.GetSyntaxRootAsync();

    // 2. Extract classes
    var classes = root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .ToList();

    if (preview)
    {
        return new {
            operation = "split",
            currentFile = filePath,
            newFiles = classes.Select(c => new {
                fileName = $"{c.Identifier.Text}.cs",
                className = c.Identifier.Text,
                lineCount = c.GetLocation().GetLineSpan().Span.Length
            })
        };
    }

    // 3. Create separate files
    foreach (var cls in classes)
    {
        var newFilePath = Path.Combine(targetDir, $"{cls.Identifier.Text}.cs");
        var newRoot = SyntaxFactory.CompilationUnit()
            .WithUsings(root.Usings)
            .AddMembers(cls);

        await File.WriteAllTextAsync(newFilePath, newRoot.ToFullString());
    }

    // 4. Update project file
    await AddFilesToProjectAsync(targetDir, classes);

    // 5. Delete original (optional)
    return new { success = true, filesCreated = classes.Count };
}
```

#### 5.2 `synthesize_files` - Объединение файлов

```csharp
[McpServerTool(Name = "synthesize_files")]
[Description("Combine multiple files into one")]
public static async Task<object> SynthesizeFiles(
    ISolutionManager solutionManager,
    [Description("Files to combine")] string[] filePaths,
    [Description("Target file")] string targetFile,
    [Description("Delete originals")] bool deleteOriginals = false)
{
    // 1. Parse all files
    var roots = await Task.WhenAll(
        filePaths.Select(async f => {
            var doc = await GetDocumentAsync(f);
            return await doc.GetSyntaxRootAsync();
        }));

    // 2. Merge usings
    var allUsings = roots.SelectMany(r => r.Usings).Distinct();

    // 3. Merge classes
    var allClasses = roots.SelectMany(r => r.DescendantNodes()
        .OfType<ClassDeclarationSyntax>());

    // 4. Create combined file
    var combined = SyntaxFactory.CompilationUnit()
        .WithUsings(SyntaxFactory.List(allUsings))
        .AddMembers(allClasses.ToArray());

    await File.WriteAllTextAsync(targetFile, combined.ToFullString());

    if (deleteOriginals)
        foreach (var file in filePaths)
            File.Delete(file);

    return new { success = true, targetFile, filesRemoved = deleteOriginals ? filePaths.Length : 0 };
}
```

**Что даёт:**
- Рефакторинг "God classes" (split large files)
- Consolidation малых файлов
- Token-efficient operations (не нужно читать весь файл)

---

### ПРИОРИТЕТ 6: Technology Detection (опционально, 3-5 дней)

```csharp
[McpServerTool(Name = "detect_technology_stack")]
[Description("Detect frameworks, languages, and dependencies")]
public static async Task<object> DetectTechnologyStack(
    ISolutionManager solutionManager)
{
    var solution = solutionManager.CurrentWorkspace.CurrentSolution;

    // 1. Languages (from projects)
    var languages = solution.Projects
        .Select(p => p.Language)
        .GroupBy(l => l)
        .Select(g => new { name = g.Key, count = g.Count() });

    // 2. Frameworks (from TargetFramework)
    var frameworks = solution.Projects
        .Select(p => GetTargetFramework(p))
        .Distinct();

    // 3. Dependencies (from PackageReferences)
    var dependencies = solution.Projects
        .SelectMany(p => GetPackageReferences(p))
        .GroupBy(d => d.Name)
        .Select(g => new { name = g.Key, versions = g.Select(d => d.Version).Distinct() });

    return new {
        languages,
        frameworks,
        dependencies,
        buildTools = DetectBuildTools(solution)
    };
}

private static string GetTargetFramework(Project project)
{
    // Parse .csproj <TargetFramework>
    var csproj = XDocument.Load(project.FilePath);
    return csproj.Descendants("TargetFramework").FirstOrDefault()?.Value ?? "unknown";
}
```

**Что даёт:**
- Быстрое понимание незнакомого проекта
- Контекст для AI (embedding metadata)
- Анализ зависимостей

---

### ПРИОРИТЕТ 7: Pattern Search (опционально, 1 неделя)

**4 режима поиска:**
1. **Entity** - по имени/типу (regex)
2. **Content** - внутри кода методов
3. **Semantic** - векторная похожесть
4. **Hybrid** - комбинация всех

```csharp
[McpServerTool(Name = "pattern_search")]
[Description("Advanced search: entity/content/semantic/hybrid")]
public static async Task<object> PatternSearch(
    ISolutionManager solutionManager,
    SemanticSearchService semanticService,
    [Description("Search pattern")] string pattern,
    [Description("Search mode")] string mode = "hybrid", // entity|content|semantic|hybrid
    [Description("Entity types filter")] string[]? entityTypes = null)
{
    switch (mode)
    {
        case "entity":
            return await SearchEntitiesByName(pattern, entityTypes);
        case "content":
            return await SearchContentInMethods(pattern);
        case "semantic":
            return await semanticService.FindSimilarCodeAsync(pattern);
        case "hybrid":
            return await CombineSearchResults(pattern, entityTypes);
    }
}
```

**Что даёт:**
- Гибкий поиск (структурный + семантический)
- Framework-aware фильтрация
- Ranking по relevance

---

## 📐 Архитектурные паттерны из ultrascript

### 1. Preview-First Pattern

**Принцип:** Все destructive операции имеют preview mode

```csharp
// Pattern
public async Task<object> DestructiveOperation(
    ...
    [Description("Preview mode")] bool preview = true)
{
    if (preview)
        return await PreviewChangesAsync(...);

    await ApplyChangesAsync(...);
}
```

**Применить к:**
- ✅ `modify_code`
- ✅ `add_member`
- ✅ `rename_symbol`
- ✅ `move_member`
- ✅ Новые file operations

---

### 2. Snapshot-Before-Modify Pattern

**Принцип:** Автоматический snapshot перед каждой модификацией

```csharp
public async Task<object> ModifyCode(...)
{
    // 1. Create snapshot
    var snapshotId = await versionManager.CreateSnapshot(...);

    try
    {
        // 2. Modify
        await ApplyChangesAsync(...);
        return new { success = true, snapshotId };
    }
    catch
    {
        // 3. Auto-rollback
        await versionManager.RollbackAsync(snapshotId);
        throw;
    }
}
```

---

### 3. Validation-Before-After Pattern

**Принцип:** Валидация до и после изменений

```csharp
public async Task<object> ModifyCode(...)
{
    // Before
    var beforeValidation = await ValidateAsync(filePath);

    // Modify
    await ApplyChangesAsync(...);

    // After
    var afterValidation = await ValidateAsync(filePath);

    return new {
        ...
        validationReport = CompareValidation(beforeValidation, afterValidation)
    };
}
```

---

### 4. Adaptive Resource Management

**НЕ критично для C#** (Roslyn синхронный), но можно добавить:

```csharp
public class RoslynResourceManager
{
    // Адаптивное управление кешем Roslyn
    public void AdjustForCodebaseSize(int fileCount, int sizeMB)
    {
        if (fileCount > 5000)
        {
            // Увеличить кеш
            SetWorkspaceCacheSize(2048);
        }
        else
        {
            SetWorkspaceCacheSize(1024);
        }
    }
}
```

---

## ⏱️ Оценка трудозатрат

### Фаза 1: Semantic MCP Tools (1-2 недели)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `semantic_search` | 🟢 | 2-3 | ✅ Инфраструктура готова |
| `semantic_diff` | 🟢 | 2-3 | ✅ Инфраструктура готова |
| `detect_code_clones` | 🟡 | 3-5 | Нужен алгоритм группировки |
| Тесты + документация | 🟢 | 2 | - |
| **Итого** | | **9-13 дней** | |

### Фаза 2: Snapshot System (1 неделя)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `VersionManager` класс | 🟡 | 3 | Git integration |
| MCP tools (4 шт) | 🟢 | 2 | VersionManager |
| Интеграция в modify_code | 🟢 | 1 | - |
| Тесты | 🟢 | 1 | - |
| **Итого** | | **7 дней** | |

### Фаза 3: Preview System (1 неделя)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `PreviewManager` класс | 🟡 | 3 | Diff generation |
| Impact estimation | 🟡 | 2 | Roslyn analysis |
| Интеграция в tools | 🟢 | 1 | - |
| Тесты | 🟢 | 1 | - |
| **Итого** | | **7 дней** | |

### Фаза 4: Validation (1 неделя)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `validate_file` tool | 🟢 | 2 | Существующий QualityTools |
| `validate_directory` batch | 🟢 | 2 | validate_file |
| Before/after comparison | 🟢 | 2 | - |
| Тесты | 🟢 | 1 | - |
| **Итого** | | **7 дней** | |

### Фаза 5: File Operations (1-2 недели)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `split_file` tool | 🟡 | 3 | Roslyn syntax tree |
| `synthesize_files` tool | 🟡 | 3 | Roslyn syntax tree |
| Auto-import updates | 🔴 | 4 | Сложный анализ |
| Тесты | 🟢 | 2 | - |
| **Итого** | | **12 дней** | |

### Фаза 6: Опциональные фичи (1-2 недели)
| Задача | Сложность | Дни | Зависимости |
|--------|-----------|-----|-------------|
| `detect_technology_stack` | 🟢 | 3 | Парсинг .csproj |
| `pattern_search` (4 режима) | 🟡 | 5 | Semantic + Roslyn |
| Тесты | 🟢 | 2 | - |
| **Итого** | | **10 дней** | |

---

## 📊 Общая оценка

### Минимальный MVP (Фаза 1-2):
- **Semantic tools** + **Snapshots**
- **Время:** 16-20 дней (3-4 недели)
- **Риск:** 🟢 Низкий (инфраструктура готова)

### Полная реализация (Фаза 1-5):
- Все core фичи
- **Время:** 42-51 день (8-10 недель)
- **Риск:** 🟡 Средний (auto-import updates сложный)

### С опциональными фичами (Фаза 1-6):
- Полный функционал
- **Время:** 52-61 день (10-12 недель)
- **Риск:** 🟡 Средний

---

## ⚠️ Риски и митигация

### Риск 1: Auto-import updates (Фаза 5)
**Проблема:** Сложный анализ зависимостей при split/synthesize

**Митигация:**
1. Начать с простого: копировать все usings
2. Позже: умный анализ через Roslyn SemanticModel
3. Fallback: manual review recommended

### Риск 2: Совместимость с текущим git flow
**Проблема:** Snapshot system может конфликтовать с существующими sharptools/* ветками

**Митигация:**
1. Использовать отдельный namespace: `snapshots/*` branches
2. Или полностью на `.backup/` directory (без git)
3. Конфигурируемый backend

### Риск 3: Performance semantic search
**Проблема:** Индексация больших solution может быть медленной

**Митигация:**
1. Incremental indexing (только изменённые файлы)
2. Cache embeddings на диск
3. Background indexing (не блокировать MCP tool)

---

## 🎯 Рекомендуемая последовательность

### Sprint 1 (2 недели): MVP Semantic
1. ✅ `semantic_search` tool
2. ✅ `semantic_diff` tool
3. ✅ `detect_code_clones` tool
4. ✅ Тесты + документация

**Результат:** Полноценный semantic поиск на базе TEI/Ollama

---

### Sprint 2 (1 неделя): Snapshots
1. ✅ `VersionManager` класс
2. ✅ 4 MCP tools (create/rollback/list/cleanup)
3. ✅ Интеграция в modify_code
4. ✅ Тесты

**Результат:** Безопасная работа с кодом (множественные restore points)

---

### Sprint 3 (1 неделя): Preview
1. ✅ `PreviewManager` класс
2. ✅ Интеграция preview mode во все modification tools
3. ✅ Impact estimation
4. ✅ Тесты

**Результат:** Preview перед всеми изменениями

---

### Sprint 4 (1 неделя): Validation
1. ✅ `validate_file` tool
2. ✅ `validate_directory` batch tool
3. ✅ Before/after comparison
4. ✅ Интеграция в modify_code

**Результат:** Автоматическая валидация изменений

---

### Sprint 5+ (опционально): File Operations & Tech Detection
1. `split_file`
2. `synthesize_files`
3. `detect_technology_stack`
4. `pattern_search`

**Результат:** Полный feature parity с ultrascript

---

## 📝 Что НЕ стоит брать из ultrascript

### ❌ Multi-agent архитектура
**Причина:** Roslyn уже предоставляет всю информацию синхронно
- Не нужны Parser/Indexer/Query агенты
- Roslyn Semantic Model быстрее чем Code Graph в SQLite
- Добавит сложности без выигрыша

### ❌ Code Graph в SQLite
**Причина:** Roslyn's Compilation/SemanticModel лучше
- Roslyn уже держит граф в памяти
- Синхронизация с SQLite = overhead
- Только для embeddings нужен storage (уже есть)

### ❌ Branch-aware indexing
**Причина:** Roslyn workspace автоматически обновляется
- Git checkout → IDE перечитывает → Roslyn автообновляется
- Не нужна изолированная БД на ветку

### ❌ JSCPD clone detection
**Причина:** Semantic search через embeddings лучше
- ML embeddings ловят больше паттернов
- Инфраструктура уже есть (TEI/Ollama)
- Token-based detection проще, но менее точный

### ❌ Knowledge Bus
**Причина:** Не нужна координация агентов
- Roslyn синхронный
- Нет async агентов
- EventEmitter достаточно (если нужен)

### ❌ Worker Pools
**Причина:** Roslyn уже параллелит компиляцию
- Compilation.WithSyntaxTrees параллельная
- SemanticModel thread-safe
- Дополнительные workers = overhead

---

## 🔄 Сравнительная таблица: что брать, что нет

| Функционал | Брать? | Причина |
|------------|--------|---------|
| **Semantic MCP tools** | ✅ ДА | Инфраструктура готова, только tools |
| **Snapshot system** | ✅ ДА | Дополняет git, fallback для non-git |
| **Preview system** | ✅ ДА | Safety before modifications |
| **Validation tools** | ✅ ДА | Before/after comparison полезно |
| **File operations** | ✅ ДА | Token-efficient, split/synthesize |
| **Technology detection** | 🟡 ОПЦИОНАЛЬНО | Nice to have, но не критично |
| **Pattern search** | 🟡 ОПЦИОНАЛЬНО | Semantic + Roslyn комбинация |
| **Multi-agent** | ❌ НЕТ | Roslyn лучше |
| **Code Graph SQLite** | ❌ НЕТ | Roslyn SemanticModel лучше |
| **Branch-aware indexing** | ❌ НЕТ | Roslyn auto-updates |
| **JSCPD** | ❌ НЕТ | Semantic лучше |
| **Knowledge Bus** | ❌ НЕТ | Нет async агентов |
| **Worker Pools** | ❌ НЕТ | Roslyn уже параллелит |
| **Adaptive Resource Manager** | ❌ НЕТ | Roslyn синхронный |
| **SIMD vector ops** | ❌ НЕТ | C# уже быстрый, SIMD не нужен |

---

## ✅ Финальные рекомендации

### Минимальный MVP (3-4 недели):
1. ✅ Semantic MCP tools (semantic_search, semantic_diff, detect_code_clones)
2. ✅ Snapshot system (create/rollback/list snapshots)

**Результат:**
- Полноценный semantic поиск
- Безопасная работа с кодом

---

### Полная реализация (8-10 недель):
1. ✅ MVP (выше)
2. ✅ Preview system
3. ✅ Validation tools
4. ✅ File operations (split/synthesize)

**Результат:**
- Feature parity с ultrascript (где применимо)
- Production-ready инструменты

---

### С опциональными фичами (10-12 недель):
1. ✅ Полная реализация (выше)
2. ⚠️ Technology detection
3. ⚠️ Pattern search (4 режима)

**Результат:**
- Максимальный функционал
- Все возможности ultrascript

---

## 📞 Следующие шаги

**Рекомендую начать с:**

### Sprint 1: Semantic Tools (2 недели)
```
1. Создать SemanticAnalysisTools.cs
2. Реализовать 3 tools:
   - semantic_search
   - semantic_diff
   - detect_code_clones
3. Написать тесты
4. Обновить ULTRA_SHARP_SEMANTIC.md
```

**Хотите, чтобы я:**
1. Создал полную реализацию `SemanticAnalysisTools.cs`?
2. Написал тесты?
3. Обновил документацию?
4. Создал PR с этими изменениями?

Выберите любой вариант, и начнём! 🚀

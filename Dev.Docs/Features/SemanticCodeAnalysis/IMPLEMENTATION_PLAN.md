# Implementation Plan - Semantic Code Analysis Enrichment

## Roadmap

```
Phase 1: Semantic Enrichment      [~3-5 days]  ✅ В работе
Phase 2: EditorConfig Generation  [~2-3 days]  ✅ В работе
Phase 3: LLM Review Integration   [TBD]        ⏸️ Отложено
```

## Phase 1: Semantic Enrichment

**Цель:** Добавить semantic enrichment к analyze_code_style с кластеризацией и relevance scoring.

### Step 1.1: Модели данных (1 день)

**Задачи:**
- [ ] Создать `Models/SemanticEnrichment/` папку
- [ ] Создать `DiagnosticStatistics.cs`
- [ ] Создать `SemanticEnrichmentResult.cs`
- [ ] Создать `DiagnosticCluster.cs`
- [ ] Создать `DiagnosticExample.cs`
- [ ] Создать `DiagnosticCategory.cs` enum
- [ ] Создать `EditorConfigSeverity.cs` enum

**Файлы:**
```
UltrasharpTools.Tools/Models/SemanticEnrichment/
├── DiagnosticStatistics.cs
├── SemanticEnrichmentResult.cs
├── DiagnosticCluster.cs
├── DiagnosticExample.cs
├── DiagnosticCategory.cs
├── EditorConfigSeverity.cs
└── EnrichmentSummary.cs
```

### Step 1.2: SemanticDiagnosticEnricher service (2 дня)

**Задачи:**
- [ ] Создать `Services/SemanticDiagnosticEnricher.cs`
- [ ] Создать `ISemanticDiagnosticEnricher` interface
- [ ] Реализовать `AnalyzeStatistics()` (LEVEL 1)
- [ ] Реализовать `ApplyHeuristicRules()` (LEVEL 1)
- [ ] Реализовать `ClusterBySimilarityAsync()` (LEVEL 2)
- [ ] Реализовать `DetectPattern()` (LEVEL 2)
- [ ] Реализовать `CalculateRelevance()` (LEVEL 2)
- [ ] Добавить unit tests

**Зависимости:**
- `ISemanticSearchService` - для генерации embeddings
- `ILogger<SemanticDiagnosticEnricher>` - для логирования

**Алгоритмы:**
```csharp
// Statistical analysis
public DiagnosticStatistics AnalyzeStatistics(
    List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics
)
{
    return new DiagnosticStatistics {
        CountByDiagnosticId = diagnostics
            .GroupBy(d => d.Diagnostic.Id)
            .ToDictionary(g => g.Key, g => g.Count()),

        FilesByDiagnosticId = diagnostics
            .GroupBy(d => d.Diagnostic.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.FilePath).Distinct().ToList()),

        ProjectsByDiagnosticId = diagnostics
            .GroupBy(d => d.Diagnostic.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProjectName).Distinct().ToList())
    };
}

// Clustering
private async Task<List<DiagnosticCluster>> ClusterBySimilarityAsync(
    List<DiagnosticOccurrence> occurrences,
    double threshold = 0.85
)
{
    // 1. Generate embeddings
    var embeddings = new List<(DiagnosticOccurrence, float[])>();

    // Batch processing для эффективности
    var batches = occurrences.Chunk(100);
    foreach (var batch in batches) {
        var contexts = batch.Select(ExtractContext).ToList();
        var batchEmbeddings = await _semanticSearch.GenerateEmbeddingsBatchAsync(contexts);

        for (int i = 0; i < batch.Length; i++) {
            embeddings.Add((batch[i], batchEmbeddings[i]));
        }
    }

    // 2. Agglomerative clustering
    // ...
}
```

### Step 1.3: Heuristic Rules (1 день)

**Задачи:**
- [ ] Создать `Services/SemanticEnrichment/Rules/` папку
- [ ] Создать `IHeuristicRule` interface
- [ ] Реализовать `LoggingOptimizationRule` (CA1873)
- [ ] Реализовать `BenchmarkInfrastructureRule` (CA1822)
- [ ] Реализовать `SecurityCriticalRule` (CA2***)
- [ ] Реализовать `PerformanceCriticalRule` (CA1829, CA1854, CA1845)
- [ ] Добавить unit tests

**Примеры rules:**
```csharp
public class LoggingOptimizationRule : IHeuristicRule
{
    public string RuleId => "HEURISTIC_CA1873_LOGGING";

    public bool Matches(DiagnosticCluster cluster, DiagnosticStatistics statistics)
    {
        return cluster.DiagnosticId == "CA1873"
            && cluster.Occurrences > 100
            && cluster.Pattern.Contains("ILogger", StringComparison.OrdinalIgnoreCase);
    }

    public DiagnosticCategory Category => DiagnosticCategory.FalsePositive;
    public EditorConfigSeverity RecommendedSeverity => EditorConfigSeverity.None;
    public double ConfidenceScore => 0.95;
    public string Justification =>
        "Modern .NET (6.0+) automatically optimizes LoggerMessage through source generators. " +
        "Manual optimization with LoggerMessage.Define is no longer necessary.";
}

public class BenchmarkInfrastructureRule : IHeuristicRule
{
    public string RuleId => "HEURISTIC_CA1822_BENCHMARK";

    public bool Matches(DiagnosticCluster cluster, DiagnosticStatistics statistics)
    {
        return cluster.DiagnosticId == "CA1822"
            && cluster.AffectedFiles.Any(f => f.Contains("Benchmark", StringComparison.OrdinalIgnoreCase));
    }

    public DiagnosticCategory Category => DiagnosticCategory.Infrastructure;
    public EditorConfigSeverity RecommendedSeverity => EditorConfigSeverity.Suggestion;
    public double ConfidenceScore => 0.90;
    public string Justification =>
        "BenchmarkDotNet requires instance methods for proper lifecycle management. " +
        "Static methods cannot participate in benchmark setup/cleanup phases.";
}
```

### Step 1.4: Интеграция с DiagnosticService (0.5 дня)

**Задачи:**
- [ ] Расширить `DiagnosticFilterOptions` добавив `EnrichWithSemantics` и `GroupBySimilarity`
- [ ] Расширить `DiagnosticAnalysisResult` добавив `SemanticEnrichment`
- [ ] Модифицировать `DiagnosticService.AnalyzeAsync()` для вызова enricher
- [ ] Добавить DI регистрацию в `Program.cs`
- [ ] Обновить MCP tool `analyze_code_style`

**Изменения в DiagnosticService:**
```csharp
public async Task<DiagnosticAnalysisResult> AnalyzeAsync(
    string solutionPath,
    DiagnosticFilterOptions filterOptions,
    CancellationToken cancellationToken = default
)
{
    // Существующий анализ
    var allDiagnostics = await GetAllDiagnosticsAsync(
        solutionPath,
        filterOptions,
        cancellationToken
    );

    // Фильтрация и пагинация (как раньше)
    var filteredDiagnostics = allDiagnostics
        .Where(item => filterOptions.ShouldIncludeDiagnostic(...))
        .ToList();

    var paginatedDiagnostics = filteredDiagnostics
        .Skip(filterOptions.Skip)
        .Take(filterOptions.Take)
        .ToList();

    var result = new DiagnosticAnalysisResult {
        Diagnostics = paginatedDiagnostics.Select(item => (item.Diagnostic, item.FilePath)).ToList(),
        TotalCount = filteredDiagnostics.Count,
        HasMore = filterOptions.Skip + filterOptions.Take < filteredDiagnostics.Count
    };

    // NEW: Semantic enrichment (опционально)
    if (filterOptions.EnrichWithSemantics && _semanticEnricher != null) {
        _logger.LogInformation("Starting semantic enrichment...");

        var statistics = _semanticEnricher.AnalyzeStatistics(allDiagnostics);
        result.SemanticEnrichment = await _semanticEnricher.EnrichAsync(
            allDiagnostics,
            statistics,
            cancellationToken
        );

        _logger.LogInformation(
            "Semantic enrichment complete: {ClusterCount} clusters, {CategoryCount} categories",
            result.SemanticEnrichment.Clusters.Count,
            result.SemanticEnrichment.Summary.CategoriesFound.Count
        );
    }

    return result;
}
```

### Step 1.5: Тестирование (0.5 дня)

**Задачи:**
- [ ] Unit tests для `SemanticDiagnosticEnricher`
- [ ] Unit tests для heuristic rules
- [ ] Integration test на UltrasharpTools.sln
- [ ] Performance benchmark

**Test cases:**
```csharp
[Fact]
public async Task EnrichAsync_WithCA1873Logging_CategorizesAsFalsePositive()
{
    // Arrange
    var diagnostics = CreateDiagnostics("CA1873", count: 1500, pattern: "ILogger");
    var statistics = _enricher.AnalyzeStatistics(diagnostics);

    // Act
    var result = await _enricher.EnrichAsync(diagnostics, statistics, CancellationToken.None);

    // Assert
    var cluster = result.Clusters.Single(c => c.DiagnosticId == "CA1873");
    Assert.Equal(DiagnosticCategory.FalsePositive, cluster.Category);
    Assert.True(cluster.ConfidenceScore > 0.9);
    Assert.Equal(EditorConfigSeverity.None, cluster.RecommendedSeverity);
}

[Fact]
public async Task EnrichAsync_WithCA1829Performance_CategorizesAsCritical()
{
    // Arrange
    var diagnostics = CreateDiagnostics("CA1829", count: 4, pattern: "Dictionary.Count()");
    var statistics = _enricher.AnalyzeStatistics(diagnostics);

    // Act
    var result = await _enricher.EnrichAsync(diagnostics, statistics, CancellationToken.None);

    // Assert
    var cluster = result.Clusters.Single(c => c.DiagnosticId == "CA1829");
    Assert.Equal(DiagnosticCategory.PerformanceCritical, cluster.Category);
    Assert.Equal(EditorConfigSeverity.Warning, cluster.RecommendedSeverity);
    Assert.True(result.RelevanceScores["CA1829"] > 8.0);
}
```

---

## Phase 2: EditorConfig Generation

**Цель:** Генерировать .editorconfig с документированными решениями.

### Step 2.1: Модели данных (0.5 дня)

**Задачи:**
- [ ] Создать `EditorConfigRecommendations.cs`
- [ ] Создать `EditorConfigRule.cs`
- [ ] Создать `ManualReviewCase.cs`
- [ ] Создать `EditorConfigOptions.cs`

**Файлы:**
```
UltrasharpTools.Tools/Models/SemanticEnrichment/
├── EditorConfigRecommendations.cs
├── EditorConfigRule.cs
├── ManualReviewCase.cs
└── EditorConfigOptions.cs
```

### Step 2.2: EditorConfigGenerator service (1.5 дня)

**Задачи:**
- [ ] Создать `Services/EditorConfigGenerator.cs`
- [ ] Создать `IEditorConfigGenerator` interface
- [ ] Реализовать `GenerateRecommendations()`
- [ ] Реализовать `GroupByCategory()`
- [ ] Реализовать `FormatSection()`
- [ ] Реализовать `IdentifyManualReviewCases()`
- [ ] Добавить templates для formatting
- [ ] Добавить unit tests

**Алгоритм:**
```csharp
public EditorConfigRecommendations GenerateRecommendations(
    SemanticEnrichmentResult enrichment,
    DiagnosticStatistics statistics
)
{
    var categories = GroupByCategory(enrichment.Clusters);
    var manualReviewCases = IdentifyManualReviewCases(enrichment.Clusters);

    var content = new StringBuilder();

    // Header
    content.AppendLine("# ============================================================================");
    content.AppendLine("# CODE ANALYSIS SEVERITY CONFIGURATION");
    content.AppendLine("# ============================================================================");
    content.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
    content.AppendLine($"# Analysis: {enrichment.Summary.TotalDiagnostics} diagnostics analyzed");
    content.AppendLine($"# Clusters: {enrichment.Clusters.Count}");
    content.AppendLine($"# Categories: {categories.Count}");
    content.AppendLine();

    // Sections по категориям
    foreach (var (category, clusters) in categories.OrderBy(x => x.Key)) {
        content.AppendLine(FormatSection(category, clusters));
        content.AppendLine();
    }

    // Manual review section
    if (manualReviewCases.Count > 0) {
        content.AppendLine(FormatManualReviewSection(manualReviewCases));
    }

    return new EditorConfigRecommendations {
        Content = content.ToString(),
        Summary = GenerateSummary(enrichment, categories),
        Rules = GenerateRules(enrichment.Clusters),
        RequiresManualReview = manualReviewCases
    };
}

private string FormatSection(
    DiagnosticCategory category,
    List<DiagnosticCluster> clusters
)
{
    var section = new StringBuilder();

    // Section header
    section.AppendLine("# " + new string('-', 76));
    section.AppendLine($"# {GetCategoryTitle(category)}");
    section.AppendLine("# " + new string('-', 76));
    section.AppendLine();

    // Rules
    foreach (var cluster in clusters.OrderBy(c => c.DiagnosticId)) {
        section.AppendLine($"# {cluster.DiagnosticId}: {cluster.Pattern}");
        section.AppendLine($"# ОБОСНОВАНИЕ: {cluster.Justification}");
        section.AppendLine($"# СТАТИСТИКА: {cluster.Occurrences} occurrences");
        section.AppendLine($"# CONFIDENCE: {cluster.ConfidenceScore:P0}");
        section.AppendLine($"dotnet_diagnostic.{cluster.DiagnosticId}.severity = {cluster.RecommendedSeverity.ToString().ToLowerInvariant()}");
        section.AppendLine();
    }

    return section.ToString();
}
```

### Step 2.3: Интеграция с DiagnosticService (0.5 дня)

**Задачи:**
- [ ] Расширить `DiagnosticFilterOptions` добавив `GenerateEditorConfigRecommendations`
- [ ] Модифицировать `DiagnosticService.AnalyzeAsync()` для вызова generator
- [ ] Добавить DI регистрацию
- [ ] Обновить MCP tool

**Интеграция:**
```csharp
// В DiagnosticService.AnalyzeAsync():
if (filterOptions.GenerateEditorConfigRecommendations
    && result.SemanticEnrichment != null
    && _editorConfigGenerator != null)
{
    _logger.LogInformation("Generating .editorconfig recommendations...");

    result.EditorConfigRecommendations = _editorConfigGenerator.GenerateRecommendations(
        result.SemanticEnrichment,
        statistics
    );

    _logger.LogInformation(
        ".editorconfig generated: {RuleCount} rules, {ManualReviewCount} require manual review",
        result.EditorConfigRecommendations.Rules.Count,
        result.EditorConfigRecommendations.RequiresManualReview.Count
    );
}
```

### Step 2.4: Тестирование (0.5 дня)

**Задачи:**
- [ ] Unit tests для `EditorConfigGenerator`
- [ ] Integration test на реальных данных
- [ ] Валидация сгенерированного .editorconfig (парсинг)
- [ ] Visual review сгенерированного контента

**Test cases:**
```csharp
[Fact]
public void GenerateRecommendations_WithFalsePositives_GeneratesNoneSeverity()
{
    // Arrange
    var enrichment = CreateEnrichment(
        new DiagnosticCluster {
            DiagnosticId = "CA1873",
            Category = DiagnosticCategory.FalsePositive,
            RecommendedSeverity = EditorConfigSeverity.None,
            Occurrences = 1500
        }
    );

    // Act
    var recommendations = _generator.GenerateRecommendations(enrichment, statistics);

    // Assert
    Assert.Contains("dotnet_diagnostic.CA1873.severity = none", recommendations.Content);
    Assert.Contains("FALSE POSITIVES", recommendations.Content);
    var rule = recommendations.Rules.Single(r => r.DiagnosticId == "CA1873");
    Assert.Equal(EditorConfigSeverity.None, rule.Severity);
}

[Fact]
public void GenerateRecommendations_WithLowConfidence_AddsToManualReview()
{
    // Arrange
    var enrichment = CreateEnrichment(
        new DiagnosticCluster {
            DiagnosticId = "CA1068",
            ConfidenceScore = 0.6, // Low confidence
            Occurrences = 2
        }
    );

    // Act
    var recommendations = _generator.GenerateRecommendations(enrichment, statistics);

    // Assert
    Assert.Single(recommendations.RequiresManualReview);
    Assert.Contains("CA1068", recommendations.Content);
    Assert.Contains("REQUIRES MANUAL REVIEW", recommendations.Content);
}
```

---

## Phase 3: LLM Review Integration (отложено)

**Статус:** ⏸️ Отложено для обдумывания концепции локального инструмента.

**Вопросы для решения:**
- Как интегрировать LLM без нарушения privacy?
- Какие части анализа отправлять в LLM? (только statistics или code snippets?)
- Нужна ли поддержка локальных LLM (Ollama, etc)?
- Как хранить API keys безопасно?
- Нужна ли опция полностью отключить LLM integration?

**Возможные подходы:**
1. **Опционально через API key** - пользователь сам решает использовать ли
2. **Только для uncertain cases** - отправляем минимум данных
3. **Локальные LLM** - поддержка Ollama/LlamaCpp для privacy
4. **Differential privacy** - обезличивание code snippets перед отправкой

---

## Приоритеты и зависимости

### Критичный путь:
```
Step 1.1 (Models)
    → Step 1.2 (Enricher)
        → Step 1.3 (Rules)
            → Step 1.4 (Integration)
                → Step 1.5 (Tests)
                    → Step 2.1 (EditorConfig Models)
                        → Step 2.2 (Generator)
                            → Step 2.3 (Integration)
                                → Step 2.4 (Tests)
```

### Можно делать параллельно:
- Step 1.2 (Enricher) и Step 1.3 (Rules) - независимы
- Step 2.1 (Models) можно начать после Step 1.2

## Критерии успеха

### Phase 1:
- ✅ Semantic enrichment работает на UltrasharpTools.sln
- ✅ Корректно категоризирует CA1873 как FalsePositive
- ✅ Корректно категоризирует CA1829/CA1854/CA1845 как Critical
- ✅ Relevance scores логичны (Security > Performance > Style)
- ✅ Performance: enrichment < 20s на 1621 diagnostics

### Phase 2:
- ✅ Генерируется валидный .editorconfig
- ✅ Все правила документированы с обоснованиями
- ✅ Manual review cases highlighted
- ✅ Сгенерированный .editorconfig читаем и понятен человеку

## Риски и митигация

| Риск | Вероятность | Воздействие | Митигация |
|------|-------------|-------------|-----------|
| Semantic model недоступна | Средняя | Высокое | Graceful degradation - только Level 1 (heuristics) |
| Embeddings слишком медленны | Средняя | Среднее | Batch processing, caching, parallel |
| Clustering даёт плохие результаты | Низкая | Среднее | Fallback на heuristics, tune threshold |
| Генерируемый .editorconfig некорректен | Низкая | Высокое | Валидация, unit tests, manual review |

## Метрики успеха

После релиза замерять:
- **Adoption rate** - сколько пользователей используют enrichment
- **Accuracy** - процент правильно категоризированных diagnostics
- **Time saved** - сколько времени экономится на ручном анализе
- **False positives suppressed** - сколько FP автоматически фильтруется
- **Performance** - время выполнения enrichment
- **User satisfaction** - feedback от пользователей

## Следующие шаги

1. ✅ Создать документацию (текущий документ)
2. ⏭️ **Phase 1 Step 1.1** - Создать модели данных
3. ⏭️ **Phase 1 Step 1.2** - Реализовать SemanticDiagnosticEnricher
4. Продолжить по плану...

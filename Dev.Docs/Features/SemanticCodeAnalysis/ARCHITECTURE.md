# Semantic Code Analysis - Архитектура

## Обзор

Semantic Code Analysis Enrichment расширяет существующий `DiagnosticService` и `analyze_code_style` MCP tool для интеллектуального анализа и категоризации диагностик Roslyn анализаторов.

## Компоненты системы

### 1. DiagnosticService (существующий)

**Текущий функционал:**
```csharp
public class DiagnosticService : IDiagnosticService
{
    // Основной метод анализа
    public Task<DiagnosticAnalysisResult> AnalyzeAsync(
        string solutionPath,
        DiagnosticFilterOptions filterOptions,
        CancellationToken cancellationToken
    );

    // Incremental analysis (commit 1d8da79)
    private HashSet<string> GetChangedFiles(Solution solution);
    private Task BuildDependencyGraphAsync(Solution solution);

    // Project-level analysis
    private Task<IEnumerable<(Diagnostic, string, string)>> AnalyzeProjectAsync(
        Project project,
        DiagnosticFilterOptions filterOptions
    );
}
```

**Возвращает:**
```csharp
public class DiagnosticAnalysisResult
{
    public List<(Diagnostic Diagnostic, string FilePath)> Diagnostics { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}
```

### 2. SemanticDiagnosticEnricher (новый) 🆕

**Ответственность:**
- Кластеризация диагностик по семантическому сходству
- Pattern detection (logging, benchmarks, tests)
- Relevance scoring
- Confidence calculation

```csharp
public class SemanticDiagnosticEnricher : ISemanticDiagnosticEnricher
{
    private readonly ISemanticSearchService _semanticSearch;
    private readonly ILogger<SemanticDiagnosticEnricher> _logger;

    // LEVEL 1: Statistical analysis
    public DiagnosticStatistics AnalyzeStatistics(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics
    );

    // LEVEL 2: Semantic enrichment
    public async Task<SemanticEnrichmentResult> EnrichAsync(
        List<(Diagnostic Diagnostic, string FilePath, string ProjectName)> diagnostics,
        DiagnosticStatistics statistics,
        CancellationToken cancellationToken
    );

    // Helper: Cluster by similarity
    private async Task<List<DiagnosticCluster>> ClusterBySimilarityAsync(
        List<DiagnosticOccurrence> occurrences,
        double similarityThreshold = 0.85
    );

    // Helper: Detect pattern
    private DiagnosticPattern DetectPattern(DiagnosticCluster cluster);

    // Helper: Calculate relevance
    private double CalculateRelevance(DiagnosticCluster cluster);
}
```

**Модели данных:**

```csharp
// Статистика по диагностикам
public class DiagnosticStatistics
{
    public Dictionary<string, int> CountByDiagnosticId { get; set; }
    public Dictionary<string, List<string>> FilesByDiagnosticId { get; set; }
    public Dictionary<string, List<string>> ProjectsByDiagnosticId { get; set; }
    public Dictionary<string, DiagnosticSeverity> SeverityByDiagnosticId { get; set; }
}

// Результат semantic enrichment
public class SemanticEnrichmentResult
{
    public List<DiagnosticCluster> Clusters { get; set; }
    public Dictionary<string, double> RelevanceScores { get; set; }
    public EnrichmentSummary Summary { get; set; }
}

// Кластер похожих диагностик
public class DiagnosticCluster
{
    public string DiagnosticId { get; set; }
    public DiagnosticCategory Category { get; set; }
    public string Pattern { get; set; }
    public int Occurrences { get; set; }
    public double ConfidenceScore { get; set; }
    public EditorConfigSeverity RecommendedSeverity { get; set; }
    public string Justification { get; set; }
    public List<DiagnosticExample> RepresentativeExamples { get; set; }
    public List<string> AffectedFiles { get; set; }
}

// Пример диагностики
public class DiagnosticExample
{
    public string FilePath { get; set; }
    public int Line { get; set; }
    public string Snippet { get; set; }
    public double SimilarityToCentroid { get; set; }
}

// Категории диагностик
public enum DiagnosticCategory
{
    FalsePositive,      // CA1873 - logging optimization
    PerformanceCritical, // CA1829, CA1854, CA1845
    Infrastructure,     // CA1822 - BenchmarkDotNet
    Security,           // CA2*** security rules
    Reliability,        // CA2*** reliability rules
    Maintainability,    // CA15** maintainability
    Style,              // IDE**** style rules
    NeedsManualReview   // Low confidence or uncertain
}

// Рекомендуемые severity
public enum EditorConfigSeverity
{
    None,      // Полностью игнорируем
    Silent,    // Применяем при автофиксе, не показываем
    Suggestion, // Показываем как suggestion в IDE
    Warning,   // Показываем как warning
    Error      // Блокируем build
}
```

### 3. EditorConfigGenerator (новый) 🆕

**Ответственность:**
- Генерация .editorconfig из clusters
- Форматирование с комментариями
- Group by category
- Highlight manual review cases

```csharp
public class EditorConfigGenerator : IEditorConfigGenerator
{
    private readonly ILogger<EditorConfigGenerator> _logger;

    public EditorConfigRecommendations GenerateRecommendations(
        SemanticEnrichmentResult enrichment,
        DiagnosticStatistics statistics
    );

    // Генерация контента .editorconfig
    private string GenerateEditorConfigContent(
        List<DiagnosticCluster> clusters,
        EditorConfigOptions options
    );

    // Группировка по категориям
    private Dictionary<DiagnosticCategory, List<DiagnosticCluster>> GroupByCategory(
        List<DiagnosticCluster> clusters
    );

    // Форматирование секции
    private string FormatSection(
        DiagnosticCategory category,
        List<DiagnosticCluster> clusters
    );
}
```

**Модель:**

```csharp
public class EditorConfigRecommendations
{
    public string Content { get; set; }  // Готовый .editorconfig
    public string Summary { get; set; }
    public List<EditorConfigRule> Rules { get; set; }
    public List<ManualReviewCase> RequiresManualReview { get; set; }
}

public class EditorConfigRule
{
    public string DiagnosticId { get; set; }
    public EditorConfigSeverity Severity { get; set; }
    public string Justification { get; set; }
    public string EditorConfigLine { get; set; }
    public DiagnosticStatistics Statistics { get; set; }
}

public class ManualReviewCase
{
    public string DiagnosticId { get; set; }
    public string Reason { get; set; }
    public int Occurrences { get; set; }
    public List<DiagnosticExample> Examples { get; set; }
}
```

### 4. Расширение DiagnosticAnalysisResult

```csharp
public class DiagnosticAnalysisResult
{
    // Существующие поля
    public List<(Diagnostic Diagnostic, string FilePath)> Diagnostics { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }

    // НОВЫЕ: Semantic enrichment (опционально)
    public SemanticEnrichmentResult? SemanticEnrichment { get; set; }

    // НОВЫЕ: EditorConfig recommendations (опционально)
    public EditorConfigRecommendations? EditorConfigRecommendations { get; set; }
}
```

## Архитектурные решения

### 1. Обратная совместимость

Новый функционал **полностью опционален**:
```csharp
// Старый способ - работает как раньше
var result = await AnalyzeAsync(solutionPath, filterOptions);

// Новый способ - opt-in
var enrichedResult = await AnalyzeAsync(
    solutionPath,
    filterOptions with {
        EnrichWithSemantics = true,
        GenerateEditorConfigRecommendations = true
    }
);
```

### 2. Эффективность

- **Переиспользование контекста:** Solution и Compilation уже загружены
- **Lazy evaluation:** Semantic enrichment только если запрошен
- **Incremental:** Использует существующий file cache (commit 1d8da79)
- **Parallel:** Кластеризация диагностик параллельно

### 3. Расширяемость

**Heuristic Rules:**
```csharp
public interface IHeuristicRule
{
    bool Matches(DiagnosticCluster cluster, DiagnosticStatistics statistics);
    DiagnosticCategory? Category { get; }
    EditorConfigSeverity? RecommendedSeverity { get; }
    string Justification { get; }
}

// Пример: CA1873 logging rule
public class LoggingOptimizationRule : IHeuristicRule
{
    public bool Matches(DiagnosticCluster cluster, DiagnosticStatistics statistics)
    {
        return cluster.DiagnosticId == "CA1873"
            && cluster.Occurrences > 100
            && cluster.Pattern.Contains("ILogger");
    }

    public DiagnosticCategory? Category => DiagnosticCategory.FalsePositive;
    public EditorConfigSeverity? RecommendedSeverity => EditorConfigSeverity.None;
    public string Justification => "Modern .NET (6.0+) optimizes LoggerMessage automatically";
}
```

**Pattern Detectors:**
```csharp
public interface IPatternDetector
{
    string DetectPattern(DiagnosticCluster cluster);
    double ConfidenceScore { get; }
}

// Пример: Benchmark pattern detector
public class BenchmarkPatternDetector : IPatternDetector
{
    public string DetectPattern(DiagnosticCluster cluster)
    {
        if (cluster.AffectedFiles.Any(f => f.Contains("Benchmark")))
            return "BenchmarkDotNet infrastructure";
        return null;
    }

    public double ConfidenceScore => 0.95;
}
```

### 4. Dependency Injection

```csharp
// Startup.cs или Program.cs
services.AddSingleton<ISemanticDiagnosticEnricher, SemanticDiagnosticEnricher>();
services.AddSingleton<IEditorConfigGenerator, EditorConfigGenerator>();

// Регистрация heuristic rules
services.AddSingleton<IHeuristicRule, LoggingOptimizationRule>();
services.AddSingleton<IHeuristicRule, BenchmarkInfrastructureRule>();
services.AddSingleton<IHeuristicRule, SecurityCriticalRule>();

// Регистрация pattern detectors
services.AddSingleton<IPatternDetector, BenchmarkPatternDetector>();
services.AddSingleton<IPatternDetector, TestPatternDetector>();
services.AddSingleton<IPatternDetector, LoggingPatternDetector>();
```

## Потоки данных

### Flow 1: Semantic Enrichment

```
analyze_code_style(enrichWithSemantics: true)
    ↓
DiagnosticService.AnalyzeAsync()
    ↓
[Существующий анализ: Roslyn + Incremental]
    ↓ List<(Diagnostic, FilePath, ProjectName)>
    ↓
SemanticDiagnosticEnricher.EnrichAsync()
    ├─→ AnalyzeStatistics() [LEVEL 1]
    │   └─→ Count occurrences, group by ID
    │
    ├─→ ApplyHeuristicRules() [LEVEL 1]
    │   └─→ Obvious patterns (logging, benchmarks)
    │
    ├─→ ClusterBySimilarityAsync() [LEVEL 2]
    │   ├─→ Generate embeddings (ISemanticSearchService)
    │   └─→ Cosine similarity clustering
    │
    ├─→ DetectPattern() [LEVEL 2]
    │   └─→ Pattern detectors (logging, tests, etc)
    │
    └─→ CalculateRelevance() [LEVEL 2]
        └─→ Scoring algorithm
    ↓
SemanticEnrichmentResult
    ↓
DiagnosticAnalysisResult.SemanticEnrichment
```

### Flow 2: EditorConfig Generation

```
analyze_code_style(generateEditorConfigRecommendations: true)
    ↓
[Flow 1: Semantic Enrichment]
    ↓ SemanticEnrichmentResult
    ↓
EditorConfigGenerator.GenerateRecommendations()
    ├─→ GroupByCategory()
    │   └─→ FALSE_POSITIVES, CRITICAL, NEEDS_REVIEW
    │
    ├─→ FormatSection() (для каждой категории)
    │   ├─→ Header comment
    │   ├─→ Rules с обоснованиями
    │   └─→ Statistics
    │
    └─→ IdentifyManualReviewCases()
        └─→ confidence < 0.8 || category == Security
    ↓
EditorConfigRecommendations
    ↓
DiagnosticAnalysisResult.EditorConfigRecommendations
```

## Алгоритмы

### 1. Semantic Clustering

```csharp
private async Task<List<DiagnosticCluster>> ClusterBySimilarityAsync(
    List<DiagnosticOccurrence> occurrences,
    double threshold = 0.85
)
{
    // 1. Generate embeddings для каждой диагностики
    var embeddings = new List<(DiagnosticOccurrence, float[])>();
    foreach (var occurrence in occurrences) {
        var context = ExtractContext(occurrence); // code snippet + message
        var embedding = await _semanticSearch.GenerateEmbeddingAsync(context);
        embeddings.Add((occurrence, embedding));
    }

    // 2. Agglomerative clustering
    var clusters = new List<DiagnosticCluster>();
    var processed = new HashSet<int>();

    for (int i = 0; i < embeddings.Count; i++) {
        if (processed.Contains(i)) continue;

        var cluster = new DiagnosticCluster {
            DiagnosticId = embeddings[i].Item1.DiagnosticId
        };

        // Find similar diagnostics
        for (int j = i + 1; j < embeddings.Count; j++) {
            if (processed.Contains(j)) continue;

            var similarity = CosineSimilarity(embeddings[i].Item2, embeddings[j].Item2);
            if (similarity >= threshold) {
                cluster.Members.Add(embeddings[j].Item1);
                processed.Add(j);
            }
        }

        clusters.Add(cluster);
        processed.Add(i);
    }

    // 3. Calculate centroid для каждого кластера
    foreach (var cluster in clusters) {
        cluster.Centroid = CalculateCentroid(cluster.Members);
        cluster.RepresentativeExamples = SelectTopExamples(cluster, 3);
    }

    return clusters;
}
```

### 2. Relevance Scoring

```csharp
private double CalculateRelevance(DiagnosticCluster cluster)
{
    double score = 0.0;

    // Category weight
    score += cluster.Category switch {
        DiagnosticCategory.Security => 10.0,
        DiagnosticCategory.PerformanceCritical => 9.0,
        DiagnosticCategory.Reliability => 8.0,
        DiagnosticCategory.Maintainability => 5.0,
        DiagnosticCategory.Infrastructure => 2.0,
        DiagnosticCategory.FalsePositive => 0.5,
        _ => 5.0
    };

    // Severity weight
    score *= cluster.Severity switch {
        DiagnosticSeverity.Error => 2.0,
        DiagnosticSeverity.Warning => 1.5,
        DiagnosticSeverity.Info => 1.0,
        _ => 0.5
    };

    // Confidence weight
    score *= cluster.ConfidenceScore;

    // Occurrences penalty (many occurrences → likely FP)
    if (cluster.Occurrences > 100) {
        score *= 0.5;
    }

    return Math.Min(score, 10.0); // Cap at 10.0
}
```

### 3. Pattern Detection

```csharp
private DiagnosticPattern DetectPattern(DiagnosticCluster cluster)
{
    // Apply pattern detectors in order of confidence
    foreach (var detector in _patternDetectors.OrderByDescending(d => d.ConfidenceScore)) {
        var pattern = detector.DetectPattern(cluster);
        if (pattern != null) {
            return new DiagnosticPattern {
                Name = pattern,
                Confidence = detector.ConfidenceScore,
                DetectorType = detector.GetType().Name
            };
        }
    }

    // Fallback: generic pattern based on file paths
    var commonPath = FindCommonPathPrefix(cluster.AffectedFiles);
    return new DiagnosticPattern {
        Name = $"Code in {commonPath}",
        Confidence = 0.5,
        DetectorType = "FallbackDetector"
    };
}
```

## Производительность

### Оптимизации:

1. **Lazy Evaluation:** Semantic enrichment только если `enrichWithSemantics = true`
2. **Parallel Processing:** Кластеризация диагностик параллельно по DiagnosticId
3. **Caching:** Embeddings кешируются на время анализа
4. **Batch Processing:** Embeddings генерируются батчами (50-100 за раз)
5. **Early Exit:** Heuristic rules применяются до semantic clustering

### Оценки:

| Операция | Время | Комментарий |
|----------|-------|-------------|
| Statistical analysis | <1s | Простой подсчёт |
| Heuristic rules | <1s | Pattern matching |
| Embedding generation | 5-10s | 1621 diagnostics, batch size 100 |
| Clustering | 2-5s | Cosine similarity matrix |
| EditorConfig generation | <1s | Template formatting |
| **TOTAL** | **~10-20s** | На 1621 diagnostics |

## Безопасность

- ✅ Всё работает локально
- ✅ Код не покидает машину
- ✅ Embeddings генерируются локальной моделью
- ✅ Нет отправки в cloud APIs
- ✅ .editorconfig не содержит code snippets (только statistics)

## Тестирование

### Unit Tests:
- `SemanticDiagnosticEnricherTests` - тесты enrichment logic
- `EditorConfigGeneratorTests` - тесты генерации .editorconfig
- `HeuristicRulesTests` - тесты heuristic rules
- `PatternDetectorTests` - тесты pattern detectors

### Integration Tests:
- `SemanticEnrichmentIntegrationTests` - полный flow на реальном solution
- `EditorConfigGenerationIntegrationTests` - генерация и валидация .editorconfig

### Performance Tests:
- `SemanticEnrichmentBenchmarks` - BenchmarkDotNet тесты

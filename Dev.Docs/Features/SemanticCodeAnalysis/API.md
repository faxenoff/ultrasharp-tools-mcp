# API Specification - Semantic Code Analysis Enrichment

## MCP Tool: `analyze_code_style`

### Расширенные параметры (новые) 🆕

```typescript
{
  // Существующие параметры
  "solutionPath": string,           // Путь к .sln
  "preset": string?,                // performance, reliability, security, etc
  "diagnosticIds": string?,         // Фильтр по ID (comma-separated)
  "severityFilter": string?,        // Warning, Info, Error
  "projectNames": string?,          // Фильтр по проектам
  "filePatterns": string?,          // Glob patterns
  "skip": number?,                  // Пагинация
  "take": number?,                  // Пагинация

  // НОВЫЕ параметры Phase 1
  "enrichWithSemantics": boolean?,  // Включить semantic enrichment (default: false)
  "groupBySimilarity": boolean?,    // Группировать похожие (default: false)
  "similarityThreshold": number?,   // Порог сходства 0.0-1.0 (default: 0.85)

  // НОВЫЕ параметры Phase 2
  "generateEditorConfigRecommendations": boolean?, // Генерировать .editorconfig (default: false)
  "editorConfigFormat": string?     // "standard" | "detailed" (default: "detailed")
}
```

### Response format

```typescript
{
  // Существующие поля
  "summary": string,                 // Markdown summary
  "totalCount": number,              // Общее количество диагностик
  "returnedCount": number,           // Возвращено в этом ответе
  "hasMore": boolean,                // Есть ли ещё результаты
  "diagnostics": [                   // Список диагностик
    {
      "id": string,                  // CA1822, IDE0001, etc
      "severity": string,            // Error, Warning, Info
      "message": string,             // Описание проблемы
      "filePath": string,            // Абсолютный путь к файлу
      "line": number,                // Строка
      "column": number               // Колонка
    }
  ],

  // НОВОЕ: Semantic enrichment (если enrichWithSemantics = true)
  "semanticEnrichment": {
    "clusters": [
      {
        "diagnosticId": string,            // CA1873, etc
        "category": string,                // Enum: FalsePositive, PerformanceCritical, etc
        "pattern": string,                 // "Logging infrastructure", "Dictionary.Count() in hot paths"
        "occurrences": number,             // Количество срабатываний
        "confidenceScore": number,         // 0.0-1.0
        "recommendedSeverity": string,     // none, suggestion, warning, error
        "justification": string,           // Обоснование решения
        "representativeExamples": [
          {
            "filePath": string,
            "line": number,
            "snippet": string,             // Фрагмент кода
            "similarity": number           // Сходство с центроидом 0.0-1.0
          }
        ],
        "affectedFiles": string[],         // Список затронутых файлов
        "affectedProjects": string[]       // Список затронутых проектов
      }
    ],

    "relevanceScores": {
      // DiagnosticId → relevance score (0.0-10.0)
      "CA1829": 10.0,    // Критично
      "CA1854": 9.5,
      "CA1845": 8.0,
      "CA1873": 0.5,     // False positive
      "CA1822": 2.0
    },

    "summary": {
      "totalDiagnostics": number,
      "totalClusters": number,
      "categoriesFound": {
        "FalsePositive": number,
        "PerformanceCritical": number,
        "Infrastructure": number,
        "Security": number,
        "Reliability": number,
        "Maintainability": number,
        "Style": number,
        "NeedsManualReview": number
      },
      "autoSuppressRecommendations": number,  // Можно автоматически игнорировать
      "criticalIssuesFound": number,          // Требует исправления
      "manualReviewRequired": number          // Требует ручной проверки
    },

    "processingStats": {
      "statisticalAnalysisMs": number,
      "heuristicRulesMs": number,
      "clusteringMs": number,
      "patternDetectionMs": number,
      "totalMs": number
    }
  },

  // НОВОЕ: EditorConfig recommendations (если generateEditorConfigRecommendations = true)
  "editorConfigRecommendations": {
    "content": string,                 // Полный .editorconfig content
    "summary": string,                 // Markdown summary
    "rules": [
      {
        "diagnosticId": string,
        "severity": string,            // none, suggestion, warning, error
        "justification": string,
        "category": string,
        "editorConfigLine": string,    // dotnet_diagnostic.CA1873.severity = none
        "statistics": {
          "occurrences": number,
          "affectedFiles": number,
          "affectedProjects": number
        }
      }
    ],
    "requiresManualReview": [
      {
        "diagnosticId": string,
        "reason": string,              // Почему требует manual review
        "occurrences": number,
        "confidenceScore": number,     // Низкий confidence
        "examples": [
          {
            "filePath": string,
            "line": number,
            "snippet": string
          }
        ],
        "suggestedActions": string[]   // Что делать дальше
      }
    ],
    "stats": {
      "totalRulesGenerated": number,
      "autoApprovedRules": number,     // Высокий confidence
      "needsReviewRules": number,      // Низкий confidence
      "byCategory": {
        "FalsePositive": number,
        "PerformanceCritical": number,
        // etc
      }
    }
  }
}
```

## Примеры использования

### Пример 1: Базовый анализ (без enrichment)

**Request:**
```json
{
  "solutionPath": "D:\\Projects\\MyApp.sln",
  "preset": "performance",
  "take": 50
}
```

**Response:**
```json
{
  "summary": "## Code Style Analysis Results\n\n**Preset:** performance\n**Total diagnostics found:** 80\n**Showing:** 50",
  "totalCount": 80,
  "returnedCount": 50,
  "hasMore": true,
  "diagnostics": [
    {
      "id": "CA1829",
      "severity": "Info",
      "message": "Use the \"Count\" property instead of Enumerable.Count()",
      "filePath": "D:\\Projects\\MyApp\\Services\\UserService.cs",
      "line": 42,
      "column": 17
    }
    // ... 49 more
  ]
}
```

### Пример 2: С semantic enrichment

**Request:**
```json
{
  "solutionPath": "D:\\Projects\\MyApp.sln",
  "severityFilter": "Info",
  "enrichWithSemantics": true,
  "groupBySimilarity": true,
  "take": 100
}
```

**Response:**
```json
{
  "summary": "## Code Style Analysis Results\n\n**Total diagnostics found:** 1621\n**Showing:** 100\n\n**Semantic Enrichment:**\n- 15 clusters identified\n- 3 categories: FalsePositive (1500), Critical (3), Infrastructure (118)",
  "totalCount": 1621,
  "returnedCount": 100,
  "hasMore": true,
  "diagnostics": [ /* ... */ ],

  "semanticEnrichment": {
    "clusters": [
      {
        "diagnosticId": "CA1873",
        "category": "FalsePositive",
        "pattern": "Logging infrastructure (ILogger patterns)",
        "occurrences": 1500,
        "confidenceScore": 0.95,
        "recommendedSeverity": "none",
        "justification": "Modern .NET (6.0+) automatically optimizes LoggerMessage through source generators. Manual optimization with LoggerMessage.Define is no longer necessary.",
        "representativeExamples": [
          {
            "filePath": "D:\\Projects\\MyApp\\Services\\DiagnosticService.cs",
            "line": 121,
            "snippet": "_logger.LogInformation(\"Starting analysis for {SolutionPath}\", solutionPath);",
            "similarity": 0.98
          },
          {
            "filePath": "D:\\Projects\\MyApp\\Services\\SolutionManager.cs",
            "line": 67,
            "snippet": "_logger.LogDebug(\"Loading solution from {Path}\", path);",
            "similarity": 0.97
          }
        ],
        "affectedFiles": [
          "Services/**/*.cs",
          "Mcp/**/*.cs",
          "Controllers/**/*.cs"
        ],
        "affectedProjects": [
          "MyApp.Services",
          "MyApp.Api"
        ]
      },
      {
        "diagnosticId": "CA1829",
        "category": "PerformanceCritical",
        "pattern": "Dictionary.Count() extension method in hot paths",
        "occurrences": 4,
        "confidenceScore": 1.0,
        "recommendedSeverity": "warning",
        "justification": "Dictionary.Count property is O(1), Count() extension method adds overhead. In performance-critical code, use the property directly.",
        "representativeExamples": [
          {
            "filePath": "D:\\Projects\\MyApp\\Benchmarks\\SIMDBenchmarks.cs",
            "line": 71,
            "snippet": "if (vec1.Count() == 0 && vec2.Count() == 0)",
            "similarity": 1.0
          }
        ],
        "affectedFiles": [
          "Benchmarks/SIMDBenchmarks.cs"
        ],
        "affectedProjects": [
          "MyApp.Benchmarks"
        ]
      }
    ],

    "relevanceScores": {
      "CA1829": 10.0,
      "CA1854": 9.5,
      "CA1845": 8.0,
      "CA1873": 0.5,
      "CA1822": 2.0
    },

    "summary": {
      "totalDiagnostics": 1621,
      "totalClusters": 15,
      "categoriesFound": {
        "FalsePositive": 1500,
        "PerformanceCritical": 3,
        "Infrastructure": 118,
        "Security": 0,
        "Reliability": 0,
        "Maintainability": 0,
        "Style": 0,
        "NeedsManualReview": 0
      },
      "autoSuppressRecommendations": 1500,
      "criticalIssuesFound": 3,
      "manualReviewRequired": 0
    },

    "processingStats": {
      "statisticalAnalysisMs": 234,
      "heuristicRulesMs": 12,
      "clusteringMs": 4567,
      "patternDetectionMs": 89,
      "totalMs": 4902
    }
  }
}
```

### Пример 3: Генерация .editorconfig

**Request:**
```json
{
  "solutionPath": "D:\\Projects\\MyApp.sln",
  "enrichWithSemantics": true,
  "generateEditorConfigRecommendations": true,
  "editorConfigFormat": "detailed"
}
```

**Response:**
```json
{
  "summary": "...",
  "totalCount": 1621,
  "diagnostics": [ /* ... */ ],
  "semanticEnrichment": { /* ... */ },

  "editorConfigRecommendations": {
    "content": "# ============================================================================\n# CODE ANALYSIS SEVERITY CONFIGURATION\n# ============================================================================\n# Generated: 2025-11-22 15:30 UTC\n# Analysis: 1621 diagnostics analyzed\n# Clusters: 15\n# Categories: 3\n\n# ----------------------------------------------------------------------------\n# FALSE POSITIVES - игнорируем заведомо ложные срабатывания\n# ----------------------------------------------------------------------------\n\n# CA1873: Avoid unnecessary boxing from logging interpolated strings\n# ОБОСНОВАНИЕ: Modern .NET (6.0+) automatically optimizes LoggerMessage\n#              through source generators and compile-time logging optimization.\n#              Manual optimization with LoggerMessage.Define is no longer necessary.\n# СТАТИСТИКА: 1500 occurrences across 3 projects\n# CONFIDENCE: 95%\ndotnet_diagnostic.CA1873.severity = none\n\n# ----------------------------------------------------------------------------\n# PERFORMANCE RULES - критичные правила производительности\n# ----------------------------------------------------------------------------\n\n# CA1829: Use Length/Count property instead of Enumerable.Count() method\n# КРИТИЧНО: Dictionary.Count property is O(1), Count() extension method adds overhead\n# СТАТИСТИКА: 4 occurrences in Benchmarks/SIMDBenchmarks.cs\n# CONFIDENCE: 100%\ndotnet_diagnostic.CA1829.severity = warning\n\n# CA1854: Prefer TryGetValue over ContainsKey then vectordb\n# КРИТИЧНО: Избегаем двойного Dictionary lookup - из O(2n) в O(n)\n# СТАТИСТИКА: 1 occurrence in Services/ConfigurationLoader.cs\n# CONFIDENCE: 100%\ndotnet_diagnostic.CA1854.severity = warning\n\n# CA1845: Use span-based 'string.Concat' instead of 'Substring'\n# ВАЖНО: Zero-allocation string operations, избегаем heap allocations\n# СТАТИСТИКА: 1 occurrence in Services/ConfigurationLoader.cs\n# CONFIDENCE: 100%\ndotnet_diagnostic.CA1845.severity = warning\n",

    "summary": "Generated .editorconfig with 6 rules:\n- FALSE POSITIVES: 1 rule (CA1873)\n- PERFORMANCE RULES: 3 rules (CA1829, CA1854, CA1845)\n- INFRASTRUCTURE: 2 rules (CA1822)\n\nAuto-approved: 6 rules\nNeeds manual review: 0 rules",

    "rules": [
      {
        "diagnosticId": "CA1873",
        "severity": "none",
        "justification": "Modern .NET (6.0+) automatically optimizes LoggerMessage through source generators...",
        "category": "FalsePositive",
        "editorConfigLine": "dotnet_diagnostic.CA1873.severity = none",
        "statistics": {
          "occurrences": 1500,
          "affectedFiles": 142,
          "affectedProjects": 3
        }
      },
      {
        "diagnosticId": "CA1829",
        "severity": "warning",
        "justification": "Dictionary.Count property is O(1), Count() extension method adds overhead...",
        "category": "PerformanceCritical",
        "editorConfigLine": "dotnet_diagnostic.CA1829.severity = warning",
        "statistics": {
          "occurrences": 4,
          "affectedFiles": 1,
          "affectedProjects": 1
        }
      }
    ],

    "requiresManualReview": [],

    "stats": {
      "totalRulesGenerated": 6,
      "autoApprovedRules": 6,
      "needsReviewRules": 0,
      "byCategory": {
        "FalsePositive": 1,
        "PerformanceCritical": 3,
        "Infrastructure": 2
      }
    }
  }
}
```

### Пример 4: Manual review case

**Request:**
```json
{
  "solutionPath": "D:\\Projects\\MyApp.sln",
  "enrichWithSemantics": true,
  "generateEditorConfigRecommendations": true
}
```

**Response (fragment):**
```json
{
  "editorConfigRecommendations": {
    "requiresManualReview": [
      {
        "diagnosticId": "CA1068",
        "reason": "Low confidence (60%) - requires understanding of API design trade-offs. This rule suggests CancellationToken should be the last parameter, but the current design may be intentional for API readability.",
        "occurrences": 2,
        "confidenceScore": 0.6,
        "examples": [
          {
            "filePath": "D:\\Projects\\MyApp\\Services\\RetryPolicy.cs",
            "line": 34,
            "snippet": "public async Task<T> ExecuteAsync<T>(\n    Func<CancellationToken, Task<T>> action,\n    string operationName,\n    CancellationToken cancellationToken, // ← Not last parameter\n    Func<Exception, bool>? retryPredicate = null\n)"
          }
        ],
        "suggestedActions": [
          "Review the API design and determine if CancellationToken placement is intentional",
          "If intentional, document the decision and suppress with justification",
          "If not intentional, consider refactoring to follow the convention"
        ]
      }
    ]
  }
}
```

## C# Client API (для прямого использования в коде)

```csharp
// Использование через DiagnosticService
public class DiagnosticService : IDiagnosticService
{
    public async Task<DiagnosticAnalysisResult> AnalyzeAsync(
        string solutionPath,
        DiagnosticFilterOptions filterOptions,
        CancellationToken cancellationToken = default
    );
}

// Опции фильтрации
public class DiagnosticFilterOptions
{
    // Существующие
    public DiagnosticSeverity? SeverityFilter { get; set; }
    public IEnumerable<string>? DiagnosticIds { get; set; }
    public IEnumerable<string>? ProjectNames { get; set; }
    public IEnumerable<string>? FilePatterns { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }

    // НОВЫЕ: Semantic enrichment
    public bool EnrichWithSemantics { get; set; }
    public bool GroupBySimilarity { get; set; }
    public double SimilarityThreshold { get; set; } = 0.85;

    // НОВЫЕ: EditorConfig generation
    public bool GenerateEditorConfigRecommendations { get; set; }
    public EditorConfigFormat EditorConfigFormat { get; set; } = EditorConfigFormat.Detailed;
}

// Пример использования
var diagnosticService = serviceProvider.GetRequiredService<IDiagnosticService>();

var result = await diagnosticService.AnalyzeAsync(
    solutionPath: @"D:\Projects\MyApp.sln",
    filterOptions: new DiagnosticFilterOptions {
        SeverityFilter = DiagnosticSeverity.Info,
        EnrichWithSemantics = true,
        GenerateEditorConfigRecommendations = true,
        Take = 100
    }
);

// Работа с результатом
if (result.SemanticEnrichment != null) {
    Console.WriteLine($"Found {result.SemanticEnrichment.Clusters.Count} clusters");

    foreach (var cluster in result.SemanticEnrichment.Clusters) {
        Console.WriteLine($"{cluster.DiagnosticId}: {cluster.Pattern}");
        Console.WriteLine($"  Category: {cluster.Category}");
        Console.WriteLine($"  Confidence: {cluster.ConfidenceScore:P0}");
        Console.WriteLine($"  Recommended: {cluster.RecommendedSeverity}");
    }
}

if (result.EditorConfigRecommendations != null) {
    // Сохранить .editorconfig
    await File.WriteAllTextAsync(
        ".editorconfig",
        result.EditorConfigRecommendations.Content
    );

    // Показать manual review cases
    foreach (var reviewCase in result.EditorConfigRecommendations.RequiresManualReview) {
        Console.WriteLine($"⚠️ {reviewCase.DiagnosticId}: {reviewCase.Reason}");
    }
}
```

## Версионирование API

**Current version:** `v1.0` (Phase 1 + Phase 2)

**Breaking changes policy:**
- Новые опциональные поля в response - NOT breaking
- Новые опциональные параметры в request - NOT breaking
- Изменение существующих полей - BREAKING (increment major version)

**Обратная совместимость:**
- Все новые поля опциональны
- Старые клиенты продолжают работать без изменений
- Новые поля возвращаются только если запрошены

## Rate Limiting

**Recommendations:**
- Semantic enrichment CPU-intensive - max 1 concurrent request per user
- Для больших solution (>500 files) рекомендуется pagination
- EditorConfig generation lightweight - no special limits

## Error Handling

**Error codes:**
```json
{
  "error": {
    "code": string,
    "message": string,
    "details": object?
  }
}
```

**Error codes:**
- `SOLUTION_NOT_FOUND` - Solution file не найден
- `SOLUTION_LOAD_FAILED` - Не удалось загрузить solution
- `SEMANTIC_SERVICE_UNAVAILABLE` - Semantic search service недоступен
- `CLUSTERING_FAILED` - Ошибка кластеризации
- `EDITORCONFIG_GENERATION_FAILED` - Ошибка генерации .editorconfig
- `INVALID_PARAMETERS` - Невалидные параметры

**Graceful degradation:**
- Если semantic service недоступен - вернуть результаты без enrichment
- Если clustering failed - вернуть heuristic-based enrichment
- Если EditorConfig generation failed - вернуть enrichment без .editorconfig

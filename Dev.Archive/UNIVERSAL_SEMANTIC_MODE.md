# Universal Semantic Mode - Architecture Design

**Дата:** 2025-11-18
**Статус:** 🔬 Research & Design
**Версия:** 2.0.0 (Revolutionary Approach)

---

## 🎯 Концепция

**Революционная идея:** Добавить semantic mode для ВСЕХ 52 инструментов, а не только для "semantic tools".

### Ключевые принципы:

1. **Динамическая активация** - semantic mode включается автоматически если:
   - ✅ Доступна локальная embedding модель (Ollama/TEI)
   - ✅ Доступен Overlord с EmbeddingService

2. **Универсальность** - каждый инструмент получает semantic capabilities:
   - `view_definition` + semantic search по похожим определениям
   - `find_references` + semantic search по похожим usage patterns
   - `modify_code` + semantic suggestions из похожих изменений
   - И так далее для всех 52 tools

3. **Неинвазивность** - semantic mode НЕ изменяет основную функциональность:
   - Инструмент работает как обычно
   - Semantic результаты добавляются как дополнительная информация
   - Можно отключить через конфигурацию

4. **Интеллектуальный routing** - автоматический выбор:
   - LOCAL mode (быстро) + semantic enrichment (опционально)
   - OVERLORD mode (мощно) + cross-project semantic (всегда)

---

## 📊 Текущая архитектура (Phase 1-11)

### Проблема 1: Hardcoded Classification

**Файл:** `ToolRouter.cs` (строки 15-37)

```csharp
// Semantic tools - всегда требуют Overlord
private static readonly HashSet<string> SemanticTools = new()
{
    "find_duplicates",
    "semantic_search",
    "semantic_diff",
    "detect_code_clones",
    "reindex_changed_files"  // ← ВСЕГО 5 ИНСТРУМЕНТОВ
};

// Hybrid tools - решение зависит от параметров
private static readonly HashSet<string> HybridTools = new()
{
    "pattern_search",
    "analyze_complexity",
    "trace_execution",
    "trace_backwards",
    "export_call_graph"  // ← ЕЩЁ 5 ИНСТРУМЕНТОВ
};

// Все остальные 42 инструмента → ТОЛЬКО LOCAL, БЕЗ SEMANTIC
```

**Проблемы:**
- ❌ Каждый новый инструмент требует ручного добавления в HashSet
- ❌ Semantic capabilities доступны только для 10 из 52 инструментов
- ❌ Невозможно динамически изменить routing без изменения кода
- ❌ Нет универсального подхода к semantic enrichment

### Проблема 2: Отсутствие Middleware

**MCP SDK не предоставляет:**
- ❌ `IToolMiddleware` interface
- ❌ Extension point для pre-execution hooks
- ❌ Global interceptor механизм

**Текущий pipeline:**
```
MCP Request → SDK Reflection → DI Injection → Direct Tool Execution
                                                    ↓
                                            (Нет точки перехвата)
```

**Что нужно:**
```
MCP Request → SDK Reflection → DI Injection → [INTERCEPTOR] → Tool Execution
                                                    ↑
                                        (Применяем routing + semantic)
```

---

## 🚀 Новая архитектура: Universal Semantic Mode

### Архитектурные компоненты:

#### 1. ISemanticModeProvider - Определение доступности semantic mode

```csharp
public interface ISemanticModeProvider
{
    /// <summary>
    /// Проверяет доступность semantic mode (локально или через Overlord)
    /// </summary>
    Task<SemanticModeAvailability> CheckAvailabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Получает embedding для текста
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Выполняет semantic search в локальной или remote векторной базе
    /// </summary>
    Task<IEnumerable<SemanticMatch>> SearchAsync(
        float[] queryVector,
        int topK = 10,
        double threshold = 0.7,
        CancellationToken ct = default);
}

public sealed class SemanticModeAvailability
{
    public bool IsAvailable { get; init; }
    public SemanticModeSource Source { get; init; }  // Local, Overlord, Both, None
    public string? ModelName { get; init; }
    public int VectorDimension { get; init; }
}

public enum SemanticModeSource
{
    None,       // Semantic mode недоступен
    Local,      // Локальная модель (Ollama/TEI)
    Overlord,   // Overlord EmbeddingService
    Both        // Оба доступны - выбор по скорости/качеству
}
```

**Реализация:**
```csharp
public sealed class SemanticModeProvider : ISemanticModeProvider
{
    private readonly IEmbeddingService? _localEmbedding;
    private readonly IServerBridgeService? _overlordBridge;
    private readonly ILogger<SemanticModeProvider> _logger;
    private SemanticModeAvailability? _cachedAvailability;
    private DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public async Task<SemanticModeAvailability> CheckAvailabilityAsync(CancellationToken ct)
    {
        // Cache для избежания повторных проверок
        if (_cachedAvailability != null && DateTime.UtcNow - _lastCheck < CacheExpiry)
        {
            return _cachedAvailability;
        }

        bool localAvailable = false;
        bool overlordAvailable = false;

        // Проверка локальной модели
        if (_localEmbedding != null)
        {
            localAvailable = await _localEmbedding.IsAvailableAsync(ct);
        }

        // Проверка Overlord
        if (_overlordBridge != null)
        {
            overlordAvailable = await _overlordBridge.IsServerAvailableAsync(ct);
        }

        var source = (localAvailable, overlordAvailable) switch
        {
            (true, true) => SemanticModeSource.Both,
            (true, false) => SemanticModeSource.Local,
            (false, true) => SemanticModeSource.Overlord,
            _ => SemanticModeSource.None
        };

        _cachedAvailability = new SemanticModeAvailability
        {
            IsAvailable = source != SemanticModeSource.None,
            Source = source,
            ModelName = localAvailable ? "nomic-embed-text" : "overlord",
            VectorDimension = 768
        };

        _lastCheck = DateTime.UtcNow;
        return _cachedAvailability;
    }

    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var availability = await CheckAvailabilityAsync(ct);

        if (!availability.IsAvailable)
        {
            return null;
        }

        // Предпочитаем локальную модель (быстрее)
        if (availability.Source == SemanticModeSource.Local || availability.Source == SemanticModeSource.Both)
        {
            return await _localEmbedding!.GetEmbeddingAsync(text, ct);
        }

        // Fallback на Overlord
        if (availability.Source == SemanticModeSource.Overlord)
        {
            // Proxy call через ServerBridge
            var requestJson = JsonSerializer.Serialize(new { text });
            var resultJson = await _overlordBridge!.CallMcpProxyAsync(
                "get_embedding",
                requestJson,
                null,
                ct);

            var result = JsonSerializer.Deserialize<float[]>(resultJson);
            return result;
        }

        return null;
    }

    public async Task<IEnumerable<SemanticMatch>> SearchAsync(
        float[] queryVector,
        int topK,
        double threshold,
        CancellationToken ct)
    {
        var availability = await CheckAvailabilityAsync(ct);

        if (!availability.IsAvailable)
        {
            return Enumerable.Empty<SemanticMatch>();
        }

        // Всегда используем Overlord для search (cross-project)
        if (availability.Source == SemanticModeSource.Overlord || availability.Source == SemanticModeSource.Both)
        {
            var requestJson = JsonSerializer.Serialize(new
            {
                targetVector = queryVector,
                threshold,
                limit = topK,
                scope = "all_projects"
            });

            var resultJson = await _overlordBridge!.CallMcpProxyAsync(
                "find_duplicates",
                requestJson,
                null,
                ct);

            var matches = JsonSerializer.Deserialize<IEnumerable<SemanticMatch>>(resultJson);
            return matches ?? Enumerable.Empty<SemanticMatch>();
        }

        // Локальный search (только current project)
        // TODO: реализовать локальный векторный поиск
        return Enumerable.Empty<SemanticMatch>();
    }
}

public sealed class SemanticMatch
{
    public string Project { get; init; } = "";
    public string FilePath { get; init; } = "";
    public double Similarity { get; init; }
    public string? Snippet { get; init; }
    public List<string> Symbols { get; init; } = new();
}
```

---

#### 2. IToolEnricher - Semantic Enrichment для инструментов

```csharp
public interface IToolEnricher
{
    /// <summary>
    /// Обогащает результат инструмента semantic данными
    /// </summary>
    Task<EnrichedToolResult> EnrichAsync(
        string toolName,
        object originalResult,
        Dictionary<string, object>? toolArguments = null,
        CancellationToken ct = default);
}

public sealed class EnrichedToolResult
{
    /// <summary>
    /// Оригинальный результат инструмента
    /// </summary>
    public object OriginalResult { get; init; } = null!;

    /// <summary>
    /// Semantic enrichment данные
    /// </summary>
    public SemanticEnrichment? Semantic { get; init; }

    /// <summary>
    /// Метаданные о процессе enrichment
    /// </summary>
    public EnrichmentMetadata Metadata { get; init; } = new();
}

public sealed class SemanticEnrichment
{
    /// <summary>
    /// Похожие определения/фрагменты кода
    /// </summary>
    public List<SemanticMatch> SimilarItems { get; init; } = new();

    /// <summary>
    /// Рекомендации на основе semantic analysis
    /// </summary>
    public List<string> Suggestions { get; init; } = new();

    /// <summary>
    /// Статистика semantic поиска
    /// </summary>
    public SemanticStats Stats { get; init; } = new();
}

public sealed class EnrichmentMetadata
{
    public bool SemanticModeActive { get; init; }
    public SemanticModeSource Source { get; init; }
    public TimeSpan EnrichmentDuration { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class SemanticStats
{
    public int TotalMatches { get; init; }
    public double AverageSimilarity { get; init; }
    public int CrossProjectMatches { get; init; }
}
```

**Реализация:**
```csharp
public sealed class ToolEnricher : IToolEnricher
{
    private readonly ISemanticModeProvider _semanticProvider;
    private readonly ILogger<ToolEnricher> _logger;

    // Стратегии enrichment для разных типов инструментов
    private readonly Dictionary<string, Func<object, Dictionary<string, object>?, Task<SemanticEnrichment?>>> _enrichmentStrategies;

    public ToolEnricher(
        ISemanticModeProvider semanticProvider,
        ILogger<ToolEnricher> logger)
    {
        _semanticProvider = semanticProvider;
        _logger = logger;

        // Регистрируем стратегии enrichment
        _enrichmentStrategies = new()
        {
            ["view_definition"] = EnrichViewDefinition,
            ["find_references"] = EnrichFindReferences,
            ["modify_code"] = EnrichModifyCode,
            ["get_members"] = EnrichGetMembers,
            ["analyze_complexity"] = EnrichAnalyzeComplexity,
            // ... для всех 52 инструментов
        };
    }

    public async Task<EnrichedToolResult> EnrichAsync(
        string toolName,
        object originalResult,
        Dictionary<string, object>? toolArguments,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var availability = await _semanticProvider.CheckAvailabilityAsync(ct);

        // Если semantic mode недоступен - возвращаем original result
        if (!availability.IsAvailable)
        {
            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    SemanticModeActive = false,
                    Source = SemanticModeSource.None,
                    EnrichmentDuration = TimeSpan.Zero
                }
            };
        }

        // Применяем enrichment strategy для этого инструмента
        SemanticEnrichment? semantic = null;

        if (_enrichmentStrategies.TryGetValue(toolName, out var strategy))
        {
            try
            {
                semantic = await strategy(originalResult, toolArguments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich {ToolName}", toolName);
            }
        }

        var duration = DateTime.UtcNow - startTime;

        return new EnrichedToolResult
        {
            OriginalResult = originalResult,
            Semantic = semantic,
            Metadata = new EnrichmentMetadata
            {
                SemanticModeActive = semantic != null,
                Source = availability.Source,
                EnrichmentDuration = duration,
                ErrorMessage = semantic == null ? "No enrichment strategy available" : null
            }
        };
    }

    // === ENRICHMENT STRATEGIES ===

    private async Task<SemanticEnrichment?> EnrichViewDefinition(
        object originalResult,
        Dictionary<string, object>? args)
    {
        // Извлекаем код из result
        var codeText = ExtractCodeFromResult(originalResult);
        if (string.IsNullOrEmpty(codeText))
        {
            return null;
        }

        // Векторизуем код
        var embedding = await _semanticProvider.GetEmbeddingAsync(codeText);
        if (embedding == null)
        {
            return null;
        }

        // Ищем похожие определения
        var similarItems = await _semanticProvider.SearchAsync(
            embedding,
            topK: 5,
            threshold: 0.7);

        return new SemanticEnrichment
        {
            SimilarItems = similarItems.ToList(),
            Suggestions = GenerateSuggestionsFromMatches(similarItems),
            Stats = new SemanticStats
            {
                TotalMatches = similarItems.Count(),
                AverageSimilarity = similarItems.Any() ? similarItems.Average(m => m.Similarity) : 0,
                CrossProjectMatches = similarItems.Count(m => m.Project != GetCurrentProject())
            }
        };
    }

    private async Task<SemanticEnrichment?> EnrichFindReferences(
        object originalResult,
        Dictionary<string, object>? args)
    {
        // Анализируем паттерны usage
        var references = ExtractReferencesFromResult(originalResult);
        if (!references.Any())
        {
            return null;
        }

        // Объединяем код всех references
        var combinedCode = string.Join("\n", references.Select(r => r.Code));

        // Векторизуем combined usage pattern
        var embedding = await _semanticProvider.GetEmbeddingAsync(combinedCode);
        if (embedding == null)
        {
            return null;
        }

        // Ищем похожие usage patterns в других проектах
        var similarUsages = await _semanticProvider.SearchAsync(
            embedding,
            topK: 5,
            threshold: 0.65);

        return new SemanticEnrichment
        {
            SimilarItems = similarUsages.ToList(),
            Suggestions = new List<string>
            {
                $"Found {similarUsages.Count()} similar usage patterns across projects",
                "Consider refactoring to shared utility class if patterns are identical"
            },
            Stats = new SemanticStats
            {
                TotalMatches = similarUsages.Count(),
                AverageSimilarity = similarUsages.Average(m => m.Similarity),
                CrossProjectMatches = similarUsages.Count(m => m.Project != GetCurrentProject())
            }
        };
    }

    private async Task<SemanticEnrichment?> EnrichModifyCode(
        object originalResult,
        Dictionary<string, object>? args)
    {
        // После модификации кода, ищем похожие изменения в истории
        if (!args.TryGetValue("newCode", out var newCodeObj))
        {
            return null;
        }

        var newCode = newCodeObj.ToString();
        var embedding = await _semanticProvider.GetEmbeddingAsync(newCode!);

        if (embedding == null)
        {
            return null;
        }

        // Ищем похожие code modifications в других проектах/коммитах
        var similarModifications = await _semanticProvider.SearchAsync(
            embedding,
            topK: 3,
            threshold: 0.75);

        var suggestions = new List<string>();

        if (similarModifications.Any())
        {
            suggestions.Add("Similar modifications found in:");
            foreach (var match in similarModifications.Take(3))
            {
                suggestions.Add($"  - {match.Project}/{match.FilePath} (similarity: {match.Similarity:P0})");
            }
        }

        return new SemanticEnrichment
        {
            SimilarItems = similarModifications.ToList(),
            Suggestions = suggestions,
            Stats = new SemanticStats
            {
                TotalMatches = similarModifications.Count(),
                AverageSimilarity = similarModifications.Average(m => m.Similarity),
                CrossProjectMatches = similarModifications.Count(m => m.Project != GetCurrentProject())
            }
        };
    }

    private async Task<SemanticEnrichment?> EnrichGetMembers(
        object originalResult,
        Dictionary<string, object>? args)
    {
        // После получения members класса, ищем классы с похожей структурой
        var members = ExtractMembersFromResult(originalResult);
        if (!members.Any())
        {
            return null;
        }

        // Создаём "signature" класса из списка members
        var classSignature = string.Join("\n", members.Select(m => m.Signature));

        var embedding = await _semanticProvider.GetEmbeddingAsync(classSignature);
        if (embedding == null)
        {
            return null;
        }

        // Ищем классы с похожей структурой
        var similarClasses = await _semanticProvider.SearchAsync(
            embedding,
            topK: 5,
            threshold: 0.7);

        return new SemanticEnrichment
        {
            SimilarItems = similarClasses.ToList(),
            Suggestions = new List<string>
            {
                $"Found {similarClasses.Count()} classes with similar structure",
                "Consider creating abstract base class if patterns are common"
            },
            Stats = new SemanticStats
            {
                TotalMatches = similarClasses.Count(),
                AverageSimilarity = similarClasses.Average(m => m.Similarity),
                CrossProjectMatches = similarClasses.Count(m => m.Project != GetCurrentProject())
            }
        };
    }

    private async Task<SemanticEnrichment?> EnrichAnalyzeComplexity(
        object originalResult,
        Dictionary<string, object>? args)
    {
        // После анализа сложности, ищем методы с похожей сложностью которые были упрощены
        var complexityScore = ExtractComplexityScore(originalResult);
        if (complexityScore < 10) // Только для сложных методов
        {
            return null;
        }

        // Ищем в истории методы с высокой сложностью которые были refactored
        // (требует интеграцию с Git history + semantic search)

        return new SemanticEnrichment
        {
            SimilarItems = new List<SemanticMatch>(),
            Suggestions = new List<string>
            {
                $"Complexity score: {complexityScore} - consider refactoring",
                "Check similar complex methods that were successfully simplified"
            },
            Stats = new SemanticStats()
        };
    }

    // === HELPER METHODS ===

    private string? ExtractCodeFromResult(object result)
    {
        // Извлекаем код из различных форматов result
        // (JSON, MarkdownCodeBlock, plain text, etc.)
        return result.ToString();
    }

    private IEnumerable<Reference> ExtractReferencesFromResult(object result)
    {
        // Парсит список references из result
        return Enumerable.Empty<Reference>();
    }

    private IEnumerable<MemberInfo> ExtractMembersFromResult(object result)
    {
        // Парсит список members из result
        return Enumerable.Empty<MemberInfo>();
    }

    private int ExtractComplexityScore(object result)
    {
        // Извлекает complexity score из result
        return 0;
    }

    private string GetCurrentProject()
    {
        // Получает название текущего проекта
        return "CurrentProject";
    }

    private List<string> GenerateSuggestionsFromMatches(IEnumerable<SemanticMatch> matches)
    {
        var suggestions = new List<string>();

        if (matches.Any())
        {
            suggestions.Add($"Found {matches.Count()} similar code fragments:");
            foreach (var match in matches.Take(3))
            {
                suggestions.Add($"  - {match.Project}/{match.FilePath} ({match.Similarity:P0} similar)");
            }

            var crossProject = matches.Count(m => m.Project != GetCurrentProject());
            if (crossProject > 0)
            {
                suggestions.Add($"Consider reusing code from {crossProject} cross-project matches");
            }
        }

        return suggestions;
    }

    private sealed class Reference
    {
        public string Code { get; init; } = "";
        public string FilePath { get; init; } = "";
    }

    private sealed class MemberInfo
    {
        public string Signature { get; init; } = "";
        public string Name { get; init; } = "";
    }
}
```

---

#### 3. McpToolInterceptor - Global Перехватчик Tool Calls

Поскольку MCP SDK не предоставляет middleware, используем **Decorator Pattern** через DI:

```csharp
public interface IMcpToolExecutor
{
    Task<object> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct = default);
}

/// <summary>
/// Interceptor который оборачивает actual tool execution
/// Применяет routing + semantic enrichment ПЕРЕД возвратом результата
/// </summary>
public sealed class McpToolInterceptor : IMcpToolExecutor
{
    private readonly IToolRouter _router;
    private readonly IToolEnricher _enricher;
    private readonly IServerBridgeService? _serverBridge;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpToolInterceptor> _logger;

    public McpToolInterceptor(
        IToolRouter router,
        IToolEnricher enricher,
        IServiceProvider serviceProvider,
        ILogger<McpToolInterceptor> logger,
        IServerBridgeService? serverBridge = null)
    {
        _router = router;
        _enricher = enricher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serverBridge = serverBridge;
    }

    public async Task<object> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct)
    {
        _logger.LogDebug("Intercepting tool call: {ToolName}", toolName);

        // 1. Determine routing
        var decision = _router.DetermineRouting(toolName, arguments);

        _logger.LogInformation(
            "Tool {ToolName} routed to: {Decision}",
            toolName,
            decision);

        object result;

        try
        {
            // 2. Execute based on routing decision
            result = decision switch
            {
                ToolRoutingDecision.Local =>
                    await ExecuteLocalAsync(toolName, arguments, ct),

                ToolRoutingDecision.Overlord =>
                    await ExecuteOverlordAsync(toolName, arguments, ct),

                ToolRoutingDecision.OverlordWithFallback =>
                    await ExecuteWithFallbackAsync(toolName, arguments, ct),

                _ => throw new NotSupportedException($"Unknown routing decision: {decision}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            throw;
        }

        // 3. Enrich with semantic data (если доступен)
        var enrichedResult = await _enricher.EnrichAsync(toolName, result, arguments, ct);

        _logger.LogInformation(
            "Tool {ToolName} enriched: semantic={SemanticActive}, duration={Duration}ms",
            toolName,
            enrichedResult.Metadata.SemanticModeActive,
            enrichedResult.Metadata.EnrichmentDuration.TotalMilliseconds);

        // 4. Возвращаем enriched result
        return enrichedResult;
    }

    private async Task<object> ExecuteLocalAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct)
    {
        // Выполняем инструмент локально через reflection
        // (аналогично тому как MCP SDK это делает)

        var toolMethod = FindToolMethod(toolName);
        if (toolMethod == null)
        {
            throw new McpException($"Tool not found: {toolName}");
        }

        // Инжектируем зависимости из DI контейнера
        var parameters = ResolveParameters(toolMethod, arguments);

        // Вызываем метод
        var result = toolMethod.Invoke(null, parameters);

        // Если это Task - await
        if (result is Task task)
        {
            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task) ?? task;
        }

        return result ?? new object();
    }

    private async Task<object> ExecuteOverlordAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct)
    {
        if (_serverBridge == null)
        {
            throw new McpException("Overlord not available but required for this tool");
        }

        var argsJson = JsonSerializer.Serialize(arguments);
        var resultJson = await _serverBridge.CallMcpProxyAsync(toolName, argsJson, null, ct);

        // Deserialize result
        var result = JsonSerializer.Deserialize<object>(resultJson);
        return result ?? new object();
    }

    private async Task<object> ExecuteWithFallbackAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken ct)
    {
        try
        {
            // Пробуем Overlord
            return await ExecuteOverlordAsync(toolName, arguments, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Overlord execution failed for {ToolName}, falling back to local",
                toolName);

            // Fallback на Local
            return await ExecuteLocalAsync(toolName, arguments, ct);
        }
    }

    private MethodInfo? FindToolMethod(string toolName)
    {
        // Находит метод с [McpServerTool(Name = toolName)]
        var assemblies = new[]
        {
            Assembly.Load("UltrasharpTools.Tools")
        };

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                    if (attr?.Name == toolName)
                    {
                        return method;
                    }
                }
            }
        }

        return null;
    }

    private object?[] ResolveParameters(MethodInfo method, Dictionary<string, object> arguments)
    {
        var parameters = method.GetParameters();
        var resolvedParams = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            // Проверяем CancellationToken
            if (param.ParameterType == typeof(CancellationToken))
            {
                resolvedParams[i] = CancellationToken.None;
                continue;
            }

            // Пробуем получить из DI
            var service = _serviceProvider.GetService(param.ParameterType);
            if (service != null)
            {
                resolvedParams[i] = service;
                continue;
            }

            // Пробуем получить из arguments
            if (arguments.TryGetValue(param.Name!, out var value))
            {
                resolvedParams[i] = Convert.ChangeType(value, param.ParameterType);
                continue;
            }

            // Default value
            resolvedParams[i] = param.HasDefaultValue ? param.DefaultValue : null;
        }

        return resolvedParams;
    }
}
```

---

## 🔧 Integration в Program.cs

```csharp
// В Droid/Program.cs

// Регистрируем SemanticModeProvider
builder.Services.AddSingleton<ISemanticModeProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SemanticModeProvider>>();
    var localEmbedding = sp.GetService<IEmbeddingService>();  // Может быть null
    var serverBridge = sp.GetService<IServerBridgeService>();  // Может быть null
    return new SemanticModeProvider(localEmbedding, serverBridge, logger);
});

// Регистрируем ToolEnricher
builder.Services.AddSingleton<IToolEnricher, ToolEnricher>();

// Регистрируем McpToolInterceptor
builder.Services.AddSingleton<IMcpToolExecutor, McpToolInterceptor>();

Console.WriteLine("Universal Semantic Mode enabled:");
Console.WriteLine("  - Auto-detection: Local embedding + Overlord");
Console.WriteLine("  - Enrichment: ALL 52 tools");
Console.WriteLine("  - Routing: Intelligent LOCAL/OVERLORD");
```

---

## 📊 Примеры Use Cases

### Use Case 1: view_definition с Semantic Enrichment

**Request:**
```json
{
  "tool": "view_definition",
  "arguments": {
    "fqn": "MyApp.Services.UserService.GetUser"
  }
}
```

**Traditional Response (без semantic):**
```json
{
  "code": "public async Task<User> GetUser(int id) { ... }",
  "filePath": "Services/UserService.cs",
  "lineNumber": 42
}
```

**Enhanced Response (с semantic):**
```json
{
  "originalResult": {
    "code": "public async Task<User> GetUser(int id) { ... }",
    "filePath": "Services/UserService.cs",
    "lineNumber": 42
  },
  "semantic": {
    "similarItems": [
      {
        "project": "AnotherApp",
        "filePath": "Data/UserRepository.cs",
        "similarity": 0.89,
        "snippet": "public async Task<UserEntity> FetchUser(int userId) { ... }"
      },
      {
        "project": "MyApp.Admin",
        "filePath": "Controllers/UserController.cs",
        "similarity": 0.76,
        "snippet": "public async Task<UserDto> GetUserById(int id) { ... }"
      }
    ],
    "suggestions": [
      "Found 2 similar code fragments:",
      "  - AnotherApp/Data/UserRepository.cs (89% similar)",
      "  - MyApp.Admin/Controllers/UserController.cs (76% similar)",
      "Consider reusing code from 1 cross-project matches"
    ],
    "stats": {
      "totalMatches": 2,
      "averageSimilarity": 0.825,
      "crossProjectMatches": 1
    }
  },
  "metadata": {
    "semanticModeActive": true,
    "source": "Both",
    "enrichmentDuration": "00:00:00.234"
  }
}
```

### Use Case 2: modify_code с Historical Suggestions

**Request:**
```json
{
  "tool": "modify_code",
  "arguments": {
    "fqn": "MyApp.Services.PaymentService.ProcessPayment",
    "newCode": "public async Task ProcessPayment(Payment payment) {\n  await _validator.ValidateAsync(payment);\n  await _gateway.ChargeAsync(payment);\n}"
  }
}
```

**Enhanced Response:**
```json
{
  "originalResult": {
    "success": true,
    "message": "Code modified successfully"
  },
  "semantic": {
    "similarItems": [
      {
        "project": "LegacyApp",
        "filePath": "Billing/PaymentProcessor.cs",
        "similarity": 0.92,
        "snippet": "// Similar payment processing logic with additional error handling"
      }
    ],
    "suggestions": [
      "Similar modifications found in:",
      "  - LegacyApp/Billing/PaymentProcessor.cs (similarity: 92%)",
      "Consider adding error handling based on similar code"
    ],
    "stats": {
      "totalMatches": 1,
      "averageSimilarity": 0.92,
      "crossProjectMatches": 1
    }
  },
  "metadata": {
    "semanticModeActive": true,
    "source": "Overlord",
    "enrichmentDuration": "00:00:00.187"
  }
}
```

### Use Case 3: get_members с Structure Similarity

**Request:**
```json
{
  "tool": "get_members",
  "arguments": {
    "fqn": "MyApp.Models.User"
  }
}
```

**Enhanced Response:**
```json
{
  "originalResult": {
    "members": [
      "int Id { get; set; }",
      "string Email { get; set; }",
      "string PasswordHash { get; set; }",
      "DateTime CreatedAt { get; set; }"
    ]
  },
  "semantic": {
    "similarItems": [
      {
        "project": "IdentityService",
        "filePath": "Entities/UserEntity.cs",
        "similarity": 0.94,
        "snippet": "// Class with identical structure"
      },
      {
        "project": "AdminPanel",
        "filePath": "Models/AdminUser.cs",
        "similarity": 0.81,
        "snippet": "// Class with similar properties + Role"
      }
    ],
    "suggestions": [
      "Found 2 classes with similar structure",
      "Consider creating abstract base class if patterns are common"
    ],
    "stats": {
      "totalMatches": 2,
      "averageSimilarity": 0.875,
      "crossProjectMatches": 2
    }
  },
  "metadata": {
    "semanticModeActive": true,
    "source": "Both",
    "enrichmentDuration": "00:00:00.156"
  }
}
```

---

## 🎯 Конфигурация Universal Semantic Mode

**Файл:** `.ultrasharp/semantic-mode-config.json`

```json
{
  "enableUniversalSemanticMode": true,

  "autoDetection": {
    "checkLocalEmbedding": true,
    "checkOverlordEmbedding": true,
    "cacheAvailabilitySeconds": 300
  },

  "enrichment": {
    "enabledForAllTools": true,
    "topKMatches": 5,
    "similarityThreshold": 0.7,
    "maxEnrichmentDurationMs": 2000,
    "fallbackToOriginalOnTimeout": true
  },

  "toolSpecificSettings": {
    "view_definition": {
      "enableSemantic": true,
      "topK": 5,
      "threshold": 0.75
    },
    "find_references": {
      "enableSemantic": true,
      "topK": 3,
      "threshold": 0.65,
      "includeUsagePatterns": true
    },
    "modify_code": {
      "enableSemantic": true,
      "topK": 3,
      "threshold": 0.8,
      "includeHistoricalChanges": true
    },
    "get_members": {
      "enableSemantic": true,
      "topK": 5,
      "threshold": 0.7,
      "compareStructureOnly": true
    },
    "analyze_complexity": {
      "enableSemantic": true,
      "topK": 3,
      "threshold": 0.75,
      "includeRefactoringExamples": true
    }
  },

  "performance": {
    "parallelEmbedding": true,
    "cacheEmbeddings": true,
    "cacheTtlSeconds": 3600
  },

  "routing": {
    "preferLocalForSpeed": true,
    "preferOverlordForCrossProject": true,
    "fallbackToLocalOnError": true
  }
}
```

---

## 🚀 Benefits

### 1. Универсальность
- ✅ Semantic capabilities для ВСЕХ 52 инструментов
- ✅ Не нужно вручную добавлять каждый новый инструмент в HashSet
- ✅ Динамическая конфигурация через JSON

### 2. Интеллектуальность
- ✅ Автоматическое определение доступности (Local/Overlord/Both)
- ✅ Semantic enrichment адаптируется под тип инструмента
- ✅ Рекомендации на основе cross-project анализа

### 3. Performance
- ✅ Кэширование availability checks
- ✅ Параллельная векторизация
- ✅ Timeout protection для enrichment
- ✅ Fallback на original result при ошибках

### 4. Неинвазивность
- ✅ Оригинальная функциональность инструментов НЕ изменяется
- ✅ Semantic данные добавляются как дополнительная информация
- ✅ Можно полностью отключить через конфигурацию

### 5. Extensibility
- ✅ Легко добавить новые enrichment strategies
- ✅ Pluggable architecture для разных типов semantic providers
- ✅ Поддержка custom embedding models

---

## 📋 Implementation Roadmap

### Phase 12.1: Core Infrastructure
- [ ] Создать ISemanticModeProvider + implementation
- [ ] Создать IToolEnricher + implementation
- [ ] Создать McpToolInterceptor
- [ ] Зарегистрировать в DI контейнере

### Phase 12.2: Enrichment Strategies
- [ ] Реализовать enrichment для top 10 most-used tools
- [ ] Протестировать semantic enrichment качество
- [ ] Оптимизировать performance

### Phase 12.3: Configuration & Testing
- [ ] Создать semantic-mode-config.json schema
- [ ] Реализовать ConfigurationLoader
- [ ] Unit tests для всех компонентов
- [ ] Integration tests для enrichment scenarios

### Phase 12.4: Documentation & Rollout
- [ ] User guide для Universal Semantic Mode
- [ ] API reference для ISemanticModeProvider
- [ ] Performance benchmarks
- [ ] Production deployment

---

**Дата:** 2025-11-18
**Версия:** 2.0.0
**Статус:** 🔬 **Ready for Implementation**

---

Это революционный подход к semantic capabilities в MCP инструментах! 🚀

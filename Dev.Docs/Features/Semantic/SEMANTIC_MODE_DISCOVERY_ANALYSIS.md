# Semantic Mode Discovery Analysis

## Проблема: Как AI клиент узнаёт о поддержке семантических запросов?

### TL;DR
**Ответ:** AI клиент узнаёт о semantic mode через **runtime detection**, а НЕ через MCP capabilities negotiation.

---

## Текущая Реализация

### 1. При Запуске Droid (Startup)

```
Droid Start
    ↓
SemanticModeProvider.CheckAvailabilityAsync()
    ↓
┌─────────────────────────────────────┐
│ CheckLocalAvailabilityAsync()       │
│ - Ping local embedding service      │
│ - Timeout: 3s (configurable)        │
│ - Test embedding: "test" → vector?  │
└─────────────────────────────────────┘
    ↓
┌─────────────────────────────────────┐
│ CheckOverlordAvailabilityAsync()    │
│ - Ping Overlord server              │
│ - Timeout: 5s (configurable)        │
│ - IsServerAvailableAsync()          │
└─────────────────────────────────────┘
    ↓
Result: SemanticModeAvailability {
    IsAvailable: bool
    Source: None | Local | Overlord | Both
    ModelName: "nomic-embed-text"
    VectorDimension: 768
}
```

**Кэширование:** 60 секунд (configurable: `CacheValiditySeconds`)

---

### 2. При Вызове Tool (Runtime)

```
AI → MCP Request: view_definition(fqn="MyClass")
    ↓
ToolRouter.RouteToolCallAsync()
    ↓
Execute Base Tool
    ↓
ToolEnricher.EnrichAsync()
    ↓
SemanticModeProvider.CheckAvailabilityAsync() ← Проверка в КАЖДОМ вызове!
    ↓
If Available:
    - Get related symbols
    - Semantic search
    - Add to result.semantic
Else:
    - Return original result
```

**Важно:** Enrichment происходит **после** выполнения основного tool, **динамически**.

---

## ❌ Проблема: AI Не Знает Заранее

### Текущая Ситуация

1. **AI делает запрос**: `view_definition(fqn="MyClass")`
2. **Droid проверяет** semantic mode availability
3. **Если доступно** - обогащает результат
4. **Если нет** - возвращает базовый результат

**Проблема:** AI узнаёт о semantic mode **только после первого запроса**.

### Последствия

```
# Первый запрос AI
AI: view_definition(fqn="MyClass")
Droid: { original: {...}, semantic: {...} }  ← Ого! Есть semantic!

# AI не знал заранее, что semantic доступен
# Невозможно оптимизировать запросы под semantic mode
```

---

## ✅ Решение 1: MCP Server Capabilities (Experimental)

### Предложение: Добавить в MCP Initialize Response

MCP Protocol поддерживает `experimental` capabilities:

```csharp
// Program.cs - при инициализации MCP сервера
.AddMcpServer(options => {
    options.ServerInfo = new Implementation {
        Name = ApplicationName,
        Version = ApplicationVersion,
    };

    // ДОБАВИТЬ:
    options.Capabilities = new ServerCapabilities {
        Experimental = new Dictionary<string, object> {
            ["semanticMode"] = new {
                enabled = true,  // или false, если недоступно
                version = "1.0",
                sources = new[] { "local", "overlord" },
                models = new[] { "nomic-embed-text" },
                vectorDimension = 768
            }
        }
    };
})
```

### MCP Initialize Flow

```
AI → MCP Initialize Request
    ↓
Droid → Check Semantic Availability (startup)
    ↓
Droid → MCP Initialize Response {
    capabilities: {
        experimental: {
            semanticMode: {
                enabled: true,
                sources: ["local", "overlord"],
                ...
            }
        }
    }
}
    ↓
AI → Knows semantic mode is available!
```

### Преимущества

✅ AI знает заранее о semantic mode
✅ AI может оптимизировать стратегию запросов
✅ AI может показать пользователю статус
✅ Стандартный MCP механизм (experimental capabilities)

### Недостатки

❌ Проверка только при старте (статичная)
❌ Если embedding service упал после старта - AI не узнает

---

## ✅ Решение 2: Dynamic Capabilities Tool

### Предложение: Добавить MCP Tool

```csharp
[McpServerTool(Name = "get_capabilities")]
public static Task<object> GetCapabilities(
    ISemanticModeProvider semanticProvider)
{
    var availability = await semanticProvider.CheckAvailabilityAsync();

    return new {
        semanticMode = new {
            enabled = availability.IsAvailable,
            source = availability.Source.ToString(),
            modelName = availability.ModelName,
            vectorDimension = availability.VectorDimension,
            localUrl = availability.LocalEmbeddingUrl,
            overlordUrl = availability.OverlordUrl
        },
        features = new {
            gitIntegration = true,
            editorConfig = true,
            universalSemanticMode = true
        }
    };
}
```

### Flow

```
AI → MCP tools/list
Droid → Returns: [..., "get_capabilities", ...]
    ↓
AI → MCP tools/call: get_capabilities()
Droid → { semanticMode: { enabled: true, ... } }
    ↓
AI → Now knows semantic mode is available!
```

### Преимущества

✅ Динамическая проверка (актуальное состояние)
✅ AI может переспросить в любой момент
✅ Простая реализация
✅ Не зависит от MCP experimental features

### Недостатки

❌ AI должен явно вызвать tool
❌ Дополнительный round-trip

---

## ✅ Решение 3: Tool Metadata Enhancement

### Предложение: Расширить Description Tools

Вместо:
```
view_definition - Views the definition of a symbol
```

Использовать:
```
view_definition - Views the definition of a symbol

[Semantic Mode: Available]
When semantic mode is enabled, this tool enriches results with:
- Related symbols (semantic similarity > 0.7)
- Similar implementations across codebase
- Context-aware suggestions

To check semantic mode availability, call: get_capabilities()
```

### Реализация

```csharp
// ToolEnricher.cs - при построении tool descriptions
public string EnhanceToolDescription(string baseTool, string baseDescription)
{
    var availability = await _semanticProvider.CheckAvailabilityAsync();

    if (availability.IsAvailable)
    {
        return $@"{baseDescription}

[Semantic Mode: ✓ Available via {availability.Source}]
Model: {availability.ModelName} (dim: {availability.VectorDimension})
Enhanced results include:
- Related symbols (similarity-based)
- Cross-project references
- Semantic context
";
    }

    return baseDescription;
}
```

### Преимущества

✅ AI видит возможности сразу в tools/list
✅ Самодокументирующийся API
✅ Не требует отдельного tool

### Недостатки

❌ Статичная информация (на момент tools/list)
❌ Может измениться во время работы

---

## 🎯 Рекомендуемое Решение: Hybrid Approach

### Комбинация всех трёх подходов:

1. **MCP Initialize** (static capabilities)
   - Передаём initial state при запуске
   - AI знает базовые возможности сразу

2. **get_capabilities Tool** (dynamic check)
   - AI может проверить актуальное состояние
   - Позволяет реагировать на изменения

3. **Enhanced Tool Descriptions** (discovery)
   - AI видит semantic возможности в tools/list
   - Понимает что делает каждый tool с semantic mode

---

## Implementation Plan

### Phase 1: Add get_capabilities Tool (Quick Win)

**File:** `UltrasharpTools.Tools/Mcp/Tools/SystemTools.cs` (new)

```csharp
[McpServerToolType]
public static class SystemTools
{
    [McpServerTool(
        Name = "get_capabilities",
        Idempotent = true,
        ReadOnly = true
    )]
    [Description("Returns server capabilities including semantic mode status")]
    public static async Task<object> GetCapabilities(
        ISemanticModeProvider semanticProvider,
        ILogger<SystemToolsLogCategory> logger,
        CancellationToken ct = default)
    {
        var availability = await semanticProvider.CheckAvailabilityAsync(ct);

        return new {
            serverVersion = Program.ApplicationVersion,
            capabilities = new {
                semanticMode = new {
                    enabled = availability.IsAvailable,
                    source = availability.Source.ToString(),
                    modelName = availability.ModelName,
                    vectorDimension = availability.VectorDimension,
                    localEmbeddingUrl = availability.LocalEmbeddingUrl,
                    overlordUrl = availability.OverlordUrl
                },
                features = new {
                    gitIntegration = true,
                    editorConfigSupport = true,
                    universalSemanticMode = true,
                    hybridMode = !string.IsNullOrEmpty(availability.OverlordUrl)
                }
            }
        };
    }
}
```

**Effort:** 1-2 hours
**Impact:** High

---

### Phase 2: Add MCP Initialize Capabilities (Medium Priority)

**File:** `UltrasharpTools.Droid/Program.cs`

```csharp
// В методе Main, после создания services
var semanticProvider = services.GetRequiredService<ISemanticModeProvider>();
var availability = await semanticProvider.CheckAvailabilityAsync();

.AddMcpServer(options => {
    options.ServerInfo = new Implementation {
        Name = ApplicationName,
        Version = ApplicationVersion,
    };

    options.Capabilities = new ServerCapabilities {
        Experimental = new Dictionary<string, object> {
            ["semanticMode"] = new {
                enabled = availability.IsAvailable,
                version = "1.0",
                source = availability.Source.ToString(),
                modelName = availability.ModelName,
                vectorDimension = availability.VectorDimension
            }
        }
    };
})
```

**Effort:** 2-3 hours (нужно синхронизировать с DI)
**Impact:** Medium

---

### Phase 3: Enhance Tool Descriptions (Low Priority)

**File:** `UltrasharpTools.Droid/Services/Hybrid/ToolEnricher.cs`

Добавить метод для динамического обогащения descriptions при tools/list.

**Effort:** 4-6 hours
**Impact:** Low (nice to have)

---

## Testing

### Manual Test

```bash
# Start Droid with semantic mode
ultrasharp-tools-mcp --mode hybrid --embedding-url http://localhost:11434

# Test 1: Check capabilities
$ mcp-client call get_capabilities
{
  "capabilities": {
    "semanticMode": {
      "enabled": true,
      "source": "Local",
      "modelName": "nomic-embed-text",
      "vectorDimension": 768
    }
  }
}

# Test 2: Use semantic-enhanced tool
$ mcp-client call view_definition --fqn MyClass
{
  "original": { ... },
  "semantic": {
    "relatedSymbols": [ ... ],
    "similarImplementations": [ ... ]
  }
}
```

---

## Summary

| Решение | Effort | Impact | Статичность | Сложность |
|---------|--------|--------|-------------|-----------|
| MCP Initialize Capabilities | Medium | Medium | Static | Medium |
| get_capabilities Tool | **Low** | **High** | **Dynamic** | **Low** |
| Enhanced Tool Descriptions | High | Low | Static | High |

**Рекомендация:** Начать с **get_capabilities tool** (Phase 1) - быстро реализуется, высокий impact.


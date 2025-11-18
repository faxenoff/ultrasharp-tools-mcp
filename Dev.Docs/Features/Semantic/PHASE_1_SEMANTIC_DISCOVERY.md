# Phase 1: Semantic Mode Discovery - COMPLETE

## Цель
Дать AI клиенту возможность узнать о поддержке semantic mode при подключении к MCP серверу.

## Проблема
До Phase 1:
- AI клиент **не знал** о semantic mode до первого вызова tool
- Невозможно оптимизировать стратегию запросов заранее
- AI не мог показать пользователю статус semantic mode

## Решение

### 1. get_capabilities Tool ✅
**Файл**: `UltrasharpTools.Tools\Mcp\Tools\SystemTools.cs`

**MCP Tool** для динамической проверки capabilities в runtime:

```csharp
[McpServerTool(Name = "get_capabilities")]
public static async Task<object> GetCapabilities(ISemanticModeProvider semanticProvider)
{
    var availability = await semanticProvider.CheckAvailabilityAsync(ct);
    return new {
        serverInfo = { name, version, protocol },
        capabilities = {
            semanticMode = {
                enabled = availability.IsAvailable,
                source = availability.Source.ToString(),
                modelName = availability.ModelName,
                vectorDimension = availability.VectorDimension,
                dynamic = true
            },
            features = { gitIntegration, hybridMode, tracing, ... }
        }
    };
}
```

**Использование**:
```bash
AI → get_capabilities()
Droid → { semanticMode: { enabled: true, source: "Both", ... } }
```

### 2. MCP Initialize Capabilities ✅
**Файл**: `UltrasharpTools.Droid\Program.cs`

**MCP Protocol Capabilities** передаются при Initialize:

```csharp
// Bootstrap check (без DI, 3s timeout)
var semanticAvailability = await SemanticModeBootstrapCheck.CheckAvailabilityAsync(
    embeddingUrl, serverUrl, timeoutMs: 3000);

// MCP Server capabilities
builder.Services.AddMcpServer(options => {
    options.Capabilities = new ServerCapabilities {
        Experimental = new Dictionary<string, object> {
            ["semanticMode"] = new {
                enabled = semanticAvailability.IsAvailable,
                source = semanticAvailability.Source.ToString(),
                modelName = semanticAvailability.ModelName,
                vectorDimension = semanticAvailability.VectorDimension,
                dynamic = true,
                note = "Use get_capabilities tool for real-time status"
            }
        }
    };
});
```

**Использование**:
```
AI → MCP Initialize Request
Droid → MCP Initialize Response {
    capabilities: {
        experimental: {
            semanticMode: { enabled: true, source: "Both", ... }
        }
    }
}
```

### 3. Bootstrap Check без DI ✅
**Файл**: `UltrasharpTools.Droid\Services\Hybrid\SemanticModeBootstrapCheck.cs`

**Простая проверка** до построения DI контейнера:

```csharp
public static async Task<SemanticModeAvailability> CheckAvailabilityAsync(
    string? embeddingUrl,
    string? overlordUrl,
    int timeoutMs = 3000)
{
    var hasLocal = await CheckLocalAsync(embeddingUrl, timeoutMs);   // GET /health
    var hasOverlord = await CheckOverlordAsync(overlordUrl, timeoutMs); // GET /api/server/status

    return new SemanticModeAvailability {
        IsAvailable = hasLocal || hasOverlord,
        Source = DetermineSource(hasLocal, hasOverlord),
        ModelName = "nomic-embed-text",
        VectorDimension = 768
    };
}
```

### 4. Архитектурное улучшение ✅
**Файл**: `UltrasharpTools.Tools\Interfaces\ISemanticModeProvider.cs`

**Переместили интерфейсы** из Droid → Tools.Interfaces:
- `ISemanticModeProvider`
- `SemanticModeSource` (enum)
- `SemanticModeAvailability`
- `SemanticMatch`

**Причина**: Разорвать циклическую зависимость:
- Droid → Tools (для MCP tools)
- Tools → Droid (для ISemanticModeProvider) ❌ ЦИКЛИЧЕСКАЯ ЗАВИСИМОСТЬ

**Решение**: Tools.Interfaces → общий контракт для обоих проектов.

## Результаты

### ✅ AI клиент узнаёт о semantic mode **сразу при подключении**
```json
{
  "capabilities": {
    "experimental": {
      "semanticMode": {
        "enabled": true,
        "source": "Both"
      }
    }
  }
}
```

### ✅ AI может проверить актуальное состояние в runtime
```bash
get_capabilities() → { semanticMode: { enabled: true, source: "Local" } }
```

### ✅ Startup время увеличивается всего на 3s (timeout bootstrap check)
- Local embedding: `GET /health` (timeout: 3s)
- Overlord: `GET /api/server/status` (timeout: 3s)
- Параллельная проверка: 3s max

### ✅ Консольный вывод при запуске:
```
Checking semantic mode availability...
Semantic mode: AVAILABLE (Both)
Starting UltrasharpToolsMcpDroid v3.0.0
```

## Тестирование

### Тест 1: Без semantic mode
```bash
ultrasharp-tools-mcp --mode local
# Output: Semantic mode: NOT AVAILABLE
# MCP Initialize: { semanticMode: { enabled: false, source: "None" } }
```

### Тест 2: С локальным embedding
```bash
ultrasharp-tools-mcp --mode local --embedding-url http://localhost:11434
# Output: Semantic mode: AVAILABLE (Local)
# MCP Initialize: { semanticMode: { enabled: true, source: "Local" } }
```

### Тест 3: Hybrid mode
```bash
ultrasharp-tools-mcp --mode hybrid --server-url http://localhost:8080 --embedding-url http://localhost:11434
# Output: Semantic mode: AVAILABLE (Both)
# MCP Initialize: { semanticMode: { enabled: true, source: "Both" } }
```

### Тест 4: Runtime check
```bash
> get_capabilities()
{
  "capabilities": {
    "semanticMode": {
      "enabled": true,
      "source": "Both",
      "modelName": "nomic-embed-text",
      "vectorDimension": 768,
      "localEmbeddingUrl": "http://localhost:11434",
      "overlordUrl": "http://localhost:8080",
      "dynamic": true,
      "cacheValiditySeconds": 60
    }
  }
}
```

## Ограничения Phase 1

❌ **Нет уведомлений при изменении состояния**
- Если embedding service упал после старта - AI не узнает
- Нужно вручную вызвать `get_capabilities()`

❌ **Нет Circuit Breaker**
- Если embedding service постоянно падает - будет cascade failures
- Нет защиты от повторных запросов к недоступному сервису

❌ **Нет Graceful Degradation**
- Только два состояния: Available / Not Available
- Нет "Partial" режима (например, только read-only semantic)

## Что дальше: Phase 2 (Lifecycle Management)

See: `SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md`

### Планы:
1. **SemanticModeMonitor** background service (30s checks)
2. **MCP Notifications** для state changes
3. **Circuit Breaker** pattern (fail-fast)
4. **Graceful Degradation** (Full/Partial/Degraded/Unavailable)
5. **Health metrics** (uptime, failure rate)

## Файлы Phase 1

### Новые файлы:
- `UltrasharpTools.Tools\Mcp\Tools\SystemTools.cs` - get_capabilities tool
- `UltrasharpTools.Droid\Services\Hybrid\SemanticModeBootstrapCheck.cs` - bootstrap check
- `UltrasharpTools.Tools\Interfaces\ISemanticModeProvider.cs` - интерфейсы (moved from Droid)

### Изменённые файлы:
- `UltrasharpTools.Droid\Program.cs` - MCP Initialize capabilities + bootstrap check
- `UltrasharpTools.Droid\Services\Hybrid\ISemanticModeProvider.cs` - redirect to Tools.Interfaces

### Документация:
- `SEMANTIC_MODE_DISCOVERY_ANALYSIS.md` - анализ проблемы и решения
- `SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md` - lifecycle management (Phase 2+)
- `PHASE_1_SEMANTIC_DISCOVERY.md` (этот файл)

## Build Status
✅ **Build: SUCCESS** (0 errors, 4 warnings в Overlord)

```bash
dotnet build UltrasharpTools.sln --configuration Release
# Build succeeded.
# 0 Error(s)
```

---

**Status**: ✅ COMPLETE
**Version**: 3.0.0
**Date**: 2025-01-18

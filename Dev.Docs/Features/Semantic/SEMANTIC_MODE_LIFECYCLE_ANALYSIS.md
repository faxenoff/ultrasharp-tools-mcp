# Semantic Mode Lifecycle & Fault Tolerance Analysis

## Проблема: Динамическое состояние vs MCP Initialize

### Сценарии Отказа

```
Timeline:
T0: Droid Start
    - Overlord: ✓ Available
    - Local Embedding: ✓ Available
    - MCP Initialize → capabilities: { semanticMode: enabled }

T1: AI работает
    - view_definition() → получает semantic enrichment ✓

T2: Overlord падает (network issue, restart, etc)
    - Overlord: ✗ UNAVAILABLE
    - Local Embedding: ✓ Available
    - semantic mode: ⚠️ Degraded (только local)

T3: AI делает запрос
    - view_definition() → ?

T4: Local Embedding падает
    - Overlord: ✗ UNAVAILABLE
    - Local Embedding: ✗ UNAVAILABLE
    - semantic mode: ✗ UNAVAILABLE

T5: AI делает запрос
    - view_definition() → ?

T6: Overlord восстанавливается
    - Overlord: ✓ Available
    - semantic mode: ✓ Partial (только Overlord)

T7: AI делает запрос
    - view_definition() → ?
```

### Вопросы

1. **Как AI узнаёт об изменении статуса?**
2. **Нужно ли переинициализировать MCP соединение?**
3. **Как обрабатывать degraded mode (local работает, Overlord нет)?**

---

## Текущая Реализация

### CheckAvailabilityAsync - С Кэшированием

```csharp
// SemanticModeProvider.cs
private SemanticModeAvailability? _cachedAvailability;
private DateTime _lastCheck = DateTime.MinValue;

public async Task<SemanticModeAvailability> CheckAvailabilityAsync(CancellationToken ct)
{
    // Проверяем кэш (60 секунд по умолчанию)
    var cacheValidity = TimeSpan.FromSeconds(_config.Availability.CacheValiditySeconds);
    if (_cachedAvailability != null && DateTime.UtcNow - _lastCheck < cacheValidity)
    {
        return _cachedAvailability; // ← Возвращает старое состояние!
    }

    // Реальная проверка
    var hasLocal = await CheckLocalAvailabilityAsync(ct);
    var hasOverlord = await CheckOverlordAvailabilityAsync(ct);

    // Обновляем кэш
    _cachedAvailability = new SemanticModeAvailability { ... };
    _lastCheck = DateTime.UtcNow;

    return _cachedAvailability;
}
```

### Проблема с Кэшем

```
T0: Check → Local: ✓, Overlord: ✓ → Cache: Both (valid for 60s)
T5: Check → Returns cached "Both" (без реальной проверки!)
T10: Local падает
T15: Check → Returns cached "Both" (НЕВЕРНО! Local мертв)
T60: Check → Recheck → Local: ✗, Overlord: ✓ → Cache: Overlord

Проблема: До 60 секунд AI получает устаревшую информацию!
```

---

## ✅ Решение 1: MCP Initialize + Notifications

### Архитектура

```
MCP Protocol поддерживает:
1. Initialize Request/Response (статические capabilities)
2. Server-initiated Notifications (динамические изменения)
```

### Implementation

#### 1.1 Initialize Response (Startup State)

```csharp
// Program.cs
public static async Task<int> Main(string[] args)
{
    // ... build services

    var semanticProvider = services.GetRequiredService<ISemanticModeProvider>();

    // ПРОВЕРКА ПРИ СТАРТЕ (без кэша)
    var initialAvailability = await semanticProvider.CheckAvailabilityAsync(
        CancellationToken.None
    );

    .AddMcpServer(options => {
        options.ServerInfo = new Implementation {
            Name = ApplicationName,
            Version = ApplicationVersion,
        };

        // ПЕРЕДАЁМ НАЧАЛЬНОЕ СОСТОЯНИЕ
        options.Capabilities = new ServerCapabilities {
            Experimental = new Dictionary<string, object> {
                ["semanticMode"] = new {
                    enabled = initialAvailability.IsAvailable,
                    version = "1.0",
                    source = initialAvailability.Source.ToString(),
                    modelName = initialAvailability.ModelName,
                    vectorDimension = initialAvailability.VectorDimension,

                    // ВАЖНО: Указываем что состояние может меняться
                    dynamic = true,
                    notificationsSupported = true
                }
            }
        };
    });
}
```

#### 1.2 Background Monitoring Service

```csharp
// UltrasharpTools.Droid/Services/Hybrid/SemanticModeMonitor.cs
public sealed class SemanticModeMonitor : BackgroundService
{
    private readonly ISemanticModeProvider _semanticProvider;
    private readonly IMcpNotificationSender _notificationSender;
    private readonly ILogger<SemanticModeMonitor> _logger;
    private readonly SemanticModeConfig _config;

    private SemanticModeAvailability? _lastKnownState;

    public SemanticModeMonitor(
        ISemanticModeProvider semanticProvider,
        IMcpNotificationSender notificationSender,
        ILogger<SemanticModeMonitor> logger,
        SemanticModeConfig config)
    {
        _semanticProvider = semanticProvider;
        _notificationSender = notificationSender;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Начальное состояние
        _lastKnownState = await _semanticProvider.CheckAvailabilityAsync(stoppingToken);

        _logger.LogInformation(
            "SemanticModeMonitor started: initial state = {State}",
            _lastKnownState.Source
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ждём настроенный интервал
                await Task.Delay(
                    TimeSpan.FromSeconds(_config.Monitoring.CheckIntervalSeconds),
                    stoppingToken
                );

                // РЕАЛЬНАЯ ПРОВЕРКА (игнорируем кэш)
                var currentState = await CheckWithoutCacheAsync(stoppingToken);

                // Сравниваем со старым состоянием
                if (HasStateChanged(_lastKnownState, currentState))
                {
                    _logger.LogWarning(
                        "Semantic mode state changed: {Old} → {New}",
                        _lastKnownState.Source,
                        currentState.Source
                    );

                    // ОТПРАВЛЯЕМ NOTIFICATION КЛИЕНТУ
                    await SendStateChangeNotificationAsync(
                        _lastKnownState,
                        currentState,
                        stoppingToken
                    );

                    _lastKnownState = currentState;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in semantic mode monitoring");
            }
        }
    }

    private async Task<SemanticModeAvailability> CheckWithoutCacheAsync(CancellationToken ct)
    {
        // Прямые проверки без кэша
        var hasLocal = await CheckLocalDirectAsync(ct);
        var hasOverlord = await CheckOverlordDirectAsync(ct);

        SemanticModeSource source;
        if (hasLocal && hasOverlord)
            source = SemanticModeSource.Both;
        else if (hasLocal)
            source = SemanticModeSource.Local;
        else if (hasOverlord)
            source = SemanticModeSource.Overlord;
        else
            source = SemanticModeSource.None;

        return new SemanticModeAvailability
        {
            IsAvailable = source != SemanticModeSource.None,
            Source = source,
            // ... остальные поля
        };
    }

    private bool HasStateChanged(
        SemanticModeAvailability? old,
        SemanticModeAvailability current)
    {
        if (old == null) return true;

        // Изменился источник
        if (old.Source != current.Source)
            return true;

        // Изменилась доступность
        if (old.IsAvailable != current.IsAvailable)
            return true;

        return false;
    }

    private async Task SendStateChangeNotificationAsync(
        SemanticModeAvailability? oldState,
        SemanticModeAvailability newState,
        CancellationToken ct)
    {
        // MCP Notification (experimental)
        await _notificationSender.SendNotificationAsync(
            "semantic-mode/state-changed",
            new {
                previous = oldState != null ? new {
                    enabled = oldState.IsAvailable,
                    source = oldState.Source.ToString()
                } : null,
                current = new {
                    enabled = newState.IsAvailable,
                    source = newState.Source.ToString(),
                    modelName = newState.ModelName,
                    vectorDimension = newState.VectorDimension,
                    timestamp = DateTime.UtcNow
                },
                reason = DetermineChangeReason(oldState, newState)
            },
            ct
        );
    }

    private string DetermineChangeReason(
        SemanticModeAvailability? old,
        SemanticModeAvailability current)
    {
        if (old == null)
            return "initial";

        if (old.Source == SemanticModeSource.Both && current.Source == SemanticModeSource.Local)
            return "overlord-unavailable";

        if (old.Source == SemanticModeSource.Both && current.Source == SemanticModeSource.Overlord)
            return "local-unavailable";

        if (old.Source != SemanticModeSource.None && current.Source == SemanticModeSource.None)
            return "all-services-down";

        if (old.Source == SemanticModeSource.None && current.Source != SemanticModeSource.None)
            return "service-restored";

        return "unknown";
    }
}
```

#### 1.3 Configuration

```json
// Run.Config/semantic-config.json
{
  "monitoring": {
    "enabled": true,
    "checkIntervalSeconds": 30,  // Проверка каждые 30 секунд
    "notifyOnChange": true
  },
  "availability": {
    "cacheValiditySeconds": 10,  // Уменьшим кэш для более свежих данных
    "localCheckTimeoutSeconds": 3,
    "overlordCheckTimeoutSeconds": 5
  }
}
```

#### 1.4 Register Background Service

```csharp
// Program.cs
services.AddHostedService<SemanticModeMonitor>();
```

---

### Flow Diagram

```
T0: Droid Start
    ↓
    SemanticModeMonitor.ExecuteAsync()
    - Initial check: Both available
    - _lastKnownState = Both
    ↓
MCP Initialize Response
    capabilities.experimental.semanticMode = {
        enabled: true,
        source: "Both",
        dynamic: true
    }
    ↓
AI: "OK, semantic mode available!"

─────────────────────────────────────

T30: Monitor Check #1
    ✓ Local: Available
    ✓ Overlord: Available
    No change → no notification

─────────────────────────────────────

T60: Monitor Check #2
    ✓ Local: Available
    ✗ Overlord: UNAVAILABLE (timeout)

    Change detected: Both → Local
    ↓
    MCP Notification:
    {
        method: "semantic-mode/state-changed",
        params: {
            previous: { source: "Both" },
            current: { source: "Local" },
            reason: "overlord-unavailable"
        }
    }
    ↓
AI receives notification:
    "Semantic mode degraded to Local only"

─────────────────────────────────────

T90: Monitor Check #3
    ✗ Local: UNAVAILABLE
    ✗ Overlord: UNAVAILABLE

    Change detected: Local → None
    ↓
    MCP Notification:
    {
        method: "semantic-mode/state-changed",
        params: {
            previous: { source: "Local" },
            current: { source: "None" },
            reason: "all-services-down"
        }
    }
    ↓
AI receives notification:
    "Semantic mode UNAVAILABLE"
    - Falls back to basic mode
    - No semantic enrichment

─────────────────────────────────────

T120: Monitor Check #4
    ✗ Local: UNAVAILABLE
    ✓ Overlord: Available (restored!)

    Change detected: None → Overlord
    ↓
    MCP Notification:
    {
        method: "semantic-mode/state-changed",
        params: {
            previous: { source: "None" },
            current: { source: "Overlord" },
            reason: "service-restored"
        }
    }
    ↓
AI receives notification:
    "Semantic mode RESTORED (Overlord)"
    - Can use semantic features again!
```

---

## ✅ Решение 2: Graceful Degradation (Fallback)

### Стратегия

Вместо бинарного "работает/не работает", используем **уровни degradation**:

```csharp
public enum SemanticModeLevel
{
    Full,       // Local + Overlord
    Partial,    // Только Local ИЛИ только Overlord
    Degraded,   // Один источник с ошибками
    Unavailable // Всё недоступно
}
```

### Implementation

```csharp
// SemanticModeProvider.cs
public async Task<EnrichedToolResult> EnrichAsync(...)
{
    var availability = await CheckAvailabilityAsync(ct);

    // GRACEFUL DEGRADATION
    switch (availability.Level)
    {
        case SemanticModeLevel.Full:
            // Полный semantic enrichment
            return await FullEnrichmentAsync(...);

        case SemanticModeLevel.Partial:
            _logger.LogWarning("Semantic mode degraded to {Source}", availability.Source);
            // Ограниченный enrichment (без cross-project search)
            return await PartialEnrichmentAsync(...);

        case SemanticModeLevel.Degraded:
            _logger.LogWarning("Semantic mode experiencing issues");
            // Minimal enrichment (только кэшированные данные)
            return await CachedEnrichmentAsync(...);

        case SemanticModeLevel.Unavailable:
            _logger.LogInformation("Semantic mode unavailable - returning base result");
            // Только базовый результат
            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    ErrorMessage = "Semantic services unavailable"
                }
            };
    }
}
```

### В ответе AI

```json
// Full mode
{
  "original": { ... },
  "semantic": {
    "relatedSymbols": [...],
    "similarImplementations": [...],
    "crossProjectReferences": [...]
  },
  "metadata": {
    "semanticLevel": "Full",
    "source": "Both"
  }
}

// Partial mode (только Local)
{
  "original": { ... },
  "semantic": {
    "relatedSymbols": [...],
    "similarImplementations": [...],
    "crossProjectReferences": null  // ← недоступно без Overlord
  },
  "metadata": {
    "semanticLevel": "Partial",
    "source": "Local",
    "degradationReason": "Overlord unavailable"
  }
}

// Unavailable
{
  "original": { ... },
  "semantic": null,
  "metadata": {
    "semanticLevel": "Unavailable",
    "errorMessage": "All semantic services down"
  }
}
```

---

## ✅ Решение 3: Retry Strategy + Circuit Breaker

### Проблема: Лавина проверок при отказе

```
Overlord падает
    ↓
Каждый tool call пытается подключиться
    ↓
Timeout 5s × 10 requests = 50 секунд задержки!
```

### Решение: Circuit Breaker Pattern

```csharp
// UltrasharpTools.Droid/Services/Hybrid/SemanticCircuitBreaker.cs
public sealed class SemanticCircuitBreaker
{
    private enum State
    {
        Closed,     // Нормальная работа
        Open,       // Сломано, не пытаемся
        HalfOpen    // Пробуем восстановиться
    }

    private State _state = State.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailure = DateTime.MinValue;
    private readonly SemanticModeConfig _config;

    public async Task<T?> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        switch (_state)
        {
            case State.Open:
                // Проверяем, можно ли перейти в HalfOpen
                if (DateTime.UtcNow - _lastFailure > TimeSpan.FromSeconds(_config.CircuitBreaker.OpenDurationSeconds))
                {
                    _state = State.HalfOpen;
                    _logger.LogInformation("Circuit breaker: Open → HalfOpen (attempting recovery)");
                }
                else
                {
                    // Быстрый fail без попытки подключения
                    _logger.LogTrace("Circuit breaker OPEN - skipping call");
                    return default;
                }
                break;

            case State.HalfOpen:
                // Пробуем одну попытку
                try
                {
                    var result = await action(ct);

                    // Успех! Переходим в Closed
                    _state = State.Closed;
                    _failureCount = 0;
                    _logger.LogInformation("Circuit breaker: HalfOpen → Closed (recovery successful)");

                    return result;
                }
                catch
                {
                    // Неудача, возвращаемся в Open
                    _state = State.Open;
                    _lastFailure = DateTime.UtcNow;
                    _logger.LogWarning("Circuit breaker: HalfOpen → Open (recovery failed)");
                    throw;
                }

            case State.Closed:
                try
                {
                    return await action(ct);
                }
                catch
                {
                    _failureCount++;
                    _lastFailure = DateTime.UtcNow;

                    if (_failureCount >= _config.CircuitBreaker.FailureThreshold)
                    {
                        _state = State.Open;
                        _logger.LogWarning(
                            "Circuit breaker: Closed → Open ({Count} failures)",
                            _failureCount
                        );
                    }

                    throw;
                }
        }

        return default;
    }
}
```

### Configuration

```json
{
  "circuitBreaker": {
    "enabled": true,
    "failureThreshold": 3,       // 3 подряд ошибки → Open
    "openDurationSeconds": 30,   // Ждём 30s перед попыткой recovery
    "timeoutSeconds": 5
  }
}
```

---

## 🎯 Рекомендуемая Архитектура

### Комбинация всех решений:

```
┌─────────────────────────────────────────────────┐
│ MCP Initialize                                  │
│ ├─ capabilities.experimental.semanticMode       │
│ │  - enabled: true/false                        │
│ │  - source: "Both"/"Local"/"Overlord"/"None"   │
│ │  - dynamic: true (может меняться!)            │
│ └─ Начальное состояние при старте               │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│ SemanticModeMonitor (Background Service)        │
│ ├─ Проверяет состояние каждые 30s              │
│ ├─ При изменении → отправляет MCP Notification  │
│ └─ AI получает real-time updates               │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│ Circuit Breaker Pattern                         │
│ ├─ Защита от лавины проверок при отказе        │
│ ├─ Быстрый fail если сервис недоступен          │
│ └─ Автоматический recovery                      │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│ Graceful Degradation                            │
│ ├─ Full mode: Local + Overlord                 │
│ ├─ Partial mode: только один источник           │
│ └─ Unavailable: fallback to basic              │
└─────────────────────────────────────────────────┘
```

---

## Implementation Priority

### Phase 1: Critical (Must Have)
1. ✅ **MCP Initialize with semantic capabilities** (2-3h)
2. ✅ **Circuit Breaker for Overlord checks** (3-4h)
3. ✅ **Graceful Degradation in ToolEnricher** (2-3h)

### Phase 2: Important (Should Have)
4. ⭐ **SemanticModeMonitor background service** (4-6h)
5. ⭐ **MCP Notifications for state changes** (2-3h)

### Phase 3: Nice to Have
6. 🔹 **get_capabilities tool** (1-2h)
7. 🔹 **Enhanced tool descriptions** (4-6h)

---

## Ответы на твои вопросы

### 1. "При старте сразу передать"

✅ **Да!** Через MCP Initialize Response:
```csharp
options.Capabilities = new ServerCapabilities {
    Experimental = new Dictionary<string, object> {
        ["semanticMode"] = initialAvailability
    }
};
```

### 2. "Семантик может отвалиться - что будет?"

✅ **Circuit Breaker + Graceful Degradation:**
- Первые 3 ошибки → логируем, пытаемся дальше
- После 3 ошибок → Circuit Breaker OPEN, быстрый fail
- Каждые 30s → пытаемся восстановиться (HalfOpen)
- В ответах AI → `metadata.semanticLevel = "Unavailable"`

### 3. "Если восстановится потом - как сообщить?"

✅ **MCP Notifications:**
```javascript
// AI получает server-initiated notification
{
  method: "semantic-mode/state-changed",
  params: {
    current: { enabled: true, source: "Overlord" },
    reason: "service-restored"
  }
}
```

---

## Summary

**Лучшая стратегия:** Hybrid approach

1. **Startup:** MCP Initialize с текущим состоянием
2. **Runtime:** Background monitor + MCP Notifications
3. **Fault Tolerance:** Circuit Breaker + Graceful Degradation

**Результат:** AI знает о semantic mode в любой момент времени и получает real-time updates при изменениях.

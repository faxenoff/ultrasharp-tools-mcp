# План оптимизации производительности .NET 10

Результаты анализа по чеклисту оптимизации .NET 10 приложений.

---

## Сводка

| Категория | Статус | Комментарий |
|-----------|--------|-------------|
| JSON Source Generators | ✅ Хорошо | 3 контекста реализованы |
| ArrayPool | ✅ Хорошо | Используется в FastHash, BufferPoolManager |
| FrozenDictionary | ✅ Хорошо | 6+ файлов |
| SearchValues | ✅ Хорошо | 2 файла |
| Span/Memory | ✅ Хорошо | 58 вхождений |
| stackalloc | ✅ Хорошо | Правильный threshold |
| ObjectPool | ✅ Хорошо | ObjectPoolProvider, ObjectPoolService |
| StringComparison.Ordinal | ✅ Хорошо | 58 вхождений |
| GeneratedRegex | ⚠️ Частично | 6 regex, но 11+ runtime |
| IHttpClientFactory | ✅ Хорошо | Нет new HttpClient() |
| **[LoggerMessage]** | ❌ **Критично** | 0 из 180 вызовов |
| **ConfigureAwait** | ❌ **Критично** | 13 из 1000+ await |
| JsonSerializer context | ⚠️ Частично | Много без context |
| GetAwaiter().GetResult() | ⚠️ Проблема | 7 мест |
| static lambdas | ❌ Отсутствует | 0 найдено |
| CompositeFormat | ❌ Отсутствует | Не используется |
| HybridCache | ❌ Отсутствует | Не используется |

---

## 1. КРИТИЧНО: [LoggerMessage] Source Generators

### Проблема
180 вызовов `_logger.Log*` без source-generated logging. Это приводит к:
- Boxing value types при каждом вызове
- Runtime парсинг шаблонов
- Аллокации даже когда уровень логирования отключён

### Затронутые файлы (топ-10 по количеству)
| Файл | Вызовов |
|------|---------|
| `VectorDBService.cs` | 6 |
| `VersionManager.cs` | 14 |
| `TEIProvider.cs` | 16 |
| `AgentController.cs` | 12 |
| `ConfigurationService.cs` | 7 |
| `CodeAnalysisService.cs` | 19 |
| `FileWatcherService.cs` | 12 |
| `GitWatcherService.cs` | 13 |
| `OrphanedDeltaCleanupService.cs` | 14 |
| `VectorCacheManager.cs` | 10 |

### Решение

**Шаг 1**: Создать partial классы с LoggerMessage:

```csharp
// Пример для VectorDBService
public partial class VectorDBService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing request: {RequestType}")]
    private partial void LogProcessingRequest(string requestType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing request: {Error}")]
    private partial void LogError(string error);
}
```

**Шаг 2**: Заменить вызовы:
```csharp
// До
_logger.LogInformation("Processing request: {RequestType}", requestType);

// После
LogProcessingRequest(requestType);
```

### Приоритет: ВЫСОКИЙ
### Оценка: ~180 изменений в ~40 файлах

---

## 2. КРИТИЧНО: ConfigureAwait(false) в библиотеках

### Проблема
Только 13 вхождений `.ConfigureAwait(false)` из 1000+ await-вызовов.
Для библиотеки `UltrasharpTools.Tools` это критично — потребители могут получить deadlock.

### Затронутые файлы
- **UltrasharpTools.Tools** — вся библиотека
- **UltraSharpTools.VectorDB** — вся библиотека

### Решение

**Вариант 1**: Добавить вручную
```csharp
await SomeMethodAsync().ConfigureAwait(false);
```

**Вариант 2**: Использовать `.editorconfig`:
```ini
# .editorconfig
dotnet_diagnostic.CA2007.severity = warning
```

**Вариант 3**: Fody.ConfigureAwait (автоматически добавляет):
```xml
<PackageReference Include="ConfigureAwait.Fody" Version="3.3.2" />
```

### Приоритет: ВЫСОКИЙ
### Оценка: ~1000+ изменений или 1 пакет

---

## 3. JsonSerializer без Source Generator Context

### Проблема
131 вхождение `JsonSerializer.Serialize/Deserialize`, многие без типизированного context.
Особенно в `McpProxyService.cs` (77 вхождений) — сериализация анонимных типов.

### Затронутые файлы
| Файл | Вхождений |
|------|-----------|
| `McpProxyService.cs` | 77 |
| `ConfigurationService.cs` | 2 |
| `ServerBridgeService.cs` | 2 |
| `SemanticModeConfigurationLoader.cs` | 4 |
| `LayeredCacheManager.cs` | 6 |
| Другие | ~40 |

### Решение

**Для McpProxyService**: Создать типизированные response классы:
```csharp
// Вместо анонимных типов
return JsonSerializer.Serialize(new { error = "Invalid arguments" });

// Использовать типизированные
[JsonSerializable(typeof(ErrorResponse))]
internal partial class McpProxyJsonContext : JsonSerializerContext { }

public record ErrorResponse(string Error);
return JsonSerializer.Serialize(new ErrorResponse("Invalid arguments"),
    McpProxyJsonContext.Default.ErrorResponse);
```

### Приоритет: СРЕДНИЙ
### Оценка: ~50 изменений + новые типы

---

## 4. GetAwaiter().GetResult() — синхронное ожидание

### Проблема
7 мест с синхронным ожиданием async кода. Риск deadlock и блокировки потоков.

### Затронутые файлы
| Файл | Строка | Контекст |
|------|--------|----------|
| `ServiceCollectionExtensions.cs` | 185 | DI registration |
| `RequestBatchingService.cs` | 36 | Timer callback |
| `BackgroundCleanupScheduler.cs` | 276 | Dispose |
| `CallGraphCacheService.cs` | 35 | Constructor |
| `SolutionManager.cs` | 653, 666, 793 | Sync wrappers |

### Решение

**Для DI**: Использовать async factory
```csharp
// До
services.AddSingleton(sp => factory.CreateAsync().GetAwaiter().GetResult());

// После
services.AddSingleton<IEmbeddingProvider>(async sp =>
{
    var factory = sp.GetRequiredService<EmbeddingProviderFactory>();
    return await factory.CreateAsync();
});
```

**Для Timer callback**: Использовать async void или Channel
```csharp
// До
_ => ProcessBatchAsync().GetAwaiter().GetResult()

// После
async _ => await ProcessBatchAsync()
```

**Для Dispose**: Implement IAsyncDisposable
```csharp
public async ValueTask DisposeAsync()
{
    await StopAsync();
}
```

### Приоритет: СРЕДНИЙ
### Оценка: 7 изменений

---

## 5. Runtime Regex вместо [GeneratedRegex]

### Проблема
11+ мест с `new Regex()` в runtime вместо compile-time генерации.

### Затронутые файлы
| Файл | Строка |
|------|--------|
| `CodeModificationService.cs` | 483 |
| `SymbolPatternMatcher.cs` | 21, 28 |
| `SqliteReflectionTypeIndex.cs` | 333 |
| `SolutionManager.cs` | 1192 |
| `AnalysisTools.cs` | 2128, 2508 |
| `ModificationTools.cs` | 2092, 3042 |
| `PatternSearchTools.cs` | 197, 333 |

### Решение

**Для статических паттернов**:
```csharp
// До
var regex = new Regex(@"\s*\bclass\b\s*", RegexOptions.Compiled);

// После
[GeneratedRegex(@"\s*\bclass\b\s*")]
private static partial Regex ClassRegex();
```

**Для динамических паттернов** (user input): оставить как есть, но кешировать.

### Приоритет: НИЗКИЙ (уже есть RegexOptions.Compiled)
### Оценка: ~5 изменений (только статические паттерны)

---

## 6. Static Lambdas

### Проблема
0 static lambdas найдено. Каждая lambda без `static` потенциально создаёт closure.

### Hot paths для оптимизации
- LINQ операции в `SolutionManager`, `FastSymbolIndex`
- Event handlers в `FileWatcherService`, `GitWatcherService`
- Callbacks в `LruCache`, `ObjectPool`

### Решение
```csharp
// До
list.Where(x => x.Name == targetName)

// После (если targetName можно передать через state)
list.Where(static (x, state) => x.Name == state, targetName)

// Или для простых случаев
list.Where(static x => x.IsPublic)
```

### Приоритет: НИЗКИЙ
### Оценка: ~20-30 изменений в hot paths

---

## 7. Collection Capacity

### Проблема
52 места с `new List<>()` или `new Dictionary<>()` без указания capacity.

### Hot paths
- `ToolEnricher.cs` — 11 вхождений
- `MultiProjectVectorStoreService.cs` — 6 вхождений
- `ThreeWayMerger.cs` — 3 вхождения
- `FileOperationTools.cs` — 6 вхождений

### Решение
```csharp
// До
var results = new List<string>();

// После (если известен примерный размер)
var results = new List<string>(expectedCount);
```

### Приоритет: НИЗКИЙ
### Оценка: ~20 изменений в hot paths

---

## 8. Рекомендуемые новые фичи

### HybridCache (.NET 9+)
Заменить `IMemoryCache` на `HybridCache` для:
- `AnalysisCacheService`
- `SyntaxTreeCacheService`
- `SemanticModelCacheService`

Преимущества:
- Встроенная защита от stampede
- L1+L2 кеширование
- Tag-based invalidation

### CompositeFormat
Для часто используемых строковых шаблонов:
```csharp
// До
string.Format("Symbol {0} not found in {1}", symbol, project);

// После
private static readonly CompositeFormat SymbolNotFoundFormat =
    CompositeFormat.Parse("Symbol {0} not found in {1}");
string.Format(null, SymbolNotFoundFormat, symbol, project);
```

### Lazy<T> для expensive сервисов
```csharp
// Отложенная инициализация тяжёлых сервисов
private readonly Lazy<HeavyService> _heavyService;
```

---

## План внедрения

### Фаза 1 (Критично) — 1-2 недели
1. [ ] Внедрить `[LoggerMessage]` в основные сервисы
2. [ ] Добавить `ConfigureAwait(false)` через Fody или editorconfig
3. [ ] Исправить `GetAwaiter().GetResult()` в критичных местах

### Фаза 2 (Важно) — 1 неделя
4. [ ] Типизировать JsonSerializer в `McpProxyService`
5. [ ] Заменить runtime Regex на `[GeneratedRegex]` где возможно

### Фаза 3 (Улучшения) — ongoing
6. [ ] Добавить `static` к lambdas в hot paths
7. [ ] Указать capacity для коллекций
8. [ ] Внедрить `HybridCache` (после обновления до .NET 9)

---

## Метрики до/после

Рекомендуется измерить с помощью BenchmarkDotNet:
- Время startup
- Memory allocations в hot paths
- Latency основных операций

Уже есть `UltrasharpTools.Benchmarks` — расширить тесты.

---

## Что уже хорошо реализовано

Проект уже использует многие best practices:

1. **ArrayPool** в `FastHash`, `BufferPoolManager`, `BloomFilter`
2. **FrozenDictionary/FrozenSet** в `DocumentOperationsService`, `FastSymbolIndex`, `SolutionManager`
3. **SearchValues** в `FuzzyFqnLookupService`, `DiagnosticPresets`
4. **stackalloc** с правильным threshold (256-1024 байт)
5. **ObjectPool** через `ObjectPoolProvider`, `ObjectPoolService`
6. **StringComparison.Ordinal** повсеместно
7. **ConcurrentDictionary** для thread-safe кешей
8. **SemaphoreSlim** для async синхронизации
9. **Channel<T>** в `IncrementalUpdateQueue`
10. **IAsyncEnumerable** для streaming
11. **ValueTask** в подходящих местах (41 вхождение)
12. **IHttpClientFactory** — нет прямого создания HttpClient
13. **Нет async void** — все async методы возвращают Task

---

*Создано: 2025-11-25*
*На основе чеклиста: Чеклист оптимизации .NET 10 приложений.md*

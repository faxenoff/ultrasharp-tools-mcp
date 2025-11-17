# Инструмент анализа логов

**Эффективный анализ production logs** — автоматическое определение формата, поиск по keywords/levels/status codes, memory-efficient processing для огромных файлов.

## 📋 Quick Reference

| Инструмент | Форматы | Скорость | Memory usage |
|------------|---------|---------|-------------|
| **AnalyzeLogs** | ECS/JSON, PlainText, Logcat, WebServer, XML | Быстро (streaming) | Низкое (не загружает весь файл) |

---

## UltrasharpTool_AnalyzeLogs

**Анализ логов с автоопределением формата** — поиск ошибок, исключений, HTTP errors в production logs без загрузки всего файла в память.

### Использование

```javascript
// Базовый поиск
UltrasharpTool_AnalyzeLogs(
filePath: "D:/Logs/application.log",
levels: ["Error", "Fatal"],
keywords: null,
statusCodes: null,
skip: 0,
take: 100,
contextBefore: 5,
contextAfter: 5,
detailLevel: "Brief"
)

// Поиск HTTP errors
UltrasharpTool_AnalyzeLogs(
filePath: "D:/Logs/access.log",
levels: null,
keywords: null,
statusCodes: [404, 500, 503],
skip: 0,
take: 50
)

// Поиск по keywords
UltrasharpTool_AnalyzeLogs(
filePath: "D:/Logs/app.log",
levels: null,
keywords: ["NullReferenceException", "OutOfMemory", "Timeout"],
skip: 0,
take: 100
)
```

### Параметры

- **filePath** (required): Путь к log файлу
- **levels** (optional): Фильтр по уровням (`["Verbose", "Debug", "Info", "Warning", "Error", "Fatal"]`)
- **keywords** (optional): Ключевые слова для поиска (case-insensitive, поиск в message и stacktrace)
- **statusCodes** (optional): HTTP status codes (`[404, 500, 503, ...]`)
- **skip** (default: 0): Пропустить N результатов (pagination)
- **take** (default: 100): Вернуть N результатов
- **contextBefore** (default: 5): Строк контекста ДО совпадения
- **contextAfter** (default: 5): Строк контекста ПОСЛЕ совпадения
- **detailLevel** (default: "Brief"): `"Brief"` (краткий) или `"Full"` (полный)

### Поддерживаемые форматы (auto-detection)

**1. ECS/JSON Format** (Elastic Common Schema):
```json
{"@timestamp":"2025-11-13T15:23:45.123Z","log.level":"error","message":"Failed to connect","error.stacktrace":"..."}
```

**2. PlainText Format** (типичные .NET logs):
```
2025-11-13 15:23:45.123 [ERROR] Failed to connect to database
   at MyApp.Services.DbService.Connect() in DbService.cs:line 45
```

**3. Logcat Format** (Android):
```
11-13 15:23:45.123  1234  5678 E MyApp  : Fatal error occurred
```

**4. WebServer Format** (Apache/Nginx access logs):
```
192.168.1.1 - - [13/Nov/2025:15:23:45 +0000] "GET /api/users HTTP/1.1" 500 1234
```

**5. XML Format**:
```xml
<log>
<timestamp>2025-11-13T15:23:45</timestamp>
<level>ERROR</level>
<message>Failed to process</message>
</log>
```

**Auto-detection:**
- Анализирует первые несколько строк файла
- Определяет формат автоматически
- Парсит соответствующим парсером

### Что показывает

**Brief mode (default):**
- 📅 **Timestamp**
- ⚠️ **Level** (Error, Warning, Info, etc.)
- 💬 **Message** (основное сообщение)
- 🔥 **Stacktrace** (если есть)
- 🌐 **URL/Path** (для web server logs)
- 📄 **Context lines** (configurable)

**Full mode:**
- Всё из Brief +
- 🔍 **Raw log entry** (полная строка лога)
- 📊 **Additional fields** (зависит от формата)

### Пример вывода

```
Log Analysis: D:/Logs/application.log

Format detected: PlainText
Total lines scanned: 1,247,893
Matches found: 47
Showing: 47 results (skip: 0, take: 100)

═══════════════════════════════════════════════════════════
⚠️ FILTERS APPLIED
═══════════════════════════════════════════════════════════

Levels: Error, Fatal
Keywords: None
Status codes: None

═══════════════════════════════════════════════════════════
🔴 RESULTS (47 matches)
═══════════════════════════════════════════════════════════

[1] Line 12,345
────────────────────────────────────────────────────────────
📅 Timestamp: 2025-11-13 14:23:45.123
⚠️ Level: ERROR
💬 Message: Failed to connect to database server

🔥 Stacktrace:
   at MyApp.Services.DatabaseService.Connect() in DatabaseService.cs:line 45
   at MyApp.Services.UserService.GetUserAsync(Int32 id) in UserService.cs:line 89
   at MyApp.API.Controllers.UserController.Get(Int32 id) in UserController.cs:line 34

📄 Context (5 lines before, 5 lines after):
12340 | 2025-11-13 14:23:44.890 [INFO] Processing user request
12341 | 2025-11-13 14:23:44.912 [DEBUG] Validating user ID: 12345
12342 | 2025-11-13 14:23:44.934 [DEBUG] Opening database connection
12343 | 2025-11-13 14:23:45.001 [DEBUG] Connection string: Server=...
12344 | 2025-11-13 14:23:45.098 [WARNING] Connection timeout reached
12345 | 2025-11-13 14:23:45.123 [ERROR] Failed to connect to database
12346 | 2025-11-13 14:23:45.156 [ERROR] Retrying connection (attempt 1/3)
12347 | 2025-11-13 14:23:46.234 [ERROR] Retrying connection (attempt 2/3)
12348 | 2025-11-13 14:23:47.345 [ERROR] Retrying connection (attempt 3/3)
12349 | 2025-11-13 14:23:48.456 [FATAL] Database connection failed permanently
12350 | 2025-11-13 14:23:48.500 [INFO] Returning error to client

[2] Line 23,456
────────────────────────────────────────────────────────────
📅 Timestamp: 2025-11-13 14:25:12.789
⚠️ Level: ERROR
💬 Message: NullReferenceException: Object reference not set

🔥 Stacktrace:
   at MyApp.Services.OrderService.ProcessOrder(Order order) in OrderService.cs:line 123
   at MyApp.Services.OrderProcessor.Execute() in OrderProcessor.cs:line 67

... (45 more results)

═══════════════════════════════════════════════════════════
💡 RECOMMENDATIONS
═══════════════════════════════════════════════════════════

Top error patterns:
1. "Failed to connect to database" - 18 occurrences
2. "NullReferenceException" - 12 occurrences
3. "Timeout waiting for response" - 8 occurrences

Suggested actions:
- Check database connection settings
- Review null checks in OrderService.cs:123
- Investigate timeout configuration

💡 Use TraceBackwards to analyze crash points
💡 Example: UltrasharpTool_TraceBackwards(
    crashPointFqn: "MyApp.Services.OrderService.ProcessOrder",
    stackTraceHints: ["at MyApp.Services.OrderService.ProcessOrder"]
)
```

### Когда использовать

✅ **Production debugging:**
- Анализ errors в production logs
- Поиск причины crash
- Tracking конкретной ошибки
- Investigation incident

✅ **Performance analysis:**
- Поиск slow requests (Web server logs)
- Найти HTTP 5xx errors
- Анализ timeout patterns

✅ **Security audit:**
- Поиск 401/403 errors
- Анализ suspicious activity
- Track login failures

✅ **Monitoring:**
- Регулярный анализ errors
- Trending error patterns
- Alert на новые типы ошибок

### Когда НЕ использовать

❌ **НЕ для:**
- Real-time log monitoring (используйте специализированные tools: ELK, Splunk)
- Полнотекстовый поиск (используйте grep для простых случаев)
- Парсинг custom форматов (расширяйте AnalyzeLogs или используйте custom scripts)

### Best Practices

1. **Начните с фильтра по level:**
   ```javascript
   // ✅ Сначала смотрим критичные
   UltrasharpTool_AnalyzeLogs(
       filePath: "app.log",
       levels: ["Fatal"],
       ...
   )

   // Затем errors
   UltrasharpTool_AnalyzeLogs(
       filePath: "app.log",
       levels: ["Error"],
       ...
   )
   ```

2. **Используйте keywords для уточнения:**
   ```javascript
   // Ищем конкретную ошибку
   UltrasharpTool_AnalyzeLogs(
       filePath: "app.log",
       levels: ["Error"],
       keywords: ["DatabaseService", "Connect"],
       ...
   )
   ```

3. **Pagination для больших результатов:**
   ```javascript
   // Первая страница
   UltrasharpTool_AnalyzeLogs(..., skip: 0, take: 100)

   // Вторая страница
   UltrasharpTool_AnalyzeLogs(..., skip: 100, take: 100)
   ```

4. **Настройте context для понимания:**
   ```javascript
   // Больше контекста для сложных случаев
   UltrasharpTool_AnalyzeLogs(
       ...,
       contextBefore: 10,  // 10 строк до
       contextAfter: 10    // 10 строк после
   )
   ```

5. **Комбинируйте с TraceBackwards:**
   ```javascript
   // 1. Найти error в логах
   UltrasharpTool_AnalyzeLogs(
       filePath: "app.log",
       keywords: ["NullReferenceException"],
       ...
   )
   // Output: stacktrace с "OrderService.ProcessOrder"

   // 2. Trace backwards в коде
   UltrasharpTool_TraceBackwards(
       crashPointFqn: "MyApp.Services.OrderService.ProcessOrder",
       stackTraceHints: [
           "at MyApp.Services.OrderService.ProcessOrder",
           "at MyApp.Services.OrderProcessor.Execute"
       ]
   )
   ```

### Memory Efficiency

**Streaming approach:**
- ✅ Файл читается построчно (не загружается весь в память)
- ✅ Обрабатывается on-the-fly
- ✅ Может обработать multi-GB файлы

**Memory usage:**
```
Small log (< 10 MB):       < 50 MB RAM
Medium log (10-100 MB):    < 100 MB RAM
Large log (100-1000 MB):   < 150 MB RAM
Huge log (> 1 GB):         < 200 MB RAM
```

**Скорость:**
```
Small log (< 10 MB):       1-3 сек
Medium log (10-100 MB):    5-15 сек
Large log (100-1000 MB):   30-90 сек
Huge log (> 1 GB):         60-180 сек
```

### Производительность (примеры)

**Real-world benchmarks:**
```
File: 500 MB, 5 million lines
Filter: levels=["Error"]
Time: 45 seconds
Memory: 120 MB
Results: 3,247 errors found
```

```
File: 2 GB, 20 million lines
Filter: keywords=["OutOfMemory"]
Time: 142 seconds
Memory: 180 MB
Results: 18 instances found
```

### Примеры фильтров

```javascript
// Все errors и fatals
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    levels: ["Error", "Fatal"]
)

// HTTP 5xx errors
UltrasharpTool_AnalyzeLogs(
    filePath: "access.log",
    statusCodes: [500, 502, 503, 504]
)

// HTTP 4xx errors
UltrasharpTool_AnalyzeLogs(
    filePath: "access.log",
    statusCodes: [400, 401, 403, 404]
)

// Поиск exceptions
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["Exception", "Error", "Failed"]
)

// Поиск specific exception
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["NullReferenceException"]
)

// Database errors
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["Database", "SQL", "Connection"]
)

// Timeout issues
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["Timeout", "TimeoutException"]
)

// Memory issues
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["OutOfMemoryException", "GC", "Heap"]
)
```

### Типичные workflow

#### Debugging Production Crash

```javascript
// 1. Найти crash в логах
UltrasharpTool_AnalyzeLogs(
    filePath: "production-2025-11-13.log",
    levels: ["Fatal", "Error"],
    skip: 0,
    take: 50
)
// Output: Found "NullReferenceException at OrderService.ProcessOrder"

// 2. Понять контекст
UltrasharpTool_AnalyzeLogs(
    filePath: "production-2025-11-13.log",
    keywords: ["OrderService"],
    contextBefore: 20,
    contextAfter: 20
)
// Смотрим что происходило до/после

// 3. Trace backwards в коде
UltrasharpTool_LoadSolution("MyApp.sln")
UltrasharpTool_TraceBackwards(
    crashPointFqn: "MyApp.Services.OrderService.ProcessOrder",
    stackTraceHints: [...]
)

// 4. Анализ кода
UltrasharpTool_ViewDefinition("MyApp.Services.OrderService.ProcessOrder")
UltrasharpTool_FindReferences("MyApp.Services.OrderService.ProcessOrder")

// 5. Fix и deploy
```

#### Performance Investigation

```javascript
// 1. Найти slow requests
UltrasharpTool_AnalyzeLogs(
    filePath: "access.log",
    statusCodes: [200],  // Success но медленные
    keywords: ["slow", "timeout"]
)

// 2. Найти 5xx errors (server errors)
UltrasharpTool_AnalyzeLogs(
    filePath: "access.log",
    statusCodes: [500, 502, 503]
)

// 3. Анализ паттернов
// Какие endpoints больше всего errors?
// В какое время суток?
// Correlation с другими events?

// 4. Fix performance issues в коде
```

#### Security Audit

```javascript
// 1. Unauthorized access attempts
UltrasharpTool_AnalyzeLogs(
    filePath: "access.log",
    statusCodes: [401, 403]
)

// 2. Suspicious patterns
UltrasharpTool_AnalyzeLogs(
    filePath: "app.log",
    keywords: ["injection", "attack", "malicious"]
)

// 3. Failed login attempts
UltrasharpTool_AnalyzeLogs(
    filePath: "auth.log",
    keywords: ["failed", "invalid password"]
)

// 4. Review и harden security
```

---

## Сравнение с другими debugging tools

| Инструмент | Назначение | Input | Output |
|------------|-----------|-------|--------|
| **AnalyzeLogs** | Поиск в production logs | Log files | Filtered entries |
| **TraceExecution** | Static code flow analysis | Entry point FQN | Execution trace |
| **TraceBackwards** | Find call paths to crash | Crash point FQN | Possible paths |

**Комбинированный workflow:**
1. `AnalyzeLogs` — найти error в production logs
2. `TraceBackwards` — найти как дошли до crash point
3. `ViewDefinition` — посмотреть код crash point
4. `FindReferences` — найти все call sites
5. `OverwriteMember` — исправить bug
6. Deploy и monitor logs

---

## Расширение AnalyzeLogs

### Добавление нового формата

```csharp
// В LogFormatDetector.cs
public enum LogFormat
{
    ECS,
    PlainText,
    Logcat,
    WebServer,
    XML,
    YourCustomFormat  // ← Add here
}

// В LogAnalysisService.cs
private ILogParser GetParser(LogFormat format)
{
    return format switch
    {
        LogFormat.YourCustomFormat => new YourCustomLogParser(),
        ...
    };
}

// Создать YourCustomLogParser.cs:ILogParser
```

### Кастомизация вывода

```csharp
// В AnalyzeLogs tool
// Можно настроить:
- Default context lines
- Default detail level
- Default take/skip
- Custom filtering logic
```

---

## См. также

- 📚 [**TRACING_TOOLS.md**](TRACING_TOOLS.md) — TraceExecution, TraceBackwards для code analysis
- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — ViewDefinition, FindReferences
- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — fix bugs найденные через logs
- 📚 [**README.md**](../README.md) — главная документация

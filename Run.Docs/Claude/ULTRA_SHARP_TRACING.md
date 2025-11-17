# Execution Tracing & Debugging

[← Back to Overview](./ULTRA_SHARP.md)

**Static analysis of execution flows** — trace forward (from entry point) and backward (from crash point) to understand logic and debug production issues.

---

## 📋 Quick Reference

| Tool | Direction | Input | Output | Use Case |
|------|-----------|-------|--------|----------|
| **TraceExecution** | Forward (→) | Entry point FQN | Execution trace | Understand what code does |
| **TraceBackwards** | Backward (←) | Crash point FQN | Multiple paths (cached ⚡) | Understand how reached error |
| **AnalyzeLogs** | - | Log file | Errors, stack traces | Find production issues |

---

## trace_execution

**Forward tracing** — from entry point to exit point.

### Usage

```javascript
trace_execution(
    entryPointFqn: "TestTracing.UserService.ProcessUser",
    exitPointFqn: "TestTracing.DatabaseService.SaveUser",  // optional
    maxDepth: 10,
    includeExternalCalls: true
)
```

### Parameters

- **entryPointFqn** (required): FQN of method to start tracing from
- **exitPointFqn** (optional): FQN of method to stop tracing at
- **maxDepth** (default: 10): Maximum call depth (1-50)
- **includeExternalCalls** (default: true): Show external library calls

### What It Shows

- 🚀 **Entry point** — entry point with parameters
- 📞 **Method calls** — method invocations
- ✏️ **Assignments** — variable assignments
- 🆕 **Object creation** — object instantiation
- 🔀 **Branches** — control flow (if/switch)
- ↩️ **Returns** — return values
- 📦 **External calls** — external library calls (signature only)

### When to Use

✅ **For understanding new code:**
- How feature works end-to-end
- What happens inside API endpoint
- Call sequence in business logic

✅ **For debugging:**
- Understand where data comes from
- Trace data flow through methods
- Find where data transformation happens

✅ **For documentation:**
- Create flow diagrams
- Understand sequence diagrams
- Document complex workflows

### Best Practices

1. **Start with small depth:**
   ```javascript
   // ✅ First shallow trace
   trace_execution(entryPointFqn: "UserService.ProcessUser", maxDepth: 5)

   // If need more details
   trace_execution(entryPointFqn: "UserService.ProcessUser", maxDepth: 15)
   ```

2. **Use exitPoint for focus:**
   ```javascript
   // Trace only to specific point
   trace_execution(
       entryPointFqn: "UserController.Post",
       exitPointFqn: "DatabaseService.SaveUser"
   )
   ```

3. **Disable external calls if not needed:**
   ```javascript
   // Only your code
   trace_execution(
       entryPointFqn: "...",
       includeExternalCalls: false
   )
   ```

4. **Combine with view_definition:**
   ```javascript
   // Trace shows what's called
   trace_execution(entryPointFqn: "ProcessUser")
   // Output: "Calls ValidateUser, CreateUser, SaveUser"

   // ViewDefinition shows details of each
   view_definition("UserService.ValidateUser")
   view_definition("UserService.CreateUser")
   ```

### Performance

- **Shallow (depth 1-5):** 1-3 sec
- **Medium (depth 6-15):** 3-10 sec
- **Deep (depth 16-50):** 10-30 sec

**Factors:**
- Trace depth
- CFG (Control Flow Graph) complexity
- Number of branches
- External calls

### Related Tools

- ⬅️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — view entry point before trace
- ➡️ [**TraceBackwards**](#trace_backwards) — reverse direction
- ➡️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — where entry point is called
- ➡️ [**AnalyzeLogs**](#analyze_logs) — find entry point in production logs

---

## trace_backwards

**Backward tracing** — from crash/error point to possible entry points.

### Usage

```javascript
trace_backwards(
    crashPointFqn: "TestTracing.UserService.ThrowInvalidUserException",
    startPointFqn: "TestTracing.Program.Main",  // optional
    stackTraceHints: [
        "at TestTracing.UserService.CreateUser",
        "at TestTracing.UserService.ProcessUser",
        "at TestTracing.Program.Main"
    ],
    maxDepth: 15,
    maxPaths: 5,
    includeExternalCallers: false
)
```

### Parameters

- **crashPointFqn** (required): FQN of method where error occurred
- **startPointFqn** (optional): Expected entry point (if not specified, finds all entry points)
- **stackTraceHints** (optional): Stack trace lines for path ranking
- **maxDepth** (default: 15): Maximum search depth (1-50)
- **maxPaths** (default: 5): Maximum number of paths (1-20)
- **includeExternalCallers** (default: false): Include calls from external libraries

### What It Shows

- Multiple possible execution paths
- **Confidence score** for each path (based on stack trace)
- 📍 Markers for frames matching stack trace
- Call site information (where method was called)
- Method parameters

### When to Use

✅ **For debugging production crashes:**
- Have stack trace from production logs
- Need to understand HOW reached problematic method
- Find all possible paths to crash point

✅ **For impact analysis:**
- Before changing method - who can call it
- Understand all entry points leading to method
- Find unexpected call paths

✅ **For code review:**
- Check where critical code is called from
- Ensure validation happens before call
- Find missing error handling in call chain

### Best Practices

1. **ALWAYS use stack trace hints:**
   ```javascript
   // ✅ With hints - accurate results
   trace_backwards(
       crashPointFqn: "OrderService.ProcessOrder",
       stackTraceHints: [
           "at OrderService.ProcessOrder",
           "at OrderProcessor.Execute",
           "at BackgroundJob.Run"
       ]
   )

   // ⚠️ Without hints - may have many false positives
   trace_backwards(crashPointFqn: "OrderService.ProcessOrder")
   ```

2. **Start with small maxPaths:**
   ```javascript
   // First top 5 most likely
   trace_backwards(..., maxPaths: 5)

   // If not found - increase
   trace_backwards(..., maxPaths: 10)
   ```

3. **Specify startPoint if known:**
   ```javascript
   // If you know entry point
   trace_backwards(
       crashPointFqn: "...",
       startPointFqn: "MyController.Post"
   )
   ```

4. **Combine with analyze_logs:**
   ```javascript
   // 1. Find crash in production logs
   analyze_logs(filePath: "prod.log", levels: ["Fatal"])
   // Output: stack trace

   // 2. Trace backwards with stack trace hints
   trace_backwards(
       crashPointFqn: "...",
       stackTraceHints: [/* from logs */]
   )
   ```

### Performance

**⚡ With caching:**
- **Cold cache (first run):** 2-15 sec
- **Warm cache (repeat run):** **0.3-2 sec (5-10x faster!)**

**Time by depth:**
- **Shallow search (depth 1-5):**
  - Cold: 2-5 sec
  - Warm: 0.3-1 sec ✅
- **Medium search (depth 6-15):**
  - Cold: 5-15 sec
  - Warm: 1-3 sec ✅
- **Deep search (depth 16-30):**
  - Cold: 15-45 sec
  - Warm: 3-8 sec ✅

**Caching:**
- ✅ **SQLite persistent cache** stores full caller data with Location info
- ✅ **CallGraphFull table** with indices on MethodFqn, FilePath, Timestamp
- ✅ **Automatic invalidation** on solution hash change
- ✅ **JSON Source Generation** for 2-5x faster serialization
- ✅ **CachedCallerInfo wrapper** for unified cache HIT/MISS paths

**Cache hit rate:** 80-95% for typical workflows

**Confidence scoring:**
- 90-100%: Perfect match with stack trace
- 70-89%: Partial match
- 50-69%: Possible path
- < 50%: Unlikely path

### Related Tools

- ⬅️ [**AnalyzeLogs**](#analyze_logs) — get stack trace from production logs
- ⬅️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — view crash point before trace
- ➡️ [**TraceExecution**](#trace_execution) — forward direction for logic understanding
- ➡️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — all references (not just call paths)

---

## analyze_logs

**Log analysis with auto-format detection** — search for errors, exceptions, HTTP errors in production logs without loading entire file into memory.

### Usage

```javascript
// Basic search
analyze_logs(
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

// Search for HTTP errors
analyze_logs(
    filePath: "D:/Logs/access.log",
    levels: null,
    keywords: null,
    statusCodes: [404, 500, 503],
    skip: 0,
    take: 50
)

// Search by keywords
analyze_logs(
    filePath: "D:/Logs/app.log",
    levels: null,
    keywords: ["NullReferenceException", "OutOfMemory", "Timeout"],
    skip: 0,
    take: 100
)
```

### Parameters

- **filePath** (required): Path to log file
- **levels** (optional): Filter by levels (`["Verbose", "Debug", "Info", "Warning", "Error", "Fatal"]`)
- **keywords** (optional): Keywords to search (case-insensitive, searches in message and stacktrace)
- **statusCodes** (optional): HTTP status codes (`[404, 500, 503, ...]`)
- **skip** (default: 0): Skip N results (pagination)
- **take** (default: 100): Return N results
- **contextBefore** (default: 5): Lines of context BEFORE match
- **contextAfter** (default: 5): Lines of context AFTER match
- **detailLevel** (default: "Brief"): `"Brief"` (concise) or `"Full"` (verbose)

### Supported Formats (auto-detection)

**1. ECS/JSON Format** (Elastic Common Schema):
```json
{"@timestamp":"2025-11-13T15:23:45.123Z","log.level":"error","message":"Failed to connect","error.stacktrace":"..."}
```

**2. PlainText Format** (typical .NET logs):
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
- Analyzes first few lines of file
- Determines format automatically
- Parses with appropriate parser

### What It Shows

**Brief mode (default):**
- 📅 **Timestamp**
- ⚠️ **Level** (Error, Warning, Info, etc.)
- 💬 **Message** (main message)
- 🔥 **Stacktrace** (if present)
- 🌐 **URL/Path** (for web server logs)
- 📄 **Context lines** (configurable)

**Full mode:**
- Everything from Brief +
- 🔍 **Raw log entry** (full log line)
- 📊 **Additional fields** (format-dependent)

### When to Use

✅ **Production debugging:**
- Analyze errors in production logs
- Search for crash causes
- Find stack traces
- Identify error patterns

✅ **For TraceBackwards:**
- Extract stack trace from logs
- Use as stackTraceHints
- Understand crash context

✅ **Monitoring & analytics:**
- Track error frequency
- Find HTTP 500 patterns
- Monitor timeout issues

### Best Practices

1. **Start with specific filters:**
   ```javascript
   // ✅ Good - focused search
   analyze_logs(
       filePath: "app.log",
       levels: ["Error", "Fatal"],
       keywords: ["NullReferenceException"]
   )

   // ⚠️ Too broad - many results
   analyze_logs(filePath: "app.log", levels: ["Info"])
   ```

2. **Use context to understand errors:**
   ```javascript
   // See what happened before/after error
   analyze_logs(
       filePath: "app.log",
       levels: ["Error"],
       contextBefore: 10,
       contextAfter: 10
   )
   ```

3. **Combine with trace_backwards:**
   ```javascript
   // 1. Find error in logs
   analyze_logs(filePath: "prod.log", levels: ["Fatal"])
   // Output: stack trace lines

   // 2. Trace backwards in code
   trace_backwards(
       crashPointFqn: "UserService.ProcessUser",
       stackTraceHints: [/* stack trace from logs */]
   )
   ```

4. **Use pagination for large results:**
   ```javascript
   // First page
   analyze_logs(..., skip: 0, take: 100)

   // Next page
   analyze_logs(..., skip: 100, take: 100)
   ```

### Performance

- **Speed:** Fast (streaming)
- **Memory:** Low (doesn't load entire file)
- **Large files (GB):** Efficient processing

**Factors:**
- File size
- Number of matches
- Context lines requested
- Detail level

### Related Tools

- ➡️ [**TraceBackwards**](#trace_backwards) — analyze crash points found in logs
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — view methods mentioned in stack trace
- ➡️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — find where error-prone methods are called

---

## Workflow: Production Crash Analysis

```javascript
// 1. Find crash in production logs
analyze_logs(
    filePath: "D:/Logs/production.log",
    levels: ["Fatal", "Error"],
    keywords: ["Exception"],
    take: 50
)
// Output:
// - Timestamp: 2025-11-13 14:23:45
// - Message: "NullReferenceException in ProcessOrder"
// - Stacktrace:
//   at OrderService.ProcessOrder(Order order)
//   at OrderProcessor.Execute()
//   at BackgroundJob.Run()

// 2. Trace backwards with stack trace hints
trace_backwards(
    crashPointFqn: "OrderService.ProcessOrder",
    stackTraceHints: [
        "at OrderService.ProcessOrder",
        "at OrderProcessor.Execute",
        "at BackgroundJob.Run"
    ],
    maxDepth: 15
)
// Output: Shows all paths leading to crash with confidence scores

// 3. View crash point implementation
view_definition("OrderService.ProcessOrder")
// Understand what caused NullReferenceException

// 4. Fix the issue
modify_code(
    fullyQualifiedMemberName: "OrderService.ProcessOrder",
    newMemberCode: "/* fixed implementation */",
    commitMessage: "Fix null reference in ProcessOrder"
)
```

---

## Tool Comparison

| Tool | Direction | Speed | Cache | Best For |
|------|-----------|-------|-------|----------|
| **TraceExecution** | Forward → | 1-30 sec | No | Understanding logic |
| **TraceBackwards** | Backward ← | 0.3-45 sec | Yes ⚡ | Debugging crashes |
| **AnalyzeLogs** | - | Fast | No | Finding production issues |

---

## See Also

- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — view code before/after tracing
- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — fix issues found
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

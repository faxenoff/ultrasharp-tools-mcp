# Execution Tracing & Debugging

**Static analysis of execution flows** — forward and backward tracing, log analysis.

---

## Tools

| Tool | Direction | Use Case |
|------|-----------|----------|
| **trace_execution** | Forward (→) | Understand what code does |
| **trace_backwards** | Backward (←) | Understand how error was reached |
| **analyze_path_feasibility** | All paths | Verification and bug finding |
| **analyze_logs** | - | Production log analysis |

---

## trace_execution

**Forward tracing** — from entry point to exit point.

```javascript
trace_execution(
    entryPointFqn: "TestTracing.UserService.ProcessUser",
    exitPointFqn: "TestTracing.DatabaseService.SaveUser",  // optional
    maxDepth: 10,
    includeExternalCalls: true
)
```

**Shows:**
- Entry point with parameters
- Method calls
- Variable assignments
- Branches (if/switch)
- External calls (signature only)

**When to use:**
- Understand how feature works
- Trace data flow
- Document complex workflows

---

## trace_backwards

**Backward tracing** — from crash point to possible entry points.

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
    maxPaths: 5
)
```

**Shows:**
- Multiple possible paths
- **Confidence score** for each path
- Stack trace match markers
- Method parameters

**Caching:**
- ✅ SQLite persistent cache
- Cold: 2-15 sec, Warm: **0.3-2 sec (5-10x faster!)**

---

## analyze_path_feasibility

**Symbolic execution** — analyze all paths via Z3 SMT solver.

```javascript
analyze_path_feasibility(
    entryPointFqn: "TestTracing.Calculator.Divide",
    exitPointFqn: "TestTracing.Calculator.ThrowException",  // optional
    maxDepth: 10,
    initialConstraints: {
        "x": "x > 0",
        "y": "y != 0"
    }
)
```

**Detects issues:**
- Division by zero
- Null reference
- Array out of bounds
- Dead code (unreachable paths)

---

## analyze_logs

**Production log analysis** — auto-format detection, efficient streaming.

```javascript
analyze_logs(
    filePath: "D:/Logs/application.log",
    levels: ["Error", "Fatal"],
    keywords: ["NullReferenceException", "Timeout"],
    statusCodes: [500, 503],
    skip: 0,
    take: 100,
    contextBefore: 5,
    contextAfter: 5,
    detailLevel: "Brief"  // or "Full"
)
```

**Supported formats:**
- ECS/JSON (Elastic Common Schema)
- PlainText (.NET logs)
- Logcat (Android)
- WebServer (Apache/Nginx)
- XML

**Memory efficient:**
- Streaming — doesn't load entire file
- < 200 MB RAM even for multi-GB files

---

## Debugging Workflow

```javascript
// 1. Find crash in logs
analyze_logs(
    filePath: "production.log",
    levels: ["Fatal", "Error"],
    take: 50
)
// Output: NullReferenceException at OrderService.ProcessOrder

// 2. Trace backwards in code
load_solution("MyApp.sln")
trace_backwards(
    crashPointFqn: "MyApp.Services.OrderService.ProcessOrder",
    stackTraceHints: [/* from logs */]
)

// 3. View problematic code
view_definition("MyApp.Services.OrderService.ProcessOrder")

// 4. Fix and verify
modify_code(...)
```

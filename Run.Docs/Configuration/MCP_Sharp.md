---
type: guide
purpose: C# development with SharpTools MCP
ai-context: Read when working with C# / .NET projects
created: 2025-01-17
tags: [csharp, mcp, roslyn, dotnet, sharptools]
---

# C# Development with SharpTools MCP

**Complete MCP suite for C# development: 36+ tools for analysis, modification, debugging, and quality.**

---

## 🚀 Quick Start (5 steps)

### 1. Initialize workspace
```
UltrasharpTool_LoadSolution("path/to/project.sln")
```
**ALWAYS do this first!** Without it, other tools won't work.

### 2. Understand project structure
```
UltrasharpTool_LoadProject(
    projectName: "MyApp",
    detailLevel: "TypesAndSignatures"  // or "TypesOnly" for overview
)
```
**30 seconds vs 30 minutes** of manual exploration.

### 3. Find code (semantic search!)
```
UltrasharpTool_FindPotentialDuplicates(
    targetCode: "async Task<IActionResult> ProcessRequest(HttpContext ctx)",
    threshold: 0.7
)
```
**Finds by MEANING, not by name!** No FQN needed.

### 4. Modify code (auto-linting included)
```
UltrasharpTool_OverwriteMember(
    fullyQualifiedTargetName: "MyApp.Services.UserService.ValidateEmail",
    codeSnippet: "/* new implementation */",
    commitMessage: "Improve email validation"
)
```
**Response includes:** compilation check, linting, quality report.

### 5. Ensure quality
```
UltrasharpTool_FormatCode(path: "src/", checkOnly: false)
UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
```
**Auto-format + auto-fix** common issues.

---

## ⚡ Performance Comparison

| Task | Manual | With MCP | Speedup |
|------|--------|----------|---------|
| Understand project | 20-40 min | 30 sec | **40x** |
| Find method usage | 5-10 min | 5 sec | **60-120x** |
| Debug crash | 15-30 min | 15 sec | **60-120x** |
| Analyze logs | 10-20 min | 5 sec | **120-240x** |

**Bottom line:** Use MCP tools immediately, don't waste time manually.

---

## 🔍 Semantic Search - Your First Tool

**Problem:** "I don't know exact class name, can't use MCP tools"
**Solution:** Semantic search finds code by MEANING!

### Examples:

**Where are HTTP requests handled?**
```
FindPotentialDuplicates(
    targetCode: "async Task<IActionResult> HandleRequest(HttpContext ctx)",
    threshold: 0.7
)
→ Finds all HTTP handlers, even with different names
```

**Where are errors logged to database?**
```
FindPotentialDuplicates(
    targetCode: "logger.LogError(ex); await db.SaveAsync();",
    threshold: 0.6
)
→ Finds all error logging + DB save patterns
```

**Are there duplicate validations?**
```
FindPotentialDuplicates(
    targetCode: "bool IsValidEmail(string email) { return email.Contains('@'); }",
    threshold: 0.8
)
→ Finds similar validation methods
```

**When to use:**
- ✅ Unfamiliar codebase
- ✅ Don't know exact class/method name
- ✅ Looking for specific functionality
- ✅ Finding duplicates

---

## 🛠️ Tool Categories

### 1️⃣ Solution & Navigation (ALWAYS start here!)
- **LoadSolution** - Initialize workspace (**REQUIRED**)
- **LoadProject** - Get project structure (namespaces → types → members)

### 2️⃣ Search & Analysis
- **FindPotentialDuplicates** - Semantic search (no FQN needed!)
- **SearchDefinitions** - Regex search in declarations
- **FindReferences** - Find usage with context
- **ViewDefinition** - View source with Roslyn context
- **GetMembers** - List type members with signatures
- **AnalyzeComplexity** - Complexity metrics

### 3️⃣ Quality Tools
- **FormatCode** - CSharpier formatting (check/apply modes)
- **AnalyzeCodeStyle** - Roslyn linting (IDE/CS/CA rules)
- **ApplyCodeFixes** - Auto-fix common issues (preview/apply modes)

### 4️⃣ Modification (with auto-linting!)
- **AddMember** - Add methods, properties, classes
- **OverwriteMember** - Modify or delete members
- **RenameSymbol** - Rename with all references updated
- **FindAndReplace** - Regex find/replace
- **MoveMember** - Move code between types
- **Undo** - Revert last change (Git-powered)

**⚠️ All modifications include automatic linting in response!**

### 5️⃣ Debugging & Diagnostics
- **TraceExecution** - CFG-based execution tracing (all paths)
- **TraceBackwards** - Reverse CFG (backtrace from crash)
- **AnalyzeLogs** - Log analysis (**5 formats**, auto-detection)
- **ExportCallGraph** - Mermaid/DOT visualization
- **AnalyzePathFeasibility** - Z3 symbolic execution

**Supported log formats:**
- ✅ ECS/JSON (Elastic Common Schema)
- ✅ Logcat (Android)
- ✅ WebServer (Apache/Nginx)
- ✅ XML (structured logs)
- ✅ PlainText (custom formats)

---

## 📋 Common Workflows

### Workflow 1: Exploring Unfamiliar Codebase
```
1. LoadSolution("project.sln")
2. LoadProject(projectName: "MyApp", detailLevel: "TypesOnly")
3. FindPotentialDuplicates(targetCode: "example of what I'm looking for")
4. ViewDefinition(fullyQualifiedName: "...")
5. FindReferences(fullyQualifiedName: "...")
```
**Time:** 2-3 minutes vs 1-2 hours manually.

**📖 Details:** [Run.Docs/Guides/Exploring-Codebase.md](../Guides/Exploring-Codebase.md)

---

### Workflow 2: Debugging Crash
```
1. AnalyzeLogs("logs/error.log", keywords: ["NullReferenceException"])
2. TraceBackwards(fullyQualifiedName: "...", stackTraceHint: "...")
3. TraceExecution(fullyQualifiedName: "...", maxDepth: 3)
4. ViewDefinition(fullyQualifiedName: "...")
5. OverwriteMember(...) // fix the bug
```
**Time:** 5-10 minutes vs 1-2 hours manually.

**📖 Details:** [Run.Docs/Guides/Debugging-Crash.md](../Guides/Debugging-Crash.md)

---

### Workflow 3: Refactoring Code
```
1. FindPotentialDuplicates(targetCode: "...", threshold: 0.8)
2. AnalyzeComplexity(fullyQualifiedName: "...", includeMembers: true)
3. OverwriteMember(...) // simplify/consolidate
4. FormatCode(path: "src/", checkOnly: false)
5. AnalyzeCodeStyle(severityFilter: "Warning")
6. ApplyCodeFixes(diagnosticId: "all", preview: false)
```
**Time:** 15-20 minutes vs 2-3 hours manually.

**📖 Details:** [Run.Docs/Guides/Refactoring.md](../Guides/Refactoring.md)

---

### Workflow 4: Code Review
```
1. AnalyzeCodeStyle(severityFilter: "Warning")
2. AnalyzeComplexity(fullyQualifiedName: "NewFeature.Service")
3. FindPotentialDuplicates(targetCode: "new method", threshold: 0.85)
4. FormatCode(path: "src/NewFeature/", checkOnly: true)
5. ApplyCodeFixes(diagnosticId: "all", preview: true)
```
**Time:** 5-10 minutes vs 30-60 minutes manually.

**📖 Details:** [Run.Docs/Guides/Code-Review.md](../Guides/Code-Review.md)

---

## ⚠️ Critical Rules

### 1. ALWAYS start with LoadSolution
```
❌ WRONG: ViewDefinition(...) // FAIL: workspace not initialized
✅ RIGHT: LoadSolution("...") → ViewDefinition(...) // Works
```

### 2. Use semantic search when you don't know FQN
```
❌ WRONG: "Need exact class name to use MCP tools"
✅ RIGHT: FindPotentialDuplicates → get FQN → use other tools
```

### 3. Don't read code manually if MCP can do it
```
❌ WRONG: "Let me read all files to understand structure"
✅ RIGHT: LoadProject(detailLevel: "TypesAndSignatures") // 30 sec
```

### 4. Trust automatic linting
```
✅ Every modification includes:
   - Compilation check
   - Roslyn linting
   - Quality report

→ Use ApplyCodeFixes or AnalyzeCodeStyle if warnings appear
```

### 5. Don't skip quality checks
```
❌ WRONG: OverwriteMember(...) → commit without checks
✅ RIGHT:
   OverwriteMember(...) // auto-linting in response
   → FormatCode(checkOnly: false)
   → ApplyCodeFixes(diagnosticId: "all")
   → Now safe to commit
```

### 6. Use Git integration
```
✅ All modifications automatically:
   - Create branches: ultrasharptools/YYYYMMDD-HHMMSS
   - Commit changes
   - Can revert via Undo

→ Don't fear experimenting!
```

---

## 🆕 New Features (2025)

### ✅ Semantic Search
- Vector embeddings (768-dim)
- Finds code by meaning, not name
- Adaptive vector store (SqliteVec / Vectorlite HNSW)
- **Use FIRST** when exploring unfamiliar code

### ✅ Quality Tools Suite
- **FormatCode** - CSharpier integration
- **AnalyzeCodeStyle** - All Roslyn analyzers (IDE, CS, CA)
- **ApplyCodeFixes** - Auto-fix common issues
- **Saves 30-60 minutes** on manual fixes

### ✅ Advanced Tracing & Debugging
- **TraceExecution** - CFG-based tracing (all execution paths)
- **TraceBackwards** - Reverse CFG (find all paths TO crash)
- **AnalyzePathFeasibility** - Z3 constraint solver
- **ExportCallGraph** - Mermaid/DOT visualization
- **SQLite caching** - 5-10x speedup on repeated queries

### ✅ Log Analysis (5 formats)
- ECS/JSON, Logcat, WebServer, XML, PlainText
- Auto-detection of format
- Filtering: log level, keywords, status codes, time range
- Pagination for large files
- Automatic statistics

### ✅ Automatic Linting
- **On EVERY code modification**
- Parallel processing (only modified files)
- Immediate feedback in tool response
- Saves API calls (no separate linting request needed)

### ✅ Git Integration
- Auto-branches: `ultrasharptools/YYYYMMDD-HHMMSS`
- Auto-commits after modifications
- Undo via Git
- Can disable via `--disable-git`

---

## 📖 Detailed Documentation

**Level 2 (You are here):** Language-specific guide with examples
**Level 3:** Detailed workflows and tool guides

### Tool Categories:
- [Run.Docs/Tools/Solution.md](../Tools/Solution.md) - LoadSolution, LoadProject
- [Run.Docs/Tools/Analysis.md](../Tools/Analysis.md) - Search, View, Analyze
- [Run.Docs/Tools/Quality.md](../Tools/Quality.md) - Format, Lint, Auto-fix
- [Run.Docs/Tools/Modification.md](../Tools/Modification.md) - Add, Modify, Rename
- [Run.Docs/Tools/Tracing.md](../Tools/Tracing.md) - Debug, Trace, Logs

### Workflow Guides:
- [Run.Docs/Guides/Exploring-Codebase.md](../Guides/Exploring-Codebase.md)
- [Run.Docs/Guides/Debugging-Crash.md](../Guides/Debugging-Crash.md)
- [Run.Docs/Guides/Refactoring.md](../Guides/Refactoring.md)
- [Run.Docs/Guides/Code-Review.md](../Guides/Code-Review.md)
- [Run.Docs/Guides/Semantic-Search.md](../Guides/Semantic-Search.md)

---

**💡 Remember:** SharpTools MCP is 10-100x faster than manual work.

**Use it FIRST, not as fallback → Save hours every day.**

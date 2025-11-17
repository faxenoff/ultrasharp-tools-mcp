---
type: guide
purpose: C# development with MCP tools (SharpToolsMCP, Code Graph RAG)
created: 2025-01-15
updated: 2025-01-12
ai-context: Use this when working with C# projects
tags: [csharp, mcp, roslyn, dotnet, sharptools, debugging, tracing, logs]
---

# C# Development with MCP Tools

You have TWO MCP servers for C# development. Each has specific strengths.

## 🎯 Decision Tree - Which Tool to Use?

```
Question: What do you need to do?

├─ UNDERSTAND code architecture/concepts?
│  └─ Code Graph RAG → semantic_search
│
├─ SEARCH for C# symbols?
│  ├─ Need regex search across code?
│  │  └─ SharpToolsMCP → UltrasharpTool_SearchDefinitions
│  └─ Need contextual snippets?
│     └─ SharpToolsMCP → UltrasharpTool_FindReferences
│
├─ ANALYZE code quality?
│  ├─ Formatting issues?
│  │  └─ SharpToolsMCP → UltrasharpTool_FormatCode (check mode)
│  ├─ Code style/diagnostics?
│  │  └─ SharpToolsMCP → UltrasharpTool_AnalyzeCodeStyle
│  ├─ Complexity metrics?
│  │  └─ SharpToolsMCP → UltrasharpTool_AnalyzeComplexity
│  └─ Impact of changes?
│     └─ Code Graph RAG → analyze_impact
│
├─ DEBUG or DIAGNOSE issues?
│  ├─ Trace code execution flow?
│  │  └─ SharpToolsMCP → UltrasharpTool_TraceExecution (CFG analysis)
│  ├─ Find crash/error origin?
│  │  └─ SharpToolsMCP → UltrasharpTool_TraceBackwards (with stack trace hints)
│  └─ Analyze log files?
│     └─ SharpToolsMCP → UltrasharpTool_AnalyzeLogs (5 formats supported)
│
├─ MODIFY C# code?
│  ├─ Format code?
│  │  └─ SharpToolsMCP → UltrasharpTool_FormatCode (apply mode)
│  ├─ Fix diagnostics?
│  │  └─ SharpToolsMCP → UltrasharpTool_ApplyCodeFixes
│  ├─ Refactor/add/change members?
│  │  └─ SharpToolsMCP → UltrasharpTool_AddMember/OverwriteMember/RenameSymbol
│  └─ Note: ALL modifications include automatic linting!
│
└─ GET detailed info about symbol?
   └─ SharpToolsMCP → UltrasharpTool_ViewDefinition (with call graphs)
```

## 🛠️ Tool 1: SharpToolsMCP - Complete C# Development Suite

### Primary Use Cases
✅ **Modifying C# code** with automatic linting
✅ **Code quality** (formatting, linting, auto-fixes)
✅ **Debugging & diagnostics** (tracing, backtrace, log analysis)
✅ Deep analysis & navigation
✅ Creating new C# files
✅ Source code from external libraries (decompilation, SourceLink)
✅ Git operations (auto-commit, undo)
✅ Managing usings and attributes

### 🌟 NEW FEATURE: Automatic Linting on Modifications

**All modification operations now include automatic linting!**

When you call:
- `UltrasharpTool_AddMember`
- `UltrasharpTool_OverwriteMember`
- `UltrasharpTool_RenameSymbol`
- `UltrasharpTool_FindAndReplace`
- `UltrasharpTool_MoveMember`

The AI automatically receives a quality check report:
```
## 📊 Code Quality Check

❌ **2 error(s)**
⚠️ **3 warning(s)**

**Top issues:**
  ❌ **CS0103**: The name 'foo' does not exist in the current context
     `MyClass.cs:42`
  ⚠️ **CS8019**: Unnecessary using directive.
     `MyClass.cs:5`

💡 **Tip:** Use UltrasharpTool_AnalyzeCodeStyle for full analysis
```

**Benefits:**
- ✅ Immediate feedback - no separate linting request needed
- ✅ Fast parallel processing - only modified files checked
- ✅ Better AI decision making - understands code quality impact instantly
- ✅ Saves time and API calls

### Available Tool Categories

#### 1️⃣ Solution & Project Tools
- **UltrasharpTool_LoadSolution** - **ALWAYS start with this** (initializes workspace)
- **UltrasharpTool_LoadProject** - Get detailed project structure (namespaces → types)

#### 2️⃣ Analysis Tools
- **UltrasharpTool_GetMembers** - List members of a type with signatures and docs
- **UltrasharpTool_ViewDefinition** - View source with Roslyn context (call graphs, references)
- **UltrasharpTool_ListImplementations** - Find all implementations/derived classes
- **UltrasharpTool_FindReferences** - Find usages with contextual snippets
- **UltrasharpTool_SearchDefinitions** - Regex search across declarations
- **UltrasharpTool_AnalyzeComplexity** - Complexity metrics (cyclomatic, cognitive, coupling)

#### 3️⃣ Quality Tools (NEW!)
- **UltrasharpTool_FormatCode** - Format C# code using CSharpier
  - Modes: `checkOnly=true` (preview) or `checkOnly=false` (apply)
  - Supports .cs, .csproj, .xml files
  - Parallel directory processing

- **UltrasharpTool_AnalyzeCodeStyle** - Lint with Roslyn analyzers
  - Detects: IDE0005 (unused usings), CS8600 (null checks), CA rules
  - Filter by severity: Hidden, Info, Warning, Error
  - Pagination support for large results

- **UltrasharpTool_ApplyCodeFixes** - Auto-fix diagnostics
  - Modes: `preview=true` (review) or `preview=false` (apply)
  - Can fix specific diagnostic IDs or "all"
  - Creates git commit when applying

#### 4️⃣ Modification Tools (with Auto-Linting!)
- **UltrasharpTool_AddMember** - Add methods, properties, classes, etc.
- **UltrasharpTool_OverwriteMember** - Modify or delete members
- **UltrasharpTool_RenameSymbol** - Rename with all references updated
- **UltrasharpTool_FindAndReplace** - Regex find/replace in symbols or files
- **UltrasharpTool_MoveMember** - Move members between types/namespaces
- **UltrasharpTool_Undo** - Revert last change (Git-powered)

**⚠️ ALL modification tools now include automatic linting in response!**

#### 5️⃣ Document Tools
- **UltrasharpTool_ReadRawFromRoslynDocument** - Read file contents
- **UltrasharpTool_CreateRoslynDocument** - Create new files
- **UltrasharpTool_OverwriteRoslynDocument** - Overwrite existing files
- **UltrasharpTool_ReadTypesFromRoslynDocument** - List types in file

#### 6️⃣ Utility Tools
- **UltrasharpTool_ManageUsings** - Read/write using directives
- **UltrasharpTool_ManageAttributes** - Read/write attributes
- **UltrasharpTool_RequestNewTool** - Request new features

#### 7️⃣ Debugging & Diagnostics Tools (NEW!)
- **UltrasharpTool_TraceExecution** - Static analysis of code execution flow
  - Uses Roslyn Control Flow Graph (CFG)
  - Traces from entry point to optional exit point
  - Shows: method calls, variable operations, conditionals, object creation
  - No code execution required (static analysis)
  - Configurable depth and external call inclusion

- **UltrasharpTool_TraceBackwards** - Reverse tracing from crash/failure point
  - Uses Roslyn SymbolFinder to build call graphs
  - Finds all possible call paths leading to crash point
  - Supports stack trace hints for confidence scoring (up to 100% on perfect match)
  - Returns multiple paths ranked by likelihood
  - Ideal for debugging production crashes

- **UltrasharpTool_AnalyzeLogs** - Universal log file analyzer
  - Auto-detects format: ECS/JSON, PlainText, Logcat, WebServer, XML
  - Efficient search: keywords, log levels, status codes
  - Context capture: N lines before/after matches
  - Two detail levels: Brief (timestamp, level, message, stacktrace, url/path) or Full
  - Pagination support for large files
  - Streams large files without loading into memory

### Standard Workflow

```
1. UltrasharpTool_LoadSolution (once per session)
   └─ Initializes MSBuild workspace, loads solution

2. UltrasharpTool_LoadProject (optional, for project overview)
   └─ Get namespace → type structure

3. Analysis & Navigation
   ├─ UltrasharpTool_ViewDefinition - Understand existing code
   ├─ UltrasharpTool_GetMembers - See type members
   └─ UltrasharpTool_FindReferences - Find usages

4. Make Modifications (auto-commits to Git branch)
   ├─ UltrasharpTool_AddMember - Add new code
   ├─ UltrasharpTool_OverwriteMember - Change existing
   └─ UltrasharpTool_RenameSymbol - Rename symbols

5. Code Quality (if needed beyond auto-linting)
   ├─ UltrasharpTool_FormatCode - Format code
   ├─ UltrasharpTool_AnalyzeCodeStyle - Full lint check
   └─ UltrasharpTool_ApplyCodeFixes - Auto-fix issues

6. If Needed: UltrasharpTool_Undo
   └─ Git-powered undo of last change
```

### Example Commands

```
# Load solution
Load solution from D:/MyProject/MyProject.sln

# Format check
Check formatting for D:/MyProject/Services/UserService.cs without applying changes

# Format apply
Format all code in D:/MyProject/Services/ directory

# Lint analysis
Analyze code style for D:/MyProject/MyProject.sln with severity Warning, skip 0, take 100

# Auto-fix (preview)
Preview fixable diagnostics in D:/MyProject/MyProject.sln

# Auto-fix (apply)
Apply fixes for IDE0005 diagnostics in D:/MyProject/MyProject.sln

# Add member (with auto-linting!)
Add a new method ProcessUser to MyNamespace.UserService class

# View with context
Show definition of MyNamespace.UserService.ProcessUser with call graphs

# Find references
Find all references to MyNamespace.IUserService.GetUser

# Trace execution (debugging)
Trace execution from MyApp.Program.Main to MyApp.Services.UserService.ProcessUser

# Trace backwards (crash analysis)
Trace backwards from MyApp.Services.DatabaseService.ExecuteQuery with stack trace hints: ["ProcessUser", "Main"]

# Analyze logs
Analyze D:/logs/production.log for ERROR and EXCEPTION keywords with 10 context lines
```

### Key Features

**Token Efficiency:**
- Code returned without indentation (~10% token savings)
- FQN-based navigation (no need to read entire files)
- Adaptive detail levels in LoadProject

**Git Integration:**
- Auto-creates `sharptools/YYYYMMDD-HHMMSS` branches
- Auto-commits every change with descriptive messages
- Git-powered Undo
- Can be disabled with `--disable-git`

**Automatic Linting:**
- Runs after EVERY modification operation
- Only lints modified .cs files (fast!)
- Parallel processing of projects
- Shows top 10 issues (errors first, then warnings)
- No separate linting request needed!

**Source Resolution:**
- Priority: Local files → SourceLink → Embedded PDB → ILSpy decompilation
- Works with .NET Framework, Core, 5+
- Supports legacy and SDK-style projects

**FQN Fuzzy Matching:**
- AI can provide imprecise/incomplete names
- Service finds best match via Levenshtein distance

### When to Use
✅ Need to MODIFY C# code (with automatic quality feedback!)
✅ Need to DEBUG or DIAGNOSE production issues
✅ Need to TRACE code execution flow or find crash origins
✅ Need to ANALYZE large log files efficiently
✅ Need code from external .NET libraries
✅ Need comprehensive project structure
✅ Need Git integration for safety
✅ Need detailed Roslyn context (call graphs, complexity, etc.)
✅ Need to format, lint, or auto-fix C# code
✅ **Need immediate feedback on code quality** after modifications

---

## 🌐 Tool 2: Code Graph RAG - Semantic Understanding

### Primary Use Cases
✅ **Natural language queries** ("Where is authentication?", "All database calls")
✅ **Architectural understanding** across entire codebase
✅ **Impact analysis** before refactoring
✅ **Code similarity** detection
✅ **Cross-cutting concerns** (logging, error handling everywhere)

### Key Tools
- `index` - Index codebase (do FIRST, once per session)
- `semantic_search` - Search by concept, not exact names
- `find_similar_code` - Find duplicate/similar patterns
- `analyze_impact` - Understand what will break
- `list_entity_relationships` - See how code connects
- `get_graph_health` - Check index status

### When to Use
✅ Starting work on unfamiliar C# codebase
✅ Need to understand "big picture" architecture
✅ Searching by concept ("all validation logic")
✅ **BEFORE major refactoring** (impact analysis)
✅ Finding code duplication
✅ After Git branch switch (see [GIT_WORKFLOW.md](./GIT_WORKFLOW.md))

---

## 📋 Standard Workflows

### Workflow 1: Starting Work on C# Solution

```
Step 1: Initialize tools
- Code Graph RAG: index /path/to/solution
- SharpToolsMCP: UltrasharpTool_LoadSolution

Step 2: Understand codebase
- Code Graph RAG: semantic_search "main entry points"
- Code Graph RAG: list_entity_relationships for key classes

Step 3: Navigate to specific code
- SharpToolsMCP: UltrasharpTool_SearchDefinitions with regex
- SharpToolsMCP: UltrasharpTool_ViewDefinition for details
```

### Workflow 2: Code Quality Check (Before Commit)

```
Step 1: Format code
- SharpToolsMCP: UltrasharpTool_FormatCode (checkOnly=true) → see issues
- SharpToolsMCP: UltrasharpTool_FormatCode (checkOnly=false) → fix formatting

Step 2: Lint code
- SharpToolsMCP: UltrasharpTool_AnalyzeCodeStyle (severity: Warning)
- Review diagnostics

Step 3: Auto-fix common issues
- SharpToolsMCP: UltrasharpTool_ApplyCodeFixes (preview=true) → review
- SharpToolsMCP: UltrasharpTool_ApplyCodeFixes (preview=false) → fix

Step 4: Check complexity
- SharpToolsMCP: UltrasharpTool_AnalyzeComplexity
- Refactor if needed
```

### Workflow 3: Major Refactoring (with Auto-Linting!)

```
Step 1: Impact analysis
- Code Graph RAG: analyze_impact for target symbol

Step 2: Find all usages
- SharpToolsMCP: UltrasharpTool_FindReferences (detailed context)

Step 3: Check for similar patterns
- Code Graph RAG: find_similar_code

Step 4: Make changes (AUTO-LINTING INCLUDED!)
- SharpToolsMCP: UltrasharpTool_RenameSymbol or UltrasharpTool_OverwriteMember
  → Response includes automatic quality check!
- Review linting results in the response
- Fix any issues immediately

Step 5: Final quality check (if needed)
- SharpToolsMCP: UltrasharpTool_AnalyzeCodeStyle (full analysis)
- SharpToolsMCP: UltrasharpTool_FormatCode (final formatting)
```

### Workflow 4: Adding New Feature (with Auto-Linting!)

```
Step 1: Understand existing code
- SharpToolsMCP: UltrasharpTool_ViewDefinition for related classes

Step 2: Add new code
- SharpToolsMCP: UltrasharpTool_AddMember
  → Response includes automatic quality check!
- Review errors/warnings in response
- Fix issues immediately if needed

Step 3: If issues found, fix them
- SharpToolsMCP: UltrasharpTool_OverwriteMember
  → Again includes automatic quality check!
- Iterate until quality check is clean

Step 4: Final verification
- SharpToolsMCP: UltrasharpTool_FormatCode (apply)
```

### Workflow 5: Debugging Production Crash (NEW!)

```
Step 1: Analyze log files
- SharpToolsMCP: UltrasharpTool_AnalyzeLogs
  → keywords: ["ERROR", "EXCEPTION", "FATAL"]
  → levels: ["Error", "Fatal"]
  → contextBefore: 10, contextAfter: 10
- Extract stack trace from log entries

Step 2: Trace backwards from crash point
- SharpToolsMCP: UltrasharpTool_TraceBackwards
  → crashPointFqn: "MyNamespace.MyClass.ProblematicMethod"
  → stackTraceHints: ["MethodA", "MethodB", "Main"]
- Review all possible call paths (ranked by confidence)
- Identify entry points leading to crash

Step 3: Trace execution flow (optional)
- SharpToolsMCP: UltrasharpTool_TraceExecution
  → entryPointFqn: "MyNamespace.Program.Main"
  → exitPointFqn: "MyNamespace.MyClass.ProblematicMethod"
- Understand what operations happen before crash
- Identify suspicious variable operations or conditionals

Step 4: View code with context
- SharpToolsMCP: UltrasharpTool_ViewDefinition for crash point
- SharpToolsMCP: UltrasharpTool_FindReferences for related methods

Step 5: Fix and verify
- Make necessary code changes
- Auto-linting feedback included in response
- Test fix thoroughly
```

### Workflow 6: Analyzing Large Log Files (NEW!)

```
Step 1: Auto-detect log format
- SharpToolsMCP: UltrasharpTool_AnalyzeLogs
  → filePath: "/logs/production.log"
  → take: 10 (small sample)
- Format detected automatically (ECS, PlainText, Logcat, WebServer, XML)

Step 2: Search for specific issues
- SharpToolsMCP: UltrasharpTool_AnalyzeLogs
  → keywords: ["OutOfMemoryException", "timeout"]
  → levels: ["Error"]
  → statusCodes: [500, 503] (for web logs)
  → contextBefore: 5, contextAfter: 5
  → skip: 0, take: 100

Step 3: Pagination for more results
- SharpToolsMCP: UltrasharpTool_AnalyzeLogs
  → (same parameters)
  → skip: 100, take: 100 (next page)

Step 4: Detailed analysis
- Review context lines around errors
- Extract stack traces
- Use TraceBackwards with extracted stack traces
```

---

## 🚫 Anti-Patterns

❌ Don't skip `UltrasharpTool_LoadSolution` - tools won't work without it
❌ Don't ignore automatic linting results in modification responses
❌ Don't format before commit - SharpTools does it automatically
❌ Don't make major changes without impact analysis (Code Graph RAG)
❌ Don't ignore Git branches created by SharpToolsMCP
❌ Don't forget that modifications now include quality checks automatically!

---

## ⚡ Performance Tips

1. **Index once** (Code Graph RAG) - subsequent queries are <100ms
2. **Load solution once** (SharpToolsMCP) - don't reload unnecessarily
3. **Use UltrasharpTool_SearchDefinitions** for regex searches - very fast
4. **Format in batch** - format entire directory, not file-by-file
5. **Trust automatic linting** - it's faster than separate quality checks
6. **Parallel processing** - SharpTools uses Task.WhenAll everywhere

---

## 🎓 Key Principles

| Tool | Best For | Speed | Modification | Auto-Linting | Debugging |
|------|----------|-------|--------------|--------------|-----------|
| **SharpToolsMCP** | Everything C# | Fast-Medium | Full + Git | ✅ Yes! | ✅ Yes! |
| **Code Graph RAG** | Semantic understanding | Very Fast | Read-only | ❌ No | ❌ No |

### Golden Rules
1. **SharpToolsMCP** = Complete C# Suite (modify, format, lint, analyze, debug, + AUTO-LINTING!)
2. **Code Graph RAG** = Semantic, Architecture, "Why"
3. **Always start with UltrasharpTool_LoadSolution**
4. **Modification tools include automatic quality checks** - use them!
5. **Use debugging tools for production issues** - tracing, logs, backtrace

### Before Every Commit
```
1. Make modifications with SharpTools
   └─ Automatic linting included in response!
2. Review linting results, fix issues if needed
3. SharpToolsMCP: UltrasharpTool_FormatCode (apply) - final format
4. SharpToolsMCP: UltrasharpTool_AnalyzeCodeStyle - final check
5. Review changes in Git branch
6. Commit (or merge SharpTools branch)
```

---

## 💡 Pro Tips

### Automatic Linting Best Practices
- ✅ **Trust the auto-lint** - it runs on every modification
- ✅ **Act on errors immediately** - don't let them accumulate
- ✅ **Warnings are informational** - fix if reasonable
- ✅ **Use UltrasharpTool_ApplyCodeFixes** for common issues (unused usings, etc.)
- ✅ **Format last** - after all code changes are done

### Git Integration Best Practices
- ✅ SharpTools creates `sharptools/` branches automatically
- ✅ Each change is a separate commit - easy to review
- ✅ Use `UltrasharpTool_Undo` if mistake - reverts last commit
- ✅ Periodically clean up old `sharptools/` branches
- ✅ Can disable Git with `--disable-git` flag

### Performance Best Practices
- ✅ Load solution once per session
- ✅ Use FQN (Fully Qualified Names) for precise targeting
- ✅ Leverage FQN fuzzy matching - don't worry about exact names
- ✅ Batch operations when possible (format directories, not files)
- ✅ Automatic linting is optimized - only modified files checked

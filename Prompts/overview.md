# UltrasharpTools - Overview

**Roslyn-powered MCP tools for intelligent C# code analysis and modification.**

## Key Concepts

### FQN-First Architecture
Always use **Fully Qualified Names** for all operations:
```
✅ "MyNamespace.MyClass.MyMethod"
❌ "MyMethod" or file scanning
```

### Standard Workflow
```
1. load_solution("path/to/solution.sln")
2. load_project("ProjectName")
3. view_definition / get_members / find_references
4. modify_code / add_member / rename_symbol
```

### Git Integration
Each modification automatically creates branch `sharptools/YYYYMMDD-HHMMSS` with commit.

---

## Tool Categories

| Category | Prompt | Tools |
|----------|--------|-------|
| **Solution** | `solution` | `load_solution`, `load_project` |
| **Analysis** | `analysis` | `view_definition`, `get_members`, `find_references`, `search_definitions` |
| **Modification** | `modification` | `modify_code`, `add_member`, `rename_symbol`, `find_and_replace` |
| **Quality** | `quality` | `format_code`, `analyze_code_style`, `apply_code_fixes` |
| **Tracing** | `tracing` | `trace_execution`, `trace_backwards`, `analyze_logs` |
| **Semantic** | `semantic` | `semantic_search`, `pattern_search`, `detect_code_clones` |
| **System** | `system` | `get_capabilities`, `create_snapshot`, `undo` |

---

## Quick Start

```javascript
// 1. Load solution
load_solution("D:/MyProject/MyProject.sln")

// 2. View project structure
load_project("MyProject.Core")

// 3. Explore class
get_members("MyNamespace.UserService", includePrivateMembers: false)

// 4. View implementation
view_definition("MyNamespace.UserService.CreateUser")

// 5. Find usages
find_references("MyNamespace.UserService.CreateUser")

// 6. Modify code
modify_code(
    fullyQualifiedMemberName: "MyNamespace.UserService.CreateUser",
    newMemberCode: "/* new implementation */",
    commitMessage: "Fix user creation"
)
```

---

## Command Line Options

```bash
UltrasharpTools.Droid.exe \
  --load-solution "D:/MyProject/MyProject.sln" \
  --build-configuration "Debug" \
  --log-level Information \
  --symbol-cache
```

**Options:**
- `--load-solution <path>` — auto-load solution on startup
- `--disable-git` — disable Git integration
- `--symbol-cache` — symbol cache (10x faster loading)

---

## Detailed Documentation

Request prompt for specific category:
- `solution` — solution and project management
- `analysis` — code analysis and navigation
- `modification` — code modification
- `quality` — formatting and linting
- `tracing` — execution tracing
- `semantic` — semantic search
- `system` — system capabilities

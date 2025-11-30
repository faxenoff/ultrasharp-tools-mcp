# UltrasharpTools MCP Server

**Roslyn-powered C# code analysis and modification.**

## Quick Start

```
1. load_solution("path/to/solution.sln")
2. load_project("ProjectName")
3. Use FQN for everything: view_definition, get_members, modify_code
```

## Key Principles

- **FQN-First**: Always use Fully Qualified Names (`MyNamespace.MyClass.MyMethod`)
- **Auto-Git**: Every modification creates branch + commit. Use `undo` to rollback
- **Token Efficient**: Code without indentation, symbol-based navigation

## Tool Categories

| Category | Tools |
|----------|-------|
| Solution | `load_solution`, `load_project` |
| Analysis | `view_definition`, `get_members`, `find_references`, `search_definitions` |
| Modification | `modify_code`, `add_member`, `rename_symbol`, `find_and_replace` |
| Quality | `format_code`, `analyze_code_style`, `apply_code_fixes` |
| Tracing | `trace_execution`, `trace_backwards`, `analyze_logs` |
| Semantic | `semantic_search`, `pattern_search`, `detect_code_clones` |
| System | `get_capabilities`, `create_snapshot`, `undo` |

## Documentation

**Use MCP prompts for detailed documentation:**

| Prompt | Content |
|--------|---------|
| `overview` | Key concepts, workflow, all categories |
| `solution` | Solution/project management |
| `analysis` | Code analysis and navigation |
| `modification` | Code modification and refactoring |
| `quality` | Formatting and linting |
| `tracing` | Execution tracing and debugging |
| `semantic` | Semantic search (requires setup) |
| `system` | Capabilities and snapshots |

## Important

- Call `get_capabilities()` at startup to check semantic mode availability
- If semantic mode disabled, use `pattern_search` instead of `semantic_search`

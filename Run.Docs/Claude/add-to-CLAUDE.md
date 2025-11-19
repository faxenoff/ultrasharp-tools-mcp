# UltrasharpTools MCP Server

**Roslyn-powered C# code analysis and modification tools for AI agents.**

## Quick Start

UltrasharpTools provides deep C# code understanding through Roslyn APIs, enabling intelligent code analysis, modification, and refactoring.

### Essential Workflow

```
1. load_solution("path/to/solution.sln")
   → Loads workspace, returns project list

2. load_project("ProjectName")
   → Returns comprehensive type map (namespaces → types)

3. Use FQN (Fully Qualified Names) for everything:
   - view_definition(fqn)
   - get_members(fqn)
   - find_references(fqn)
   - modify_code(fqn, newCode)
```

### Key Principles

**FQN-First Navigation**
- Always use Fully Qualified Names (e.g., `MyNamespace.MyClass.MyMethod`)
- Fuzzy matching automatically finds close matches
- Saves tokens by avoiding full file reads

**Auto-Git Integration**
- Every modification creates `sharptools/YYYYMMDD-HHMMSS` branch
- Auto-commits with descriptive messages
- Use `undo` to rollback last change
- Disable with `--disable-git` flag

**Token Efficiency**
- Code returned without indentation (~10% token savings)
- Adaptive detail levels in LoadProject
- Symbol-based navigation instead of file scanning

## Tool Categories

| Category | Purpose | Key Tools |
|----------|---------|-----------|
| **Solution** | Load & navigate | load_solution, load_project |
| **Analysis** | Code understanding | view_definition, get_members, find_references |
| **Modification** | Code changes | modify_code, add_member, rename_symbol |
| **Quality** | Formatting & fixes | format_code, analyze_code_style, apply_code_fixes |
| **Document** | File operations | read_raw_from_roslyn_document, overwrite_roslyn_document |
| **Tracing** | Debugging | trace_execution, trace_backwards, analyze_logs |
| **Semantic** | Smart search | semantic_search, semantic_diff |
| **System** | Server info | get_capabilities |

## Detailed Documentation

For comprehensive tool documentation and advanced usage, see:
**[ULTRA_SHARP.md](./ULTRA_SHARP.md)** - Complete reference with examples

## Common Patterns

### Explore Codebase
```
1. LoadSolution → get projects
2. LoadProject → get type map
3. ViewDefinition → understand implementations
4. find_references → see usage patterns
```

### Refactor Code
```
1. ViewDefinition → read current code
2. OverwriteMember → apply changes (auto-commits)
3. FormatCode → cleanup style
4. analyze_code_style → verify quality
```

### Debug Issues
```
1. TraceExecution → follow execution path
2. AnalyzeLogs → extract structured data
3. view_definition → examine suspect code
4. OverwriteMember → apply fix
```

## Configuration

**Check Server Capabilities** (REQUIRED at startup):
```
get_capabilities()
→ Returns: semantic mode status, enabled features, server info
→ Use to: detect if semantic search is available

Example response:
{
  "semanticMode": {
    "enabled": true,
    "mode": "Local",           // Local, Overlord, Both, or None
    "provider": "TEI",          // TEI, Ollama, Memory, or null
    "dimension": 384
  },
  "version": "3.0.0",
  "roslynVersion": "5.0.0"
}
```

**⚠️ IMPORTANT - Semantic Features:**
Before using `semantic_search`, `semantic_diff`, or `detect_code_clones`:
1. **ALWAYS call `get_capabilities()` first**
2. Check `semanticMode.enabled === true`
3. If disabled, these tools WILL FAIL - use alternative tools instead:
   - Instead of `semantic_search` → use `pattern_search` or `search_definitions`
   - Instead of `semantic_diff` → use `find_and_replace` with git diff
   - Instead of `detect_code_clones` → use `analyze_complexity`

**Semantic Search Setup** (optional, user must configure):
- Setup: `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`
- Providers: TEI (recommended), Ollama, Memory
- Config: `Run.Config/semantic-config.json`
- Verify after setup: `get_capabilities()` → `semanticMode.enabled`

**Build Configuration**:
- Use `--build-configuration Debug` for full debugging symbols
- Use `--build-configuration Release` for production code paths

## Best Practices

✅ **DO**:
- Call `get_capabilities()` at startup to check available features
- Start with load_solution + load_project
- Use FQN for all symbol operations
- Check analyze_code_style before committing
- Use format_code for consistency
- Review changes with git diff

❌ **DON'T**:
- Don't scan files manually - use load_project type map
- Don't guess FQNs - fuzzy matching handles variations
- Don't skip load_solution - required for all operations
- Don't forget to load dependencies with load_solution
- Don't assume semantic mode is available - check with get_capabilities()

## Support & Resources

- **Full Documentation**: [ULTRA_SHARP.md](./ULTRA_SHARP.md)
- **Tool Reference**: Individual `ULTRA_SHARP_*.md` files
- **Examples**: `Run.Docs/Guides/` directory
- **Architecture**: `Dev.Docs/Architecture/` directory

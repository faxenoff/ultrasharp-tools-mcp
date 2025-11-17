# UltrasharpTools MCP Server

**Roslyn-powered C# code analysis and modification tools for AI agents.**

## Quick Start

UltrasharpTools provides deep C# code understanding through Roslyn APIs, enabling intelligent code analysis, modification, and refactoring.

### Essential Workflow

```
1. UltrasharpTool_LoadSolution("path/to/solution.sln")
   → Loads workspace, returns project list

2. UltrasharpTool_LoadProject("ProjectName")
   → Returns comprehensive type map (namespaces → types)

3. Use FQN (Fully Qualified Names) for everything:
   - UltrasharpTool_ViewDefinition(fqn)
   - UltrasharpTool_GetMembers(fqn)
   - UltrasharpTool_FindReferences(fqn)
   - UltrasharpTool_OverwriteMember(fqn, newCode)
```

### Key Principles

**FQN-First Navigation**
- Always use Fully Qualified Names (e.g., `MyNamespace.MyClass.MyMethod`)
- Fuzzy matching automatically finds close matches
- Saves tokens by avoiding full file reads

**Auto-Git Integration**
- Every modification creates `sharptools/YYYYMMDD-HHMMSS` branch
- Auto-commits with descriptive messages
- Use `UltrasharpTool_Undo` to rollback last change
- Disable with `--disable-git` flag

**Token Efficiency**
- Code returned without indentation (~10% token savings)
- Adaptive detail levels in LoadProject
- Symbol-based navigation instead of file scanning

## Tool Categories

| Category | Purpose | Key Tools |
|----------|---------|-----------|
| **Solution** | Load & navigate | LoadSolution, LoadProject |
| **Analysis** | Code understanding | ViewDefinition, GetMembers, FindReferences |
| **Modification** | Code changes | OverwriteMember, AddMember, RenameSymbol |
| **Quality** | Formatting & fixes | FormatCode, AnalyzeCodeStyle, ApplyCodeFixes |
| **Document** | File operations | ReadRawFromRoslynDocument, OverwriteRoslynDocument |
| **Tracing** | Debugging | TraceExecution, TraceBackwards, AnalyzeLogs |
| **Semantic** | Smart search | SemanticSearch, SemanticDiff |

## Detailed Documentation

For comprehensive tool documentation and advanced usage, see:
**[ULTRA_SHARP.md](./ULTRA_SHARP.md)** - Complete reference with examples

## Common Patterns

### Explore Codebase
```
1. LoadSolution → get projects
2. LoadProject → get type map
3. ViewDefinition → understand implementations
4. FindReferences → see usage patterns
```

### Refactor Code
```
1. ViewDefinition → read current code
2. OverwriteMember → apply changes (auto-commits)
3. FormatCode → cleanup style
4. AnalyzeCodeStyle → verify quality
```

### Debug Issues
```
1. TraceExecution → follow execution path
2. AnalyzeLogs → extract structured data
3. ViewDefinition → examine suspect code
4. OverwriteMember → apply fix
```

## Configuration

**Semantic Search** (optional):
- Setup: `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`
- Providers: TEI (recommended), Ollama, Memory
- Config: `Run.Config/semantic-config.json`

**Build Configuration**:
- Use `--build-configuration Debug` for full debugging symbols
- Use `--build-configuration Release` for production code paths

## Best Practices

✅ **DO**:
- Start with LoadSolution + LoadProject
- Use FQN for all symbol operations
- Check AnalyzeCodeStyle before committing
- Use FormatCode for consistency
- Review changes with git diff

❌ **DON'T**:
- Don't scan files manually - use LoadProject type map
- Don't guess FQNs - fuzzy matching handles variations
- Don't skip LoadSolution - required for all operations
- Don't forget to load dependencies with LoadSolution

## Support & Resources

- **Full Documentation**: [ULTRA_SHARP.md](./ULTRA_SHARP.md)
- **Tool Reference**: Individual `ULTRA_SHARP_*.md` files
- **Examples**: `Run.Docs/Guides/` directory
- **Architecture**: `Dev.Docs/Architecture/` directory

# UltrasharpTools - C# Development MCP Server

**For C# projects, use UltrasharpTools MCP server.**

## Quick Reference

**See [MCP_SHARP.md](./MCP_SHARP.md) for complete documentation.**

## Key Capabilities

### Code Modification (with Auto-Linting!)
- ✅ All modification operations include **automatic quality checks**
- ✅ Add/modify/rename/move members with Roslyn precision
- ✅ Git integration: auto-branches, commits, undo support

### Code Quality
- ✅ **FormatCode** - CSharpier formatting
- ✅ **AnalyzeCodeStyle** - Roslyn analyzers
- ✅ **ApplyCodeFixes** - Auto-fix common issues

### Debugging & Diagnostics
- ✅ **TraceExecution** - Static code flow analysis (CFG)
- ✅ **TraceBackwards** - Backtrace from crash with stack hints
- ✅ **AnalyzeLogs** - 5 formats (ECS/JSON/Logcat/WebServer/XML)

## Essential Workflow

```
1. UltrasharpTool_LoadSolution (ALWAYS start with this!)
2. Make modifications → automatic linting included
3. Review quality feedback → fix issues
4. FormatCode → AnalyzeCodeStyle → commit
```

## Critical Rules

- ❌ Don't skip `LoadSolution` - tools require it
- ❌ Don't ignore automatic linting in responses
- ✅ Trust auto-linting - runs on every modification
- ✅ Use Git integration - auto-branches, easy undo

---

**📖 Complete documentation:** [MCP_SHARP.md](./MCP_SHARP.md)

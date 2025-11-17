---
type: guide
purpose: Quick MCP tools reference for AI assistants
ai-context: **READ THIS when starting work on ANY project**
priority: CRITICAL
tags: [mcp, quick-reference, decision-tree]
---

# MCP Tools - Quick Reference

**You have multiple MCP servers available. Use them PROACTIVELY, not as last resort.**

## 🎯 Quick Decision: Which MCP to use?

### Working with C# / .NET project?
→ **See [MCP_Sharp.md](./MCP_Sharp.md)** - Complete C# development suite

**SharpTools MCP provides:**
- ✅ Semantic search (find code by meaning, not name!)
- ✅ Code modification with auto-linting
- ✅ Quality tools (formatting, linting, auto-fixes)
- ✅ Debugging (CFG tracing, backtrace, log analysis)
- ✅ 36+ specialized tools for C# development

**Key workflow:**
```
1. LoadSolution → initialize workspace
2. FindPotentialDuplicates → semantic search (no FQN needed!)
3. OverwriteMember → modify code (auto-linting included)
4. FormatCode + ApplyCodeFixes → ensure quality
```

---

### Working with other languages?
→ Use general-purpose MCP tools (grep, file operations, etc.)

---

## ⚡ Critical Rules

### 1. MCP tools are 10-100x FASTER than manual work
```
❌ DON'T: "Let me manually read the codebase to understand it"
✅ DO: Use MCP tools immediately (30 sec vs 30 min)
```

### 2. Use MCP tools FIRST, not when "task is specific enough"
```
❌ WRONG: "I'll try to figure it out myself first, then use MCP if needed"
✅ RIGHT: "C# project detected → LoadSolution immediately"
```

### 3. Semantic search when you don't know exact names
```
❌ WRONG: "I need exact class name to use MCP tools"
✅ RIGHT: "FindPotentialDuplicates → finds by meaning, not name"
```

### 4. Trust automatic quality checks
```
Every code modification includes:
  ✅ Compilation check
  ✅ Linting (Roslyn analyzers)
  ✅ Quality report in response
```

---

## 📖 Documentation Structure

**Level 1 (You are here):** Quick reference
**Level 2:** Language-specific guides
  - [MCP_Sharp.md](./MCP_Sharp.md) - C# / .NET projects
  - *(Other languages: coming soon)*

**Level 3:** Detailed tool documentation
  - [Run.Docs/Tools/](../Tools/) - Individual tool guides
  - [Run.Docs/Guides/](../Guides/) - Workflow examples

---

## 🆕 New Capabilities (2025)

**For C# projects** - see [MCP_Sharp.md](./MCP_Sharp.md):
- ✅ Semantic code search (vector embeddings)
- ✅ 5 log formats with auto-detection
- ✅ CFG-based tracing and backtrace
- ✅ Quality tools suite (format, lint, auto-fix)
- ✅ Automatic linting on every modification

---

**💡 Remember:** MCP tools are designed to be your FIRST approach, not fallback.

**Use proactively → Save hours of work.**

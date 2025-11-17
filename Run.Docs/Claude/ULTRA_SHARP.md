# UltrasharpTools - Complete Reference

**Roslyn-powered MCP tools for intelligent C# code analysis and modification.**

---

## 📚 Documentation Structure

This is the main overview. For detailed method documentation, see:

- **[ULTRA_SHARP_SOLUTION.md](./ULTRA_SHARP_SOLUTION.md)** - Solution & Project management
- **[ULTRA_SHARP_ANALYSIS.md](./ULTRA_SHARP_ANALYSIS.md)** - Code analysis & navigation
- **[ULTRA_SHARP_MODIFICATION.md](./ULTRA_SHARP_MODIFICATION.md)** - Code modification & refactoring
- **[ULTRA_SHARP_QUALITY.md](./ULTRA_SHARP_QUALITY.md)** - Formatting & code fixes
- **[ULTRA_SHARP_DOCUMENT.md](./ULTRA_SHARP_DOCUMENT.md)** - Document operations
- **[ULTRA_SHARP_TRACING.md](./ULTRA_SHARP_TRACING.md)** - Execution tracing & debugging
- **[ULTRA_SHARP_SEMANTIC.md](./ULTRA_SHARP_SEMANTIC.md)** - Semantic search & AI-powered features

---

## 🎯 Core Concepts

### 1. FQN-First Architecture

**Always use Fully Qualified Names** for all operations:
```
✅ Good: "MyNamespace.MyClass.MyMethod"
❌ Bad: "MyMethod" or scanning files for names
```

**Benefits:**
- Direct symbol resolution via Roslyn
- Fuzzy matching handles typos/variations
- ~10% token savings (no indentation in responses)
- Cross-file navigation without file paths

### 2. Workflow Pattern

```
┌─────────────────────────────────────────────────┐
│ 1. LoadSolution("path/to/solution.sln")        │
│    → Returns: project list, configuration      │
└─────────────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────────────┐
│ 2. LoadProject("ProjectName")                  │
│    → Returns: namespace → types map            │
└─────────────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────────────┐
│ 3. Navigate with FQN tools                     │
│    • ViewDefinition(fqn) → source code         │
│    • GetMembers(fqn) → API surface             │
│    • FindReferences(fqn) → usage locations     │
└─────────────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────────────┐
│ 4. Modify code (auto-commits to git)           │
│    • OverwriteMember(fqn, code)                │
│    • AddMember(containerFqn, code)             │
│    • RenameSymbol(fqn, newName)                │
└─────────────────────────────────────────────────┘
```

### 3. Git Integration

**Every modification creates a branch** `sharptools/YYYYMMDD-HHMMSS`:
- Automatic commits with descriptive messages
- Use `UltrasharpTool_Undo` to rollback last change
- Disable with `--disable-git` flag
- Supports custom branch retention policies

### 4. Token Efficiency

**Code optimization strategies:**
- All code returned **without indentation** (~10% savings)
- Navigate by FQN instead of reading full files
- Adaptive detail levels in `LoadProject`
- Paginated results for large queries

---

## 🛠️ Tool Categories Overview

### Solution Management
[→ Detailed docs](./ULTRA_SHARP_SOLUTION.md)

Load and navigate C# solutions:
- `UltrasharpTool_LoadSolution` - Load .sln file, initialize workspace
- `UltrasharpTool_LoadProject` - Get comprehensive type map for navigation

**Use when:** Starting any work with a C# codebase

---

### Code Analysis
[→ Detailed docs](./ULTRA_SHARP_ANALYSIS.md)

Understand code structure and relationships:
- `UltrasharpTool_GetMembers` - List type members with signatures & docs
- `UltrasharpTool_ViewDefinition` - Read source code of any symbol
- `UltrasharpTool_FindReferences` - Find all usages
- `UltrasharpTool_ListImplementations` - Find interface implementations
- `UltrasharpTool_GetAllSubtypes` - Explore nested types recursively
- `UltrasharpTool_SearchDefinitions` - Search symbols by name/pattern
- `UltrasharpTool_AnalyzeComplexity` - Measure cyclomatic/cognitive complexity

**Use when:** Understanding existing code before modifications

---

### Code Modification
[→ Detailed docs](./ULTRA_SHARP_MODIFICATION.md)

Modify code with automatic git tracking:
- `UltrasharpTool_OverwriteMember` - Replace method/property/class implementation
- `UltrasharpTool_AddMember` - Add new member to type
- `UltrasharpTool_RenameSymbol` - Intelligent rename with reference updates
- `UltrasharpTool_FindAndReplace` - Text-based find/replace in files
- `UltrasharpTool_MoveMember` - Move member to different type
- `UltrasharpTool_Undo` - Rollback last modification

**Use when:** Implementing features, fixing bugs, refactoring

---

### Quality Tools
[→ Detailed docs](./ULTRA_SHARP_QUALITY.md)

Ensure code quality and consistency:
- `UltrasharpTool_FormatCode` - Format code with CSharpier
- `UltrasharpTool_AnalyzeCodeStyle` - Run Roslyn analyzers
- `UltrasharpTool_ApplyCodeFixes` - Auto-fix common issues

**Use when:** Preparing code for commit, ensuring standards

---

### Document Operations
[→ Detailed docs](./ULTRA_SHARP_DOCUMENT.md)

Raw file operations when FQN approach doesn't fit:
- `UltrasharpTool_ReadRawFromRoslynDocument` - Read file content as-is
- `UltrasharpTool_CreateRoslynDocument` - Create new file in project
- `UltrasharpTool_OverwriteRoslynDocument` - Replace entire file content

**Use when:** Working with config files, non-C# files, or full-file replacements

---

### Execution Tracing
[→ Detailed docs](./ULTRA_SHARP_TRACING.md)

Debug and analyze program behavior:
- `UltrasharpTool_TraceExecution` - Trace execution path from entry point
- `UltrasharpTool_TraceBackwards` - Trace backwards from crash/error
- `UltrasharpTool_AnalyzeLogs` - Extract structured data from logs

**Use when:** Debugging crashes, understanding execution flow, analyzing logs

---

### Semantic Search
[→ Detailed docs](./ULTRA_SHARP_SEMANTIC.md)

AI-powered code understanding (requires setup):
- `UltrasharpTool_SemanticSearch` - Find semantically similar code
- `UltrasharpTool_SemanticDiff` - Compare code semantic changes

**Use when:** Finding similar patterns, refactoring duplicates, code review

**Setup required:** Run `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`

---

## 📋 Common Workflows

### 1. Explore Unknown Codebase

```
Step 1: Load solution
→ UltrasharpTool_LoadSolution("path/to/solution.sln")

Step 2: Get project overview
→ UltrasharpTool_LoadProject("CoreProject")
  Returns namespace hierarchy and type names

Step 3: Explore interesting types
→ UltrasharpTool_GetMembers("MyNamespace.ImportantClass")
  See public API surface

Step 4: Read implementations
→ UltrasharpTool_ViewDefinition("MyNamespace.ImportantClass.KeyMethod")
  Understand how it works

Step 5: Find usage patterns
→ UltrasharpTool_FindReferences("MyNamespace.ImportantClass.KeyMethod")
  See how it's used across codebase
```

### 2. Implement New Feature

```
Step 1: Understand existing code
→ ViewDefinition(similarFeatureFqn)

Step 2: Find where to add code
→ GetMembers(containerTypeFqn)

Step 3: Add implementation
→ AddMember(containerTypeFqn, newMethodCode)
  Auto-creates git branch & commits

Step 4: Ensure quality
→ FormatCode(projectPath)
→ AnalyzeCodeStyle(solutionPath)

Step 5: Verify changes
→ Check git diff in sharptools/* branch
```

### 3. Fix Bug

```
Step 1: Locate bug
→ TraceExecution(entryPointFqn, "reproduction scenario")
  OR TraceBackwards(crashLocation, stackTrace)

Step 2: Read suspect code
→ ViewDefinition(suspectMethodFqn)

Step 3: Apply fix
→ OverwriteMember(suspectMethodFqn, fixedCode)
  Auto-commits with message

Step 4: Verify fix
→ Run tests or trace again
→ If wrong, use Undo and try again
```

### 4. Refactor Code

```
Step 1: Find code smells
→ AnalyzeComplexity(fqn) - find complex methods
→ SemanticSearch("pattern to deduplicate")

Step 2: Plan refactoring
→ FindReferences(targetFqn) - see impact
→ ListImplementations(interfaceFqn) - find all implementations

Step 3: Execute refactoring
→ RenameSymbol(fqn, newName) - intelligent rename
→ MoveMember(sourceFqn, targetContainerFqn)
→ OverwriteMember(fqn, refactoredCode)

Step 4: Clean up
→ ApplyCodeFixes(solutionPath, "IDE0005") - remove unused usings
→ FormatCode(solutionPath)

Step 5: Verify
→ AnalyzeCodeStyle(solutionPath) - check for new issues
```

---

## ⚙️ Configuration

### Command-Line Options

**Both servers support:**
```bash
--log-level <level>           # Verbose|Debug|Information|Warning|Error|Fatal
--log-directory <path>        # Log output directory
--load-solution <path>        # Auto-load solution on startup
--build-configuration <cfg>   # Debug|Release
--disable-git                 # Disable git integration
--git-branch-retention-count N  # Keep only N recent sharptools/* branches
--git-auto-cleanup            # Auto-cleanup old branches after modifications
```

**Droid (stdio) specific:**
```bash
--auto-reload                 # Auto-reload when .sln/.csproj changes
--reload-debounce-ms <ms>     # Debounce delay for auto-reload
--symbol-cache                # Enable persistent symbol cache (faster startup)
--symbol-cache-clear          # Clear symbol cache on startup
```

**Overlord (HTTP) specific:**
```bash
--port <port>                 # HTTP port (default: 3001)
```

### Semantic Search Setup

**Required for SemanticSearch tools:**
1. Run setup: `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`
2. Choose provider:
   - **TEI** (recommended): HuggingFace Text Embeddings Inference via Docker
   - **Ollama**: Local inference with granite-embedding or mxbai-embed-large
   - **Memory**: In-process (experimental, small codebases only)
3. Config saved to: `Run.Config/semantic-config.json`

**Validation:**
```bash
pwsh Dev.Scripts/validate-semantic-config.ps1
```

---

## 🎓 Best Practices

### ✅ DO

1. **Always start with LoadSolution + LoadProject**
   - Provides type map for efficient navigation
   - Initializes Roslyn workspace correctly

2. **Use FQN for everything**
   - Fuzzy matching handles variations
   - Direct symbol resolution, no file scanning

3. **Review git changes before pushing**
   - Check `sharptools/*` branches
   - Verify commit messages are descriptive

4. **Run quality tools before committing**
   ```
   → FormatCode(path)
   → AnalyzeCodeStyle(solutionPath)
   → ApplyCodeFixes(solutionPath, "all")
   ```

5. **Use appropriate detail levels**
   - `LoadProject` supports `DetailLevel` enum
   - Start with `Summary`, drill down as needed

### ❌ DON'T

1. **Don't scan files manually**
   - Use `LoadProject` type map instead
   - Use `SearchDefinitions` for fuzzy searches

2. **Don't guess FQNs**
   - Use `SearchDefinitions` to find correct name
   - Fuzzy matching is forgiving but not magic

3. **Don't skip LoadSolution**
   - Required for all operations
   - Initializes MSBuildWorkspace

4. **Don't forget dependencies**
   - Load solution, not individual projects
   - Ensures all references are resolved

5. **Don't ignore complexity warnings**
   - `AnalyzeComplexity` highlights problematic code
   - Refactor before adding more features

---

## 🔧 Troubleshooting

### Common Issues

**"Symbol not found" errors:**
- Verify FQN is correct: use `SearchDefinitions`
- Ensure solution is loaded: `LoadSolution` first
- Check fuzzy match suggestions in error message

**"Project not found" errors:**
- Use exact project name from `LoadSolution` results
- Project name ≠ file name (check .sln file)

**Git conflicts:**
- Use `Undo` to rollback problematic changes
- Clean up old branches: `git branch -D sharptools/YYYYMMDD-HHMMSS`
- Or disable git: `--disable-git` flag

**Performance issues:**
- Enable symbol cache: `--symbol-cache`
- Use `DetailLevel.Summary` in `LoadProject`
- Limit `FindReferences` scope when possible

**Semantic search not working:**
- Run validation: `pwsh Dev.Scripts/validate-semantic-config.ps1`
- Check TEI/Ollama is running
- Verify config: `Run.Config/semantic-config.json`

---

## 📖 Further Reading

- **[add-to-CLAUDE.md](./add-to-CLAUDE.md)** - Quick reference (add to .claude/CLAUDE.md)
- **[../Guides/](../Guides/)** - Step-by-step tutorials
- **[../../Dev.Docs/Architecture/](../../Dev.Docs/Architecture/)** - Design documents
- **Tool-specific docs** - `ULTRA_SHARP_*.md` files in this directory

---

## 🚀 Quick Command Reference

```bash
# Essential workflow
UltrasharpTool_LoadSolution(solutionPath)
UltrasharpTool_LoadProject(projectName)
UltrasharpTool_GetMembers(fqn)
UltrasharpTool_ViewDefinition(fqn)
UltrasharpTool_OverwriteMember(fqn, code)

# Quality assurance
UltrasharpTool_FormatCode(path)
UltrasharpTool_AnalyzeCodeStyle(solutionPath)
UltrasharpTool_ApplyCodeFixes(solutionPath, "all")

# Navigation
UltrasharpTool_SearchDefinitions(query, symbolKind)
UltrasharpTool_FindReferences(fqn)
UltrasharpTool_ListImplementations(interfaceFqn)

# Debugging
UltrasharpTool_TraceExecution(entryFqn, scenario)
UltrasharpTool_AnalyzeLogs(logPath, query)

# Refactoring
UltrasharpTool_RenameSymbol(fqn, newName)
UltrasharpTool_MoveMember(fqn, targetContainerFqn)
UltrasharpTool_Undo()
```

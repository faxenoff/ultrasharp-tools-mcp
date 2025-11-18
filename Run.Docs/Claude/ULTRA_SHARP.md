# UltrasharpTools - Complete Reference

**Roslyn-powered MCP tools for intelligent C# code analysis and modification.**

---

## 📚 Documentation Structure

This is the main overview. For detailed method documentation, see:

- **[ULTRA_SHARP_SOLUTION.md](./ULTRA_SHARP_SOLUTION.md)** - Solution & Project management
- **[ULTRA_SHARP_ANALYSIS.md](./ULTRA_SHARP_ANALYSIS.md)** - Code analysis & navigation
- **[ULTRA_SHARP_MODIFICATION.md](./ULTRA_SHARP_MODIFICATION.md)** - Code modification & refactoring
- **[ULTRA_SHARP_QUALITY.md](./ULTRA_SHARP_QUALITY.md)** - Formatting & code fixes
- **[ULTRA_SHARP_VALIDATION.md](./ULTRA_SHARP_VALIDATION.md)** - Code validation & diagnostics (NEW)
- **[ULTRA_SHARP_FILE_OPS.md](./ULTRA_SHARP_FILE_OPS.md)** - File operations & refactoring (NEW)
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
│    • view_definition(fqn) → source code         │
│    • GetMembers(fqn) → API surface             │
│    • find_references(fqn) → usage locations     │
└─────────────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────────────┐
│ 4. Modify code (auto-commits to git)           │
│    • modify_code(fqn, code)                │
│    • add_member(containerFqn, code)             │
│    • rename_symbol(fqn, newName)                │
└─────────────────────────────────────────────────┘
```

### 3. Git Integration

**Every modification creates a branch** `sharptools/YYYYMMDD-HHMMSS`:
- Automatic commits with descriptive messages
- Use `undo` to rollback last change
- Disable with `--disable-git` flag
- Supports custom branch retention policies

### 4. Token Efficiency

**Code optimization strategies:**
- All code returned **without indentation** (~10% savings)
- Navigate by FQN instead of reading full files
- Adaptive detail levels in `load_project`
- Paginated results for large queries

---

## 🛠️ Tool Categories Overview

### Solution Management
[→ Detailed docs](./ULTRA_SHARP_SOLUTION.md)

Load and navigate C# solutions:
- `load_solution` - Load .sln file, initialize workspace
- `load_project` - Get comprehensive type map for navigation

**Use when:** Starting any work with a C# codebase

---

### Code Analysis
[→ Detailed docs](./ULTRA_SHARP_ANALYSIS.md)

Understand code structure and relationships:
- `get_members` - List type members with signatures & docs
- `view_definition` - Read source code of any symbol
- `find_references` - Find all usages
- `list_implementations` - Find interface implementations
- `get_all_subtypes` - Explore nested types recursively
- `search_definitions` - Search symbols by name/pattern
- `analyze_complexity` - Measure cyclomatic/cognitive complexity

**Use when:** Understanding existing code before modifications

---

### Code Modification
[→ Detailed docs](./ULTRA_SHARP_MODIFICATION.md)

Modify code with automatic git tracking:
- `modify_code` - Replace method/property/class implementation
- `add_member` - Add new member to type
- `rename_symbol` - Intelligent rename with reference updates
- `find_and_replace` - Text-based find/replace in files
- `move_member` - Move member to different type
- `undo` - Rollback last modification

**Use when:** Implementing features, fixing bugs, refactoring

---

### Quality Tools
[→ Detailed docs](./ULTRA_SHARP_QUALITY.md)

Ensure code quality and consistency:
- `format_code` - Format code with CSharpier
- `analyze_code_style` - Run Roslyn analyzers
- `apply_code_fixes` - Auto-fix common issues

**Use when:** Preparing code for commit, ensuring standards

---

### Document Operations
[→ Detailed docs](./ULTRA_SHARP_DOCUMENT.md)

Raw file operations when FQN approach doesn't fit:
- `read_file` - Read file content as-is
- `create_file` - Create new file in project
- `overwrite_file` - Replace entire file content

**Use when:** Working with config files, non-C# files, or full-file replacements

---

### Execution Tracing
[→ Detailed docs](./ULTRA_SHARP_TRACING.md)

Debug and analyze program behavior:
- `trace_execution` - Trace execution path from entry point
- `trace_backwards` - Trace backwards from crash/error
- `analyze_logs` - Extract structured data from logs

**Use when:** Debugging crashes, understanding execution flow, analyzing logs

---

### Code Validation
[→ Detailed docs](./ULTRA_SHARP_VALIDATION.md)

Automated quality checks with Roslyn diagnostics:
- `validate_file` - Validate single C# file with analyzers
- `validate_directory` - Batch validate directory (parallel)
- `compare_validation` - Track quality improvements

**Use when:** Pre-commit checks, CI/CD pipelines, quality tracking

---

### File Operations
[→ Detailed docs](./ULTRA_SHARP_FILE_OPS.md)

Large-scale file refactoring:
- `split_file` - Split large file by top-level types
- `synthesize_files` - Combine multiple files into one

**Use when:** Refactor "God classes", consolidate utilities, reorganize codebase

---

### Semantic Search
[→ Detailed docs](./ULTRA_SHARP_SEMANTIC.md)

AI-powered code understanding (requires setup):
- `semantic_search` - Find semantically similar code
- `semantic_diff` - Compare code semantic changes
- `detect_code_clones` - Find duplicate/similar code

**Use when:** Finding similar patterns, refactoring duplicates, code review

**Setup required:** Run `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`

---

## 📋 Common Workflows

### 1. Explore Unknown Codebase

```
Step 1: Load solution
→ load_solution("path/to/solution.sln")

Step 2: Get project overview
→ load_project("CoreProject")
  Returns namespace hierarchy and type names

Step 3: Explore interesting types
→ get_members("MyNamespace.ImportantClass")
  See public API surface

Step 4: Read implementations
→ view_definition("MyNamespace.ImportantClass.KeyMethod")
  Understand how it works

Step 5: Find usage patterns
→ find_references("MyNamespace.ImportantClass.KeyMethod")
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
→view_definitionn(suspectMethodFqn)

Step 3: Apply fix
→ Overmodify_codepectMethodFqn, fixedCode)
  Auto-commits with message

Step 4: Verify fix
→ Run tests or trace again
→ If wrong, use Undo and try again
```

### 4. Refactor Code

```
Step 1: Find code smells
→ analyze_complexity(fqn) - find complex methods
→ SemanticSearch("pattern to deduplicate")

Step 2: Plan refactoring
→ FindReferences(targetFqn) - see impact
→ ListImplementations(interfaceFqn) - find all implementations

Step 3: Execute refactoring
→ RenameSymbol(fqn, newName) - intelligent rename
→ MoveMember(sourceFqn, targetContainerFqn)
→ OverwriteMember(fqn, refactoredCode)

Step 4: Clean up
→ apply_code_fixes(solutionPath, "IDE0005") - remove unused usings
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

1. **Always start with load_solution + LoadProject**
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
   - `load_project` supports `DetailLevel` enum
   - Start with `Summary`, drill down as needed

### ❌ DON'T

1. **Don't scan files manually**
   - Use `load_project` type map instead
   - Use `search_definitions` for fuzzy searches

2. **Don't guess FQNs**
   - Use `search_definitions` to find correct name
   - Fuzzy matching is forgiving but not magic

3. **Don't skip LoadSolution**
   - Required for all operations
   - Initializes MSBuildWorkspace

4. **Don't forget dependencies**
   - Load solution, not individual projects
   - Ensures all references are resolved

5. **Don't ignore complexity warnings**
   - `analyze_complexity` highlights problematic code
   - Refactor before adding more features

---

## 🔧 Troubleshooting

### Common Issues

**"Symbol not found" errors:**
- Verify FQN is correct: use `search_definitions`
- Ensure solution is loaded: `load_solution` first
- Check fuzzy match suggestions in error message

**"Project not found" errors:**
- Use exact project name from `load_solution` results
- Project name ≠ file name (check .sln file)

**Git conflicts:**
- Use `undo` to rollback problematic changes
- Clean up old branches: `git branch -D sharptools/YYYYMMDD-HHMMSS`
- Or disable git: `--disable-git` flag

**Performance issues:**
- Enable symbol cache: `--symbol-cache`
- Use `DetailLevel.Summary` in `load_project`
- Limit `find_references` scope when possible

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
load_solution(solutionPath)
load_project(projectName)
get_members(fqn)
view_definition(fqn)
modify_code(fqn, code)

# Quality assurance
format_code(path)
analyze_code_style(solutionPath)
apply_code_fixes(solutionPath, "all")

# Navigation
search_definitions(query, symbolKind)
find_references(fqn)
list_implementations(interfaceFqn)

# Debugging
trace_execution(entryFqn, scenario)
analyze_logs(logPath, query)

# Refactoring
rename_symbol(fqn, newName)
move_member(fqn, targetContainerFqn)
undo()
```

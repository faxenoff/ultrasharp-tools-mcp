# Formatting & Code Fixes

[← Back to Overview](./ULTRA_SHARP.md)

**Automatic formatting, linting, and fixing** — maintain consistent code style via CSharpier and Roslyn analyzers. All modification operations automatically include quality checks.

---

## 📋 Quick Reference

| Tool | Technology | Purpose | Auto-fix |
|------|-----------|---------|---------|
| **FormatCode** | CSharpier | Consistent code style | ✅ Yes |
| **AnalyzeCodeStyle** | Roslyn Analyzers | Find issues (warnings, errors) | ❌ No |
| **ApplyCodeFixes** | Roslyn Code Fixes | Automatic fixes | ✅ Yes |

---

## 🔄 Integration with Modification Tools

**Important:** All modification operations **automatically** include quality checks:

```javascript
add_member(...)
// Automatically:
// 1. ✅ Syntax check
// 2. ✅ Compilation check
// 3. ⚠️ Warning if formatting needed
// 4. 📊 Code style warnings report

modify_code(...)
// Same - automatic checks

rename_symbol(...)
// Also with automatic checks
```

**Recommended workflow:**
```javascript
// 1. Modification (with automatic checks)
add_member(...)
// Output: "✅ No errors. ⚠️ Consider running FormatCode"

// 2. Formatting
format_code(path: "src/", checkOnly: false)

// 3. Detailed analysis
analyze_code_style(severityFilter: "Warning")

// 4. Automatic fixes
apply_code_fixes(diagnosticId: "all", preview: false)
```

---

## format_code

**Automatic formatting** — formats C# code to consistent style via CSharpier.

### Usage

```javascript
// Check without changes
format_code(
    path: "D:/MyProject/src/Services",
    checkOnly: true
)

// Apply formatting
format_code(
    path: "D:/MyProject/src/Services",
    checkOnly: false
)
```

### Parameters

- **path** (required): Path to file or directory
- **checkOnly** (default: true): `true` — check only, `false` — apply formatting

### Supported Files

- ✅ `.cs` — C# source files
- ✅ `.csproj` — Project files
- ✅ `.xml` — XML configuration files

### What It Does

**CheckOnly mode:**
1. 🔍 Scans all files in path (recursively)
2. ✅ Checks formatting of each file
3. 📊 Returns list of files needing formatting
4. ❌ Does NOT modify files

**Apply mode (checkOnly: false):**
1. 🔍 Scans files
2. 🎨 Formats each file (parallel)
3. 💾 Saves changes
4. 🌳 **Creates Git commit** (if files changed)
5. 📊 Returns statistics

### CSharpier Style Guide

**Key rules:**
- ✅ Indentation: 4 spaces (not tabs)
- ✅ Braces: on new line (Allman style)
- ✅ Max line length: 120 characters
- ✅ Trailing whitespace: removed
- ✅ Final newline: added
- ✅ Using statements: sorted and grouped
- ✅ Consistent spacing around operators

**Example formatting:**
```csharp
// ❌ BEFORE (unformatted)
public class UserService{
private readonly IUserRepository _repo;
public async Task<User>GetUserAsync(int id){
if(id<=0)throw new ArgumentException();
var user=await _repo.GetByIdAsync(id);return user;}}

// ✅ AFTER (CSharpier)
public class UserService
{
    private readonly IUserRepository _repo;

    public async Task<User> GetUserAsync(int id)
    {
        if (id <= 0)
            throw new ArgumentException();

        var user = await _repo.GetByIdAsync(id);
        return user;
    }
}
```

### When to Use

✅ **After modifications:**
- After AddMember
- After OverwriteMember
- After FindAndReplace
- After mass changes

✅ **Regularly:**
- Before commit to Git
- After merge
- Code review process
- CI/CD pipeline

✅ **For entire project:**
- Onboarding new developer
- Unifying code style
- Migration to new style guide

### Best Practices

1. **Check first, then apply:**
   ```javascript
   // ✅ Correct - see what will change
   format_code(path: "src/", checkOnly: true)
   // Output: "12 files need formatting"

   format_code(path: "src/", checkOnly: false)
   // Apply
   ```

2. **Format directories, not files:**
   ```javascript
   // ✅ Good - entire directory
   format_code(path: "src/Services/", checkOnly: false)

   // ⚠️ Acceptable but inefficient - one file at a time
   format_code(path: "src/Services/UserService.cs", checkOnly: false)
   ```

3. **Integrate into workflow:**
   ```javascript
   // After changes
   modify_code(...)
   format_code(path: "src/", checkOnly: false)
   analyze_code_style(...)
   apply_code_fixes(...)
   ```

### Performance

- **Check (checkOnly: true):** 0.5-2 sec for ~50 files
- **Format (checkOnly: false):** 1-5 sec for ~50 files
- **Parallelism:** File processing is parallel (multi-threaded)

### Related Tools

- ➡️ [**AnalyzeCodeStyle**](#analyze_code_style) — analyze after formatting
- ➡️ [**ApplyCodeFixes**](#apply_code_fixes) — automatic fixes
- ⬅️ **Modification Tools** — formatting after modifications

---

## analyze_code_style

**Code quality analysis** — runs Roslyn analyzers to find code style issues, warnings, errors.

### Usage

```javascript
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    severityFilter: "Warning",
    skip: 0,
    take: 100
)
```

### Parameters

- **solutionPath** (required): Path to .sln file
- **severityFilter** (default: "Warning"): Minimum severity level (`"Hidden"`, `"Info"`, `"Warning"`, `"Error"`)
- **skip** (default: 0): Skip N results (pagination)
- **take** (default: 100): Return N results (pagination)

### What It Shows

**For each diagnostic:**
- 🔍 **Diagnostic ID** (IDE0005, CS8019, CA1001, etc.)
- ⚠️ **Severity** (Hidden, Info, Warning, Error)
- 📄 **Message** (problem description)
- 📁 **Location** (file:line:column)
- 💡 **Code snippet** (context)
- 🔧 **Has fix** (is automatic fix available)

**Grouping:**
- By severity (Error → Warning → Info → Hidden)
- By diagnostic ID
- Sorted by files

### Diagnostic Categories

**IDE#### — Code Style:**
- `IDE0001-IDE9999` — Visual Studio IDE analyzers
- Examples: IDE0005 (unused using), IDE0028 (collection init), IDE0055 (formatting)

**CS#### — C# Compiler:**
- `CS0001-CS9999` — C# compiler warnings/errors
- Examples: CS8019 (unused using), CS0168 (unused variable), CS8600 (nullable)

**CA#### — Code Analysis:**
- `CA1000-CA9999` — .NET code analysis rules
- Examples: CA1001 (IDisposable), CA1031 (catch Exception), CA2007 (ConfigureAwait)

### Severity Levels

- 🔴 **Error** — Must fix (breaks compilation or critical issue)
- ⚠️ **Warning** — Should fix (potential bugs, bad practices)
- 💡 **Info** — Consider fixing (improvements, suggestions)
- 👁️ **Hidden** — Optional (very minor style issues)

### When to Use

✅ **After modifications:**
- After AddMember/modify_code
- After refactoring
- Before commit

✅ **Regularly:**
- Code review process
- CI/CD pipeline
- Weekly/monthly quality checks

✅ **For analyzing technical debt:**
- Count warnings
- Track improvement over time
- Prioritize fixes

### Best Practices

1. **Start with Errors, then Warnings:**
   ```javascript
   // First critical
   analyze_code_style(severityFilter: "Error")

   // Then warnings
   analyze_code_style(severityFilter: "Warning")

   // Info optional
   analyze_code_style(severityFilter: "Info")
   ```

2. **Use pagination for large projects:**
   ```javascript
   // First page
   analyze_code_style(severityFilter: "Warning", skip: 0, take: 100)

   // Second page
   analyze_code_style(severityFilter: "Warning", skip: 100, take: 100)
   ```

3. **Automate fixes:**
   ```javascript
   // Analysis
   analyze_code_style(severityFilter: "Warning")
   // Output: "28 auto-fixable warnings"

   // Auto-fixes
   apply_code_fixes(diagnosticId: "all", preview: false)

   // Re-analyze
   analyze_code_style(severityFilter: "Warning")
   // Output: "15 warnings" (only manual fixes)
   ```

4. **Track progress:**
   ```javascript
   // Baseline
   analyze_code_style(severityFilter: "Warning")
   // "147 warnings"

   // After work
   analyze_code_style(severityFilter: "Warning")
   // "98 warnings" - 33% improvement!
   ```

### Performance

- **Small project (3-5 projects):** 5-10 sec
- **Medium project (10-20 projects):** 15-30 sec
- **Large project (50+ projects):** 45-90 sec

**Factors:**
- Number of analyzers
- Solution size
- File count
- Code complexity

### Related Tools

- ⬅️ [**FormatCode**](#format_code) — format before analysis
- ➡️ [**ApplyCodeFixes**](#apply_code_fixes) — auto-fix found issues
- ⬅️ [**AnalyzeComplexity**](./ULTRA_SHARP_ANALYSIS.md#analyze_complexity) — complexity metrics

---

## apply_code_fixes

**Automatic fixes** — applies Roslyn code fixes for diagnostics.

### Usage

```javascript
// Preview mode (see what will change)
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",
    preview: true
)

// Apply mode (apply changes)
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",
    preview: false
)
```

### Parameters

- **solutionPath** (required): Path to .sln file
- **diagnosticId** (default: "all"): Diagnostic ID or `"all"` for all
- **preview** (default: true): `true` — preview, `false` — apply

### Supported Diagnostics

**Built-in (always supported):**
- ✅ `IDE0005` — Remove unnecessary using directives
- ✅ `CS8019` — Unnecessary using directive
- ✅ `IDE0028` — Use collection initializers
- ✅ `IDE0090` — Use 'var' instead of explicit type
- ✅ `IDE0017` — Object initialization can be simplified
- ✅ And many more...

**Easily extensible:**
- System supports any Roslyn code fixes
- New diagnostics added via configuration

### What It Does

**Preview mode:**
1. 🔍 Scans solution for found diagnostics
2. 📊 Groups by diagnostic ID
3. 💡 Shows what will be fixed
4. 📄 Shows diff for each file
5. ❌ Does NOT modify files

**Apply mode:**
1. 🔍 Finds all instances of diagnostic
2. 🔧 Applies code fix to each
3. 💾 Saves changes
4. 🌳 **Creates Git commit**
5. 📊 Returns statistics

### When to Use

✅ **For mass fixes:**
- Remove all unused usings
- Apply code style rules
- Fix naming violations
- Simplify code patterns

✅ **After analysis:**
- AnalyzeCodeStyle showed auto-fixable issues
- Apply all suggested fixes

✅ **For migration:**
- Upgrade to new C# syntax
- Apply new .NET conventions
- Modernize codebase

### Best Practices

1. **ALWAYS preview before apply:**
   ```javascript
   // ✅ CORRECT
   apply_code_fixes(diagnosticId: "IDE0005", preview: true)
   // See what will change

   apply_code_fixes(diagnosticId: "IDE0005", preview: false)
   // Apply

   // ❌ DANGEROUS - don't know what will change
   apply_code_fixes(diagnosticId: "all", preview: false)
   ```

2. **Fix one diagnostic at a time:**
   ```javascript
   // ✅ Good - controllable
   apply_code_fixes(diagnosticId: "IDE0005", preview: false)
   apply_code_fixes(diagnosticId: "CS8019", preview: false)

   // ⚠️ Careful - many changes at once
   apply_code_fixes(diagnosticId: "all", preview: false)
   ```

3. **Verify after apply:**
   ```javascript
   apply_code_fixes(diagnosticId: "IDE0005", preview: false)

   // Check compilation
   // Output: "✅ Compilation: Success"

   // Run tests
   // dotnet test

   // Re-analyze
   analyze_code_style(severityFilter: "Warning")
   ```

4. **Use Undo if something's wrong:**
   ```javascript
   apply_code_fixes(diagnosticId: "IDE0028", preview: false)
   // Oops, this broke code!

   undo()
   // Reverted
   ```

### Performance

- **Preview:** 5-15 sec (depends on instance count)
- **Apply:** 10-30 sec (+ Git commit time)
- **Parallelism:** Project processing is parallel

**Factors:**
- Instance count
- File count
- Solution size

### Common Diagnostics for Auto-fix

```javascript
// Remove unused usings (most common)
diagnosticId: "IDE0005"  // or "CS8019"

// Simplify collection initialization
diagnosticId: "IDE0028"

// Use var
diagnosticId: "IDE0090"

// Simplify object initialization
diagnosticId: "IDE0017"

// All at once (careful!)
diagnosticId: "all"
```

### Related Tools

- ⬅️ [**AnalyzeCodeStyle**](#analyze_code_style) — find what needs fixing
- ⬅️ [**FormatCode**](#format_code) — format before fixes
- ➡️ [**Undo**](./ULTRA_SHARP_MODIFICATION.md#undo) — rollback if something's wrong

---

## Workflow: Comprehensive Quality Improvement

### Standard Workflow

```javascript
// 1. Formatting
format_code(path: "src/", checkOnly: true)
// Output: "12 files need formatting"

format_code(path: "src/", checkOnly: false)
// Applied formatting

// 2. Analysis
analyze_code_style(severityFilter: "Warning")
// Output: "43 warnings, 28 auto-fixable"

// 3. Automatic fixes
apply_code_fixes(diagnosticId: "IDE0005", preview: true)
// See what will change

apply_code_fixes(diagnosticId: "IDE0005", preview: false)
// Applied unused usings fix

apply_code_fixes(diagnosticId: "all", preview: true)
// See other fixes

apply_code_fixes(diagnosticId: "all", preview: false)
// Applied all

// 4. Re-analyze
analyze_code_style(severityFilter: "Warning")
// Output: "15 warnings" - only manual fixes remain

// 5. Manual fixes (outside SharpTools)
// Review remaining 15 warnings
// Fix manually via IDE or modification tools

// 6. Final check
format_code(path: "src/", checkOnly: true)
analyze_code_style(severityFilter: "Warning")
// All clean ✅
```

### After Modification Workflow

```javascript
// 1. Modification
add_member(
    fullyQualifiedTargetName: "UserService",
    codeSnippet: "...",
    commitMessage: "Add ValidateEmail method"
)
// Output: "✅ No errors. ⚠️ Consider formatting"

// 2. Quality checks (automatic workflow)
format_code(
    path: "src/Services/UserService.cs",
    checkOnly: false
)

analyze_code_style(severityFilter: "Warning")
// Check new warnings

apply_code_fixes(diagnosticId: "all", preview: false)
// Fix any auto-fixable issues

// 3. Done ✅
```

### CI/CD Integration

```bash
# In CI/CD pipeline

# 1. Check formatting
format_code(path: "src/", checkOnly: true)
# If returned "files need formatting" - FAIL BUILD

# 2. Analysis
analyze_code_style(severityFilter: "Error")
# If errors exist - FAIL BUILD

analyze_code_style(severityFilter: "Warning")
# Report warnings (but don't fail)

# 3. Can auto-fix (optional)
# apply_code_fixes(diagnosticId: "all", preview: false)
# Create PR with fixes
```

---

## Tool Comparison

| Tool | What It Does | Modifies Code | Git Commit | Speed |
|------|-------------|--------------|-----------|-------|
| **FormatCode** | Consistent code style | ✅ Yes | ✅ Yes | Fast (1-5 sec) |
| **AnalyzeCodeStyle** | Finds issues | ❌ No | ❌ No | Medium (15-90 sec) |
| **ApplyCodeFixes** | Fixes issues | ✅ Yes | ✅ Yes | Medium (10-30 sec) |

---

## See Also

- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — modifications with automatic quality checks
- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — code analysis
- 📚 [**ULTRA_SHARP_SOLUTION.md**](./ULTRA_SHARP_SOLUTION.md) — load solution first
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

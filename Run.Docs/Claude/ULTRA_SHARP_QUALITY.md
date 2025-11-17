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
UltrasharpTool_AddMember(...)
// Automatically:
// 1. ✅ Syntax check
// 2. ✅ Compilation check
// 3. ⚠️ Warning if formatting needed
// 4. 📊 Code style warnings report

UltrasharpTool_OverwriteMember(...)
// Same - automatic checks

UltrasharpTool_RenameSymbol(...)
// Also with automatic checks
```

**Recommended workflow:**
```javascript
// 1. Modification (with automatic checks)
UltrasharpTool_AddMember(...)
// Output: "✅ No errors. ⚠️ Consider running FormatCode"

// 2. Formatting
UltrasharpTool_FormatCode(path: "src/", checkOnly: false)

// 3. Detailed analysis
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")

// 4. Automatic fixes
UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
```

---

## UltrasharpTool_FormatCode

**Automatic formatting** — formats C# code to consistent style via CSharpier.

### Usage

```javascript
// Check without changes
UltrasharpTool_FormatCode(
    path: "D:/MyProject/src/Services",
    checkOnly: true
)

// Apply formatting
UltrasharpTool_FormatCode(
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
   UltrasharpTool_FormatCode(path: "src/", checkOnly: true)
   // Output: "12 files need formatting"

   UltrasharpTool_FormatCode(path: "src/", checkOnly: false)
   // Apply
   ```

2. **Format directories, not files:**
   ```javascript
   // ✅ Good - entire directory
   UltrasharpTool_FormatCode(path: "src/Services/", checkOnly: false)

   // ⚠️ Acceptable but inefficient - one file at a time
   UltrasharpTool_FormatCode(path: "src/Services/UserService.cs", checkOnly: false)
   ```

3. **Integrate into workflow:**
   ```javascript
   // After changes
   UltrasharpTool_OverwriteMember(...)
   UltrasharpTool_FormatCode(path: "src/", checkOnly: false)
   UltrasharpTool_AnalyzeCodeStyle(...)
   UltrasharpTool_ApplyCodeFixes(...)
   ```

### Performance

- **Check (checkOnly: true):** 0.5-2 sec for ~50 files
- **Format (checkOnly: false):** 1-5 sec for ~50 files
- **Parallelism:** File processing is parallel (multi-threaded)

### Related Tools

- ➡️ [**AnalyzeCodeStyle**](#UltrasharpTool_AnalyzeCodeStyle) — analyze after formatting
- ➡️ [**ApplyCodeFixes**](#UltrasharpTool_ApplyCodeFixes) — automatic fixes
- ⬅️ **Modification Tools** — formatting after modifications

---

## UltrasharpTool_AnalyzeCodeStyle

**Code quality analysis** — runs Roslyn analyzers to find code style issues, warnings, errors.

### Usage

```javascript
UltrasharpTool_AnalyzeCodeStyle(
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
- After AddMember/OverwriteMember
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
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Error")

   // Then warnings
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")

   // Info optional
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Info")
   ```

2. **Use pagination for large projects:**
   ```javascript
   // First page
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning", skip: 0, take: 100)

   // Second page
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning", skip: 100, take: 100)
   ```

3. **Automate fixes:**
   ```javascript
   // Analysis
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
   // Output: "28 auto-fixable warnings"

   // Auto-fixes
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)

   // Re-analyze
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
   // Output: "15 warnings" (only manual fixes)
   ```

4. **Track progress:**
   ```javascript
   // Baseline
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
   // "147 warnings"

   // After work
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
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

- ⬅️ [**FormatCode**](#UltrasharpTool_FormatCode) — format before analysis
- ➡️ [**ApplyCodeFixes**](#UltrasharpTool_ApplyCodeFixes) — auto-fix found issues
- ⬅️ [**AnalyzeComplexity**](./ULTRA_SHARP_ANALYSIS.md#UltrasharpTool_AnalyzeComplexity) — complexity metrics

---

## UltrasharpTool_ApplyCodeFixes

**Automatic fixes** — applies Roslyn code fixes for diagnostics.

### Usage

```javascript
// Preview mode (see what will change)
UltrasharpTool_ApplyCodeFixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",
    preview: true
)

// Apply mode (apply changes)
UltrasharpTool_ApplyCodeFixes(
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
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: true)
   // See what will change

   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: false)
   // Apply

   // ❌ DANGEROUS - don't know what will change
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
   ```

2. **Fix one diagnostic at a time:**
   ```javascript
   // ✅ Good - controllable
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: false)
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "CS8019", preview: false)

   // ⚠️ Careful - many changes at once
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
   ```

3. **Verify after apply:**
   ```javascript
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: false)

   // Check compilation
   // Output: "✅ Compilation: Success"

   // Run tests
   // dotnet test

   // Re-analyze
   UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
   ```

4. **Use Undo if something's wrong:**
   ```javascript
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0028", preview: false)
   // Oops, this broke code!

   UltrasharpTool_Undo()
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

- ⬅️ [**AnalyzeCodeStyle**](#UltrasharpTool_AnalyzeCodeStyle) — find what needs fixing
- ⬅️ [**FormatCode**](#UltrasharpTool_FormatCode) — format before fixes
- ➡️ [**Undo**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_Undo) — rollback if something's wrong

---

## Workflow: Comprehensive Quality Improvement

### Standard Workflow

```javascript
// 1. Formatting
UltrasharpTool_FormatCode(path: "src/", checkOnly: true)
// Output: "12 files need formatting"

UltrasharpTool_FormatCode(path: "src/", checkOnly: false)
// Applied formatting

// 2. Analysis
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
// Output: "43 warnings, 28 auto-fixable"

// 3. Automatic fixes
UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: true)
// See what will change

UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005", preview: false)
// Applied unused usings fix

UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: true)
// See other fixes

UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
// Applied all

// 4. Re-analyze
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
// Output: "15 warnings" - only manual fixes remain

// 5. Manual fixes (outside SharpTools)
// Review remaining 15 warnings
// Fix manually via IDE or modification tools

// 6. Final check
UltrasharpTool_FormatCode(path: "src/", checkOnly: true)
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
// All clean ✅
```

### After Modification Workflow

```javascript
// 1. Modification
UltrasharpTool_AddMember(
    fullyQualifiedTargetName: "UserService",
    codeSnippet: "...",
    commitMessage: "Add ValidateEmail method"
)
// Output: "✅ No errors. ⚠️ Consider formatting"

// 2. Quality checks (automatic workflow)
UltrasharpTool_FormatCode(
    path: "src/Services/UserService.cs",
    checkOnly: false
)

UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
// Check new warnings

UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
// Fix any auto-fixable issues

// 3. Done ✅
```

### CI/CD Integration

```bash
# In CI/CD pipeline

# 1. Check formatting
UltrasharpTool_FormatCode(path: "src/", checkOnly: true)
# If returned "files need formatting" - FAIL BUILD

# 2. Analysis
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Error")
# If errors exist - FAIL BUILD

UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
# Report warnings (but don't fail)

# 3. Can auto-fix (optional)
# UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
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

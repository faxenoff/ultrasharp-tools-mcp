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
| **CleanupUsings** | Roslyn + GlobalUsings | Remove redundant usings | ✅ Yes |

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

**Code quality analysis** — runs Roslyn analyzers to find code style issues, warnings, errors. **NEW**: Supports advanced filtering via presets, diagnostic codes, file patterns, and project names. Results are cached for 5 minutes for 10x faster repeated queries.

### Usage

```javascript
// Basic usage
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    severityFilter: "Warning",
    skip: 0,
    take: 100
)

// NEW: Using presets (category-based)
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    preset: "performance",
    severityFilter: "Info"
)

// NEW: Using presets (priority-based)
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    preset: "critical",
    severityFilter: "Warning"
)

// NEW: Filter by specific diagnostic codes
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticIds: "CA1822,CA1860,CS8019"
)

// NEW: Filter by file patterns
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    filePatterns: "**/Services/*.cs,**/Controllers/*.cs"
)

// NEW: Filter by projects
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    projectNames: "MyProject.Core,MyProject.Services"
)

// NEW: Combined filters
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    preset: "performance",
    filePatterns: "**/Services/*.cs",
    severityFilter: "Info"
)
```

### Parameters

- **solutionPath** (required): Path to .sln file
- **severityFilter** (default: "Warning"): Minimum severity level (`"Hidden"`, `"Info"`, `"Warning"`, `"Error"`)
- **preset** (optional): **NEW** — Quick filter by category or priority (see Presets below)
- **diagnosticIds** (optional): **NEW** — Comma-separated list of specific diagnostic codes (e.g., `"CA1822,CA1860,CS8019"`)
- **filePatterns** (optional): **NEW** — Comma-separated glob patterns (e.g., `"**/Services/*.cs,**/*Controller.cs"`)
- **projectNames** (optional): **NEW** — Comma-separated project names (e.g., `"MyProject.Core,MyProject.Api"`)
- **skip** (default: 0): Skip N results (pagination)
- **take** (default: 100): Return N results (pagination)

### Presets

**Category Presets** (semantic grouping):
- `performance` — Performance issues (CA1860, CA1861, CA1850, etc.)
- `security` — Security vulnerabilities (CA2100, CA3001, CA5350, etc.)
- `reliability` — Reliability issues (CA2000, CA2007, CA2016, etc.)
- `maintainability` — Code maintainability (CA1822, CA1506, CA1505, etc.)
- `usage` — API usage issues (CA1031, CA2201, CA2208, etc.)
- `design` — Design issues (CA1000, CA1001, CA1063, etc.)
- `globalization` — Globalization (CA1303, CA1304, CA1310, etc.)
- `naming` — Naming conventions (CA1700, CA1707, CA1715, etc.)
- `documentation` — Documentation (CA1200, CS1591, etc.)
- `logging` — Logging optimization (CA1848, CA1873, etc.)

**Priority Presets** (by importance):
- `critical` — Critical issues (security)
- `high` — High priority (reliability + key performance)
- `medium` — Medium priority (performance + maintainability)
- `low` — Low priority (style, naming)

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

1. **Start with Critical, then work down by priority:**
   ```javascript
   // First: Critical security issues
   analyze_code_style(preset: "critical")

   // Then: High priority (reliability + key performance)
   analyze_code_style(preset: "high")

   // Medium priority (performance + maintainability)
   analyze_code_style(preset: "medium")

   // Low priority (style, naming)
   analyze_code_style(preset: "low")
   ```

2. **Use category presets for focused improvements:**
   ```javascript
   // Performance week
   analyze_code_style(preset: "performance", severityFilter: "Info")

   // Security audit
   analyze_code_style(preset: "security", severityFilter: "Warning")

   // Code maintainability
   analyze_code_style(preset: "maintainability")
   ```

3. **Filter by specific areas for targeted fixes:**
   ```javascript
   // Focus on Services layer
   analyze_code_style(
       preset: "performance",
       filePatterns: "**/Services/*.cs"
   )

   // Check specific project
   analyze_code_style(
       diagnosticIds: "CA1822",
       projectNames: "MyProject.Core"
   )
   ```

4. **Use pagination for large projects:**
   ```javascript
   // First page
   analyze_code_style(severityFilter: "Warning", skip: 0, take: 100)

   // Second page
   analyze_code_style(severityFilter: "Warning", skip: 100, take: 100)
   ```

5. **Leverage caching for iterative work:**
   ```javascript
   // First query - scans solution (30 sec)
   analyze_code_style(preset: "performance")

   // Fix some issues, then check specific file (3 sec, uses cache!)
   analyze_code_style(
       preset: "performance",
       filePatterns: "**/UserService.cs"
   )

   // Check different category (3 sec, still uses cache!)
   analyze_code_style(preset: "security")
   ```

6. **Automate fixes by diagnostic code:**
   ```javascript
   // Analysis
   analyze_code_style(diagnosticIds: "CA1860")
   // Output: "47 instances of CA1860"

   // Auto-fixes
   apply_code_fixes(diagnosticId: "CA1860", preview: false)

   // Re-analyze (uses cache - fast!)
   analyze_code_style(diagnosticIds: "CA1860")
   // Output: "0 instances" ✅
   ```

7. **Track progress over time:**
   ```javascript
   // Baseline
   analyze_code_style(preset: "high")
   // "147 high priority issues"

   // After work
   analyze_code_style(preset: "high")
   // "98 issues" - 33% improvement!
   ```

### Performance

**First analysis (cold cache):**
- **Small project (3-5 projects):** 5-10 sec
- **Medium project (10-20 projects):** 15-30 sec
- **Large project (50+ projects):** 45-90 sec

**Repeated analysis (cached, within 5 minutes): 3-5 sec** — **10x faster!** 🚀

**Caching:**
- ✅ All diagnostics cached for 5 minutes
- ✅ Filters applied after retrieval (instant)
- ✅ Repeated queries with different filters use cache
- ✅ Cache auto-expires after 5 minutes

**Example:**
```javascript
// First run - scans everything (30 sec)
analyze_code_style(solutionPath: "...", preset: "performance")

// Immediate second run with different filter - uses cache (3 sec)
analyze_code_style(solutionPath: "...", preset: "security")

// Different project name filter - still uses cache (3 sec)
analyze_code_style(solutionPath: "...", projectNames: "Core")
```

**Factors affecting speed:**
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

## cleanup_usings

**Remove redundant global usings** — scans solution for using directives that duplicate global usings declared in `GlobalUsings.cs` files. Removes redundant usings to keep code clean.

### Usage

```javascript
// Preview mode (see what will be removed)
cleanup_usings(
    path: "D:/MyProject",
    preview: true
)

// Apply mode (remove redundant usings)
cleanup_usings(
    path: "D:/MyProject",
    preview: false
)
```

### Parameters

- **path** (required): Path to solution directory
- **preview** (default: true): `true` — preview only, `false` — apply changes

### What It Does

1. 🔍 **Scans for GlobalUsings.cs files** in all projects
2. 📋 **Collects global using directives** (e.g., `global using System;`)
3. 🔎 **Scans all .cs files** for matching regular usings
4. 🗑️ **Removes redundant usings** that duplicate global usings
5. 💾 **Saves changes** (if not preview mode)
6. 🌳 **Creates Git commit** (if files changed)

### When to Use

✅ **After adding GlobalUsings.cs:**
- Migrated to C# 10 implicit usings
- Added new global using directives
- Want to clean up redundant usings

✅ **For code cleanup:**
- Reduce using statement clutter
- Enforce global using pattern
- Standardize namespace imports

✅ **During migration:**
- Upgrading from older .NET versions
- Consolidating common usings
- Preparing for code review

### Example

**GlobalUsings.cs:**
```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using Microsoft.Extensions.Logging;
```

**Before cleanup (UserService.cs):**
```csharp
using System;                           // ❌ Redundant (in GlobalUsings)
using System.Collections.Generic;       // ❌ Redundant (in GlobalUsings)
using System.Linq;                      // ❌ Redundant (in GlobalUsings)
using System.Threading.Tasks;           // ✅ Keep (not in GlobalUsings)
using Microsoft.Extensions.Logging;     // ❌ Redundant (in GlobalUsings)
using MyProject.Domain;                 // ✅ Keep (not in GlobalUsings)

namespace MyProject.Services;
public class UserService { ... }
```

**After cleanup:**
```csharp
using System.Threading.Tasks;
using MyProject.Domain;

namespace MyProject.Services;
public class UserService { ... }
```

### Best Practices

1. **Always preview first:**
   ```javascript
   cleanup_usings(path: "D:/MyProject", preview: true)
   // Output: "Found 47 redundant usings in 23 files"

   cleanup_usings(path: "D:/MyProject", preview: false)
   // Apply
   ```

2. **Use with FormatCode:**
   ```javascript
   cleanup_usings(path: "D:/MyProject", preview: false)
   format_code(path: "D:/MyProject/src", checkOnly: false)
   ```

3. **Combine with other quality tools:**
   ```javascript
   // Full quality workflow
   cleanup_usings(path: "D:/MyProject", preview: false)
   apply_code_fixes(diagnosticId: "IDE0005", preview: false)
   format_code(path: "src/", checkOnly: false)
   ```

### Performance

- **Preview:** 2-5 sec (scans all files)
- **Apply:** 3-10 sec (modifies files + Git commit)

### Related Tools

- ➡️ [**ApplyCodeFixes**](#apply_code_fixes) — remove unused usings (IDE0005)
- ➡️ [**FormatCode**](#format_code) — format after cleanup
- ⬅️ [**ManageUsings**](./ULTRA_SHARP_ANALYSIS.md#manage_usings) — manual using management

---

## Workflow: Comprehensive Quality Improvement

### Standard Workflow

```javascript
// 1. Formatting
format_code(path: "src/", checkOnly: true)
// Output: "12 files need formatting"

format_code(path: "src/", checkOnly: false)
// Applied formatting

// 2. Analysis by priority (NEW approach)
// Start with critical security issues
analyze_code_style(preset: "critical")
// Output: "3 security issues" ⚠️

// Fix critical issues first...

// Then high priority (reliability + performance)
analyze_code_style(preset: "high")
// Output: "43 high priority issues"

// 3. Systematic fixes by diagnostic code
// Find specific issue type
analyze_code_style(diagnosticIds: "CA1860")
// Output: "47 instances of CA1860"

apply_code_fixes(diagnosticId: "CA1860", preview: false)
// Fixed automatically

// Re-check (uses cache - fast!)
analyze_code_style(diagnosticIds: "CA1860")
// Output: "0 instances" ✅

// 4. Continue with other issues
analyze_code_style(diagnosticIds: "IDE0005,CS8019")
apply_code_fixes(diagnosticId: "IDE0005", preview: false)
apply_code_fixes(diagnosticId: "CS8019", preview: false)

// 5. Focus on specific areas
analyze_code_style(
    preset: "performance",
    filePatterns: "**/Services/*.cs"
)
// Targeted fixes for Services layer

// 6. Final check by priority
analyze_code_style(preset: "high")
// Output: "5 high priority issues remaining"

// Manual fixes if needed...

// 7. All clean ✅
format_code(path: "src/", checkOnly: true)
analyze_code_style(preset: "critical")
// Output: "0 critical issues" ✅
```

### NEW: Systematic Code Quality Improvement Workflow

```javascript
// Day 1: Critical issues
analyze_code_style(preset: "critical")
// Fix all security issues

// Day 2: High priority
analyze_code_style(preset: "high", take: 20)
// Fix 20 issues per day

// Day 3: Continue high priority (uses cache!)
analyze_code_style(preset: "high", skip: 20, take: 20)
// Next 20 issues

// Day 4: Performance improvements
analyze_code_style(preset: "performance", severityFilter: "Info")
// Focus on performance

// Day 5: Maintainability
analyze_code_style(preset: "maintainability")
// CA1822, CA1506, etc.

// Track progress
analyze_code_style(preset: "high")
// "Before: 147 issues → After: 25 issues" (83% improvement!)
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
| **CleanupUsings** | Remove redundant usings | ✅ Yes | ✅ Yes | Fast (3-10 sec) |

---

## See Also

- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — modifications with automatic quality checks
- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — code analysis
- 📚 [**ULTRA_SHARP_SOLUTION.md**](./ULTRA_SHARP_SOLUTION.md) — load solution first
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

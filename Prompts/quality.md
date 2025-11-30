# Code Quality & Formatting

**Code quality tools** — formatting, Roslyn analyzers, automatic fixes.

---

## Tools

| Tool | Purpose | Auto-fix |
|------|---------|----------|
| **format_code** | Format via CSharpier | ✅ |
| **analyze_code_style** | Roslyn analyzers | - |
| **apply_code_fixes** | Automatic fixes | ✅ |
| **cleanup_usings** | Remove duplicate usings | ✅ |
| **validate_file** | Validate single file | - |
| **validate_directory** | Batch validate directory | - |

---

## format_code

**Code formatting** via Roslyn Formatter.

```javascript
// Check (preview)
format_code(
    path: "D:/MyProject/src/",
    checkOnly: true
)

// Apply formatting
format_code(
    path: "D:/MyProject/src/",
    checkOnly: false
)
```

**Supports:**
- `.cs` files
- `.csproj` files
- `.xml` files

---

## analyze_code_style

**Roslyn analyzers** — find code smells, potential bugs.

```javascript
analyze_code_style(
    solutionPath: "D:/MyProject/MyProject.sln",
    severityFilter: "Warning",  // Hidden, Info, Warning, Error
    preset: "performance",       // optional: performance, security, reliability
    diagnosticIds: "CA1822,IDE0005"  // optional: specific diagnostics
)
```

**Presets:**
- `performance` — performance issues
- `security` — security vulnerabilities
- `reliability` — code reliability
- `maintainability` — maintainability
- `critical` — critical issues

---

## apply_code_fixes

**Automatic fixes** for Roslyn diagnostics.

```javascript
// Preview
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "IDE0005",  // or "all"
    preview: true
)

// Apply
apply_code_fixes(
    solutionPath: "D:/MyProject/MyProject.sln",
    diagnosticId: "all",
    preview: false
)
```

**Common diagnostics:**
- `IDE0005` — unused usings
- `CS8019` — unnecessary using directives
- `CA1822` — can be made static

---

## cleanup_usings

**Remove duplicate usings** — removes usings already declared in GlobalUsings.cs.

```javascript
// Preview
cleanup_usings(
    path: "D:/MyProject/src/",
    preview: true
)

// Apply
cleanup_usings(
    path: "D:/MyProject/src/",
    preview: false
)
```

---

## validate_file

**Validate single file** with Roslyn analyzers.

```javascript
validate_file(
    filePath: "D:/MyProject/src/Services/UserService.cs",
    minSeverity: "Warning"  // Error, Warning, Info, Hidden
)
```

---

## validate_directory

**Batch validate directory** — parallel processing.

```javascript
validate_directory(
    directoryPath: "D:/MyProject/src/",
    recursive: true,
    maxFiles: 100,
    minSeverity: "Warning"
)
```

---

## Recommended Workflow

```javascript
// 1. After modifications — format
format_code(path: "src/", checkOnly: false)

// 2. Quality analysis
analyze_code_style(
    solutionPath: "MyProject.sln",
    severityFilter: "Warning"
)

// 3. Auto-fixes
apply_code_fixes(
    solutionPath: "MyProject.sln",
    diagnosticId: "all",
    preview: false
)

// 4. Cleanup usings
cleanup_usings(path: "src/", preview: false)
```

---

## compare_validation

**Compare quality before/after** — track improvements.

```javascript
compare_validation(
    errorsBefore: 15,
    warningsBefore: 47,
    errorsAfter: 3,
    warningsAfter: 12
)
```

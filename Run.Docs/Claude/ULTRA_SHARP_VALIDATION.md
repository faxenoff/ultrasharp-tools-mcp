# Code Validation & Quality Tools

[← Back to Overview](./ULTRA_SHARP.md)

**Automated code quality checks** — validate C# code with Roslyn analyzers, batch process directories, track quality improvements.

---

## 📋 Quick Reference

| Tool | Purpose | Batch Support | Use Case |
|------|---------|---------------|----------|
| **validate_file** | Validate single C# file | No | Pre-commit checks, single file analysis |
| **validate_directory** | Batch validate directory | Yes | CI/CD pipelines, project-wide analysis |
| **compare_validation** | Compare before/after | No | Track quality improvements, regression detection |

---

## validate_file

**Validate a single C# file** — runs Roslyn analyzers and returns all diagnostics with location information.

### Usage

```javascript
validate_file(
    filePath: "D:/MyProject/Services/UserService.cs",
    minSeverity: "Warning"
)
```

### Parameters

- **filePath** (required): Absolute path to `.cs` file to validate
- **minSeverity** (default: `"Warning"`): Minimum severity to report
  - `"Error"` — Only compilation errors
  - `"Warning"` — Errors + warnings (default)
  - `"Info"` — Errors + warnings + suggestions
  - `"Hidden"` — Everything including hidden diagnostics

### Returns

```json
{
  "filePath": "D:/MyProject/Services/UserService.cs",
  "totalProblems": 5,
  "summary": {
    "errors": 1,
    "warnings": 3,
    "info": 1,
    "hidden": 0
  },
  "problems": [
    {
      "severity": "Error",
      "message": "The name 'invalidVar' does not exist in the current context",
      "ruleId": "CS0103",
      "location": {
        "line": 45,
        "column": 13,
        "endLine": 45,
        "endColumn": 23
      },
      "descriptor": "CS0103"
    },
    {
      "severity": "Warning",
      "message": "This async method lacks 'await' operators...",
      "ruleId": "CS1998",
      "location": {
        "line": 78,
        "column": 5,
        "endLine": 78,
        "endColumn": 32
      },
      "descriptor": "CS1998"
    }
  ],
  "truncated": false
}
```

### Notes

- Returns up to **100 problems** per file (oldest first by source position)
- File must be part of the loaded solution
- Uses Roslyn semantic model for deep analysis
- Includes all analyzer rules configured in `.editorconfig` or `GlobalAnalyzerConfig`

### Example Workflow

```javascript
// Before modification
const before = validate_file({
    filePath: "D:/MyProject/UserService.cs"
});

// Modify code with modify_code tool
modify_code({
    fullyQualifiedMemberName: "MyProject.UserService.ProcessUser",
    newMemberCode: "... fixed code ...",
    preview: false
});

// After modification
const after = validate_file({
    filePath: "D:/MyProject/UserService.cs"
});

// Compare
compare_validation({
    errorsBefore: before.summary.errors,
    warningsBefore: before.summary.warnings,
    errorsAfter: after.summary.errors,
    warningsAfter: after.summary.warnings
});
```

---

## validate_directory

**Batch validate all C# files in a directory** — processes multiple files in parallel with aggregated statistics.

### Usage

```javascript
validate_directory(
    directoryPath: "D:/MyProject/Services",
    recursive: true,
    minSeverity: "Warning",
    maxFiles: 100
)
```

### Parameters

- **directoryPath** (required): Directory to scan (absolute path)
- **recursive** (default: `true`): Search subdirectories
- **minSeverity** (default: `"Warning"`): Minimum severity (`"Error"`, `"Warning"`, `"Info"`, `"Hidden"`)
- **maxFiles** (default: `100`): Maximum files to process (1-1000, for performance)

### Returns

```json
{
  "directoryPath": "D:/MyProject/Services",
  "totalFiles": 23,
  "aggregated": {
    "totalErrors": 5,
    "totalWarnings": 47,
    "totalInfo": 12,
    "filesWithProblems": 15
  },
  "topProblematicFiles": [
    {
      "filePath": "D:/MyProject/Services/UserService.cs",
      "status": "validated",
      "errors": 3,
      "warnings": 12,
      "info": 2
    },
    {
      "filePath": "D:/MyProject/Services/OrderService.cs",
      "status": "validated",
      "errors": 2,
      "warnings": 8,
      "info": 1
    }
  ],
  "allFiles": [
    /* ... full list of all validated files ... */
  ]
}
```

### Performance

- **Parallel processing**: Processes files in batches of 10
- **Controlled concurrency**: Prevents resource exhaustion
- **Progress tracking**: Logs progress for large directories
- **Early termination**: Respects `maxFiles` limit

### File Status

Each file has a `status` field:
- `"validated"` — Successfully analyzed
- `"not_in_solution"` — File not part of loaded solution
- `"error"` — Validation failed (includes error message)

### Example: CI/CD Integration

```javascript
// Validate entire project
const report = validate_directory({
    directoryPath: "D:/MyProject/src",
    recursive: true,
    minSeverity: "Error"
});

// Fail build if errors found
if (report.aggregated.totalErrors > 0) {
    console.error(`Build failed: ${report.aggregated.totalErrors} errors found`);
    process.exit(1);
}

// Generate quality report
console.log(`Quality Report:
  Files: ${report.totalFiles}
  Errors: ${report.aggregated.totalErrors}
  Warnings: ${report.aggregated.totalWarnings}
  Clean files: ${report.totalFiles - report.aggregated.filesWithProblems}
`);
```

---

## compare_validation

**Compare validation results** — tracks quality improvements or regressions between two validation states.

### Usage

```javascript
compare_validation(
    errorsBefore: 5,
    warningsBefore: 23,
    errorsAfter: 2,
    warningsAfter: 18
)
```

### Parameters

- **errorsBefore** (required): Error count before changes
- **warningsBefore** (required): Warning count before changes
- **errorsAfter** (required): Error count after changes
- **warningsAfter** (required): Warning count after changes

### Returns

```json
{
  "before": {
    "errors": 5,
    "warnings": 23,
    "total": 28
  },
  "after": {
    "errors": 2,
    "warnings": 18,
    "total": 20
  },
  "changes": {
    "errorsFixed": 3,
    "warningsFixed": 5,
    "errorsIntroduced": 0,
    "warningsIntroduced": 0,
    "netErrorChange": -3,
    "netWarningChange": -5,
    "netTotalChange": -8
  },
  "status": "improved",
  "message": "✅ Code quality improved! Fixed 3 errors and 5 warnings (net: 8 problems resolved)"
}
```

### Status Values

- `"improved"` — Net reduction in problems (✅)
- `"degraded"` — Net increase in problems (⚠️)
- `"unchanged"` — Same number of problems

### Example: Refactoring Quality Check

```javascript
// Before refactoring
const beforeValidation = validate_file({
    filePath: "D:/MyProject/LegacyService.cs"
});

// Perform refactoring
modify_code({
    fullyQualifiedMemberName: "MyProject.LegacyService.ProcessData",
    newMemberCode: "... refactored code ...",
    preview: false
});

// After refactoring
const afterValidation = validate_file({
    filePath: "D:/MyProject/LegacyService.cs"
});

// Quality check
const comparison = compare_validation({
    errorsBefore: beforeValidation.summary.errors,
    warningsBefore: beforeValidation.summary.warnings,
    errorsAfter: afterValidation.summary.errors,
    warningsAfter: afterValidation.summary.warnings
});

if (comparison.status === "degraded") {
    console.warn("⚠️ Refactoring introduced new problems!");
    // Consider rollback with rollback_snapshot
}
```

---

## 🔧 Integration Patterns

### Pattern 1: Pre-Commit Hook

```javascript
// Validate changed files before commit
const changedFiles = getGitChangedFiles(); // Your git integration

for (const file of changedFiles.filter(f => f.endsWith('.cs'))) {
    const validation = validate_file({ filePath: file });

    if (validation.summary.errors > 0) {
        console.error(`❌ Cannot commit: ${file} has ${validation.summary.errors} errors`);
        process.exit(1);
    }
}

console.log("✅ All files validated successfully");
```

### Pattern 2: Quality Gate

```javascript
// Define quality thresholds
const MAX_WARNINGS_PER_FILE = 5;
const MAX_TOTAL_ERRORS = 0;

const report = validate_directory({
    directoryPath: "D:/MyProject/src",
    recursive: true
});

// Check thresholds
const highWarningFiles = report.allFiles.filter(f =>
    f.status === "validated" && f.warnings > MAX_WARNINGS_PER_FILE
);

if (report.aggregated.totalErrors > MAX_TOTAL_ERRORS) {
    console.error("❌ Quality gate failed: Errors found");
    process.exit(1);
}

if (highWarningFiles.length > 0) {
    console.warn(`⚠️ ${highWarningFiles.length} files exceed warning threshold`);
}
```

### Pattern 3: Validation-Before-After (with Snapshot)

```javascript
// 1. Create snapshot
const snapshot = create_snapshot({
    description: "Before code cleanup",
    files: ["D:/MyProject/Services/UserService.cs"]
});

// 2. Validate before
const before = validate_file({
    filePath: "D:/MyProject/Services/UserService.cs"
});

// 3. Modify code
modify_code({
    fullyQualifiedMemberName: "MyProject.UserService.ValidateUser",
    newMemberCode: "... cleaned code ...",
    preview: false
});

// 4. Validate after
const after = validate_file({
    filePath: "D:/MyProject/Services/UserService.cs"
});

// 5. Compare
const comparison = compare_validation({
    errorsBefore: before.summary.errors,
    warningsBefore: before.summary.warnings,
    errorsAfter: after.summary.errors,
    warningsAfter: after.summary.warnings
});

// 6. Rollback if degraded
if (comparison.status === "degraded") {
    console.warn("⚠️ Code quality degraded, rolling back...");
    rollback_snapshot({ snapshotId: snapshot.snapshotId });
}
```

---

## 💡 Best Practices

### ✅ DO

- **Validate before committing** — catch issues early
- **Use directory validation for CI/CD** — ensure project-wide quality
- **Track quality metrics** — use `compare_validation` to measure improvements
- **Set appropriate severity levels** — use `"Error"` for strict validation, `"Warning"` for general checks
- **Limit file count in CI** — use `maxFiles` parameter to control build time

### ❌ DON'T

- **Don't validate files outside solution** — they must be loaded with `load_solution` first
- **Don't ignore warnings** — they often indicate real issues
- **Don't skip validation after modifications** — always verify changes
- **Don't validate binary/generated files** — focus on source code

---

## 🐛 Troubleshooting

### Problem: "File not found in loaded solution"

**Solution:** Ensure you've called `load_solution` and the file is part of the solution:
```javascript
load_solution({ solutionPath: "D:/MyProject/MyProject.sln" });
validate_file({ filePath: "D:/MyProject/Services/UserService.cs" });
```

### Problem: Too many warnings

**Solution:** Increase severity threshold:
```javascript
validate_file({
    filePath: "D:/MyProject/Services/UserService.cs",
    minSeverity: "Error"  // Only errors, skip warnings
});
```

### Problem: Validation timeout on large directories

**Solution:** Reduce `maxFiles` or validate smaller subdirectories:
```javascript
validate_directory({
    directoryPath: "D:/MyProject/Services",
    maxFiles: 50,  // Reduced from default 100
    recursive: false  // Don't scan subdirectories
});
```

---

## 🔗 Related Tools

- **[modify_code](./ULTRA_SHARP_MODIFICATION.md)** — Modify code with automatic validation
- **[create_snapshot](./ULTRA_SHARP_VERSIONING.md)** — Create restore points before risky changes
- **[format_code](./ULTRA_SHARP_QUALITY.md)** — Format code to fix style issues
- **[analyze_code_style](./ULTRA_SHARP_QUALITY.md)** — Additional style analysis

---

**Next:** [File Operations →](./ULTRA_SHARP_FILE_OPS.md)
**Previous:** [← Snapshot & Versioning](./ULTRA_SHARP_VERSIONING.md)

# File Operations & Refactoring

[← Back to Overview](./ULTRA_SHARP.md)

**Large-scale file refactoring** — split large files into modules, combine small files, reorganize codebase structure.

---

## 📋 Quick Reference

| Tool | Purpose | Preview Mode | Use Case |
|------|---------|--------------|----------|
| **split_file** | Split file by types | ✅ Yes | Refactor "God classes", improve organization |
| **synthesize_files** | Combine multiple files | ✅ Yes | Consolidate utilities, prepare migrations |

---

## split_file

**Split a large C# file into multiple files** — creates one file per top-level type (class, interface, struct, enum, record).

### Usage

```javascript
split_file(
    filePath: "D:/MyProject/Models/AllModels.cs",
    targetDirectory: "D:/MyProject/Models",
    preview: true,
    deleteOriginal: false
)
```

### Parameters

- **filePath** (required): Absolute path to `.cs` file to split
- **targetDirectory** (required): Target directory for new files (absolute path)
- **preview** (default: `true`): Preview mode (shows what will be created without making changes)
- **deleteOriginal** (default: `false`): Delete original file after successful split

### Preview Mode (preview: true)

```json
{
  "operation": "split",
  "mode": "preview",
  "currentFile": "D:/MyProject/Models/AllModels.cs",
  "targetDirectory": "D:/MyProject/Models",
  "typeCount": 5,
  "newFiles": [
    {
      "fileName": "User.cs",
      "typeName": "User",
      "typeKind": "class",
      "lineCount": 45,
      "targetPath": "D:/MyProject/Models/User.cs"
    },
    {
      "fileName": "Order.cs",
      "typeName": "Order",
      "typeKind": "class",
      "lineCount": 67,
      "targetPath": "D:/MyProject/Models/Order.cs"
    },
    {
      "fileName": "UserRole.cs",
      "typeName": "UserRole",
      "typeKind": "enum",
      "lineCount": 8,
      "targetPath": "D:/MyProject/Models/UserRole.cs"
    }
  ],
  "deleteOriginal": false,
  "message": "Set preview=false to perform the split"
}
```

### Apply Mode (preview: false)

```json
{
  "operation": "split",
  "mode": "applied",
  "originalFile": "D:/MyProject/Models/AllModels.cs",
  "targetDirectory": "D:/MyProject/Models",
  "filesCreated": [
    "D:/MyProject/Models/User.cs",
    "D:/MyProject/Models/Order.cs",
    "D:/MyProject/Models/UserRole.cs",
    "D:/MyProject/Models/Product.cs",
    "D:/MyProject/Models/Category.cs"
  ],
  "typeCount": 5,
  "originalDeleted": false,
  "message": "Successfully split into 5 files"
}
```

### What Gets Preserved

- ✅ **Usings** — All `using` directives copied to each new file
- ✅ **Externs** — All `extern alias` declarations preserved
- ✅ **Namespaces** — Original namespace structure maintained
- ✅ **Formatting** — Code normalized with `NormalizeWhitespace()`

### Supported Types

- `class` — Classes (including nested classes become top-level)
- `interface` — Interfaces
- `struct` — Structures
- `enum` — Enumerations
- `record` — Record types (C# 9+)

### Example: Refactor God Class

**Before:** `AllModels.cs` (500 lines)
```csharp
using System;
using System.Collections.Generic;

namespace MyProject.Models;

public class User { /* 100 lines */ }
public class Order { /* 150 lines */ }
public class Product { /* 120 lines */ }
public enum UserRole { /* 10 lines */ }
public interface IRepository { /* 80 lines */ }
```

**After split:**
- `User.cs` (100 lines)
- `Order.cs` (150 lines)
- `Product.cs` (120 lines)
- `UserRole.cs` (10 lines)
- `IRepository.cs` (80 lines)

Each file contains:
```csharp
using System;
using System.Collections.Generic;

namespace MyProject.Models;

public class User
{
    // ... original code ...
}
```

### Workflow

```javascript
// 1. Preview split
const preview = split_file({
    filePath: "D:/MyProject/Models/AllModels.cs",
    targetDirectory: "D:/MyProject/Models/Split",
    preview: true
});

console.log(`Will create ${preview.typeCount} files:`);
preview.newFiles.forEach(f => {
    console.log(`  - ${f.fileName} (${f.typeKind}, ${f.lineCount} lines)`);
});

// 2. Apply split
split_file({
    filePath: "D:/MyProject/Models/AllModels.cs",
    targetDirectory: "D:/MyProject/Models/Split",
    preview: false,
    deleteOriginal: true  // Remove original after split
});

// 3. Update project file (if needed)
// Add new files to .csproj manually or rebuild project
```

### Notes

- Creates target directory if it doesn't exist
- Files are **overwritten** if they already exist
- Preserves file-scoped namespace syntax (`namespace X;`)
- Handles both traditional and file-scoped namespaces
- Code is auto-formatted with Roslyn's `NormalizeWhitespace()`

---

## synthesize_files

**Combine multiple C# files into a single file** — merges types, usings, and namespaces intelligently.

### Usage

```javascript
synthesize_files(
    filePaths: [
        "D:/MyProject/Utils/StringHelper.cs",
        "D:/MyProject/Utils/DateHelper.cs",
        "D:/MyProject/Utils/MathHelper.cs"
    ],
    targetFilePath: "D:/MyProject/Utils/Helpers.cs",
    preview: true,
    deleteOriginals: false
)
```

### Parameters

- **filePaths** (required): Array of absolute paths to `.cs` files to combine
- **targetFilePath** (required): Target file path (absolute, must end with `.cs`)
- **preview** (default: `true`): Preview mode (shows combined result)
- **deleteOriginals** (default: `false`): Delete source files after synthesis

### Preview Mode (preview: true)

```json
{
  "operation": "synthesize",
  "mode": "preview",
  "sourceFiles": [
    "D:/MyProject/Utils/StringHelper.cs",
    "D:/MyProject/Utils/DateHelper.cs",
    "D:/MyProject/Utils/MathHelper.cs"
  ],
  "targetFilePath": "D:/MyProject/Utils/Helpers.cs",
  "totalSourceFiles": 3,
  "combinedLineCount": 287,
  "combinedUsings": 5,
  "combinedTypes": 3,
  "namespaces": ["MyProject.Utils"],
  "deleteOriginals": false,
  "preview": "using System;\nusing System.Linq;\nusing System.Text;\n\nnamespace MyProject.Utils;\n\npublic static class StringHelper\n{\n    // ... code ...\n}\n\npublic static class DateHelper\n{\n    // ... code ...\n}\n\npublic static class MathHelper\n{\n    // ... code ...\n}",
  "message": "Set preview=false to create the file"
}
```

**Note:** Preview truncated if combined code exceeds 10,000 characters.

### Apply Mode (preview: false)

```json
{
  "operation": "synthesize",
  "mode": "applied",
  "sourceFiles": [
    "D:/MyProject/Utils/StringHelper.cs",
    "D:/MyProject/Utils/DateHelper.cs",
    "D:/MyProject/Utils/MathHelper.cs"
  ],
  "targetFilePath": "D:/MyProject/Utils/Helpers.cs",
  "totalSourceFiles": 3,
  "combinedLineCount": 287,
  "filesDeleted": [
    "D:/MyProject/Utils/StringHelper.cs",
    "D:/MyProject/Utils/DateHelper.cs",
    "D:/MyProject/Utils/MathHelper.cs"
  ],
  "message": "Successfully synthesized 3 files into D:/MyProject/Utils/Helpers.cs"
}
```

### Merging Strategy

**Usings:**
- Merged and **deduplicated** by namespace
- Sorted alphabetically
- Example:
  ```csharp
  // File 1: using System; using System.Linq;
  // File 2: using System; using System.Text;
  // Result: using System; using System.Linq; using System.Text;
  ```

**Namespaces:**
- Types from **same namespace** are grouped together
- Types **without namespace** are placed at root level
- Namespace structure is preserved

**Types:**
- All top-level types from all files are included
- No duplicate checking (assumes different type names)

### Example: Consolidate Utilities

**Before:** 3 separate files

`StringHelper.cs`:
```csharp
using System;

namespace MyProject.Utils;

public static class StringHelper
{
    public static bool IsNullOrEmpty(string value) => string.IsNullOrEmpty(value);
}
```

`DateHelper.cs`:
```csharp
using System;

namespace MyProject.Utils;

public static class DateHelper
{
    public static DateTime Today => DateTime.Today;
}
```

`MathHelper.cs`:
```csharp
using System;

namespace MyProject.Utils;

public static class MathHelper
{
    public static int Max(int a, int b) => Math.Max(a, b);
}
```

**After synthesis:** `Helpers.cs`
```csharp
using System;

namespace MyProject.Utils;

public static class StringHelper
{
    public static bool IsNullOrEmpty(string value) => string.IsNullOrEmpty(value);
}

public static class DateHelper
{
    public static DateTime Today => DateTime.Today;
}

public static class MathHelper
{
    public static int Max(int a, int b) => Math.Max(a, b);
}
```

### Workflow

```javascript
// 1. Preview synthesis
const preview = synthesize_files({
    filePaths: [
        "D:/MyProject/Utils/StringHelper.cs",
        "D:/MyProject/Utils/DateHelper.cs",
        "D:/MyProject/Utils/MathHelper.cs"
    ],
    targetFilePath: "D:/MyProject/Utils/Helpers.cs",
    preview: true
});

console.log(`Combining ${preview.totalSourceFiles} files:`);
console.log(`  - Total lines: ${preview.combinedLineCount}`);
console.log(`  - Usings: ${preview.combinedUsings}`);
console.log(`  - Types: ${preview.combinedTypes}`);

// 2. Apply synthesis
synthesize_files({
    filePaths: [
        "D:/MyProject/Utils/StringHelper.cs",
        "D:/MyProject/Utils/DateHelper.cs",
        "D:/MyProject/Utils/MathHelper.cs"
    ],
    targetFilePath: "D:/MyProject/Utils/Helpers.cs",
    preview: false,
    deleteOriginals: true  // Clean up after synthesis
});
```

### Notes

- Target file is **overwritten** if it exists
- All source files must have `.cs` extension
- Minimum 2 files required (returns error for single file)
- Combined code is auto-formatted with `NormalizeWhitespace()`
- Namespaces are preserved (not flattened)

---

## 🔧 Integration Patterns

### Pattern 1: Split-Refactor-Validate

```javascript
// 1. Create snapshot before splitting
const snapshot = create_snapshot({
    description: "Before splitting AllModels.cs",
    files: ["D:/MyProject/Models/AllModels.cs"]
});

// 2. Preview split
const preview = split_file({
    filePath: "D:/MyProject/Models/AllModels.cs",
    targetDirectory: "D:/MyProject/Models",
    preview: true
});

console.log(`Planning to create ${preview.typeCount} files`);

// 3. Apply split
split_file({
    filePath: "D:/MyProject/Models/AllModels.cs",
    targetDirectory: "D:/MyProject/Models",
    preview: false,
    deleteOriginal: false  // Keep original for now
});

// 4. Validate new files
for (const newFile of preview.newFiles) {
    const validation = validate_file({
        filePath: newFile.targetPath
    });

    if (validation.summary.errors > 0) {
        console.error(`❌ Error in ${newFile.fileName}: ${validation.summary.errors} errors`);
        rollback_snapshot({ snapshotId: snapshot.snapshotId });
        return;
    }
}

// 5. Delete original only after successful validation
fs.unlinkSync("D:/MyProject/Models/AllModels.cs");
console.log("✅ Split completed successfully");
```

### Pattern 2: Progressive Consolidation

```javascript
// Find all utility files in a directory
const utilFiles = fs.readdirSync("D:/MyProject/Utils")
    .filter(f => f.endsWith('Helper.cs'))
    .map(f => path.join("D:/MyProject/Utils", f));

console.log(`Found ${utilFiles.length} utility files to consolidate`);

// Synthesize in batches of 5 files
const BATCH_SIZE = 5;
for (let i = 0; i < utilFiles.length; i += BATCH_SIZE) {
    const batch = utilFiles.slice(i, i + BATCH_SIZE);

    if (batch.length < 2) continue; // Skip single files

    const targetFile = `D:/MyProject/Utils/Helpers_Batch${Math.floor(i / BATCH_SIZE)}.cs`;

    synthesize_files({
        filePaths: batch,
        targetFilePath: targetFile,
        preview: false,
        deleteOriginals: true
    });

    console.log(`✅ Created ${targetFile} from ${batch.length} files`);
}
```

### Pattern 3: Split with Auto-Import Updates

```javascript
// Note: Auto-import updates not yet implemented, manual workflow:

// 1. Split file
const result = split_file({
    filePath: "D:/MyProject/Services/AllServices.cs",
    targetDirectory: "D:/MyProject/Services",
    preview: false
});

// 2. Find all references to the original file
const references = find_references({
    fullyQualifiedName: "MyProject.Services.UserService"
});

// 3. Update imports manually (or use IDE refactoring)
console.log(`Update imports in ${references.length} locations`);
references.forEach(ref => {
    console.log(`  - ${ref.filePath}:${ref.lineNumber}`);
});

// Future: Automatic import updates will be added in later version
```

---

## 💡 Best Practices

### ✅ DO

- **Preview first** — always use `preview: true` before applying
- **Create snapshots** — use `create_snapshot` before risky operations
- **Validate after split** — use `validate_file` on all new files
- **Keep originals initially** — set `deleteOriginal: false` until verified
- **Check project files** — update `.csproj` if needed after split/synthesis

### ❌ DON'T

- **Don't split single-type files** — tool returns error if only 1 type found
- **Don't synthesize 1 file** — minimum 2 files required
- **Don't skip validation** — always verify generated files compile
- **Don't trust blindly** — review preview output before applying
- **Don't forget project updates** — new files may need manual `.csproj` updates

---

## 🐛 Troubleshooting

### Problem: "No type declarations found in file"

**Solution:** File must contain at least one top-level type (class, interface, struct, enum, record). Check file contents.

### Problem: Split creates files with compilation errors

**Solution:** Original file may have complex dependencies. Use `validate_file` to check:
```javascript
const validation = validate_file({
    filePath: "D:/MyProject/Models/AllModels.cs"
});

if (validation.summary.errors > 0) {
    console.error("Fix errors in original file before splitting");
}
```

### Problem: Synthesized file has duplicate usings

**Solution:** This is intentional! Duplicates are removed automatically. If you see duplicates in output, it's a bug — please report.

### Problem: Namespace structure changed after synthesis

**Solution:** Namespace structure is preserved. If you see changes, verify source files have consistent namespaces:
```javascript
// Check preview to see namespace handling
const preview = synthesize_files({
    filePaths: [...],
    targetFilePath: "...",
    preview: true
});

console.log("Namespaces:", preview.namespaces);
```

---

## 🔗 Related Tools

- **[create_snapshot](./ULTRA_SHARP_VERSIONING.md)** — Create restore points before file operations
- **[validate_file](./ULTRA_SHARP_VALIDATION.md)** — Validate generated files
- **[format_code](./ULTRA_SHARP_QUALITY.md)** — Format code after synthesis
- **[move_member](./ULTRA_SHARP_MODIFICATION.md)** — Move individual members between files

---

**Next:** [Semantic Search →](./ULTRA_SHARP_SEMANTIC.md)
**Previous:** [← Code Validation](./ULTRA_SHARP_VALIDATION.md)

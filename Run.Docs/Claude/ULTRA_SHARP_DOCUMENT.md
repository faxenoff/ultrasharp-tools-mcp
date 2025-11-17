# Document Operations

[← Back to Overview](./ULTRA_SHARP.md)

**Direct file operations** — reading, creating, overwriting documents. Use when you need full file control, not individual symbol manipulation.

---

## 📋 Quick Reference

| Tool | Purpose | Git Automation | When to Use |
|------|---------|----------------|-------------|
| **ReadRawFromRoslynDocument** | Read entire file | No | View configs, full file context |
| **ReadTypesFromRoslynDocument** | List types and members in file | No | File navigation, architectural overview |
| **CreateRoslynDocument** | Create new file | ✅ Branch + Commit | New classes, configurations |
| **OverwriteRoslynDocument** | Overwrite entire file | ✅ Branch + Commit | Mass file changes |

---

## When to Use Document Tools vs Symbol Tools

### ✅ Document Tools (these tools)
- Working with configuration files (appsettings.json, .csproj)
- Creating new classes/files
- Mass file overwrite
- Viewing entire file for context

### ✅ Symbol Tools (ULTRA_SHARP_ANALYSIS, ULTRA_SHARP_MODIFICATION)
- Working with C# code via semantic analysis
- Targeted changes (method, class)
- Renaming with reference updates
- Refactoring with compilation checks

**Rule of thumb:**
- For **.cs files with code** → use Symbol Tools (ViewDefinition, AddMember, etc.)
- For **non-code files** or **full file operations** → use Document Tools

---

## UltrasharpTool_ReadRawFromRoslynDocument

**Read entire file** — returns complete file contents without indentation (token efficient).

### Usage

```javascript
UltrasharpTool_ReadRawFromRoslynDocument(
    filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Parameters

- **filePath** (required): Full absolute path to file

### What It Shows

- 📄 **Entire file content** (without indentation for token savings)
- 📁 **File path**
- 📊 **Statistics** (lines, characters)

### When to Use

✅ **For understanding context:**
- See entire file at once
- Understand structure before modification
- Check using statements
- View namespace

✅ **For non-code files:**
- appsettings.json
- .csproj files
- .editorconfig
- Any configuration files

✅ **Before OverwriteRoslynDocument:**
- Read current contents
- Modify outside SharpTools
- Write back

### When NOT to Use

❌ **Don't use if:**
- Need only one class from file → `ViewDefinition`
- Need type list → `ReadTypesFromRoslynDocument`
- File is very large (>1000 lines) → use ViewDefinition for specific symbols

### Best Practices

1. **For .cs files prefer ViewDefinition:**
   ```javascript
   // ❌ Bad - read entire file (500 lines)
   UltrasharpTool_ReadRawFromRoslynDocument("UserService.cs")

   // ✅ Good - read only needed class
   UltrasharpTool_ViewDefinition("MyNamespace.UserService")
   ```

2. **For configurations - ReadRaw is ideal:**
   ```javascript
   // ✅ Good
   UltrasharpTool_ReadRawFromRoslynDocument("appsettings.json")
   UltrasharpTool_ReadRawFromRoslynDocument("MyProject.csproj")
   ```

### Performance

- **Small file (< 100 lines):** < 50 ms
- **Medium file (100-500 lines):** 50-200 ms
- **Large file (500-2000 lines):** 200-500 ms

**Token usage:**
- Without indentation: ~10% token savings
- For 500-line file: ~3000-5000 tokens

### Related Tools

- ➡️ [**ReadTypesFromRoslynDocument**](#UltrasharpTool_ReadTypesFromRoslynDocument) — for type navigation in file
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#UltrasharpTool_ViewDefinition) — for reading specific symbol
- ➡️ [**OverwriteRoslynDocument**](#UltrasharpTool_OverwriteRoslynDocument) — for overwriting file

---

## UltrasharpTool_ReadTypesFromRoslynDocument

**Structural file map** — returns hierarchy of types and their members in specific file.

### Usage

```javascript
UltrasharpTool_ReadTypesFromRoslynDocument(
    filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Parameters

- **filePath** (required): Full absolute path to .cs file

### What It Shows

**For each type in file:**
- 📦 **Namespace**
- 🏷️ **Type** (class, interface, struct, enum)
- 📋 **All members** with signatures
- 🔒 **Access modifiers**
- 🆔 **FQN** for use with other tools

### When to Use

✅ **For file navigation:**
- File contains multiple classes
- Need to understand file structure
- Get FQN for further analysis

✅ **For architectural analysis:**
- Understand code organization in file
- Find helper classes
- Check convention compliance (one class per file)

✅ **Transition from "file domain" to "type domain":**
- You know the file but not exact FQNs of types
- Want to use Symbol Tools (ViewDefinition, GetMembers)

### Best Practices

1. **Use for multi-class files:**
   ```javascript
   // File contains UserService, UserServiceException, UserServiceExtensions
   UltrasharpTool_ReadTypesFromRoslynDocument("UserService.cs")
   // Get FQN of all three types

   // Then view details of each
   UltrasharpTool_ViewDefinition("MyProject.Services.UserService")
   UltrasharpTool_ViewDefinition("MyProject.Services.UserServiceException")
   ```

2. **Alternative to LoadProject:**
   ```javascript
   // Instead of LoadProject for entire project
   UltrasharpTool_LoadProject("MyProject.Core")  // All files

   // Can use ReadTypes for specific file
   UltrasharpTool_ReadTypesFromRoslynDocument("UserService.cs")  // One file
   ```

### Performance

- **Speed:** 100-500 ms (depends on file size)
- **Faster than:** ReadRaw for navigation (doesn't load entire content)

### Related Tools

- ⬅️ [**LoadProject**](./ULTRA_SHARP_SOLUTION.md#UltrasharpTool_LoadProject) — for entire project overview
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#UltrasharpTool_ViewDefinition) — specific type details
- ➡️ [**GetMembers**](./ULTRA_SHARP_ANALYSIS.md#UltrasharpTool_GetMembers) — specific type members

---

## UltrasharpTool_CreateRoslynDocument

**Create new file** — creates new file with specified contents.

### Usage

```javascript
UltrasharpTool_CreateRoslynDocument(
    filePath: "D:/MyProject/src/Validators/EmailValidator.cs",
    content: `
using System;
using System.Text.RegularExpressions;

namespace MyProject.Validators
{
    /// <summary>
    /// Validates email addresses.
    /// </summary>
    public class EmailValidator
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+$",
            RegexOptions.Compiled
        );

        public static bool IsValid(string email)
        {
            return !string.IsNullOrWhiteSpace(email) &&
                EmailRegex.IsMatch(email);
        }
    }
}`,
    commitMessage: "Add EmailValidator class"
)
```

### Parameters

- **filePath** (required): Full absolute path of new file
- **content** (required): File contents (without indentation, will be auto-formatted)
- **commitMessage** (required): Git commit message

### What It Does

1. ✅ Checks file doesn't exist
2. 📁 Creates directories if needed
3. 📝 Writes content
4. 🎨 Formats (for .cs files)
5. ✅ Checks compilation (for .cs files)
6. 🌳 Git branch + commit
7. 📊 Returns status

### When to Use

✅ **For new classes/files:**
- Create new service
- Create new controller
- Create helper class
- Create interface

✅ **For configuration files:**
- New appsettings.{env}.json
- New .editorconfig
- Documentation (.md files)

✅ **For tests:**
- Create new test file
- Create mock class

### Best Practices

1. **Write without indentation (auto-format):**
   ```javascript
   // ✅ Correct - code will be formatted
   content: `
namespace MyNamespace
{
public class MyClass
{
public void MyMethod()
{
Console.WriteLine("Test");
}
}
}`
   ```

2. **Include full namespace and usings:**
   ```javascript
   // ✅ Correct - self-sufficient file
   content: `
using System;
using MyProject.Domain;

namespace MyProject.Services
{
public class NewService { ... }
}`
   ```

3. **For .cs files - one public class per file:**
   ```javascript
   // ✅ Good
   // EmailValidator.cs contains only EmailValidator

   // ⚠️ Acceptable but not recommended
   // EmailValidator.cs contains EmailValidator + EmailValidatorException
   ```

### Common Errors

#### ❌ Error: "File already exists"
```
ERROR: File already exists: D:/MyProject/src/Services/UserService.cs
```
**Solution:**
- Use different file name
- Or use `OverwriteRoslynDocument` (⚠️ CAREFUL - overwrites file!)
- Or use `AddMember` to add to existing type

### Related Tools

- ➡️ [**AddMember**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_AddMember) — to add to existing class
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#UltrasharpTool_ViewDefinition) — verify created file
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#UltrasharpTool_FormatCode) — format after creation

---

## UltrasharpTool_OverwriteRoslynDocument

**⚠️ Overwrite entire file** — completely replaces existing file contents.

### ⚠️ WARNING

This is a **dangerous** tool - it completely deletes old file contents!

**Use:**
- ✅ Only for non-code files (json, xml, config)
- ✅ After `ReadRawFromRoslynDocument` to save modified content
- ✅ When sure you need to replace entire file

**DON'T use:**
- ❌ For changing C# code → use Symbol Tools
- ❌ Without first reading file
- ❌ For partial changes → use AddMember/OverwriteMember

### Usage

```javascript
// 1. First read
UltrasharpTool_ReadRawFromRoslynDocument(
    filePath: "D:/MyProject/appsettings.json"
)

// 2. Modify content outside SharpTools

// 3. Write back
UltrasharpTool_OverwriteRoslynDocument(
    filePath: "D:/MyProject/appsettings.json",
    content: `{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyDb;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}`,
    commitMessage: "Update connection string in appsettings.json"
)
```

### Parameters

- **filePath** (required): Full absolute path to existing file
- **content** (required): New contents (NO INDENTATION for .cs, with indentation for json/xml)
- **commitMessage** (required): Git commit message

### What It Does

1. ✅ Checks file exists
2. 🗑️ **Deletes all contents**
3. 📝 Writes new contents
4. 🎨 Formats (for .cs files)
5. ✅ Checks compilation (for .cs files)
6. 🌳 Git branch + commit

### When to Use

✅ **For configuration files:**
- appsettings.json
- .csproj (but careful!)
- .editorconfig
- launchSettings.json

✅ **For generated files:**
- Auto-generated code
- Build artifacts
- Temporary files

### When NOT to Use

❌ **NEVER use for:**
- Changing one method in .cs → `OverwriteMember`
- Adding member to class → `AddMember`
- Renaming → `RenameSymbol`
- Regex replacements → `FindAndReplace`
- Any targeted code changes

### Best Practices

1. **ALWAYS read before writing:**
   ```javascript
   // ✅ CORRECT
   UltrasharpTool_ReadRawFromRoslynDocument(filePath: "config.json")
   // Modify content
   UltrasharpTool_OverwriteRoslynDocument(filePath: "config.json", content: modified, ...)

   // ❌ DANGEROUS - don't know what was in file
   UltrasharpTool_OverwriteRoslynDocument(filePath: "config.json", content: ..., ...)
   ```

2. **Use Undo if mistake:**
   ```javascript
   UltrasharpTool_OverwriteRoslynDocument(...)
   // Oops, error!

   UltrasharpTool_Undo()
   // Restored old content
   ```

3. **For .cs files prefer Symbol Tools:**
   ```javascript
   // ❌ Bad - overwrite entire file
   UltrasharpTool_OverwriteRoslynDocument("UserService.cs", newContent, ...)

   // ✅ Good - change only needed method
   UltrasharpTool_OverwriteMember("UserService.MyMethod", newMethodCode, ...)
   ```

### Related Tools

- ⬅️ [**ReadRawFromRoslynDocument**](#UltrasharpTool_ReadRawFromRoslynDocument) — REQUIRED before overwrite
- ➡️ [**Undo**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_Undo) — if something went wrong
- ➡️ [**CreateRoslynDocument**](#UltrasharpTool_CreateRoslynDocument) — for new files

---

## Workflow: Modifying Configuration File

```javascript
// 1. Read current contents
UltrasharpTool_ReadRawFromRoslynDocument(
    filePath: "D:/MyProject/appsettings.json"
)
// Output: { "ConnectionStrings": {...}, "Logging": {...} }

// 2. Modify (in your code, outside SharpTools)
//    For example, parse JSON, modify, serialize back

// 3. Write back
UltrasharpTool_OverwriteRoslynDocument(
    filePath: "D:/MyProject/appsettings.json",
    content: modifiedJson,
    commitMessage: "Update database connection string"
)

// 4. Verify
UltrasharpTool_ReadRawFromRoslynDocument(
    filePath: "D:/MyProject/appsettings.json"
)
// Verify changes
```

## Workflow: Creating New Class

```javascript
// 1. Check class doesn't exist
UltrasharpTool_SearchDefinitions("EmailValidator")
// Output: "No matches"

// 2. Create file
UltrasharpTool_CreateRoslynDocument(
    filePath: "D:/MyProject/src/Validators/EmailValidator.cs",
    content: `
using System;

namespace MyProject.Validators
{
    public class EmailValidator
    {
        public static bool IsValid(string email)
        {
            // Implementation
            return true;
        }
    }
}`,
    commitMessage: "Add EmailValidator class"
)

// 3. Verify
UltrasharpTool_ViewDefinition("MyProject.Validators.EmailValidator")

// 4. Format
UltrasharpTool_FormatCode(path: "D:/MyProject/src/Validators/EmailValidator.cs", checkOnly: false)

// 5. Check quality
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
```

---

## Tool Comparison

| Tool | Reads | Writes | Risk | Use Case |
|------|-------|--------|------|----------|
| **ReadRaw** | ✅ | ❌ | Safe ✅ | View files |
| **ReadTypes** | ✅ (structure) | ❌ | Safe ✅ | File navigation |
| **Create** | ❌ | ✅ | Low ✅ | New files |
| **Overwrite** | ❌ | ✅ | High ⚠️ | Configs, full replacement |

---

## See Also

- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — for targeted C# code changes
- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — for semantic analysis
- 📚 [**ULTRA_SHARP_QUALITY.md**](./ULTRA_SHARP_QUALITY.md) — formatting after creation
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

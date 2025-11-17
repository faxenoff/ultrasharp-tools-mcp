# Code Modification & Refactoring

[← Back to Overview](./ULTRA_SHARP.md)

**High-precision C# code modification operations via Roslyn API.** All modifications automatically create Git branches/commits, are checked for compilation errors, and can be reverted via Undo.

---

## 📋 Quick Reference

| Tool | Purpose | Git Automation | Auto-lint |
|------|---------|----------------|-----------|
| **AddMember** | Add method/property/class to type | ✅ Branch + Commit | ✅ Yes |
| **OverwriteMember** | Replace or delete existing member | ✅ Branch + Commit | ✅ Yes |
| **RenameSymbol** | Rename with reference updates | ✅ Branch + Commit | ✅ Yes |
| **FindAndReplace** | Regex replacement in code | ✅ Branch + Commit | ✅ Yes |
| **MoveMember** | Move member to different type/namespace | ✅ Branch + Commit | ✅ Yes |
| **Undo** | Rollback last modification | ✅ Git revert | - |

---

## ⚠️ Important Modification Features

### 🔄 Automatic Git Integration

**Each modification:**
1. ✅ Creates new branch `sharptools/YYYYMMDD-HHMMSS`
2. ✅ Makes commit with change description
3. ✅ Saves changes for possible Undo

**Example workflow:**
```bash
# Starting branch: main

add_member(...)
# Created branch: sharptools/20251113-140523
# Commit: "Add method CreateUser to UserService"

rename_symbol(...)
# Created branch: sharptools/20251113-140612
# Commit: "Rename oldName to newName"

undo()
# Rollback last commit, return to sharptools/20251113-140523
```

**Disabling Git:**
```bash
# When starting server
UltrasharpTools.Droid.exe --disable-git
```

### ✅ Automatic Linting

**Each modification is checked:**
1. ✅ **Compilation errors** — immediate report
2. ✅ **Syntax errors** — detection before save
3. ✅ **Format check** — warning if formatting needed
4. ✅ **Code style warnings** — optional but useful

**Recommended workflow:**
```javascript
// 1. Modification
add_member(...)
// Output: "✅ No compilation errors. ⚠️ Consider running FormatCode"

// 2. Formatting
format_code(path: "src/", checkOnly: false)

// 3. Linting
analyze_code_style(severityFilter: "Warning")

// 4. Auto-fixes
apply_code_fixes(diagnosticId: "all", preview: false)
```

### 🔙 undo Mechanism

**How it works:**
- Stores stack of recent changes
- Rollback via `git reset --hard HEAD~1`
- Can rollback multiple changes sequentially

**Limitations:**
- ⚠️ Cannot undo if there were external changes
- ⚠️ Cannot undo if switched to another branch
- ⚠️ Undo stack resets on server restart

---

## add_member

**Add new member** — method, property, field, nested class to existing type.

### Usage

```javascript
add_member(
    fullyQualifiedTargetName: "MyNamespace.UserService",
    codeSnippet: `
/// <summary>
/// Validates user email format.
/// </summary>
private bool ValidateEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        return false;

    return Regex.IsMatch(email, @"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
}`,
    fileNameHint: "auto",
    lineNumberHint: -1,
    commitMessage: "Add email validation method"
)
```

### Parameters

- **fullyQualifiedTargetName** (required): FQN of parent type
- **codeSnippet** (required): Code of new member (without indentation, will be auto-formatted)
- **fileNameHint** (required): File name for partial types, `"auto"` — auto-select
- **lineNumberHint** (required): Desired insertion line, `-1` — auto-select
- **commitMessage** (required): Git commit message

### What It Does

1. 🔍 Finds target type via FuzzyFqnLookup
2. 📝 Parses codeSnippet via Roslyn
3. 📍 Determines insertion point (lineNumberHint or end of type)
4. ➕ Inserts new member
5. 🎨 Formats code (CSharpier)
6. ✅ Checks compilation
7. 🌳 Creates Git branch + commit
8. 📊 Returns diff

### When to Use

✅ **For adding functionality:**
- New method to existing class
- New property
- New field
- Nested class/enum

✅ **For extending API:**
- Add public method
- Add extension method
- Add helper method

### Best Practices

1. **Write code without indentation (will be auto-formatted):**
   ```javascript
   // ✅ Correct - no indentation
   codeSnippet: `
public void MyMethod()
{
Console.WriteLine("Test");
}`
   ```

2. **Include XML documentation:**
   ```javascript
   codeSnippet: `
/// <summary>
/// Validates user data.
/// </summary>
/// <param name="user">User to validate.</param>
/// <returns>True if valid.</returns>
public bool ValidateUser(User user)
{
    // ...
}`
   ```

3. **Use meaningful commit messages:**
   ```javascript
   // ✅ Good
   commitMessage: "Add email validation to UserService"

   // ❌ Bad
   commitMessage: "add method"
   ```

### Related Tools

- ⬅️ [**GetMembers**](./ULTRA_SHARP_ANALYSIS.md#get_members) — view existing members before adding
- ⬅️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — see context where adding
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#format_code) — format after adding
- ➡️ [**Undo**](#undo) — rollback if mistake

---

## modify_code

**Replace or delete existing member** — complete replacement of method/property/class definition with new code or deletion.

### Usage

```javascript
// Replace
modify_code(
    fullyQualifiedMemberName: "MyNamespace.UserService.ValidateEmail",
    newMemberCode: `
/// <summary>
/// Validates email using improved regex.
/// </summary>
private bool ValidateEmail(string email)
{
    return !string.IsNullOrWhiteSpace(email) &&
        Regex.IsMatch(email, @"^[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+$");
}`,
    commitMessage: "Improve email validation regex"
)

// Delete
modify_code(
    fullyQualifiedMemberName: "MyNamespace.UserService.ObsoleteMethod",
    newMemberCode: "// Delete ObsoleteMethod",
    commitMessage: "Remove obsolete method"
)
```

### Parameters

- **fullyQualifiedMemberName** (required): FQN of member to replace
- **newMemberCode** (required): New code (or `"// Delete {name}"` for deletion)
- **commitMessage** (required): Git commit message

### What It Does

1. 🔍 Finds existing member
2. 🗑️ Deletes old definition (completely, including attributes/XML docs)
3. ➕ Inserts new definition (if not deletion)
4. 🎨 Formats code
5. ✅ Checks compilation
6. 🌳 Git branch + commit
7. 📊 Returns diff

### When to Use

✅ **For changing logic:**
- Fix bug in method
- Improve algorithm
- Change validation

✅ **For refactoring:**
- Simplify complex method
- Extract variables
- Change structure

✅ **For deletion:**
- Remove obsolete methods
- Remove unused fields
- Remove deprecated API

### Best Practices

1. **Always include attributes and XML docs:**
   ```javascript
   // ✅ Correct - full definition
   newMemberCode: `
[Obsolete("Use NewMethod instead")]
/// <summary>
/// Old method.
/// </summary>
public void OldMethod() { ... }`
   ```

2. **Check impact before changing:**
   ```javascript
   // First check where it's used
   find_references("MyClass.MyMethod")

   // Then modify
   modify_code(...)
   ```

3. **For deletion use correct syntax:**
   ```javascript
   // ✅ Correct
   newMemberCode: "// Delete MyMethod"

   // ❌ Wrong
   newMemberCode: ""
   ```

### Related Tools

- ⬅️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — see current definition
- ⬅️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — check impact
- ➡️ [**Undo**](#undo) — rollback if something went wrong

---

## rename_symbol

**Rename symbol** — changes name and automatically updates all references in solution.

### Usage

```javascript
rename_symbol(
    fullyQualifiedSymbolName: "MyNamespace.UserService.ValidateEmail",
    newName: "ValidateEmailFormat",
    commitMessage: "Rename ValidateEmail to ValidateEmailFormat for clarity"
)
```

### Parameters

- **fullyQualifiedSymbolName** (required): FQN of symbol to rename
- **newName** (required): New name (without namespace, just name)
- **commitMessage** (required): Git commit message

### What It Does

1. 🔍 Finds symbol and all its references
2. ✏️ Renames definition
3. 🔄 Updates all call sites
4. 🎨 Formats affected files
5. ✅ Checks compilation
6. 🌳 Git branch + commit
7. 📊 Returns list of modified files

### When to Use

✅ **For improving clarity:**
- Rename poorly named variables
- Follow naming conventions
- Make code more readable

✅ **For refactoring:**
- Rename after changing responsibility
- Fix typos
- Align naming

✅ **Safe renaming:**
- Method used in 100+ places
- Class with many references
- Interface with many implementations

### Performance

- **Small scope (< 10 refs):** < 1 sec
- **Medium scope (10-100 refs):** 2-5 sec
- **Large scope (100+ refs):** 5-15 sec

### Best Practices

1. **Check rename scope:**
   ```javascript
   // First see how many references
   find_references("OldName")
   // Output: "147 references in 42 files"

   // If many - make sure you want to change everything
   rename_symbol("OldName", "NewName", "...")
   ```

2. **Use descriptive commit messages:**
   ```javascript
   // ✅ Good - clear reasoning
   commitMessage: "Rename User to Customer for domain consistency"

   // ❌ Bad
   commitMessage: "rename"
   ```

### Related Tools

- ⬅️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — see scope before renaming
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#format_code) — format affected files
- ➡️ [**Undo**](#undo) — rollback if needed

---

## find_and_replace

**Regex find/replace** — text replacement in code via regex patterns. Works with FQN (within symbol) or glob paths (in files).

### Usage

```javascript
// Within symbol
find_and_replace(
    regexPattern: "Console\\.WriteLine\\((.*)\\)",
    replacementText: "_logger.LogInformation($1)",
    target: "MyNamespace.UserService.ProcessUser",
    commitMessage: "Replace Console.WriteLine with logger"
)

// In files (glob)
find_and_replace(
    regexPattern: "var\\s+(\\w+)\\s*=\\s*new\\s+List<",
    replacementText: "var $1 = [",
    target: "src/**/*.cs",
    commitMessage: "Use collection expressions"
)
```

### Parameters

- **regexPattern** (required): Regex in multiline mode (use `\\s*` for indentation)
- **replacementText** (required): Replacement (can include groups: `$1`, `${name}`)
- **target** (required): FQN of symbol OR glob path (`*` supported)
- **commitMessage** (required): Git commit message

### Regex Modes

**Multiline mode:**
- `^` and `$` work for each line
- `.` does NOT match `\n` (use `[\s\S]` for any character)

**Important for indentation:**
```javascript
// ✅ Correct - accounts for unknown indentation
regexPattern: "^\\s*if\\s*\\("

// ❌ Bad - won't work if different indentation
regexPattern: "^if\\s*\\("
```

### When to Use

✅ **For mass changes:**
- Replace deprecated API with new one
- Change naming convention
- Fix typos
- Update version numbers

✅ **For refactoring:**
- Replace using with collection expressions
- Change null checks to null-coalescing
- Simplify boolean expressions

✅ **For migration:**
- Update namespace after renaming
- Replace deprecated attributes
- Change syntax to new C# version

### Example Regex Patterns

```javascript
// Replace var with explicit type
regexPattern: "var\\s+(\\w+)\\s*=\\s*new\\s+(\\w+)",
replacementText: "$2 $1 = new $2"

// Remove trailing whitespace
regexPattern: "\\s+$",
replacementText: ""

// Replace == null with is null
regexPattern: "(\\w+)\\s*==\\s*null",
replacementText: "$1 is null"

// Add async/await
regexPattern: "public\\s+(\\w+)\\s+(\\w+)\\(",
replacementText: "public async Task<$1> $2Async("

// Replace string.IsNullOrEmpty with IsNullOrWhiteSpace
regexPattern: "string\\.IsNullOrEmpty\\((.*)\\)",
replacementText: "string.IsNullOrWhiteSpace($1)"
```

### Best Practices

1. **Test regex separately:**
   ```javascript
   // Use online regex tester (regex101.com)
   // Test on code examples
   // Then apply in SharpTools
   ```

2. **Use capture groups:**
   ```javascript
   // ✅ Preserve parts of original code
   regexPattern: "if\\s*\\((.*)\\s*==\\s*null\\)",
   replacementText: "if ($1 is null)"

   // Was: if (user == null)
   // Now: if (user is null)
   ```

3. **Account for indentation:**
   ```javascript
   // ✅ Correct - works with any indentation
   regexPattern: "^\\s*Console\\.WriteLine",

   // ❌ Wrong - will miss indented code
   regexPattern: "^Console\\.WriteLine"
   ```

4. **Use glob for mass changes:**
   ```javascript
   // All .cs files in src/
   target: "src/**/*.cs"

   // Only Controllers
   target: "src/API/Controllers/*.cs"

   // Specific file
   target: "src/Services/UserService.cs"
   ```

### Related Tools

- ⬅️ [**SearchDefinitions**](./ULTRA_SHARP_ANALYSIS.md#search_definitions) — preview matches
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#format_code) — format after replacements
- ➡️ [**Undo**](#undo) — rollback if something went wrong

---

## move_member

**Move member** — moves method/property/field from one type to another type or namespace.

### Usage

```javascript
move_member(
    fullyQualifiedMemberName: "MyNamespace.UserService.ValidateEmail",
    fullyQualifiedDestinationTypeOrNamespaceName: "MyNamespace.Validators.EmailValidator",
    commitMessage: "Move email validation to EmailValidator class"
)
```

### Parameters

- **fullyQualifiedMemberName** (required): FQN of member to move
- **fullyQualifiedDestinationTypeOrNamespaceName** (required): FQN of target type or namespace
- **commitMessage** (required): Git commit message

### What It Does

1. 🔍 Finds source member
2. 📋 Copies definition (with attributes, XML docs)
3. 🗑️ Removes from source
4. ➕ Adds to destination
5. 🔄 Updates using statements (if needed)
6. 🎨 Formats affected files
7. ✅ Checks compilation
8. 🌳 Git branch + commit

### When to Use

✅ **For refactoring:**
- Move method to more appropriate class
- Extract utility methods to helper class
- Organize code by responsibility

✅ **For improving structure:**
- Follow Single Responsibility Principle
- Reduce coupling
- Improve cohesion

### Best Practices

1. **Check references before moving:**
   ```javascript
   find_references("UserService.ValidateEmail")
   // Understand impact

   move_member(...)
   // Update call sites manually
   ```

2. **Check access modifiers:**
   ```javascript
   // If was private - may become public in new class
   // Check and change if necessary
   ```

3. **Update tests:**
   ```javascript
   // After moving update unit tests
   // They may reference old location
   ```

### Related Tools

- ⬅️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — check impact
- ⬅️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — see member before moving
- ➡️ [**Undo**](#undo) — rollback if needed

---

## undo

**Rollback last change** — reverts last modification via Git revert.

### Usage

```javascript
undo()
```

### Parameters

No parameters.

### What It Does

1. 📜 Checks last commit in sharptools/* branch
2. ⬅️ Executes `git reset --hard HEAD~1`
3. 📊 Returns information about reverted changes

### When to Use

✅ **For quick rollback:**
- Made mistake in modification
- Changed mind after modification
- Want to try different approach

✅ **For experimentation:**
- Trying different refactoring variants
- A/B testing different implementations

### Limitations

⚠️ **Cannot undo if:**
- Switched to another branch
- There were external changes (other dev, IDE)
- Server was restarted (undo stack reset)
- Git was disabled (`--disable-git`)

### Best Practices

1. **Use immediately if mistake:**
   ```javascript
   add_member(...)
   // Output: "ERROR: Compilation failed"

   undo()
   // Quickly rollback

   // Fix and try again
   add_member(...) // fixed version
   ```

2. **Can rollback multiple changes:**
   ```javascript
   undo() // Rollback last
   undo() // Rollback second-to-last
   undo() // And one more
   ```

---

## Workflow: Safe Modification

### Standard Workflow

```javascript
// 1. Analyze current state
view_definition("MyClass.MyMethod")
get_members("MyClass", false)
find_references("MyClass.MyMethod")

// 2. Modification
modify_code(
    fullyQualifiedMemberName: "MyClass.MyMethod",
    newMemberCode: "/* new implementation */",
    commitMessage: "Improve MyMethod performance"
)

// 3. Verification
// Output: "✅ No compilation errors"

// 4. Quality checks
format_code(path: "src/", checkOnly: false)
analyze_code_style(severityFilter: "Warning")
apply_code_fixes(diagnosticId: "all", preview: false)

// 5. Testing (outside SharpTools)
// dotnet test

// 6. If all ok - merge to main
// git checkout main
// git merge sharptools/20251113-XXX

// 7. If problem - rollback
undo()
```

### Workflow for Breaking Changes

```javascript
// 1. Assess impact
find_references("MyClass.OldMethod")
// Output: "147 references in 42 files" - many!

// 2. Create new method instead of changing old one
add_member(
    fullyQualifiedTargetName: "MyClass",
    codeSnippet: `
[Obsolete("Use NewMethod instead")]
public void OldMethod() {
    NewMethod(); // delegate to new
}

public void NewMethod() {
    // new implementation
}`,
    commitMessage: "Add NewMethod, deprecate OldMethod"
)

// 3. Gradually migrate call sites
find_and_replace(
    regexPattern: "\\.OldMethod\\(",
    replacementText: ".NewMethod(",
    target: "src/Module1/**/*.cs",
    commitMessage: "Migrate Module1 to NewMethod"
)

// Repeat for other modules...

// 4. When all migrated - remove old
modify_code(
    fullyQualifiedMemberName: "MyClass.OldMethod",
    newMemberCode: "// Delete OldMethod",
    commitMessage: "Remove deprecated OldMethod"
)
```

---

## Tool Comparison

| Tool | Scope | Updates References | Risk of Breaking Changes |
|------|-------|-------------------|-------------------------|
| **AddMember** | Single type | No | Low |
| **OverwriteMember** | Single member | No | High (if signature changes) |
| **RenameSymbol** | Solution-wide | Yes | Low |
| **FindAndReplace** | Symbol or files | No | Medium |
| **MoveMember** | 2 types | Partially | Medium |
| **Undo** | Last change | Reverts | None |

---

## See Also

- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — analyze before modifying
- 📚 [**ULTRA_SHARP_QUALITY.md**](./ULTRA_SHARP_QUALITY.md) — format and lint after modifications
- 📚 [**ULTRA_SHARP_SOLUTION.md**](./ULTRA_SHARP_SOLUTION.md) — load solution first
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

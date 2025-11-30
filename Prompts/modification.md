# Code Modification & Refactoring

**C# code modification via Roslyn API** — automatic Git commits, compilation checks, rollback support.

---

## Tools

| Tool | Purpose | Git | Auto-lint |
|------|---------|-----|-----------|
| **add_member** | Add method/property | ✅ | ✅ |
| **modify_code** | Replace/delete member | ✅ | ✅ |
| **rename_symbol** | Rename with reference updates | ✅ | ✅ |
| **find_and_replace** | Regex replacement in code | ✅ | ✅ |
| **move_member** | Move member to another type | ✅ | ✅ |
| **undo** | Rollback last change | ✅ | - |

---

## add_member

**Add new member** — method, property, field, nested class.

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

**Important:**
- Write code **without indentation** (auto-formatted)
- Include XML documentation
- Use meaningful commit messages

---

## modify_code

**Replace or delete existing member.**

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

---

## rename_symbol

**Rename symbol** — automatically updates all references in solution.

```javascript
rename_symbol(
    fullyQualifiedSymbolName: "MyNamespace.UserService.ValidateEmail",
    newName: "ValidateEmailFormat",
    commitMessage: "Rename ValidateEmail to ValidateEmailFormat for clarity"
)
```

**Performance:**
- < 10 refs: < 1 sec
- 10-100 refs: 2-5 sec
- 100+ refs: 5-15 sec

---

## find_and_replace

**Regex replacement in code** — works with FQN (inside symbol) or glob paths.

```javascript
// Inside class (via FQN)
find_and_replace(
    regexPattern: "\\.LogInformation\\(",
    replacementText: ".LogDebug(",
    target: "MyNamespace.UserService",  // FQN of class!
    commitMessage: "Lower log level in UserService"
)

// In files (glob)
find_and_replace(
    regexPattern: "Console\\.WriteLine\\((.*)\\)",
    replacementText: "_logger.LogInformation($1)",
    target: "src/**/*.cs",
    commitMessage: "Replace Console.WriteLine with logger"
)
```

**Important:**
- `target` with FQN — replacement inside class (recommended)
- `target` with glob — replacement in files
- Use `\\s*` for unknown indentation

---

## move_member

**Move member** — from one type to another.

```javascript
move_member(
    fullyQualifiedMemberName: "MyNamespace.UserService.ValidateEmail",
    fullyQualifiedDestinationTypeOrNamespaceName: "MyNamespace.Validators.EmailValidator",
    commitMessage: "Move email validation to EmailValidator class"
)
```

---

## undo

**Rollback last change** via git reset.

```javascript
undo()
```

**Limitations:**
- ⚠️ Doesn't work after branch switch
- ⚠️ Doesn't work after external changes
- ⚠️ Stack resets on server restart

---

## Safe Workflow

```javascript
// 1. Analyze current state
view_definition("MyClass.MyMethod")
find_references("MyClass.MyMethod")

// 2. Modify
modify_code(
    fullyQualifiedMemberName: "MyClass.MyMethod",
    newMemberCode: "/* new implementation */",
    commitMessage: "Improve MyMethod performance"
)

// 3. Quality check
format_code(path: "src/", checkOnly: false)
analyze_code_style(severityFilter: "Warning")

// 4. If problem — rollback
undo()
```

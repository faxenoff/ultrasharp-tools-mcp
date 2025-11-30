# Code Analysis & Navigation

**C# code analysis tools** — understand structure, navigate code, find dependencies.

---

## Tools

| Tool | Purpose | Speed |
|------|---------|-------|
| **get_members** | List type members with signatures | < 100 ms |
| **view_definition** | Symbol source code | 1-3 sec |
| **find_references** | All usage locations | 2-15 sec |
| **list_implementations** | Interface/base class implementations | 0.5-2 sec |
| **search_definitions** | Regex search across definitions | 2-15 sec |
| **view_call_graph** | Method call graph | 1-3 sec |
| **analyze_complexity** | Code complexity metrics | 0.1-60 sec |

---

## get_members

**Quick API overview** — all type members with signatures and XML docs.

```javascript
get_members(
    fullyQualifiedTypeName: "MyNamespace.MyClass",
    includePrivateMembers: false
)
```

**Shows:**
- Full signatures with parameter types
- XML documentation
- Access modifiers
- FQN for use with other tools

---

## view_definition

**Symbol source code** — class, method, property definition with context.

```javascript
view_definition(
    fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

**For methods shows:**
- Source code (no indentation)
- Call graph (what method calls)
- Incoming calls (who calls this method)
- File location

---

## find_references

**All usage locations** — where symbol is used in solution.

```javascript
find_references(
    fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

**Shows:**
- File and line number
- Contextual code (lines around)
- Usage type (call, assignment, etc.)

---

## list_implementations

**Find implementations** — all classes implementing interface or inheriting class.

```javascript
list_implementations(
    fullyQualifiedSymbolName: "MyNamespace.IUserRepository"
)
```

---

## search_definitions

**Regex search** — search signatures, type/method names, declarations.

```javascript
search_definitions(
    regexPattern: ".*UserService.*"
)
```

**Example patterns:**
```javascript
"async Task.*"           // All async methods
".*Validate.*\\("        // Methods with "Validate"
"class.*Controller\\s*:" // Controllers
"interface I\\w+"        // Interfaces
```

---

## view_call_graph

**Method dependency graph** — incoming and outgoing calls.

```javascript
view_call_graph(
    fullyQualifiedMethodName: "MyNamespace.MyClass.MyMethod"
)
```

---

## analyze_complexity

**Complexity metrics** — cyclomatic, cognitive complexity, coupling.

```javascript
analyze_complexity(
    scope: "method",  // or "class", "project"
    target: "MyNamespace.MyClass.MyMethod"
)
```

**Thresholds:**
- ✅ Cyclomatic 1-10: Simple
- ⚠️ Cyclomatic 11-20: Needs attention
- 🔴 Cyclomatic 21+: Needs refactoring

---

## Common Scenarios

### Exploring New Class

```javascript
get_members("MyNamespace.UserService", includePrivateMembers: false)
view_definition("MyNamespace.UserService.CreateUser")
find_references("MyNamespace.UserService.CreateUser")
analyze_complexity(scope: "class", target: "MyNamespace.UserService")
```

### Preparing for Refactoring

```javascript
analyze_complexity(scope: "project", target: "MyProject.Core")
view_definition("MyProject.Services.ComplexMethod")
find_references("MyProject.Services.ComplexMethod")
```

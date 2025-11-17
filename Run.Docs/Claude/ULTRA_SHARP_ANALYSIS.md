# Code Analysis & Navigation

[← Back to Overview](./ULTRA_SHARP.md)

**Comprehensive toolkit for analyzing C# codebases**: view definitions, find usages, analyze complexity, manage using/attributes. All tools work through Roslyn API for precise semantic analysis.

---

## 📋 Quick Reference

| Tool | Purpose | Primary Use |
|------|---------|-------------|
| **GetMembers** | List all type members with signatures | Quick API overview |
| **ViewDefinition** | Source code of symbol with context | Understand implementation |
| **ListImplementations** | All interface/base class implementations | Find inheritance, polymorphism |
| **FindReferences** | All symbol usage locations | Understand where/how used |
| **SearchDefinitions** | Regex search through definitions | Find patterns, naming violations |
| **ManageUsings** | Read/write using directives | Add/remove usings |
| **ManageAttributes** | Read/write attributes | Add/change attributes |
| **AnalyzeComplexity** | Code complexity metrics | Find complex code for refactoring |

---

## UltrasharpTool_GetMembers

**Quick API overview** — returns all members (methods, properties, fields, events) with signatures and XML documentation.

### Usage

```javascript
UltrasharpTool_GetMembers(
    fullyQualifiedTypeName: "MyNamespace.MyClass",
    includePrivateMembers: false
)
```

### Parameters

- **fullyQualifiedTypeName** (required): FQN of type (class, interface, struct, enum)
- **includePrivateMembers** (required): `true` — all members, `false` — only public/protected/internal

### What It Shows

For each member:
- 🔹 **Signature** (complete, with parameter and return types)
- 📝 **XML documentation** (summary, remarks, params, returns)
- 🔒 **Access modifiers** (public, private, protected, internal)
- 🔧 **Modifiers** (static, virtual, abstract, override, sealed)
- 🆔 **FQN** for use with other tools

### When to Use

✅ **For exploring API:**
- First look at unfamiliar class
- Understand public interface
- Find method by signature
- Study parameters and return types

✅ **Before modifications:**
- See all existing members
- Avoid duplicate names
- Understand naming conventions

### Related Tools

- ➡️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see member implementation
- ➡️ [**ListImplementations**](#UltrasharpTool_ListImplementations) — for interfaces/base classes
- ➡️ [**FindReferences**](#UltrasharpTool_FindReferences) — where member is used

---

## UltrasharpTool_ViewDefinition

**Full source code of symbol** — returns definition of class, method, property with contextual information (call graph, type references).

### Usage

```javascript
UltrasharpTool_ViewDefinition(
    fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

### Parameters

- **fullyQualifiedSymbolName** (required): FQN of symbol (type, method, property, field)

### What It Shows

**For types (class, interface, struct):**
- 📄 Full source code (without indentation)
- 📦 Namespace and using directives
- 🔗 Base class and implemented interfaces
- 📊 Nested types
- 📁 File location

**For methods:**
- 📄 Method source code
- 📞 **Call graph** (what method calls)
- 🔍 **Incoming calls** (who calls this method — preview 3 examples)
- 📁 File location

**For properties/fields:**
- 📄 Definition
- 🔍 Where used (preview)
- 📁 File location

### When to Use

✅ **For understanding implementation:**
- How method works internally
- What class does
- What dependencies it uses

✅ **For analyzing execution flow:**
- What's called from method (call graph)
- Where method is called from (incoming calls)
- Understanding data flow

✅ **Before modifications:**
- See current implementation
- Understand impact on other methods
- Determine tests to verify

### Source Code Resolution

ViewDefinition can retrieve code from multiple sources:

1. **Local files** (instant)
2. **SourceLink** (downloads from GitHub/GitLab)
3. **Embedded PDB** (extracts from debug info)
4. **Decompilation** (ILSpy, if no sources)

**Priority:** Local → SourceLink → Embedded → Decompilation

### Performance

- **First call:** 1-3 sec (compilation + semantic model)
- **Repeated call:** < 100 ms (thanks to cache)
- **With call graph:** +0.5-2 sec (depends on complexity)

### Related Tools

- ⬅️ [**GetMembers**](#UltrasharpTool_GetMembers) — see member list first
- ➡️ [**FindReferences**](#UltrasharpTool_FindReferences) — all usage locations
- ➡️ [**TraceExecution**](./ULTRA_SHARP_TRACING.md#UltrasharpTool_TraceExecution) — detailed execution trace

---

## UltrasharpTool_ListImplementations

**Find descendants** — finds all interface implementations, abstract method implementations, or derived classes.

### Usage

```javascript
UltrasharpTool_ListImplementations(
    fullyQualifiedSymbolName: "MyNamespace.IUserRepository"
)
```

### Parameters

- **fullyQualifiedSymbolName** (required): FQN of interface, base class, or abstract method

### What It Shows

**For interfaces:**
- All classes implementing the interface
- Definition files and lines

**For base classes:**
- All derived classes
- Complete inheritance hierarchy

**For abstract methods:**
- All override implementations
- FQN for further analysis

### When to Use

✅ **For polymorphism analysis:**
- Find all interface implementations
- Understand dependency injection usage
- Find mock/test implementations

✅ **For refactoring:**
- Find all classes needing changes
- Check impact of interface changes
- Find duplicate implementations

✅ **For architectural analysis:**
- Build inheritance hierarchy
- Find SOLID principle violations
- Understand dependency graph

### Performance

- **Speed:** 0.5-2 sec (depends on codebase size)
- **Parallel:** Search across projects is parallelized

### Related Tools

- ➡️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see found class implementation
- ➡️ [**FindReferences**](#UltrasharpTool_FindReferences) — where interface is used
- ➡️ [**AnalyzeComplexity**](#UltrasharpTool_AnalyzeComplexity) — compare implementation complexity

---

## UltrasharpTool_FindReferences

**Find all usages** — finds all locations where symbol (method, property, class, etc.) is used with code context.

### Usage

```javascript
UltrasharpTool_FindReferences(
    fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

### Parameters

- **fullyQualifiedSymbolName** (required): FQN of symbol to search for

### What It Shows

For each usage location:
- 📁 **File and line number**
- 📝 **Contextual code** (several lines around)
- 🔍 **Usage type** (call, assignment, initialization, etc.)
- 📊 **Grouped by files**

### When to Use

✅ **For understanding impact:**
- Before deleting method
- Before changing signature
- Understand where and how API is used

✅ **For refactoring:**
- Find all places to update
- Check usage consistency
- Find incorrect usage

✅ **For debugging:**
- Where problematic method is called from
- What parameters are passed
- Understand call chain

### Performance

- **Small project:** 0.5-1 sec
- **Medium project:** 2-5 sec
- **Large project:** 5-15 sec

**Optimizations:**
- ✅ Early termination on limit reached
- ✅ Parallel search across projects
- ✅ SemanticModel caching

### Related Tools

- ⬅️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see symbol implementation
- ➡️ [**TraceBackwards**](./ULTRA_SHARP_TRACING.md#UltrasharpTool_TraceBackwards) — full call path
- ➡️ [**RenameSymbol**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_RenameSymbol) — rename everywhere

---

## UltrasharpTool_SearchDefinitions

**Regex search through definitions** — searches for patterns in signatures, type/method names, declarations. Works in both source code and compiled assemblies.

### Usage

```javascript
UltrasharpTool_SearchDefinitions(
    regexPattern: ".*UserService.*"
)
```

### Parameters

- **regexPattern** (required): Regex pattern for search (multiline mode)

### What It Shows

For each match:
- 🔍 **FQN** of found symbol
- 📄 **Full declaration** (signature)
- 📁 **Location** (file:line or assembly)
- 🏷️ **Symbol type** (class, method, property, interface)

### When to Use

✅ **For finding patterns:**
- Find all async methods
- Find all methods containing "Validate"
- Find naming violations

✅ **For refactoring:**
- Find duplicated functionality
- Find deprecated patterns
- Find inconsistent naming

✅ **For architectural analysis:**
- Find all Controllers
- Find all Repositories
- Find all classes ending with "Service"

### Example Regex Patterns

```javascript
// All async methods
"async Task.*"

// All methods containing "Validate"
".*Validate.*\\("

// All classes ending with "Controller"
"class.*Controller\\s*:"

// All interfaces starting with "I"
"interface I\\w+"

// All List<> properties
"List<.*>.*\\{.*get"

// All methods with [HttpPost] attribute
"\\[HttpPost\\].*"
```

### Performance

- **Source code:** 2-5 sec (fast)
- **Compiled assemblies:** 5-15 sec (slower, decompilation)

**Dual-engine:**
1. Search in source code (Roslyn Syntax API)
2. Search in compiled assemblies (Reflection + Decompilation)

### Related Tools

- ➡️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see details of found item
- ➡️ [**FindReferences**](#UltrasharpTool_FindReferences) — where found symbol is used
- ➡️ [**AnalyzeComplexity**](#UltrasharpTool_AnalyzeComplexity) — analyze found methods

---

## UltrasharpTool_ManageUsings

**Manage using directives** — read and write using statements in file.

### Usage

```javascript
// Read
UltrasharpTool_ManageUsings(
    operation: "read",
    codeToWrite: "None",
    filePath: "D:/MyProject/src/Services/UserService.cs"
)

// Write
UltrasharpTool_ManageUsings(
    operation: "write",
    codeToWrite: "using System;\nusing System.Linq;\nusing MyProject.Domain;",
    filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Parameters

- **operation** (required): `"read"` or `"write"`
- **codeToWrite** (required): For read: `"None"`, for write: complete using list
- **filePath** (required): Full path to .cs file

### What It Does

**Read:**
- Returns all current using directives
- Shows order

**Write:**
- **REPLACES** all using directives with specified ones
- Automatically formats
- Creates Git commit

### When to Use

✅ **For adding dependencies:**
- Add using for new type
- Add using for extension methods

### Best Practices

**Better use ApplyCodeFixes instead:**
```javascript
// ❌ Manual using management
UltrasharpTool_ManageUsings(...)

// ✅ Automatic unused removal
UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005")
```

### Related Tools

- ➡️ [**ApplyCodeFixes**](./ULTRA_SHARP_QUALITY.md#UltrasharpTool_ApplyCodeFixes) — auto-remove unused usings
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#UltrasharpTool_FormatCode) — organize usings

---

## UltrasharpTool_ManageAttributes

**Manage attributes** — read and write attributes on declarations (class, method, property, etc.).

### Usage

```javascript
// Read
UltrasharpTool_ManageAttributes(
    operation: "read",
    codeToWrite: "None",
    targetDeclaration: "MyNamespace.MyClass.MyMethod"
)

// Write
UltrasharpTool_ManageAttributes(
    operation: "write",
    codeToWrite: "[Obsolete(\"Use NewMethod instead\")]\n[EditorBrowsable(EditorBrowsableState.Never)]",
    targetDeclaration: "MyNamespace.MyClass.MyMethod"
)
```

### Parameters

- **operation** (required): `"read"` or `"write"`
- **codeToWrite** (required): For read: `"None"`, for write: all attributes
- **targetDeclaration** (required): FQN of declaration (type, method, property, field)

### What It Does

**Read:**
- Returns all current attributes

**Write:**
- **REPLACES** all attributes with specified ones
- Creates Git commit

### When to Use

✅ **For adding metadata:**
- Add [Obsolete]
- Add API documentation attributes
- Add validation attributes

✅ **For changing configuration:**
- Change routing ([HttpGet], [Route])
- Change authorization ([Authorize])
- Change serialization ([JsonProperty])

### Best Practices

**Always read first, then write:**
```javascript
// ✅ Correct - preserve existing
UltrasharpTool_ManageAttributes(operation: "read", codeToWrite: "None", targetDeclaration: "...")
// Output: [Existing1]\n[Existing2]

// Add new attribute
UltrasharpTool_ManageAttributes(
    operation: "write",
    codeToWrite: "[Existing1]\n[Existing2]\n[NewAttribute]",
    targetDeclaration: "..."
)
```

### Related Tools

- ⬅️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see current attributes
- ➡️ [**OverwriteMember**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_OverwriteMember) — for larger changes

---

## UltrasharpTool_AnalyzeComplexity

**Analyze complexity metrics** — calculates cyclomatic complexity, cognitive complexity, coupling, inheritance depth, method statistics.

### Usage

```javascript
// Analyze method
UltrasharpTool_AnalyzeComplexity(
    scope: "method",
    target: "MyNamespace.MyClass.MyMethod"
)

// Analyze class
UltrasharpTool_AnalyzeComplexity(
    scope: "class",
    target: "MyNamespace.MyClass"
)

// Analyze project
UltrasharpTool_AnalyzeComplexity(
    scope: "project",
    target: "MyProject.Core"
)
```

### Parameters

- **scope** (required): `"method"`, `"class"`, or `"project"`
- **target** (required): FQN of method/class or project name

### What It Shows

**For method:**
- 🔢 **Cyclomatic Complexity** (number of execution paths)
- 🧠 **Cognitive Complexity** (understanding difficulty)
- 📏 **Lines of Code**
- 🔀 **Branch Count** (if/switch/loop)
- 📊 **Expression Depth** (nesting)

**For class:**
- 📊 **All method metrics** (top 10 complex)
- 🔗 **Coupling** (dependencies on other types)
- 📈 **Inheritance Depth**
- 📦 **Member Count**
- ⚠️ **Warnings** (high complexity)

**For project:**
- 📊 **Summary statistics** across all classes
- 🏆 **Top 20 most complex methods**
- 🏆 **Top 20 most complex classes**
- 📈 **Complexity distribution**
- ⚠️ **Hotspots** for refactoring

### Threshold Values

**Cyclomatic Complexity:**
- ✅ 1-10: Simple, easily testable
- ⚠️ 11-20: Moderately complex, needs attention
- 🔴 21+: Very complex, needs refactoring

**Cognitive Complexity:**
- ✅ 1-15: Easy to understand
- ⚠️ 16-25: Requires effort to understand
- 🔴 26+: Hard to understand and maintain

**Coupling (Instability):**
- ✅ 0.0-0.3: Stable (few dependencies)
- ⚠️ 0.3-0.7: Balanced
- 🔴 0.7-1.0: Unstable (many dependencies)

### When to Use

✅ **For finding technical debt:**
- Find most complex methods
- Find classes with high coupling
- Prioritize refactoring

✅ **Before refactoring:**
- Measure baseline complexity
- Choose refactoring target
- Verify improvement after refactoring

✅ **For code review:**
- Check new code complexity
- Enforce complexity standards
- Prevent "god classes"

✅ **For architectural analysis:**
- Find tight coupling
- Find single responsibility violations
- Assess maintainability

### Performance

- **Method:** < 100 ms
- **Class:** 0.5-2 sec
- **Project:** 10-60 sec (depends on size)

### Related Tools

- ⬅️ [**ViewDefinition**](#UltrasharpTool_ViewDefinition) — see complex method code
- ⬅️ [**SearchDefinitions**](#UltrasharpTool_SearchDefinitions) — find all methods to analyze
- ➡️ [**OverwriteMember**](./ULTRA_SHARP_MODIFICATION.md#UltrasharpTool_OverwriteMember) — refactor complex method

---

## Tool Comparison

| Tool | What It Finds | Speed | Use Case |
|------|---------------|-------|----------|
| **GetMembers** | All type members | < 100 ms | API overview |
| **ViewDefinition** | Source code + call graph | 1-3 sec | Understand implementation |
| **ListImplementations** | Descendants/implementations | 0.5-2 sec | Polymorphism |
| **FindReferences** | All usages | 2-15 sec | Impact analysis |
| **SearchDefinitions** | Regex patterns | 2-15 sec | Find naming/patterns |
| **AnalyzeComplexity** | Complexity metrics | 0.1-60 sec | Technical debt |

---

## Common Usage Scenarios

### Exploring New Class

```javascript
// 1. Quick public API overview
UltrasharpTool_GetMembers("MyNamespace.UserService", includePrivateMembers: false)

// 2. Details of interesting method
UltrasharpTool_ViewDefinition("MyNamespace.UserService.CreateUser")

// 3. Where this method is used
UltrasharpTool_FindReferences("MyNamespace.UserService.CreateUser")

// 4. Check complexity
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "MyNamespace.UserService")
```

### Preparing for Refactoring

```javascript
// 1. Find most complex methods in project
UltrasharpTool_AnalyzeComplexity(scope: "project", target: "MyProject.Core")

// 2. Details of most complex method
UltrasharpTool_ViewDefinition("MyProject.Services.ComplexMethod")

// 3. All places where it's used
UltrasharpTool_FindReferences("MyProject.Services.ComplexMethod")

// 4. Refactoring plan...
```

### Analyzing Interface and Implementations

```javascript
// 1. View interface contract
UltrasharpTool_GetMembers("IUserRepository", includePrivateMembers: false)

// 2. Find all implementations
UltrasharpTool_ListImplementations("IUserRepository")

// 3. Compare implementations by complexity
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "SqlUserRepository")
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "InMemoryUserRepository")

// 4. Specific implementation details
UltrasharpTool_ViewDefinition("SqlUserRepository.GetByIdAsync")
```

### Finding Duplicate Logic

```javascript
// 1. Find all methods containing "Validate"
UltrasharpTool_SearchDefinitions(".*Validate.*Email.*")

// 2. View each implementation
UltrasharpTool_ViewDefinition("UserService.ValidateEmail")
UltrasharpTool_ViewDefinition("EmailValidator.ValidateEmailFormat")

// 3. Find where they're used
UltrasharpTool_FindReferences("UserService.ValidateEmail")
UltrasharpTool_FindReferences("EmailValidator.ValidateEmailFormat")

// 4. Decide which to keep, consolidate duplicate logic
```

### Code Review of New Code

```javascript
// 1. Check public API
UltrasharpTool_GetMembers("NewFeature.NewService", includePrivateMembers: false)

// 2. Check key method implementation
UltrasharpTool_ViewDefinition("NewFeature.NewService.ProcessData")

// 3. Check complexity
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "NewFeature.NewService")

// 4. Check naming conventions
UltrasharpTool_SearchDefinitions("NewFeature.*(?!Async).*async Task")
```

---

## See Also

- 📚 [**ULTRA_SHARP_SOLUTION.md**](./ULTRA_SHARP_SOLUTION.md) — LoadSolution, LoadProject
- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — code modifications
- 📚 [**ULTRA_SHARP_TRACING.md**](./ULTRA_SHARP_TRACING.md) — execution tracing
- 📚 [**ULTRA_SHARP_QUALITY.md**](./ULTRA_SHARP_QUALITY.md) — formatting and linting
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

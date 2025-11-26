# Code Analysis & Navigation

[← Back to Overview](./ULTRA_SHARP.md)

**Comprehensive toolkit for analyzing C# codebases**: view definitions, find usages, analyze complexity, manage using/attributes. All tools work through Roslyn API for precise semantic analysis.

---

## 📋 Quick Reference

| Tool | Purpose | Primary Use |
|------|---------|-------------|
| **get_members** | List all type members with signatures | Quick API overview |
| **view_definition** | Source code of symbol with context | Understand implementation |
| **list_implementations** | All interface/base class implementations | Find inheritance, polymorphism |
| **find_references** | All symbol usage locations | Understand where/how used |
| **search_definitions** | Regex search through definitions | Find patterns, naming violations |
| **get_all_subtypes** | Recursively list nested types | Explore type hierarchy |
| **view_inheritance_chain** | Show base types and derived types | Understand inheritance |
| **view_call_graph** | Show incoming/outgoing calls | Analyze method dependencies |
| **pattern_search** | Multi-mode search (entity/content/semantic/hybrid) | Advanced code search |
| **find_duplicates** | Find semantically similar methods | Identify refactoring opportunities |
| **manage_usings** | Read/write using directives | Add/remove usings |
| **manage_attributes** | Read/write attributes | Add/change attributes |
| **analyze_complexity** | Code complexity metrics | Find complex code for refactoring |

---

## get_members

**Quick API overview** — returns all members (methods, properties, fields, events) with signatures and XML documentation.

### Usage

```javascript
get_members(
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

- ➡️ [**ViewDefinition**](#view_definition) — see member implementation
- ➡️ [**ListImplementations**](#list_implementations) — for interfaces/base classes
- ➡️ [**FindReferences**](#find_references) — where member is used

---

## view_definition

**Full source code of symbol** — returns definition of class, method, property with contextual information (call graph, type references).

### Usage

```javascript
view_definition(
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

- ⬅️ [**GetMembers**](#get_members) — see member list first
- ➡️ [**FindReferences**](#find_references) — all usage locations
- ➡️ [**TraceExecution**](./ULTRA_SHARP_TRACING.md#trace_execution) — detailed execution trace

---

## list_implementations

**Find descendants** — finds all interface implementations, abstract method implementations, or derived classes.

### Usage

```javascript
list_implementations(
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

- ➡️ [**ViewDefinition**](#view_definition) — see found class implementation
- ➡️ [**FindReferences**](#find_references) — where interface is used
- ➡️ [**AnalyzeComplexity**](#analyze_complexity) — compare implementation complexity

---

## find_references

**Find all usages** — finds all locations where symbol (method, property, class, etc.) is used with code context.

### Usage

```javascript
find_references(
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

- ⬅️ [**ViewDefinition**](#view_definition) — see symbol implementation
- ➡️ [**TraceBackwards**](./ULTRA_SHARP_TRACING.md#trace_backwards) — full call path
- ➡️ [**RenameSymbol**](./ULTRA_SHARP_MODIFICATION.md#rename_symbol) — rename everywhere

---

## search_definitions

**Regex search through definitions** — searches for patterns in signatures, type/method names, declarations. Works in both source code and compiled assemblies.

### Usage

```javascript
search_definitions(
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

- ➡️ [**ViewDefinition**](#view_definition) — see details of found item
- ➡️ [**FindReferences**](#find_references) — where found symbol is used
- ➡️ [**AnalyzeComplexity**](#analyze_complexity) — analyze found methods

---

## manage_usings

**Manage using directives** — read and write using statements in file.

### Usage

```javascript
// Read
manage_usings(
    operation: "read",
    codeToWrite: "None",
    filePath: "D:/MyProject/src/Services/UserService.cs"
)

// Write
manage_usings(
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
manage_usings(...)

// ✅ Automatic unused removal
apply_code_fixes(diagnosticId: "IDE0005")
```

### Related Tools

- ➡️ [**ApplyCodeFixes**](./ULTRA_SHARP_QUALITY.md#apply_code_fixes) — auto-remove unused usings
- ➡️ [**FormatCode**](./ULTRA_SHARP_QUALITY.md#format_code) — organize usings

---

## manage_attributes

**Manage attributes** — read and write attributes on declarations (class, method, property, etc.).

### Usage

```javascript
// Read
manage_attributes(
    operation: "read",
    codeToWrite: "None",
    targetDeclaration: "MyNamespace.MyClass.MyMethod"
)

// Write
manage_attributes(
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
manage_attributes(operation: "read", codeToWrite: "None", targetDeclaration: "...")
// Output: [Existing1]\n[Existing2]

// Add new attribute
manage_attributes(
    operation: "write",
    codeToWrite: "[Existing1]\n[Existing2]\n[NewAttribute]",
    targetDeclaration: "..."
)
```

### Related Tools

- ⬅️ [**ViewDefinition**](#view_definition) — see current attributes
- ➡️ [**OverwriteMember**](./ULTRA_SHARP_MODIFICATION.md#modify_code) — for larger changes

---

## analyze_complexity

**Analyze complexity metrics** — calculates cyclomatic complexity, cognitive complexity, coupling, inheritance depth, method statistics.

### Usage

```javascript
// Analyze method
analyze_complexity(
    scope: "method",
    target: "MyNamespace.MyClass.MyMethod"
)

// Analyze class
analyze_complexity(
    scope: "class",
    target: "MyNamespace.MyClass"
)

// Analyze project
analyze_complexity(
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

- ⬅️ [**ViewDefinition**](#view_definition) — see complex method code
- ⬅️ [**SearchDefinitions**](#search_definitions) — find all methods to analyze
- ➡️ [**OverwriteMember**](./ULTRA_SHARP_MODIFICATION.md#modify_code) — refactor complex method

---

## get_all_subtypes

**Explore nested types recursively** — lists all nested types, methods, properties, fields, and enums within a parent type. Ideal for gaining a complete mental model of a type hierarchy at a glance.

### Usage

```javascript
get_all_subtypes(
    fullyQualifiedParentTypeName: "MyNamespace.MyClass"
)
```

### Parameters

- **fullyQualifiedParentTypeName** (required): FQN of the parent type (class, interface, struct)

### What It Shows

For each member:
- 🏷️ **Kind** (Class, Interface, Struct, Enum, Method, Property, Field, Event)
- 📝 **Signature** with modifiers
- 🆔 **FQN** for use with other tools
- 📍 **Line number** in source file
- 🔄 **Nested members** (recursive for nested types)

### When to Use

✅ **For understanding complex types:**
- Explore large class with many nested types
- Understand enum values and structure
- See complete type contents at a glance

✅ **Before modifications:**
- Plan where to add new member
- Understand existing structure
- Find conflicts and duplicates

### Related Tools

- ➡️ [**GetMembers**](#get_members) — API surface only (simpler)
- ➡️ [**ViewInheritanceChain**](#view_inheritance_chain) — base/derived types
- ➡️ [**ViewDefinition**](#view_definition) — see specific member code

---

## view_inheritance_chain

**Analyze type relationships** — shows the complete inheritance hierarchy for a class or interface (base types and derived types). Essential for understanding type relationships and architecture.

### Usage

```javascript
view_inheritance_chain(
    fullyQualifiedTypeName: "MyNamespace.MyClass"
)
```

### Parameters

- **fullyQualifiedTypeName** (required): FQN of the type to analyze

### What It Shows

- 📤 **Base types** (parent classes, implemented interfaces)
- 📥 **Derived types** (classes that inherit from this type)
- 📍 **Location** of each type
- 🔗 **Complete hierarchy** visualization

### When to Use

✅ **For understanding architecture:**
- See inheritance hierarchy
- Find all types implementing interface
- Understand polymorphism relationships

✅ **For refactoring:**
- Check impact of base class changes
- Find candidates for extracting interface
- Analyze coupling between types

### Related Tools

- ➡️ [**ListImplementations**](#list_implementations) — find interface implementations
- ➡️ [**GetAllSubtypes**](#get_all_subtypes) — nested types (different from inheritance)
- ➡️ [**ViewDefinition**](#view_definition) — see type implementation

---

## view_call_graph

**Analyze method dependencies** — displays methods that call a specific method (incoming) and methods called by it (outgoing). Critical for understanding control flow and method relationships across the codebase.

### Usage

```javascript
view_call_graph(
    fullyQualifiedMethodName: "MyNamespace.MyClass.MyMethod"
)
```

### Parameters

- **fullyQualifiedMethodName** (required): FQN of the method to analyze

### What It Shows

- 📞 **Callers** (incoming) — methods that call this method
- 📤 **Callees** (outgoing) — methods called by this method
- 📍 **Location** of each call
- 📊 **Call count** and pagination for large graphs

### When to Use

✅ **For understanding control flow:**
- How does execution reach this method?
- What does this method trigger?
- Trace data flow through code

✅ **For impact analysis:**
- Before changing method signature
- Before deleting method
- Understanding dependencies

✅ **For debugging:**
- Find all paths to problematic code
- Understand call chains
- Identify side effects

### Related Tools

- ➡️ [**ViewDefinition**](#view_definition) — includes call graph preview
- ➡️ [**TraceExecution**](./ULTRA_SHARP_TRACING.md#trace_execution) — detailed execution trace
- ➡️ [**TraceBackwards**](./ULTRA_SHARP_TRACING.md#trace_backwards) — trace from error point

---

## pattern_search

**Multi-mode advanced search** — searches code using 4 modes: entity (by name/type), content (inside method bodies), semantic (ML-powered similarity), hybrid (combines all with intelligent ranking).

### Usage

```javascript
// Entity mode - search by name/type
pattern_search(
    pattern: ".*Service.*",
    mode: "entity",
    entityTypes: ["class", "interface"],
    limit: 20
)

// Content mode - search inside method bodies
pattern_search(
    pattern: "Console\\.WriteLine",
    mode: "content",
    limit: 50
)

// Semantic mode - natural language search (requires semantic service)
pattern_search(
    pattern: "validate email address format",
    mode: "semantic",
    minSimilarity: 0.7
)

// Hybrid mode - combines all modes with ranking
pattern_search(
    pattern: "user authentication",
    mode: "hybrid",
    limit: 30
)
```

### Parameters

- **pattern** (required): Search pattern (regex for entity/content, natural language for semantic/hybrid)
- **mode** (optional): `"entity"` | `"content"` | `"semantic"` | `"hybrid"` (default: `"hybrid"`)
- **entityTypes** (optional): Filter by type (`["class", "interface", "method", "property", "field", "enum"]`)
- **namespaceFilter** (optional): Filter by namespace (e.g., `"MyApp.Services"`)
- **limit** (optional): Maximum results (1-100, default: 20)
- **minSimilarity** (optional): Minimum similarity for semantic/hybrid (0.0-1.0, default: 0.7)

### Search Modes

| Mode | Pattern Type | Use Case |
|------|-------------|----------|
| **entity** | Regex | Find by name pattern, type filtering |
| **content** | Regex | Find patterns inside method bodies |
| **semantic** | Natural language | Find similar code by meaning |
| **hybrid** | Both | Combines all with intelligent ranking |

### When to Use

✅ **Entity mode:**
- Find all classes matching pattern
- Find naming convention violations
- Filter by type (class, method, etc.)

✅ **Content mode:**
- Find code patterns inside methods
- Find usage of specific APIs
- Search for debug/logging statements

✅ **Semantic mode:**
- Find similar functionality by description
- Discover related code by meaning
- Natural language queries

✅ **Hybrid mode:**
- Best overall results
- Combines multiple signals
- Ranked by relevance

### Fallback Behavior

If semantic service is unavailable:
- `"hybrid"` → falls back to `"entity"`
- `"semantic"` → falls back to `"entity"`

Check `get_capabilities()` to verify semantic mode availability.

### Related Tools

- ➡️ [**SearchDefinitions**](#search_definitions) — simpler regex search
- ➡️ [**SemanticSearch**](./ULTRA_SHARP_SEMANTIC.md#semantic_search) — pure semantic search
- ➡️ [**ViewDefinition**](#view_definition) — see found code

---

## find_duplicates

**Find semantically similar methods** — identifies groups of methods with similar functionality within the solution based on a similarity threshold. Requires semantic search service.

### Usage

```javascript
find_duplicates(
    similarityThreshold: 0.75
)
```

### Parameters

- **similarityThreshold** (required): Minimum similarity score (0.0 to 1.0)
  - `0.75` — recommended starting value
  - `0.85+` — nearly identical code
  - `0.6-0.75` — similar functionality

### What It Shows

Groups of similar methods:
- 📊 **Similarity score** between methods
- 🆔 **FQN** of each method in group
- 📍 **Location** (file:line)
- 📝 **Code preview** for comparison

### When to Use

✅ **For finding technical debt:**
- Discover copy-paste code
- Find consolidation opportunities
- Identify refactoring targets

✅ **For code review:**
- Check for duplicate implementations
- Ensure DRY principle
- Find inconsistent implementations

✅ **For refactoring:**
- Find methods to extract to shared utility
- Identify candidates for abstraction
- Reduce code duplication

### Prerequisites

⚠️ **Requires semantic search service:**
- Check with `get_capabilities()` first
- If unavailable, use `DetectCodeClones` instead

### Related Tools

- ➡️ [**DetectCodeClones**](./ULTRA_SHARP_SEMANTIC.md#detect_code_clones) — more options, different algorithm
- ➡️ [**SemanticSearch**](./ULTRA_SHARP_SEMANTIC.md#semantic_search) — find similar to specific code
- ➡️ [**AnalyzeComplexity**](#analyze_complexity) — complexity metrics

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
| **GetAllSubtypes** | Nested types recursively | 0.5-2 sec | Explore type structure |
| **ViewInheritanceChain** | Base/derived types | 0.5-1 sec | Inheritance hierarchy |
| **ViewCallGraph** | Callers and callees | 1-3 sec | Control flow analysis |
| **PatternSearch** | Multi-mode search | 1-10 sec | Advanced code search |
| **FindDuplicates** | Similar methods | 5-30 sec | Code deduplication |

---

## Common Usage Scenarios

### Exploring New Class

```javascript
// 1. Quick public API overview
get_members("MyNamespace.UserService", includePrivateMembers: false)

// 2. Details of interesting method
view_definition("MyNamespace.UserService.CreateUser")

// 3. Where this method is used
find_references("MyNamespace.UserService.CreateUser")

// 4. Check complexity
analyze_complexity(scope: "class", target: "MyNamespace.UserService")
```

### Preparing for Refactoring

```javascript
// 1. Find most complex methods in project
analyze_complexity(scope: "project", target: "MyProject.Core")

// 2. Details of most complex method
view_definition("MyProject.Services.ComplexMethod")

// 3. All places where it's used
find_references("MyProject.Services.ComplexMethod")

// 4. Refactoring plan...
```

### Analyzing Interface and Implementations

```javascript
// 1. View interface contract
get_members("IUserRepository", includePrivateMembers: false)

// 2. Find all implementations
list_implementations("IUserRepository")

// 3. Compare implementations by complexity
analyze_complexity(scope: "class", target: "SqlUserRepository")
analyze_complexity(scope: "class", target: "InMemoryUserRepository")

// 4. Specific implementation details
view_definition("SqlUserRepository.GetByIdAsync")
```

### Finding Duplicate Logic

```javascript
// 1. Find all methods containing "Validate"
search_definitions(".*Validate.*Email.*")

// 2. View each implementation
view_definition("UserService.ValidateEmail")
view_definition("EmailValidator.ValidateEmailFormat")

// 3. Find where they're used
find_references("UserService.ValidateEmail")
find_references("EmailValidator.ValidateEmailFormat")

// 4. Decide which to keep, consolidate duplicate logic
```

### Code Review of New Code

```javascript
// 1. Check public API
get_members("NewFeature.NewService", includePrivateMembers: false)

// 2. Check key method implementation
view_definition("NewFeature.NewService.ProcessData")

// 3. Check complexity
analyze_complexity(scope: "class", target: "NewFeature.NewService")

// 4. Check naming conventions
search_definitions("NewFeature.*(?!Async).*async Task")
```

---

## See Also

- 📚 [**ULTRA_SHARP_SOLUTION.md**](./ULTRA_SHARP_SOLUTION.md) — LoadSolution, LoadProject
- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — code modifications
- 📚 [**ULTRA_SHARP_TRACING.md**](./ULTRA_SHARP_TRACING.md) — execution tracing
- 📚 [**ULTRA_SHARP_QUALITY.md**](./ULTRA_SHARP_QUALITY.md) — formatting and linting
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

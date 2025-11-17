# Solution & Project Management

[← Back to Overview](./ULTRA_SHARP.md)

**Essential tools for loading and initializing C# solution workspaces.** These tools are the entry point for all SharpTools operations.

---

## 📋 Quick Reference

| Tool | Purpose | When to Use |
|------|---------|-------------|
| **LoadSolution** | Load .sln file and initialize workspace | **ALWAYS** at the start of any C# project work |
| **LoadProject** | Get structural map of a project | After LoadSolution to navigate types |

---

## load_solution

**Critical tool** — initializes MSBuildWorkspace and loads .sln file. Other tools won't work without it.

### Usage

```javascript
load_solution(
    solutionPath: "D:/MyProject/MyProject.sln"
)
```

### Parameters

- **solutionPath** (required): Full path to .sln file

### What It Does

1. 🔧 Initializes MSBuildWorkspace with correct configuration
2. 📂 Loads all projects from .sln file
3. 🔍 Creates **FastSymbolIndex** (355k+ symbols in ~21.7 sec)
4. 💾 Sets up caches for Compilation and SemanticModel
5. 📦 Resolves all NuGet dependencies (parallel)
6. 🔗 Loads reflection cache for external libraries (parallel)
7. 🌳 Creates or opens Git repository (unless `--disable-git`)

### Performance

**Typical loading times:**
- Small project (3-5 projects, ~50k LOC): 5-10 sec
- Medium project (10-20 projects, ~200k LOC): 15-30 sec
- Large project (50+ projects, ~1M LOC): 45-90 sec

**Main phases:**
```
Phase 1: MSBuild Solution Loading        [5-15 sec]
Phase 2: FastSymbolIndex Creation        [15-30 sec]
Phase 3: NuGet Resolution (parallel)     [0.3-0.8 sec]
Phase 4: Reflection Cache (parallel)     [0.5-2 sec]
Phase 5: Git Initialization              [0.1-0.5 sec]
```

**Optimizations:**
- ✅ Parallel assembly loading (4-5x speedup)
- ✅ Parallel NuGet resolution (3-4x speedup)
- ✅ Bloom filter for FastSymbolIndex (5-300x search speedup)
- ✅ FrozenDictionary for reflection cache (20-30% faster)

### Example Output

```
Solution loaded successfully: MyProject.sln

📊 Statistics:
   Projects: 12
   Documents: 847
   Symbols Indexed: 89,347
   Index Build Time: 18.3s
   NuGet Packages: 156 (resolved in 0.4s)
   External Assemblies: 42 (cached in 1.2s)

✅ Workspace ready
🌳 Git integration enabled (branch: sharptools/20251113-143022)

💡 Next: Use load_project to explore project structure
```

### When to Use

✅ **ALWAYS at the start:**
- Before any code analysis
- Before any modifications
- When switching to another solution
- After git pull with large changes

### When NOT to Use

❌ **No need to re-invoke:**
- Between operations (workspace is cached)
- When working with the same solution
- To refresh after your own changes (automatic)

⚠️ **Requires reload ONLY if:**
- External changes occurred (other developer/IDE)
- Projects added/removed from .sln
- NuGet dependencies changed

### Best Practices

1. **Always call first:**
   ```javascript
   // ✅ Correct
   load_solution("D:/MyProject/MyProject.sln")
   load_project("MyProject.Core")
   view_definition("MyNamespace.MyClass")

   // ❌ Wrong - LoadSolution skipped
   view_definition("MyNamespace.MyClass") // ERROR: Solution not loaded
   ```

2. **Use absolute paths:**
   ```javascript
   // ✅ Correct
   load_solution("D:/Projects/MyApp/MyApp.sln")

   // ❌ Bad - relative paths may not work
   load_solution("../MyApp.sln")
   ```

3. **Check build configuration:**
   ```bash
   # Specify configuration when starting server
   UltrasharpTools.Droid.exe --build-configuration Release
   ```

4. **Use logs for diagnostics:**
   ```bash
   # Use Debug logs when troubleshooting
   UltrasharpTools.Droid.exe --log-level Debug --log-directory ./Run.Logs
   ```

### Common Errors

#### ❌ Error: "Solution file not found"
```
ERROR: Solution file not found: D:/MyProject/MyProject.sln
```
**Solution:**
- Check path (absolute, not relative)
- Verify file exists
- Check access permissions

#### ❌ Error: "The .NET SDK for this solution is not installed"
```
ERROR: The current .NET SDK does not support targeting .NET 6.0
```
**Solution:**
- Install corresponding .NET SDK
- Check: `dotnet --list-sdks`
- For .NET 6: install .NET 6 SDK

#### ❌ Error: "MSBuild project load failed"
```
ERROR: Failed to load project MyProject.csproj: The imported project "..." was not found
```
**Solution:**
- Verify project builds: `dotnet build MyProject.sln`
- Restore NuGet: `dotnet restore MyProject.sln`
- Check paths in .csproj files

### FastSymbolIndex Performance

**What it is:**
- Bloom filter + hash table for instant symbol lookup
- Indexes ALL symbols from solution: types, methods, properties, fields, events

**Metrics:**
```
Small project (50k LOC):    15k symbols in 3-5 sec
Medium project (200k LOC):  60k symbols in 10-15 sec
Large project (1M LOC):     355k symbols in 21.7 sec
```

**Search speed:**
- Without index: O(N) through all projects — 5-15 sec
- With index: O(1) through Bloom filter — 0.05-0.3 sec
- **Speedup: 5-300x** (depends on project size)

**Memory:**
- Bloom filter: ~100 KB (fixed)
- Hash tables: ~2-10 MB (depends on symbol count)

### Related Tools

- ➡️ [**LoadProject**](#load_project) — next step after LoadSolution
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — view code after loading
- ➡️ [**SearchDefinitions**](./ULTRA_SHARP_ANALYSIS.md#search_definitions) — search through index
- ➡️ [**Undo**](./ULTRA_SHARP_MODIFICATION.md#undo) — works with Git created by LoadSolution

---

## load_project

**Structural project map** — returns hierarchy of namespaces → types for navigation and architecture understanding.

### Usage

```javascript
load_project(
    projectName: "MyProject.Core"
)
```

### Parameters

- **projectName** (required): Project name from solution (no path, no .csproj)

### What It Shows

Adaptive project structure with 3 detail levels:

1. **OverviewOnly** (< 50 types):
   ```
   Namespace.SubNamespace
   ├─ Class1
   ├─ Class2
   └─ Interface1
   ```

2. **TypesAndPublicMembers** (50-200 types):
   ```
   Namespace.SubNamespace
   ├─ Class1
   │  ├─ Method1(string param)
   │  └─ Property1 { get; set; }
   └─ Class2
   ```

3. **Full** (> 200 types):
   ```
   Namespace.SubNamespace
   ├─ Class1 (public class)
   │  ├─ Method1(string param) : void
   │  ├─ Method2(int x, int y) : int
   │  ├─ Property1 { get; set; } : string
   │  └─ _field1 : int (private)
   ```

**Automatic size-based adaptation:**
- Small projects → more details
- Large projects → brief overview (to avoid token overflow)

### Example Output

```
Project: MyProject.Core (847 documents, 12,453 types)
Detail Level: TypesAndPublicMembers (50-200 types)

═══════════════════════════════════════════════════════════
📦 MyProject.Core.Domain
═══════════════════════════════════════════════════════════

MyProject.Core.Domain.Entities
├─ User (public class)
│  ├─ Id : int
│  ├─ Name : string
│  ├─ Email : string
│  └─ Validate() : bool
│
├─ Order (public class)
│  ├─ OrderId : Guid
│  ├─ UserId : int
│  ├─ Items : List<OrderItem>
│  ├─ Total() : decimal
│  └─ Submit() : Task<bool>

MyProject.Core.Domain.Interfaces
├─ IUserRepository (public interface)
│  ├─ GetByIdAsync(int id) : Task<User>
│  ├─ CreateAsync(User user) : Task<int>
│  └─ UpdateAsync(User user) : Task<bool>

═══════════════════════════════════════════════════════════
📦 MyProject.Core.Services
═══════════════════════════════════════════════════════════

MyProject.Core.Services
├─ UserService (public class)
│  ├─ Constructor(IUserRepository repo)
│  ├─ GetUserAsync(int id) : Task<User>
│  └─ CreateUserAsync(string name, string email) : Task<int>

💡 Use view_definition with FQN to see full source code
💡 Example: view_definition("MyProject.Core.Domain.Entities.User")
```

### When to Use

✅ **Right after LoadSolution:**
- Understand structure of unfamiliar project
- Find types for further analysis
- Identify entry points (Controllers, Services)
- Study domain model

✅ **For navigation:**
- Find FQN for use with other tools
- See project's public API
- Understand namespace organization

### When NOT to Use

❌ **Not needed if:**
- You already know the FQN of needed type (use ViewDefinition directly)
- Searching for specific pattern in code (use SearchDefinitions)
- Need method implementation (use ViewDefinition)

### Best Practices

1. **Use for first-time exploration:**
   ```javascript
   // ✅ Correct workflow for new project
   load_solution("D:/MyProject/MyProject.sln")
   load_project("MyProject.Core")       // Get overview
   view_definition("MyProject.Core.Services.UserService") // Details
   ```

2. **Exact project name:**
   ```javascript
   // ✅ Correct - project name from .sln
   load_project("MyProject.Core")

   // ❌ Wrong
   load_project("MyProject.Core.csproj")  // No extension!
   load_project("src/MyProject.Core")     // No path!
   ```

3. **Use for documentation:**
   ```javascript
   // Create architectural documentation
   load_project("MyProject.API")      // Controllers
   load_project("MyProject.Core")     // Business Logic
   load_project("MyProject.Data")     // Data Access
   ```

### Common Errors

#### ❌ Error: "Project not found"
```
ERROR: Project 'MyProject.Core' not found in solution
```
**Solution:**
- Check exact project name in .sln file
- Use name without .csproj extension
- Verify project is loaded in solution

#### ❌ Error: "LoadSolution must be called first"
```
ERROR: Solution not loaded. Call load_solution first.
```
**Solution:**
- Call load_solution first
- Verify LoadSolution returned success

### Adaptive Detail Logic

**Level selection logic:**
```csharp
if (typeCount < 50)
    detailLevel = DetailLevel.OverviewOnly;
else if (typeCount < 200)
    detailLevel = DetailLevel.TypesAndPublicMembers;
else
    detailLevel = DetailLevel.Full;
```

**Why:**
- Token savings for large projects
- Detailed information for small projects
- Prevents context overflow

### Related Tools

- ⬅️ [**LoadSolution**](#load_solution) — must call before load_project
- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — detailed type view
- ➡️ [**GetMembers**](./ULTRA_SHARP_ANALYSIS.md#get_members) — get all type members
- ➡️ [**SearchDefinitions**](./ULTRA_SHARP_ANALYSIS.md#search_definitions) — regex search

---

## Common Usage Scenarios

### First Time Exploring a Project

```javascript
// 1. Load solution
load_solution("D:/MyProject/MyProject.sln")

// 2. View structure of main projects
load_project("MyProject.API")      // Entry point
load_project("MyProject.Core")     // Business logic
load_project("MyProject.Data")     // Data access

// 3. Detailed analysis of interesting types
view_definition("MyProject.API.Controllers.UserController")
get_members("MyProject.Core.Services.UserService", includePrivateMembers: false)
```

### Analyzing Unknown Feature

```javascript
// 1. Load solution
load_solution("D:/LegacyApp/LegacyApp.sln")

// 2. Search for entry point by name
search_definitions("OrderProcessing")

// 3. View structure of found project
load_project("LegacyApp.Orders")

// 4. Analyze found types
view_definition("LegacyApp.Orders.OrderProcessor")
find_references("LegacyApp.Orders.OrderProcessor.ProcessOrder")
```

### Preparing for Refactoring

```javascript
// 1. Load solution
load_solution("D:/Refactoring/MyApp.sln")

// 2. Get overview of all projects
load_project("MyApp.Core")
load_project("MyApp.Services")
load_project("MyApp.Data")

// 3. Search for duplicated logic
search_definitions("ValidateUser")

// 4. Analyze complexity
analyze_complexity(scope: "project", target: "MyApp.Core")
```

---

## Performance & Optimization

### Caching

**What's cached afterload_solutionn:**
- ✅ MSBuildWorkspace (singleton)
- ✅ Solution (until nexload_solutionon)
- ✅ Compilation per project (LRU cache, 10 items)
- ✅ SemanticModel per document (LRU cache, 50 items)
- ✅ FastSymbolIndex (entire index in memory)
- ✅ Reflection cache for external assemblies (FrozenDictionary)

**Invalidation:**
- ✅ Automatic on modifications through SharpTools
- ✅ Manual on external changes (requires ReloadSolution)

### Memory Usage

**Typical memory consumption:**
```
Small project (3-5 projects):     200-400 MB
Medium project (10-20 projects):  500-1000 MB
Large project (50+ projects):     1.5-3 GB
```

**Breakdown:**
- MSBuildWorkspace: 30-40%
- Compilations cache: 20-30%
- FastSymbolIndex: 5-10%
- Reflection cache: 10-15%
- Other: 15-25%

### Optimization Tips

1. **Don't reload solution unnecessarily:**
   ```javascript
   // ❌ Bad - unnecessary reload
   load_solution(...)
   add_member(...)
   load_solution(...)  // NOT NEEDED!

   // ✅ Good - solution updates automatically
   load_solution(...)
   add_member(...)
   view_definition(...) // Sees changes
   ```

2. **Use appropriate log level:**
   ```bash
   # Production
   --log-level Information  # Only important events

   # Development
   --log-level Debug        # Detailed diagnostics

   # Performance testing
   --log-level Warning      # Minimal logging
   ```

---

## Command-Line Options

Available when starting servers (Droid or Overlord):

```bash
UltrasharpTools.Droid.exe \
  --load-solution "D:/MyProject/MyProject.sln" \
  --build-configuration "Release" \
  --disable-git \
  --log-level Information \
  --log-directory "./Run.Logs"
```

**Options:**
- `--load-solution <path>` — auto-load .sln on startup (optional, better use load_solution)
- `--build-configuration <config>` — Debug or Release (default: Debug)
- `--disable-git` — disable Git integration
- `--log-level <level>` — Verbose, Debug, Information, Warning, Error, Fatal
- `--log-directory <path>` — directory for logs (Droid only)
- `--log-file <path>` — log file path (Overlord only)
- `--port <number>` — HTTP port (Overlord only, default: 3001)

---

## See Also

- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — code analysis after loading
- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — code modifications
- 📚 [**ULTRA_SHARP_QUALITY.md**](./ULTRA_SHARP_QUALITY.md) — formatting and linting
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation

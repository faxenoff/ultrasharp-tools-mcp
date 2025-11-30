# Solution & Project Management

**C# solution and project management** — entry point for all UltrasharpTools operations.

---

## Tools

| Tool | Purpose | When to Use |
|------|---------|-------------|
| **load_solution** | Load .sln file | **ALWAYS** at start |
| **load_project** | Project structure map | After load_solution for navigation |

---

## load_solution

**Critical tool** — initializes MSBuildWorkspace and loads solution.

### Usage

```javascript
load_solution(
    solutionPath: "D:/MyProject/MyProject.sln"
)
```

### Parameters

- **solutionPath** (required): Full path to .sln file

### What It Does

1. Initializes MSBuildWorkspace
2. Loads all projects from .sln
3. Creates **FastSymbolIndex** (355k+ symbols in ~21.7 sec)
4. Sets up Compilation and SemanticModel caches
5. Resolves NuGet dependencies (parallel)
6. Creates Git repository (unless `--disable-git`)

### Performance

```
Small project (3-5 projects, ~50k LOC): 5-10 sec
Medium project (10-20 projects, ~200k LOC): 15-30 sec
Large project (50+ projects, ~1M LOC): 45-90 sec
```

### Common Errors

❌ **"Solution file not found"**
- Check path (absolute, not relative)
- Verify file exists

❌ **".NET SDK not installed"**
- Install appropriate .NET SDK
- Check: `dotnet --list-sdks`

---

## load_project

**Project structure map** — returns namespace → types hierarchy.

### Usage

```javascript
load_project(
    projectName: "MyProject.Core"
)
```

### Parameters

- **projectName** (required): Project name from solution (no path, no .csproj)

### Output

Adaptive structure with 3 detail levels:

```
Namespace.SubNamespace
├─ Class1 (public class)
│  ├─ Method1(string param) : void
│  ├─ Property1 { get; set; } : string
└─ Class2
```

### When to Use

✅ **After load_solution:**
- Understand unfamiliar project structure
- Find types for further analysis
- Find entry points (Controllers, Services)

---

## Typical Workflow

```javascript
// 1. Load solution
load_solution("D:/MyProject/MyProject.sln")

// 2. View main project structures
load_project("MyProject.API")      // Entry point
load_project("MyProject.Core")     // Business logic
load_project("MyProject.Data")     // Data access

// 3. Detailed analysis of interesting types
view_definition("MyProject.API.Controllers.UserController")
get_members("MyProject.Core.Services.UserService", includePrivateMembers: false)
```

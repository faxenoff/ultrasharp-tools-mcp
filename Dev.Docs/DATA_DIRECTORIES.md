# UltraSharpTools Data Directories

## Overview

Starting from version 3.0.7, UltraSharpTools uses a **central data directory** for all persistent data instead of storing it in project-specific `.ultrasharp/` folders.

This simplifies multi-project scenarios and ensures consistent data location across different operating systems.

## Data Directory Location

### Windows
```
%LOCALAPPDATA%\UltraSharpTools\
```
Typical path: `C:\Users\{Username}\AppData\Local\UltraSharpTools\`

### Linux / macOS
```
~/.ultrasharp/
```
Typical path: `/home/{username}/.ultrasharp/`

## Directory Structure

```
UltraSharpTools/  (or .ultrasharp/)
├── logs/              # Droid and Indexer logs
├── db/                # Main database files
├── vector-db/         # Semantic embedding vector database
├── cache/
│   ├── analysis/      # Analysis cache (incremental compilation)
│   ├── callgraph/     # Call graph cache
│   └── symbols/       # Symbol cache (10x faster solution load)
├── config/
│   └── semantic-config.json  # Semantic embedding configuration
└── Scripts/
    ├── setup-semantic-embedding.cmd  # Windows setup script
    ├── setup-semantic-embedding.sh   # Linux/macOS setup script
    └── setup-semantic-embedding.ps1  # PowerShell setup script
```

## What's Stored Where

### Logs (`logs/`)
- Droid process logs
- Indexer process logs
- MCP communication logs (if enabled)

**Auto-rotation**: Old logs are automatically cleaned up after 7 days.

### Database (`db/`)
- Symbol metadata
- Project references
- Analysis results

**Size**: Typically 10-50 MB per analyzed solution.

### Vector Database (`vector-db/`)
- Semantic embeddings for code search
- Only created if semantic mode is enabled

**Size**: Varies by codebase size (50-500 MB typical).

### Cache (`cache/`)
- **analysis/**: Incremental compilation cache
- **callgraph/**: Call graph analysis cache
- **symbols/**: Roslyn symbol cache (dramatically speeds up solution loading)

**Benefit**: Symbol cache reduces solution initialization from 33s → 3-5s.

### Config (`config/`)
- `semantic-config.json`: Embedding provider settings (TEI, Ollama, Memory)

**Auto-created**: Missing config is auto-generated on first run with detected settings.

### Scripts (`Scripts/`)
- Setup scripts for semantic embedding providers
- Located in distribution: `Run.Build/Droid/Config/Scripts/`
- Copied to central location on first use

## Migration from Project-Local Structure

**Old structure** (before 3.0.7):
```
your-project/
└── .ultrasharp/
    ├── logs/
    ├── cache/
    └── ...
```

**New structure** (3.0.7+):
```
%LOCALAPPDATA%\UltraSharpTools\  (Windows)
~/.ultrasharp/                    (Linux/macOS)
```

**Migration**: Automatic on first run. Old `.ultrasharp/` folders can be safely deleted.

## Configuration

All paths are determined automatically by `ProjectPathHelper` class:

```csharp
// Get paths programmatically:
var logsPath = ProjectPathHelper.GetLogsPath();
var dbPath = ProjectPathHelper.GetDatabasePath();
var vectorDbPath = ProjectPathHelper.GetVectorDatabasePath();
var configPath = ProjectPathHelper.GetConfigPath();
var scriptsPath = ProjectPathHelper.GetScriptsPath();
```

## FAQ

### Q: Can I change the data directory location?
**A**: Not currently. The location is determined by OS conventions for application data.

### Q: What happens if I delete the data directory?
**A**: UltraSharpTools will recreate it on next run. You'll lose:
- Cached symbols (slower solution load on first run)
- Logs (no impact on functionality)
- Semantic embeddings (will be regenerated if semantic mode is enabled)

### Q: How much disk space does it use?
**A**: Typical usage:
- Logs: 10-50 MB (auto-cleaned)
- Database: 10-50 MB per solution
- Vector DB: 50-500 MB (if semantic mode enabled)
- Cache: 50-200 MB (symbol cache)
- **Total**: ~100-800 MB depending on codebase size

### Q: Can I use different data directories for different projects?
**A**: No. The central directory is shared across all projects. This is intentional to:
- Enable Droid process reuse across projects
- Share symbol cache between projects
- Simplify maintenance (one location to backup/clean)

### Q: Where are build artifacts stored?
**A**: Build artifacts (compiled binaries) remain in project-specific `bin/` and `obj/` folders. Only analysis data and logs are centralized.

## See Also

- [COMM_GRACEFUL_SHUTDOWN_FIX.md](COMM_GRACEFUL_SHUTDOWN_FIX.md) - IPC architecture details
- [Dev.Docs/Architecture/PHASE_3_3_SEMANTIC_IPC.md](Dev.Docs/Architecture/PHASE_3_3_SEMANTIC_IPC.md) - Hybrid architecture

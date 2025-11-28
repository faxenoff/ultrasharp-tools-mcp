# UltraSharpTools.Comm (Portable C)

Lightweight proxy for UltraSharpTools Droid MCP server.
Single portable binary for Windows, Linux, and macOS using [Cosmopolitan Libc](https://github.com/jart/cosmopolitan).

## Features

- **~700KB binary** (vs ~15MB .NET AOT)
- **Single file** runs on Win/Linux/macOS/BSD - no separate builds needed!
- Zero dependencies
- Minimal memory footprint (~1MB)

## How it works

```
Claude Desktop <--stdin/stdout--> Comm <--Named Pipe/Unix Socket--> Droid
```

1. Connects to Droid via Named Pipe (Windows) or Unix Socket (Linux/macOS)
2. If Droid not running - starts it with `--pipe-server`
3. Proxies stdin/stdout ↔ pipe (pure byte-level, no parsing)

## Quick Start (Windows)

```powershell
# 1. Install cosmocc (one-time)
.\setup.ps1

# 2. Build (creates comm.com in ..\Run.Release\Droid)
.\build.ps1
```

Or using CMD:
```cmd
setup.cmd
build.cmd
```

## Building

### Automatic Setup (Recommended)

**PowerShell:**
```powershell
.\setup.ps1   # Downloads and installs cosmocc
.\build.ps1   # Builds and copies to Run.Release\Droid
```

**CMD:**
```cmd
setup.cmd
build.cmd
```

### Manual Build

```bash
# Using Makefile (Linux/macOS/WSL)
make

# Using cosmocc directly
cosmocc -Os -DNDEBUG -o UltraSharp-tools.com comm.c

# Using gcc directly (single platform only)
gcc -Os -o comm comm.c
```

## Usage

```bash
# Show help
./comm.com --help

# Show version
./comm.com --version

# Normal usage (called by Claude Desktop)
./comm.com [args forwarded to Droid]
```

## Claude Desktop Configuration

```json
{
  "mcpServers": {
    "ultrasharp": {
      "command": "path/to/comm.com",
      "args": []
    }
  }
}
```

## File locations

| OS | Pipe/Socket Path |
|----|------------------|
| Windows | `\\.\pipe\UltraSharpTools_Droid` |
| Linux/macOS | `/tmp/UltraSharpTools_Droid.sock` |

## Comparison with .NET version

| Metric | .NET AOT | Cosmopolitan C |
|--------|----------|----------------|
| Binary size | ~15 MB | ~700 KB |
| Memory usage | ~30 MB | ~1 MB |
| Startup time | ~100ms | ~1ms |
| Platforms | Separate binaries | Single file |
| Dependencies | .NET Runtime | None |

## License

Same as UltraSharpTools project.

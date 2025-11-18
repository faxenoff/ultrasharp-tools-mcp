# UltrasharpTools v3.0.0 - Release Notes

**Comprehensive modernization of SharpTools MCP Server with focus on performance and quality.**

---

## 🚀 Performance Improvements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Cold start** | 3-5 sec | 1.5-2.5 sec | **2x faster** |
| **Warm start** | 48.3 sec | 8.9 sec | **5.4x faster** |
| **Symbol search** | 5-15 sec | < 100 ms | **50-150x faster** |
| **Branch switch** | 48+ sec | 16.6 ms | **2900x faster** |
| **FindReferences** | 5-10 sec | Instant | **Instant** |
| **Assembly loading** | Baseline | Parallel | **4-5x faster** |
| **NuGet resolution** | Baseline | Concurrent | **3-4x faster** |
| **Hash computations** | Baseline | SIMD AVX2 | **2.2-5.7x faster** |
| **Hot paths** | Baseline | Span<T> + ArrayPool | **4-5x faster** |
| **Startup** | Baseline | ReadyToRun (R2R) | **50% faster** |

**Key Optimizations:**
- ✅ N+1 Problem Fix — 99.8% reduction in redundant compilation calls
- ✅ Fast Symbol Index — Bloom filters + bitwise flags
- ✅ Layered Indexing — Git-aware three-layer architecture
- ✅ SQLite Persistence — Cache for warm starts
- ✅ SIMD Vectorization — AVX2 for computations
- ✅ Zero-allocation — Span<T> + ArrayPool in hot paths
- ✅ Dynamic PGO — Adaptive runtime optimization (+30-50%)

---

## ✨ New Tools (37 total, was 36)

### Quality Tools (3 new)
- **`format_code`** — Automatic C# formatting via CSharpier
- **`analyze_code_style`** — Code quality analysis via Roslyn Analyzers
- **`apply_code_fixes`** — Automatic issue fixing (unnecessary usings, etc.)

### Tracing Tools (3 new)
- **`trace_execution`** — Static execution tracing with data flow
- **`trace_backwards`** — Reverse tracing from crash point
- **`analyze_path_feasibility`** — Symbolic execution via Z3

### System Tools (1 new)
- **`get_capabilities`** — Server capabilities and semantic mode status

### New Features
- **Semantic Merge** — 3-way merge with semantic understanding (80-90% auto-resolution)
- **Semantic Search** — Local (Ollama/TEI/Memory) or Centralized (Overlord) modes
- **Auto-linting** — All modification tools now include linting results

---

## 🔧 Breaking Changes

- Minimum .NET version: **10.0**
- Updated MCP protocol to **0.4.0-preview.3**

### Migration
1. Install .NET 10 SDK
2. Rebuild: `Dev.Scripts\build-releases.cmd`
3. Update `claude.json` with new paths

---

**Full Changelog:** https://github.com/faxenoff/ultrasharp-tools-mcp/compare/v2.0.0...v3.0.0

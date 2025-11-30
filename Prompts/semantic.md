# Semantic Search & AI Features

**AI-powered code search and analysis** — semantic search, duplicate detection, semantic merge.

⚠️ **Requires setup:** Run `setup-semantic-embedding.cmd` to enable.

---

## Tools

| Tool | Purpose | Semantic Mode |
|------|---------|---------------|
| **semantic_search** | Search by meaning | Required |
| **pattern_search** | Multi-mode search | Optional |
| **detect_code_clones** | Find duplicates | Required |
| **find_duplicates** | Groups of similar methods | Required |
| **semantic_replace** | Batch replace with context | Optional |
| **semantic_merge** | AI-powered branch merge | Optional |

---

## Check Availability

```javascript
// Check semantic mode status
get_capabilities()
// Output: SemanticMode: Local | Overlord | Both | None
```

---

## semantic_search

**Search by meaning** — finds semantically similar code.

```javascript
semantic_search(
    query: "validate email address format",
    scope: "solution",  // or "project:MyProject", "methods", "classes"
    topK: 10,
    minSimilarity: 0.7
)
```

---

## pattern_search

**Multi-mode search** — 4 modes with intelligent ranking.

```javascript
// Entity mode — by name/type
pattern_search(
    pattern: ".*Service.*",
    mode: "entity",
    entityTypes: ["class", "interface"],
    limit: 20
)

// Content mode — inside method bodies
pattern_search(
    pattern: "Console\\.WriteLine",
    mode: "content",
    limit: 50
)

// Semantic mode — natural language
pattern_search(
    pattern: "validate email address format",
    mode: "semantic",
    minSimilarity: 0.7
)

// Hybrid mode — combines all
pattern_search(
    pattern: "user authentication",
    mode: "hybrid",
    limit: 30
)
```

**Fallback:** If semantic unavailable → entity mode.

---

## detect_code_clones

**Find code duplicates** — semantic similarity analysis.

```javascript
detect_code_clones(
    mode: "semantic",      // semantic, exact, similar
    minSimilarity: 0.85,   // 0.85 for duplicates, 0.7 for patterns
    membersOnly: true,     // methods/properties only
    maxGroups: 20
)
```

---

## find_duplicates

**Groups of similar methods** — identify refactoring opportunities.

```javascript
find_duplicates(
    similarityThreshold: 0.75  // 0.75 recommended, 0.85+ nearly identical
)
```

---

## semantic_replace

**Batch replace with context** — preview and apply modes.

```javascript
// Preview mode
semantic_replace(
    pattern: "Console\\.WriteLine",
    scope: "member",  // statement, block, member, type, file
    searchMode: "regex"  // regex, roslyn, semantic
)

// Apply mode
semantic_replace(
    apply: true,
    replacements: '[{"matchId":"sr-xxx","newCode":"..."}]',
    applyMode: "AllOrNothing"  // or "BestEffort"
)
```

---

## semantic_merge

**AI-powered branch merge** — 3-way merge with natural language instructions.

```javascript
semantic_merge(
    sourceBranch: "feature/caching",
    targetBranch: "main",  // default: current branch
    filePatterns: "*.cs,*.json",  // default: *
    instructions: "ignore swagger files; prefer source for caching",
    apply: true  // false = preview
)
```

**Capabilities:**
- Semantic matching of structural changes
- Fast Path (90%) + Slow Path (10%) for performance
- Handle moves and renames
- Natural language conflict resolution

---

## Semantic Mode Setup

```bash
# Windows
setup-semantic-embedding.cmd

# PowerShell
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

**Providers:**
- **TEI** (recommended): HuggingFace Text Embeddings Inference
- **Ollama**: Local inference
- **Memory**: In-process (experimental)

**Validation:**
```bash
pwsh Dev.Scripts/validate-semantic-config.ps1
```

---

## Workflow: Find Duplicates

```javascript
// 1. Find similar code
detect_code_clones(
    mode: "semantic",
    minSimilarity: 0.8,
    maxGroups: 20
)

// 2. Examine found groups
view_definition("MyService.ValidateEmail")
view_definition("OtherService.CheckEmail")

// 3. Decide which to keep, consolidate
```

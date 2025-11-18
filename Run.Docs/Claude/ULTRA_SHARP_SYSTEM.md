# UltrasharpTools - System Tools

**Server capabilities and runtime information.**

---

## 📋 Available Tools

### get_capabilities

**Get server capabilities and semantic mode status**

Returns comprehensive information about server features and semantic mode availability.

```typescript
get_capabilities() → ServerCapabilities
```

#### Response Structure

```json
{
  "serverInfo": {
    "name": "UltrasharpTools MCP Droid",
    "version": "3.0.0",
    "protocol": "MCP 1.0"
  },
  "capabilities": {
    "semanticMode": {
      "enabled": true,
      "source": "Both",           // None | Local | Overlord | Both
      "modelName": "nomic-embed-text",
      "vectorDimension": 768,
      "localEmbeddingUrl": "http://localhost:11434",
      "overlordUrl": "http://localhost:8080",
      "dynamic": true,
      "cacheValiditySeconds": 60,
      "description": "Semantic code search and similarity analysis using vector embeddings"
    },
    "features": {
      "gitIntegration": true,
      "editorConfigSupport": true,
      "universalSemanticMode": true,
      "hybridMode": true,
      "tracing": true,
      "codeModification": true,
      "projectAnalysis": true,
      "symbolCaching": true
    }
  },
  "timestamp": "2025-11-18T22:30:00Z"
}
```

#### Semantic Mode Sources

- **None**: Semantic mode disabled or unavailable
- **Local**: Local embedding service (Ollama/TEI) available
- **Overlord**: Remote Overlord server available
- **Both**: Both local and remote sources available (hybrid mode)

#### Use Cases

**1. Detect semantic capabilities at startup**
```
get_capabilities()
→ Check if semanticMode.enabled = true
→ Optimize query strategy based on availability
```

**2. Choose semantic search strategy**
```
get_capabilities()
→ If source = "Both": prefer local for speed
→ If source = "Overlord": use remote search
→ If source = "None": fallback to non-semantic tools
```

**3. Display server status to user**
```
get_capabilities()
→ Show: "Semantic search: ✓ Available (Local + Overlord)"
→ Show: "Model: nomic-embed-text (768 dimensions)"
→ Show: "Git integration: ✓ Enabled"
```

**4. Runtime health check**
```
// Check if semantic mode recovered after failure
get_capabilities()
→ semanticMode.enabled changed from false → true
→ Resume using semantic-enhanced tools
```

#### Features Explained

**gitIntegration** - Automatic git branching and commits for all modifications
**editorConfigSupport** - Uses .editorconfig for code formatting
**universalSemanticMode** - All analysis tools can be enriched with semantic data
**hybridMode** - Can route tools between local and remote execution
**tracing** - Advanced execution tracing capabilities available
**codeModification** - Code modification and refactoring tools enabled
**projectAnalysis** - Full project analysis and navigation capabilities
**symbolCaching** - Symbol cache enabled for faster initialization

#### When to Call

**Recommended:**
- ✅ At startup - check available capabilities
- ✅ Before using semantic search - verify availability
- ✅ After network issues - check if Overlord recovered
- ✅ When showing status to user

**Not needed:**
- ❌ Before every tool call (capabilities cached for 60s)
- ❌ For non-semantic operations (git, analysis, modification)

#### Performance

- **Cache**: 60 seconds validity (configurable)
- **Response time**: < 100ms (cached), < 3s (fresh check)
- **Network**: Checks local embedding + Overlord in parallel

#### Example Workflow

```typescript
// 1. Check capabilities at startup
const caps = await get_capabilities();

// 2. Decide strategy based on semantic mode
if (caps.capabilities.semanticMode.enabled) {
  console.log(`Semantic search available via ${caps.capabilities.semanticMode.source}`);

  // Use semantic-enhanced tools
  const result = await view_definition("MyClass");
  // result.semantic contains similar symbols, related implementations

} else {
  console.log("Semantic mode unavailable - using standard analysis");

  // Use standard tools without semantic enrichment
  const result = await view_definition("MyClass");
  // result.semantic will be empty or null
}

// 3. Show features to user
console.log("Available features:");
for (const [feature, enabled] of Object.entries(caps.capabilities.features)) {
  console.log(`  ${feature}: ${enabled ? '✓' : '✗'}`);
}
```

#### Notes

- **Dynamic capability**: Semantic mode can change at runtime (service restart, network issues)
- **Cache invalidation**: Cached for 60s to avoid overhead on every request
- **Graceful degradation**: Tools work without semantic mode, just without enrichment
- **MCP Initialize**: Same info available via MCP Initialize Response (static snapshot at startup)

#### Related

- **MCP Initialize Response**: Static capabilities at connection time
- **Semantic tools**: [ULTRA_SHARP_SEMANTIC.md](./ULTRA_SHARP_SEMANTIC.md)
- **Hybrid mode**: [../../Dev.Docs/Architecture/HYBRID_MODE.md](../../Dev.Docs/Architecture/HYBRID_MODE.md)

---

## 🎯 Quick Reference

| Action | Tool | Response Time |
|--------|------|---------------|
| Check semantic availability | `get_capabilities()` | < 100ms (cached) |
| Full capability report | `get_capabilities()` | < 3s (fresh) |
| Startup detection | MCP Initialize | 3s timeout |

---

## 📚 See Also

- [Semantic Mode Discovery](../../Dev.Docs/Features/Semantic/PHASE_1_SEMANTIC_DISCOVERY.md) - Architecture
- [Semantic Setup Guide](../Deployment/SEMANTIC_SETUP_GUIDE.md) - How to enable semantic mode
- [ROADMAP.md](../../ROADMAP.md) - Phase 13.5 (Semantic Discovery)

# Semantic Mode Documentation

Документация по Universal Semantic Mode и AI capability discovery.

## Файлы

### Анализ и Архитектура
- **[SEMANTIC_MODE_DISCOVERY_ANALYSIS.md](SEMANTIC_MODE_DISCOVERY_ANALYSIS.md)** - Анализ проблемы discovery semantic mode capabilities и предложенные решения (get_capabilities tool, MCP Initialize, enhanced tool descriptions)

- **[SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md](SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md)** - Lifecycle management: failure detection, recovery notification, circuit breaker, graceful degradation

### Реализация
- **[PHASE_1_SEMANTIC_DISCOVERY.md](PHASE_1_SEMANTIC_DISCOVERY.md)** - Phase 1 implementation summary: get_capabilities tool + MCP Initialize capabilities

## Связанные документы

### Deployment
- **[Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md](../../../Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md)** - Setup guide для semantic embedding services

### Scripts
- **[Dev.Scripts/setup-semantic-embedding.cmd](../../../Dev.Scripts/setup-semantic-embedding.cmd)** - Automated setup script для embedding services

### Configuration
- **[Run.Config/semantic-config.json](../../../Run.Config/semantic-config.json)** - Semantic mode configuration
- **[Run.Config/semantic-config.schema.json](../../../Run.Config/semantic-config.schema.json)** - JSON schema для validation

### Architecture
- **[Dev.Docs/Architecture/UNIVERSAL_SEMANTIC_MODE_SUMMARY.md](../../Architecture/UNIVERSAL_SEMANTIC_MODE_SUMMARY.md)** - Universal Semantic Mode architecture summary

## Quick Links

### Phase 1: Semantic Discovery ✅
**Status**: COMPLETE
**Date**: 2025-11-18

Реализованы:
1. `get_capabilities` MCP tool - dynamic runtime check
2. MCP Initialize capabilities - static bootstrap check
3. SemanticModeBootstrapCheck - lightweight check без DI
4. ISemanticModeProvider moved to Tools.Interfaces (breaking circular dependency)

### Future Phases

**Phase 2**: Lifecycle Management (planned)
- SemanticModeMonitor background service
- MCP Notifications для state changes
- Circuit Breaker pattern
- Graceful Degradation levels

## Navigation

```
ultrasharp-tools-mcp/
├── Dev.Docs/
│   └── Features/
│       └── Semantic/           ← YOU ARE HERE
│           ├── README.md
│           ├── SEMANTIC_MODE_DISCOVERY_ANALYSIS.md
│           ├── SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md
│           └── PHASE_1_SEMANTIC_DISCOVERY.md
├── Run.Docs/
│   └── Deployment/
│       └── SEMANTIC_SETUP_GUIDE.md
├── Dev.Scripts/
│   └── setup-semantic-embedding.cmd
└── Run.Config/
    ├── semantic-config.json
    └── semantic-config.schema.json
```

## См. также

- [ROADMAP.md](../../../ROADMAP.md) - Project roadmap
- [CHANGELOG.md](../../../CHANGELOG.md) - Version history
- [ARCHITECTURE.md](../../../ARCHITECTURE.md) - System architecture

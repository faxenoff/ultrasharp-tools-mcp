# Semantic Replace - Summary

## Статус: ✅ Phase 1-5 завершены (с интеграцией семантики)

### Обзор

**semantic_replace** — инструмент для массовых изменений кода с полным извлечением контекста. Позволяет:

1. **Preview**: Найти все вхождения паттерна и получить полный код контейнера (метод/класс)
2. **Manual Apply**: Применить batch изменений с индивидуальным кодом для каждого
3. **Semantic Apply**: LLM автоматически трансформирует код по описанию

### Ключевые возможности

| Возможность | Описание |
|-------------|----------|
| **3 режима поиска** | Regex, Roslyn (FQN), Semantic (embeddings) |
| **5 уровней scope** | Statement, Block, Member, Type, File |
| **Batch операции** | Одна команда для множества изменений |
| **Атомарность** | Rollback при ошибках (AllOrNothing mode) |
| **Валидация** | Проверка компиляции перед commit |
| **Git интеграция** | Автоматический commit с описанием |

### Архитектура

```
┌─────────────────────────────────────────────────────────┐
│                 semantic_replace (MCP)                   │
├─────────────────────────────────────────────────────────┤
│                 SemanticReplaceService                   │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐   │
│  │PatternMatcher│  │ContextExtract│  │BatchReplacer  │   │
│  │ (3 modes)   │  │ (5 scopes)   │  │ (atomic tx)   │   │
│  └─────────────┘  └──────────────┘  └───────────────┘   │
├─────────────────────────────────────────────────────────┤
│  Optional: SemanticTransformer (LLM-based)              │
└─────────────────────────────────────────────────────────┘
```

### Примеры использования

#### 1. Миграция логирования

```
Preview:  semantic_replace(pattern: "Console.WriteLine", scope: "member")
Apply:    semantic_replace(apply: true, replacements: [...])

Или автоматически:
          semantic_replace(pattern: "Console.WriteLine",
                          transformation: "Replace with ILogger",
                          useSemanticModel: true, apply: true)
```

#### 2. API модернизация

```
semantic_replace(
  pattern: "if.*==.*null.*throw.*ArgumentNullException",
  scope: "statement",
  transformation: "Replace with ArgumentNullException.ThrowIfNull()",
  useSemanticModel: true,
  apply: true
)
```

#### 3. Добавление CancellationToken

```
semantic_replace(
  pattern: "async Task",
  searchMode: "roslyn",
  scope: "member",
  transformation: "Add CancellationToken parameter with default value",
  useSemanticModel: true
)
```

### Отличия от существующих инструментов

| Инструмент | semantic_replace | Преимущества |
|------------|------------------|--------------|
| `find_and_replace` | ✅ Полный контекст | Видишь весь метод/класс, не только строку |
| `replace_all_references` | ✅ Индивидуальные изменения | Каждое вхождение можно изменить по-своему |
| `replace_references_by_pattern` | ✅ Семантическое понимание | LLM понимает контекст и делает умные изменения |

### План реализации

| Phase | Статус | Описание |
|-------|--------|----------|
| 1. Models | ✅ | CodeMatch, CodeReplacement, ReplaceResult, PatternMatch |
| 2. Pattern Matching | ✅ | Regex, Roslyn, **Semantic search (ISemanticSearchService)** |
| 3. Context Extraction | ✅ | Scope expansion, metadata, SemanticFqn integration |
| 4. Batch Replacer | ✅ | Atomic apply, validation, rollback via VersionManager |
| 5. MCP Tools | ✅ | semantic_replace endpoint with preview/apply modes |
| 6. Semantic Transformer | 🔲 | LLM-based transformation (optional, not implemented) |
| 7. Testing | 🔲 | Unit + Integration tests |

**Завершено**: Phase 1-5 полностью реализованы

### Метрики успеха

| Метрика | Target |
|---------|--------|
| Preview latency | < 2 sec / 100 matches |
| Apply latency | < 5 sec / 50 replacements |
| Semantic accuracy | > 90% корректных трансформаций |
| Rollback reliability | 100% |

### Зависимости

**Используемые сервисы**:
- `ISolutionManager` — доступ к Roslyn workspace
- `IFastSymbolIndex` — O(1) поиск символов
- `ICodeModificationService` — применение изменений
- `IFormattingService` — форматирование кода
- `IDiagnosticService` — проверка компиляции
- `IGitService` — Git commit
- `VersionManager` — snapshot/rollback
- `ISemanticSimilarityService` — поиск дупликатов (опционально)
- `ISemanticSearchService` — **semantic search по коду (опционально)** ✨

**Новые компоненты**:
- `IPatternMatcherService`
- `IContextExtractorService`
- `IBatchReplacerService`
- `ISemanticReplaceService`
- `ISemanticTransformerService` (опционально)

### Риски и митигация

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| LLM галлюцинации | Средняя | Roslyn валидация, preview before apply |
| Large batches OOM | Низкая | Pagination, streaming |
| Breaking changes | Средняя | Snapshot + rollback, compile check |
| Performance | Низкая | Caching, parallel processing |

### Документация

- [01_Design.md](./01_Design.md) — архитектура и API
- [02_Examples.md](./02_Examples.md) — use cases и примеры
- [03_Implementation.md](./03_Implementation.md) — код и roadmap

### Следующие шаги

1. ✅ Документация завершена
2. ✅ Phase 1: Models & Interfaces
3. ✅ Phase 2: PatternMatcherService (Regex, Roslyn, **Semantic**)
4. ✅ Phase 3: ContextExtractorService (scope expansion, metadata)
5. ✅ Phase 4: BatchReplacerService (atomic transactions)
6. ✅ Phase 5: SemanticReplaceTools MCP endpoint
7. 🔲 Phase 6: SemanticTransformerService (LLM-based, optional)
8. 🔲 Интеграционное тестирование
9. 🔲 Документация для Run.Docs/Claude/

### Semantic Integration Details

**PatternMatcherService** теперь поддерживает `SearchMode.Semantic`:
- Использует `ISemanticSearchService.FindSimilarCodeAsync()`
- Возвращает результаты с `SemanticSimilarity` и `SemanticFqn`
- Требует активированный semantic mode (TEI/Ollama)

**DI Registration** (`WithSemanticReplace()`):
- `PatternMatcherService` получает `ISemanticSearchService?` опционально
- Если semantic mode не включён — используйте `regex` или `roslyn` режимы

---

**Автор**: Claude
**Дата**: 2025-11-26
**Версия**: 1.1

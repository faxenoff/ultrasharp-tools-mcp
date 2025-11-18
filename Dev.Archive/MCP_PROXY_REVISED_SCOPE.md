# MCP Proxy - Пересмотр scope

**Дата:** 2025-11-18

---

## 🤔 Проблема

Semantic tools (semantic_search, semantic_diff, detect_code_clones) требуют **embedding сервис** (Ollama/TEI) для векторизации текста.

### Архитектурные варианты:

#### Вариант A: Overlord имеет свой embedding сервис
- ✅ Может обрабатывать semantic запросы напрямую
- ❌ Дублирование зависимости (Ollama/TEI и на Droid, и на Overlord)
- ❌ Лишняя нагрузка на сервер

#### Вариант B: Semantic tools только в local mode
- ✅ Без дублирования
- ✅ Простая архитектура
- ❌ Нет cross-project semantic search

#### Вариант C: Hybrid routing с vector passing
- Droid делает embedding локально
- Droid отправляет vector на Overlord
- Overlord ищет по MultiProjectVectorStore
- ✅ Без дублирования
- ✅ Cross-project search
- ❌ Нужно изменить API (добавить vector parameter)

---

## ✅ РЕШЕНИЕ: Вариант B + частично C

### Semantic tools в LOCAL MODE ONLY

**Обоснование:**
1. Embedding сервис (Ollama/TEI) - это локальная зависимость разработчика
2. Overlord не должен иметь ML зависимости
3. Cross-project semantic search - это future feature

**Реализация:**
- `semantic_search` → LOCAL ONLY
- `semantic_diff` → LOCAL ONLY
- `detect_code_clones` → LOCAL ONLY
- `pattern_search` (semantic mode) → LOCAL ONLY

### Cross-project duplicate detection через VECTOR API

**find_duplicates уже реализован**, но требует доработки:

```csharp
// Текущая реализация (заглушка):
var matches = await _vectorStore.SearchAcrossProjectsAsync(
    queryVector: new float[768], // TODO: реальный вектор
    ...
);
```

**Правильная реализация:**

```csharp
public sealed class FindDuplicatesArgs
{
    public string? TargetCode { get; set; }
    public float[]? TargetVector { get; set; }  // ← Новое поле
    public double Threshold { get; set; } = 0.7;
    public string Scope { get; set; } = "current_project";
    public int Limit { get; set; } = 10;
}

// В ExecuteFindDuplicates:
float[] queryVector;

if (args.TargetVector != null && args.TargetVector.Length > 0)
{
    // Vector передан явно (от Droid в hybrid mode)
    queryVector = args.TargetVector;
}
else if (!string.IsNullOrEmpty(args.TargetCode) && _embeddingService != null)
{
    // Есть embedding сервис (standalone Overlord)
    queryVector = await _embeddingService.GetEmbeddingAsync(args.TargetCode, ct);
}
else
{
    return JsonSerializer.Serialize(new
    {
        error = "Either TargetVector or TargetCode with embedding service required"
    });
}

var matches = await _vectorStore.SearchAcrossProjectsAsync(queryVector, ...);
```

---

## 📊 ФИНАЛЬНЫЙ SCOPE для McpProxyService

### ✅ УЖЕ РЕАЛИЗОВАНО (7 инструментов)

1. ✅ `load_solution` - загрузка solution на сервере
2. ✅ `find_duplicates` - cross-project векторный поиск (требует доработки)
3. ✅ `view_definition` - просмотр определений через Symbol Resolution
4. ✅ `find_references` - поиск ссылок через Symbol Resolution
5. ✅ `modify_code` - модификация кода через Symbol Resolution
6. ✅ `analyze_complexity` - анализ сложности (project scope)
7. ✅ `format_code` - форматирование кода

### ⬜ ТРЕБУЕТСЯ ДОРАБОТАТЬ (1 инструмент)

8. ⬜ `find_duplicates` - добавить поддержку TargetVector parameter

### ⬜ ТРЕБУЕТСЯ РЕАЛИЗОВАТЬ (1 инструмент)

9. ⬜ `reindex_changed_files` - переиндексация векторов для измененных файлов

### ❌ НЕ РЕАЛИЗУЕМ в McpProxyService (остаются LOCAL ONLY)

- ❌ `semantic_search` - требует embedding, LOCAL ONLY
- ❌ `semantic_diff` - требует embedding, LOCAL ONLY
- ❌ `detect_code_clones` - требует embedding, LOCAL ONLY
- ❌ `pattern_search` (semantic mode) - требует embedding, LOCAL ONLY

---

## 🎯 ИТОГОВАЯ АРХИТЕКТУРА

### Local Mode
```
Claude → Droid (Local) → SemanticSearchService + Embeddings
                      ↓
                 All 52 tools работают локально
```

### Hybrid Mode - Semantic Tools
```
Claude → Droid (Hybrid) → EmbeddingService (локально)
                       ↓
                  SemanticSearchService (локально)
                       ↓
                  Semantic tools работают по локальной базе
```

### Hybrid Mode - Cross-Project Duplicates
```
Claude → Droid (Hybrid) → EmbeddingService.GetEmbedding(code)
                       ↓
                   vector (float[])
                       ↓
                   POST /api/agent/mcp-proxy
                   { tool: "find_duplicates", vector: [...] }
                       ↓
                   Overlord.McpProxyService
                       ↓
                   MultiProjectVectorStore.SearchAcrossProjects(vector)
                       ↓
                   Cross-project matches
```

### Hybrid Mode - Symbol Operations
```
Claude → Droid (Hybrid) → POST /api/agent/mcp-proxy
                       ↓
                   Overlord.McpProxyService
                       ↓
                   Symbol Resolution Service
                       ↓
                   ISolutionManager (Overlord's solution)
                       ↓
                   view_definition, find_references, modify_code, etc.
```

---

## 📝 UPDATED TODO

### Phase 8.1: Доработка find_duplicates
- [x] Добавить TargetVector parameter в FindDuplicatesArgs
- [ ] Реализовать векторизацию TargetCode (если нет TargetVector)
- [ ] Добавить опциональный IEmbeddingService в McpProxyService
- [ ] Обновить ExecuteFindDuplicates с правильной логикой

### Phase 8.2: Реализация reindex_changed_files
- [ ] Добавить reindex_changed_files в McpProxyService
- [ ] Вызывать MultiProjectVectorStore для обновления векторов
- [ ] Интеграция с FileWatcher events

### Phase 8.3: Tool Routing Logic в Droid
- [ ] Создать ToolRouter service
- [ ] Определить routing rules (LOCAL/OVERLORD)
- [ ] Интегрировать в MCP server pipeline

### Phase 8.4: Testing & Documentation
- [ ] Unit tests для McpProxyService
- [ ] Integration tests для hybrid mode
- [ ] Обновить документацию

---

## ✅ ЗАКЛЮЧЕНИЕ

**McpProxyService финальный scope: 9 инструментов (не 12)**

- 7 уже реализовано
- 1 требует доработки (find_duplicates)
- 1 требует реализации (reindex_changed_files)

**Semantic tools остаются LOCAL ONLY** - это правильное архитектурное решение, избегающее дублирования ML зависимостей.

**Cross-project functionality** реализуется через vector passing, без необходимости иметь embedding сервис на Overlord.

---

**Дата:** 2025-11-18
**Статус:** ✅ Архитектура пересмотрена и уточнена

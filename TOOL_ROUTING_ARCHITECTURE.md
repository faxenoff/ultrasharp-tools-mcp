# Tool Routing Architecture - Hybrid Mode

**Дата:** 2025-11-18
**Версия:** 1.0.0

---

## 🎯 Архитектурная концепция

### Local Mode
- Все инструменты работают локально
- Локальная Roslyn workspace
- Нет сетевых запросов

### Hybrid Mode с Overlord
- **Векторная база синхронизируется постоянно** (FileWatcher, GitWatcher → Overlord)
- **Простые инструменты → LOCAL** (Roslyn операции по локальной копии)
- **Семантические + ресурсоёмкие инструменты → OVERLORD** (векторный поиск, cross-project анализ)
- **Future:** Cross-project операции над несколькими базами вместе

---

## 📊 КЛАССИФИКАЦИЯ ВСЕХ 50+ ИНСТРУМЕНТОВ

### 🟢 LOCAL ONLY (33 инструмента)
**Критерий:** Быстрые Roslyn операции, не требуют векторного поиска

#### 1. Solution & Project Management (2)
| Инструмент | Обоснование |
|------------|-------------|
| `load_solution` | Локальная загрузка Roslyn workspace |
| `load_project` | Локальное построение иерархии типов |

#### 2. Code Analysis - Structural (9)
| Инструмент | Обоснование |
|------------|-------------|
| `get_all_subtypes` | Roslyn API: INamedTypeSymbol.GetMembers() |
| `get_members` | Roslyn API: получение членов типа |
| `view_definition` | Roslyn API: SyntaxTree.GetText() |
| `list_implementations` | Roslyn API: SymbolFinder.FindImplementationsAsync |
| `find_references` | Roslyn API: SymbolFinder.FindReferencesAsync |
| `view_inheritance_chain` | Roslyn API: INamedTypeSymbol.BaseType |
| `view_call_graph` | Roslyn API: обход syntax tree |
| `search_definitions` | Roslyn API + FuzzyFqnLookup (локальный) |
| `manage_usings` | Roslyn API: SyntaxTree manipulation |

#### 3. Code Modification (8)
| Инструмент | Обоснование |
|------------|-------------|
| `add_member` | Roslyn API: SyntaxFactory, добавление узлов |
| `modify_code` | Roslyn API: ReplaceNode |
| `rename_symbol` | Roslyn API: Renamer.RenameSymbolAsync |
| `replace_all_references` | Roslyn API: SyntaxRewriter |
| `replace_references_by_pattern` | Roslyn API + Regex |
| `undo` | Локальный Git: git reset |
| `find_and_replace` | Roslyn API + Regex |
| `move_member` | Roslyn API: refactoring |

#### 4. Document Operations (4)
| Инструмент | Обоснование |
|------------|-------------|
| `read_file` | File I/O: File.ReadAllText |
| `create_file` | File I/O: File.WriteAllText |
| `overwrite_file` | File I/O: File.WriteAllText |
| `list_file_entities` | Roslyn API: SyntaxTree parsing |

#### 5. Code Quality (3)
| Инструмент | Обоснование |
|------------|-------------|
| `format_code` | CSharpier: локальное форматирование |
| `analyze_code_style` | Roslyn Analyzers: локальные правила |
| `apply_code_fixes` | Roslyn CodeFixes: локальное применение |

#### 6. Validation (3)
| Инструмент | Обоснование |
|------------|-------------|
| `validate_file` | Roslyn Analyzers: компиляция + диагностика |
| `validate_directory` | Roslyn Analyzers: batch validation |
| `compare_validation` | Локальное сравнение результатов |

#### 7. Snapshots (3)
| Инструмент | Обоснование |
|------------|-------------|
| `create_snapshot` | Локальный Git: commit |
| `rollback_snapshot` | Локальный Git: checkout |
| `list_snapshots` | Локальный Git: git log |

#### 8. Miscellaneous (1)
| Инструмент | Обоснование |
|------------|-------------|
| `cleanup_snapshots` | Локальный Git: удаление веток |

---

### 🔴 OVERLORD REQUIRED (12 инструментов)
**Критерий:** Векторный поиск, семантический анализ, cross-project операции

#### 1. Semantic Analysis (4)
| Инструмент | Обоснование |
|------------|-------------|
| `semantic_search` | **Векторная база:** embedding search по описанию на NL |
| `semantic_diff` | **Векторная база:** семантическое сравнение кода |
| `detect_code_clones` | **ML модель:** обнаружение дубликатов через embeddings |
| `find_duplicates` | **Векторная база:** similarity search |

#### 2. Pattern Search - Semantic Mode (1)
| Инструмент | Обоснование |
|------------|-------------|
| `pattern_search` (semantic mode) | **Векторная база:** когда mode="semantic" или "hybrid" |

#### 3. Cross-Project Analysis (3) - FUTURE
| Инструмент | Обоснование |
|------------|-------------|
| `find_duplicates` (scope=all_projects) | **Multi-project векторная база:** поиск во всех проектах |
| `semantic_search` (scope=all_projects) | **Multi-project векторная база:** cross-project search |
| `detect_code_clones` (scope=all_projects) | **Multi-project векторная база:** team-wide clones |

#### 4. Reindexing (1)
| Инструмент | Обоснование |
|------------|-------------|
| `reindex_changed_files` | **Векторная база:** обновление embeddings |

#### 5. Complex Analysis (3) - Потенциально ресурсоёмкие
| Инструмент | Обоснование |
|------------|-------------|
| `analyze_complexity` (project scope) | **Ресурсоёмкий:** анализ всего проекта, лучше на сервере |
| `trace_execution` | **Ресурсоёмкий:** построение CFG для больших методов |
| `analyze_path_feasibility` | **ОЧЕНЬ ресурсоёмкий:** Z3 Solver, символическое выполнение |

---

### 🟡 HYBRID (5 инструментов)
**Критерий:** Могут работать локально, но с Overlord эффективнее

#### 1. Pattern Search (1)
| Инструмент | Режим работы |
|------------|--------------|
| `pattern_search` | **LOCAL:** mode="entity" или "content" (Roslyn/Regex)<br>**OVERLORD:** mode="semantic" или "hybrid" (векторная база) |

#### 2. Analysis - Complexity (1)
| Инструмент | Режим работы |
|------------|--------------|
| `analyze_complexity` | **LOCAL:** method/class scope (быстрый анализ)<br>**OVERLORD:** project scope (ресурсоёмкий) |

#### 3. Tracing (3)
| Инструмент | Режим работы |
|------------|--------------|
| `trace_execution` | **LOCAL:** small methods (< 100 LOC)<br>**OVERLORD:** large methods или cross-method tracing |
| `trace_backwards` | **LOCAL:** локальный scope<br>**OVERLORD:** cross-file backtrace |
| `export_call_graph` | **LOCAL:** single file<br>**OVERLORD:** project-wide graph |

#### 4. Technology Detection (1)
| Инструмент | Режим работы |
|------------|--------------|
| `detect_technology_stack` | **LOCAL:** быстрое сканирование<br>**OVERLORD:** глубокий анализ + ML classification |

---

### ❓ СПЕЦИАЛЬНЫЕ СЛУЧАИ (2 инструмента)

#### 1. Log Analysis (1)
| Инструмент | Режим работы |
|------------|--------------|
| `analyze_logs` | **LOCAL:** небольшие логи (< 10 MB)<br>**OVERLORD:** большие логи (> 10 MB) + ML pattern detection |

#### 2. File Operations - Split/Synthesize (2)
| Инструмент | Режим работы |
|------------|--------------|
| `split_file` | **LOCAL:** простое разделение<br>**OVERLORD:** с анализом зависимостей |
| `synthesize_files` | **LOCAL:** простое объединение<br>**OVERLORD:** с оптимизацией импортов |

#### 3. Attributes & Usings (1)
| Инструмент | Режим работы |
|------------|--------------|
| `manage_attributes` | **LOCAL:** всегда локально (быстрая операция) |

#### 4. Package Management (1)
| Инструмент | Режим работы |
|------------|--------------|
| `add_package` | **LOCAL:** всегда локально (изменение .csproj) |

#### 5. Request New Tool (1)
| Инструмент | Режим работы |
|------------|--------------|
| `request_new_tool` | **LOCAL:** просто логирование запроса |

---

## 📈 ИТОГОВАЯ СТАТИСТИКА

| Категория | Количество | % от общего |
|-----------|------------|-------------|
| 🟢 LOCAL ONLY | 33 | 63% |
| 🔴 OVERLORD REQUIRED | 12 | 23% |
| 🟡 HYBRID | 5 | 10% |
| ❓ СПЕЦИАЛЬНЫЕ | 2 | 4% |
| **ВСЕГО** | **52** | **100%** |

---

## 🚀 ROUTING LOGIC

### Droid должен принимать решение:

```csharp
public enum ToolRoutingDecision
{
    Local,      // Выполнить локально
    Overlord,   // Отправить на Overlord
    Hybrid      // Решение на основе параметров
}

public ToolRoutingDecision DetermineRouting(string toolName, Dictionary<string, object> args)
{
    // 1. Если local mode - всё локально
    if (_mode == AgentMode.Local) return ToolRoutingDecision.Local;

    // 2. Semantic tools → всегда Overlord
    if (SemanticTools.Contains(toolName)) return ToolRoutingDecision.Overlord;

    // 3. Hybrid tools → анализ параметров
    if (HybridTools.Contains(toolName))
    {
        // pattern_search: mode = "semantic" → Overlord
        if (toolName == "pattern_search" && args["mode"] is "semantic" or "hybrid")
            return ToolRoutingDecision.Overlord;

        // analyze_complexity: scope = "project" → Overlord
        if (toolName == "analyze_complexity" && args["scope"] == "project")
            return ToolRoutingDecision.Overlord;

        // trace_execution: large method → Overlord
        if (toolName == "trace_execution" && EstimateMethodSize(args) > 100)
            return ToolRoutingDecision.Overlord;
    }

    // 4. По умолчанию → Local
    return ToolRoutingDecision.Local;
}
```

---

## 🎯 PRIORITY IMPLEMENTATION

### Phase 1: Критические semantic tools (4 инструмента)
- ✅ `find_duplicates` - уже реализовано
- ⬜ `semantic_search` - требуется
- ⬜ `semantic_diff` - требуется
- ⬜ `detect_code_clones` - требуется

### Phase 2: Hybrid tools (5 инструментов)
- ✅ `analyze_complexity` - уже реализовано
- ⬜ `pattern_search` - routing логика
- ⬜ `trace_execution` - routing логика
- ⬜ `trace_backwards` - routing логика
- ⬜ `export_call_graph` - routing логика

### Phase 3: Resource-intensive tools (2 инструмента)
- ⬜ `analyze_path_feasibility` - Z3 solver на сервере
- ⬜ `analyze_logs` - большие файлы на сервере

### Phase 4: Future - Cross-project (3 инструмента)
- ⬜ `find_duplicates` с scope=all_projects
- ⬜ `semantic_search` с scope=all_projects
- ⬜ `detect_code_clones` с scope=all_projects

---

## 🔧 ТЕХНИЧЕСКИЕ ДЕТАЛИ

### Overlord должен иметь McpProxyService с:

**Обязательные инструменты (12):**
1. `find_duplicates` ✅
2. `semantic_search` ⬜
3. `semantic_diff` ⬜
4. `detect_code_clones` ⬜
5. `reindex_changed_files` ⬜
6. `pattern_search` (semantic mode) ⬜
7. `analyze_complexity` (project scope) ✅
8. `trace_execution` (large methods) ⬜
9. `trace_backwards` ⬜
10. `export_call_graph` ⬜
11. `analyze_path_feasibility` ⬜
12. `analyze_logs` (large files) ⬜

### Droid должен иметь routing logic:

```csharp
public class ToolRouter
{
    private readonly HashSet<string> _semanticTools = new()
    {
        "find_duplicates",
        "semantic_search",
        "semantic_diff",
        "detect_code_clones",
        "reindex_changed_files"
    };

    private readonly HashSet<string> _hybridTools = new()
    {
        "pattern_search",
        "analyze_complexity",
        "trace_execution",
        "trace_backwards",
        "export_call_graph"
    };

    public async Task<string> ExecuteTool(string toolName, string argsJson)
    {
        var decision = DetermineRouting(toolName, argsJson);

        return decision switch
        {
            ToolRoutingDecision.Local => await _localMcpServer.ExecuteTool(toolName, argsJson),
            ToolRoutingDecision.Overlord => await _serverBridge.CallMcpProxyAsync(toolName, argsJson),
            _ => throw new NotSupportedException()
        };
    }
}
```

---

## 📊 ПРЕИМУЩЕСТВА АРХИТЕКТУРЫ

### 1. Performance
- 63% инструментов работают локально (низкая latency)
- Только 23% требуют сетевых запросов
- 10% адаптируются к размеру задачи

### 2. Scalability
- Ресурсоёмкие операции на сервере (Z3, ML)
- Локальные операции не нагружают сеть
- Возможность horizontal scaling Overlord'а

### 3. Offline Support
- Local mode полностью функционален без сети
- Hybrid mode gracefully degradation если Overlord недоступен

### 4. Future-Ready
- Cross-project анализ "из коробки"
- Multi-tenant поддержка (разные команды)
- Централизованное кэширование embeddings

---

## 🎉 ЗАКЛЮЧЕНИЕ

**Архитектура определена!**

- **33 инструмента** работают только локально (быстрые Roslyn операции)
- **12 инструментов** требуют Overlord (semantic + ресурсоёмкие)
- **5 инструментов** адаптивные (routing на основе параметров)
- **2 инструмента** специальные случаи

**Next Steps:**
1. Реализовать routing logic в Droid
2. Добавить semantic tools в McpProxyService
3. Реализовать hybrid tools с условным routing
4. Обновить документацию

---

**Дата:** 2025-11-18
**Версия:** 1.0.0
**Статус:** ✅ **Architecture Defined**

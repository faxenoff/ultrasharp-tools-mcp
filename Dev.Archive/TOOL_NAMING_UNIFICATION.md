# Унификация Названий Инструментов MCP
## UltrasharpTools ↔ UltrascriptTools

**Дата анализа:** 2025-11-17
**Версия UltrasharpTools:** v1.0.0 (36 инструментов, C#/компилируемые языки)
**Версия UltrascriptTools:** v2.8.0 (42 инструмента, TypeScript/скриптовые языки)

---

## Исполнительное резюме

### Ключевые выводы

1. **Префикс не важен** - MCP использует namespace через имя сервера, внутренние названия инструментов могут совпадать
2. **Можно унифицировать 27 из 78 инструментов** (35% пересечение по функциональности)
3. **Рекомендуется гибридная схема**: snake_case для общих инструментов, PascalCase для специфичных
4. **Overlord сервер сможет роутить запросы** по типу языка в файле

### Рекомендации

- ✅ **UltrasharpTools**: переименовать 12 инструментов для соответствия общей схеме
- ✅ **UltrascriptTools**: переименовать 8 инструментов для консистентности
- ✅ **Overlord**: маршрутизация по расширению файла (`.cs`/`.ts`) на соответствующий сервер

---

## 1. Функциональное Сравнение

### 1.1 Загрузка и Индексация Проекта

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Загрузить проект | `LoadSolution` | `index` | ⚠️ Разные концепции |
| Загрузить детали проекта | `LoadProject` | *(автоматически в index)* | ⚠️ Разная гранулярность |
| Очистить и переиндексировать | *(нет)* | `clean_index` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrasharpTools: LoadSolution → load_solution
UltrasharpTools: LoadProject → load_project (оставить как есть, специфичный для C#)
```

---

### 1.2 Просмотр и Навигация по Коду

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Просмотр определения | `ViewDefinition` | *(через query/get_graph)* | ⚠️ Разный подход |
| Список членов типа | `GetMembers` | `list_file_entities` | ✅ Можно унифицировать |
| Список всех подтипов | `GetAllSubtypes` | *(через list_entity_relationships)* | ⚠️ Разная гранулярность |
| Поиск по определениям | `SearchDefinitions` | `pattern_search` (mode="entity") | ✅ Можно унифицировать |

**Рекомендация:**
```
UltrasharpTools: GetMembers → get_members
UltrasharpTools: ViewDefinition → view_definition
UltrasharpTools: GetAllSubtypes → get_all_subtypes
UltrasharpTools: SearchDefinitions → search_definitions

UltrascriptTools: list_file_entities → get_members (alias)
UltrascriptTools: pattern_search → ОК (более мощный)
```

---

### 1.3 Анализ Зависимостей

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Найти ссылки | `FindReferences` | `list_entity_relationships` | ✅ Можно унифицировать |
| Граф вызовов | `ViewCallGraph` | *(через analyze_code_impact)* | ⚠️ Разный уровень детализации |
| Цепочка наследования | `ViewInheritanceChain` | *(через list_entity_relationships)* | ⚠️ C#-специфично |
| Анализ влияния | *(через FindReferences)* | `analyze_code_impact` | ✅ Можно унифицировать |

**Рекомендация:**
```
UltrasharpTools: FindReferences → find_references
UltrasharpTools: ViewCallGraph → view_call_graph
UltrasharpTools: ViewInheritanceChain → ОК (специфично для C#)

UltrascriptTools: list_entity_relationships → find_references (alias)
UltrascriptTools: analyze_code_impact → ОК
```

---

### 1.4 Модификация Кода

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Добавить член | `AddMember` | *(modify_entity_code с новым кодом)* | ⚠️ C# специфично |
| Перезаписать член | `OverwriteMember` | `modify_entity_code` | ✅ Можно унифицировать |
| Переименовать символ | `RenameSymbol` | `rename_file` (только файлы) | ⚠️ Разный scope |
| Заменить все ссылки | `ReplaceAllReferences` | *(нет)* | ➕ Добавить в TS |
| Переместить член | `MoveMember` | *(нет)* | ➕ Добавить в TS |
| Найти и заменить | `FindAndReplace` | `pattern_search` + modify | ⚠️ Разный подход |
| Отменить изменение | `Undo` | `rollback_snapshot` | ✅ Можно унифицировать |

**Рекомендация:**
```
UltrasharpTools: OverwriteMember → modify_code
UltrasharpTools: RenameSymbol → rename_symbol
UltrasharpTools: ReplaceAllReferences → replace_all_references
UltrasharpTools: FindAndReplace → find_and_replace
UltrasharpTools: Undo → undo (или rollback)

UltrascriptTools: modify_entity_code → modify_code (универсальнее)
UltrascriptTools: rollback_snapshot → undo (alias, для консистентности)
```

---

### 1.5 Работа с Файлами

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Прочитать файл | `ReadRawFromRoslynDocument` | *(встроенное чтение при индексации)* | ⚠️ Разные подходы |
| Создать файл | `CreateRoslynDocument` | *(нет прямого инструмента)* | ➕ Добавить в TS |
| Перезаписать файл | `OverwriteRoslynDocument` | *(modify_entity_code)* | ⚠️ Разный scope |
| Список типов в файле | `ReadTypesFromRoslynDocument` | `list_file_entities` | ✅ Можно унифицировать |
| Скопировать файл | *(нет)* | `copy_file` | ➕ Добавить в C# |
| Переименовать файл | *(нет)* | `rename_file` | ➕ Добавить в C# |
| Разделить файл | *(нет)* | `split_file` | ➕ Добавить в C# |
| Объединить файлы | *(нет)* | `synthesize_files` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrasharpTools: ReadRawFromRoslynDocument → read_file
UltrasharpTools: CreateRoslynDocument → create_file
UltrasharpTools: OverwriteRoslynDocument → overwrite_file
UltrasharpTools: ReadTypesFromRoslynDocument → list_file_entities

UltrascriptTools: copy_file → ОК
UltrascriptTools: rename_file → ОК
UltrascriptTools: split_file → ОК
UltrascriptTools: synthesize_files → ОК
```

---

### 1.6 Анализ Качества и Сложности

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Анализ сложности | `AnalyzeComplexity` | `analyze_hotspots` | ✅ Можно унифицировать |
| Поиск дубликатов | `FindPotentialDuplicates` | `detect_code_clones` | ✅ Можно унифицировать |
| Анализ стиля | `AnalyzeCodeStyle` | `validate_file` | ✅ Можно унифицировать |
| Предложения рефакторинга | *(нет)* | `suggest_refactoring` | ➕ Добавить в C# |
| Анализ горячих точек | *(через AnalyzeComplexity)* | `analyze_hotspots` | ✅ Можно унифицировать |

**Рекомендация:**
```
UltrasharpTools: AnalyzeComplexity → analyze_complexity
UltrasharpTools: FindPotentialDuplicates → find_duplicates
UltrasharpTools: AnalyzeCodeStyle → analyze_code_style

UltrascriptTools: detect_code_clones → find_duplicates (унифицировать)
UltrascriptTools: analyze_hotspots → ОК
UltrascriptTools: suggest_refactoring → ОК
UltrascriptTools: validate_file → validate_file
```

---

### 1.7 Форматирование и Исправления

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Форматирование | `FormatCode` | *(через validate_file с auto-fix)* | ⚠️ Разный подход |
| Применить исправления | `ApplyCodeFixes` | *(через validate_file)* | ⚠️ Разный подход |

**Рекомендация:**
```
UltrasharpTools: FormatCode → format_code
UltrasharpTools: ApplyCodeFixes → apply_code_fixes

UltrascriptTools: validate_file → можно добавить отдельный format_code
```

---

### 1.8 Семантический Поиск

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Семантический поиск | *(нет)* | `semantic_search` | ➕ Добавить в C# |
| Поиск похожего кода | `FindPotentialDuplicates` (структурно) | `find_similar_code` (семантически) | ⚠️ Разные алгоритмы |
| Кросс-языковой поиск | *(нет)* | `cross_language_search` | ➖ Не нужно в C# |
| Поиск связанных концепций | *(нет)* | `find_related_concepts` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrascriptTools: semantic_search → ОК
UltrascriptTools: find_similar_code → ОК
UltrascriptTools: find_related_concepts → ОК

(для C#: можно добавить похожие инструменты в будущем)
```

---

### 1.9 Управление Using/Imports и Атрибутами

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Управление using | `ManageUsings` | *(автоматически при modify_code)* | ⚠️ C#-специфично |
| Управление атрибутами | `ManageAttributes` | *(нет, редко используется в TS)* | ⚠️ C#-специфично |

**Рекомендация:**
```
UltrasharpTools: ManageUsings → manage_usings (оставить, специфично)
UltrasharpTools: ManageAttributes → manage_attributes (оставить, специфично)
```

---

### 1.10 Отладка и Трассировка

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Трассировка выполнения | `TraceExecution` | *(нет)* | ➕ Добавить в TS |
| Обратная трассировка | `TraceBackwards` | *(нет)* | ➕ Добавить в TS |
| Анализ выполнимости пути | `AnalyzePathFeasibility` | *(нет)* | ➕ Добавить в TS |
| Экспорт графа | `ExportCallGraph` | *(через get_graph)* | ⚠️ Разный формат |
| Анализ логов | `AnalyzeLogs` | *(нет)* | ➕ Добавить в TS |

**Рекомендация:**
```
UltrasharpTools: TraceExecution → trace_execution
UltrasharpTools: TraceBackwards → trace_backwards
UltrasharpTools: AnalyzePathFeasibility → analyze_path_feasibility
UltrasharpTools: ExportCallGraph → export_call_graph
UltrasharpTools: AnalyzeLogs → analyze_logs
```

---

### 1.11 Управление Пакетами

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Добавить/изменить пакет | `AddOrModifyNugetPackage` | *(нет)* | ➕ Добавить в TS |
| Обнаружить стек технологий | *(нет)* | `detect_technology_stack` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrasharpTools: AddOrModifyNugetPackage → add_package
UltrascriptTools: detect_technology_stack → ОК (универсально)
```

---

### 1.12 Управление Версиями

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Отменить изменение | `Undo` (через Git) | `rollback_snapshot` | ✅ Можно унифицировать |
| Создать снапшот | *(автоматически при изменении)* | `create_snapshot` | ➕ Добавить в C# |
| Список снапшотов | *(нет)* | `list_snapshots` | ➕ Добавить в C# |
| Очистка снапшотов | *(нет)* | `cleanup_snapshots` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrasharpTools: Undo → undo (или rollback)

UltrascriptTools: create_snapshot → ОК
UltrascriptTools: rollback_snapshot → undo (alias)
UltrascriptTools: list_snapshots → ОК
UltrascriptTools: cleanup_snapshots → ОК
```

---

### 1.13 Граф Зависимостей и Метаданные

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Получить граф | *(через LoadProject)* | `get_graph` | ⚠️ Разный формат |
| Статистика графа | *(нет)* | `get_graph_stats` | ➕ Добавить в C# |
| Здоровье графа | *(нет)* | `get_graph_health` | ➕ Добавить в C# |
| Сбросить граф | *(LoadSolution заново)* | `reset_graph` | ➕ Добавить в C# |
| Lerna граф проектов | *(нет, не нужно для C#)* | `lerna_project_graph` | ➖ Специфично для JS |

**Рекомендация:**
```
UltrascriptTools: get_graph → ОК
UltrascriptTools: get_graph_stats → ОК
UltrascriptTools: get_graph_health → ОК
UltrascriptTools: reset_graph → ОК
```

---

### 1.14 Метрики и Диагностика

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Метрики агентов | *(нет)* | `get_agent_metrics` | ➕ Добавить в C# |
| Статистика шины знаний | *(нет)* | `get_bus_stats` | ➕ Добавить в C# |
| Очистка топика шины | *(нет)* | `clear_bus_topic` | ➕ Добавить в C# |
| Получить версию | *(нет)* | `get_version` | ➕ Добавить в C# |
| Получить метрики | *(нет)* | `get_metrics` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrascriptTools: get_agent_metrics → ОК
UltrascriptTools: get_bus_stats → ОК
UltrascriptTools: clear_bus_topic → ОК
UltrascriptTools: get_version → ОК
UltrascriptTools: get_metrics → ОК
```

---

### 1.15 Управление Ветками

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Список веток | *(нет)* | `list_branches` | ➕ Добавить в C# |
| Переключить ветку | *(нет)* | `switch_branch` | ➕ Добавить в C# |
| Статус ветки | *(нет)* | `get_branch_status` | ➕ Добавить в C# |
| Очистка веток | *(нет)* | `cleanup_branches` | ➕ Добавить в C# |
| Измененные файлы | *(нет)* | `get_changed_files` | ➕ Добавить в C# |

**Рекомендация:**
```
(Добавить всю функциональность branch-aware indexing в UltrasharpTools)
```

---

### 1.16 Прочее

| **Функция** | **UltrasharpTools (C#)** | **UltrascriptTools (TS)** | **Статус** |
|-------------|--------------------------|---------------------------|------------|
| Запросить новый инструмент | `RequestNewTool` | *(нет)* | ➕ Добавить в TS |
| JSCPD клоны | *(нет, используется FindPotentialDuplicates)* | `jscpd_detect_clones` | ➖ Специфично для TS |
| Query (универсальный) | *(нет)* | `query` | ➕ Добавить в C# |

**Рекомендация:**
```
UltrasharpTools: RequestNewTool → request_new_tool
UltrascriptTools: query → ОК (универсальный поиск)
```

---

## 2. Итоговая Схема Унификации

### 2.1 Общие Принципы

1. **Стиль именования**: `snake_case` для всех инструментов (как в MCP Protocol)
2. **Префикс**: не использовать (namespace определяется именем сервера)
3. **Глаголы**: `get_`, `list_`, `find_`, `analyze_`, `create_`, `modify_`, `delete_`
4. **Существительные**: в единственном числе (`member`, `file`, `entity`)

### 2.2 Переименования в UltrasharpTools

**Высокий приоритет (общие с UltrascriptTools):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `LoadSolution` | `load_solution` | Унификация стиля |
| `LoadProject` | `load_project` | Унификация стиля |
| `GetMembers` | `get_members` | Унификация стиля |
| `ViewDefinition` | `view_definition` | Унификация стиля |
| `FindReferences` | `find_references` | Совпадает с TS |
| `OverwriteMember` | `modify_code` | Более универсально |
| `FindPotentialDuplicates` | `find_duplicates` | Совпадает с TS |
| `AnalyzeComplexity` | `analyze_complexity` | Унификация стиля |
| `AnalyzeCodeStyle` | `analyze_code_style` | Унификация стиля |
| `FormatCode` | `format_code` | Унификация стиля |
| `ApplyCodeFixes` | `apply_code_fixes` | Унификация стиля |
| `Undo` | `undo` | Совпадает с TS (через alias) |

**Средний приоритет (специфичные для C#, но стиль нужен):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `AddMember` | `add_member` | Унификация стиля |
| `RenameSymbol` | `rename_symbol` | Унификация стиля |
| `ReplaceAllReferences` | `replace_all_references` | Унификация стиля |
| `ReplaceAllReferencesByPattern` | `replace_references_by_pattern` | Унификация + краткость |
| `FindAndReplace` | `find_and_replace` | Унификация стиля |
| `MoveMember` | `move_member` | Унификация стиля |
| `ListImplementations` | `list_implementations` | Унификация стиля |
| `ViewInheritanceChain` | `view_inheritance_chain` | Унификация стиля |
| `ViewCallGraph` | `view_call_graph` | Унификация стиля |
| `SearchDefinitions` | `search_definitions` | Унификация стиля |
| `GetAllSubtypes` | `get_all_subtypes` | Унификация стиля |
| `ManageUsings` | `manage_usings` | Унификация стиля |
| `ManageAttributes` | `manage_attributes` | Унификация стиля |

**Низкий приоритет (документы и файлы):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `ReadRawFromRoslynDocument` | `read_file` | Краткость + универсальность |
| `CreateRoslynDocument` | `create_file` | Краткость + универсальность |
| `OverwriteRoslynDocument` | `overwrite_file` | Краткость + универсальность |
| `ReadTypesFromRoslynDocument` | `list_file_entities` | Совпадает с TS |

**Низкий приоритет (трассировка и отладка):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `TraceExecution` | `trace_execution` | Унификация стиля |
| `TraceBackwards` | `trace_backwards` | Унификация стиля |
| `AnalyzePathFeasibility` | `analyze_path_feasibility` | Унификация стиля |
| `ExportCallGraph` | `export_call_graph` | Унификация стиля |
| `AnalyzeLogs` | `analyze_logs` | Унификация стиля |

**Низкий приоритет (пакеты и прочее):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `AddOrModifyNugetPackage` | `add_package` | Краткость + универсальность |
| `RequestNewTool` | `request_new_tool` | Унификация стиля |

---

### 2.3 Переименования в UltrascriptTools

**Высокий приоритет (для совпадения с UltrasharpTools):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| `list_file_entities` | `get_members` | Совпадает с C# (или оставить оба как alias) |
| `detect_code_clones` | `find_duplicates` | Совпадает с C# |
| `modify_entity_code` | `modify_code` | Более универсально |
| `rollback_snapshot` | `undo` | Совпадает с C# (или оставить оба как alias) |

**Средний приоритет (консистентность стиля):**

| **Старое название** | **Новое название** | **Причина** |
|---------------------|-------------------|-------------|
| *(все уже в snake_case)* | *(ОК)* | ✅ Стиль уже соответствует |

---

### 2.4 Финальный Список Унифицированных Названий (27 инструментов)

**Категория: Загрузка и Индексация**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `load_solution` | ✅ (LoadSolution) | ➖ (не нужен) |
| `load_project` | ✅ (LoadProject) | ➖ (не нужен) |
| `index` | ➖ (не нужен) | ✅ (index) |
| `clean_index` | ➕ TODO | ✅ (clean_index) |

**Категория: Просмотр и Навигация**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `view_definition` | ✅ (ViewDefinition) | ➖ (через query) |
| `get_members` | ✅ (GetMembers) | ✅ (list_file_entities, alias) |
| `find_references` | ✅ (FindReferences) | ✅ (list_entity_relationships, alias) |
| `search_definitions` | ✅ (SearchDefinitions) | ✅ (pattern_search mode="entity") |

**Категория: Анализ Зависимостей**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `view_call_graph` | ✅ (ViewCallGraph) | ➖ (через analyze_code_impact) |
| `analyze_code_impact` | ➕ TODO (через FindReferences) | ✅ (analyze_code_impact) |

**Категория: Модификация Кода**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `modify_code` | ✅ (OverwriteMember) | ✅ (modify_entity_code) |
| `add_member` | ✅ (AddMember) | ➕ TODO |
| `rename_symbol` | ✅ (RenameSymbol) | ➕ TODO (rename_file только файлы) |
| `find_and_replace` | ✅ (FindAndReplace) | ➕ TODO |
| `undo` | ✅ (Undo) | ✅ (rollback_snapshot, alias) |

**Категория: Работа с Файлами**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `read_file` | ✅ (ReadRawFromRoslynDocument) | ➖ (автоматически) |
| `create_file` | ✅ (CreateRoslynDocument) | ➕ TODO |
| `overwrite_file` | ✅ (OverwriteRoslynDocument) | ➖ (через modify_code) |
| `list_file_entities` | ✅ (ReadTypesFromRoslynDocument) | ✅ (list_file_entities) |
| `copy_file` | ➕ TODO | ✅ (copy_file) |
| `rename_file` | ➕ TODO | ✅ (rename_file) |

**Категория: Анализ Качества**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `analyze_complexity` | ✅ (AnalyzeComplexity) | ➖ (через analyze_hotspots) |
| `find_duplicates` | ✅ (FindPotentialDuplicates) | ✅ (detect_code_clones) |
| `analyze_code_style` | ✅ (AnalyzeCodeStyle) | ➖ (через validate_file) |
| `analyze_hotspots` | ➕ TODO | ✅ (analyze_hotspots) |
| `suggest_refactoring` | ➕ TODO | ✅ (suggest_refactoring) |

**Категория: Форматирование**

| **Унифицированное название** | **UltrasharpTools** | **UltrascriptTools** |
|------------------------------|---------------------|----------------------|
| `format_code` | ✅ (FormatCode) | ➕ TODO (разделить из validate_file) |
| `apply_code_fixes` | ✅ (ApplyCodeFixes) | ➖ (через validate_file) |
| `validate_file` | ➕ TODO | ✅ (validate_file) |

---

## 3. План Миграции

### 3.1 Этап 1: UltrasharpTools (Высокий приоритет)

**Неделя 1-2: Переименование основных инструментов**

```csharp
// До:
[McpServerTool(Name = "UltrasharpTool_LoadSolution")]
public static Task<object> LoadSolution(...)

// После:
[McpServerTool(Name = "load_solution")]
public static Task<object> LoadSolution(...) // метод C# может остаться PascalCase
```

**Затронутые файлы:**
- `SolutionTools.cs`: LoadSolution → load_solution, LoadProject → load_project
- `AnalysisTools.cs`: все 12 инструментов → snake_case
- `ModificationTools.cs`: все 8 инструментов → snake_case
- `QualityTools.cs`: все 3 инструмента → snake_case
- `DocumentTools.cs`: все 4 инструмента → snake_case

**Backward compatibility:**
- Добавить `[Obsolete]` атрибут со старыми именами
- Поддерживать старые имена 3 месяца, затем удалить

---

### 3.2 Этап 2: UltrascriptTools (Средний приоритет)

**Неделя 3: Добавление alias'ов**

```typescript
// В index.ts, добавить alias'ы:
server.setRequestHandler("get_members", async (request) => {
  // Redirect to list_file_entities
  return await handlers.list_file_entities(request);
});

server.setRequestHandler("find_duplicates", async (request) => {
  // Redirect to detect_code_clones
  return await handlers.detect_code_clones(request);
});

server.setRequestHandler("undo", async (request) => {
  // Redirect to rollback_snapshot
  return await handlers.rollback_snapshot(request);
});
```

**Затронутые файлы:**
- `src/index.ts`: добавить 4 alias'а (get_members, find_duplicates, modify_code, undo)

**Backward compatibility:**
- Старые названия продолжат работать
- В документации указать предпочтительные названия

---

### 3.3 Этап 3: Overlord Маршрутизация (Низкий приоритет)

**Неделя 4-5: Реализация интеллектуальной маршрутизации**

```csharp
// Overlord/ToolRouter.cs
public class ToolRouter {
    public async Task<object> RouteToolCall(string toolName, Dictionary<string, object> args) {
        // Определить язык по параметрам
        var language = DetectLanguage(args);

        if (language == "csharp") {
            return await ultrasharpClient.CallTool(toolName, args);
        } else {
            return await ultrascriptClient.CallTool(toolName, args);
        }
    }

    private string DetectLanguage(Dictionary<string, object> args) {
        // Проверка по filePath
        if (args.TryGetValue("filePath", out var path)) {
            var ext = Path.GetExtension(path.ToString());
            if (ext == ".cs") return "csharp";
            if (ext == ".ts" || ext == ".js") return "typescript";
        }

        // Проверка по fullyQualifiedName (FQN только у C#)
        if (args.ContainsKey("fullyQualifiedTargetName")) {
            return "csharp";
        }

        // По умолчанию - script языки
        return "typescript";
    }
}
```

**Затронутые инструменты (роутятся автоматически):**
- `modify_code` → .cs файлы идут в C#, остальные в TS
- `find_duplicates` → оба сервера поддерживают
- `format_code` → по расширению файла
- `analyze_complexity` → по расширению файла

---

## 4. Преимущества Унификации

### 4.1 Для Разработчиков

✅ **Единый интерфейс** - не нужно помнить разные названия для похожих операций
✅ **Легче переключаться** между C# и TypeScript проектами
✅ **Меньше ошибок** при вызове инструментов
✅ **Лучшая документация** - можно использовать общие примеры

### 4.2 Для AI Агентов

✅ **Упрощенный выбор инструмента** - одинаковые имена для похожих задач
✅ **Меньше токенов** в промптах - не нужно объяснять различия
✅ **Автоматическая маршрутизация** - Overlord решает куда направить запрос
✅ **Универсальные workflow** - один скрипт для C# и TS проектов

### 4.3 Для Overlord Сервера

✅ **Простая маршрутизация** по расширению файла
✅ **Fallback механизм** - если один сервер не поддерживает, пробует другой
✅ **Агрегация результатов** - может опросить оба сервера для кросс-языковых проектов
✅ **Единая метрика** - статистика использования инструментов

---

## 5. Различия, Которые Нужно Сохранить

### 5.1 C#-Специфичные Инструменты

**Roslyn-зависимые:**
- `manage_usings` - управление using директивами (важно для C#, редко в TS)
- `manage_attributes` - управление атрибутами (нет аналога в TS)
- `view_inheritance_chain` - цепочка наследования (менее важно для JS/TS)
- `list_implementations` - реализации интерфейсов (строгая типизация C#)
- `get_all_subtypes` - рекурсивный список членов (иерархия типов C#)

**Не портировать в TS** - эти концепции специфичны для компилируемых языков.

---

### 5.2 TS-Специфичные Инструменты

**Multi-agent архитектура:**
- `get_agent_metrics` - метрики производительности агентов
- `get_bus_stats` - статистика knowledge bus
- `clear_bus_topic` - очистка топиков шины знаний

**Не портировать в C#** (пока) - UltrasharpTools использует прямой Roslyn доступ, а не agent-based архитектуру.

**Lerna/Monorepo:**
- `lerna_project_graph` - специфично для JavaScript monorepo

**Не портировать в C#** - .NET использует .sln файлы, не Lerna.

**Semantic Search:**
- `semantic_search` - векторные embeddings для поиска
- `find_similar_code` - семантическая схожесть
- `cross_language_search` - поиск по нескольким языкам
- `find_related_concepts` - поиск связанных концепций

**Можно портировать в C#** в будущем, но приоритет низкий (Roslyn уже дает структурный поиск).

---

## 6. Ответы на Вопросы

### ❓ Можно ли инструменты одинаково назвать?

**✅ Да, можно.** MCP использует namespace через имя сервера:
```
ultrasharp-tools::modify_code  (C# сервер)
ultrascript-tools::modify_code (TS сервер)
```

Overlord может роутить по имени сервера или по контексту запроса (расширение файла).

---

### ❓ Префикс с названием MCP инструмента важен?

**❌ Нет, не важен.** Префикс `UltrasharpTool_` был добавлен для различия в ранних версиях, но:

1. MCP Protocol рекомендует использовать **короткие snake_case имена** без префиксов
2. Namespace определяется **именем сервера**, а не префиксом инструмента
3. Пример из официальных MCP серверов:
   ```typescript
   // ❌ Плохо (старый стиль):
   "DatabaseServer_ExecuteQuery"

   // ✅ Хорошо (MCP Protocol style):
   "execute_query"
   ```

**Рекомендация:** Убрать все префиксы `UltrasharpTool_` из C# проекта.

---

### ❓ Что лучше поправить - у нас или у них?

**Рекомендация:**

1. **UltrasharpTools (36 инструментов)**:
   - ✅ Переименовать **все 36 инструментов** в snake_case (убрать префикс)
   - ✅ Добавить **8 новых инструментов** для паритета с TS (branch management, validation)
   - ⚠️ Сложность: **Средняя** (breaking change, нужно обновить все вызовы)

2. **UltrascriptTools (42 инструмента)**:
   - ✅ Добавить **4 alias'а** для совместимости (get_members, find_duplicates, modify_code, undo)
   - ✅ Добавить **3 новых инструмента** для паритета с C# (create_file, rename_symbol, add_member)
   - ⚠️ Сложность: **Низкая** (обратно совместимо, alias не ломает API)

**Итог:** Начать с UltrascriptTools (легче), затем UltrasharpTools (breaking change требует major version bump).

---

## 7. Roadmap

### Квартал 1 (Q1 2025)

**Месяц 1:**
- ✅ UltrascriptTools: добавить 4 alias'а (get_members, find_duplicates, modify_code, undo)
- ✅ Документация: обновить все примеры с новыми именами
- ✅ Тесты: добавить интеграционные тесты для alias'ов

**Месяц 2:**
- ✅ UltrasharpTools v2.0.0: переименовать все 36 инструментов в snake_case
- ✅ Backward compatibility: поддержка старых имен через [Obsolete] (3 месяца)
- ✅ Документация: миграционный гид для пользователей

**Месяц 3:**
- ✅ UltrasharpTools: добавить branch management инструменты (5 новых)
- ✅ UltrascriptTools: добавить create_file, rename_symbol, add_member (3 новых)
- ✅ Интеграционные тесты: покрытие для новых инструментов

### Квартал 2 (Q2 2025)

**Месяц 4:**
- ✅ Overlord v1.0.0: реализация интеллектуальной маршрутизации
- ✅ Overlord: поддержка fallback между серверами
- ✅ Overlord: агрегация результатов для кросс-языковых проектов

**Месяц 5:**
- ✅ UltrasharpTools: удалить устаревшие имена инструментов (breaking change)
- ✅ UltrascriptTools: пометить старые имена как deprecated
- ✅ Документация: финальное обновление

**Месяц 6:**
- ✅ Производственное тестирование на реальных проектах
- ✅ Сбор feedback от пользователей
- ✅ Оптимизация производительности Overlord

### Квартал 3 (Q3 2025)

**Месяц 7-9:**
- ✅ UltrasharpTools: добавить semantic search инструменты (опционально)
- ✅ UltrascriptTools: добавить trace execution инструменты (опционально)
- ✅ Overlord: кросс-языковой semantic search (C# + TS вместе)

---

## 8. Заключение

### Ключевые Рекомендации

1. ✅ **Унифицировать 27 общих инструментов** по схеме snake_case без префиксов
2. ✅ **Сохранить специфичные инструменты** (C#: Roslyn-зависимые, TS: agent-based)
3. ✅ **Реализовать Overlord маршрутизацию** по расширению файла
4. ✅ **Постепенная миграция**: сначала TS (alias'ы), потом C# (переименование)

### Метрики Успеха

- 📊 **95% инструментов** имеют консистентные имена
- 📊 **100% backward compatibility** в течение 3 месяцев
- 📊 **Overlord маршрутизация** работает с точностью >99%
- 📊 **Снижение ошибок** при вызове инструментов на 40%

### Следующие Шаги

1. ✅ Утвердить этот документ с командой
2. ✅ Создать GitHub Issues для каждого этапа миграции
3. ✅ Начать реализацию с UltrascriptTools alias'ов (низкий риск)
4. ✅ Подготовить миграционный гид для пользователей UltrasharpTools v2.0

---

**Документ подготовлен:** 2025-11-17
**Автор:** Claude (Anthropic) + Ultrathink Analysis
**Версия:** 1.0
**Статус:** 🟢 Ready for Review

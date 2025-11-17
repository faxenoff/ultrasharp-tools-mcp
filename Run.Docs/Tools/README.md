# MCP Tools Documentation

Документация по всем MCP инструментам UltrasharpTools.

## 📋 Категории инструментов

### [Solution.md](Solution.md) - Управление решением
**2 инструмента**

Загрузка и навигация по C# решениям:
- `load_solution` - загрузка .sln файла
- `load_project` - детальная структура проекта

**С чего начать:** Всегда начинайте с LoadSolution!

---

### [Analysis.md](Analysis.md) - Анализ кода
**12 инструментов**

Навигация и анализ кодовой базы:
- Поиск символов, референсов, имплементаций
- Анализ наследования и call graphs
- Complexity analysis
- Semantic similarity detection

**Основные use cases:**
- Понять структуру незнакомой кодовой базы
- Найти все использования метода/класса
- Анализ зависимостей и coupling

---

### [Modification.md](Modification.md) - Модификация кода
**8 инструментов**

Точные изменения кода через Roslyn:
- Добавление/изменение членов классов
- Переименование с обновлением всех референсов
- Find & Replace с regex
- Move members между типами

**Git интеграция:**
- Автоматические коммиты в ветки `ultrasharptools/*`
- Undo через Git для отката последнего изменения

---

### [Quality.md](Quality.md) - Качество кода
**3 инструмента**

Автоматическое улучшение качества кода:
- `format_code` - форматирование через CSharpier
- `analyze_code_style` - Roslyn analyzers
- `apply_code_fixes` - автоисправление проблем

**Типичный workflow:**
1. AnalyzeCodeStyle - найти проблемы
2. apply_code_fixes - исправить автоматически
3. FormatCode - привести к единому стилю

---

### [Tracing.md](Tracing.md) - Debugging & Diagnostics
**5 инструментов**

Статический дебаггинг без выполнения кода:
- `trace_execution` - CFG-based трейсинг выполнения
- `trace_backwards` - обратный трейсинг от краша
- `analyze_path_feasibility` - символьное выполнение (Z3)
- `export_call_graph` - визуализация графа вызовов
- `analyze_logs` - анализ лог-файлов

**Продвинутые возможности:**
- Persistent SQLite caching (5-10x speedup)
- Stack trace hints для точного матчинга
- Automatic log format detection

---

### [LogAnalysis.md](LogAnalysis.md) - Анализ логов
**1 инструмент**

Умный анализ логов с автоопределением формата:
- Поддержка ECS/JSON, PlainText, Logcat, WebServer, XML
- Поиск по keywords, log levels, status codes
- Pagination для больших файлов

---

### [Document.md](Document.md) - Файловые операции
**4 инструмента**

Чтение и модификация файлов:
- Чтение без отступов (экономия токенов)
- Создание новых файлов
- Перезапись существующих
- Список типов в файле

---

## 🎯 Quick Start Workflows

### Workflow 1: Изучение незнакомой кодовой базы
```
1. LoadSolution - загрузить .sln
2. load_project - получить структуру проекта
3. GetMembers - изучить методы интересующего класса
4. ViewDefinition - посмотреть реализацию
5. FindReferences - где используется?
```

### Workflow 2: Рефакторинг
```
1. analyze_complexity - найти сложные методы
2. FindPotentialDuplicates - найти дубликаты
3. modify_code - упростить/изменить код
4. FormatCode - привести к стандарту
5. AnalyzeCodeStyle - проверить качество
```

### Workflow 3: Debugging краша
```
1. AnalyzeLogs - проанализировать лог с крашем
2. TraceBackwards - найти все пути к месту краша
3. TraceExecution - понять flow выполнения
4. analyze_path_feasibility - проверить feasibility
5. ExportCallGraph - визуализировать граф
```

## 📊 Статистика

| Категория | Инструментов | Описание |
|-----------|--------------|----------|
| Solution | 2 | Загрузка и навигация |
| Analysis | 12 | Поиск и анализ |
| Modification | 8 | Изменение кода |
| Quality | 3 | Улучшение качества |
| Tracing | 5 | Debugging & diagnostics |
| LogAnalysis | 1 | Анализ логов |
| Document | 4 | Файловые операции |
| **TOTAL** | **35+** | |

## 🔗 См. также

- [../Configuration/MCP_Sharp.md](../Configuration/MCP_Sharp.md) - полное руководство
- [../Setup/](../Setup/) - инструкции по развертыванию
- [../../README.md](../../README.md) - главный README проекта

```
        ██  ██
        ██  ██  ██    ██████ █████▄  ▄████▄
        ██  ██  ██      ██   ██▄▄██▄ ██▄▄██
        ██  ██  ██      ██   ██   ██ ██  ██
        ██  ██  ██████  ██   ██   ██ ██  ██
        ▀████▀           ▄▄▄▄ ▄▄ ▄▄  ▄▄▄  ▄▄▄▄  ▄▄▄▄
                        ███▄▄ ██▄██ ██▀██ ██▄█▄ ██▄█▀
                        ▄▄██▀ ██ ██ ██▀██ ██ ██ ██
     ╔═════════════════════════════════════════════════════╗
     ║            ULTRASHARP-TOOLS MCP SERVER              ║
     ╚═════════════════════════════════════════════════════╝
```


[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Roslyn](https://img.shields.io/badge/Powered%20by-Roslyn-blue)](https://github.com/dotnet/roslyn)

**Умный инструмент работы с C# кодом для вас и вашего ИИ **

Если вы активно используете ИИ для написания кода, то наверняка сталкивались с тем, что ИИ тратит много времени и токенов в попытках полнотекстового поиска и редактирования. Если надо поменять много кода, то ИИ пишет скрипты под каждую задачу, которые запускаются примерно после десятой правки и хорошо если ничего не сломают.

Ultrasharp Tools - это инструмент для вашего инструмента (ИИ), который решит все эти проблемы.

- Весь код парсится в готовую связанную структуру и работа всех инструментов идёт именно с ней, а не с исходным кодом.
- Молниеносный парсинг структуры - 500К строк за 10 сек, 6М строк кода за 50 сек. Потом все из кеша за 0.02 сек.
- Структура кешируется, инкрементно обновляется, разбита на слои для каждой git-ветки. При переключении веток или перезапуске - вся структура сразу готова.
- Точно так же строится/кешируется векторная база для взаимодействия с локальными embed-моделями (Ollama/TEI).
- Инструменты поиска/анализа/модификации сразу возвращают/меняют нужные части кода без лишнего поиска.
- При изменениях сразу происходит статический анализ/линтинг/форматирование изменённого кода и если есть что новое - оно сразу приходит в сообщении о завершении модификации.
- Команды инструментам можно давать в простом виде: "Добавь новый метод в этот класс", "Найди где используется этот сервис", "Покажи что сломается если изменить этот интерфейс" — локальная семантическая модель сама всё разберёт и запустит что нужно, сколько нужно и вернёт результат.
- При использовании инструментов семантической замены или мержа - модель сама разберется с деталями на месте (которые не совпадают по структуре). Если нет уверенности - выдумывать не станет, сообщит честно. Можно за секунды поменять 500+ точек логирования на новый формат с умным реформатированием по смыслу.
- Есть инструменты "статической трассировки" выполнения - задаёте точку старта и завершения, инструмент пройдёт по ней и вернет последовательность что там происходило, а ИИ уже по ней разберётся. Или наоборот, у вас есть точка падения и вы ищите причины - инструмент "отмотает назад" и вернёт ИИ что там было.
- Ещё очень выручает semantic_merge, который на трёх уровнях разбирается со сложными мержами сам.
- Архитектура разделена на три части: Comm (маленький процесс-мост, запускается у каждого агента), Droid (основной процесс), VectorDB (работа с семантикой). При любом количестве агентов у вас всегда один основной процесс работает, нет дублирования и лишнего потребления ресурсов. Экономия до 10Gb RAM.


---
Для работы с JS/TS/Python - используйте аналогичный [ultrascript-tools-mcp](https://github.com/faxenoff/ultrascript-tools-mcp)

---

## 📦 **Установка**

1. Скачайте релиз на [GitHub Releases](https://github.com/yourusername/ultrasharp-tools-mcp/releases) и распакуйте.
2. Настройте ИИ (Claude Code / Claude Desktop):
```json
// Добавить в ~/.claude.json (Claude Code) или claude_desktop_config.json (Desktop)
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "C:\\Tools\\UltrasharpTools\\Comm\\UltrasharpTools.Comm.exe"
    }
  }
}
```

> **Примечание:** Запускается именно **Comm.exe** — лёгкий stdio-bridge (~5 MB).
> Comm автоматически запустит Droid (Roslyn сервер) и VectorDB (semantic индексер) при первом подключении.
3. Добавить в CLAUDE.md описание работы с инструментами
4. Настройте семантический поиск -  запустите мастер настройки и следуйте его инструкциям:

**Windows:**
```cmd
C:\Tools\UltrasharpTools\Scripts\setup-semantic-embedding.cmd
```

**Linux/macOS:**
```bash
PATH/Tools/UltrasharpTools/Scripts/setup-semantic-embedding.sh
```
В зависимости от опредёленного GPU - будет предложено выбрать систему Ollama/TEI-в-docker. Потом выбрать наиболее подходящую вам embedding модель.

Специально для GTX50xx (Blackwood) используется неофициальная TEI, которая поддерживает такую архитектуру.

---

## 💡 **Что это даёт вам?**

### Работайте с C# кодом как человек-разработчик

Вместо простых текстовых операций, AI получает те же инструменты что и вы:

**Примеры команд:**
- "Добавь метод ValidateEmail в класс UserService"
- "Покажи все места где используется PaymentProcessor"
- "Переименуй метод GetUser → FetchUserAsync и обнови все вызовы"
- "Что сломается если я изменю интерфейс IAuthService?"
- "Найди дубликаты этого метода в проекте"

**Результат:** точные изменения + автоформатирование + проверка ошибок + git commit.

### Экономьте время на рутине

| Задача | Обычный AI (текст) | С UltrasharpTools (Roslyn) |
|--------|-------------------|---------------------------|
| Добавить метод в класс | Часто ломает синтаксис, не видит using'и | **Точное добавление** в нужное место с корректными using'ами |
| Переименовать символ | Находит только текст, пропускает вызовы | **100% замена** всех ссылок в solution через SymbolFinder |
| Найти использование | grep по коду, много false positives | **Semantic Find References** - только реальные вызовы |
| Проверить на ошибки | "Запусти dotnet build" | **Мгновенная компиляция** в памяти после каждого изменения |

### Умный анализ через Roslyn, а не текст

**Текстовые операции AI:**
```
AI: "Добавляю метод в конец файла..."
Результат: ❌ Метод внутри другого метода
           ❌ Отсутствуют using'и
           ❌ Не скомпилируется
```

**UltrasharpTools с Roslyn:**
```
AI использует add_member:
✅ Метод добавлен в правильное место класса
✅ Автоматически добавлены нужные using System.Linq
✅ Код отформатирован через CSharpier
✅ 0 ошибок компиляции
✅ Git commit создан автоматически
```

Потому что **понимает структуру кода**, а не просто текст.

---

## 🚀 **Реальные примеры использования**

### 1. Добавление нового функционала

**Проблема**: Нужно добавить валидацию email во все user-сервисы.

**Решение**:
```
Вы: "Добавь метод ValidateEmail в класс UserService с проверкой на regex"

UltrasharpTools:
✅ Метод добавлен в Services/UserService.cs
✅ Автоматически добавлен using System.Text.RegularExpressions
✅ Код отформатирован через CSharpier
✅ Компиляция успешна (0 ошибок)
✅ Git commit: "feat: Add email validation to UserService"

Время: 2 секунды вместо 5 минут ручного редактирования
```

### 2. Масштабный рефакторинг

**Проблема**: Нужно переименовать IUserRepository → IUserDataAccess во всём solution (120 файлов).

**Решение**:
```
Вы: "Переименуй IUserRepository в IUserDataAccess везде в solution"

UltrasharpTools:
✅ Найдено 347 использований в 23 файлах
✅ Обновлены:
  - Определение интерфейса
  - Все реализации (UserRepository, TestUserRepository)
  - Все DI регистрации
  - Все параметры конструкторов
  - Все XML документации
✅ Компиляция успешна
✅ Git commit: "refactor: Rename IUserRepository to IUserDataAccess"

Без ошибок, без пропущенных мест. Время: 8 секунд.
```

### 3. Анализ влияния изменений

**Проблема**: Нужно изменить сигнатуру метода ProcessPayment, но не понятно что сломается.

**Решение**:
```
Вы: "Покажи где используется PaymentService.ProcessPayment"

UltrasharpTools:
✅ Найдены все вызовы:
  1. OrderController.Checkout() - прямой вызов
     File: Controllers/OrderController.cs:45
     Code: await _paymentService.ProcessPayment(order.Total)

  2. SubscriptionService.RenewSubscription() - через DI
     File: Services/SubscriptionService.cs:78

  3. PaymentQueueWorker.ProcessAsync() - background job
     File: Workers/PaymentQueueWorker.cs:112

Затронуто: 3 компонента, 3 файла
Рекомендация: добавить перегрузку метода для обратной совместимости
```

---

## ⚡ **Насколько это быстро?**

**Производительность на реальных проектах:**

| Операция | Результат | Детали |
|----------|-----------|--------|
| **Индексация 500К символов** | **10 секунд** | Cold start с Type Dictionary Cache |
| **Индексация с кешем** | **< 9 секунд** | Cache hit (5.4x быстрее) |
| **Поиск символа** | **< 100 миллисекунд** | Bloom filter + FastSymbolIndex |
| **Переключение Git ветки** | **< 20 мс** | Layered index (base + deltas) |
| **Компиляция solution** | **В памяти** | Мгновенная проверка ошибок |

### Ожидаемое время загрузки по размеру проекта

| Размер проекта | Проектов | Время загрузки | Примечание |
|----------------|----------|----------------|------------|
| **~100K LOC** | 10-20 | **5 сек** | Типичный микросервис |
| **~500K LOC** | 50-100 | **10 сек** | Средний enterprise проект |
| **~1M LOC** | 100-200 | **20 сек** | Крупный монолит |
| **~2-3M LOC** | 400-600 | **40 сек** | ASP.NET Core (1504 проекта*) |
| **~4-5M LOC** | 250-400 | **60 сек** | Roslyn compiler (663 проекта*) |


---

## 🎯 **Главные возможности**

### Точные модификации через Roslyn
"Добавь метод Calculate в класс MathService" → добавляется в правильное место с using'ами

### Semantic Find & Replace
Находит все **реальные** использования символа через SymbolFinder, не просто текст

### Автоматический Git workflow
Каждое изменение → автоматический commit с описанием изменений

### Quality Tools из коробки
CSharpier форматирование + Roslyn analyzers + автофиксы прямо в процессе

### Advanced Tracing (статическая отладка)
- **TraceExecution**: путь выполнения через Control Flow Graph
- **TraceBackwards**: обратная трассировка от точки краша
- **SymbolicExecution**: Z3 SMT solver для проверки path feasibility

### Semantic Merge (умное слияние)
3-way merge с пониманием структуры кода, обнаружением движения и переименований

### Поддержка любых .NET проектов
.NET Framework, Core, 5+, legacy csproj, SDK-style, respects .editorconfig

---

## 🔌 **Режимы работы**

UltrasharpTools предоставляет два режима работы под разные сценарии.

### 📍 Droid - Локальный режим (stdio)

**Основной режим для разработчиков**

**Архитектура (три процесса):**
```
┌─────────────────────────────────────────────────────────────────────────┐
│  Claude Desktop    Claude Code    Cursor    VS Code + Continue.dev     │
│       ↓                ↓            ↓                ↓                  │
│    Comm.exe         Comm.exe     Comm.exe        Comm.exe              │
│  (stdio bridge)   (stdio bridge)   ...            ...                  │
└────────┬───────────────┬────────────┬──────────────┬────────────────────┘
         │               │            │              │
         └───────────────┴─────┬──────┴──────────────┘
                               │ Named Pipes (IPC)
                               ↓
┌─────────────────────────────────────────────────────────────┐
│  UltrasharpTools.Droid.exe  (singleton, auto-start)         │
│  ├─ MCP Server (многоклиентный)                             │
│  ├─ Roslyn Workspace (анализ и модификация кода)            │
│  ├─ Git Integration (автокоммиты)                           │
│  └─ VectorDB Client ──────┐                                 │
└───────────────────────────│─────────────────────────────────┘
                            │ Named Pipes (IPC)
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  UltrasharpTools.VectorDB.exe  (singleton, lazy start)      │
│  ├─ Semantic Index (sqlite-vec / vectorlite)                │
│  ├─ Embedding Generator (TEI/Ollama)                        │
│  └─ Power Management (Efficiency Mode в idle)               │
└─────────────────────────────────────────────────────────────┘
    ↓
Ваши C# проекты на диске
```

**Почему три процесса?**

| Процесс | Роль | Lifecycle |
|---------|------|-----------|
| **Comm** | Лёгкий stdio-bridge (~5 MB) | Один на каждый AI-агент/редактор |
| **Droid** | Roslyn workspace, Git, MCP tools | Singleton — один на все Comm |
| **VectorDB** | Semantic index, embeddings | Singleton — lazy start по требованию |

| Преимущество | Описание |
|--------------|----------|
| **Общие ресурсы** | 5 редакторов используют ОДИН Droid+VectorDB вместо 5 копий (экономия 2-3 GB RAM) |
| **Общий кеш** | Roslyn compilation cache, symbol index — загружаются один раз |
| **Изоляция памяти** | VectorDB 500MB+ не влияет на Droid, Comm минималистичен |
| **Независимый перезапуск** | VectorDB перезапускается без потери Droid сессии |
| **Энергосбережение** | VectorDB → Efficiency Mode после 3 минут простоя |
| **Ленивый запуск** | VectorDB стартует только при первом semantic запросе |

**Конфигурация (`~/.claude.json`):**
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "type": "stdio",
      "command": "D:/path/to/Run.Publish/Comm/UltrasharpTools.Comm.exe",
      "args": [],
      "env": {}
    }
  }
}
```

> ⚠️ **Важно:** Запускается **Comm.exe**, а не Droid.exe! Comm автоматически запустит Droid при первом подключении.

**Возможности:**
- ✅ **Много агентов — один сервер** — Claude Desktop + Claude Code + Cursor одновременно
- ✅ **Общий Roslyn workspace** — все редакторы видят одни и те же изменения
- ✅ **Локальный Git** — автоматические коммиты в `sharptools/*` ветки
- ✅ **Максимальная скорость** — нет сетевых задержек
- ✅ **NuGet packages** из локального кэша (`~/.nuget/packages/`)
- ✅ **Локальная семантика** — semantic search через Ollama/TEI (VectorDB)
- ✅ **Auto-recovery** — Droid и VectorDB перезапускаются при сбоях

**Use case:** Индивидуальная разработка с несколькими AI-агентами/редакторами одновременно

---

### 🌐 Overlord - Semantic Hub для команды (в тестировании)

> ⚠️ **Статус:** В активном тестировании. API может меняться.

**Централизованный сервер для semantic обработки кода**

**Архитектура:**
```
Droid (на машине разработчика)
    ↓ работает с локальными файлами
    ↓ создаёт code embeddings
    ↓ отправляет векторы через HTTP
        ↓
    Overlord (Docker/Kubernetes)
        ↓ принимает векторы от всех Droid клиентов
        ↓ хранит в едином MultiProjectVectorStore
        ↓ использует мощную embedding модель (Ollama/TEI)
        ↓ выполняет cross-project semantic search
        ↓ возвращает результаты с рекомендациями
```

**Ключевая особенность:** Overlord **НЕ работает с файлами напрямую**!
Каждый Droid работает со своими локальными проектами, а Overlord:
- Агрегирует векторы от всех разработчиков
- Предоставляет единую точку доступа к мощной embedding модели
- Выполняет поиск по всем проектам команды одновременно

**Deployment через Docker:**
```bash
docker run -d \
  --name ultrasharp-overlord \
  -p 3001:3001 \
  -v ultrasharp-vectors:/app/data \
  -e EMBEDDING_URL=http://ollama:11434 \
  -e EMBEDDING_MODEL=granite-embedding \
  ghcr.io/faxenoff/ultrasharp-tools-overlord:latest
```

**Подключение Droid к Overlord:**
```bash
# Ваш Droid работает с локальными файлами
# + использует Overlord для semantic операций
UltrasharpTools.Droid.exe \
  --overlord-url http://overlord:3001 \
  --load-solution D:/MyProject/MyApp.sln
```

**Что получаете:**
- 🧠 **Shared semantic brain** - единая база знаний о коде всей команды
- 🔍 **Cross-project search** - "найди дубликаты во ВСЕХ проектах команды"
- 💪 **Powerful embedding** - дорогая модель на GPU доступна всем
- 📊 **Smart recommendations** - "в проекте TeamA уже есть такая реализация!"
- ⚡ **No local GPU needed** - embedding выполняется на сервере
- 🔄 **Auto-enrichment** - все инструменты автоматически обогащаются semantic данными

**Use case:**
- Команды разработчиков с несколькими проектами
- Поиск дубликатов кода между микросервисами
- Централизованный доступ к мощной embedding модели
- Knowledge sharing через semantic recommendations

**📖 Подробнее:** [Run.Docs/OVERLORD_README.md](Run.Docs/OVERLORD_README.md) | [Deployment Guide](Run.Docs/Deployment/README.md)

---

## 🎨 **Полный список инструментов (52 tools)**

> **Легенда**: 🔷 = требует Semantic Mode (проверьте `get_capabilities()`)

### 🔷 Solution Management (2)
| Инструмент | Что делает |
|------------|------------|
| `load_solution` | Загружает .sln и инициализирует Roslyn workspace |
| `load_project` | Детальный обзор структуры проекта (namespaces, types) |

### 🔍 Analysis Tools (15)
| Инструмент | Что делает | Semantic |
|------------|------------|:--------:|
| `get_members` | Список членов типа с сигнатурами и XML docs | - |
| `view_definition` | Показывает source code символа с контекстом | - |
| `list_implementations` | Находит реализации интерфейса/производные классы | - |
| `find_references` | Все использования символа в solution | - |
| `search_definitions` | Regex поиск по декларациям в коде и assemblies | - |
| `view_call_graph` | Incoming/outgoing вызовы метода | - |
| `view_inheritance_chain` | Цепочка наследования типа | - |
| `get_all_subtypes` | Рекурсивный список вложенных членов | - |
| `manage_usings` | Чтение/запись using директив | - |
| `manage_attributes` | Чтение/запись атрибутов на декларациях | - |
| `analyze_complexity` | Метрики сложности (cyclomatic, cognitive, coupling) | - |
| `pattern_search` | 4 режима поиска: entity, content, semantic, hybrid | * |
| `find_duplicates` | Семантический поиск похожего кода |  |
| `detect_technology_stack` | Определяет frameworks, languages, dependencies | - |
| `list_file_entities` | Список types и members в файле | - |

> \* `pattern_search` работает без semantic в режимах `entity` и `content`

### ✏️ Modification Tools (8)
| Инструмент | Что делает |
|------------|------------|
| `add_member` | Добавляет member (method/property/field/class) в тип |
| `modify_code` | Заменяет или удаляет member definition |
| `rename_symbol` | Переименовывает символ + все references в solution |
| `replace_all_references` | Заменяет все ссылки на символ указанным кодом |
| `replace_references_by_pattern` | Batch переименование по паттерну (wildcards/regex) |
| `find_and_replace` | Regex find & replace в коде/файлах |
| `move_member` | Перемещает member между типами/namespaces |
| `undo` | Откатывает последнее изменение через Git |

### ✨ Quality Tools (4)
| Инструмент | Что делает |
|------------|------------|
| `format_code` | Форматирование через CSharpier (.cs, .csproj, .xml) |
| `analyze_code_style` | Анализ через Roslyn analyzers (presets, filters, 5-min cache) |
| `apply_code_fixes` | Автоприменение code fixes (unused usings и др.) |
| `cleanup_usings` | Удаляет usings дублирующие GlobalUsings.cs |

### ✅ Validation Tools (3)
| Инструмент | Что делает |
|------------|------------|
| `validate_file` | Валидация C# файла с Roslyn analyzers |
| `validate_directory` | Batch валидация директории (параллельно) |
| `compare_validation` | Сравнение результатов до/после изменений |

### 🐛 Debugging & Tracing (5)
| Инструмент | Что делает |
|------------|------------|
| `trace_execution` | Статическая трассировка через Control Flow Graph |
| `trace_backwards` | Обратная трассировка от точки краша (с кешем 5-10x) |
| `analyze_path_feasibility` | Symbolic execution с Z3 solver (null checks, div by zero) |
| `export_call_graph` | Экспорт графа (DOT/Mermaid/GraphML) |
| `analyze_logs` | Анализ логов (ECS/JSON, PlainText, Logcat, XML) |

### 📄 Document Tools (3)
| Инструмент | Что делает |
|------------|------------|
| `read_file` | Читает файл (без indentation для экономии токенов) |
| `create_file` | Создаёт новый файл с контентом |
| `overwrite_file` | Перезаписывает существующий файл |

### 📁 File Operations (2)
| Инструмент | Что делает |
|------------|------------|
| `split_file` | Разбивает файл по top-level типам (класс → файл) |
| `synthesize_files` | Объединяет несколько файлов в один |

### 🧠 Semantic Tools (8) — требуют Semantic Mode
| Инструмент | Что делает |
|------------|------------|
| `semantic_search` | Поиск кода по смыслу (natural language) |
| `semantic_diff` | Сравнение semantic изменений (поведение vs текст) |
| `detect_code_clones` | Обнаружение дубликатов через ML |
| `semantic_replace` | Batch find & replace с контекстом (preview + apply) |
| `get_semantic_replace_info` | Справка по возможностям semantic_replace |
| `reindex_changed_files` | Инкрементальная переиндексация |
| `semantic_merge` | 3-way merge с пониманием структуры кода |
| `get_semantic_merge_info` | Справка по возможностям semantic_merge |

### 💾 Snapshot Tools (4)
| Инструмент | Что делает |
|------------|------------|
| `create_snapshot` | Создаёт точку восстановления (backup) |
| `list_snapshots` | Список доступных снимков |
| `rollback_snapshot` | Откат к предыдущему состоянию ⚠️ |
| `cleanup_snapshots` | Удаление старых снимков ⚠️ |

### 📦 Package & Misc (2)
| Инструмент | Что делает |
|------------|------------|
| `add_package` | Добавляет/обновляет NuGet пакет в проект |
| `request_new_tool` | Запрос новых инструментов (логируется для review) |

### ⚙️ System (1)
| Инструмент | Что делает |
|------------|------------|
| `get_capabilities` | Проверка возможностей сервера (semantic mode, версия, features) |

---

### 📊 Semantic vs Instrumental

**Работают всегда (без Semantic Mode):**
- Все Solution, Modification, Quality, Validation, Tracing, Document, File Ops, Snapshot, Package, System инструменты
- `pattern_search` в режимах `entity` и `content`

**Требуют Semantic Mode (проверка через `get_capabilities()`):**
- `semantic_search`, `semantic_diff`, `detect_code_clones`
- `semantic_replace`, `get_semantic_replace_info`
- `find_duplicates`, `reindex_changed_files`
- `semantic_merge`, `get_semantic_merge_info`
- `pattern_search` в режимах `semantic` и `hybrid`

**📖 Подробная документация**: [Run.Docs/Claude/](Run.Docs/Claude/) - примеры, best practices, workflows для каждого инструмента.

---

## 🔧 **Дополнительные возможности**

### Semantic Code Search

Для поиска похожего кода по смыслу (не по тексту) можно включить векторные embeddings.

**Что получите:**
- ✅ Поиск дубликатов даже с разными названиями переменных
- ✅ "Найди код похожий на этот метод" → семантический поиск
- ✅ Автоматическая группировка похожих методов/классов

**Два варианта setup:**

**1. Локальная семантика (Droid)**

Быстрая настройка через `setup-semantic-embedding.cmd`:

```bash
# Windows
Dev.Scripts\setup-semantic-embedding.cmd

# Linux/Mac
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

Выберите embedding provider:
- **Ollama** - простая установка, работает на CPU/GPU
- **TEI** - максимальная производительность, требует Docker + NVIDIA GPU
- **Memory** - для тестирования без внешних зависимостей

**Ollama quick start:**
```bash
# 1. Установите Ollama (https://ollama.ai)
ollama pull granite-embedding

# 2. Запустите setup
setup-semantic-embedding.cmd
# Выберите "Ollama"

# 3. Готово! Droid автоматически использует Ollama
```

**2. Централизованная семантика (Overlord)**

Для командной работы - shared semantic index на Overlord сервере.
Все разработчики используют единую базу векторов и embedding service.

**Адаптивные векторные бэкенды** (автоматическое переключение):
- **SqliteVec** (< 10K символов): Brute-force SIMD, 100% accuracy, fast indexing
- **Vectorlite HNSW** (> 10K символов): 3-100x быстрее поиск, 99.9%+ recall, масштабируется до 100K+ векторов
- **Auto-switching**: автоматический выбор оптимального backend по размеру базы

**📖 Подробнее:** [Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md](Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md)

### Layered Indexing для Git workflow (включено по умолчанию)

Автоматическое управление индексами при работе с Git ветками:

**Что происходит автоматически:**
- ✅ При `git checkout feature` → индекс переключается на feature
- ✅ При изменении файла → обновляется Working Delta (< 1ms)
- ✅ При `git commit` → Working Delta → Branch Delta
- ✅ Cleanup orphaned branches автоматически

**Производительность:**
- Переключение ветки: **16.6ms** вместо полной переиндексации
- Background compaction: **2.2s для 112 веток**
- SQLite persistence: **5.4x быстрее** холодного старта

Подробнее: [Dev.Docs/Development/LAYERED_INDEXING_DESIGN.md](Dev.Docs/Development/LAYERED_INDEXING_DESIGN.md)

---

### Конфигурация

**Command line options:**
```bash
# Droid (stdio)
--log-level <level>              # Trace|Debug|Information|Warning|Error|Critical
--load-solution <path>           # Автозагрузка .sln при старте
--build-configuration <config>   # Debug|Release
--disable-git                    # Отключить Git integration
--symbol-cache                   # Включить persistent cache (по умолчанию: true)
--symbol-cache-clear             # Очистить cache при старте
--low-memory                     # Режим экономии памяти (SQLite для reflection types)

# Overlord (HTTP/SSE)
--port <number>                  # HTTP порт (по умолчанию: 3001)
# + все опции Droid
```

**Layered Indexing** (настраивается в DI):
```csharp
services.WithLayeredIndexing(
    maxBranchDeltas: 50,              // Max веток в кеше (LRU eviction)
    enablePersistence: true,          // SQLite persistence
    deltaCompactionThreshold: 500     // Порог для compaction
);
```

**Symbol Cache** (10x faster startup):
```csharp
services.WithUltrasharpToolsServices(
    symbolCacheOptions: new SymbolCacheOptions {
        Enabled = true,               // Включить persistent cache
        ClearOnStartup = false,       // Очистка при старте
        CacheDirectory = null         // null = %TEMP%/UltrasharpTools/SymbolCache
    }
);
```

**Low Memory Mode** (экономия ~50-100MB):
```csharp
services.WithUltrasharpToolsServices(
    lowMemoryMode: true  // Использует SQLite для reflection types вместо FrozenDictionary
);
```

Или через CLI:
```bash
UltrasharpTools.Droid.exe --low-memory
```

**Оптимизации в режиме `--low-memory`:**
- SQLite FTS5 для reflection type search вместо in-memory FrozenDictionary
- LRU cache (500 типов) для горячих данных
- Lazy loading Type объектов через MetadataLoadContext
- Уменьшенные лимиты MemoryCache (150MB + 250MB вместо 500MB + 1GB)

</details>

---

## 🛠️ **Для разработчиков**

Инструкции по сборке из исходников, запуску тестов и разработке см. [Dev.Docs/CLAUDE.md](Dev.Docs/CLAUDE.md)


---

## 📄 **License**

MIT License - см. [LICENSE](LICENSE)

**Основан на**: [sharp-tools](https://github.com/tluyben/sharp-tools) by tluyben.
([Отличия от оригинального проекта](Dev.Docs/ULTRA-SHARPED.md))


**Links**: [GitHub](https://github.com/yourusername/ultrasharp-tools-mcp) • [Documentation](Run.Docs/) • [MCP Protocol](https://github.com/modelcontextprotocol)


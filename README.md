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

**Умный ассистент для работы с C# кодом**

Представьте, что вы можете просто сказать AI: "Добавь новый метод в этот класс", "Найди где используется этот сервис", "Покажи что сломается если изменить этот интерфейс" — и получить точные изменения за секунды, с автоматическим форматированием, проверкой на ошибки и Git коммитом.

UltrasharpTools делает именно это. Он даёт AI полный доступ к вашей C# кодовой базе через Roslyn — не просто как к тексту, а как к **реальному коду**, который можно анализировать, модифицировать и проверять на ошибки автоматически.

**🚀 35x быстрее** | **✨ 37 готовых инструментов** | **🔍 485К символов за 10.16 сек** | **⚡ < 20ms переключение веток**

---
Для работы с JS/TS/Python - используйте родственный проект [ultrascript-tools-mcp](https://github.com/faxenoff/ultrascript-tools-mcp)

## 📦 **Быстрая установка**

### Шаг 1: Скачать релиз

Перейдите на [GitHub Releases](https://github.com/yourusername/ultrasharp-tools-mcp/releases) и скачайте архив для вашей ОС:

- **Windows**: `UltrasharpTools-win-x64.zip`
- **Linux**: `UltrasharpTools-linux-x64.tar.gz`
- **macOS**: `UltrasharpTools-osx-x64.tar.gz`

### Шаг 2: Распаковать

**Windows:**
```cmd
# Распакуйте архив в удобное место, например:
C:\Tools\UltrasharpTools\
```

**Linux/macOS:**
```bash
# Создайте директорию и распакуйте
mkdir -p ~/Tools/UltrasharpTools
tar -xzf UltrasharpTools-linux-x64.tar.gz -C ~/Tools/UltrasharpTools
chmod +x ~/Tools/UltrasharpTools/UltrasharpTools.Droid
```

### Шаг 3: Настроить Claude Desktop

Откройте конфигурационный файл Claude Desktop:

**Windows**: `%USERPROFILE%\.claude\config.json`
**Linux/macOS**: `~/.claude/config.json`

Добавьте конфигурацию:

```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "C:\\Tools\\UltrasharpTools\\UltrasharpTools.Droid.exe",
      "args": ["--log-level", "Information"]
    }
  }
}
```

> **Linux/macOS**: Замените путь на `/home/username/Tools/UltrasharpTools/UltrasharpTools.Droid`

### Шаг 4: (Опционально) Настроить семантический поиск

Для умного поиска похожего кода запустите мастер настройки:

**Windows:**
```cmd
cd C:\Tools\UltrasharpTools
Scripts\setup-semantic-embedding.cmd
```

**Linux/macOS:**
```bash
cd ~/Tools/UltrasharpTools
pwsh Scripts/setup-semantic-embedding.ps1
```

Следуйте инструкциям мастера:
1. Выберите **Ollama** (проще всего) или **TEI** (для GPU)
2. Скрипт автоматически установит необходимые компоненты
3. Готово! Семантический поиск теперь доступен

> **Примечание**: Для работы скрипта нужен PowerShell 7+. Установите: https://aka.ms/powershell

### Готово! 🎉

Перезапустите Claude Desktop. Теперь можно работать:

```
Вы: "Загрузи solution D:/MyProject/MyApp.sln"
Claude: ✅ Solution loaded: 15 projects, 482K symbols

Вы: "Добавь метод SendEmail в EmailService"
Claude: ✅ Метод добавлен, отформатирован, git commit создан
```

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
| **Индексация 485К символов** | **10.16 секунд** | Cold start с Type Dictionary Cache |
| **Индексация с кешем** | **< 9 секунд** | Cache hit (5.4x быстрее) |
| **Поиск символа** | **< 100 миллисекунд** | Bloom filter + FastSymbolIndex |
| **Переключение Git ветки** | **< 20 мс** | Layered index (base + deltas) |
| **Компиляция solution** | **В памяти** | Мгновенная проверка ошибок |

**Почему так быстро?**

### Layered Indexing (Phase 7)
```
┌─────────────────────────────────────────┐
│ Base Layer (SQLite)                     │  ← Основной индекс (кеш)
│ 485К символов, загружается 1 раз        │     Загрузка: 10.16s cold / 8.9s warm
├─────────────────────────────────────────┤
│ Branch Deltas (по ветке)                │  ← Изменения в ветке
│ Только изменённые символы               │     Переключение: < 20ms
├─────────────────────────────────────────┤
│ Working Delta (незакоммиченное)         │  ← Текущие правки
│ Ваши изменения до git commit            │     Обновление: < 1ms
└─────────────────────────────────────────┘
```

**Результат**: при переключении веток не нужна полная переиндексация — только дельты!

**Ключевая оптимизация**: Type Dictionary Cache (FastSymbolIndex) — O(1) поиск типов вместо O(N) для каждого символа. Это дало **35x ускорение** (от 356 сек до 10.16 сек).

### Fast Symbol Index
- ✅ **Bloom Filter** - 99.9% false positive rate < 0.01%
- ✅ **Parallel processing** - Assembly loading в 4-5 потоков
- ✅ **SIMD optimizations** - xxHash32 вместо SHA256 (2-3x быстрее)
- ✅ **SQLite WAL mode** - параллельные read операции

**Пример**: solution на 890K символов (112 веток):
- Холодный старт: 48.3s
- С кешем: **8.9s** (5.4x быстрее)
- Поиск: **< 100ms**
- Переключение ветки: **16.6ms**

### Ожидаемое время загрузки по размеру проекта

| Размер проекта | Проектов | Время загрузки | Примечание |
|----------------|----------|----------------|------------|
| **~100K LOC** | 10-20 | **5-15 сек** | Типичный микросервис |
| **~500K LOC** | 50-100 | **20-40 сек** | Средний enterprise проект |
| **~1M LOC** | 100-200 | **40-80 сек** | Крупный монолит |
| **~2-3M LOC** | 400-600 | **60-90 сек** | ASP.NET Core (1504 проекта*) |
| **~4-5M LOC** | 250-400 | **45-60 сек** | Roslyn compiler (663 проекта*) |

\* Multi-target проекты (`net10.0;net462`) создают несколько проектов в Roslyn workspace

**Реальные тесты на крупных .slnx:**

| Solution | C# проектов | Загружено | Время | В workspace |
|----------|-------------|-----------|-------|-------------|
| **Roslyn.slnx** | 267 | 158 | **~49 сек** | 663 проекта |
| **aspnetcore.slnx** | 577 | 405 | **~67 сек** | 1504 проекта |

> **Примечание**: .slnx загружается параллельно (8 потоков, 60 сек таймаут на проект).
> VB.NET и F# проекты автоматически пропускаются.

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

**Как работает:**
```
Claude Desktop/Claude Code
    ↓ stdio процесс на вашей машине
UltrasharpTools.Droid.exe
    ↓ прямой доступ к файловой системе
Ваши C# проекты на диске
```

**Конфигурация (`~/.claude.json`):**
```json
{
  "Droids": {
    "ultrasharp-tools": {
      "type": "stdio",
      "command": "D:/path/to/Run.Publish/Droid/UltrasharpTools.Droid.exe",
      "args": ["--log-level", "Information"],
      "env": {}
    }
  }
}
```

**Возможности:**
- ✅ **Полный доступ** к файловой системе вашей машины
- ✅ **Локальный Git** - автоматические коммиты в `sharptools/*` ветки
- ✅ **Максимальная скорость** - нет сетевых задержек
- ✅ **NuGet packages** из локального кэша (`~/.nuget/packages/`)
- ✅ **Локальная семантика** (опционально) - semantic search через Ollama/TEI/Memory

**Use case:** Индивидуальная разработка, рефакторинг, отладка на локальной машине

---

### 🌐 Overlord - Semantic Hub для команды

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

### 📊 Сравнение режимов

| Аспект | Droid (автономный) | Droid + Overlord (гибридный) |
|--------|-------------------|------------------------------|
| **Работа с файлами** | ✅ Локально на вашей машине | ✅ Локально на вашей машине |
| **Semantic search** | Локальная модель (Ollama/TEI) | Централизованная мощная модель |
| **Vector store** | Локальный SQLite | Shared MultiProjectVectorStore |
| **Cross-project** | ❌ Только текущий проект | ✅ Поиск по всем проектам команды |
| **GPU requirements** | Желательно для TEI | Не нужно (на сервере) |
| **Recommendations** | Нет | ✅ "Есть похожий код в TeamProject" |
| **Setup сложность** | 🟢 Простой | 🟡 + Overlord deployment |
| **Использование** | Индивидуальная работа | Командная разработка |

---

### 🎯 Какой режим выбрать?

**Автономный Droid (без Overlord):**
- ✅ Работаете в одиночку
- ✅ Один проект за раз
- ✅ Хотите простую настройку
- ✅ Локальная semantic модель достаточна (Ollama на вашей машине)
- ✅ Не нужен поиск между проектами

**Droid + Overlord (гибридный режим):**
- ✅ Работаете в команде (2+ разработчика)
- ✅ Несколько проектов/микросервисов
- ✅ Нужен cross-project поиск дубликатов
- ✅ Хотите мощную embedding модель на GPU без локального GPU
- ✅ Важны smart recommendations ("похожий код в другом проекте")
- ✅ Team knowledge sharing

**Типичный сценарий:**
1. Начните с **автономного Droid** - быстрый старт, всё локально
2. Когда вырастет команда → добавьте **Overlord** для semantic синхронизации
3. Каждый разработчик работает со своими файлами + получает знания от всей команды

---

## 🎨 **Полный список инструментов (50 tools)**

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
| `pattern_search` | 4 режима поиска: entity, content, semantic, hybrid | 🔷* |
| `find_duplicates` | Семантический поиск похожего кода | 🔷 |
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

### 🧠 Semantic Tools (6) — требуют Semantic Mode 🔷
| Инструмент | Что делает |
|------------|------------|
| `semantic_search` | Поиск кода по смыслу (natural language) |
| `semantic_diff` | Сравнение semantic изменений (поведение vs текст) |
| `detect_code_clones` | Обнаружение дубликатов через ML |
| `reindex_changed_files` | Инкрементальная переиндексация |
| `SemanticMerge` | 3-way merge с пониманием структуры кода |
| `GetSemanticMergeInfo` | Статистика индексации для merge |

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
- `find_duplicates`, `reindex_changed_files`
- `SemanticMerge`, `GetSemanticMergeInfo`
- `pattern_search` в режимах `semantic` и `hybrid`

**📖 Подробная документация**: [Run.Docs/Claude/](Run.Docs/Claude/) - примеры, best practices, workflows для каждого инструмента.

---

## 🔧 **Дополнительные возможности**

### Semantic Code Search (опционально)

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
- **Ollama** (рекомендуется) - простая установка, работает на CPU/GPU
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

## 📊 **Что внутри (для технарей)**

<details>
<summary>Технические детали архитектуры</summary>

### Архитектура

**3-проектная структура** для разделения ответственности:

```
UltrasharpTools.Tools (Class Library)
├─ Вся бизнес-логика (Roslyn, Git, Analysis)
├─ Все MCP tool implementations
├─ Все сервисы (SolutionManager, CodeModificationService)
└─ Dependencies: Roslyn 5.0, LibGit2Sharp, NuGet.Protocol

         ↑                           ↑
         │                           │
         │                           │

Droid (Console)         Overlord (Web)
├─ Stdio transport          ├─ HTTP/SSE transport
├─ For: Claude Code         ├─ For: Remote access
└─ Output: exe + deps       └─ Output: exe + deps
```

### Performance Optimizations

**Layered Symbol Index:**
- **Base Layer**: SQLite cache (full index, загружается 1 раз)
- **Branch Delta**: Изменения относительно base для каждой ветки
- **Working Delta**: Незакоммиченные изменения

**Fast Operations:**
- Bloom Filter: O(1) проверка существования (false positive < 0.01%)
- SIMD xxHash32: 2-3x быстрее чем SHA256
- Parallel assembly loading: 4-5x speedup
- SQLite prepared statements caching: +25-30% batch operations

**Call Graph Caching:**
- SQLite persistence для TraceBackwards
- 5-10x speedup на warm cache
- 80-95% hit rate на реальных проектах

**Git-Aware Layered Index:**
- Branch switching: **< 20ms** (вместо полной переиндексации)
- Incremental updates: только измененные файлы
- Automatic cleanup: LRU eviction для старых веток

### Системные требования

**Минимум**:
- .NET 10 SDK
- 4GB RAM
- Dual-core CPU

**Рекомендуется**:
- .NET 10 SDK
- 16GB RAM (для больших solutions 890K+ символов)
- Quad-core CPU
- SSD

### Performance Benchmarks

**Реальный проект: 890K символов, 112 ветки**

| Операция | Время | Детали |
|----------|-------|--------|
| Cold start (первая загрузка) | 48.3s | Полная индексация + Roslyn compilation |
| Warm start (с кешем) | **8.9s** | 5.4x быстрее, только загрузка из SQLite |
| Symbol search | **< 100ms** | Bloom filter + indexed lookup |
| Branch switch | **16.6ms** | Только delta применяется |
| Background cleanup (112 веток) | 2.2s | Compaction + orphan cleanup |

**Call Graph Tracing (TraceBackwards)**
| Метрика | Значение |
|---------|----------|
| Cache hit rate | 80-95% |
| Speedup (warm cache) | 5-10x |
| Storage | SQLite (< 10MB для 890K symbols) |

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

## 📝 **Что нового?**

### v3.0.6 (2025-11-20) - Production Release

**Ключевые улучшения:**

**🚀 Performance (2-150x ускорение):**
- ⚡ Phase 7: Layered Indexing - Base + Branch Deltas + Working Deltas
  - Git-aware три-слойная архитектура
  - SQLite persistence (5.4x speedup: 48.3s → 8.9s)
  - Branch switching < 20ms
  - SIMD optimizations (4-8x для similarity calculations)
  - xxHash32 вместо SHA256 (2-3x faster hashing)
- ⚡ Fast Symbol Index - Bloom filters + битовые флаги (10-100x)
- ⚡ Parallel processing - Assembly loading (4-5x), NuGet resolution (3-4x)
- ⚡ SIMD vectorization - AVX2 для ComputeHashes (2.2-5.7x)
- ⚡ Dynamic PGO - адаптивная runtime оптимизация (+30-50%)
- ⚡ ReadyToRun (R2R) - AOT для 50% faster startup

**✨ Новые возможности:**
- Quality Tools - format_code (CSharpier), AnalyzeCodeStyle, ApplyCodeFixes
- Semantic Merge - умное 3-way слияние с movement/rename detection
- Advanced Tracing - trace_execution, TraceBackwards, AnalyzePathFeasibility (Z3)
- Auto-linting integration - автоматическая проверка после модификаций

**🏗️ Infrastructure:**
- .NET 10 + C# 13.0 с modern language features
- Docker + Kubernetes - production-ready deployment
- Helm Chart - гибкая конфигурация
- GitHub Actions CI/CD - автоматическая сборка
- Structured logging - Microsoft.Extensions.Logging

**📊 Production Validation:**
- 890K символов, 112 git branches
- 5.4x speedup с кешем
- < 100ms symbol search
- 16.6ms branch switching
- Memory trade-off: +596 MB → 10-100x faster operations

[Полная документация изменений](Dev.Docs/ULTRA-SHARPED.md)

---

## 🤝 **Contributing**

Приветствуются contributions!

1. Fork репозиторий
2. Создайте feature branch
3. Следуйте [Development Guidelines](Dev.Docs/Development/Normalization.md)
4. Submit pull request

**Development Docs**: [Dev.Docs/](Dev.Docs/) - архитектура, design decisions, implementation guides

---

## 📄 **License**

MIT License - см. [LICENSE](LICENSE)

**Основан на**: [sharp-tools](https://github.com/tluyben/sharp-tools) by tluyben

**Links**: [GitHub](https://github.com/yourusername/ultrasharp-tools-mcp) • [Documentation](Run.Docs/) • [MCP Protocol](https://github.com/modelcontextprotocol)


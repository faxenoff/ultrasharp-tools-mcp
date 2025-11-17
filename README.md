# UltrasharpTools MCP Server

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Roslyn](https://img.shields.io/badge/Powered%20by-Roslyn-blue)](https://github.com/dotnet/roslyn)

**Умный ассистент для работы с C# кодом**

Представьте, что вы можете просто сказать AI: "Добавь новый метод в этот класс", "Найди где используется этот сервис", "Покажи что сломается если изменить этот интерфейс" — и получить точные изменения за секунды, с автоматическим форматированием, проверкой на ошибки и Git коммитом.

UltrasharpTools делает именно это. Он даёт AI полный доступ к вашей C# кодовой базе через Roslyn — не просто как к тексту, а как к **реальному коду**, который можно анализировать, модифицировать и проверять на ошибки автоматически.

**🚀 2-150x быстрее** | **✨ 36 готовых инструментов** | **🔍 482К+ символов за 4.8 сек** | **⚡ < 20ms переключение веток**

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
AI использует AddMember:
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
| **Индексация 482К символов** | **4.8 секунды** | С кешем (первый раз 23.5s) |
| **Поиск символа** | **< 100 миллисекунд** | Bloom filter + SQLite cache |
| **Переключение Git ветки** | **16.6 мс** | Layered index (base + deltas) |
| **Компиляция solution** | **В памяти** | Мгновенная проверка ошибок |

**Почему так быстро?**

### Layered Indexing (Phase 7)
```
┌─────────────────────────────────────────┐
│ Base Layer (SQLite)                     │  ← Основной индекс (кеш)
│ 482К символов, загружается 1 раз        │     Загрузка: 4.8s
├─────────────────────────────────────────┤
│ Branch Deltas (по ветке)                │  ← Изменения в ветке
│ Только изменённые символы               │     Переключение: 16ms
├─────────────────────────────────────────┤
│ Working Delta (незакоммиченное)         │  ← Текущие правки
│ Ваши изменения до git commit            │     Обновление: < 1ms
└─────────────────────────────────────────┘
```

**Результат**: при переключении веток не нужна полная переиндексация — только дельты!

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

## 📦 **Быстрый старт**

### Сборка (2 минуты)

**Windows (быстрый способ):**
```cmd
publish-droid.cmd
```

**PowerShell:**
```powershell
.\Dev.Scripts\publish-mcp.ps1
```

**Linux/Mac:**
```bash
./Dev.Scripts/publish-mcp.sh
```

**Результат**: `Run.Publish/Droid/` - готовый к запуску сервер со всеми зависимостями.

### Интеграция с Claude Desktop

**Глобальная конфигурация** (`~/.claude.json` или `%USERPROFILE%\.claude.json`):
```json
{
  "Droids": {
    "ultrasharp-tools": {
      "type": "stdio",
      "command": "D:/path/to/Run.Publish/Droid/UltrasharpTools.Droid.exe",
      "args": [
        "--log-level", "Information"
      ],
      "env": {}
    }
  }
}
```

**Важно:**
- ✅ Используйте **полные абсолютные пути**
- ✅ На Windows: `\\` или `/` (оба работают)
- ✅ Для глобальной конфигурации: добавьте `"type": "stdio"` и `"env": {}`

См. примеры: `claude_global_config.example.json`, `claude_desktop_config.example.json`

### Готово! Начинайте работать

```
Вы: "Загрузи solution D:/MyProject/MyApp.sln"
Claude: ✅ Solution loaded: 15 projects, 482K symbols

Вы: "Добавь метод SendEmail в EmailService"
Claude: ✅ Метод добавлен, отформатирован, git commit создан

Вы: "Покажи где используется UserRepository"
Claude: Найдено 45 использований в 12 файлах...
```

---

## 🔌 **Режимы работы и доступ к файлам**

UltrasharpTools MCP поддерживает два режима работы с **разным доступом к файловой системе**.

### 📍 Local Mode (Stdio) - Для разработчиков

**Для кого:** Индивидуальные разработчики, работающие на своей машине.

**Как работает:**
```
Claude Desktop (ваша машина)
    ↓ запускает локальный процесс через stdio
UltrasharpTools.Droid.exe
    ↓ ПРЯМОЙ доступ к файловой системе
Ваши проекты (D:\Projects\, C:\Users\, /home/user/, и т.д.)
```

**Конфигурация (`claude_desktop_config.json`):**
```json
{
  "Droids": {
    "ultrasharp-tools": {
      "type": "stdio",
      "command": "D:/path/to/UltrasharpTools.Droid.exe",
      "args": ["--log-level", "Information"]
    }
  }
}
```

**Доступ к файлам:**
- ✅ **Полный доступ** ко всей файловой системе вашей машины
- ✅ Работает с **локальными путями** (`D:/MyProject/App.sln`, `/home/user/project/`)
- ✅ Читает/пишет файлы **напрямую**
- ✅ Использует **локальный Git** репозиторий
- ✅ NuGet packages из `~/.nuget/packages/`

**Пример использования:**
```
Claude: LoadSolution("D:/MyProjects/MyApp/MyApp.sln")
→ Сервер читает файлы напрямую с вашего диска D:\
→ Индексирует код, assemblies, references
→ Готов к работе!
```

**Преимущества:**
- ⚡ **Максимальная скорость** - нет сетевых задержек
- 🎯 **Простая настройка** - один JSON файл
- 🔒 **Безопасность** - всё локально, ничего не уходит в сеть
- 💾 **Прямой доступ** - работа с вашими файлами без копирования

**Use case:** Разработка, отладка, рефакторинг на локальной машине.

---

### 🌐 Remote Mode (HTTP/SSE) - Для команд и CI/CD

**Для кого:** Команды разработчиков, CI/CD pipelines, shared environments.

**Как работает:**
```
User Machine (Claude Desktop)
    ↓ HTTP/SSE запросы через сеть
Remote MCP Server (Kubernetes pod / Docker container)
    ↓ доступ ТОЛЬКО к mounted volumes
PersistentVolume (/app/projects/)
    ← git clone из GitHub/GitLab
```

**⚠️ ВАЖНО:** Remote сервер **НЕ ИМЕЕТ доступа** к файлам на вашей машине!

**Как предоставить файлы remote серверу?**

**Вариант 1: Git-based workflow (рекомендуется)**

```bash
# В Kubernetes pod (init container или manual):
cd /app/projects
git clone https://github.com/mycompany/myproject.git
```

Затем Claude использует:
```
LoadSolution("/app/projects/myproject/MyApp.sln")
→ Сервер читает из /app/projects (PersistentVolume внутри pod)
```

**Вариант 2: NFS/SMB Mount**

```yaml
# Kubernetes PersistentVolume с NFS
apiVersion: v1
kind: PersistentVolume
spec:
  nfs:
    server: nfs-server.example.com
    path: "/exported/projects"  # ваши проекты на NFS
```

После mount:
```
LoadSolution("/app/projects/MyApp/MyApp.sln")
→ Читает через NFS mount
```

**Вариант 3: Direct Volume Copy**

```bash
# Скопируйте проект в PersistentVolume
kubectl cp ./MyProject/ pod-name:/app/projects/MyProject/
```

**Deployment конфигурация:**

```yaml
# kubernetes/deployment.yaml
spec:
  volumes:
  - name: projects-storage
    persistentVolumeClaim:
      claimName: projects-pvc

  containers:
  - name: ultrasharp-server
    volumeMounts:
    - name: projects-storage
      mountPath: /app/projects  # <-- здесь будут ваши проекты
```

**Преимущества:**
- 👥 **Shared access** - вся команда использует один сервер
- 🔄 **CI/CD integration** - автоматизация code review, analysis
- 🛡️ **Изоляция** - код анализируется в контейнере
- 📊 **Масштабируемость** - можно добавить replicas

**Use case:** Team code review server, CI/CD pipelines, shared analysis infrastructure.

**Подробная документация:** [Run.Docs/Deployment/README.md](Run.Docs/Deployment/README.md)

---

### 📊 Сравнение режимов

| Аспект | Local (Stdio) | Remote (HTTP/SSE) |
|--------|---------------|-------------------|
| **Доступ к файлам** | ✅ Прямой к вашей ФС | ⚠️ Только mounted volumes |
| **Где исходники?** | На вашей машине | Git clone в pod/NFS mount |
| **Скорость** | ⚡ Максимальная | Зависит от network/storage |
| **Setup сложность** | 🟢 Простой (1 JSON) | 🟡 Средний (Kubernetes/Docker) |
| **Security** | Локально | Изолированно в контейнере |
| **Использование** | Один разработчик | Команда / CI/CD |
| **NuGet packages** | ~/.nuget/packages | Внутри контейнера |
| **Git operations** | Локальный репозиторий | Git в pod |

---

### 🎯 Какой режим выбрать?

**Используйте Local (Stdio), если:**
- ✅ Работаете на своей машине
- ✅ Нужна максимальная скорость
- ✅ Хотите простую настройку
- ✅ Работаете с локальными проектами

**Используйте Remote (HTTP/SSE), если:**
- ✅ Нужен shared server для команды
- ✅ Интеграция с CI/CD
- ✅ Код должен быть изолирован
- ✅ Используете Kubernetes/Docker infrastructure

**Hybrid подход:**
- **Local** для разработки и отладки
- **Remote** для code review и CI/CD

**📖 Подробная документация:** [Run.Docs/Setup/Access-Modes.md](Run.Docs/Setup/Access-Modes.md) - детальное руководство по режимам работы с примерами, troubleshooting и FAQ.

---

## 🎨 **Полный список инструментов (36 tools)**

### 🔷 Solution Management (2)
| Инструмент | Что делает |
|------------|------------|
| `LoadSolution` | Загружает .sln и инициализирует Roslyn workspace |
| `LoadProject` | Детальный обзор структуры проекта (namespaces, types) |

### 🔍 Analysis Tools (12)
| Инструмент | Что делает |
|------------|------------|
| `GetMembers` | Список членов типа с сигнатурами и XML docs |
| `ViewDefinition` | Показывает source code символа с контекстом |
| `ListImplementations` | Находит реализации интерфейса/производные классы |
| `FindReferences` | Все использования символа в solution |
| `SearchDefinitions` | Regex поиск по декларациям в коде и assemblies |
| `ViewCallGraph` | Incoming/outgoing вызовы метода |
| `ViewInheritanceChain` | Цепочка наследования типа |
| `GetAllSubtypes` | Рекурсивный список вложенных членов |
| `ManageUsings` | Чтение/запись using директив |
| `ManageAttributes` | Чтение/запись атрибутов на декларациях |
| `AnalyzeComplexity` | Метрики сложности (cyclomatic, cognitive, coupling) |
| `FindPotentialDuplicates` | Семантический поиск похожего кода |

### ✏️ Modification Tools (8)
| Инструмент | Что делает |
|------------|------------|
| `AddMember` | Добавляет member (method/property/field/class) в тип |
| `OverwriteMember` | Заменяет или удаляет member definition |
| `RenameSymbol` | Переименовывает символ + все references в solution |
| `ReplaceAllReferences` | Заменяет все ссылки на символ указанным кодом |
| `ReplaceAllReferencesByPattern` | Batch переименование по паттерну (wildcards/regex) |
| `FindAndReplace` | Regex find & replace в коде/файлах |
| `MoveMember` | Перемещает member между типами/namespaces |
| `Undo` | Откатывает последнее изменение через Git |

### ✨ Quality Tools (3)
| Инструмент | Что делает |
|------------|------------|
| `FormatCode` | Форматирование через CSharpier (.cs, .csproj, .xml) |
| `AnalyzeCodeStyle` | Анализ через Roslyn analyzers (warnings, errors) |
| `ApplyCodeFixes` | Автоприменение code fixes (unused usings и др.) |

### 🐛 Debugging & Tracing (5)
| Инструмент | Что делает |
|------------|------------|
| `TraceExecution` | Статическая трассировка через Control Flow Graph |
| `TraceBackwards` | Обратная трассировка от точки краша (с кешем 5-10x) |
| `AnalyzePathFeasibility` | Symbolic execution с Z3 solver (null checks, div by zero) |
| `ExportCallGraph` | Экспорт графа (DOT/Mermaid/GraphML) |
| `AnalyzeLogs` | Анализ логов (ECS/JSON, PlainText, Logcat, XML) |

### 📄 Document Tools (4)
| Инструмент | Что делает |
|------------|------------|
| `ReadRawFromRoslynDocument` | Читает файл (без indentation для экономии токенов) |
| `CreateRoslynDocument` | Создаёт новый файл с контентом |
| `OverwriteRoslynDocument` | Перезаписывает существующий файл |
| `ReadTypesFromRoslynDocument` | Список types и members в файле |

### 📦 Package & Misc (2)
| Инструмент | Что делает |
|------------|------------|
| `AddOrModifyNugetPackage` | Добавляет/обновляет NuGet пакет в проект |
| `RequestNewTool` | Запрос новых инструментов (логируется для review) |

**📖 Подробная документация**: [Run.Docs/Tools/](Run.Docs/Tools/) - примеры, best practices, workflows для каждого инструмента.

---

## 🔧 **Дополнительные возможности**

### Semantic Code Search (опционально)

Для поиска похожего кода по смыслу (не по тексту) можно включить векторные embeddings:

**Что получите:**
- ✅ Поиск дубликатов даже с разными названиями переменных
- ✅ "Найди код похожий на этот метод" → семантический поиск
- ✅ Автоматическая группировка похожих методов/классов

**Адаптивные векторные бэкенды** (автоматическое переключение):
- **SqliteVec** (< 10K символов): Brute-force SIMD, 100% accuracy, fast indexing
- **Vectorlite HNSW** (> 10K символов): 3-100x быстрее поиск, 99.9%+ recall, масштабируется до 100K+ векторов
- **Auto-switching**: автоматический выбор оптимального backend по размеру базы

**Конфигурации для разных проектов:**
```csharp
// Малые (< 50K): M=16, efConstruction=100
// Средние (50K-200K): M=24, efConstruction=150
// Большие (> 200K): M=32, efConstruction=200
```

**Настройка**: см. [Run.Docs/Setup/Embeddings.md](Run.Docs/Setup/Embeddings.md)

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

</details>

---

## 🛠️ **Разработка**

### Development Build
```bash
# Сборка solution для разработки
dotnet build UltrasharpTools.sln
```

### Production Build
```bash
# Windows
publish-droid.cmd

# PowerShell
.\Dev.Scripts\publish-mcp.ps1

# Linux/Mac
./Dev.Scripts/publish-mcp.sh

# Оба сервера (MCP + Remote)
.\Dev.Scripts\publish-all.ps1  # Windows
./Dev.Scripts\publish-all.sh   # Linux/Mac
```

### Запуск из исходников
```bash
# Droid (stdio)
cd UltrasharpTools.Droid
dotnet run -- --log-level Debug

# Overlord (HTTP)
cd UltrasharpTools.Overlord
dotnet run -- --port 3001 --log-level Information
```

### Tests
```bash
# Layered Index tests
cd UltrasharpTools.Test/UltrasharpTools.Test.LayeredIndex
dotnet run -- --index-self

# Semantic Merge tests
cd UltrasharpTools.Test/UltrasharpTools.Test.SemanticMerge
dotnet run
```

---

## 📝 **Что нового?**

### v1.0.0 (2025-11-17) - Production Release

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
- Quality Tools - FormatCode (CSharpier), AnalyzeCodeStyle, ApplyCodeFixes
- Semantic Merge - умное 3-way слияние с movement/rename detection
- Advanced Tracing - TraceExecution, TraceBackwards, AnalyzePathFeasibility (Z3)
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


# UltrasharpTools - Руководство по использованию

**Версия:** 3.6.1
**Дата:** 2025-11-27

---

## Содержание

1. [Быстрый старт](#1-быстрый-старт)
2. [Основные сценарии](#2-основные-сценарии)
3. [Работа с инструментами](#3-работа-с-инструментами)
4. [Advanced Workflows](#4-advanced-workflows)
5. [Troubleshooting](#5-troubleshooting)

---

## 1. Быстрый старт

### 1.1 Установка

**Windows (быстрый способ):**
```cmd
publish-droid.cmd
```

**PowerShell/Linux:**
```bash
pwsh Dev.Scripts/publish-mcp.ps1  # Windows/Linux
./Dev.Scripts/publish-mcp.sh      # Linux/Mac
```

**Результат:** `Run.Publish/Droid/UltraSharp-tools.com` (~50 KB) + `Run.Publish/Droid/UltrasharpTools.Droid.exe` (103 MB)

### 1.2 Архитектура Comm/Droid

UltrasharpTools использует **трёхпроцессную архитектуру** с lazy-запуском:

```
Claude₁ ←stdio→ Comm₁ ──┐
Claude₂ ←stdio→ Comm₂ ──┼── Named Pipe ──→ Droid (singleton) ──→ VectorDB
Claude₃ ←stdio→ Comm₃ ──┘
```

**Компоненты:**
- **Comm** (~700 KB) — универсальный stdio-bridge для MCP-клиентов (один бинарник для всех ОС)
- **Droid** (~103 MB) — основной Roslyn-сервер (singleton, multi-client)
- **VectorDB** (~40 MB) — семантический движок (lazy start, опционально)

**Как это работает:**
1. Первый Comm запускает Droid как daemon с `--pipe-server`
2. Последующие Comm подключаются к существующему Droid
3. Wake-up из idle режима через Named Event (мгновенное пробуждение)
4. Все Comm разделяют один SolutionManager (solution грузится один раз)

### 1.3 Конфигурация Claude Desktop

Создайте/отредактируйте `~/.claude.json` (Linux/Mac) или `%USERPROFILE%\.claude.json` (Windows):

```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:/Tools/UltrasharpTools/Droid/UltraSharp-tools.com"
    }
  }
}
```

**⚠️ Важно:**
- Запускайте **UltraSharp-tools.com** (~700 KB), а не Droid.exe!
- Comm — универсальный stdio-bridge (работает на Win/Linux/macOS), который автоматически запустит Droid при первом подключении
- Используйте **полный абсолютный путь**
- На Windows: `\\` или `/` (оба работают)
- **Multiple instances:** Несколько Claude окон могут работать одновременно — все используют один Droid

### 1.4 Первый запрос

**В Claude Desktop:**

```
Вы: Загрузи solution D:/MyProject/MyApp.sln

Claude: ✅ Solution loaded successfully!
• Projects: 5
• Documents: 234 файла
• Symbols indexed: 125,847
• Time: 8.9s (warm cache)

Готов к работе! Что делаем дальше?
```

---

## 2. Основные сценарии

### 2.1 Навигация по коду

#### Карта проекта

```
Вы: Покажи структуру проекта MyApp.Services

Claude вызывает: load_project(projectName: "MyApp.Services")

→ Результат:
MyApp.Services
├─ Namespaces:
│  ├─ MyApp.Services.Users
│  │  ├─ UserService (class)
│  │  └─ IUserRepository (interface)
│  ├─ MyApp.Services.Orders
│  │  └─ OrderService (class)
│  └─ MyApp.Services.Common
│     └─ BaseService (abstract class)
```

#### Просмотр типа

```
Вы: Покажи класс UserService

Claude вызывает: view_definition(
  fullyQualifiedName: "MyApp.Services.Users.UserService"
)

→ Результат:
```csharp
public class UserService : BaseService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User> GetUserAsync(int id)
    {
        return await _repository.FindByIdAsync(id);
    }

    public bool ValidateEmail(string email)
    {
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }
}
```

**Referenced types:** IUserRepository, BaseService, User, Regex
**Referencing types:** UserController (Controllers/UserController.cs:15)
```
```

#### Члены типа

```
Вы: Какие методы есть у UserService?

Claude вызывает: get_members(
  fullyQualifiedName: "MyApp.Services.Users.UserService"
)

→ Результат:
UserService members:

Fields (1):
• _repository: IUserRepository (readonly)

Constructors (1):
• UserService(IUserRepository)

Methods (2):
• GetUserAsync(int id): Task<User>
• ValidateEmail(string email): bool
```

### 2.2 Поиск и анализ

#### Где используется символ

```
Вы: Где используется метод GetUserAsync?

Claude вызывает: find_references(
  fullyQualifiedName: "MyApp.Services.Users.UserService.GetUserAsync"
)

→ Результат:
Found 7 references:

1. Controllers/UserController.cs:25
   ```csharp
   var user = await _userService.GetUserAsync(id);
   ```

2. Services/AuthService.cs:45
   ```csharp
   var existing = await _userService.GetUserAsync(userId);
   ```

3. Tests/UserServiceTests.cs:33
   ```csharp
   var result = await sut.GetUserAsync(123);
   ```

... (4 more)
```

#### Поиск похожих методов

```
Вы: Найди похожие методы валидации email

Claude вызывает: find_duplicates(
  targetCode: "bool ValidateEmail(string email) { ... }",
  threshold: 0.7
)

→ Результат (с Universal Semantic Mode):
Similar methods found:

1. TeamProject.Utils.EmailValidator.IsValid (similarity: 0.92)
   Location: ../TeamProject/Utils/EmailValidator.cs:15
   Recommendation: Можно переиспользовать готовый класс!

2. SharedLibrary.Validation.EmailHelper.Verify (similarity: 0.85)
   Location: NuGet package SharedLibrary.Core
   Recommendation: Использовать библиотечную реализацию
```

### 2.3 Модификация кода

#### Добавление метода

```
Вы: Добавь метод SendWelcomeEmail в UserService

Claude вызывает: add_member(
  containerFqn: "MyApp.Services.Users.UserService",
  memberCode: """
  public async Task SendWelcomeEmail(User user)
  {
      var emailService = _serviceProvider.GetService<IEmailService>();
      await emailService.SendAsync(user.Email, "Welcome!", "...");
  }
  """
)

→ Результат:
✅ Method added successfully
✅ Auto-added: using Microsoft.Extensions.DependencyInjection
✅ Code formatted via CSharpier
✅ Git commit created: feat: Add SendWelcomeEmail to UserService
✅ Compilation successful (0 errors)

Branch: ultrasharptools/20251118-143022
```

#### Изменение метода

```
Вы: Обнови ValidateEmail чтобы проверял только домены .com и .org

Claude вызывает: modify_code(
  fullyQualifiedName: "MyApp.Services.Users.UserService.ValidateEmail",
  newCode: """
  public bool ValidateEmail(string email)
  {
      if (string.IsNullOrWhiteSpace(email))
          return false;

      var regex = new Regex(@"^[^@\s]+@[^@\s]+\.(com|org)$");
      return regex.IsMatch(email);
  }
  """
)

→ Результат:
✅ Method replaced successfully
✅ Compilation successful
✅ Git commit: refactor: Restrict email validation to .com/.org domains
```

#### Переименование символа

```
Вы: Переименуй GetUserAsync в FetchUserAsync везде в solution

Claude вызывает: rename_symbol(
  fullyQualifiedName: "MyApp.Services.Users.UserService.GetUserAsync",
  newName: "FetchUserAsync"
)

→ Результат:
✅ Symbol renamed in 8 locations:
  1. UserService.cs:15 (definition)
  2. UserController.cs:25 (call)
  3. AuthService.cs:45 (call)
  4. UserServiceTests.cs:33 (call)
  ... (4 more)

✅ All tests passing
✅ Git commit: refactor: Rename GetUserAsync → FetchUserAsync
```

### 2.4 Quality & Formatting

#### Форматирование кода

```
Вы: Отформатируй все файлы в проекте MyApp.Services

Claude вызывает: format_code(
  path: "D:/MyProject/MyApp.Services",
  checkOnly: false
)

→ Результат:
✅ Formatted 45 files (234 files total, 189 already formatted)
✅ Time: 2.3s

Changed files:
• Services/UserService.cs (indentation, spacing)
• Services/OrderService.cs (line breaks)
... (43 more)

Git commit: style: Format code with CSharpier
```

#### Анализ качества

```
Вы: Проанализируй качество кода в MyApp.Services

Claude вызывает: analyze_code_style(
  solutionPath: "D:/MyProject/MyApp.sln",
  severityFilter: "Warning"
)

→ Результат:
Found 12 warnings:

High Severity (3):
• CA1062: Validate parameter 'user' is non-null (UserService.cs:45)
• CA1822: Member 'GetConfig' does not access instance data (BaseService.cs:23)
• IDE0059: Unnecessary assignment to 'result' (OrderService.cs:67)

Medium Severity (5):
• IDE0005: Remove unnecessary using 'System.Linq' (5 files)

... (4 more)

Recommendation: Run apply_code_fixes для автоисправления
```

#### Автоисправление

```
Вы: Примени автоисправления

Claude вызывает: apply_code_fixes(
  solutionPath: "D:/MyProject/MyApp.sln",
  diagnosticId: "IDE0005",  // Удалить ненужные using
  preview: false
)

→ Результат:
✅ Fixed 5 files:
  • UserService.cs: Removed 2 unused usings
  • OrderService.cs: Removed 1 unused using
  ... (3 more)

✅ Compilation successful
✅ Git commit: refactor: Remove unused using directives
```

---

## 3. Работа с инструментами

### 3.1 Solution & Project Management

| Инструмент | Использование | Когда применять |
|-----------|---------------|-----------------|
| `load_solution` | Загрузка .sln | Первый шаг при работе с проектом |
| `load_project` | Обзор проекта | Навигация по структуре проекта |

**Типичный flow:**
```
1. load_solution("path/to/solution.sln")
   → Получить список проектов

2. load_project("ProjectName")
   → Получить карту типов (namespaces → types)

3. Работа с символами через FQN
   → view_definition, get_members, find_references, etc.
```

### 3.2 Analysis Tools (12)

| Инструмент | Назначение | Пример |
|-----------|-----------|--------|
| `get_members` | Члены типа | Методы, свойства, поля класса |
| `view_definition` | Source code символа | Полный код класса/метода |
| `list_implementations` | Реализации интерфейса | Найти все классы, реализующие IUserRepository |
| `find_references` | Использования символа | Где вызывается метод/используется класс |
| `search_definitions` | Regex поиск | Найти все классы, содержащие "Service" |
| `view_call_graph` | Граф вызовов | Incoming/outgoing calls метода |
| `view_inheritance_chain` | Цепочка наследования | Base → Derived классы |
| `get_all_subtypes` | Рекурсивные члены | Nested types & members |
| `manage_usings` | Using директивы | Чтение/запись using statements |
| `manage_attributes` | Атрибуты | Чтение/запись attributes |
| `analyze_complexity` | Метрики сложности | Cyclomatic, cognitive, coupling |
| `find_duplicates` | Поиск дубликатов | Семантически похожий код |

### 3.3 Modification Tools (8)

| Инструмент | Назначение | Безопасность |
|-----------|-----------|--------------|
| `add_member` | Добавить член | ✅ Безопасно (новый код) |
| `modify_code` | Заменить/удалить член | ⚠️ Тестируйте изменения |
| `rename_symbol` | Переименовать символ | ✅ Безопасно (Roslyn refactoring) |
| `replace_all_references` | Заменить все ссылки | ⚠️ Review перед применением |
| `replace_references_by_pattern` | Batch замена | ⚠️ Проверяйте паттерн |
| `find_and_replace` | Regex find & replace | ⚠️ Опасно без preview |
| `move_member` | Переместить член | ⚠️ Может сломать зависимости |
| `undo` | Откатить изменение | ✅ Всегда безопасно |

**⚠️ Best Practices:**
1. Всегда делайте git commit перед масштабными изменениями
2. Используйте `undo` при ошибках
3. Проверяйте компиляцию после модификаций
4. Запускайте тесты перед push

### 3.4 Quality Tools (3)

| Инструмент | Назначение | Workflow |
|-----------|-----------|----------|
| `format_code` | Форматирование (CSharpier) | checkOnly=true → review → checkOnly=false |
| `analyze_code_style` | Roslyn analyzers | severityFilter: Warning/Error |
| `apply_code_fixes` | Автоисправления | preview=true → review → preview=false |

**Типичный quality workflow:**
```
1. analyze_code_style("path/to/solution.sln", severityFilter: "Warning")
   → Найти проблемы

2. apply_code_fixes("path/to/solution.sln", diagnosticId: "IDE0005", preview: true)
   → Предпросмотр

3. apply_code_fixes(..., preview: false)
   → Применить исправления

4. format_code("path/to/project/", checkOnly: false)
   → Финальное форматирование
```

### 3.5 Tracing Tools (5)

| Инструмент | Назначение | Use Case |
|-----------|-----------|----------|
| `trace_execution` | CFG трассировка | Понять выполнение метода |
| `trace_backwards` | Обратная трассировка | Найти путь к крашу |
| `analyze_path_feasibility` | Symbolic execution | Проверить достижимость пути |
| `export_call_graph` | Экспорт графа | DOT/Mermaid/GraphML |
| `analyze_logs` | Парсинг логов | Structured log analysis |

---

## 4. Advanced Workflows

### 4.1 Debugging Production Crash

**Сценарий:** Production краш, есть stack trace

```
Stack Trace:
at MyApp.Services.OrderService.ProcessPayment(Order order)
at MyApp.Controllers.OrderController.Checkout() in OrderController.cs:line 45
```

**Workflow:**

```
1. Вы: Загрузи solution и найди путь к крашу в OrderService.ProcessPayment

2. Claude вызывает: trace_backwards(
     crashLocation: "MyApp.Services.OrderService.ProcessPayment",
     stackTraceHints: [
       "MyApp.Controllers.OrderController.Checkout"
     ],
     maxDepth: 8
   )

   → Результат:
   Found 2 possible paths:

   Path 1 (confidence: 92%):
   [0] OrderController.Checkout (entry point)
   [1]   _orderService.ProcessPayment(order)
   [2]     _paymentGateway.Charge(amount) [CRASH HERE]

   Crash reason: amount is negative due to discount calculation error

   Path 2 (confidence: 78%):
   [0] BackgroundJob.ProcessPendingOrders
   [1]   OrderService.ProcessPayment(order)
   [2]     ... [CRASH]

3. Вы: Покажи метод ProcessPayment

4. Claude вызывает: view_definition(
     fullyQualifiedName: "MyApp.Services.OrderService.ProcessPayment"
   )

   → Находит баг: amount не проверяется на отрицательное значение

5. Вы: Добавь проверку перед вызовом Charge

6. Claude вызывает: modify_code(...новый код с проверкой...)

   → ✅ Fix applied, tests passing
```

### 4.2 Large-Scale Refactoring

**Сценарий:** Переименовать IUserRepository → IUserDataAccess во всём solution

**Workflow:**

```
1. Анализ влияния:
   Вы: Где используется IUserRepository?

   Claude: find_references("IUserRepository")
   → 47 использований в 12 файлах

2. Переименование:
   Вы: Переименуй IUserRepository в IUserDataAccess везде

   Claude: rename_symbol(
     fullyQualifiedName: "MyApp.Data.IUserRepository",
     newName: "IUserDataAccess"
   )

   → ✅ 47 occurrences renamed
   → ✅ 0 compilation errors
   → ✅ Git commit created

3. Проверка:
   Вы: Запусти тесты

   → ✅ All tests passing
```

### 4.3 Code Quality Improvement Sprint

**Сценарий:** Улучшить качество кода перед релизом

**Workflow:**

```
1. Baseline analysis:
   analyze_code_style(solution, severityFilter: "Warning")
   → 45 warnings found

2. Auto-fix simple issues:
   apply_code_fixes(solution, diagnosticId: "IDE0005")  # Unused usings
   → 12 files fixed

   apply_code_fixes(solution, diagnosticId: "CS8019")  # Unnecessary using
   → 8 files fixed

3. Format all code:
   format_code(projectPath, checkOnly: false)
   → 67 files formatted

4. Re-analyze:
   analyze_code_style(solution, severityFilter: "Warning")
   → 23 warnings remaining (manual review needed)

5. Complexity analysis:
   analyze_complexity(high_complexity_methods)
   → Identify refactoring candidates

6. Final check:
   → ✅ Warnings reduced from 45 to 23 (49% improvement)
   → ✅ All code formatted consistently
   → ✅ Ready for release
```

### 4.4 Cross-Project Code Reuse (Hybrid Mode)

**Сценарий:** Найти переиспользуемый код в других проектах команды

**Workflow:**

```
1. Enable hybrid mode:
   Droid: --mode hybrid --server-url http://overlord:3001

2. Вы: Найди похожие реализации email validation

   Claude вызывает: find_duplicates(
     targetCode: "ValidateEmail(...)",
     threshold: 0.7,
     scope: "all_projects"  # Ключевое!
   )

   → Результат (cross-project search):

   1. TeamProject1.Utils.EmailValidator (similarity: 0.95)
      Location: \\shared\TeamProject1\Utils\
      Recommendation: Использовать существующую реализацию!

   2. SharedLibrary.Core.Validation.Email (similarity: 0.89)
      Location: NuGet package (internal)
      Recommendation: Добавить пакет через add_package

3. Вы: Добавь пакет SharedLibrary.Core

   Claude: add_package(
     projectPath: "MyApp.Services.csproj",
     packageId: "SharedLibrary.Core",
     version: "latest"
   )

   → ✅ Package added
   → ✅ Solution reloaded
   → ✅ Now can use SharedLibrary.Core.Validation.Email
```

---

## 5. Troubleshooting

### 5.1 Solution не загружается

**Проблема:**
```
Error: Failed to load solution: Could not find SDK 'Microsoft.NET.Sdk'
```

**Решение:**
1. Убедитесь, что установлен соответствующий .NET SDK:
   ```bash
   dotnet --list-sdks
   # Должен быть SDK, соответствующий TargetFramework проекта
   ```

2. Если проект использует .NET 8, а у вас .NET 10:
   ```bash
   # Установите .NET 8 SDK параллельно
   # или обновите TargetFramework в .csproj
   ```

### 5.2 Symbol не найден

**Проблема:**
```
Error: Symbol 'MyNamespace.MyClass' not found in index
```

**Решение:**
1. Проверьте FQN (Fully Qualified Name):
   ```
   Правильно: MyApp.Services.UserService
   Неправильно: UserService (ambiguous)
   ```

2. Убедитесь, что solution загружен:
   ```
   load_solution("path/to/solution.sln")
   ```

3. Если проект не был загружен:
   ```
   load_project("ProjectName")
   ```

### 5.3 Git операции не работают

**Проблема:**
```
Error: Repository not found or not a git repository
```

**Решение:**
1. Убедитесь, что `.git` директория существует:
   ```bash
   git status
   ```

2. Или отключите Git integration:
   ```bash
   UltrasharpTools.Droid.exe --disable-git
   ```

### 5.4 Медленная работа

**Проблема:** Загрузка solution занимает > 30 секунд

**Решение:**
1. При первой загрузке это нормально (cold cache)
2. Subsequent loads должны быть 5-10x быстрее
3. Убедитесь, что symbol cache включен (по умолчанию)
4. Проверьте логи для bottleneck'ов:
   ```bash
   --log-level Debug
   ```

### 5.5 CSharpier dependency issue

**Проблема:**
```
Error: Could not load file or assembly 'CSharpier.Core'
```

**Решение:**
Пересобрийте проект с `CopyLocalLockFileAssemblies`:
```bash
dotnet clean
dotnet build
```

Или используйте publish script:
```bash
publish-droid.cmd
```

### 5.6 Named Pipe: Droid не запускается

**Проблема:**
```
Error: Failed to connect to Droid pipe after 30s timeout
```

**Решение:**
1. Проверьте, запущен ли Droid:
   ```powershell
   Get-Process -Name "UltrasharpTools.Droid" -ErrorAction SilentlyContinue
   ```

2. Если Droid завис, убейте процесс:
   ```powershell
   Stop-Process -Name "UltrasharpTools.Droid" -Force
   ```

3. Убедитесь, что путь к Droid указан правильно в Comm:
   - Comm ищет Droid в той же директории или в `../Droid/`

4. Проверьте логи Droid:
   ```bash
   # Логи в Run.Logs/droid-*.log
   ```

### 5.7 Named Pipe: Multiple Droid instances

**Проблема:** Запустилось несколько Droid процессов

**Причина:** Race condition при одновременном старте нескольких Comm

**Решение:**
```powershell
# Остановите все Droid
Stop-Process -Name "UltrasharpTools.Droid" -Force

# Перезапустите Comm — он стартует один Droid
```

**Превентивно:** Named Event `UltraSharpTools_Droid_WakeUp` должен предотвратить эту проблему. Если она повторяется, проверьте логи.

---

## 6. Performance Tips

### 6.1 Оптимизация запросов

**❌ Плохо:**
```
Вы: Покажи все методы в solution
→ Огромный ответ, тысячи методов
```

**✅ Хорошо:**
```
Вы: Покажи методы класса UserService
→ Точный, компактный ответ
```

### 6.2 Используйте FQN

**❌ Плохо:**
```
view_definition("UserService")
→ Ambiguous: найдено 3 класса с именем UserService
```

**✅ Хорошо:**
```
view_definition("MyApp.Services.Users.UserService")
→ Точное совпадение за < 100ms
```

### 6.3 Кэширование

**Автоматическое:**
- Symbol cache (warm load 5-10x faster)
- Call graph cache (TraceBackwards 5-7x faster)
- Layered index (branch switch 330x faster)

**Управление:**
```bash
# Очистить весь кэш
--symbol-cache-clear

# Отключить кэш (не рекомендуется)
# (опция не реализована, кэш всегда включен для производительности)
```

---

## 7. Лучшие практики

### 7.1 Workflow с Git

1. **Всегда делайте checkpoint перед масштабными изменениями:**
   ```
   git commit -m "Before UltrasharpTools refactoring"
   ```

2. **UltrasharpTools создаёт ветки `ultrasharptools/*` автоматически**
   - Каждое изменение = новая ветка + commit
   - Используйте `undo` для отката

3. **Периодическая очистка:**
   ```bash
   # Включить auto-cleanup (по умолчанию)
   --git-auto-cleanup
   --git-branch-retention-count 10
   ```

### 7.2 Large Solutions

Для solution > 1M символов:

1. Загружайте проекты по отдельности при необходимости
2. Используйте `load_project` вместо полного обхода
3. Фильтруйте результаты по severity/threshold
4. Увеличьте memory limit если нужно (> 2GB RAM)

### 7.3 Team Collaboration (Hybrid Mode)

1. **Запустите Overlord сервер:**
   ```bash
   cd UltrasharpTools.Overlord
   dotnet run -- --port 3001 \
     --embedding-url http://ollama:11434 \
     --embedding-model nomic-embed-text
   ```

2. **Настройте Droid в hybrid mode:**
   ```bash
   cd UltrasharpTools.Droid
   dotnet run -- --mode hybrid \
     --server-url http://overlord:3001 \
     --load-solution /path/to/solution.sln
   ```

3. **Cross-project search работает автоматически!**

---

## 8. Ресурсы

### Документация
- [ARCHITECTURE.md](Architecture/ARCHITECTURE.md) - Архитектура системы
- [README.md](../README.md) - Обзор и quick start
- [CHANGELOG.md](../CHANGELOG.md) - История изменений
- [ROADMAP.md](../ROADMAP.md) - Планы развития
- [CLAUDE.md](CLAUDE.md) - Для Claude Code

### Помощь
- GitHub Issues: https://github.com/your-org/ultrasharp-tools-mcp/issues
- Документация MCP Protocol: https://github.com/modelcontextprotocol

---

**Версия:** 3.6.1
**Последнее обновление:** 2025-11-27

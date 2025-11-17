# Инструменты анализа кода

**Комплексный набор для анализа C# кодовых баз**: просмотр определений, поиск использований, анализ сложности, управление using/attributes. Все инструменты работают через Roslyn API для точного semantic analysis.

## 📋 Quick Reference

| Инструмент | Назначение | Основное применение |
|------------|-----------|---------------------|
| **GetMembers** | Список всех членов типа с сигнатурами | Быстрый overview API класса |
| **ViewDefinition** | Исходный код символа с контекстом | Понять реализацию метода/класса |
| **ListImplementations** | Все реализации интерфейса/базового класса | Найти наследников, полиморфизм |
| **FindReferences** | Все места использования символа | Понять где и как используется |
| **SearchDefinitions** | Regex поиск по определениям | Найти паттерны, naming violations |
| **ManageUsings** | Чтение/запись using directives | Добавить/удалить using |
| **ManageAttributes** | Чтение/запись атрибутов | Добавить/изменить аттрибуты |
| **AnalyzeComplexity** | Метрики сложности кода | Найти сложный код для рефакторинга |

---

## UltrasharpTool_GetMembers

**Быстрый overview API типа** — возвращает все члены (methods, properties, fields, events) с сигнатурами и XML документацией.

### Использование

```javascript
UltrasharpTool_GetMembers(
fullyQualifiedTypeName: "MyNamespace.MyClass",
includePrivateMembers: false
)
```

### Параметры

- **fullyQualifiedTypeName** (required): FQN типа (класс, интерфейс, struct, enum)
- **includePrivateMembers** (required): `true` — все члены, `false` — только public/protected/internal

### Что показывает

Для каждого члена:
- 🔹 **Сигнатура** (полная, с типами параметров и возврата)
- 📝 **XML документация** (summary, remarks, params, returns)
- 🔒 **Модификаторы доступа** (public, private, protected, internal)
- 🔧 **Модификаторы** (static, virtual, abstract, override, sealed)
- 🆔 **FQN** для использования с другими инструментами

### Пример вывода

```
Type: MyProject.Services.UserService

═══════════════════════════════════════════════════════════
📦 CONSTRUCTORS
═══════════════════════════════════════════════════════════

public UserService(IUserRepository repository, ILogger<UserService> logger)
/// <summary>
/// Initializes a new instance of the UserService class.
/// </summary>
/// <param name="repository">The user repository.</param>
/// <param name="logger">The logger instance.</param>
FQN: MyProject.Services.UserService..ctor(IUserRepository, ILogger<UserService>)

═══════════════════════════════════════════════════════════
📋 METHODS
═══════════════════════════════════════════════════════════

public async Task<User> GetUserByIdAsync(int userId)
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="userId">The user ID to search for.</param>
/// <returns>The user object if found, null otherwise.</returns>
FQN: MyProject.Services.UserService.GetUserByIdAsync(int)

public async Task<int> CreateUserAsync(string name, string email)
/// <summary>
/// Creates a new user with the specified name and email.
/// </summary>
/// <param name="name">The user's full name.</param>
/// <param name="email">The user's email address.</param>
/// <returns>The newly created user's ID.</returns>
/// <exception cref="ArgumentException">Thrown when name or email is invalid.</exception>
FQN: MyProject.Services.UserService.CreateUserAsync(string, string)

private async Task<bool> ValidateUserAsync(User user)
FQN: MyProject.Services.UserService.ValidateUserAsync(User)

═══════════════════════════════════════════════════════════
📊 PROPERTIES
═══════════════════════════════════════════════════════════

public ILogger<UserService> Logger { get; }
/// <summary>
/// Gets the logger instance for this service.
/// </summary>
FQN: MyProject.Services.UserService.Logger

═══════════════════════════════════════════════════════════
📁 FIELDS
═══════════════════════════════════════════════════════════

private readonly IUserRepository _repository
FQN: MyProject.Services.UserService._repository

💡 Use UltrasharpTool_ViewDefinition with FQN to see implementation
```

### Когда использовать

✅ **Для изучения API:**
- Первый взгляд на незнакомый класс
- Понять публичный интерфейс
- Найти нужный метод по сигнатуре
- Изучить параметры и возвращаемые типы

✅ **Для документации:**
- Генерация API reference
- Проверка XML комментариев
- Понимание contracts

✅ **Перед модификацией:**
- Увидеть все существующие члены
- Избежать дублирования имён
- Понять naming conventions

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Нужна реализация метода → `ViewDefinition`
- Нужно найти где используется → `FindReferences`
- Нужно найти все классы похожей структуры → `SearchDefinitions`

### Best Practices

1. **Начинайте с includePrivateMembers: false:**
   ```javascript
   // ✅ Сначала смотрим public API
   UltrasharpTool_GetMembers("MyNamespace.MyClass", includePrivateMembers: false)

   // ✅ Потом, если нужно, private детали
   UltrasharpTool_GetMembers("MyNamespace.MyClass", includePrivateMembers: true)
   ```

2. **Используйте FQN из вывода:**
   ```javascript
   // ✅ GetMembers возвращает точные FQN для других инструментов
   UltrasharpTool_GetMembers("MyClass", false)
   // Output: "FQN: MyNamespace.MyClass.MyMethod(int, string)"

   UltrasharpTool_ViewDefinition("MyNamespace.MyClass.MyMethod(int, string)")
   ```

3. **Для интерфейсов - найдите реализации:**
   ```javascript
   UltrasharpTool_GetMembers("IUserService", false)  // См членов интерфейса
   UltrasharpTool_ListImplementations("IUserService") // Найти реализации
   ```

### Типичные ошибки

#### ❌ Ошибка: "Type not found"
```
ERROR: Type 'MyClass' not found
```
**Решение:**
- Используйте полный FQN: `MyNamespace.MyClass`
- Проверьте что solution загружен
- Проверьте spelling (fuzzy matching может не помочь)

### Производительность

- **Скорость:** Мгновенно (< 100 мс) благодаря FastSymbolIndex
- **Memory:** Минимально (только metadata, без source code)

### Связанные инструменты

- ➡️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см. реализацию члена
- ➡️ [**ListImplementations**](#UltrasharpTool_listimplementations) — для интерфейсов/базовых классов
- ➡️ [**FindReferences**](#UltrasharpTool_findreferences) — где используется член

---

## UltrasharpTool_ViewDefinition

**Полный исходный код символа** — возвращает определение класса, метода, свойства с контекстной информацией (call graph, type references).

### Использование

```javascript
UltrasharpTool_ViewDefinition(
fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

### Параметры

- **fullyQualifiedSymbolName** (required): FQN символа (тип, метод, свойство, поле)

### Что показывает

**Для типов (class, interface, struct):**
- 📄 Полный исходный код (без отступов)
- 📦 Namespace и using directives
- 🔗 Base class и implemented interfaces
- 📊 Вложенные типы
- 📁 Расположение файла

**Для методов:**
- 📄 Исходный код метода
- 📞 **Call graph** (что метод вызывает)
- 🔍 **Incoming calls** (кто вызывает этот метод — preview 3 examples)
- 📁 Расположение файла

**Для свойств/полей:**
- 📄 Определение
- 🔍 Где используется (preview)
- 📁 Расположение файла

### Пример вывода

```
Definition: MyProject.Services.UserService.CreateUserAsync

File: D:/MyProject/src/Services/UserService.cs:45

═══════════════════════════════════════════════════════════
📄 SOURCE CODE
═══════════════════════════════════════════════════════════

/// <summary>
/// Creates a new user with the specified name and email.
/// </summary>
public async Task<int> CreateUserAsync(string name, string email)
{
if (string.IsNullOrWhiteSpace(name))
throw new ArgumentException("Name cannot be empty", nameof(name));

if (!IsValidEmail(email))
throw new ArgumentException("Invalid email format", nameof(email));

var user = new User
{
Name = name,
Email = email,
CreatedAt = DateTime.UtcNow
};

var isValid = await ValidateUserAsync(user);
if (!isValid)
throw new ValidationException("User validation failed");

var userId = await _repository.CreateAsync(user);
_logger.LogInformation("User created with ID: {UserId}", userId);

return userId;
}

═══════════════════════════════════════════════════════════
📞 CALLS (what this method calls)
═══════════════════════════════════════════════════════════

1. MyProject.Services.UserService.IsValidEmail(string) : bool
   Line 51: if (!IsValidEmail(email))

2. MyProject.Domain.User..ctor() : void
   Line 54: var user = new User

3. MyProject.Services.UserService.ValidateUserAsync(User) : Task<bool>
   Line 62: var isValid = await ValidateUserAsync(user);

4. MyProject.Data.IUserRepository.CreateAsync(User) : Task<int>
   Line 67: var userId = await _repository.CreateAsync(user);

5. Microsoft.Extensions.Logging.ILogger.LogInformation(string, object[]) : void
   Line 68: _logger.LogInformation(...)

═══════════════════════════════════════════════════════════
🔍 INCOMING CALLS (who calls this method) - Top 3
═══════════════════════════════════════════════════════════

1. MyProject.API.Controllers.UserController.Post(CreateUserRequest)
   File: Controllers/UserController.cs:78
   Context:
   var userId = await _userService.CreateUserAsync(
       request.Name,
       request.Email
   );

2. MyProject.Tests.UserServiceTests.CreateUser_ValidData_ReturnsUserId()
   File: Tests/UserServiceTests.cs:45
   Context:
   var userId = await _service.CreateUserAsync("John Doe", "john@example.com");

3. MyProject.BackgroundJobs.UserImportJob.ImportUsersAsync()
   File: BackgroundJobs/UserImportJob.cs:112
   Context:
   await _userService.CreateUserAsync(row.Name, row.Email);

💡 Use UltrasharpTool_FindReferences for complete list of all 15 callers
```

### Когда использовать

✅ **Для понимания реализации:**
- Как работает метод изнутри
- Что делает класс
- Какие зависимости использует

✅ **Для анализа потока выполнения:**
- Что вызывается из метода (call graph)
- Откуда вызывается метод (incoming calls)
- Понимание data flow

✅ **Для отладки:**
- Найти источник бага
- Понять логику
- Проверить validation

✅ **Перед модификацией:**
- Увидеть текущую реализацию
- Понять impact на другие методы
- Определить тесты для проверки

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Нужен только список членов → `GetMembers`
- Нужны все места использования → `FindReferences`
- Ищете паттерн в коде → `SearchDefinitions`

### Best Practices

1. **Используйте call graph для навигации:**
   ```javascript
   // ViewDefinition показывает что метод вызывает
   UltrasharpTool_ViewDefinition("UserService.CreateUserAsync")
   // Output: "Calls: UserService.ValidateUserAsync"

   // Переходим к вызываемому методу
   UltrasharpTool_ViewDefinition("UserService.ValidateUserAsync")
   ```

2. **Для классов - начинайте с GetMembers:**
   ```javascript
   // ✅ Сначала обзор
   UltrasharpTool_GetMembers("MyClass", false)

   // ✅ Затем детали интересующего члена
   UltrasharpTool_ViewDefinition("MyClass.InterestingMethod")
   ```

3. **Используйте incoming calls как отправную точку:**
   ```javascript
   UltrasharpTool_ViewDefinition("MyClass.ComplexMethod")
   // Output: "Incoming calls: CallerA, CallerB, CallerC"

   // Анализируем как вызывается
   UltrasharpTool_FindReferences("MyClass.ComplexMethod")
   ```

### Производительность

- **Первый вызов:** 1-3 сек (компиляция + semantic model)
- **Повторный вызов:** < 100 мс (благодаря кэшу)
- **С call graph:** +0.5-2 сек (зависит от сложности)

### Разрешение source code

ViewDefinition умеет получать код из разных источников:

1. **Local files** (мгновенно)
2. **SourceLink** (скачивает с GitHub/GitLab)
3. **Embedded PDB** (извлекает из debug info)
4. **Decompilation** (ILSpy, если нет исходников)

**Приоритет:** Local → SourceLink → Embedded → Decompilation

### Типичные ошибки

#### ❌ Ошибка: "Symbol not found"
```
ERROR: Symbol 'MyMethod' not found
```
**Решение:**
- Используйте полный FQN: `MyNamespace.MyClass.MyMethod`
- Для overloads укажите параметры: `MyMethod(int, string)`
- Проверьте spelling

#### ❌ Ошибка: "Source code not available"
```
WARNING: Source code not available for external symbol
```
**Решение:**
- Для NuGet packages: убедитесь что есть SourceLink
- Fallback на decompilation (автоматически)

### Связанные инструменты

- ⬅️ [**GetMembers**](#UltrasharpTool_getmembers) — сначала см список членов
- ➡️ [**FindReferences**](#UltrasharpTool_findreferences) — все места использования
- ➡️ [**TraceExecution**](TRACING_TOOLS.md#UltrasharpTool_traceexecution) — детальный trace выполнения

---

## UltrasharpTool_ListImplementations

**Поиск наследников** — находит все реализации интерфейса, абстрактного метода или производные классы.

### Использование

```javascript
UltrasharpTool_ListImplementations(
fullyQualifiedSymbolName: "MyNamespace.IUserRepository"
)
```

### Параметры

- **fullyQualifiedSymbolName** (required): FQN интерфейса, базового класса или абстрактного метода

### Что показывает

**Для интерфейсов:**
- Все классы, реализующие интерфейс
- Файлы и строки определения

**Для базовых классов:**
- Все производные классы
- Вся иерархия наследования

**Для абстрактных методов:**
- Все override реализации
- FQN для дальнейшего анализа

### Пример вывода

```
Implementations of: MyProject.Data.IUserRepository

═══════════════════════════════════════════════════════════
📦 IMPLEMENTATIONS (3 found)
═══════════════════════════════════════════════════════════

1. MyProject.Data.SqlUserRepository
   File: D:/MyProject/src/Data/SqlUserRepository.cs:15
   FQN: MyProject.Data.SqlUserRepository
   Access: public class

2. MyProject.Data.InMemoryUserRepository
   File: D:/MyProject/src/Data/InMemoryUserRepository.cs:8
   FQN: MyProject.Data.InMemoryUserRepository
   Access: public class

3. MyProject.Tests.Mocks.MockUserRepository
   File: D:/MyProject/tests/Mocks/MockUserRepository.cs:5
   FQN: MyProject.Tests.Mocks.MockUserRepository
   Access: internal class

💡 Use UltrasharpTool_ViewDefinition to see implementation details
💡 Example: UltrasharpTool_ViewDefinition("MyProject.Data.SqlUserRepository")
```

### Когда использовать

✅ **Для анализа полиморфизма:**
- Найти все реализации интерфейса
- Понять как используется dependency injection
- Найти mock/test реализации

✅ **Для рефакторинга:**
- Найти все классы которые надо изменить
- Проверить impact изменения интерфейса
- Найти дублирующиеся реализации

✅ **Для архитектурного анализа:**
- Построить иерархию наследования
- Найти нарушения принципов SOLID
- Понять dependency graph

### Когда НЕ использовать

❌ **НЕ нужен для:**
- Поиска где используется тип → `FindReferences`
- Просмотра реализации конкретного класса → `ViewDefinition`

### Best Practices

1. **Анализируйте найденные реализации:**
   ```javascript
   // 1. Находим все реализации
   UltrasharpTool_ListImplementations("IUserService")

   // 2. Смотрим каждую реализацию
   UltrasharpTool_ViewDefinition("UserService")
   UltrasharpTool_ViewDefinition("CachedUserService")
   UltrasharpTool_ViewDefinition("MockUserService")
   ```

2. **Для базовых классов - проверьте всю иерархию:**
   ```javascript
   UltrasharpTool_ListImplementations("BaseController")
   // Найдёт: UserController, OrderController, ProductController

   // Проверьте каждый на консистентность
   ```

3. **Комбинируйте с AnalyzeComplexity:**
   ```javascript
   UltrasharpTool_ListImplementations("IService")
   // Output: ServiceA, ServiceB, ServiceC

   UltrasharpTool_AnalyzeComplexity(scope: "class", target: "ServiceA")
   UltrasharpTool_AnalyzeComplexity(scope: "class", target: "ServiceB")
   // Найдите самую сложную реализацию для рефакторинга
   ```

### Производительность

- **Скорость:** 0.5-2 сек (зависит от размера codebase)
- **Parallel:** Поиск по проектам параллельный

### Связанные инструменты

- ➡️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см реализацию найденного класса
- ➡️ [**FindReferences**](#UltrasharpTool_findreferences) — где используется интерфейс
- ➡️ [**AnalyzeComplexity**](#UltrasharpTool_analyzecomplexity) — сравнить сложность реализаций

---

## UltrasharpTool_FindReferences

**Поиск всех использований** — находит все места где используется символ (метод, свойство, класс, etc.) с контекстом кода.

### Использование

```javascript
UltrasharpTool_FindReferences(
fullyQualifiedSymbolName: "MyNamespace.MyClass.MyMethod"
)
```

### Параметры

- **fullyQualifiedSymbolName** (required): FQN символа для поиска

### Что показывает

Для каждого места использования:
- 📁 **Файл и номер строки**
- 📝 **Контекстный код** (несколько строк вокруг)
- 🔍 **Тип использования** (вызов, присваивание, инициализация, etc.)
- 📊 **Группировка по файлам**

### Пример вывода

```
References to: MyProject.Services.UserService.CreateUserAsync

Found 15 references in 8 files

═══════════════════════════════════════════════════════════
📁 MyProject.API/Controllers/UserController.cs (3 references)
═══════════════════════════════════════════════════════════

Line 78:
[HttpPost]
public async Task<IActionResult> Post([FromBody] CreateUserRequest request)
{
var userId = await _userService.CreateUserAsync(
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
request.Name,
request.Email
);
return Ok(new { UserId = userId });
}

Line 156:
var newUserId = await _userService.CreateUserAsync(
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
importedUser.Name,
importedUser.Email
);

═══════════════════════════════════════════════════════════
📁 MyProject.Tests/UserServiceTests.cs (5 references)
═══════════════════════════════════════════════════════════

Line 45:
[Fact]
public async Task CreateUser_ValidData_ReturnsUserId()
{
var userId = await _service.CreateUserAsync("John Doe", "john@example.com");
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
Assert.True(userId > 0);
}

Line 67:
await Assert.ThrowsAsync<ArgumentException>(
() => _service.CreateUserAsync("", "test@example.com")
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
);

... (10 more references)

💡 Total: 15 references across 8 files
```

### Когда использовать

✅ **Для понимания impact:**
- Перед удалением метода
- Перед изменением сигнатуры
- Понять где и как используется API

✅ **Для рефакторинга:**
- Найти все места для обновления
- Проверить consistency использования
- Найти неправильное использование

✅ **Для отладки:**
- Откуда вызывается проблемный метод
- Какие параметры передаются
- Понять call chain

✅ **Для документации:**
- Примеры использования API
- Типичные паттерны вызовов

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Нужна только реализация → `ViewDefinition`
- Нужен call graph одного метода → `ViewDefinition` (показывает top 3)
- Ищете общий паттерн → `SearchDefinitions`

### Best Practices

1. **Проверяйте перед breaking changes:**
   ```javascript
   // Хотим изменить сигнатуру метода
   UltrasharpTool_FindReferences("UserService.CreateUser")
   // Видим 47 references - много работы!

   // Может лучше создать новый метод?
   UltrasharpTool_AddMember("UserService", "CreateUserV2(...)")
   ```

2. **Для анализа паттернов использования:**
   ```javascript
   UltrasharpTool_FindReferences("IUserRepository.GetByIdAsync")
   // Смотрим контекст каждого использования
   // Находим anti-patterns (N+1 queries, etc.)
   ```

3. **Комбинируйте с TraceBackwards:**
   ```javascript
   // Нашли баг в методе
   UltrasharpTool_FindReferences("ProblematicMethod")
   // Видим все call sites

   UltrasharpTool_TraceBackwards(
   crashPointFqn: "ProblematicMethod",
   stackTraceHints: [...]
   )
   // Видим полный call path
   ```

### Производительность

- **Малый проект:** 0.5-1 сек
- **Средний проект:** 2-5 сек
- **Большой проект:** 5-15 сек

**Оптимизации:**
- ✅ Early termination при достижении лимита
- ✅ Параллельный поиск по проектам
- ✅ Кэширование SemanticModel

### Типичные ошибки

#### ❌ Ошибка: "Too many references"
```
WARNING: Found 5000+ references, showing first 1000
```
**Решение:**
- Это нормально для базовых типов (string, int)
- Уточните поиск (конкретный метод, а не класс)

### Связанные инструменты

- ⬅️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см реализацию символа
- ➡️ [**TraceBackwards**](TRACING_TOOLS.md#UltrasharpTool_tracebackwards) — полный call path
- ➡️ [**RenameSymbol**](MODIFICATION_TOOLS.md#UltrasharpTool_renamesymbol) — переименовать везде

---

## UltrasharpTool_SearchDefinitions

**Regex поиск по определениям** — ищет паттерны в сигнатурах, именах типов/методов, декларациях. Работает как в исходниках, так и в compiled assemblies.

### Использование

```javascript
UltrasharpTool_SearchDefinitions(
regexPattern: ".*UserService.*"
)
```

### Параметры

- **regexPattern** (required): Regex паттерн для поиска (multiline mode)

### Что показывает

Для каждого совпадения:
- 🔍 **FQN** найденного символа
- 📄 **Полная декларация** (сигнатура)
- 📁 **Расположение** (файл:строка или assembly)
- 🏷️ **Тип символа** (class, method, property, interface)

### Пример вывода

```
Search results for pattern: .*Validate.*Email.*

Found 12 matches

═══════════════════════════════════════════════════════════
📦 METHODS (8 matches)
═══════════════════════════════════════════════════════════

1. MyProject.Services.UserService.ValidateEmail
   Signature: private bool ValidateEmail(string email)
   File: Services/UserService.cs:156
   FQN: MyProject.Services.UserService.ValidateEmail(string)

2. MyProject.Utilities.EmailValidator.ValidateEmailFormat
   Signature: public static bool ValidateEmailFormat(string email)
   File: Utilities/EmailValidator.cs:23
   FQN: MyProject.Utilities.EmailValidator.ValidateEmailFormat(string)

3. MyProject.API.Validators.UserRequestValidator.ValidateEmailAddress
   Signature: protected bool ValidateEmailAddress(string email)
   File: API/Validators/UserRequestValidator.cs:45
   FQN: MyProject.API.Validators.UserRequestValidator.ValidateEmailAddress(string)

═══════════════════════════════════════════════════════════
📦 CLASSES (2 matches)
═══════════════════════════════════════════════════════════

4. MyProject.Validators.EmailValidator
   Declaration: public class EmailValidator : IValidator<string>
   File: Validators/EmailValidator.cs:8
   FQN: MyProject.Validators.EmailValidator

═══════════════════════════════════════════════════════════
📦 EXTERNAL (from compiled assemblies) (2 matches)
═══════════════════════════════════════════════════════════

5. FluentValidation.Validators.EmailValidator
   Assembly: FluentValidation.dll
   FQN: FluentValidation.Validators.EmailValidator

💡 Use UltrasharpTool_ViewDefinition to see full source code
💡 Example: UltrasharpTool_ViewDefinition("MyProject.Services.UserService.ValidateEmail")
```

### Когда использовать

✅ **Для поиска паттернов:**
- Найти все async методы
- Найти все методы содержащие "Validate"
- Найти naming violations (неправильный naming convention)

✅ **Для рефакторинга:**
- Найти дублирующуюся функциональность
- Найти устаревшие паттерны
- Найти inconsistent naming

✅ **Для архитектурного анализа:**
- Найти все Controllers
- Найти все Repositories
- Найти все классы оканчивающиеся на "Service"

✅ **Для code review:**
- Найти использование deprecated API
- Найти potential security issues
- Проверить соблюдение conventions

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Знаете точный FQN → `ViewDefinition`
- Ищете использования символа → `FindReferences`
- Нужен полный текстовый поиск в коде → используйте grep/IDE

### Best Practices

1. **Используйте конкретные паттерны:**
   ```javascript
   // ❌ Слишком общий - много результатов
   UltrasharpTool_SearchDefinitions("User")

   // ✅ Конкретный - нужные результаты
   UltrasharpTool_SearchDefinitions("class.*UserService")
   UltrasharpTool_SearchDefinitions("async.*User.*Repository")
   ```

2. **Для поиска naming violations:**
   ```javascript
   // Найти async методы без "Async" суффикса
   UltrasharpTool_SearchDefinitions("async Task.*(?!Async)\\(")

   // Найти public методы начинающиеся с "_"
   UltrasharpTool_SearchDefinitions("public.*\\s_\\w+\\(")
   ```

3. **Комбинируйте с AnalyzeComplexity:**
   ```javascript
   // Найти все "Service" классы
   UltrasharpTool_SearchDefinitions("class.*Service")

   // Проанализировать сложность каждого
   UltrasharpTool_AnalyzeComplexity(scope: "class", target: "UserService")
   ```

### Regex паттерны (примеры)

```javascript
// Все async методы
"async Task.*"

// Все методы содержащие "Validate"
".*Validate.*\\("

// Все классы заканчивающиеся на "Controller"
"class.*Controller\\s*:"

// Все интерфейсы начинающиеся с "I"
"interface I\\w+"

// Все свойства типа List<>
"List<.*>.*\\{.*get"

// Все методы с атрибутом [HttpPost]
"\\[HttpPost\\].*"
```

### Производительность

- **Source code:** 2-5 сек (быстро)
- **Compiled assemblies:** 5-15 сек (медленнее, decompilation)

**Dual-engine:**
1. Поиск в source code (Roslyn Syntax API)
2. Поиск в compiled assemblies (Reflection + Decompilation)

### Связанные инструменты

- ➡️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см детали найденного
- ➡️ [**FindReferences**](#UltrasharpTool_findreferences) — где используется найденный символ
- ➡️ [**AnalyzeComplexity**](#UltrasharpTool_analyzecomplexity) — проанализировать найденные методы

---

## UltrasharpTool_ManageUsings

**Управление using directives** — чтение и запись using statements в файле.

### Использование

```javascript
// Чтение
UltrasharpTool_ManageUsings(
operation: "read",
codeToWrite: "None",
filePath: "D:/MyProject/src/Services/UserService.cs"
)

// Запись
UltrasharpTool_ManageUsings(
operation: "write",
codeToWrite: "using System;\nusing System.Linq;\nusing MyProject.Domain;",
filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Параметры

- **operation** (required): `"read"` или `"write"`
- **codeToWrite** (required): Для read: `"None"`, для write: полный список usings
- **filePath** (required): Полный путь к .cs файлу

### Что делает

**Read:**
- Возвращает все текущие using directives
- Показывает порядок

**Write:**
- **ЗАМЕНЯЕТ** все using directives на указанные
- Автоматически форматирует
- Создаёт Git commit

### Пример вывода (read)

```
Using directives in: Services/UserService.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyProject.Domain.Entities;
using MyProject.Data.Interfaces;
```

### Когда использовать

✅ **Для добавления зависимостей:**
- Добавить using для нового типа
- Добавить using для extension methods

✅ **Для очистки:**
- Удалить неиспользуемые usings (лучше через ApplyCodeFixes)
- Упорядочить usings

### Когда НЕ использовать

❌ **Используйте вместо:**
- [**ApplyCodeFixes**](QUALITY_TOOLS.md#UltrasharpTool_applycodefixes) — для удаления unused usings (автоматически)
- [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — для упорядочивания

### Best Practices

1. **Сначала read, потом write:**
   ```javascript
   // ✅ Правильно - сохраняем существующие
   UltrasharpTool_ManageUsings(operation: "read", codeToWrite: "None", filePath: "...")
   // Output: using System; using System.Linq;

   // Добавляем новый using
   UltrasharpTool_ManageUsings(
   operation: "write",
   codeToWrite: "using System;\nusing System.Linq;\nusing MyProject.NewNamespace;",
   filePath: "..."
   )
   ```

2. **Лучше используйте ApplyCodeFixes:**
   ```javascript
   // ❌ Ручное управление usings
   UltrasharpTool_ManageUsings(...)

   // ✅ Автоматическое удаление unused
   UltrasharpTool_ApplyCodeFixes(diagnosticId: "IDE0005")
   ```

### Связанные инструменты

- ➡️ [**ApplyCodeFixes**](QUALITY_TOOLS.md#UltrasharpTool_applycodefixes) — автоудаление unused usings
- ➡️ [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — упорядочивание usings

---

## UltrasharpTool_ManageAttributes

**Управление атрибутами** — чтение и запись attributes на декларациях (class, method, property, etc.).

### Использование

```javascript
// Чтение
UltrasharpTool_ManageAttributes(
operation: "read",
codeToWrite: "None",
targetDeclaration: "MyNamespace.MyClass.MyMethod"
)

// Запись
UltrasharpTool_ManageAttributes(
operation: "write",
codeToWrite: "[Obsolete(\"Use NewMethod instead\")]\n[EditorBrowsable(EditorBrowsableState.Never)]",
targetDeclaration: "MyNamespace.MyClass.MyMethod"
)
```

### Параметры

- **operation** (required): `"read"` или `"write"`
- **codeToWrite** (required): Для read: `"None"`, для write: все атрибуты
- **targetDeclaration** (required): FQN декларации (type, method, property, field)

### Что делает

**Read:**
- Возвращает все текущие attributes

**Write:**
- **ЗАМЕНЯЕТ** все attributes на указанные
- Создаёт Git commit

### Пример вывода (read)

```
Attributes on: MyProject.API.Controllers.UserController.GetUser

[HttpGet("{id}")]
[ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[Authorize(Roles = "Admin,User")]
```

### Когда использовать

✅ **Для добавления metadata:**
- Добавить [Obsolete]
- Добавить API documentation attributes
- Добавить validation attributes

✅ **Для изменения конфигурации:**
- Изменить routing ([HttpGet], [Route])
- Изменить authorization ([Authorize])
- Изменить serialization ([JsonProperty])

### Best Practices

1. **Сначала read, потом write:**
   ```javascript
   // Read current
   UltrasharpTool_ManageAttributes(operation: "read", codeToWrite: "None", targetDeclaration: "...")

   // Write updated
   UltrasharpTool_ManageAttributes(operation: "write", codeToWrite: "[Existing]\n[NewAttribute]", ...)
   ```

2. **Полный список атрибутов:**
   ```javascript
   // ❌ Плохо - потеряете существующие
   UltrasharpTool_ManageAttributes(operation: "write", codeToWrite: "[NewAttribute]", ...)

   // ✅ Хорошо - сохраняете все
   UltrasharpTool_ManageAttributes(operation: "write", codeToWrite: "[Existing1]\n[Existing2]\n[NewAttribute]", ...)
   ```

### Связанные инструменты

- ⬅️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см текущие attributes
- ➡️ [**OverwriteMember**](MODIFICATION_TOOLS.md#UltrasharpTool_overwritemember) — для больших изменений

---

## UltrasharpTool_AnalyzeComplexity

**Анализ метрик сложности** — вычисляет cyclomatic complexity, cognitive complexity, coupling, inheritance depth, method statistics.

### Использование

```javascript
// Анализ метода
UltrasharpTool_AnalyzeComplexity(
scope: "method",
target: "MyNamespace.MyClass.MyMethod"
)

// Анализ класса
UltrasharpTool_AnalyzeComplexity(
scope: "class",
target: "MyNamespace.MyClass"
)

// Анализ проекта
UltrasharpTool_AnalyzeComplexity(
scope: "project",
target: "MyProject.Core"
)
```

### Параметры

- **scope** (required): `"method"`, `"class"`, или `"project"`
- **target** (required): FQN метода/класса или имя проекта

### Что показывает

**Для метода:**
- 🔢 **Cyclomatic Complexity** (количество путей выполнения)
- 🧠 **Cognitive Complexity** (сложность понимания)
- 📏 **Lines of Code**
- 🔀 **Branch Count** (if/switch/loop)
- 📊 **Expression Depth** (вложенность)

**Для класса:**
- 📊 **Метрики всех методов** (top 10 сложных)
- 🔗 **Coupling** (зависимости от других типов)
- 📈 **Inheritance Depth**
- 📦 **Member Count**
- ⚠️ **Warnings** (высокая сложность)

**Для проекта:**
- 📊 **Сводная статистика** по всем классам
- 🏆 **Top 20 самых сложных методов**
- 🏆 **Top 20 самых сложных классов**
- 📈 **Распределение complexity**
- ⚠️ **Hotspots** для рефакторинга

### Пример вывода (метод)

```
Complexity Analysis: MyProject.Services.UserService.ProcessUserOrder

═══════════════════════════════════════════════════════════
📊 METHOD COMPLEXITY
═══════════════════════════════════════════════════════════

Cyclomatic Complexity: 15   ⚠️ HIGH (threshold: 10)
Cognitive Complexity:  23   🔴 VERY HIGH (threshold: 15)
Lines of Code:         87
Branch Count:          12   (if: 7, switch: 2, loops: 3)
Max Expression Depth:  5
Parameters:            4

⚠️ RECOMMENDATION: This method is too complex!
   Consider refactoring into smaller methods.

Complexity breakdown:
- if statements: +7
- switch cases: +3
- loops: +3
- nested conditions: +10

💡 Target: Cyclomatic < 10, Cognitive < 15
```

### Пример вывода (класс)

```
Complexity Analysis: MyProject.Services.OrderProcessingService

═══════════════════════════════════════════════════════════
📊 CLASS METRICS
═══════════════════════════════════════════════════════════

Total Methods: 23
Total Properties: 8
Total Fields: 5
Lines of Code: 1,247
Inheritance Depth: 2   (OrderProcessingService → BaseService → Object)

Coupling (Afferent): 15 types depend on this class
Coupling (Efferent): 28 types used by this class
Instability: 0.65   (Efferent / (Afferent + Efferent))

═══════════════════════════════════════════════════════════
📊 TOP 10 MOST COMPLEX METHODS
═══════════════════════════════════════════════════════════

1. ProcessOrder           CC: 18, CogC: 27  🔴 VERY HIGH
2. ValidateOrderItems     CC: 12, CogC: 19  ⚠️ HIGH
3. CalculateDiscount      CC: 11, CogC: 16  ⚠️ HIGH
4. ApplyPromoCode         CC:  9, CogC: 14  ✅ OK
5. GenerateInvoice        CC:  8, CogC: 11  ✅ OK
...

⚠️ 3 methods exceed complexity thresholds
💡 Focus refactoring on: ProcessOrder, ValidateOrderItems
```

### Когда использовать

✅ **Для поиска технического долга:**
- Найти самые сложные методы
- Найти классы с high coupling
- Приоритизировать рефакторинг

✅ **Перед рефакторингом:**
- Измерить baseline complexity
- Выбрать target для рефакторинга
- Проверить improvement после рефакторинга

✅ **Для code review:**
- Проверить новый код на сложность
- Enforceсложность standards
- Предотвратить появление "god classes"

✅ **Для архитектурного анализа:**
- Найти тесное coupling
- Найти нарушения single responsibility
- Оценить maintainability

### Когда НЕ использовать

❌ **НЕ абсолютная метрика:**
- Высокая сложность != плохой код (иногда неизбежна)
- Низкая сложность != хороший код (может быть слишком раздроблен)

### Пороговые значения

**Cyclomatic Complexity:**
- ✅ 1-10: Простой, легко тестируемый
- ⚠️ 11-20: Умеренно сложный, требует внимания
- 🔴 21+: Очень сложный, требует рефакторинга

**Cognitive Complexity:**
- ✅ 1-15: Легко понять
- ⚠️ 16-25: Требует усилий для понимания
- 🔴 26+: Сложно понять и поддерживать

**Coupling (Instability):**
- ✅ 0.0-0.3: Stable (мало зависимостей)
- ⚠️ 0.3-0.7: Balanced
- 🔴 0.7-1.0: Unstable (много зависимостей)

### Best Practices

1. **Регулярно анализируйте весь проект:**
   ```javascript
   UltrasharpTool_AnalyzeComplexity(scope: "project", target: "MyProject.Core")
   // Найдите top 20 hotspots
   // Создайте план рефакторинга
   ```

2. **Измеряйте до и после рефакторинга:**
   ```javascript
   // Before
   UltrasharpTool_AnalyzeComplexity(scope: "method", target: "ComplexMethod")
   // CC: 25, CogC: 38

   // Refactor...

   // After
   UltrasharpTool_AnalyzeComplexity(scope: "method", target: "RefactoredMethod")
   // CC: 8, CogC: 12 ✅
   ```

3. **Фокусируйтесь на cognitive, не только cyclomatic:**
   ```javascript
   // Cyclomatic может быть обманчив
   // Cognitive лучше отражает сложность понимания
   ```

### Производительность

- **Method:** < 100 мс
- **Class:** 0.5-2 сек
- **Project:** 10-60 сек (зависит от размера)

### Связанные инструменты

- ⬅️ [**ViewDefinition**](#UltrasharpTool_viewdefinition) — см код сложного метода
- ⬅️ [**SearchDefinitions**](#UltrasharpTool_searchdefinitions) — найти все методы для анализа
- ➡️ [**OverwriteMember**](MODIFICATION_TOOLS.md#UltrasharpTool_overwritemember) — рефакторинг сложного метода

---

## Сравнение инструментов анализа

| Инструмент | Что находит | Скорость | Use case |
|------------|-------------|----------|----------|
| **GetMembers** | Все члены типа | < 100 мс | API overview |
| **ViewDefinition** | Исходный код + call graph | 1-3 сек | Понять реализацию |
| **ListImplementations** | Наследники/реализации | 0.5-2 сек | Полиморфизм |
| **FindReferences** | Все использования | 2-15 сек | Impact analysis |
| **SearchDefinitions** | Regex паттерны | 2-15 сек | Найти naming/patterns |
| **AnalyzeComplexity** | Метрики сложности | 0.1-60 сек | Технический долг |

---

## Типичные сценарии использования

### Изучение нового класса

```javascript
// 1. Быстрый обзор публичного API
UltrasharpTool_GetMembers("MyNamespace.UserService", includePrivateMembers: false)

// 2. Детали интересующего метода
UltrasharpTool_ViewDefinition("MyNamespace.UserService.CreateUser")

// 3. Где этот метод используется
UltrasharpTool_FindReferences("MyNamespace.UserService.CreateUser")

// 4. Проверить сложность
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "MyNamespace.UserService")
```

### Подготовка к рефакторингу

```javascript
// 1. Найти самые сложные методы в проекте
UltrasharpTool_AnalyzeComplexity(scope: "project", target: "MyProject.Core")

// 2. Детали самого сложного метода
UltrasharpTool_ViewDefinition("MyProject.Services.ComplexMethod")

// 3. Все места где используется
UltrasharpTool_FindReferences("MyProject.Services.ComplexMethod")

// 4. План рефакторинга...
```

### Анализ интерфейса и реализаций

```javascript
// 1. Посмотреть контракт интерфейса
UltrasharpTool_GetMembers("IUserRepository", includePrivateMembers: false)

// 2. Найти все реализации
UltrasharpTool_ListImplementations("IUserRepository")

// 3. Сравнить реализации по сложности
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "SqlUserRepository")
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "InMemoryUserRepository")

// 4. Детали конкретной реализации
UltrasharpTool_ViewDefinition("SqlUserRepository.GetByIdAsync")
```

### Поиск дублирующейся логики

```javascript
// 1. Найти все методы содержащие "Validate"
UltrasharpTool_SearchDefinitions(".*Validate.*Email.*")

// 2. Посмотреть реализацию каждого
UltrasharpTool_ViewDefinition("UserService.ValidateEmail")
UltrasharpTool_ViewDefinition("EmailValidator.ValidateEmailFormat")

// 3. Найти где используются
UltrasharpTool_FindReferences("UserService.ValidateEmail")
UltrasharpTool_FindReferences("EmailValidator.ValidateEmailFormat")

// 4. Решить какой оставить, объединить дублирующуюся логику
```

### Code Review нового кода

```javascript
// 1. Проверить публичный API
UltrasharpTool_GetMembers("NewFeature.NewService", includePrivateMembers: false)

// 2. Проверить реализацию ключевых методов
UltrasharpTool_ViewDefinition("NewFeature.NewService.ProcessData")

// 3. Проверить сложность
UltrasharpTool_AnalyzeComplexity(scope: "class", target: "NewFeature.NewService")

// 4. Проверить naming conventions
UltrasharpTool_SearchDefinitions("NewFeature.*(?!Async).*async Task")
```

---

## См. также

- 📚 [**SOLUTION_TOOLS.md**](SOLUTION_TOOLS.md) — LoadSolution, LoadProject
- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — изменение кода
- 📚 [**TRACING_TOOLS.md**](TRACING_TOOLS.md) — трейсинг выполнения
- 📚 [**QUALITY_TOOLS.md**](QUALITY_TOOLS.md) — форматирование и линтинг
- 📚 [**README.md**](../README.md) — главная документация

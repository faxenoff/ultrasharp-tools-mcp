# Инструменты работы с файлами

**Прямая работа с файлами решения** — чтение, создание, перезапись документов. Используйте когда нужен полный контроль над файлом, а не отдельными символами.

## 📋 Quick Reference

| Инструмент | Назначение | Git автоматизация | Когда использовать |
|------------|-----------|------------------|-------------------|
| **ReadRawFromRoslynDocument** | Прочитать файл целиком | Нет | Просмотр конфигураций, full file context |
| **ReadTypesFromRoslynDocument** | Список типов и членов в файле | Нет | Навигация по файлу, architectural overview |
| **CreateRoslynDocument** | Создать новый файл | ✅ Branch + Commit | Новые классы, конфигурации |
| **OverwriteRoslynDocument** | Перезаписать весь файл | ✅ Branch + Commit | Массовые изменения файла |

---

## Когда использовать Document Tools vs Symbol Tools

### ✅ Document Tools (эти инструменты)
- Работа с конфигурационными файлами (appsettings.json, .csproj)
- Создание новых классов/файлов
- Массовая перезапись файла
- Просмотр файла целиком для контекста

### ✅ Symbol Tools (ANALYSIS_TOOLS, MODIFICATION_TOOLS)
- Работа с C# кодом через semantic analysis
- Точечные изменения (метод, класс)
- Переименование с обновлением references
- Рефакторинг с проверкой компиляции

**Правило большого пальца:**
- Для **.cs файлов с кодом** → используйте Symbol Tools (ViewDefinition, AddMember, etc.)
- Для **non-code файлов** или **full file operations** → используйте Document Tools

---

## UltrasharpTool_ReadRawFromRoslynDocument

**Чтение файла целиком** — возвращает полное содержимое файла без отступов (token efficient).

### Использование

```javascript
UltrasharpTool_ReadRawFromRoslynDocument(
filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Параметры

- **filePath** (required): Полный абсолютный путь к файлу

### Что показывает

- 📄 **Весь content файла** (без отступов для экономии токенов)
- 📁 **Путь к файлу**
- 📊 **Статистика** (строки, символы)

### Пример вывода

```
File: D:/MyProject/src/Services/UserService.cs
Lines: 247
Characters: 8,542

═══════════════════════════════════════════════════════════
📄 CONTENT (indentation omitted)
═══════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyProject.Domain;
using MyProject.Data;

namespace MyProject.Services
{
/// <summary>
/// Service for managing users.
/// </summary>
public class UserService
{
private readonly IUserRepository _repository;
private readonly ILogger<UserService> _logger;

public UserService(IUserRepository repository, ILogger<UserService> logger)
{
_repository = repository;
_logger = logger;
}

public async Task<User> GetUserByIdAsync(int userId)
{
... (rest of file)
```

### Когда использовать

✅ **Для понимания контекста:**
- Увидеть весь файл целиком
- Понять структуру до модификации
- Проверить using statements
- Посмотреть namespace

✅ **Для non-code файлов:**
- appsettings.json
- .csproj files
- .editorconfig
- Любые конфигурационные файлы

✅ **Перед OverwriteRoslynDocument:**
- Прочитать текущее содержимое
- Модифицировать вне SharpTools
- Записать обратно

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужен только один класс из файла → `ViewDefinition`
- Нужен список типов → `ReadTypesFromRoslynDocument`
- Файл очень большой (>1000 строк) → используйте ViewDefinition для конкретных символов

### Best Practices

1. **Для .cs файлов предпочитайте ViewDefinition:**
   ```javascript
   // ❌ Плохо - читаем весь файл (500 строк)
   UltrasharpTool_ReadRawFromRoslynDocument("UserService.cs")

   // ✅ Хорошо - читаем только нужный класс
   UltrasharpTool_ViewDefinition("MyNamespace.UserService")
   ```

2. **Для конфигураций - ReadRaw идеален:**
   ```javascript
   // ✅ Хорошо
   UltrasharpTool_ReadRawFromRoslynDocument("appsettings.json")
   UltrasharpTool_ReadRawFromRoslynDocument("MyProject.csproj")
   ```

3. **Используйте абсолютные пути:**
   ```javascript
   // ✅ Правильно
   filePath: "D:/MyProject/src/Services/UserService.cs"

   // ❌ Неправильно - относительные пути могут не работать
   filePath: "../Services/UserService.cs"
   ```

### Производительность

- **Малый файл (< 100 строк):** < 50 мс
- **Средний файл (100-500 строк):** 50-200 мс
- **Большой файл (500-2000 строк):** 200-500 мс

**Token usage:**
- Без отступов: ~10% экономия токенов
- Для файла 500 строк: ~3000-5000 токенов

### Связанные инструменты

- ➡️ [**ReadTypesFromRoslynDocument**](#UltrasharpTool_readtypesfromroslyndocument) — для навигации по типам в файле
- ➡️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — для чтения конкретного символа
- ➡️ [**OverwriteRoslynDocument**](#UltrasharpTool_overwriteroslyndocument) — для перезаписи файла

---

## UltrasharpTool_ReadTypesFromRoslynDocument

**Структурная карта файла** — возвращает иерархию типов и их членов в конкретном файле.

### Использование

```javascript
UltrasharpTool_ReadTypesFromRoslynDocument(
filePath: "D:/MyProject/src/Services/UserService.cs"
)
```

### Параметры

- **filePath** (required): Полный абсолютный путь к .cs файлу

### Что показывает

**Для каждого типа в файле:**
- 📦 **Namespace**
- 🏷️ **Тип** (class, interface, struct, enum)
- 📋 **Все члены** с сигнатурами
- 🔒 **Access modifiers**
- 🆔 **FQN** для использования с другими инструментами

### Пример вывода

```
File: D:/MyProject/src/Services/UserService.cs
Types found: 2

═══════════════════════════════════════════════════════════
📦 MyProject.Services
═══════════════════════════════════════════════════════════

[1] public class UserService
    FQN: MyProject.Services.UserService

    CONSTRUCTORS:
    - public UserService(IUserRepository repository, ILogger<UserService> logger)
      FQN: MyProject.Services.UserService..ctor(IUserRepository, ILogger<UserService>)

    METHODS:
    - public async Task<User> GetUserByIdAsync(int userId)
      FQN: MyProject.Services.UserService.GetUserByIdAsync(int)

    - public async Task<int> CreateUserAsync(string name, string email)
      FQN: MyProject.Services.UserService.CreateUserAsync(string, string)

    - private bool ValidateEmail(string email)
      FQN: MyProject.Services.UserService.ValidateEmail(string)

    PROPERTIES:
    - public ILogger<UserService> Logger { get; }
      FQN: MyProject.Services.UserService.Logger

    FIELDS:
    - private readonly IUserRepository _repository
      FQN: MyProject.Services.UserService._repository

[2] internal class UserServiceException : Exception
    FQN: MyProject.Services.UserServiceException

    CONSTRUCTORS:
    - public UserServiceException(string message) : base(message)
      FQN: MyProject.Services.UserServiceException..ctor(string)

💡 Use UltrasharpTool_ViewDefinition with FQN to see source code
💡 Example: UltrasharpTool_ViewDefinition("MyProject.Services.UserService.GetUserByIdAsync")
```

### Когда использовать

✅ **Для навигации по файлу:**
- Файл содержит несколько классов
- Нужно понять структуру файла
- Получить FQN для дальнейшего анализа

✅ **Для архитектурного анализа:**
- Понять организацию кода в файле
- Найти helper classes
- Проверить соответствие conventions (один класс на файл)

✅ **Переход от "file domain" к "type domain":**
- Вы знаете файл, но не знаете точные FQN типов
- Хотите использовать Symbol Tools (ViewDefinition, GetMembers)

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Знаете FQN типа → `ViewDefinition` напрямую
- Нужен весь файл целиком → `ReadRawFromRoslynDocument`
- Нужна реализация метода → `ViewDefinition`

### Best Practices

1. **Используйте для multi-class файлов:**
   ```javascript
   // Файл содержит UserService, UserServiceException, UserServiceExtensions
   UltrasharpTool_ReadTypesFromRoslynDocument("UserService.cs")
   // Получаем FQN всех трёх типов

   // Затем смотрим детали каждого
   UltrasharpTool_ViewDefinition("MyProject.Services.UserService")
   UltrasharpTool_ViewDefinition("MyProject.Services.UserServiceException")
   ```

2. **Альтернатива LoadProject:**
   ```javascript
   // Вместо LoadProject для всего проекта
   UltrasharpTool_LoadProject("MyProject.Core")  // Все файлы

   // Можно использовать ReadTypes для конкретного файла
   UltrasharpTool_ReadTypesFromRoslynDocument("UserService.cs")  // Один файл
   ```

### Производительность

- **Скорость:** 100-500 мс (зависит от размера файла)
- **Faster than:** ReadRaw для навигации (не загружает весь content)

### Связанные инструменты

- ⬅️ [**LoadProject**](SOLUTION_TOOLS.md#UltrasharpTool_loadproject) — для overview всего проекта
- ➡️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — детали конкретного типа
- ➡️ [**GetMembers**](ANALYSIS_TOOLS.md#UltrasharpTool_getmembers) — члены конкретного типа

---

## UltrasharpTool_CreateRoslynDocument

**Создание нового файла** — создаёт новый файл с указанным содержимым.

### Использование

```javascript
UltrasharpTool_CreateRoslynDocument(
filePath: "D:/MyProject/src/Validators/EmailValidator.cs",
content: `
using System;
using System.Text.RegularExpressions;

namespace MyProject.Validators
{
/// <summary>
/// Validates email addresses.
/// </summary>
public class EmailValidator
{
private static readonly Regex EmailRegex = new Regex(
@"^[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+$",
RegexOptions.Compiled
);

public static bool IsValid(string email)
{
return !string.IsNullOrWhiteSpace(email) &&
EmailRegex.IsMatch(email);
}
}
}`,
commitMessage: "Add EmailValidator class"
)
```

### Параметры

- **filePath** (required): Полный абсолютный путь нового файла
- **content** (required): Содержимое файла (без отступов, будет auto-formatted)
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. ✅ Проверяет что файл не существует
2. 📁 Создаёт директории если нужно
3. 📝 Записывает content
4. 🎨 Форматирует (для .cs файлов)
5. ✅ Проверяет компиляцию (для .cs файлов)
6. 🌳 Git branch + commit
7. 📊 Возвращает статус

### Пример вывода

```
✅ Document created successfully

File: D:/MyProject/src/Validators/EmailValidator.cs
Lines: 24
Type: C# source file

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

No errors found.
File successfully added to project.

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-150234
Commit: d4e9f23 "Add EmailValidator class"

💡 Use UltrasharpTool_ViewDefinition to verify the new type
💡 Example: UltrasharpTool_ViewDefinition("MyProject.Validators.EmailValidator")
```

### Когда использовать

✅ **Для новых классов/файлов:**
- Создать новый сервис
- Создать новый контроллер
- Создать helper class
- Создать интерфейс

✅ **Для конфигурационных файлов:**
- Новый appsettings.{env}.json
- Новый .editorconfig
- Документация (.md файлы)

✅ **Для тестов:**
- Создать новый test file
- Создать mock class

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Добавляете в существующий класс → `AddMember`
- Файл уже существует → `OverwriteRoslynDocument` (но осторожно!)
- Создаёте partial class в существующем файле → используйте новый файл

### Best Practices

1. **Пишите без отступов (auto-format):**
   ```javascript
   // ✅ Правильно - код будет отформатирован
   content: `
   namespace MyNamespace
   {
   public class MyClass
   {
   public void MyMethod()
   {
   Console.WriteLine("Test");
   }
   }
   }`
   ```

2. **Включайте полный namespace и usings:**
   ```javascript
   // ✅ Правильно - самодостаточный файл
   content: `
   using System;
   using MyProject.Domain;

   namespace MyProject.Services
   {
   public class NewService { ... }
   }`
   ```

3. **Проверьте что файл не существует:**
   ```javascript
   // CreateRoslynDocument вернёт ошибку если файл существует
   // Используйте Glob для проверки:
   UltrasharpTool_SearchDefinitions("EmailValidator")
   // Если нашли - файл существует
   ```

4. **Для .cs файлов - один public класс на файл:**
   ```javascript
   // ✅ Хорошо
   // EmailValidator.cs содержит только EmailValidator

   // ⚠️ Допустимо, но не рекомендуется
   // EmailValidator.cs содержит EmailValidator + EmailValidatorException
   ```

### Типичные ошибки

#### ❌ Ошибка: "File already exists"
```
ERROR: File already exists: D:/MyProject/src/Services/UserService.cs
```
**Решение:**
- Используйте другое имя файла
- Или используйте `OverwriteRoslynDocument` (⚠️ ОСТОРОЖНО - перезапишет файл!)
- Или используйте `AddMember` для добавления в существующий тип

#### ❌ Ошибка: "Compilation error in new file"
```
ERROR: Compilation failed
- Type 'User' not found
```
**Решение:**
- Добавьте missing usings
- Проверьте namespace
- Проверьте типы параметров

### Связанные инструменты

- ➡️ [**AddMember**](MODIFICATION_TOOLS.md#UltrasharpTool_addmember) — если хотите добавить в существующий класс
- ➡️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — проверьте созданный файл
- ➡️ [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — отформатируйте после создания

---

## UltrasharpTool_OverwriteRoslynDocument

**⚠️ Перезапись файла целиком** — полностью заменяет содержимое существующего файла.

### ⚠️ ВНИМАНИЕ

Это **опасный** инструмент - он полностью удаляет старое содержимое файла!

**Используйте:**
- ✅ Только для non-code файлов (json, xml, config)
- ✅ После `ReadRawFromRoslynDocument` для сохранения modified content
- ✅ Когда уверены что нужно заменить весь файл

**НЕ используйте:**
- ❌ Для изменения C# кода → используйте Symbol Tools
- ❌ Без предварительного чтения файла
- ❌ Для partial changes → используйте AddMember/OverwriteMember

### Использование

```javascript
// 1. Сначала читаем
UltrasharpTool_ReadRawFromRoslynDocument(
filePath: "D:/MyProject/appsettings.json"
)

// 2. Модифицируем content вне SharpTools

// 3. Записываем обратно
UltrasharpTool_OverwriteRoslynDocument(
filePath: "D:/MyProject/appsettings.json",
content: `{
"ConnectionStrings": {
"DefaultConnection": "Server=localhost;Database=MyDb;..."
},
"Logging": {
"LogLevel": {
"Default": "Information"
}
}
}`,
commitMessage: "Update connection string in appsettings.json"
)
```

### Параметры

- **filePath** (required): Полный абсолютный путь к существующему файлу
- **content** (required): Новое содержимое (БЕЗ ОТСТУПОВ для .cs, с отступами для json/xml)
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. ✅ Проверяет что файл существует
2. 🗑️ **Удаляет всё содержимое**
3. 📝 Записывает новое содержимое
4. 🎨 Форматирует (для .cs файлов)
5. ✅ Проверяет компиляцию (для .cs файлов)
6. 🌳 Git branch + commit

### Пример вывода

```
✅ Document overwritten successfully

File: D:/MyProject/appsettings.json
Previous size: 542 characters
New size: 387 characters

═══════════════════════════════════════════════════════════
⚠️ WARNING
═══════════════════════════════════════════════════════════

File was completely replaced.
Old content is available in Git history.

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-151045
Commit: a8f7e34 "Update connection string in appsettings.json"

💡 Use UltrasharpTool_Undo to restore old content
💡 Use ReadRawFromRoslynDocument to verify new content
```

### Когда использовать

✅ **Для конфигурационных файлов:**
- appsettings.json
- .csproj (но осторожно!)
- .editorconfig
- launchSettings.json

✅ **Для generated файлов:**
- Auto-generated code
- Build artifacts
- Временные файлы

✅ **После полного редизайна:**
- Полная переработка класса (редко! лучше OverwriteMember)
- Миграция на новый API

### Когда НЕ использовать

❌ **НИКОГДА не используйте для:**
- Изменения одного метода в .cs → `OverwriteMember`
- Добавления члена в класс → `AddMember`
- Переименования → `RenameSymbol`
- Regex замены → `FindAndReplace`
- Любых точечных изменений кода

### Best Practices

1. **ВСЕГДА читайте перед записью:**
   ```javascript
   // ✅ ПРАВИЛЬНО
   UltrasharpTool_ReadRawFromRoslynDocument(filePath: "config.json")
   // Модифицируем content
   UltrasharpTool_OverwriteRoslynDocument(filePath: "config.json", content: modified, ...)

   // ❌ ОПАСНО - не знаем что было в файле
   UltrasharpTool_OverwriteRoslynDocument(filePath: "config.json", content: ..., ...)
   ```

2. **Используйте Undo если ошиблись:**
   ```javascript
   UltrasharpTool_OverwriteRoslynDocument(...)
   // Ой, ошибка!

   UltrasharpTool_Undo()
   // Восстановили старый content
   ```

3. **Для .cs файлов предпочитайте Symbol Tools:**
   ```javascript
   // ❌ Плохо - перезаписываем весь файл
   UltrasharpTool_OverwriteRoslynDocument("UserService.cs", newContent, ...)

   // ✅ Хорошо - изменяем только нужный метод
   UltrasharpTool_OverwriteMember("UserService.MyMethod", newMethodCode, ...)
   ```

### Типичные ошибки

#### ❌ Ошибка: "File not found"
```
ERROR: File not found: D:/MyProject/config.json
```
**Решение:**
- Используйте `CreateRoslynDocument` для новых файлов
- Проверьте путь

#### ❌ Ошибка: "Compilation failed after overwrite"
```
ERROR: Compilation failed
- Multiple errors (файл поврежден)
```
**Решение:**
- `UltrasharpTool_Undo` немедленно!
- Используйте Symbol Tools вместо OverwriteRoslynDocument

### Связанные инструменты

- ⬅️ [**ReadRawFromRoslynDocument**](#UltrasharpTool_readrawfromroslyndocument) — ОБЯЗАТЕЛЬНО перед overwrite
- ➡️ [**Undo**](MODIFICATION_TOOLS.md#UltrasharpTool_undo) — если что-то пошло не так
- ➡️ [**CreateRoslynDocument**](#UltrasharpTool_createroslyndocument) — для новых файлов

---

## Workflow: Модификация конфигурационного файла

```javascript
// 1. Читаем текущее содержимое
UltrasharpTool_ReadRawFromRoslynDocument(
filePath: "D:/MyProject/appsettings.json"
)
// Output: { "ConnectionStrings": {...}, "Logging": {...} }

// 2. Модифицируем (в вашем коде, вне SharpTools)
//    Например, парсим JSON, изменяем, сериализуем обратно

// 3. Записываем обратно
UltrasharpTool_OverwriteRoslynDocument(
filePath: "D:/MyProject/appsettings.json",
content: modifiedJson,
commitMessage: "Update database connection string"
)

// 4. Проверяем
UltrasharpTool_ReadRawFromRoslynDocument(
filePath: "D:/MyProject/appsettings.json"
)
// Verify changes
```

## Workflow: Создание нового класса

```javascript
// 1. Проверяем что класс не существует
UltrasharpTool_SearchDefinitions("EmailValidator")
// Output: "No matches"

// 2. Создаём файл
UltrasharpTool_CreateRoslynDocument(
filePath: "D:/MyProject/src/Validators/EmailValidator.cs",
content: `
using System;

namespace MyProject.Validators
{
public class EmailValidator
{
public static bool IsValid(string email)
{
// Implementation
return true;
}
}
}`,
commitMessage: "Add EmailValidator class"
)

// 3. Проверяем
UltrasharpTool_ViewDefinition("MyProject.Validators.EmailValidator")

// 4. Форматируем
UltrasharpTool_FormatCode(path: "D:/MyProject/src/Validators/EmailValidator.cs", checkOnly: false)

// 5. Проверяем quality
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
```

---

## Сравнение Document Tools

| Инструмент | Читает | Пишет | Опасность | Use case |
|------------|--------|-------|----------|----------|
| **ReadRaw** | ✅ | ❌ | Безопасно ✅ | Просмотр файлов |
| **ReadTypes** | ✅ (структура) | ❌ | Безопасно ✅ | Навигация по файлу |
| **Create** | ❌ | ✅ | Низкая ✅ | Новые файлы |
| **Overwrite** | ❌ | ✅ | Высокая ⚠️ | Configs, полная замена |

---

## См. также

- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — для точечных изменений C# кода
- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — для semantic analysis
- 📚 [**QUALITY_TOOLS.md**](QUALITY_TOOLS.md) — форматирование после создания
- 📚 [**README.md**](../README.md) — главная документация

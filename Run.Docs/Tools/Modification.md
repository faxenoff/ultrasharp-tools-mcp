# Инструменты модификации кода

**Высокоточные операции изменения C# кода через Roslyn API.** Все модификации автоматически создают Git ветки/коммиты, проверяются на ошибки компиляции, и могут быть отменены через Undo.

## 📋 Quick Reference

| Инструмент | Назначение | Git автоматизация | Auto-lint |
|------------|-----------|------------------|-----------|
| **AddMember** | Добавить метод/property/class в тип | ✅ Branch + Commit | ✅ Да |
| **OverwriteMember** | Заменить или удалить существующий член | ✅ Branch + Commit | ✅ Да |
| **RenameSymbol** | Переименовать с обновлением всех референсов | ✅ Branch + Commit | ✅ Да |
| **FindAndReplace** | Regex замена в коде | ✅ Branch + Commit | ✅ Да |
| **MoveMember** | Переместить член в другой тип/namespace | ✅ Branch + Commit | ✅ Да |
| **Undo** | Откатить последнюю модификацию | ✅ Git revert | - |

---

## ⚠️ Важные особенности модификации

### 🔄 Автоматическая Git интеграция

**Каждая модификация:**
1. ✅ Создаёт новую ветку `sharptools/YYYYMMDD-HHMMSS`
2. ✅ Делает commit с описанием изменения
3. ✅ Сохраняет изменения для возможного Undo

**Пример workflow:**
```bash
# Исходная ветка: main

UltrasharpTool_AddMember(...)
# Создана ветка: sharptools/20251113-140523
# Commit: "Add method CreateUser to UserService"

UltrasharpTool_RenameSymbol(...)
# Создана ветка: sharptools/20251113-140612
# Commit: "Rename oldName to newName"

UltrasharpTool_Undo()
# Откат последнего commit, возврат к sharptools/20251113-140523
```

**Отключение Git:**
```bash
# При запуске сервера
UltrasharpTools.Droid.exe --disable-git
```

### ✅ Автоматический Linting

**Каждая модификация проверяется:**
1. ✅ **Compilation errors** — немедленный отчёт
2. ✅ **Syntax errors** — обнаружение до сохранения
3. ✅ **Format check** — предупреждение если нужно форматирование
4. ✅ **Code style warnings** — необязательные, но полезные

**Рекомендуемый workflow:**
```javascript
// 1. Модификация
UltrasharpTool_AddMember(...)
// Output: "✅ No compilation errors. ⚠️ Consider running FormatCode"

// 2. Форматирование
UltrasharpTool_FormatCode(path: "src/", checkOnly: false)

// 3. Линтинг
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")

// 4. Авто-фиксы
UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)
```

### 🔙 Undo механизм

**Как работает:**
- Хранит stack последних изменений
- Откатывает через `git reset --hard HEAD~1`
- Можно откатить несколько изменений подряд

**Ограничения:**
- ⚠️ Нельзя откатить если были внешние изменения
- ⚠️ Нельзя откатить если переключились на другую ветку
- ⚠️ Stack undo сбрасывается при перезапуске сервера

---

## UltrasharpTool_AddMember

**Добавить новый член** — метод, свойство, поле, вложенный класс в существующий тип.

### Использование

```javascript
UltrasharpTool_AddMember(
fullyQualifiedTargetName: "MyNamespace.UserService",
codeSnippet: `
/// <summary>
/// Validates user email format.
/// </summary>
private bool ValidateEmail(string email)
{
if (string.IsNullOrWhiteSpace(email))
return false;

return Regex.IsMatch(email, @"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
}`,
fileNameHint: "auto",
lineNumberHint: -1,
commitMessage: "Add email validation method"
)
```

### Параметры

- **fullyQualifiedTargetName** (required): FQN родительского типа
- **codeSnippet** (required): Код нового члена (без отступов, будет auto-formatted)
- **fileNameHint** (required): Имя файла для partial types, `"auto"` — автовыбор
- **lineNumberHint** (required): Желаемая строка вставки, `-1` — автовыбор
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. 🔍 Находит target тип через FuzzyFqnLookup
2. 📝 Парсит codeSnippet через Roslyn
3. 📍 Определяет место вставки (lineNumberHint или end of type)
4. ➕ Вставляет новый член
5. 🎨 Форматирует код (CSharpier)
6. ✅ Проверяет компиляцию
7. 🌳 Создаёт Git branch + commit
8. 📊 Возвращает diff

### Пример вывода

```
✅ Member added successfully

File: D:/MyProject/src/Services/UserService.cs
Target: MyNamespace.UserService
Inserted at: line 87

═══════════════════════════════════════════════════════════
📄 DIFF
═══════════════════════════════════════════════════════════

@@ -84,6 +84,18 @@ public class UserService
private readonly ILogger<UserService> _logger;

+    /// <summary>
+    /// Validates user email format.
+    /// </summary>
+    private bool ValidateEmail(string email)
+    {
+        if (string.IsNullOrWhiteSpace(email))
+            return false;
+
+        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
+    }
+
public UserService(IUserRepository repository, ILogger<UserService> logger)
{
_repository = repository;

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

No errors found.

⚠️ CODE STYLE SUGGESTIONS:
- Consider adding 'using System.Text.RegularExpressions;'
- Consider extracting regex pattern to const field

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-141523
Commit: c4f2e89 "Add email validation method"

💡 Use UltrasharpTool_Undo to revert this change
💡 Use UltrasharpTool_FormatCode to apply formatting
```

### Когда использовать

✅ **Для добавления функциональности:**
- Новый метод в существующий класс
- Новое свойство
- Новое поле
- Вложенный класс/enum

✅ **Для расширения API:**
- Добавить публичный метод
- Добавить extension method
- Добавить helper method

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужно изменить существующий член → `OverwriteMember`
- Нужно создать новый файл → `CreateRoslynDocument`
- Добавляете только using → `ManageUsings`

### Best Practices

1. **Пишите код без отступов (будет auto-formatted):**
   ```javascript
   // ✅ Правильно - без отступов
   codeSnippet: `
   public void MyMethod()
   {
   Console.WriteLine("Test");
   }`

   // ❌ Не нужно - отступы будут удалены
   codeSnippet: `
       public void MyMethod()
       {
           Console.WriteLine("Test");
       }`
   ```

2. **Включайте XML documentation:**
   ```javascript
   codeSnippet: `
   /// <summary>
   /// Validates user data.
   /// </summary>
   /// <param name="user">User to validate.</param>
   /// <returns>True if valid.</returns>
   public bool ValidateUser(User user)
   {
   // ...
   }`
   ```

3. **Используйте осмысленные commit messages:**
   ```javascript
   // ✅ Хорошо
   commitMessage: "Add email validation to UserService"

   // ❌ Плохо
   commitMessage: "add method"
   ```

4. **Проверяйте compilation errors:**
   ```javascript
   UltrasharpTool_AddMember(...)
   // Output: "ERROR: Type 'Regex' not found"

   // Добавьте using
   UltrasharpTool_ManageUsings(operation: "write", codeToWrite: "...\nusing System.Text.RegularExpressions;", ...)

   // Retry
   UltrasharpTool_AddMember(...)
   ```

### Типичные ошибки

#### ❌ Ошибка: "Compilation error after add"
```
ERROR: Compilation failed
- Type 'Regex' not found
- Member 'DoSomething' already exists
```
**Решение:**
- Добавьте missing usings
- Проверьте что член не дублируется (используйте `GetMembers` сначала)
- Проверьте типы параметров

#### ❌ Ошибка: "Target type not found"
```
ERROR: Type 'UserService' not found
```
**Решение:**
- Используйте полный FQN: `MyNamespace.UserService`
- Проверьте spelling (fuzzy matching может не помочь)

### Связанные инструменты

- ⬅️ [**GetMembers**](ANALYSIS_TOOLS.md#UltrasharpTool_getmembers) — посмотрите существующие члены перед добавлением
- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — посмотрите контекст куда добавляете
- ➡️ [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — отформатируйте после добавления
- ➡️ [**Undo**](#UltrasharpTool_undo) — откатите если ошиблись

---

## UltrasharpTool_OverwriteMember

**Заменить или удалить существующий член** — полная замена definition метода/свойства/класса новым кодом или удаление.

### Использование

```javascript
// Замена
UltrasharpTool_OverwriteMember(
fullyQualifiedMemberName: "MyNamespace.UserService.ValidateEmail",
newMemberCode: `
/// <summary>
/// Validates email using improved regex.
/// </summary>
private bool ValidateEmail(string email)
{
return !string.IsNullOrWhiteSpace(email) &&
Regex.IsMatch(email, @"^[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+$");
}`,
commitMessage: "Improve email validation regex"
)

// Удаление
UltrasharpTool_OverwriteMember(
fullyQualifiedMemberName: "MyNamespace.UserService.ObsoleteMethod",
newMemberCode: "// Delete ObsoleteMethod",
commitMessage: "Remove obsolete method"
)
```

### Параметры

- **fullyQualifiedMemberName** (required): FQN члена для замены
- **newMemberCode** (required): Новый код (или `"// Delete {name}"` для удаления)
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. 🔍 Находит существующий член
2. 🗑️ Удаляет старую definition (полностью, включая attributes/XML docs)
3. ➕ Вставляет новую definition (если не удаление)
4. 🎨 Форматирует код
5. ✅ Проверяет компиляцию
6. 🌳 Git branch + commit
7. 📊 Возвращает diff

### Пример вывода

```
✅ Member overwritten successfully

File: D:/MyProject/src/Services/UserService.cs
Member: MyNamespace.UserService.ValidateEmail

═══════════════════════════════════════════════════════════
📄 DIFF
═══════════════════════════════════════════════════════════

@@ -87,10 +87,9 @@ public class UserService

-    /// <summary>
-    /// Validates user email format.
-    /// </summary>
-    private bool ValidateEmail(string email)
-    {
-        if (string.IsNullOrWhiteSpace(email))
-            return false;
-
-        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
-    }
+    /// <summary>
+    /// Validates email using improved regex.
+    /// </summary>
+    private bool ValidateEmail(string email)
+    {
+        return !string.IsNullOrWhiteSpace(email) &&
+            Regex.IsMatch(email, @"^[\w\.+-]+@[\w\.-]+\.[\w\.-]+$");
+    }

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

No errors found.

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-142314
Commit: a9b3c45 "Improve email validation regex"

💡 Use UltrasharpTool_FindReferences to check usage
💡 Use UltrasharpTool_Undo to revert this change
```

### Когда использовать

✅ **Для изменения логики:**
- Исправить баг в методе
- Улучшить алгоритм
- Изменить validation

✅ **Для рефакторинга:**
- Упростить сложный метод
- Извлечь переменные
- Изменить структуру

✅ **Для удаления:**
- Удалить obsolete методы
- Удалить unused поля
- Удалить deprecated API

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужно добавить новый член → `AddMember`
- Нужно только переименовать → `RenameSymbol`
- Нужна простая regex замена → `FindAndReplace`

### Best Practices

1. **Всегда включайте attributes и XML docs:**
   ```javascript
   // ✅ Правильно - полная definition
   newMemberCode: `
   [Obsolete("Use NewMethod instead")]
   /// <summary>
   /// Old method.
   /// </summary>
   public void OldMethod() { ... }`

   // ❌ Плохо - потеряете attributes/docs
   newMemberCode: `public void OldMethod() { ... }`
   ```

2. **Проверьте impact перед изменением:**
   ```javascript
   // Сначала проверьте где используется
   UltrasharpTool_FindReferences("MyClass.MyMethod")

   // Затем изменяйте
   UltrasharpTool_OverwriteMember(...)
   ```

3. **Для удаления используйте правильный синтаксис:**
   ```javascript
   // ✅ Правильно
   newMemberCode: "// Delete MyMethod"

   // ❌ Неправильно
   newMemberCode: ""
   newMemberCode: "/* delete */"
   ```

### Типичные ошибки

#### ❌ Ошибка: "Member not found"
```
ERROR: Member 'MyMethod' not found in type 'MyClass'
```
**Решение:**
- Используйте полный FQN с параметрами: `MyClass.MyMethod(int, string)`
- Проверьте spelling
- Используйте `GetMembers` для получения точного FQN

#### ❌ Ошибка: "Breaking change detected"
```
ERROR: Compilation failed after overwrite
- 'MyMethod' is referenced in 15 places
```
**Решение:**
- Проверьте `FindReferences` перед изменением
- Обновите call sites
- Или используйте `RenameSymbol` если меняете только имя

### Связанные инструменты

- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — посмотрите текущую definition
- ⬅️ [**FindReferences**](ANALYSIS_TOOLS.md#UltrasharpTool_findreferences) — проверьте impact
- ➡️ [**Undo**](#UltrasharpTool_undo) — откатите если что-то пошло не так

---

## UltrasharpTool_RenameSymbol

**Переименовать символ** — меняет имя и автоматически обновляет все references в solution.

### Использование

```javascript
UltrasharpTool_RenameSymbol(
fullyQualifiedSymbolName: "MyNamespace.UserService.ValidateEmail",
newName: "ValidateEmailFormat",
commitMessage: "Rename ValidateEmail to ValidateEmailFormat for clarity"
)
```

### Параметры

- **fullyQualifiedSymbolName** (required): FQN символа для переименования
- **newName** (required): Новое имя (без namespace, только имя)
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. 🔍 Находит символ и все его references
2. ✏️ Переименовывает definition
3. 🔄 Обновляет все call sites
4. 🎨 Форматирует затронутые файлы
5. ✅ Проверяет компиляцию
6. 🌳 Git branch + commit
7. 📊 Возвращает список изменённых файлов

### Пример вывода

```
✅ Symbol renamed successfully

Old name: ValidateEmail
New name: ValidateEmailFormat
Total references updated: 23

═══════════════════════════════════════════════════════════
📁 MODIFIED FILES (8)
═══════════════════════════════════════════════════════════

1. Services/UserService.cs
   - Line 87: Method definition renamed
   - Line 142: Call site updated
   - Line 198: Call site updated

2. API/Controllers/UserController.cs
   - Line 56: Call site updated
   - Line 123: Call site updated

3. Tests/UserServiceTests.cs
   - Line 45: Call site updated
   - Line 67: Call site updated
   - Line 89: Call site updated

... (5 more files)

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

No errors found.
All 23 references updated successfully.

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-143022
Commit: e7d8f12 "Rename ValidateEmail to ValidateEmailFormat for clarity"

💡 Use UltrasharpTool_FindReferences to verify all usages updated
💡 Use UltrasharpTool_Undo to revert this change
```

### Когда использовать

✅ **Для улучшения clarity:**
- Переименовать плохо названные переменные
- Следовать naming conventions
- Сделать код более читаемым

✅ **Для рефакторинга:**
- Переименовать после изменения responsibility
- Исправить typos
- Согласовать naming

✅ **Безопасное переименование:**
- Метода используемого в 100+ местах
- Класса с множеством references
- Интерфейса с множеством implementations

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Меняете сигнатуру (не только имя) → `OverwriteMember`
- Перемещаете в другой namespace → `MoveMember`
- Символ используется в reflection (найдите вручную)

### Best Practices

1. **Проверьте scope переименования:**
   ```javascript
   // Сначала посмотрите сколько references
   UltrasharpTool_FindReferences("OldName")
   // Output: "147 references in 42 files"

   // Если много - убедитесь что хотите изменить всё
   UltrasharpTool_RenameSymbol("OldName", "NewName", "...")
   ```

2. **Используйте descriptive commit messages:**
   ```javascript
   // ✅ Хорошо - понятно зачем
   commitMessage: "Rename User to Customer for domain consistency"

   // ❌ Плохо
   commitMessage: "rename"
   ```

3. **Для переменных - локальный scope:**
   ```javascript
   // RenameSymbol работает для любых символов
   UltrasharpTool_RenameSymbol("MyMethod.localVar", "betterName", "...")
   ```

4. **Проверьте после переименования:**
   ```javascript
   UltrasharpTool_RenameSymbol(...)

   // Проверьте компиляцию
   // Output: "✅ No errors"

   // Проверьте tests
   // Run: dotnet test
   ```

### Типичные ошибки

#### ❌ Ошибка: "Name conflicts"
```
ERROR: Symbol 'NewName' already exists in this scope
```
**Решение:**
- Выберите другое имя
- Или сначала переименуйте conflicting символ

#### ❌ Ошибка: "Reflection usages not found"
```
WARNING: Some references may use reflection (not updated)
```
**Решение:**
- Поищите string literals с именем: `SearchDefinitions("\"OldName\"")`
- Обновите reflection код вручную
- Обновите конфигурационные файлы

### Производительность

- **Малый scope (< 10 refs):** < 1 сек
- **Средний scope (10-100 refs):** 2-5 сек
- **Большой scope (100+ refs):** 5-15 сек

### Связанные инструменты

- ⬅️ [**FindReferences**](ANALYSIS_TOOLS.md#UltrasharpTool_findreferences) — посмотрите scope перед переименованием
- ➡️ [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — отформатируйте затронутые файлы
- ➡️ [**Undo**](#UltrasharpTool_undo) — откатите если нужно

---

## UltrasharpTool_FindAndReplace

**Regex find/replace** — замена текста в коде через regex паттерны. Работает с FQN (в пределах символа) или glob paths (в файлах).

### Использование

```javascript
// В пределах символа
UltrasharpTool_FindAndReplace(
regexPattern: "Console\\.WriteLine\\((.*)\\)",
replacementText: "_logger.LogInformation($1)",
target: "MyNamespace.UserService.ProcessUser",
commitMessage: "Replace Console.WriteLine with logger"
)

// В файлах (glob)
UltrasharpTool_FindAndReplace(
regexPattern: "var\\s+(\\w+)\\s*=\\s*new\\s+List<",
replacementText: "var $1 = [",
target: "src/**/*.cs",
commitMessage: "Use collection expressions"
)
```

### Параметры

- **regexPattern** (required): Regex в multiline mode (используйте `\\s*` для отступов)
- **replacementText** (required): Замена (может включать группы: `$1`, `${name}`)
- **target** (required): FQN символа ИЛИ glob path (`*` поддерживается)
- **commitMessage** (required): Сообщение для Git commit

### Regex режимы

**Multiline mode:**
- `^` и `$` работают для каждой строки
- `.` НЕ матчит `\n` (используйте `[\s\S]` для любого символа)

**Важно для отступов:**
```javascript
// ✅ Правильно - учитывает неизвестные отступы
regexPattern: "^\\s*if\\s*\\("

// ❌ Плохо - не сработает если другие отступы
regexPattern: "^if\\s*\\("
```

### Пример вывода

```
✅ Find and replace completed

Pattern: Console\.WriteLine\((.*)\)
Replacement: _logger.LogInformation($1)
Target: MyNamespace.UserService

═══════════════════════════════════════════════════════════
📁 MODIFIED FILES (3)
═══════════════════════════════════════════════════════════

Services/UserService.cs
- 5 replacements

@@ -45,7 +45,7 @@ public void ProcessUser(string userName)
{
-    Console.WriteLine($"Processing user: {userName}");
+    _logger.LogInformation($"Processing user: {userName}");

@@ -67,7 +67,7 @@ public void ProcessUser(string userName)
{
-    Console.WriteLine($"User processed successfully");
+    _logger.LogInformation($"User processed successfully");

... (3 more replacements)

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

No errors found.

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-144512
Commit: b2a9c78 "Replace Console.WriteLine with logger"

💡 5 occurrences replaced in 3 files
💡 Use UltrasharpTool_Undo to revert this change
```

### Когда использовать

✅ **Для массовых изменений:**
- Заменить устаревший API на новый
- Изменить naming convention
- Исправить typos
- Обновить version numbers

✅ **Для рефакторинга:**
- Заменить using на collection expressions
- Изменить null checks на null-coalescing
- Упростить boolean expressions

✅ **Для migration:**
- Обновить namespace после переименования
- Заменить deprecated attributes
- Изменить синтаксис на новый C# version

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужно переименовать символ → `RenameSymbol` (безопаснее)
- Нужно заменить только definition → `OverwriteMember`
- Нужно изменить using statements → `ManageUsings`

### Best Practices

1. **Тестируйте regex отдельно:**
   ```javascript
   // Используйте онлайн regex tester (regex101.com)
   // Проверьте на примерах кода
   // Затем применяйте в SharpTools
   ```

2. **Используйте capture groups:**
   ```javascript
   // ✅ Сохраняем части исходного кода
   regexPattern: "if\\s*\\((.*)\\s*==\\s*null\\)",
   replacementText: "if ($1 is null)"

   // Было: if (user == null)
   // Стало: if (user is null)
   ```

3. **Учитывайте отступы:**
   ```javascript
   // ✅ Правильно - работает с любыми отступами
   regexPattern: "^\\s*Console\\.WriteLine",

   // ❌ Неправильно - пропустит отступы
   regexPattern: "^Console\\.WriteLine"
   ```

4. **Используйте glob для массовых изменений:**
   ```javascript
   // Все .cs файлы в src/
   target: "src/**/*.cs"

   // Только Controllers
   target: "src/API/Controllers/*.cs"

   // Конкретный файл
   target: "src/Services/UserService.cs"
   ```

5. **Проверьте перед apply:**
   ```javascript
   // Сначала используйте SearchDefinitions для preview
   UltrasharpTool_SearchDefinitions("Console\\.WriteLine")
   // Смотрим сколько matches

   // Затем применяем FindAndReplace
   UltrasharpTool_FindAndReplace(...)
   ```

### Типичные ошибки

#### ❌ Ошибка: "Regex pattern invalid"
```
ERROR: Invalid regex pattern
```
**Решение:**
- Проверьте escaping: `\.` для точки, `\\(` для скобки
- Тестируйте на regex101.com
- Используйте raw strings в вашем языке

#### ❌ Ошибка: "Too many replacements"
```
WARNING: Pattern matched 500+ times. This may be too broad.
```
**Решение:**
- Уточните pattern
- Используйте более конкретный target (FQN вместо glob)

### Примеры regex паттернов

```javascript
// Заменить var на explicit type
regexPattern: "var\\s+(\\w+)\\s*=\\s*new\\s+(\\w+)",
replacementText: "$2 $1 = new $2"

// Убрать trailing whitespace
regexPattern: "\\s+$",
replacementText: ""

// Заменить == null на is null
regexPattern: "(\\w+)\\s*==\\s*null",
replacementText: "$1 is null"

// Добавить async/await
regexPattern: "public\\s+(\\w+)\\s+(\\w+)\\(",
replacementText: "public async Task<$1> $2Async("

// Заменить string.IsNullOrEmpty на string.IsNullOrWhiteSpace
regexPattern: "string\\.IsNullOrEmpty\\((.*)\\)",
replacementText: "string.IsNullOrWhiteSpace($1)"
```

### Связанные инструменты

- ⬅️ [**SearchDefinitions**](ANALYSIS_TOOLS.md#UltrasharpTool_searchdefinitions) — preview matches
- ➡️ [**FormatCode**](QUALITY_TOOLS.md#UltrasharpTool_formatcode) — форматируйте после замен
- ➡️ [**Undo**](#UltrasharpTool_undo) — откатите если что-то пошло не так

---

## UltrasharpTool_MoveMember

**Переместить член** — перемещает метод/свойство/поле из одного типа в другой тип или namespace.

### Использование

```javascript
UltrasharpTool_MoveMember(
fullyQualifiedMemberName: "MyNamespace.UserService.ValidateEmail",
fullyQualifiedDestinationTypeOrNamespaceName: "MyNamespace.Validators.EmailValidator",
commitMessage: "Move email validation to EmailValidator class"
)
```

### Параметры

- **fullyQualifiedMemberName** (required): FQN члена для перемещения
- **fullyQualifiedDestinationTypeOrNamespaceName** (required): FQN target типа или namespace
- **commitMessage** (required): Сообщение для Git commit

### Что делает

1. 🔍 Находит source member
2. 📋 Копирует definition (с attributes, XML docs)
3. 🗑️ Удаляет из source
4. ➕ Добавляет в destination
5. 🔄 Обновляет using statements (если нужно)
6. 🎨 Форматирует затронутые файлы
7. ✅ Проверяет компиляцию
8. 🌳 Git branch + commit

### Пример вывода

```
✅ Member moved successfully

From: MyNamespace.UserService.ValidateEmail
To: MyNamespace.Validators.EmailValidator

═══════════════════════════════════════════════════════════
📁 MODIFIED FILES
═══════════════════════════════════════════════════════════

Services/UserService.cs
- Removed: ValidateEmail method (15 lines)

Validators/EmailValidator.cs
- Added: ValidateEmail method (15 lines)
- Added using: System.Text.RegularExpressions

═══════════════════════════════════════════════════════════
⚠️ REFERENCES UPDATE REQUIRED
═══════════════════════════════════════════════════════════

Found 12 references to ValidateEmail that may need updates:

1. UserController.cs:56
   - May need to change from: _userService.ValidateEmail(...)
   - To: _emailValidator.ValidateEmail(...)

2. UserServiceTests.cs:45
   - May need to update test setup

... (10 more references)

💡 Use UltrasharpTool_FindReferences to review all usages
💡 Consider using RenameSymbol if you also need to update calls

═══════════════════════════════════════════════════════════
✅ COMPILATION CHECK
═══════════════════════════════════════════════════════════

⚠️ Warnings found:
- CS0649: Field 'ValidateEmail' is never assigned to

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-145234
Commit: f3e8d67 "Move email validation to EmailValidator class"

💡 Use UltrasharpTool_Undo to revert this change
```

### Когда использовать

✅ **Для рефакторинга:**
- Переместить метод в более подходящий класс
- Извлечь utility methods в helper class
- Организовать код по responsibility

✅ **Для улучшения структуры:**
- Следовать Single Responsibility Principle
- Уменьшить coupling
- Улучшить cohesion

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужно изменить namespace всего файла → rename namespace вручную + update references
- Перемещаете в новый файл → используйте `AddMember` в новый тип

### Best Practices

1. **Проверьте references перед перемещением:**
   ```javascript
   UltrasharpTool_FindReferences("UserService.ValidateEmail")
   // Поймите impact

   UltrasharpTool_MoveMember(...)
   // Обновите call sites вручную
   ```

2. **Проверьте access modifiers:**
   ```javascript
   // Если был private - может стать public в новом классе
   // Проверьте и измените при необходимости
   ```

3. **Обновите tests:**
   ```javascript
   // После перемещения обновите unit tests
   // Они могут ссылаться на старую location
   ```

### Типичные ошибки

#### ❌ Ошибка: "Destination type not found"
```
ERROR: Type 'EmailValidator' not found
```
**Решение:**
- Создайте destination тип сначала: `AddMember` для создания класса
- Или используйте существующий тип

### Связанные инструменты

- ⬅️ [**FindReferences**](ANALYSIS_TOOLS.md#UltrasharpTool_findreferences) — проверьте impact
- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#UltrasharpTool_viewdefinition) — посмотрите member перед перемещением
- ➡️ [**Undo**](#UltrasharpTool_undo) — откатите если нужно

---

## UltrasharpTool_Undo

**Откат последнего изменения** — отменяет последнюю модификацию через Git revert.

### Использование

```javascript
UltrasharpTool_Undo()
```

### Параметры

Нет параметров.

### Что делает

1. 📜 Проверяет последний commit в sharptools/* ветке
2. ⬅️ Делает `git reset --hard HEAD~1`
3. 📊 Возвращает информацию об откаченных изменениях

### Пример вывода

```
✅ Last change undone successfully

═══════════════════════════════════════════════════════════
📜 UNDONE CHANGE
═══════════════════════════════════════════════════════════

Commit: f3e8d67 "Move email validation to EmailValidator class"
Author: SharpTools
Date: 2025-11-13 14:52:34
Files changed: 2

Services/UserService.cs
Validators/EmailValidator.cs

═══════════════════════════════════════════════════════════
🌳 GIT STATUS
═══════════════════════════════════════════════════════════

Current branch: sharptools/20251113-145122
HEAD is now at: e7d8f12 "Rename ValidateEmail to ValidateEmailFormat"

💡 You can undo multiple times to revert more changes
💡 Cannot undo after switching branches or external changes
```

### Когда использовать

✅ **Для быстрого отката:**
- Сделали ошибку в модификации
- Передумали после изменения
- Хотите попробовать другой подход

✅ **Для экспериментирования:**
- Пробуете разные варианты рефакторинга
- A/B testing разных реализаций

### Ограничения

⚠️ **Нельзя undo если:**
- Переключились на другую ветку
- Были внешние изменения (другой dev, IDE)
- Сервер был перезапущен (undo stack сбросился)
- Git был отключен (`--disable-git`)

### Best Practices

1. **Используйте сразу если ошиблись:**
   ```javascript
   UltrasharpTool_AddMember(...)
   // Output: "ERROR: Compilation failed"

   UltrasharpTool_Undo()
   // Быстро откатываем

   // Исправляем и пробуем снова
   UltrasharpTool_AddMember(...) // fixed version
   ```

2. **Можно откатить несколько изменений:**
   ```javascript
   UltrasharpTool_Undo() // Откат последнего
   UltrasharpTool_Undo() // Откат предпоследнего
   UltrasharpTool_Undo() // И ещё одного
   ```

3. **Проверьте git status после:**
   ```javascript
   UltrasharpTool_Undo()
   // Смотрим на output - какой commit теперь HEAD
   ```

### Альтернативы Undo

```bash
# Через Git напрямую
git log  # Найти нужный commit
git reset --hard <commit-hash>

# Через Git revert (создаёт новый commit)
git revert HEAD

# Откатить конкретную ветку
git checkout main  # Переключиться на main (откатит все sharptools/* изменения)
```

### Связанные инструменты

- Используется после любого modification tool при ошибке

---

## Workflow: Безопасная модификация

### Стандартный workflow

```javascript
// 1. Анализ текущего состояния
UltrasharpTool_ViewDefinition("MyClass.MyMethod")
UltrasharpTool_GetMembers("MyClass", false)
UltrasharpTool_FindReferences("MyClass.MyMethod")

// 2. Модификация
UltrasharpTool_OverwriteMember(
fullyQualifiedMemberName: "MyClass.MyMethod",
newMemberCode: "/* new implementation */",
commitMessage: "Improve MyMethod performance"
)

// 3. Проверка
// Output: "✅ No compilation errors"

// 4. Quality checks
UltrasharpTool_FormatCode(path: "src/", checkOnly: false)
UltrasharpTool_AnalyzeCodeStyle(severityFilter: "Warning")
UltrasharpTool_ApplyCodeFixes(diagnosticId: "all", preview: false)

// 5. Testing (вне SharpTools)
// dotnet test

// 6. Если всё ок - merge в main
// git checkout main
// git merge sharptools/20251113-XXX

// 7. Если проблема - откат
UltrasharpTool_Undo()
```

### Workflow для breaking changes

```javascript
// 1. Оценка impact
UltrasharpTool_FindReferences("MyClass.OldMethod")
// Output: "147 references in 42 files" - много!

// 2. Создаём новый метод вместо изменения старого
UltrasharpTool_AddMember(
fullyQualifiedTargetName: "MyClass",
codeSnippet: `
[Obsolete("Use NewMethod instead")]
public void OldMethod() {
NewMethod(); // delegate to new
}

public void NewMethod() {
// new implementation
}`,
commitMessage: "Add NewMethod, deprecate OldMethod"
)

// 3. Постепенно мигрируем call sites
UltrasharpTool_FindAndReplace(
regexPattern: "\\.OldMethod\\(",
replacementText: ".NewMethod(",
target: "src/Module1/**/*.cs",
commitMessage: "Migrate Module1 to NewMethod"
)

// Repeat для других модулей...

// 4. Когда все мигрировали - удаляем старый
UltrasharpTool_OverwriteMember(
fullyQualifiedMemberName: "MyClass.OldMethod",
newMemberCode: "// Delete OldMethod",
commitMessage: "Remove deprecated OldMethod"
)
```

---

## Сравнение инструментов модификации

| Инструмент | Scope | Обновляет References | Риск Breaking Changes |
|------------|-------|---------------------|----------------------|
| **AddMember** | Один тип | Нет | Низкий ✅ |
| **OverwriteMember** | Один член | Нет | Высокий ⚠️ |
| **RenameSymbol** | Весь solution | Да ✅ | Низкий ✅ |
| **FindAndReplace** | FQN или файлы | Нет | Средний ⚠️ |
| **MoveMember** | Два типа | Нет | Высокий ⚠️ |
| **Undo** | Последний commit | Да ✅ | Нет ✅ |

**Рекомендации:**
- ✅ Самый безопасный: **AddMember**, **RenameSymbol**, **Undo**
- ⚠️ Требуют внимания: **OverwriteMember**, **FindAndReplace**, **MoveMember**

---

## См. также

- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — анализ перед модификацией
- 📚 [**QUALITY_TOOLS.md**](QUALITY_TOOLS.md) — форматирование и линтинг после
- 📚 [**SOLUTION_TOOLS.md**](SOLUTION_TOOLS.md) — LoadSolution перед началом
- 📚 [**README.md**](../README.md) — главная документация

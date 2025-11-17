# Инструменты качества кода

**Автоматическое форматирование, линтинг и исправление** — поддерживайте единый code style через CSharpier и Roslyn analyzers. Все modification operations автоматически включают quality checks.

## 📋 Quick Reference

| Инструмент | Технология | Назначение | Auto-fix |
|------------|-----------|-----------|---------|
| **FormatCode** | CSharpier | Единый code style | ✅ Да |
| **AnalyzeCodeStyle** | Roslyn Analyzers | Поиск проблем (warnings, errors) | ❌ Нет |
| **ApplyCodeFixes** | Roslyn Code Fixes | Автоматическое исправление | ✅ Да |

---

## 🔄 Интеграция с Modification Tools

**Важно:** Все modification operations **автоматически** включают quality checks:

```javascript
add_member(...)
// Автоматически:
// 1. ✅ Проверка синтаксиса
// 2. ✅ Проверка компиляции
// 3. ⚠️ Предупреждение если нужно форматирование
// 4. 📊 Отчёт о code style warnings

modify_code(...)
// То же самое - автоматические проверки

rename_symbol(...)
// Тоже с автоматическими проверками
```

**Рекомендуемый workflow:**
```javascript
// 1. Модификация (с автоматическими checks)
add_member(...)
// Output: "✅ No errors. ⚠️ Consider running FormatCode"

// 2. Форматирование
format_code(path: "src/", checkOnly: false)

// 3. Детальный анализ
analyze_code_style(severityFilter: "Warning")

// 4. Автоматические исправления
apply_code_fixes(diagnosticId: "all", preview: false)
```

---

## format_code

**Автоматическое форматирование** — приводит C# код к единому стилю через CSharpier.

### Использование

```javascript
// Проверка без изменений
format_code(
path: "D:/MyProject/src/Services",
checkOnly: true
)

// Применение форматирования
format_code(
path: "D:/MyProject/src/Services",
checkOnly: false
)
```

### Параметры

- **path** (required): Путь к файлу или директории
- **checkOnly** (default: true): `true` — только проверка, `false` — применить форматирование

### Поддерживаемые файлы

- ✅ `.cs` — C# source files
- ✅ `.csproj` — Project files
- ✅ `.xml` — XML configuration files

### Что делает

**CheckOnly mode:**
1. 🔍 Сканирует все файлы в path (рекурсивно)
2. ✅ Проверяет форматирование каждого файла
3. 📊 Возвращает список файлов требующих форматирования
4. ❌ НЕ изменяет файлы

**Apply mode (checkOnly: false):**
1. 🔍 Сканирует файлы
2. 🎨 Форматирует каждый файл (параллельно)
3. 💾 Сохраняет изменения
4. 🌳 **Создаёт Git commit** (если изменены файлы)
5. 📊 Возвращает статистику

### Пример вывода (checkOnly: true)

```
Format Check: D:/MyProject/src/Services

═══════════════════════════════════════════════════════════
📊 SCAN RESULTS
═══════════════════════════════════════════════════════════

Files scanned: 47
Files needing formatting: 12
Files already formatted: 35

═══════════════════════════════════════════════════════════
📁 FILES NEEDING FORMATTING
═══════════════════════════════════════════════════════════

1. Services/UserService.cs
   Issues: Inconsistent indentation, missing spaces

2. Services/OrderService.cs
   Issues: Trailing whitespace, brace placement

3. Services/ProductService.cs
   Issues: Line length > 120 chars

... (9 more files)

💡 Run with checkOnly: false to apply formatting
💡 Example: format_code(path: "...", checkOnly: false)
```

### Пример вывода (checkOnly: false)

```
✅ Formatting applied

Path: D:/MyProject/src/Services
Files processed: 47
Files formatted: 12
Files unchanged: 35

═══════════════════════════════════════════════════════════
📁 FORMATTED FILES
═══════════════════════════════════════════════════════════

Services/UserService.cs
Services/OrderService.cs
Services/ProductService.cs
Services/EmailValidator.cs
... (8 more files)

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-152314
Commit: c8e7f45 "Format code with CSharpier (12 files)"

💡 Formatting complete
💡 All files now follow CSharpier style guide
```

### CSharpier Style Guide

**Ключевые правила:**
- ✅ Отступы: 4 spaces (не tabs)
- ✅ Скобки: на новой строке (Allman style)
- ✅ Max line length: 120 characters
- ✅ Trailing whitespace: удаляется
- ✅ Final newline: добавляется
- ✅ Using statements: сортируются и группируются
- ✅ Consistent spacing вокруг операторов

**Пример форматирования:**
```csharp
// ❌ BEFORE (неформатированный)
public class UserService{
private readonly IUserRepository _repo;
public async Task<User>GetUserAsync(int id){
if(id<=0)throw new ArgumentException();
var user=await _repo.GetByIdAsync(id);return user;}}

// ✅ AFTER (CSharpier)
public class UserService
{
    private readonly IUserRepository _repo;

    public async Task<User> GetUserAsync(int id)
    {
        if (id <= 0)
            throw new ArgumentException();

        var user = await _repo.GetByIdAsync(id);
        return user;
    }
}
```

### Когда использовать

✅ **После модификаций:**
- После AddMember
- После OverwriteMember
- После FindAndReplace
- После массовых изменений

✅ **Регулярно:**
- Перед commit в Git
- После merge
- Code review process
- CI/CD pipeline

✅ **Для всего проекта:**
- Onboarding нового разработчика
- Унификация code style
- Migration на новый style guide

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Код уже отформатирован (checkOnly покажет)
- Работаете с generated code (может нарушить generation)
- Временные/experimental файлы

### Best Practices

1. **Сначала check, потом apply:**
   ```javascript
   // ✅ Правильно - видим что изменится
   format_code(path: "src/", checkOnly: true)
   // Output: "12 files need formatting"

   format_code(path: "src/", checkOnly: false)
   // Применяем
   ```

2. **Форматируйте директории, не файлы:**
   ```javascript
   // ✅ Хорошо - вся директория
   format_code(path: "src/Services/", checkOnly: false)

   // ⚠️ Допустимо но неэффективно - по одному файлу
   format_code(path: "src/Services/UserService.cs", checkOnly: false)
   ```

3. **Интегрируйте в workflow:**
   ```javascript
   // После изменений
   modify_code(...)
   format_code(path: "src/", checkOnly: false)
   analyze_code_style(...)
   apply_code_fixes(...)
   ```

4. **Используйте в CI/CD:**
   ```bash
   # В CI/CD pipeline
   format_code(path: "src/", checkOnly: true)
   # Если вернул "files need formatting" - fail build
   ```

### Производительность

- **Проверка (checkOnly: true):** 0.5-2 сек для ~50 файлов
- **Форматирование (checkOnly: false):** 1-5 сек для ~50 файлов
- **Параллелизм:** Обработка файлов параллельная (multi-threaded)

**Оптимизация:**
- Форматируются только файлы требующие изменений
- Кэширование результатов проверки
- Batch обработка для больших директорий

### Связанные инструменты

- ➡️ [**AnalyzeCodeStyle**](#analyze_code_style) — анализ после форматирования
- ➡️ [**ApplyCodeFixes**](#apply_code_fixes) — автоматические исправления
- ⬅️ **Modification Tools** — форматирование после модификаций

---

## analyze_code_style

**Анализ качества кода** — запуск Roslyn analyzers для поиска code style issues, warnings, errors.

### Использование

```javascript
analyze_code_style(
solutionPath: "D:/MyProject/MyProject.sln",
severityFilter: "Warning",
skip: 0,
take: 100
)
```

### Параметры

- **solutionPath** (required): Путь к .sln файлу
- **severityFilter** (default: "Warning"): Минимальный уровень серьёзности (`"Hidden"`, `"Info"`, `"Warning"`, `"Error"`)
- **skip** (default: 0): Пропустить N результатов (pagination)
- **take** (default: 100): Вернуть N результатов (pagination)

### Что показывает

**Для каждой диагностики:**
- 🔍 **Diagnostic ID** (IDE0005, CS8019, CA1001, etc.)
- ⚠️ **Severity** (Hidden, Info, Warning, Error)
- 📄 **Message** (описание проблемы)
- 📁 **Location** (файл:строка:колонка)
- 💡 **Code snippet** (контекст)
- 🔧 **Has fix** (доступно ли автоматическое исправление)

**Группировка:**
- По severity (Error → Warning → Info → Hidden)
- По diagnostic ID
- Сортировка по файлам

### Пример вывода

```
Code Style Analysis: MyProject.sln

═══════════════════════════════════════════════════════════
📊 SUMMARY
═══════════════════════════════════════════════════════════

Total diagnostics: 147
Errors: 0
Warnings: 43
Info: 78
Hidden: 26

Showing: 43 warnings (skip: 0, take: 100)

═══════════════════════════════════════════════════════════
🔴 ERRORS (0)
═══════════════════════════════════════════════════════════

No errors found ✅

═══════════════════════════════════════════════════════════
⚠️ WARNINGS (43)
═══════════════════════════════════════════════════════════

[IDE0005] Using directive is unnecessary
────────────────────────────────────────────────────────────
Location: Services/UserService.cs:3:1
Snippet:
1 | using System;
2 | using System.Linq;
3 | using System.Collections;  // ← Unnecessary
4 |
🔧 Fix available: Yes (can auto-fix with apply_code_fixes)

[CS8019] Unnecessary using directive
────────────────────────────────────────────────────────────
Location: Services/OrderService.cs:5:1
Snippet:
3 | using MyProject.Domain;
4 | using MyProject.Data;
5 | using Newtonsoft.Json;  // ← Unnecessary
6 |
🔧 Fix available: Yes (can auto-fix with ApplyCodeFixes)

[CA1031] Do not catch general exception types
────────────────────────────────────────────────────────────
Location: Services/UserService.cs:45:13
Snippet:
43 |     try {
44 |         await ProcessUserAsync(user);
45 |     } catch (Exception ex) {  // ← Too broad
46 |         _logger.LogError(ex, "Error");
47 |     }
🔧 Fix available: No (manual fix required)

[IDE0028] Collection initialization can be simplified
────────────────────────────────────────────────────────────
Location: Services/ProductService.cs:67:25
Snippet:
65 |     var items = new List<Product>();
66 |     items.Add(product1);
67 |     items.Add(product2);  // ← Use collection initializer
68 |
🔧 Fix available: Yes (can auto-fix with ApplyCodeFixes)

... (39 more warnings)

═══════════════════════════════════════════════════════════
💡 RECOMMENDATIONS
═══════════════════════════════════════════════════════════

Auto-fixable issues: 28 warnings
Manual fixes required: 15 warnings

Next steps:
1. Run apply_code_fixes(diagnosticId: "IDE0005") for unused usings
2. Run apply_code_fixes(diagnosticId: "all") for all auto-fixable issues
3. Review remaining 15 warnings manually

💡 Use skip/take parameters for pagination if needed
```

### Diagnostic Categories

**IDE#### — Code Style:**
- `IDE0001-IDE9999` — Visual Studio IDE analyzers
- Примеры: IDE0005 (unused using), IDE0028 (collection init), IDE0055 (formatting)

**CS#### — C# Compiler:**
- `CS0001-CS9999` — C# compiler warnings/errors
- Примеры: CS8019 (unused using), CS0168 (unused variable), CS8600 (nullable)

**CA#### — Code Analysis:**
- `CA1000-CA9999` — .NET code analysis rules
- Примеры: CA1001 (IDisposable), CA1031 (catch Exception), CA2007 (ConfigureAwait)

### Severity Levels

- 🔴 **Error** — Must fix (breaks compilation or critical issue)
- ⚠️ **Warning** — Should fix (potential bugs, bad practices)
- 💡 **Info** — Consider fixing (improvements, suggestions)
- 👁️ **Hidden** — Optional (very minor style issues)

### Когда использовать

✅ **После модификаций:**
- После AddMember/modify_code
- После рефакторинга
- Перед commit

✅ **Регулярно:**
- Code review process
- CI/CD pipeline
- Weekly/monthly quality checks

✅ **Для анализа технического долга:**
- Подсчёт количества warnings
- Tracking improvement over time
- Prioritize fixes

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Нужна только компиляция (смотрите вывод modification tools)
- Ищете конкретную проблему (используйте IDE или grep)

### Best Practices

1. **Начните с Errors, потом Warnings:**
   ```javascript
   // Сначала критичные
   analyze_code_style(severityFilter: "Error")

   // Затем предупреждения
   analyze_code_style(severityFilter: "Warning")

   // Info опционально
   analyze_code_style(severityFilter: "Info")
   ```

2. **Используйте pagination для больших проектов:**
   ```javascript
   // Первая страница
   analyze_code_style(severityFilter: "Warning", skip: 0, take: 100)

   // Вторая страница
   analyze_code_style(severityFilter: "Warning", skip: 100, take: 100)
   ```

3. **Автоматизируйте исправления:**
   ```javascript
   // Анализ
   analyze_code_style(severityFilter: "Warning")
   // Output: "28 auto-fixable warnings"

   // Автофиксы
   apply_code_fixes(diagnosticId: "all", preview: false)

   // Повторный анализ
   analyze_code_style(severityFilter: "Warning")
   // Output: "15 warnings" (только manual fixes)
   ```

4. **Track progress:**
   ```javascript
   // Baseline
   analyze_code_style(severityFilter: "Warning")
   // "147 warnings"

   // После работы
   analyze_code_style(severityFilter: "Warning")
   // "98 warnings" - улучшение на 33%!
   ```

### Производительность

- **Малый проект (3-5 проектов):** 5-10 сек
- **Средний проект (10-20 проектов):** 15-30 сек
- **Большой проект (50+ проектов):** 45-90 сек

**Факторы:**
- Количество analyzers
- Размер solution
- Количество файлов
- Complexity кода

### Связанные инструменты

- ⬅️ [**FormatCode**](#format_code) — форматирование перед анализом
- ➡️ [**ApplyCodeFixes**](#apply_code_fixes) — автоисправление найденных проблем
- ⬅️ [**AnalyzeComplexity**](ANALYSIS_TOOLS.md#UltrasharpTool_analyzecomplexity) — метрики сложности

---

## apply_code_fixes

**Автоматические исправления** — применяет Roslyn code fixes для диагностик.

### Использование

```javascript
// Preview mode (посмотреть что изменится)
apply_code_fixes(
solutionPath: "D:/MyProject/MyProject.sln",
diagnosticId: "IDE0005",
preview: true
)

// Apply mode (применить изменения)
apply_code_fixes(
solutionPath: "D:/MyProject/MyProject.sln",
diagnosticId: "IDE0005",
preview: false
)
```

### Параметры

- **solutionPath** (required): Путь к .sln файлу
- **diagnosticId** (default: "all"): ID диагностики или `"all"` для всех
- **preview** (default: true): `true` — preview, `false` — apply

### Поддерживаемые диагностики

**Встроенные (always supported):**
- ✅ `IDE0005` — Remove unnecessary using directives
- ✅ `CS8019` — Unnecessary using directive
- ✅ `IDE0028` — Use collection initializers
- ✅ `IDE0090` — Use 'var' instead of explicit type
- ✅ `IDE0017` — Object initialization can be simplified
- ✅ И многие другие...

**Легко расширяемые:**
- Система поддерживает любые Roslyn code fixes
- Новые диагностики добавляются через configuration

### Что делает

**Preview mode:**
1. 🔍 Сканирует solution для найденных diagnostics
2. 📊 Группирует по diagnostic ID
3. 💡 Показывает что будет исправлено
4. 📄 Показывает diff для каждого файла
5. ❌ НЕ изменяет файлы

**Apply mode:**
1. 🔍 Находит все instances диагностики
2. 🔧 Применяет code fix к каждому
3. 💾 Сохраняет изменения
4. 🌳 **Создаёт Git commit**
5. 📊 Возвращает статистику

### Пример вывода (preview: true)

```
Code Fixes Preview: IDE0005 (Remove unnecessary usings)

═══════════════════════════════════════════════════════════
📊 SUMMARY
═══════════════════════════════════════════════════════════

Diagnostic: IDE0005 - Remove unnecessary using directives
Instances found: 28
Files affected: 12

═══════════════════════════════════════════════════════════
📁 CHANGES PREVIEW
═══════════════════════════════════════════════════════════

[1] Services/UserService.cs (3 fixes)
────────────────────────────────────────────────────────────
@@ Line 3 @@
- using System.Collections;
@@ Line 5 @@
- using System.Text;
@@ Line 8 @@
- using Newtonsoft.Json;

[2] Services/OrderService.cs (2 fixes)
────────────────────────────────────────────────────────────
@@ Line 4 @@
- using System.Xml;
@@ Line 7 @@
- using Microsoft.AspNetCore.Http;

... (10 more files)

═══════════════════════════════════════════════════════════
💡 NEXT STEPS
═══════════════════════════════════════════════════════════

To apply these fixes:
apply_code_fixes(
    solutionPath: "...",
    diagnosticId: "IDE0005",
    preview: false
)

⚠️ This will modify 12 files and create a Git commit
```

### Пример вывода (preview: false)

```
✅ Code fixes applied successfully

Diagnostic: IDE0005 - Remove unnecessary using directives
Files modified: 12
Total fixes applied: 28

═══════════════════════════════════════════════════════════
📁 MODIFIED FILES
═══════════════════════════════════════════════════════════

Services/UserService.cs (3 fixes)
Services/OrderService.cs (2 fixes)
Services/ProductService.cs (4 fixes)
API/Controllers/UserController.cs (1 fix)
API/Controllers/OrderController.cs (2 fixes)
... (7 more files)

═══════════════════════════════════════════════════════════
🌳 GIT
═══════════════════════════════════════════════════════════

Branch: sharptools/20251113-153445
Commit: f2d8e91 "Apply code fixes for IDE0005 (28 instances in 12 files)"

═══════════════════════════════════════════════════════════
✅ VERIFICATION
═══════════════════════════════════════════════════════════

Compilation: Success ✅
All fixes applied correctly.

💡 Run analyze_code_style to check remaining issues
💡 Use undo if you need to revert these changes
```

### Когда использовать

✅ **Для массовых исправлений:**
- Удалить все unused usings
- Применить code style rules
- Fix naming violations
- Simplify code patterns

✅ **После анализа:**
- analyze_code_style показал auto-fixable issues
- Применить все предложенные fixes

✅ **Для миграции:**
- Upgrade to new C# syntax
- Apply new .NET conventions
- Modernize codebase

### Когда НЕ использовать

❌ **НЕ используйте для:**
- Сложных рефакторингов (требуют manual work)
- Логических багов (не code style)
- Architecture changes

### Best Practices

1. **ВСЕГДА preview перед apply:**
   ```javascript
   // ✅ ПРАВИЛЬНО
   apply_code_fixes(diagnosticId: "IDE0005", preview: true)
   // Смотрим что изменится

   apply_code_fixes(diagnosticId: "IDE0005", preview: false)
   // Применяем

   // ❌ ОПАСНО - не знаем что изменится
   apply_code_fixes(diagnosticId: "all", preview: false)
   ```

2. **Исправляйте по одной diagnostic:**
   ```javascript
   // ✅ Хорошо - контролируемо
   apply_code_fixes(diagnosticId: "IDE0005", preview: false)
   apply_code_fixes(diagnosticId: "CS8019", preview: false)

   // ⚠️ Осторожно - много изменений сразу
   apply_code_fixes(diagnosticId: "all", preview: false)
   ```

3. **Проверяйте после apply:**
   ```javascript
   apply_code_fixes(diagnosticId: "IDE0005", preview: false)

   // Проверяем компиляцию
   // Output: "✅ Compilation: Success"

   // Запускаем tests
   // dotnet test

   // Повторный анализ
   analyze_code_style(severityFilter: "Warning")
   ```

4. **Используйте Undo если что-то не так:**
   ```javascript
   apply_code_fixes(diagnosticId: "IDE0028", preview: false)
   // Ой, это сломало код!

   undo()
   // Откатили
   ```

### Производительность

- **Preview:** 5-15 сек (зависит от количества instances)
- **Apply:** 10-30 сек (+ время на Git commit)
- **Параллелизм:** Обработка проектов параллельная

**Факторы:**
- Количество instances
- Количество файлов
- Размер solution

### Частые диагностики для auto-fix

```javascript
// Удалить unused usings (самое частое)
diagnosticId: "IDE0005"  // или "CS8019"

// Упростить collection initialization
diagnosticId: "IDE0028"

// Использовать var
diagnosticId: "IDE0090"

// Упростить object initialization
diagnosticId: "IDE0017"

// Все сразу (осторожно!)
diagnosticId: "all"
```

### Связанные инструменты

- ⬅️ [**AnalyzeCodeStyle**](#analyze_code_style) — найти что нужно исправить
- ⬅️ [**FormatCode**](#format_code) — форматирование перед fixes
- ➡️ [**Undo**](MODIFICATION_TOOLS.md#undo) — откат если что-то не так

---

## Workflow: Комплексное улучшение качества

### Стандартный workflow

```javascript
// 1. Форматирование
format_code(path: "src/", checkOnly: true)
// Output: "12 files need formatting"

format_code(path: "src/", checkOnly: false)
// Применили форматирование

// 2. Анализ
analyze_code_style(severityFilter: "Warning")
// Output: "43 warnings, 28 auto-fixable"

// 3. Автоматические исправления
apply_code_fixes(diagnosticId: "IDE0005", preview: true)
// Смотрим что изменится

apply_code_fixes(diagnosticId: "IDE0005", preview: false)
// Применили unused usings fix

apply_code_fixes(diagnosticId: "all", preview: true)
// Смотрим остальные fixes

apply_code_fixes(diagnosticId: "all", preview: false)
// Применили все

// 4. Повторный анализ
analyze_code_style(severityFilter: "Warning")
// Output: "15 warnings" - остались только manual fixes

// 5. Manual fixes (вне SharpTools)
// Review оставшихся 15 warnings
// Исправляем вручную через IDE или modification tools

// 6. Финальная проверка
format_code(path: "src/", checkOnly: true)
analyze_code_style(severityFilter: "Warning")
// Всё чисто ✅
```

### After modification workflow

```javascript
// 1. Модификация
add_member(
    fullyQualifiedTargetName: "UserService",
    codeSnippet: "...",
    commitMessage: "Add ValidateEmail method"
)
// Output: "✅ No errors. ⚠️ Consider formatting"

// 2. Quality checks (автоматический workflow)
format_code(
    path: "src/Services/UserService.cs",
    checkOnly: false
)

analyze_code_style(severityFilter: "Warning")
// Check new warnings

apply_code_fixes(diagnosticId: "all", preview: false)
// Fix any auto-fixable issues

// 3. Done ✅
```

### CI/CD Integration

```bash
# В CI/CD pipeline

# 1. Проверка форматирования
format_code(path: "src/", checkOnly: true)
# Если вернул "files need formatting" - FAIL BUILD

# 2. Анализ
analyze_code_style(severityFilter: "Error")
# Если есть errors - FAIL BUILD

analyze_code_style(severityFilter: "Warning")
# Report warnings (но не fail)

# 3. Можно автоматически исправлять (опционально)
# apply_code_fixes(diagnosticId: "all", preview: false)
# Создать PR с fixes
```

---

## Сравнение Quality Tools

| Инструмент | Что делает | Изменяет код | Git commit | Скорость |
|------------|-----------|-------------|-----------|---------|
| **FormatCode** | Единый code style | ✅ Да | ✅ Да | Быстро (1-5 сек) |
| **AnalyzeCodeStyle** | Находит проблемы | ❌ Нет | ❌ Нет | Средне (15-90 сек) |
| **ApplyCodeFixes** | Исправляет проблемы | ✅ Да | ✅ Да | Средне (10-30 сек) |

---

## См. также

- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — модификации с автоматическими quality checks
- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — анализ кода
- 📚 [**SOLUTION_TOOLS.md**](SOLUTION_TOOLS.md) — загрузка solution
- 📚 [**README.md**](../README.md) — главная документация

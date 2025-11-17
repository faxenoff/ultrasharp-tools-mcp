# Инструменты работы с Solution

**Точка входа для всех операций с C# проектами.** Загружает .sln файл, инициализирует MSBuildWorkspace и предоставляет структурную карту проекта для навигации.

## 📋 Quick Reference

| Инструмент | Назначение | Когда использовать |
|------------|-----------|-------------------|
| **LoadSolution** | Загрузить .sln файл и инициализировать workspace | **ВСЕГДА** в начале работы с проектом |
| **LoadProject** | Получить структурную карту проекта | После LoadSolution для навигации по типам |

---

## load_solution

**Критически важный инструмент** — инициализирует MSBuildWorkspace и загружает .sln файл. Без него другие инструменты работать не будут.

### Использование

```javascript
load_solution(
    solutionPath: "D:/MyProject/MyProject.sln"
)
```

### Параметры

- **solutionPath** (required): Полный путь к .sln файлу

### Что делает

1. 🔧 Инициализирует MSBuildWorkspace с правильной конфигурацией
2. 📂 Загружает все проекты из .sln файла
3. 🔍 Создаёт **FastSymbolIndex** (355k+ символов за ~21.7 сек)
4. 💾 Настраивает кэши для Compilation и SemanticModel
5. 📦 Резолвит все NuGet зависимости (параллельно)
6. 🔗 Загружает reflection кэш для внешних библиотек (параллельно)
7. 🌳 Создаёт Git репозиторий или открывает существующий (если не --disable-git)

### Производительность

**Типичные времена загрузки:**
- Малый проект (3-5 проектов, ~50k строк): 5-10 сек
- Средний проект (10-20 проектов, ~200k строк): 15-30 сек
- Большой проект (50+ проектов, ~1M строк): 45-90 сек

**Основные фазы:**
```
Phase 1: MSBuild Solution Loading        [5-15 сек]
Phase 2: FastSymbolIndex Creation        [15-30 сек]
Phase 3: NuGet Resolution (parallel)     [0.3-0.8 сек]
Phase 4: Reflection Cache (parallel)     [0.5-2 сек]
Phase 5: Git Initialization              [0.1-0.5 сек]
```

**Оптимизации:**
- ✅ Параллельная загрузка assemblies (4-5x ускорение)
- ✅ Параллельный резолв NuGet (3-4x ускорение)
- ✅ Bloom filter для FastSymbolIndex (5-300x ускорение поиска)
- ✅ FrozenDictionary для reflection кэша (20-30% быстрее)

### Пример вывода

```
Solution loaded successfully: MyProject.sln

📊 Statistics:
   Projects: 12
   Documents: 847
   Symbols Indexed: 89,347
   Index Build Time: 18.3s
   NuGet Packages: 156 (resolved in 0.4s)
   External Assemblies: 42 (cached in 1.2s)

✅ Workspace ready
🌳 Git integration enabled (branch: sharptools/20251113-143022)

💡 Next: Use load_project to explore project structure
```

### Когда использовать

✅ **ВСЕГДА в начале работы:**
- Перед любым анализом кода
- Перед любой модификацией
- При переключении на другой solution
- После git pull с большими изменениями

### Когда НЕ использовать

❌ **НЕ нужно повторно вызывать:**
- Между операциями (workspace кэшируется)
- При работе с одним и тем же solution
- Для обновления после своих изменений (автоматически)

⚠️ **Требует перезагрузки только если:**
- Были внешние изменения (другой разработчик/IDE)
- Добавлены/удалены проекты из .sln
- Изменены NuGet зависимости

### Best Practices

1. **Всегда первый вызов:**
   ```javascript
   // ✅ Правильно
   load_solution("D:/MyProject/MyProject.sln")
   load_project("MyProject.Core")
   view_definition("MyNamespace.MyClass")

   // ❌ Неправильно - load_solution пропущен
   view_definition("MyNamespace.MyClass") // ERROR: Solution not loaded
   ```

2. **Используйте абсолютные пути:**
   ```javascript
   // ✅ Правильно
   load_solution("D:/Projects/MyApp/MyApp.sln")

   // ❌ Плохо - относительные пути могут не работать
   load_solution("../MyApp.sln")
   ```

3. **Проверяйте build configuration:**
   ```bash
   # При запуске сервера укажите нужную конфигурацию
   UltrasharpTools.Droid.exe --build-configuration Release
   ```

4. **Логируйте для диагностики:**
   ```bash
   # Используйте Debug логи при проблемах с загрузкой
   UltrasharpTools.Droid.exe --log-level Debug --log-directory ./Run.Logs
   ```

### Типичные ошибки

#### ❌ Ошибка: "Solution file not found"
```
ERROR: Solution file not found: D:/MyProject/MyProject.sln
```
**Решение:**
- Проверьте путь (абсолютный, не относительный)
- Проверьте существование файла
- Проверьте права доступа

#### ❌ Ошибка: "The .NET SDK for this solution is not installed"
```
ERROR: The current .NET SDK does not support targeting .NET 6.0
```
**Решение:**
- Установите соответствующий .NET SDK
- Проверьте: `dotnet --list-sdks`
- Для .NET 6: установите .NET 6 SDK

#### ❌ Ошибка: "MSBuild project load failed"
```
ERROR: Failed to load project MyProject.csproj: The imported project "..." was not found
```
**Решение:**
- Проверьте что проект собирается: `dotnet build MyProject.sln`
- Восстановите NuGet: `dotnet restore MyProject.sln`
- Проверьте пути в .csproj файлах

### Производительность: FastSymbolIndex

**Что это:**
- Bloom filter + хэш-таблица для мгновенного поиска символов
- Индексирует ALL символы из solution: types, methods, properties, fields, events

**Метрики:**
```
Small project (50k LOC):    15k symbols in 3-5 сек
Medium project (200k LOC):  60k symbols in 10-15 сек
Large project (1M LOC):     355k symbols in 21.7 сек
```

**Скорость поиска:**
- Без индекса: O(N) через все проекты — 5-15 сек
- С индексом: O(1) через Bloom filter — 0.05-0.3 сек
- **Ускорение: 5-300x** (зависит от размера проекта)

**Память:**
- Bloom filter: ~100 KB (fixed)
- Hash tables: ~2-10 MB (зависит от количества символов)

### Связанные инструменты

- ➡️ [**LoadProject**](#load_project) — следующий шаг послеload_solutionn
- ➡️ [**ViewDefinition**](ANALYSIS_TOOLS.md#view_definition) — просмотр кода после загрузки
- ➡️ [**SearchDefinitions**](ANALYSIS_TOOLS.md#search_definitions) — поиск по индексу
- ➡️ [**Undo**](MODIFICATION_TOOLS.md#UltrasharpTool_undo) — работает с Git, созданным прload_solutionon

---

## load_project

**Структурная карта проекта** — возвращает иерархию namespaces → types для навигации и понимания архитектуры.

### Использование

```javascript
load_project(
    projectName: "MyProject.Core"
)
```

### Параметры

- **projectName** (required): Имя проекта из solution (без пути, без .csproj)

### Что показывает

Адаптивная структура проекта с 3 уровнями детализации:

1. **OverviewOnly** (< 50 types):
   ```
   Namespace.SubNamespace
   ├─ Class1
   ├─ Class2
   └─ Interface1
   ```

2. **TypesAndPublicMembers** (50-200 types):
   ```
   Namespace.SubNamespace
   ├─ Class1
   │  ├─ Method1(string param)
   │  └─ Property1 { get; set; }
   └─ Class2
   ```

3. **Full** (> 200 types):
   ```
   Namespace.SubNamespace
   ├─ Class1 (public class)
   │  ├─ Method1(string param) : void
   │  ├─ Method2(int x, int y) : int
   │  ├─ Property1 { get; set; } : string
   │  └─ _field1 : int (private)
   ```

**Автоматическая адаптация по размеру:**
- Малые проекты → больше деталей
- Большие проекты → краткая overview (чтобы не переполнить токены)

### Пример вывода

```
Project: MyProject.Core (847 documents, 12,453 types)
Detail Level: TypesAndPublicMembers (50-200 types)

═══════════════════════════════════════════════════════════
📦 MyProject.Core.Domain
═══════════════════════════════════════════════════════════

MyProject.Core.Domain.Entities
├─ User (public class)
│  ├─ Id : int
│  ├─ Name : string
│  ├─ Email : string
│  └─ Validate() : bool
│
├─ Order (public class)
│  ├─ OrderId : Guid
│  ├─ UserId : int
│  ├─ Items : List<OrderItem>
│  ├─ Total() : decimal
│  └─ Submit() : Task<bool>

MyProject.Core.Domain.Interfaces
├─ IUserRepository (public interface)
│  ├─ GetByIdAsync(int id) : Task<User>
│  ├─ CreateAsync(User user) : Task<int>
│  └─ UpdateAsync(User user) : Task<bool>

═══════════════════════════════════════════════════════════
📦 MyProject.Core.Services
═══════════════════════════════════════════════════════════

MyProject.Core.Services
├─ UserService (public class)
│  ├─ Constructor(IUserRepository repo)
│  ├─ GetUserAsync(int id) : Task<User>
│  └─ CreateUserAsync(string name, string email) : Task<int>

💡 Use view_definition with FQN to see full source code
💡 Example: view_definition("MyProject.Core.Domain.Entities.User")
```

### Когда использовать

✅ **Сразу после LoadSolution:**
- Понять структуру незнакомого проекта
- Найти нужные типы для дальнейшего анализа
- Определить entry points (Controllers, Services)
- Изучить domain model

✅ **Для навигации:**
- Найти FQN для использования с другими инструментами
- Увидеть публичный API проекта
- Понять разбиение на namespaces

### Когда НЕ использовать

❌ **НЕ нужен если:**
- Вы уже знаете FQN нужного типа (используйте ViewDefinition напрямую)
- Ищете конкретный паттерн в коде (используйте SearchDefinitions)
- Нужна реализация метода (используйте ViewDefinition)

### Best Practices

1. **Используйте для first-time exploration:**
   ```javascript
   // ✅ Правильный workflow для нового проекта
   load_solution("D:/MyProject/MyProject.sln")
   load_project("MyProject.Core")       // Получаем обзор
   view_definition("MyProject.Core.Services.UserService") // Детали
   ```

2. **Точное имя проекта:**
   ```javascript
   // ✅ Правильно - имя проекта из .sln
   load_project("MyProject.Core")

   // ❌ Неправильно
   load_project("MyProject.Core.csproj")  // Без расширения!
   load_project("src/MyProject.Core")     // Без пути!
   ```

3. **Используйте для документации:**
   ```javascript
   // Создайте архитектурную документацию
   load_project("MyProject.API")      // Controllers
   load_project("MyProject.Core")     // Business Logic
   load_project("MyProject.Data")     // Data Access
   ```

### Типичные ошибки

#### ❌ Ошибка: "Project not found"
```
ERROR: Project 'MyProject.Core' not found in solution
```
**Решение:**
- Проверьте точное имя проекта в .sln файле
- Используйте имя без .csproj расширения
- Проверьте что проект загружен в solution

#### ❌ Ошибка: "LoadSolution must be called first"
```
ERROR: Solution not loaded. Call load_solution first.
```
**Решение:**
- Сначала вызовите LoadSolution
- Проверьте что LoadSolution вернул успех

### Адаптивная детализация

**Логика выбора уровня:**
```csharp
if (typeCount < 50)
    detailLevel = DetailLevel.OverviewOnly;
else if (typeCount < 200)
    detailLevel = DetailLevel.TypesAndPublicMembers;
else
    detailLevel = DetailLevel.Full;
```

**Зачем:**
- Экономия токенов для больших проектов
- Детальная информация для малых проектов
- Предотвращение переполнения контекста

**Можно изменить:**
- В коде: `LoadProjectDetailLevel` enum
- Будущая feature: параметр `detailLevel` (см. TODO.md)

### Связанные инструменты

- ⬅️ [**LoadSolution**](#load_solution) — обязательно вызвать перед LoadProject
- ➡️ [**ViewDefinition**](ANALYSIS_TOOLS.md#view_definition) — детальный просмотр типа
- ➡️ [**GetMembers**](ANALYSIS_TOOLS.md#get_members) — получить все члены типа
- ➡️ [**SearchDefinitions**](ANALYSIS_TOOLS.md#search_definitions) — поиск по regex

---

## Типичные сценарии использования

### Первое знакомство с проектом

```javascript
// 1. Загружаем solution
load_solution("D:/MyProject/MyProject.sln")

// 2. Смотрим структуру основных проектов
load_project("MyProject.API")      // Entry point
load_project("MyProject.Core")     // Business logic
load_project("MyProject.Data")     // Data access

// 3. Детальный анализ интересующих типов
view_definition("MyProject.API.Controllers.UserController")
get_members("MyProject.Core.Services.UserService", includePrivateMembers: false)
```

### Анализ незнакомой feature

```javascript
// 1. Загружаем solution
load_solution("D:/LegacyApp/LegacyApp.sln")

// 2. Ищем entry point по названию
search_definitions("OrderProcessing")

// 3. Смотрим структуру найденного проекта
load_project("LegacyApp.Orders")

// 4. Анализируем найденные типы
view_definition("LegacyApp.Orders.OrderProcessor")
find_references("LegacyApp.Orders.OrderProcessor.ProcessOrder")
```

### Подготовка к рефакторингу

```javascript
// 1. Загружаем solution
load_solution("D:/Refactoring/MyApp.sln")

// 2. Получаем overview всех проектов
load_project("MyApp.Core")
load_project("MyApp.Services")
load_project("MyApp.Data")

// 3. Ищем дублирующуюся логику
search_definitions("ValidateUser")

// 4. Анализируем complexity
analyze_complexity(scope: "project", target: "MyApp.Core")
```

---

## Производительность и оптимизация

### Кэширование

**Что кэшируется посload_solutionion:**
- ✅ MSBuildWorkspace (singleton)
- ✅ Solution (до следующего LoadSolution)
- ✅ Compilation для каждого проекта (LRU cache, 10 items)
- ✅ SemanticModel для каждого документа (LRU cache, 50 items)
- ✅ FastSymbolIndex (весь индекс в памяти)
- ✅ Reflection cache для external assemblies (FrozenDictionary)

**Invalidation:**
- ✅ Автоматически при модификации через SharpTools
- ✅ Вручную при внешних изменениях (требуется ReloadSolution — см. TODO.md)

### Memory Usage

**Типичное потребление памяти:**
```
Small project (3-5 projects):     200-400 MB
Medium project (10-20 projects):  500-1000 MB
Large project (50+ projects):     1.5-3 GB
```

**Breakdown:**
- MSBuildWorkspace: 30-40%
- Compilations cache: 20-30%
- FastSymbolIndex: 5-10%
- Reflection cache: 10-15%
- Other: 15-25%

### Советы по оптимизации

1. **Используйте Static PGO для production:**
   ```powershell
   .\Utils\publish-mcp-static-pgo.ps1 -Server MCP
   ```
   **Результат:** 10-15% ускорение hot paths

2. **Не перезагружайте solution без необходимости:**
   ```javascript
   // ❌ Плохо - ненужная перезагрузка
   load_solution(...)
   add_member(...)
   load_solution(...)  // НЕ НУЖНО!

   // ✅ Хорошо - solution обновляется автоматически
   load_solution(...)
   add_member(...)
   view_definition(...) // Видит изменения
   ```

3. **Используйте правильный log level:**
   ```bash
   # Production
   --log-level Information  # Только важные события

   # Development
   --log-level Debug        # Подробная диагностика

   # Performance testing
   --log-level Warning      # Минимум логов
   ```

---

## Тестовый пример

### Быстрый тест LoadSolution

```bash
# 1. Запустите сервер
cd UltrasharpTools.Droid
dotnet run -- --log-level Debug

# 2. Через MCP вызовите
load_solution(solutionPath: "D:/YourProject/YourProject.sln")

# 3. Проверьте логи
# Должны увидеть:
# - "Loading solution: YourProject.sln"
# - "Building FastSymbolIndex..."
# - "FastSymbolIndex built: 89,347 symbols in 18.3s"
# - "Solution loaded successfully"
```

### Тест LoadProject

```javascript
// После успешного LoadSolution
load_project(projectName: "YourProject.Core")

// Проверьте:
// - Вывод содержит namespaces
// - Вывод содержит types
// - FQN корректные (namespace.typename)
```

### Тест производительности

```bash
# Замерьте время загрузки
time dotnet run -- --load-solution "D:/MyProject/MyProject.sln"

# Сравните с expected:
# Small:  5-10 сек
# Medium: 15-30 сек
# Large:  45-90 сек
```

---

## Дополнительные опции командной строки

При запуске серверов (Droid или Overlord) доступны опции для настройки поведения:

```bash
UltrasharpTools.Droid.exe \
  --load-solution "D:/MyProject/MyProject.sln" \
  --build-configuration "Release" \
  --disable-git \
  --log-level Information \
  --log-directory "./Run.Logs"
```

**Опции:**
- `--load-solution <path>` — загрузить .sln при старте (опционально, лучше через load_solution)
- `--build-configuration <config>` — Debug или Release (default: Debug)
- `--disable-git` — отключить Git интеграцию
- `--log-level <level>` — Verbose, Debug, Information, Warning, Error, Fatal
- `--log-directory <path>` — директория для логов (только Droid)
- `--log-file <path>` — файл для логов (только Overlord)
- `--port <number>` — порт для HTTP (только Overlord, default: 3001)

---

## См. также

- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — анализ кода после загрузки
- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — модификация кода
- 📚 [**QUALITY_TOOLS.md**](QUALITY_TOOLS.md) — форматирование и линтинг
- 📚 [**README.md**](../README.md) — главная документация проекта

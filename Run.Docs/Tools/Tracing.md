# Инструменты трейсинга кода

**Статический анализ потоков выполнения** — трейсинг вперёд (от entry point) и назад (от crash point) для понимания логики и отладки production issues.

## 📋 Quick Reference

| Инструмент | Направление | Input | Output | Use case |
|------------|-------------|-------|--------|----------|
| **TraceExecution** | Forward (→) | Entry point FQN | Execution trace | Понять что делает код |
| **TraceBackwards** | Backward (←) | Crash point FQN | Multiple paths (cached ⚡) | Понять как дошли до ошибки |
| **AnalyzePathFeasibility** | Forward (all paths) | Entry point FQN | Symbolic paths + issues | Верификация и поиск багов |

---

SharpTools включает три мощных инструмента для анализа потока выполнения кода:

## trace_execution

**Прямой трейсинг** — от точки входа к точке выхода.

### Использование

```javascript
trace_execution(
    entryPointFqn: "TestTracing.UserService.ProcessUser",
    exitPointFqn: "TestTracing.DatabaseService.SaveUser",  // опционально
    maxDepth: 10,
    includeExternalCalls: true
)
```

### Параметры

- **entryPointFqn** (required): FQN метода, с которого начать трейсинг
- **exitPointFqn** (optional): FQN метода, на котором остановить трейсинг
- **maxDepth** (default: 10): Максимальная глубина вызовов (1-50)
- **includeExternalCalls** (default: true): Показывать ли вызовы внешних библиотек

### Что показывает

- 🚀 **Entry point** — точка входа с параметрами
- 📞 **Method calls** — вызовы методов
- ✏️  **Assignments** — присваивания переменных
- 🆕 **Object creation** — создание объектов
- 🔀 **Branches** — ветвления (if/switch)
- ↩️  **Returns** — возвращаемые значения
- 📦 **External calls** — вызовы внешних библиотек (только сигнатура)

### Пример вывода

```
Execution Trace: TestTracing.UserService.ProcessUser
════════════════════════════════════════════════════════════

[1] 🚀 ENTRY: TestTracing.UserService.ProcessUser(string userName)
    ├─ userName: string (parameter)
    └─ Program.cs:18

[2] 📦 EXTERNAL: System.Console.WriteLine(string)

[3] 📞 CALL: ValidateUser(string)
    ├─ userName: string (passed)
    └─ Program.cs:20

[4] 🔀 BRANCH: _validator.ValidateUser(userName)
    └─ Program.cs:20

[5] 📞 CALL: CreateUser(string)
    ├─ name: string (passed)

[6] 🆕 NEW: TestTracing.User
    └─ Program.cs:28

[7] 📞 CALL: SaveUser(User)
    ├─ user: User (passed)
    └─ Program.cs:23

[8] 🎯 EXIT: TestTracing.DatabaseService.SaveUser
```

### Когда использовать

✅ **Для понимания нового кода:**
- Как работает feature от начала до конца
- Что происходит внутри API endpoint
- Последовательность вызовов в бизнес-логике

✅ **Для отладки:**
- Понять откуда берутся данные
- Проследить data flow через методы
- Найти где происходит трансформация данных

✅ **Для документации:**
- Создать flow diagrams
- Понять sequence diagrams
- Документировать сложные workflows

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужно найти КТО вызывает метод → `find_references` или `trace_backwards`
- Ищете конкретный баг в одном методе → `view_definition`
- Нужна реализация без execution flow → `view_definition`
- Код использует много reflection/dynamic → TraceExecution не увидит

### Best Practices

1. **Начинайте с малой глубины:**
   ```javascript
   // ✅ Сначала shallow trace
   trace_execution(entryPointFqn: "UserService.ProcessUser", maxDepth: 5)

   // Если нужно больше деталей
   trace_execution(entryPointFqn: "UserService.ProcessUser", maxDepth: 15)
   ```

2. **Используйте exitPoint для фокусировки:**
   ```javascript
   // Trace только до конкретной точки
   trace_execution(
       entryPointFqn: "UserController.Post",
       exitPointFqn: "DatabaseService.SaveUser"
   )
   ```

3. **Отключайте external calls если не нужны:**
   ```javascript
   // Только ваш код
   trace_execution(
       entryPointFqn: "...",
       includeExternalCalls: false
   )
   ```

4. **Комбинируйте с ViewDefinition:**
   ```javascript
   // Trace показывает что вызывается
   trace_execution(entryPointFqn: "ProcessUser")
   // Output: "Calls ValidateUser, CreateUser, SaveUser"

   // ViewDefinition показывает детали каждого
   view_definition("UserService.ValidateUser")
   view_definition("UserService.CreateUser")
   ```

### Производительность

- **Shallow (depth 1-5):** 1-3 сек
- **Medium (depth 6-15):** 3-10 сек
- **Deep (depth 16-50):** 10-30 сек

**Факторы:**
- Глубина trace
- Сложность CFG (Control Flow Graph)
- Количество ветвлений
- External calls

### Связанные инструменты

- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#view_definition) — посмотрите entry point перед trace
- ➡️ [**TraceBackwards**](#trace_backwards) — обратное направление
- ➡️ [**FindReferences**](ANALYSIS_TOOLS.md#UltrasharpTool_findreferences) — где вызывается entry point
- ➡️ [**AnalyzeLogs**](LOG_ANALYSIS_TOOLS.md) — найти entry point в production logs

---

## trace_backwards

**Обратный трейсинг** — от точки падения/ошибки к возможным точкам входа.

### Использование

```javascript
trace_backwards(
    crashPointFqn: "TestTracing.UserService.ThrowInvalidUserException",
    startPointFqn: "TestTracing.Program.Main",  // опционально
    stackTraceHints: [
        "at TestTracing.UserService.CreateUser",
        "at TestTracing.UserService.ProcessUser",
        "at TestTracing.Program.Main"
    ],
    maxDepth: 15,
    maxPaths: 5,
    includeExternalCallers: false
)
```

### Параметры

- **crashPointFqn** (required): FQN метода, где произошла ошибка
- **startPointFqn** (optional): Ожидаемая точка входа (если не указана, ищет все entry points)
- **stackTraceHints** (optional): Строки из stack trace для ранжирования путей
- **maxDepth** (default: 15): Максимальная глубина поиска (1-50)
- **maxPaths** (default: 5): Максимальное количество путей (1-20)
- **includeExternalCallers** (default: false): Включать ли вызовы из внешних библиотек

### Что показывает

- Несколько возможных путей выполнения
- **Confidence score** для каждого пути (на основе stack trace)
- 📍 Маркеры для frames, совпадающих со stack trace
- Информацию о call sites (где вызван метод)
- Параметры методов

### Пример вывода

```
Backtrace from: TestTracing.UserService.ThrowInvalidUserException
════════════════════════════════════════════════════════════

Path #1 (Confidence: 90%, Depth: 4)
────────────────────────────────────────────────────────────
✓ Reached entry point

📍[0] CALLED FROM: Main
   └─ TestTracing.Program.Main(string[] args)
      Parameters: string[] args
      Called from: Program.cs:7

📍[1]   CALLED FROM: ProcessUser
     └─ TestTracing.UserService.ProcessUser(string userName)
        Parameters: string userName
        Called from: Program.cs:18
        Expression: service.ProcessUser("John Doe")

📍[2]     CALLED FROM: CreateUser
       └─ TestTracing.UserService.CreateUser(string name)
          Parameters: string name
          Called from: Program.cs:22
          Expression: CreateUser(userName)

  [3]       CRASH: ThrowInvalidUserException
         └─ TestTracing.UserService.ThrowInvalidUserException()
            Called from: Program.cs:32
            Expression: ThrowInvalidUserException()
```

### Когда использовать

✅ **Для debugging production crashes:**
- У вас есть stack trace из production logs
- Нужно понять КАК дошли до проблемного метода
- Найти все возможные пути к crash point

✅ **Для impact analysis:**
- Перед изменением метода - кто может его вызвать
- Понять все entry points ведущие к методу
- Найти неожиданные call paths

✅ **Для code review:**
- Проверить откуда вызывается критичный код
- Убедиться что validation происходит перед вызовом
- Найти missing error handling в call chain

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужен forward trace (что делает метод) → `trace_execution`
- Нужны все references (не только call paths) → `find_references`
- Ищете реализацию метода → `view_definition`
- Нет stack trace hints → результаты могут быть неточными

### Best Practices

1. **ВСЕГДА используйте stack trace hints:**
   ```javascript
   // ✅ С hints - точные результаты
   trace_backwards(
       crashPointFqn: "OrderService.ProcessOrder",
       stackTraceHints: [
           "at OrderService.ProcessOrder",
           "at OrderProcessor.Execute",
           "at BackgroundJob.Run"
       ]
   )

   // ⚠️ Без hints - может быть много false positives
   trace_backwards(crashPointFqn: "OrderService.ProcessOrder")
   ```

2. **Начните с малого maxPaths:**
   ```javascript
   // Сначала top 5 наиболее вероятных
   trace_backwards(..., maxPaths: 5)

   // Если не нашли - увеличьте
   trace_backwards(..., maxPaths: 10)
   ```

3. **Укажите startPoint если знаете:**
   ```javascript
   // Если знаете entry point
   trace_backwards(
       crashPointFqn: "...",
       startPointFqn: "MyController.Post"
   )
   ```

4. **Комбинируйте с AnalyzeLogs:**
   ```javascript
   // 1. Найти crash в production logs
   analyze_logs(filePath: "prod.log", levels: ["Fatal"])
   // Output: stack trace

   // 2. Trace backwards с stack trace hints
   trace_backwards(
       crashPointFqn: "...",
       stackTraceHints: [/* из логов */]
   )
   ```

### Производительность

**⚡ С кэшированием (Variant A - 2025-11-13):**
- **Cold cache (первый запуск):** 2-15 сек
- **Warm cache (повторный запуск):** **0.3-2 сек (5-10x быстрее!)**

**Время по глубине:**
- **Shallow search (depth 1-5):**
  - Cold: 2-5 сек
  - Warm: 0.3-1 сек ✅
- **Medium search (depth 6-15):**
  - Cold: 5-15 сек
  - Warm: 1-3 сек ✅
- **Deep search (depth 16-30):**
  - Cold: 15-45 сек
  - Warm: 3-8 сек ✅

**Кэширование:**
- ✅ **SQLite persistent cache** хранит полные данные о callers с Location информацией
- ✅ **CallGraphFull table** с индексами по MethodFqn, FilePath, Timestamp
- ✅ **Автоматическая инвалидация** при изменении solution hash
- ✅ **JSON Source Generation** для 2-5x быстрой сериализации
- ✅ **CachedCallerInfo wrapper** для унификации cache HIT/MISS путей

**Cache hit rate:** 80-95% для типичных workflows

**Факторы:**
- Глубина поиска (maxDepth)
- Количество путей (maxPaths)
- Размер codebase
- Количество callers на каждом уровне
- **Cache warmness (cold vs warm)**

**Confidence scoring:**
- 90-100%: Perfect match со stack trace
- 70-89%: Partial match
- 50-69%: Возможный path
- < 50%: Unlikely path

### Связанные инструменты

- ⬅️ [**AnalyzeLogs**](LOG_ANALYSIS_TOOLS.md) — получить stack trace из production logs
- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#view_definition) — посмотрите crash point перед trace
- ➡️ [**TraceExecution**](#trace_execution) — прямое направление для понимания логики
- ➡️ [**FindReferences**](ANALYSIS_TOOLS.md#UltrasharpTool_findreferences) — все references (не только call paths)

---

## analyze_path_feasibility

**Символьное выполнение (Symbolic Execution)** — анализ всех возможных путей выполнения с проверкой выполнимости условий через Z3 SMT solver.

### Использование

```javascript
analyze_path_feasibility(
    entryPointFqn: "TestTracing.Calculator.Divide",
    exitPointFqn: "TestTracing.Calculator.ThrowException",  // опционально
    maxDepth: 10,
    initialConstraints: {
        "x": "x > 0",
        "y": "y != 0"
    }
)
```

### Параметры

- **entryPointFqn** (required): FQN метода для анализа
- **exitPointFqn** (optional): FQN целевой точки (анализ фокусируется на путях к ней)
- **maxDepth** (default: 10): Максимальная глубина исследования путей (1-50)
- **initialConstraints** (optional): Начальные ограничения на параметры метода (JSON dictionary)

### Что показывает

- 🔀 **Все возможные пути выполнения** через метод
- ✅ **Feasible paths** — выполнимые пути с примерами входных данных
- ❌ **Infeasible paths** — невыполнимые пути (противоречивые условия)
- 🐛 **Potential issues** — найденные проблемы:
  - Division by zero
  - Null reference
  - Array out of bounds
  - Invalid casts
- 📊 **Symbolic constraints** — условия на каждом пути
- 🎯 **Example inputs** — конкретные значения, активирующие путь

### Пример вывода

```
Symbolic Execution: TestTracing.Calculator.Divide
════════════════════════════════════════════════════════════

Summary:
  Total Paths: 3
  Feasible Paths: 2
  Infeasible Paths: 1
  Issues Found: 2
  Exit Point Reachable: true
  Max Depth Reached: 5

Path #1 (FEASIBLE, Depth: 3)
────────────────────────────────────────────────────────────
Constraints:
  - y != 0
  - y > 0
Example Inputs: { y: 5, x: 10 }

  [1] ENTRY: Divide(int x, int y)
      State: { x: symbolic_int, y: symbolic_int }

  [2] BRANCH: y > 0 → TRUE
      State: { x: symbolic_int, y: symbolic_int }
      Added Constraint: y > 0

  [3] ASSIGNMENT: result = x / y
      State: { x: symbolic_int, y: symbolic_int, result: x/y }

  [4] RETURN: return result

Path #2 (FEASIBLE, Depth: 3)
────────────────────────────────────────────────────────────
Constraints:
  - y != 0
  - y <= 0
Example Inputs: { y: -5, x: 10 }

Issues:
  ⚠️ Warning: Potential division by zero
     Location: Calculator.cs:15
     Triggering Constraints: [y <= 0]

  [1] ENTRY: Divide(int x, int y)
  [2] BRANCH: y > 0 → FALSE
      Added Constraint: y <= 0
  [3] ASSIGNMENT: result = x / y
  [4] RETURN: return result

Path #3 (INFEASIBLE, Depth: 2)
────────────────────────────────────────────────────────────
Infeasibility Reason: Contradictory constraints (y == 0 AND y != 0)

  [1] ENTRY: Divide(int x, int y)
  [2] BRANCH: y == 0 → TRUE
      Added Constraint: y == 0
```

### Когда использовать

✅ **Для верификации кода:**
- Проверить все возможные сценарии выполнения
- Найти dead code (недостижимые пути)
- Проверить корректность условий
- Выявить противоречивые ограничения

✅ **Для поиска багов:**
- Division by zero detection
- Null reference detection
- Array out of bounds
- Invalid casts
- Unreachable exit points

✅ **Для генерации тестов:**
- Получить example inputs для каждого пути
- Автоматически создать test cases
- Убедиться в покрытии всех веток

✅ **Для security analysis:**
- Найти vulnerability paths
- Проверить constraint validation
- Обнаружить edge cases

### Когда НЕ использовать

❌ **НЕ используйте если:**
- Нужен просто trace выполнения → `trace_execution`
- Нужны caller paths → `trace_backwards`
- Метод слишком большой (>50 строк) → разбейте на части
- Много reflection/dynamic → symbolic execution не увидит

### Best Practices

1. **Начинайте с малых методов:**
   ```javascript
   // ✅ Хорошо - маленький метод
   analyze_path_feasibility(
       entryPointFqn: "Calculator.Add"  // 5-10 строк
   )

   // ⚠️ Плохо - большой метод
   analyze_path_feasibility(
       entryPointFqn: "OrderProcessor.ProcessOrder"  // 200 строк, 50 веток
   )
   ```

2. **Используйте initialConstraints для фокусировки:**
   ```javascript
   // Проверить только сценарий с отрицательными числами
   analyze_path_feasibility(
       entryPointFqn: "Calculator.Sqrt",
       initialConstraints: { "x": "x < 0" }
   )
   ```

3. **Ограничьте maxDepth для производительности:**
   ```javascript
   // Быстрый анализ
   analyze_path_feasibility(..., maxDepth: 5)

   // Глубокий анализ (медленно)
   analyze_path_feasibility(..., maxDepth: 20)
   ```

4. **Комбинируйте с TraceExecution:**
   ```javascript
   // 1. Понять поток выполнения
   trace_execution(entryPointFqn: "ProcessOrder")

   // 2. Проверить feasibility критичных методов
   analyze_path_feasibility(entryPointFqn: "ValidatePayment")
   ```

### Производительность

**Время анализа:**
- **Simple method (5 paths, 10 constraints):** ~500ms
- **Medium method (10 paths, 20 constraints):** ~2-5s
- **Complex method (20 paths, 50 constraints):** ~10-20s

**Факторы:**
- Количество условных веток (path explosion)
- Глубина анализа (maxDepth)
- Сложность ограничений (Z3 solver timeout)
- Количество detected issues

**Z3 Solver:**
- Timeout: 5 секунд на путь
- Typical: 10-100ms для простых constraints
- Complex: 500ms-2s для вложенных логических выражений

**Оптимизация:**
- Path pruning при достижении maxPaths
- Early termination при достижении exitPoint
- Constraint simplification

### Типы обнаруживаемых проблем

#### 1. Division by Zero
- **Error**: Деление на константный ноль (`x / 0`)
- **Warning**: Деление на переменную, которая может быть нулём

#### 2. Null Reference
- **Error**: Обращение к точно null объекту
- **Warning**: Обращение к объекту с неизвестной nullability

#### 3. Array Out of Bounds
- **Error**: Доступ по константному индексу вне границ
- **Warning**: Доступ по переменному индексу без проверки

#### 4. Invalid Cast
- **Warning**: Потенциально некорректный explicit cast

### Связанные инструменты

- ⬅️ [**TraceExecution**](#trace_execution) — понять что делает метод перед анализом
- ⬅️ [**ViewDefinition**](ANALYSIS_TOOLS.md#view_definition) — посмотреть код метода
- ➡️ [**TraceBackwards**](#trace_backwards) — найти откуда вызывается проблемный path
- ➡️ [**OverwriteMember**](MODIFICATION_TOOLS.md) — исправить найденные issues

### Технология

**Roslyn Control Flow Graph (CFG):**
- Статический анализ без выполнения кода
- Поддержка всех C# конструкций
- Path exploration с branch tracking

**Z3 SMT Solver:**
- Constraint satisfiability checking
- Model generation (example inputs)
- Timeout protection (5 сек на constraint set)

**Symbolic Values:**
- Integer, Boolean, String, Reference types
- Array length tracking
- Null state tracking

---

## Сравнение инструментов

| Характеристика | trace_execution | trace_backwards | AnalyzePathFeasibility |
|----------------|----------------|----------------|------------------------|
| **Направление** | Forward (вперёд) | Backward (назад) | Forward (все пути) |
| **От** | Entry point | Crash point | Entry point |
| **До** | Exit point | Entry points | Exit point |
| **Результат** | Один путь | Несколько путей | Все пути с constraints |
| **Технология** | Control Flow Graph | SymbolFinder + Cache | CFG + Z3 SMT Solver |
| **Use case** | Понять что делает код | Понять как дошли до ошибки | Верификация + поиск багов |
| **Детализация** | Высокая (CFG) | Средняя (call graph) | Очень высокая (symbolic) |
| **Stack trace** | Не используется | Ранжирует пути | Не используется |
| **Issues** | Не детектирует | Не детектирует | 4 типа (div/0, null, etc.) |
| **Кэширование** | Нет | **✅ SQLite cache** | Нет |

---

## Типичные сценарии использования

### Отладка падения приложения

1. У вас есть exception с stack trace
2. Запускаете `trace_backwards` с crash point и stack trace hints
3. Получаете все возможные пути, отсортированные по вероятности
4. Видите где именно вызывается проблемный метод

### Понимание нового кода

1. Хотите понять как работает feature
2. Находите entry point (например, API endpoint handler)
3. Запускаете `trace_execution` с этого entry point
4. Видите полный поток выполнения с ветвлениями и вызовами

### Анализ производительности

1. Нашли медленный метод через profiler
2. Используете `trace_backwards` чтобы найти где он вызывается
3. Видите все места вызова и можете оптимизировать

---

## Ограничения

###trace_executionn
- ⚠️ Не выполняет код (статический анализ)
- ⚠️ Сложности с dynamic, reflection
- ⚠️ Виртуальные вызовы требуют дополнительной логики
- ⚠️ Показывает один возможный путь (берёт первую ветку в if)

### TraceBackwards
- ⚠️ Может пропустить некоторые пути (если глубина недостаточна)
- ⚠️ Не всегда находит точный path из stack trace
- ⚠️ Для больших кодовых баз может быть медленным
- ⚠️ Требует загруженного solution

---

## Тестовый пример

См. `TestTracing/` проект для демонстрации:

```bash
cd TestTracing
dotnet build
```

Затем используйте MCP tools для анализа:

**Forward trace:**
```
trace_execution(
    entryPointFqn: "TestTracing.Program.Main"
)
```

**Backward trace:**
```
trace_backwards(
    crashPointFqn: "TestTracing.UserService.ThrowInvalidUserException"
)
```

**Symbolic execution:**
```
analyze_path_feasibility(
    entryPointFqn: "TestTracing.Calculator.Divide",
    maxDepth: 10
)
```

---

## См. также

- 📚 [**ANALYSIS_TOOLS.md**](ANALYSIS_TOOLS.md) — ViewDefinition, FindReferences для детального анализа
- 📚 [**LOG_ANALYSIS_TOOLS.md**](LOG_ANALYSIS_TOOLS.md) — AnalyzeLogs для получения stack traces
- 📚 [**MODIFICATION_TOOLS.md**](MODIFICATION_TOOLS.md) — исправление найденных багов
- 📚 [**SOLUTION_TOOLS.md**](SOLUTION_TOOLS.md) — LoadSolution для начала работы
- 📚 [**README.md**](../README.md) — главная документация проекта

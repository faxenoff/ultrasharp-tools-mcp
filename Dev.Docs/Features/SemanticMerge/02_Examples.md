# Semantic Merge - Примеры использования и Edge Cases

## 📚 Типичные сценарии

### Сценарий 1: Метод переименован и перемещён

#### Исходное состояние (Base)
```csharp
// File: Services/UserService.cs
namespace MyApp.Services
{
    public class UserService
    {
        public User GetUser(int id)
        {
            return _repository.Find(id);
        }
    }
}
```

#### Branch A: Переименование + async
```csharp
// File: Services/UserService.cs
namespace MyApp.Services
{
    public class UserService
    {
        public async Task<User> GetUserAsync(int id)  // Renamed + async
        {
            return await _repository.FindAsync(id);
        }
    }
}
```

#### Branch B: Добавлена валидация
```csharp
// File: Services/UserService.cs
namespace MyApp.Services
{
    public class UserService
    {
        public User GetUser(int id)
        {
            if (id <= 0)
                throw new ArgumentException("Invalid user id");

            return _repository.Find(id);
        }
    }
}
```

#### Semantic Merge Result
```
Анализ:
1. Fast Path:
   - A vs Base: Signature changed (GetUser → GetUserAsync)
   - B vs Base: StructuralHash changed (validation added)
   → Fast Path не нашёл точного match

2. Slow Path:
   - Semantic similarity: 88% (оба изменяют один метод)
   - Intent A: Refactoring (async pattern)
   - Intent B: BugFix (validation)
   - Intents compatible ✅

3. Merge Strategy:
   Combine both changes:
```

```csharp
// Merged result
public async Task<User> GetUserAsync(int id)
{
    if (id <= 0)  // From Branch B
        throw new ArgumentException("Invalid user id");

    return await _repository.FindAsync(id);  // From Branch A
}
```

---

### Сценарий 2: Класс перемещён в другой namespace

#### Base
```csharp
// File: Models/User.cs
namespace MyApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
```

#### Branch A: Перемещение + новое поле
```csharp
// File: Domain/Entities/User.cs
namespace MyApp.Domain.Entities  // Moved!
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }  // New field
    }
}
```

#### Branch B: Добавлен метод
```csharp
// File: Models/User.cs
namespace MyApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string GetFullName()  // New method
        {
            return Name;
        }
    }
}
```

#### Semantic Merge Result
```
Анализ:
1. Fast Path:
   - Content hash different (новые члены в обеих ветках)
   - Structural hash different
   → Fast Path failed

2. Slow Path:
   - MovementDetector: File moved (Models → Domain/Entities)
   - Semantic similarity: 90% (тот же класс)
   - StructuralAligner: Класс User совпадает по signature

3. Merge Strategy:
   Combine at new location (Domain/Entities/User.cs):
```

```csharp
// File: Domain/Entities/User.cs
namespace MyApp.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }  // From Branch A

        public string GetFullName()         // From Branch B
        {
            return Name;
        }
    }
}
```

---

### Сценарий 3: JSON merge (swagger.json)

#### Base
```json
{
  "paths": {
    "/users/{id}": {
      "get": {
        "summary": "Get user by ID",
        "parameters": [
          { "name": "id", "in": "path", "type": "integer" }
        ],
        "responses": {
          "200": { "description": "Success" }
        }
      }
    }
  }
}
```

#### Branch A: Добавлен endpoint
```json
{
  "paths": {
    "/users/{id}": { ... },
    "/users": {  // New endpoint
      "post": {
        "summary": "Create user",
        "responses": {
          "201": { "description": "Created" }
        }
      }
    }
  }
}
```

#### Branch B: Добавлен error response
```json
{
  "paths": {
    "/users/{id}": {
      "get": {
        "summary": "Get user by ID",
        "parameters": [
          { "name": "id", "in": "path", "type": "integer" }
        ],
        "responses": {
          "200": { "description": "Success" },
          "404": { "description": "Not found" }  // Added
        }
      }
    }
  }
}
```

#### Semantic Merge Result
```
Анализ:
1. JsonParser:
   - Unit: /users/{id}/get → Branch B добавил response
   - Unit: /users → Branch A добавил новый endpoint
   - Overlaps: None (разные части API)

2. Merge:
   Auto-merge (no conflicts)
```

```json
{
  "paths": {
    "/users/{id}": {
      "get": {
        "summary": "Get user by ID",
        "parameters": [
          { "name": "id", "in": "path", "type": "integer" }
        ],
        "responses": {
          "200": { "description": "Success" },
          "404": { "description": "Not found" }  // From B
        }
      }
    },
    "/users": {  // From A
      "post": {
        "summary": "Create user",
        "responses": {
          "201": { "description": "Created" }
        }
      }
    }
  }
}
```

---

### Сценарий 4: Refactoring - Extract Method

#### Base
```csharp
public void ProcessOrder(Order order)
{
    // Validation
    if (order == null)
        throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0)
        throw new ArgumentException("Invalid total");

    // Processing
    _repository.Save(order);
    _emailService.SendConfirmation(order.Email);
}
```

#### Branch A: Extract validation
```csharp
public void ProcessOrder(Order order)
{
    ValidateOrder(order);  // Extracted!

    _repository.Save(order);
    _emailService.SendConfirmation(order.Email);
}

private void ValidateOrder(Order order)
{
    if (order == null)
        throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0)
        throw new ArgumentException("Invalid total");
}
```

#### Branch B: Добавлен logging
```csharp
public void ProcessOrder(Order order)
{
    // Validation
    if (order == null)
        throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0)
        throw new ArgumentException("Invalid total");

    _logger.LogInformation("Processing order {OrderId}", order.Id);  // Added

    // Processing
    _repository.Save(order);
    _emailService.SendConfirmation(order.Email);
}
```

#### Semantic Merge Result
```
Анализ:
1. Slow Path:
   - ProcessOrder (A) vs ProcessOrder (B): 75% similarity
   - Detected: Code block moved to ValidateOrder (Branch A)
   - Intent A: Refactoring (extract method)
   - Intent B: FeatureAddition (logging)

2. Merge Strategy:
   Combine: extracted validation + logging
```

```csharp
public void ProcessOrder(Order order)
{
    ValidateOrder(order);  // From A

    _logger.LogInformation("Processing order {OrderId}", order.Id);  // From B

    _repository.Save(order);
    _emailService.SendConfirmation(order.Email);
}

private void ValidateOrder(Order order)  // From A
{
    if (order == null)
        throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0)
        throw new ArgumentException("Invalid total");
}
```

---

## 🚨 Edge Cases и конфликты

### Edge Case 1: Semantic конфликт

#### Проблема
Обе ветки изменяют **логику**, но по-разному.

```csharp
// Base
public decimal CalculateDiscount(Order order)
{
    return order.Total * 0.1m;  // 10% discount
}

// Branch A: Увеличил discount
public decimal CalculateDiscount(Order order)
{
    return order.Total * 0.15m;  // 15% discount
}

// Branch B: Изменил логику
public decimal CalculateDiscount(Order order)
{
    if (order.Total > 1000)
        return order.Total * 0.2m;  // 20% for large orders
    return order.Total * 0.1m;
}
```

#### Semantic Merge
```
Анализ:
- Fast Path: failed (разный код)
- Slow Path: similarity 65% (низкая, т.к. логика разная)
- Intent A: Modification (changed constant)
- Intent B: FeatureAddition (added condition)
- Intents: INCOMPATIBLE ⚠️

Конфликт:
Type: LogicConflict
Description: Both branches modify calculation logic differently

Suggested Resolutions:
1. Use Branch A (15% flat discount)
2. Use Branch B (tiered discount)
3. Manual merge (combine both: 15% base, 20% for >1000)
```

### Edge Case 2: Параллельные API изменения

```csharp
// Base
public interface IUserService
{
    User GetUser(int id);
}

// Branch A: Async refactoring
public interface IUserService
{
    Task<User> GetUserAsync(int id);
}

// Branch B: Добавлен параметр
public interface IUserService
{
    User GetUser(int id, bool includeDeleted);
}
```

#### Semantic Merge
```
Конфликт:
Type: APIConflict
Description: Incompatible signature changes

Suggested Resolutions:
1. Create both methods (GetUser + GetUserAsync)
2. Combine: async Task<User> GetUserAsync(int id, bool includeDeleted)
3. Manual decision required
```

### Edge Case 3: Type переименован в обеих ветках

```csharp
// Base
public class User { ... }

// Branch A
public class UserEntity { ... }  // Renamed to UserEntity

// Branch B
public class ApplicationUser { ... }  // Renamed to ApplicationUser
```

#### Semantic Merge
```
Конфликт:
Type: NamingConflict
Description: Same type renamed differently in both branches

Semantic similarity: 98% (только название изменилось)

Suggested Resolutions:
1. Use "UserEntity" (from A)
2. Use "ApplicationUser" (from B)
3. Create new name: "UserModel", "DomainUser", etc.

Impact Analysis:
- 47 references need updating
- 12 files affected
```

---

## 🔧 Advanced Features

### Feature 1: Multi-File Refactoring Detection

```
Scenario: Метод переместился между классами

Base:
  UserService.cs: ValidateUser()
  OrderService.cs: ProcessOrder()

Branch A:
  UserService.cs: (ValidateUser removed)
  ValidationService.cs: ValidateUser()  // Moved to new file!
  OrderService.cs: ProcessOrder()

Detection:
1. FastPathMatcher: ValidateUser not in UserService (deleted?)
2. MovementDetector:
   - Search in all files of Branch A
   - Find semantic match in ValidationService.cs
   - Confidence: 95% (same signature + body)
   → Detected: MOVED

Merge result:
  Keep in ValidationService.cs (new location)
```

### Feature 2: CFG-Preserving Merge

```csharp
// Base
public void Process(int value)
{
    if (value > 0)
    {
        DoSomething();
    }
}

// Branch A: Added else
public void Process(int value)
{
    if (value > 0)
    {
        DoSomething();
    }
    else
    {
        DoSomethingElse();
    }
}

// Branch B: Added logging
public void Process(int value)
{
    _logger.LogDebug("Processing {Value}", value);

    if (value > 0)
    {
        DoSomething();
    }
}

// Merged with CFG preservation
public void Process(int value)
{
    _logger.LogDebug("Processing {Value}", value);  // From B

    if (value > 0)
    {
        DoSomething();
    }
    else
    {
        DoSomethingElse();  // From A
    }
}
```

**CFG Analysis**:
- Base: 1 branch (if)
- A: 2 branches (if-else)
- B: 1 branch (if) + statement before
- Merged: 2 branches + statement ✅ (combines both)

---

## 📊 Статистика по типам конфликтов

### Автоматически разрешаемые (85%)
- ✅ Different files modified
- ✅ Same file, different methods
- ✅ Same method, compatible intents (refactor + bugfix)
- ✅ Code movement (detected by SemanticMatcher)
- ✅ JSON: different keys modified

### Требуют ручного решения (15%)
- ⚠️ Same logic, different implementation
- ⚠️ API conflicts (signature changes)
- ⚠️ Parallel renames
- ⚠️ Complex CFG changes (restructuring)
- ⚠️ JSON: same key, different values

---

## 💡 Рекомендации по использованию

### Когда использовать Semantic Merge
1. ✅ Долгоживущие feature branches
2. ✅ Активный рефакторинг кодовой базы
3. ✅ Большие команды с параллельной работой
4. ✅ API-first разработка (swagger.json merge)
5. ✅ Миграции и архитектурные изменения

### Когда НЕ использовать
1. ❌ Tiny hotfixes (овер-инжиниринг)
2. ❌ Auto-generated code (нет семантики)
3. ❌ Binary files (не поддерживается)
4. ❌ Первый commit в репозитории (нет base)

### Best Practices
1. **Коммитьте часто**: Меньше изменений → меньше конфликтов
2. **Используйте intents**: Пишите осмысленные commit messages
3. **Тестируйте merged код**: Semantic merge не гарантирует корректность
4. **Review конфликты**: Даже с AI suggestions - проверяйте вручную
5. **Кэшируйте индексы**: Переиспользуйте для faster merges

---

## 🧪 Testing Checklist

### Unit Tests
- [ ] Fast Path matching (hash, signature, structural)
- [ ] Slow Path matching (embeddings, CFG)
- [ ] Intent classification
- [ ] Movement detection
- [ ] JSON parsing

### Integration Tests
- [ ] E2E merge scenarios
- [ ] Multi-file refactoring
- [ ] Conflict resolution
- [ ] Git integration
- [ ] Performance benchmarks

### Manual Testing
- [ ] Real-world feature branches
- [ ] Swagger.json merge
- [ ] Large codebase (100K+ LOC)
- [ ] Edge cases (renames, movements)

---

**См. также**:
- SEMANTIC_MERGE_DESIGN.md - полная техническая спецификация
- SEMANTIC_MERGE_SUMMARY.md - краткое резюме архитектуры

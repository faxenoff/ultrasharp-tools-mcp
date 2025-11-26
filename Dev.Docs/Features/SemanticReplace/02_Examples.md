# Semantic Replace - Examples

## Use Case 1: Замена Console.WriteLine на ILogger

### Сценарий

Проект использует `Console.WriteLine` для логирования. Нужно мигрировать на `ILogger<T>`.

### Шаг 1: Preview

```json
// Request
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "Console\\.WriteLine\\(",
    "searchMode": "regex",
    "scope": "member",
    "filePattern": "**/*.cs",
    "limit": 50
  }
}
```

```json
// Response
{
  "matches": [
    {
      "id": "sr-001-a1b2c3",
      "containerFqn": "MyApp.Services.UserService.CreateUser",
      "filePath": "Services/UserService.cs",
      "matchLine": 45,
      "containerType": "Method",
      "fullCode": "public async Task<User> CreateUser(string name, string email)\n{\n    Console.WriteLine($\"Creating user: {name}\");\n    var user = new User { Name = name, Email = email };\n    await _repository.AddAsync(user);\n    Console.WriteLine($\"User created with ID: {user.Id}\");\n    return user;\n}",
      "matchFragment": "Console.WriteLine($\"Creating user: {name}\");",
      "metadata": {
        "hasILoggerField": false,
        "constructorParams": ["IUserRepository repository"],
        "usedTypes": ["User", "IUserRepository"]
      }
    },
    {
      "id": "sr-001-d4e5f6",
      "containerFqn": "MyApp.Services.OrderService.ProcessOrder",
      "filePath": "Services/OrderService.cs",
      "matchLine": 78,
      "containerType": "Method",
      "fullCode": "public async Task ProcessOrder(int orderId)\n{\n    Console.WriteLine($\"Processing order: {orderId}\");\n    var order = await _orderRepository.GetByIdAsync(orderId);\n    if (order == null)\n    {\n        Console.WriteLine($\"Order not found: {orderId}\");\n        throw new OrderNotFoundException(orderId);\n    }\n    // ... processing logic\n}",
      "matchFragment": "Console.WriteLine($\"Processing order: {orderId}\");",
      "metadata": {
        "hasILoggerField": true,
        "loggerFieldName": "_logger",
        "constructorParams": ["IOrderRepository orderRepository", "ILogger<OrderService> logger"]
      }
    }
  ],
  "total": 23,
  "hasMore": false
}
```

### Шаг 2a: Manual Apply (выборочно)

```json
// Request
{
  "tool": "semantic_replace",
  "params": {
    "apply": true,
    "replacements": [
      {
        "matchId": "sr-001-d4e5f6",
        "newCode": "public async Task ProcessOrder(int orderId)\n{\n    _logger.LogInformation(\"Processing order: {OrderId}\", orderId);\n    var order = await _orderRepository.GetByIdAsync(orderId);\n    if (order == null)\n    {\n        _logger.LogWarning(\"Order not found: {OrderId}\", orderId);\n        throw new OrderNotFoundException(orderId);\n    }\n    // ... processing logic\n}",
        "description": "Replace Console.WriteLine with structured logging"
      }
    ],
    "commitMessage": "refactor: migrate OrderService to ILogger"
  }
}
```

```json
// Response
{
  "success": true,
  "applied": 1,
  "failed": 0,
  "changes": [
    {
      "matchId": "sr-001-d4e5f6",
      "filePath": "Services/OrderService.cs",
      "status": "applied",
      "diffPreview": "- Console.WriteLine($\"Processing order: {orderId}\");\n+ _logger.LogInformation(\"Processing order: {OrderId}\", orderId);"
    }
  ]
}
```

### Шаг 2b: Semantic Apply (автоматически)

```json
// Request
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "Console\\.WriteLine\\(",
    "scope": "member",
    "transformation": "Replace Console.WriteLine with appropriate ILogger method:\n- Use LogInformation for info messages\n- Use LogWarning for warnings\n- Use LogError for errors\n- Use structured logging with named parameters\n- If class doesn't have ILogger field, add it and update constructor",
    "useSemanticModel": true,
    "apply": true,
    "commitMessage": "refactor: migrate all logging to ILogger"
  }
}
```

```json
// Response
{
  "success": true,
  "applied": 23,
  "failed": 0,
  "transformations": [
    {
      "matchId": "sr-001-a1b2c3",
      "transformation": "Added ILogger<UserService> to constructor, replaced 2 Console.WriteLine calls",
      "status": "applied"
    },
    // ... остальные 22
  ]
}
```

---

## Use Case 2: Миграция на ArgumentNullException.ThrowIfNull

### Сценарий

Заменить ручные null-проверки на новый API из .NET 6+.

### Preview

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "if\\s*\\(\\s*\\w+\\s*==\\s*null\\s*\\)\\s*\\n?\\s*throw\\s+new\\s+ArgumentNullException",
    "searchMode": "regex",
    "scope": "block",
    "filePattern": "**/*.cs"
  }
}
```

```json
// Response
{
  "matches": [
    {
      "id": "sr-002-x1y2z3",
      "containerFqn": "MyApp.Services.PaymentService.ProcessPayment",
      "filePath": "Services/PaymentService.cs",
      "matchLine": 12,
      "containerType": "Block",
      "fullCode": "if (request == null)\n    throw new ArgumentNullException(nameof(request));",
      "matchFragment": "if (request == null)\n    throw new ArgumentNullException(nameof(request));"
    },
    {
      "id": "sr-002-a1b2c3",
      "containerFqn": "MyApp.Controllers.UserController.Create",
      "filePath": "Controllers/UserController.cs",
      "matchLine": 25,
      "containerType": "Block",
      "fullCode": "if (dto == null)\n{\n    throw new ArgumentNullException(nameof(dto));\n}",
      "matchFragment": "if (dto == null)\n{\n    throw new ArgumentNullException(nameof(dto));\n}"
    }
  ],
  "total": 45
}
```

### Apply (batch)

```json
{
  "tool": "semantic_replace",
  "params": {
    "apply": true,
    "replacements": [
      {
        "matchId": "sr-002-x1y2z3",
        "newCode": "ArgumentNullException.ThrowIfNull(request);"
      },
      {
        "matchId": "sr-002-a1b2c3",
        "newCode": "ArgumentNullException.ThrowIfNull(dto);"
      }
    ],
    "commitMessage": "refactor: use ArgumentNullException.ThrowIfNull"
  }
}
```

---

## Use Case 3: Добавление CancellationToken к async методам

### Сценарий

Добавить `CancellationToken` параметр ко всем async методам, которые его не имеют.

### Preview с Roslyn mode

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "async Task",
    "searchMode": "roslyn",
    "scope": "member",
    "namespaceFilter": "MyApp.Services.*"
  }
}
```

```json
// Response (filtered: только методы без CancellationToken)
{
  "matches": [
    {
      "id": "sr-003-m1n2o3",
      "containerFqn": "MyApp.Services.EmailService.SendEmailAsync",
      "filePath": "Services/EmailService.cs",
      "matchLine": 15,
      "containerType": "Method",
      "fullCode": "public async Task SendEmailAsync(string to, string subject, string body)\n{\n    await _smtpClient.SendMailAsync(new MailMessage(_from, to, subject, body));\n}",
      "matchFragment": "public async Task SendEmailAsync(string to, string subject, string body)",
      "metadata": {
        "hasCancellationToken": false,
        "parameters": ["string to", "string subject", "string body"],
        "callsAsyncMethods": ["_smtpClient.SendMailAsync"]
      }
    }
  ],
  "total": 12
}
```

### Semantic Apply

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "async Task",
    "searchMode": "roslyn",
    "scope": "member",
    "namespaceFilter": "MyApp.Services.*",
    "transformation": "Add CancellationToken parameter with default value. Pass it to all awaited calls that support it.",
    "useSemanticModel": true,
    "apply": true,
    "commitMessage": "feat: add CancellationToken support to async methods"
  }
}
```

---

## Use Case 4: Замена устаревшего API

### Сценарий

Заменить `DateTime.Now` на `DateTimeOffset.UtcNow` или injected `IDateTimeProvider`.

### Preview

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "DateTime\\.Now",
    "searchMode": "regex",
    "scope": "statement"
  }
}
```

```json
// Response
{
  "matches": [
    {
      "id": "sr-004-p1q2r3",
      "containerFqn": "MyApp.Services.AuditService.LogAction",
      "filePath": "Services/AuditService.cs",
      "matchLine": 34,
      "containerType": "Statement",
      "fullCode": "var entry = new AuditEntry { Timestamp = DateTime.Now, Action = action };",
      "matchFragment": "DateTime.Now"
    }
  ],
  "total": 28
}
```

### Apply с разными стратегиями

```json
// Простая замена
{
  "tool": "semantic_replace",
  "params": {
    "apply": true,
    "replacements": [
      {
        "matchId": "sr-004-p1q2r3",
        "newCode": "var entry = new AuditEntry { Timestamp = DateTimeOffset.UtcNow, Action = action };"
      }
    ]
  }
}
```

---

## Use Case 5: Семантический поиск и рефакторинг

### Сценарий

Найти все места где делается "file I/O" и обернуть в try-catch.

### Semantic Search

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "file reading or writing operations",
    "searchMode": "semantic",
    "scope": "member"
  }
}
```

```json
// Response
{
  "matches": [
    {
      "id": "sr-005-s1t2u3",
      "containerFqn": "MyApp.Services.ConfigService.LoadConfig",
      "filePath": "Services/ConfigService.cs",
      "matchLine": 22,
      "containerType": "Method",
      "fullCode": "public Config LoadConfig(string path)\n{\n    var json = File.ReadAllText(path);\n    return JsonSerializer.Deserialize<Config>(json);\n}",
      "matchFragment": "File.ReadAllText(path)",
      "metadata": {
        "semanticSimilarity": 0.89,
        "matchReason": "Contains File.ReadAllText which is a file reading operation"
      }
    },
    {
      "id": "sr-005-v1w2x3",
      "containerFqn": "MyApp.Services.ExportService.ExportToCsv",
      "filePath": "Services/ExportService.cs",
      "matchLine": 45,
      "containerType": "Method",
      "fullCode": "public void ExportToCsv(IEnumerable<Record> records, string path)\n{\n    var csv = string.Join(\"\\n\", records.Select(r => r.ToCsv()));\n    File.WriteAllText(path, csv);\n}",
      "matchFragment": "File.WriteAllText(path, csv)",
      "metadata": {
        "semanticSimilarity": 0.92,
        "matchReason": "Contains File.WriteAllText which is a file writing operation"
      }
    }
  ],
  "total": 8
}
```

---

## Use Case 6: Batch rename с сохранением контекста

### Сценарий

Переименовать все методы `Get*` в `Fetch*` в определённом namespace.

### Preview

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "^Get",
    "searchMode": "roslyn",
    "scope": "member",
    "namespaceFilter": "MyApp.Repositories.*"
  }
}
```

### Apply с трансформацией

```json
{
  "tool": "semantic_replace",
  "params": {
    "pattern": "^Get",
    "searchMode": "roslyn",
    "scope": "member",
    "namespaceFilter": "MyApp.Repositories.*",
    "transformation": "Rename method from Get* to Fetch* (e.g., GetUser → FetchUser). Update all call sites.",
    "useSemanticModel": true,
    "apply": true
  }
}
```

---

## Edge Cases

### EC1: Вложенные matches

```csharp
// Файл содержит:
public class OuterClass
{
    public void OuterMethod()
    {
        Console.WriteLine("outer");

        void InnerMethod()
        {
            Console.WriteLine("inner");
        }
    }
}

// При scope: "member" получим 2 matches:
// 1. OuterMethod (содержит 2 Console.WriteLine)
// 2. InnerMethod (local function, содержит 1 Console.WriteLine)
```

### EC2: Partial classes

```csharp
// UserService.cs
public partial class UserService
{
    public void Method1() { Console.WriteLine("1"); }
}

// UserService.Generated.cs
public partial class UserService
{
    public void Method2() { Console.WriteLine("2"); }
}

// Matches будут из ОБОИХ файлов
// При apply - изменения применяются к соответствующим файлам
```

### EC3: Expression-bodied members

```csharp
// Match для:
public string Name => Console.WriteLine("getting") + _name;

// fullCode включает весь expression-bodied member
// Трансформация должна учитывать синтаксис
```

### EC4: Конфликт изменений

```json
// Два replacement пытаются изменить один и тот же метод
{
  "replacements": [
    { "matchId": "sr-001", "newCode": "version A" },
    { "matchId": "sr-002", "newCode": "version B" }  // sr-002 в том же методе!
  ]
}

// Response:
{
  "success": false,
  "error": "Conflicting replacements for method MyApp.Services.UserService.Create",
  "conflicts": [
    {
      "matchIds": ["sr-001", "sr-002"],
      "containerFqn": "MyApp.Services.UserService.Create"
    }
  ]
}
```

---

## Тестовые сценарии

### Test 1: Basic regex replace

```
Given: Файл с 5 Console.WriteLine
When: semantic_replace(pattern: "Console.WriteLine", scope: "statement")
Then: 5 matches returned, каждый с statement context
```

### Test 2: Scoped extraction

```
Given: Метод с 3 Console.WriteLine внутри
When: semantic_replace(pattern: "Console.WriteLine", scope: "member")
Then: 1 match returned, fullCode содержит весь метод
```

### Test 3: Semantic search

```
Given: Codebase с различными logging patterns (Console, Debug, Trace)
When: semantic_replace(pattern: "logging statements", searchMode: "semantic")
Then: Matches включают все варианты логирования, отсортированы по similarity
```

### Test 4: Apply with validation

```
Given: replacement с синтаксической ошибкой
When: semantic_replace(apply: true, replacements: [{...invalid code...}])
Then: Error returned, никакие файлы не изменены, snapshot rolled back
```

### Test 5: Large batch performance

```
Given: 500 matches в codebase
When: semantic_replace с pagination (limit: 50)
Then: Response содержит hasMore: true, можно запросить следующую страницу
```

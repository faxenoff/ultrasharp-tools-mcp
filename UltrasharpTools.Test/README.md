# UltrasharpTools Test Suite

Организованная тестовая инфраструктура для UltrasharpTools с централизованной конфигурацией.

## Структура

```
UltrasharpTools.Test/
├── UltrasharpTools.Test.Common/          # Общий код для тестов
│   ├── TestConfiguration.cs              # Конфигурация (загрузка test-config.json)
│   ├── TestServiceProvider.cs            # Helpers для DI setup
│   ├── TestBase.cs                       # Базовые классы для тестов
│   └── test-config.json                  # Пути к решениям, параметры
├── UltrasharpTools.Test.LayeredIndex/    # Тесты layered indexing
│   ├── Program.cs                        # Базовый интеграционный тест
│   ├── BranchSwitchTest.cs              # Тест переключения веток
│   └── CompactionAndCleanupTest.cs      # Тест compaction/cleanup
└── UltrasharpTools.Test.SemanticMerge/   # Тесты semantic merge
    └── Program.cs                        # Umbraco CMS merge test
```

## Конфигурация

Все пути к тестовым решениям настраиваются в `test-config.json`:

```json
{
  "TestSolutions": {
    "Umbraco": "D:\\_mcp\\_test_repos\\Umbraco-CMS\\.merge-test-umbraco",
    "UltrasharpTools": "D:\\OneDrive\\_mcp\\ultrasharp-tools-mcp\\UltrasharpTools.sln"
  },
  "LogLevel": "Information",
  "BuildConfiguration": "Debug",
  "EnableGit": true,
  "LayeredIndexing": {
    "Development": { ... },
    "Production": { ... }
  }
}
```

## Использование

### Layered Index Tests

```bash
# Базовый интеграционный тест (Fabuza solution)
cd UltrasharpTools.Test/UltrasharpTools.Test.LayeredIndex
dotnet run

# Тест переключения веток
dotnet run -- --branch-test

# Тест compaction/cleanup
dotnet run -- --cleanup-test
```

### Semantic Merge Test

```bash
cd UltrasharpTools.Test/UltrasharpTools.Test.SemanticMerge

# С memory provider (default)
dotnet run

# С Ollama provider
dotnet run -- ollama 384 granite-embedding:latest
```

## Добавление новых тестов

1. Создайте новый проект в `UltrasharpTools.Test/`
2. Добавьте ссылку на `UltrasharpTools.Test.Common`
3. Используйте `TestConfiguration.Load()` для загрузки конфигурации
4. Для layered index тестов: наследуйтесь от `LayeredIndexTestBase`
5. Для semantic merge тестов: наследуйтесь от `SemanticMergeTestBase`

## Helpers

### TestConfiguration

```csharp
var config = TestConfiguration.Load();
var solutionPath = config.GetSolutionPath("Fabuza");
```

### TestServiceProvider

```csharp
// Для layered index тестов
var serviceProvider = TestServiceProvider.CreateForLayeredIndexTest(
    config,
    preset: "Development"
);

// Для semantic merge тестов
var serviceProvider = TestServiceProvider.CreateForSemanticMergeTest(
    config,
    provider: "memory",
    dimension: 384
);
```

### TestBase классы

```csharp
public class MyTest : LayeredIndexTestBase
{
    public MyTest() : base(preset: "Development") { }

    protected override async Task RunTestAsync()
    {
        var solutionPath = GetSolutionPath("Fabuza");
        // ...
    }
}
```

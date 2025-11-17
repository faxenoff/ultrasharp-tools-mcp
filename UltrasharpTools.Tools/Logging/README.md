# Легковесный File Logger

Минималистичная замена `Serilog.Extensions.Logging.File` без внешних зависимостей.

## Характеристики

- **Размер**: 533 строки кода (~15 KB)
- **Зависимости**: Только `Microsoft.Extensions.Logging` (уже есть в проекте)
- **Замененные пакеты**:
  - ❌ `Serilog.Extensions.Logging.File` (3.0.0)
  - ❌ `Serilog` (транзитивная зависимость)
  - ❌ `Serilog.Sinks.File` (транзитивная зависимость)

## Возможности

✅ **Основные**:
- Запись логов в файл с UTF-8 (без BOM)
- Все уровни логирования (Trace, Debug, Info, Warning, Error, Critical)
- Структурированное логирование с параметрами `{Parameter}`
- Thread-safe асинхронная запись
- Ротация файлов по размеру
- Настраиваемый формат вывода

✅ **Дополнительные**:
- Поддержка `{Date:format}` в пути к файлу
- Настраиваемый timestamp формат
- Автоматический flush или буферизация
- Фильтрация по категориям (через стандартный `ILoggerProvider`)

## Использование

### Простое использование (как Serilog)

```csharp
using UltrasharpTools.Tools.Logging;

builder.Logging.AddFile(logFilePath, LogLevel.Information);
```

### С настройками

```csharp
builder.Logging.AddFile(options =>
{
    options.FilePath = "logs/app-{Date:yyyy-MM-dd}.log";
    options.MinimumLevel = LogLevel.Debug;
    options.MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    options.MaxRetainedFiles = 10;
    options.AutoFlush = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
});
```

## Формат вывода

```
2025-11-18 00:06:04.889 [INFO] Microsoft.Hosting.Lifetime: Application started. Press Ctrl+C to shut down.
2025-11-18 00:06:04.901 [INFO] Microsoft.Hosting.Lifetime: Hosting environment: Production
```

Формат: `{Timestamp} [{Level}] {Category}: {Message}`

## Производительность

- Асинхронная запись в отдельном потоке
- Буферизация (по умолчанию 100 записей)
- Оптимизированная работа с файлами
- Минимальный overhead (~10-15 KB кода)

## Ротация файлов

При достижении `MaxFileSizeBytes`:
- Текущий файл → `.log.1`
- `.log.1` → `.log.2`
- `.log.2` → `.log.3`
- ...
- `.log.{MaxRetainedFiles}` удаляется

## Структурированное логирование

```csharp
_logger.LogInformation("Processing {Count} items from {Source}",
    itemCount, sourceName);
// Output: Processing 42 items from Database
```

Параметры `{Count}` и `{Source}` автоматически заменяются на значения.

## Отличия от Serilog

| Функция | Serilog | FileLogger |
|---------|---------|------------|
| Размер | ~500+ KB (3 пакета) | ~15 KB (встроено) |
| Зависимости | 3 NuGet пакета | 0 (встроено) |
| Sinks | Множество | Только файл |
| Enrichers | Да | Нет (не требуется) |
| Структурированное логирование | Да | Да (базовое) |
| Async запись | Да | Да |
| Ротация файлов | Да | Да |
| Производительность | Отличная | Отличная |

## Миграция с Serilog

1. **Удалить пакет**:
   ```xml
   <!-- Удалить из .csproj -->
   <PackageReference Include="Serilog.Extensions.Logging.File" Version="3.0.0" />
   ```

2. **Добавить using**:
   ```csharp
   using UltrasharpTools.Tools.Logging;
   ```

3. **Готово!** API полностью совместимый:
   ```csharp
   // Было (Serilog):
   builder.Logging.AddFile(logFilePath, minimumLogLevel);

   // Стало (FileLogger):
   builder.Logging.AddFile(logFilePath, minimumLogLevel);
   // Никаких изменений!
   ```

## Настройки по умолчанию

```csharp
FilePath = "logs/app.log"
MinimumLevel = LogLevel.Information
MaxFileSizeBytes = 10 MB
MaxRetainedFiles = 5
IncludeTimestamp = true
IncludeLogLevel = true
IncludeCategory = true
TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff"
AutoFlush = true
BufferSize = 100
```

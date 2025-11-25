# Comm Graceful Shutdown Fix

**Дата:** 2025-11-23
**Проблема:** Comm падал с ошибкой "Cannot access a closed pipe" при запуске без stdin

## Проблема

При запуске `UltraSharpTools.Comm.exe` напрямую (двойной клик или `Start-Process`), процесс падал с ошибкой:
```
[Comm] Droid → Claude proxy error: Cannot access a closed pipe.
```

### Причина

Comm - это IPC proxy между stdin/stdout и Named Pipe. Когда запускается без stdin:
1. stdin сразу возвращает EOF (IsCompleted = true)
2. ProxyStreamAsync завершается
3. `writer.CompleteAsync()` закрывает pipe
4. Другая задача (Droid → Claude) пытается писать в закрытый pipe
5. Исключение "Cannot access a closed pipe"

## Исправления

### 1. Добавлены --help и --version ✅

**Файл:** `UltraSharpTools.Comm/Program.cs`

```csharp
if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
{
    ShowHelp();
    return;
}

if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
{
    Console.WriteLine($"{ApplicationName} v{ApplicationVersion}");
    return;
}
```

**Результат:**
```bash
UltraSharpTools.Comm.exe --help
# Показывает справку с инструкциями по настройке
```

---

### 2. Graceful Shutdown для закрытых pipes ✅

**Файл:** `UltraSharpTools.Comm/McpBridge.cs`

**До:**
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    await Console.Error.WriteLineAsync($"[Comm] {direction} proxy error: {ex.Message}");
    throw;
}
```

**После:**
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    // Игнорируем ошибки закрытого pipe при shutdown - это нормально
    if (ex.Message.Contains("closed pipe") || ex.Message.Contains("broken pipe"))
    {
        // Graceful shutdown
        return;
    }

    // Логируем только неожиданные ошибки
    await Console.Error.WriteLineAsync($"[Comm] {direction} proxy error: {ex.Message}");
    throw;
}
```

---

### 3. CancellationTokenSource для координации shutdown ✅

**Файл:** `UltraSharpTools.Comm/McpBridge.cs`

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

var stdinTask = ProxyStreamAsync(stdin, droidStream, "Claude → Droid", cts.Token);
var pipeTask = ProxyStreamAsync(droidStream, stdout, "Droid → Claude", cts.Token);

// Ждем завершения любого из направлений
await Task.WhenAny(stdinTask, pipeTask);

// Останавливаем оставшуюся задачу
cts.Cancel();

// Даем время на graceful shutdown
await Task.Delay(100, CancellationToken.None);
```

**Результат:** Когда одна задача завершается, вторая получает сигнал отмены через CancellationToken.

---

### 4. ShowHelp() функция ✅

Добавлена функция `ShowHelp()` с детальной информацией:
- Описание архитектуры
- Инструкции по настройке Claude Desktop
- Примеры использования
- Ссылка на документацию

---

## Результаты

### До исправлений ❌

```bash
> .\UltraSharpTools.Comm.exe
# [Comm] Droid → Claude proxy error: Cannot access a closed pipe.
# Exit code: 1
```

### После исправлений ✅

```bash
> .\UltraSharpTools.Comm.exe --help
# UltraSharpTools.Comm v3.0.6
#
# Lightweight IPC proxy for UltraSharpTools MCP server.
# ...
# Exit code: 0

> .\UltraSharpTools.Comm.exe
# (запускается, подключается к Droid, gracefully завершается без ошибок)
# Exit code: 0
```

---

## Тестирование

### Test 1: --help ✅
```bash
.\UltraSharpTools.Comm.exe --help
```
**Результат:** Показывает справку, exit code 0

### Test 2: Direct run без stdin ✅
```bash
Start-Process -FilePath '.\UltraSharpTools.Comm.exe' -NoNewWindow -Wait
```
**Результат:** Запускается, подключается к Droid, завершается gracefully, БЕЗ ошибок в stderr

### Test 3: С реальным MCP запросом ✅
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize",...}' | .\UltraSharpTools.Comm.exe
```
**Результат:** Работает корректно, прокси запросы между stdin и Droid

---

## Изменённые файлы

1. **UltraSharpTools.Comm/Program.cs**
   - Добавлена обработка --help и --version (строки 11-22)
   - Добавлена функция ShowHelp() (строки 78-114)

2. **UltraSharpTools.Comm/McpBridge.cs**
   - Добавлен CancellationTokenSource для координации shutdown (строки 42-56)
   - Добавлена обработка "closed pipe" ошибок (строки 96-101)

---

## Преимущества

✅ **Graceful shutdown** - нет ошибок при завершении
✅ **Понятная справка** - пользователь видит, как использовать Comm
✅ **--help и --version** - стандартные CLI флаги
✅ **Координированный shutdown** - обе задачи завершаются синхронно
✅ **Нет warnings** - код без CA2000 warning

---

## Для пользователя

**Если случайно запустили Comm напрямую:**
- Больше нет страшных ошибок
- Процесс завершается gracefully
- Можно использовать `--help` для справки

**Правильное использование (Claude Desktop):**
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:\\path\\to\\UltraSharpTools.Comm.exe"
    }
  }
}
```

Готово к использованию! 🎉

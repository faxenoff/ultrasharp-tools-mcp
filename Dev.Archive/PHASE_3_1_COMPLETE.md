# Phase 3.1 Complete: IPC Infrastructure & Publishing

**Дата**: 23 ноября 2025
**Статус**: ✅ ГОТОВО К ТЕСТИРОВАНИЮ

## Выполненные задачи

### 1. Исправлена интеграция MCP с IPC

**Проблема**: `IMcpServer.RunAsync()` не имел перегрузки для работы с Named Pipe

**Решение**:
- Упрощена архитектура: вместо `IpcHostedService` с ручной обработкой MCP
- Используется перенаправление `Console.SetIn/SetOut` на Named Pipe
- Стандартный `WithStdioServerTransport()` теперь работает через IPC

**Файлы**:
- ❌ Удален: `UltrasharpTools.Droid/Ipc/IpcHostedService.cs` (устаревший подход)
- ✅ Обновлен: `UltrasharpTools.Droid/Program.cs` (строки 1040-1062)
  - Добавлен вызов `IpcStdioReplacement.WaitForConnectionAsync()`
  - Добавлено перенаправление Console streams на Named Pipe
  - MCP server запускается после перенаправления streams

### 2. Исправлены ошибки компиляции AOT

**Проблема**: `IlcInstructionSet` содержал неподдерживаемый набор инструкций `bmi2`

**Решение**: Удалена строка `<IlcInstructionSet>` из обоих .csproj - компилятор сам выберет оптимальный набор

**Файлы**:
- `UltraSharpTools.Comm/UltraSharpTools.Comm.csproj` (строка 38 удалена)
- `UltraSharpTools.VectorDB/UltraSharpTools.VectorDB.csproj` (строка 40 удалена)

### 3. Временно отключен Native AOT

**Причина**: Отсутствие Visual Studio C++ Build Tools на машине

**Решение**: Изменены publish скрипты для использования обычного self-contained publish с trimming

**Файлы**:
- `Dev.Scripts/publish-comm.ps1` (строки 33-42)
- `Dev.Scripts/publish-vectordb.ps1` (строки 33-42)

**Параметры**:
```powershell
-p:PublishAot=false
-p:PublishTrimmed=true
-p:PublishSingleFile=true
```

**TODO**: Вернуть `PublishAot=true` после установки VS C++ Build Tools для достижения целевых размеров

### 4. Успешная публикация Hybrid Architecture

**Результат**: Все три компонента опубликованы в `Run.Publish/Hybrid/`

**Размеры** (без AOT):
| Компонент | Размер | Целевой размер (с AOT) |
|-----------|--------|------------------------|
| Comm      | 12.82 MB | ~5-10 MB |
| Droid     | 0.16 MB (+ DLLs) | ~60-70 MB |
| VectorDB   | 12.73 MB | ~30-40 MB |
| **Total** | **221.25 MB** | **~95-120 MB** |

**Заметка**: Размеры больше целевых из-за отсутствия AOT, но для тестирования IPC функциональности это приемлемо.

## Архитектура IPC (финальная)

### Comm → Droid поток

```
1. Comm.exe запускается
   ↓
2. DroidProcessManager ищет Droid процесс
   ↓
3a. Найден → подключается к Named Pipe "UltraSharpTools_Droid"
3b. Не найден → запускает Droid.exe --ipc-mode
   ↓
4. McpBridge перенаправляет stdin/stdout → Named Pipe
   ↓
5. Droid обрабатывает MCP запросы через перенаправленные streams
```

### Droid → VectorDB поток

```
1. Droid получает запрос semantic search
   ↓
2. IndexerClient.SendRequestAsync()
   ↓
3. Проверяет подключение к VectorDB
   ↓
4a. Подключен → отправляет JSON-RPC запрос
4b. Не подключен → запускает VectorDB.exe и подключается
   ↓
5. VectorDB обрабатывает запрос и возвращает результат
```

## Текущее состояние файлов

### Созданные файлы (Phase 1-2)

**UltraSharpTools.Comm/**
- ✅ `UltraSharpTools.Comm.csproj` - конфигурация проекта
- ✅ `Program.cs` - entry point
- ✅ `DroidProcessManager.cs` - поиск/запуск Droid
- ✅ `McpBridge.cs` - IPC прокси

**UltraSharpTools.VectorDB/**
- ✅ `UltraSharpTools.VectorDB.csproj` - конфигурация проекта
- ✅ `Program.cs` - entry point
- ✅ `IndexerService.cs` - IPC сервер (TODO: реализация методов)

**UltrasharpTools.Droid/Ipc/**
- ✅ `SingletonLock.cs` - mutex для singleton
- ✅ `IpcServer.cs` - Named Pipe server
- ✅ `IndexerClient.cs` - клиент для VectorDB IPC
- ✅ `IpcStdioReplacement.cs` - helper для перенаправления streams

### Обновленные файлы (Phase 3.1)

- ✅ `UltrasharpTools.Droid/Program.cs` - интеграция IPC mode
- ✅ `Dev.Scripts/publish-comm.ps1` - отключен AOT временно
- ✅ `Dev.Scripts/publish-vectordb.ps1` - отключен AOT временно

### Скрипты

- ✅ `Dev.Scripts/publish-hybrid.ps1` - публикация всех компонентов
- ✅ `publish-hybrid.cmd` - Windows launcher

## Следующие шаги (Phase 3.2)

### 1. Тестирование Comm → Droid IPC ⏳ IN PROGRESS

**Действия**:
1. Запустить Droid в IPC режиме:
   ```cmd
   cd Run.Publish\Hybrid
   UltrasharpTools.Droid.exe --ipc-mode
   ```

2. В другом терминале запустить Comm:
   ```cmd
   cd Run.Publish\Hybrid
   UltraSharpTools.Comm.exe
   ```

3. Отправить тестовый MCP запрос через stdin Comm

4. Проверить, что Droid получает и обрабатывает запросы

**Ожидаемый результат**:
- Comm подключается к Droid через Named Pipe
- MCP запросы проксируются успешно
- Ответы возвращаются обратно через Comm

### 2. Миграция Semantic компонентов в VectorDB

**Файлы для переноса из Tools:**
- `Tools/Semantic/VectorStore.cs`
- `Tools/Semantic/SemanticSearchService.cs`
- `Tools/Semantic/EmbeddingGenerator.cs`
- `Tools/Semantic/Backends/*.cs`
- `Tools/Semantic/Models/*.cs`

**Целевая структура в VectorDB:**
```
UltraSharpTools.VectorDB/
├── Semantic/
│   ├── VectorStore.cs
│   ├── SemanticSearchService.cs
│   ├── EmbeddingGenerator.cs
│   ├── Backends/
│   │   ├── OllamaBackend.cs
│   │   ├── TEIBackend.cs
│   │   └── MemoryBackend.cs
│   └── Models/
│       ├── VectorEmbedding.cs
│       └── SimilarityResult.cs
└── IndexerService.cs (обновить методы)
```

### 3. Реализация IndexerService методов

**Методы для реализации**:
```csharp
public async Task<IndexResponse> IndexCodeAsync(IndexCodeRequest request)
{
    // 1. Получить embedding от EmbeddingGenerator
    // 2. Сохранить в VectorStore
    // 3. Вернуть статус
}

public async Task<SearchResponse> SearchSimilarAsync(SearchRequest request)
{
    // 1. Получить embedding от EmbeddingGenerator для query
    // 2. Искать похожие в VectorStore
    // 3. Вернуть результаты
}

public Task<StatusResponse> GetStatusAsync()
{
    // Вернуть статус VectorDB (количество embedding, версия, etc.)
}
```

### 4. Обновление SemanticTools в Droid

**Изменения**:
- Удалить прямые вызовы VectorStore
- Заменить на вызовы `IndexerClient.SendRequestAsync()`
- Делегировать всю semantic логику в VectorDB

### 5. End-to-end тестирование

**Полный стек**:
```
Claude Desktop
    ↓ stdio
UltraSharpTools.Comm.exe
    ↓ Named Pipe
UltrasharpTools.Droid.exe --ipc-mode
    ↓ Named Pipe
UltraSharpTools.VectorDB.exe
```

## Известные ограничения

1. **Native AOT отключен** - размеры компонентов больше целевых
   - **Workaround**: Использовать self-contained с trimming
   - **Fix**: Установить VS C++ Build Tools

2. **VectorDB методы не реализованы** - IndexerService содержит только TODO
   - **Status**: Будет реализовано в Phase 3.2

3. **Semantic компоненты еще в Tools** - не перенесены в VectorDB
   - **Status**: Будет мигрировано в Phase 3.2

## Сводка изменений

**Статус компиляции**: ✅ Все проекты собираются успешно
**Статус публикации**: ✅ Все компоненты опубликованы
**Статус тестирования**: ⏳ Готово к началу тестирования IPC

**Всего файлов изменено**: 8
**Всего файлов создано**: 13
**Всего файлов удалено**: 1

---

**Следующий этап**: Тестирование Comm → Droid IPC подключения

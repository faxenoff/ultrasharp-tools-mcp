# Статус реализации Hybrid Architecture

**Дата:** 2025-11-23
**Версия:** Phase 1 - Base Infrastructure

---

## ✅ Выполнено

### 1. UltraSharpTools.Comm (Прокси с AOT)

**Создан проект:** `UltraSharpTools.Comm/`

**Особенности:**
- ✅ Native AOT + Full Trimming (целевой размер ~5-10 MB)
- ✅ Минимальные зависимости (только System.IO.Pipelines, System.Text.Json)
- ✅ Оптимизации: AVX2, BMI2, LZCNT, SSE4.2

**Компоненты:**
- `Program.cs` - точка входа, обработка MCP запросов
- `DroidProcessManager.cs` - поиск/запуск Droid процесса
  - Ищет UltraSharpTools.Droid.exe в той же директории
  - Подключается к наиболее свежему экземпляру
  - Запускает новый если не найден
- `McpBridge.cs` - bidirectional проксирование MCP между stdin/stdout и Droid IPC
  - Newline-delimited JSON protocol
  - Zero-copy проксирование с PipeReader/PipeWriter
  - Обработка ошибок в MCP формате

**Статус:** ✅ Компилируется успешно

---

### 2. UltraSharpTools.VectorDB (Семантическая индексация с AOT)

**Создан проект:** `UltraSharpTools.VectorDB/`

**Особенности:**
- ✅ Native AOT + Full Trimming (целевой размер ~30 MB)
- ✅ Зависимости для векторной БД (SQLite, vectorlite.dll)
- ✅ IPC сервер для Droid через Named Pipe

**Компоненты:**
- `Program.cs` - Named Pipe сервер
- `IndexerService.cs` - обработка запросов от Droid
  - `index_code` - индексация кода в векторную БД
  - `search_similar` - семантический поиск
  - `get_status` - статус индексатора
- `Native/vectorlite.dll` - скопирован из Tools

**Статус:** ✅ Компилируется успешно

**TODO:** Перенести Semantic/ компоненты из UltrasharpTools.Tools

---

### 3. UltrasharpTools.Droid (IPC инфраструктура)

**Добавлены компоненты:** `UltrasharpTools.Droid/Ipc/`

**Новые классы:**
- `SingletonLock.cs` - механизм единственного экземпляра
  - Использует глобальный Mutex `Global\UltraSharpTools_Droid_Singleton`
  - Проверяет есть ли другие экземпляры
  - Захватывает lock для текущего процесса

- `IpcServer.cs` - Named Pipe сервер для Comm
  - Pipe name: `UltraSharpTools_Droid`
  - Принимает подключения от Comm
  - Проксирует MCP запросы к основному движку

- `IndexerClient.cs` - IPC клиент для VectorDB
  - Подключается к `UltraSharpTools_Indexer` pipe
  - Запускает VectorDB процесс если нужно
  - Методы: `IndexCodeAsync()`, `SearchSimilarAsync()`
  - Thread-safe запросы через SemaphoreSlim

**Статус:** ✅ Компилируется успешно

**TODO:** Интегрировать в Program.cs (см. ниже)

---

## 🚧 Следующие шаги

### Phase 2: Интеграция в Droid

**Задачи:**

1. **Обновить Program.cs:**
   ```csharp
   // Добавить опцию --ipc-mode
   var ipcModeOption = new Option<bool>("--ipc-mode")
   {
       Description = "Run in IPC mode (for Comm proxy)",
       DefaultValueFactory = _ => false,
   };

   // В начале Main:
   using var singletonLock = new SingletonLock();
   if (!singletonLock.TryAcquire(TimeSpan.FromSeconds(1)))
   {
       Console.Error.WriteLine("Another instance of Droid is already running.");
       return 1;
   }
   ```

2. **Запуск IPC сервера (если --ipc-mode):**
   ```csharp
   if (ipcMode)
   {
       var ipcServer = host.Services.GetRequiredService<IpcServer>();
       ipcServer.Start((input, output, ct) =>
       {
           // Проксируем к основному MCP движку
           return mcpServer.ProcessRequestsAsync(input, output, ct);
       });
   }
   ```

3. **Регистрация в DI:**
   ```csharp
   builder.Services.AddSingleton<IpcServer>();
   builder.Services.AddSingleton<IndexerClient>();
   ```

4. **Обновить Semantic Tools:**
   - Модифицировать `semantic_search`, `find_duplicates` и др.
   - Делегировать в IndexerClient вместо локального выполнения

---

### Phase 3: Перенос Semantic компонентов в VectorDB

**Из UltrasharpTools.Tools переместить в UltrasharpTools.VectorDB:**

```
UltrasharpTools.Tools/Semantic/
├─ VectorStore.cs              → VectorDB/Services/VectorStore.cs
├─ SemanticSearchService.cs    → VectorDB/Services/SemanticSearchService.cs
├─ EmbeddingGenerator.cs       → VectorDB/Services/EmbeddingGenerator.cs
├─ Backends/                   → VectorDB/Backends/
└─ Models/                     → VectorDB/Models/

UltrasharpTools.Tools/Layered/
├─ VectorCacheManager.cs       → VectorDB/Layered/VectorCacheManager.cs
├─ LayeredCacheManager.cs      → VectorDB/Layered/LayeredCacheManager.cs
└─ ...                         → VectorDB/Layered/
```

**После переноса:**
- Обновить IndexerService.cs для использования этих компонентов
- Убрать Semantic зависимости из UltrasharpTools.Tools
- Droid будет запрашивать семантические операции через IndexerClient

---

### Phase 4: Сборка и оптимизация

**Создать скрипты публикации:**

1. **publish-comm.ps1:**
   ```powershell
   dotnet publish UltraSharpTools.Comm `
       -c Release `
       -r win-x64 `
       --self-contained `
       -p:PublishAot=true `
       -p:StripSymbols=true `
       -o Run.Publish/Comm
   ```

2. **publish-vectordb.ps1:**
   ```powershell
   dotnet publish UltraSharpTools.VectorDB `
       -c Release `
       -r win-x64 `
       --self-contained `
       -p:PublishAot=true `
       -p:StripSymbols=true `
       -o Run.Publish/VectorDB
   ```

3. **publish-all.ps1:**
   - Собрать Comm, Droid, VectorDB
   - Скопировать в единую директорию
   - Проверить размеры

**Целевые размеры (Release + AOT + Trimming):**
- Comm: ~5-10 MB
- Droid: ~60-70 MB (без Semantic)
- VectorDB: ~30-40 MB (с Semantic + vectorlite)
- **Total: ~95-120 MB** (vs текущие 103 MB только для Droid)

---

## 📊 Текущая архитектура

```
┌─────────────────────────────────────────────────────┐
│ Claude Desktop (MCP Client)                         │
└────────────────┬────────────────────────────────────┘
                 │ stdin/stdout (JSON-RPC)
                 ↓
┌─────────────────────────────────────────────────────┐
│ UltraSharpTools.Comm (~8 MB, AOT)                   │
│ ├─ DroidProcessManager (поиск/запуск Droid)         │
│ └─ McpBridge (проксирование MCP)                    │
└────────────────┬────────────────────────────────────┘
                 │ Named Pipe IPC
                 ↓
┌─────────────────────────────────────────────────────┐
│ UltraSharpTools.Droid (~65 MB, Singleton)           │
│ ├─ SingletonLock (только 1 экземпляр)               │
│ ├─ IpcServer (для Comm)                             │
│ ├─ Roslyn + Tools (Core)                            │
│ └─ IndexerClient (к VectorDB)                        │
└────────────────┬────────────────────────────────────┘
                 │ Named Pipe IPC
                 ↓
┌─────────────────────────────────────────────────────┐
│ UltraSharpTools.VectorDB (~32 MB, AOT)               │
│ ├─ VectorStore (SQLite + vectorlite)                │
│ ├─ SemanticSearch                                   │
│ ├─ EmbeddingGenerator                               │
│ └─ Layered Indexing                                 │
└─────────────────────────────────────────────────────┘
```

---

## 🎯 Преимущества новой архитектуры

**1. Легкий прокси (Comm):**
- Пользователь запускает легкий Comm (~8 MB)
- Comm находит/запускает Droid автоматически
- Прозрачное проксирование MCP

**2. Единственный экземпляр Droid:**
- Несколько Comm могут подключаться к одному Droid
- Экономия памяти (не нужно N копий Roslyn workspace)
- Singleton Lock предотвращает конфликты

**3. Отдельный процесс индексации:**
- Изоляция тяжелых семантических операций
- Можно перезапустить VectorDB без перезагрузки Droid
- AOT + trimming для минимального размера

**4. IPC через Named Pipes:**
- Нативный Windows механизм
- Быстрее чем HTTP localhost
- Не требует портов / firewall правил

---

## 📝 Примечания

**Обратная совместимость:**
- Droid можно запускать напрямую (stdio mode) как раньше
- --ipc-mode опциональный (для Comm)
- Можно постепенно мигрировать

**Тестирование:**
```bash
# 1. Запустить Droid в IPC режиме
UltraSharpTools.Droid.exe --ipc-mode

# 2. Запустить Comm (найдет Droid и подключится)
UltraSharpTools.Comm.exe

# 3. Comm проксирует MCP запросы к Droid
```

**Следующий шаг:**
- Интегрировать IpcServer и SingletonLock в Program.cs
- Добавить опцию --ipc-mode
- Протестировать Comm → Droid взаимодействие

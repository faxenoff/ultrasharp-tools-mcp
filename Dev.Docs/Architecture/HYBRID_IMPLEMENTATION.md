# Hybrid Architecture Implementation

**Статус:** ✅ Реализовано (Phase 1 & 2)

## Обзор

Гибридная архитектура ultrasharp-tools разделяет систему на:
- **UltrasharpTools.Agent** (~32 MB) - легкий клиент на машине разработчика
- **UltrasharpTools.Overlord** (~103 MB) - мощный сервер с централизованным хранением

## Архитектура

```
┌─────────────────────────────────────────┐
│ Developer Machine                       │
│                                         │
│  UltrasharpTools.Agent (32 MB)         │
│  ├─ GitWatcherService                  │
│  ├─ FileWatcherService                 │
│  ├─ EmbeddingsService (Ollama/TEI)     │
│  └─ ServerBridgeService                │
│                                         │
│  Локальные файлы + Git                 │
└──────────┬──────────────────────────────┘
           │
           ↓ HTTP/JSON (~6 KB per file)
           │
┌──────────────────────────────────────────┐
│ Remote Server (Kubernetes/VM)           │
│                                          │
│  UltrasharpTools.Overlord (103 MB)      │
│  ├─ AgentController (API)               │
│  ├─ MultiProjectVectorStore             │
│  ├─ Roslyn Analysis                     │
│  └─ MCP Server                           │
│                                          │
│  Централизованное хранение:             │
│  ~/data/multi-project-vectors/           │
│    ├─ Project1/                         │
│    │  ├─ main/vectors.db                │
│    │  └─ feature-*/vectors.db           │
│    └─ Project2/...                      │
└──────────────────────────────────────────┘
```

## Компоненты

### UltrasharpTools.Agent

**Размер:** 32 MB (optimized with trimming)

**Зависимости:**
- LibGit2Sharp (10 MB) - Git integration
- ModelContextProtocol SDK (~5 MB) - MCP stdio
- Microsoft.Extensions.Http (~2 MB) - HTTP client
- .NET Runtime essentials (~15 MB)

**Сервисы:**

1. **GitWatcherService** - отслеживает Git изменения
   - Branch switches
   - New commits
   - Отправляет события на сервер

2. **FileWatcherService** - отслеживает файловые изменения
   - FileSystemWatcher для *.cs файлов
   - Debouncing (500ms по умолчанию)
   - Векторизация через EmbeddingService

3. **EmbeddingService** - векторизация кода
   - Поддержка Ollama (http://localhost:11434)
   - Поддержка TEI (HuggingFace)
   - Модели: nomic-embed-text, granite-embedding, mxbai-embed-large

4. **ServerBridgeService** - коммуникация с Overlord
   - POST /api/agent/file-changed
   - POST /api/agent/branch-switched
   - POST /api/agent/git-commit

**Запуск:**
```bash
UltrasharpTools.Agent.exe \
  --project-name MyProject \
  --project-path D:/MyProject \
  --server-url http://localhost:3001 \
  --embedding-url http://localhost:11434 \
  --embedding-model nomic-embed-text
```

### UltrasharpTools.Overlord (расширен)

**Новые компоненты:**

1. **AgentController** (`Controllers/AgentController.cs`)
   - `POST /api/agent/file-changed` - прием событий изменения файлов
   - `POST /api/agent/branch-switched` - переключение веток
   - `POST /api/agent/git-commit` - коммиты
   - `GET /api/agent/health` - health check

2. **MultiProjectVectorStoreService** (`Services/MultiProjectVectorStoreService.cs`)
   - Управляет векторными хранилищами ВСЕХ проектов
   - Отдельный VectorStore для каждого project/branch
   - Cross-project search через `SearchAcrossProjectsAsync()`
   - Централизованное хранение в `~/data/multi-project-vectors/`

**Регистрация сервисов:**
```csharp
// Program.cs
builder.Services.AddSingleton<IMultiProjectVectorStoreService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<MultiProjectVectorStoreService>>();
    var basePath = Path.Combine(AppContext.BaseDirectory, "data", "multi-project-vectors");
    return new MultiProjectVectorStoreService(logger, basePath, 768);
});

builder.Services.AddControllers();
```

## Протокол коммуникации

### File Changed Event

**Agent → Overlord:**
```json
{
  "type": "file_changed",
  "project": "MyApp",
  "branch": "feature-auth",
  "file": "src/Services/UserService.cs",
  "action": "modified",
  "content": "public class UserService { ... }",
  "vectors": [0.123, 0.456, ..., 0.789],  // 768 floats
  "symbols": [
    {
      "name": "UserService.ValidateEmail",
      "kind": "method",
      "line": 45
    }
  ],
  "timestamp": "2025-01-18T12:34:56Z"
}
```

**Размер:** ~6 KB (768 floats × 4 bytes + metadata)

**Overlord обработка:**
- Сохраняет в `data/multi-project-vectors/MyApp/feature-auth/vectors.db`
- Индексирует для fast similarity search

### Cross-Project Search

**Пример использования:**
```csharp
// Поиск дубликатов ПО ВСЕМ проектам команды
var matches = await _vectorStore.SearchAcrossProjectsAsync(
    queryVector: embedding,
    threshold: 0.7,
    limit: 10,
    projects: null  // null = all projects
);

// Результат:
// [
//   { Project: "TeamProject1", Similarity: 0.92, ... },
//   { Project: "MyApp", Similarity: 0.87, ... },
//   { Project: "SharedLibraries", Similarity: 0.85, ... }
// ]
```

## Workflow

### 1. Developer работает локально

```
1. Разработчик редактирует UserService.cs
2. FileWatcher обнаруживает изменение (debounce 500ms)
3. EmbeddingService векторизует код через Ollama
4. ServerBridge отправляет на Overlord
5. Overlord сохраняет в MultiProjectVectorStore

Latency: ~200-500ms
Network traffic: ~6 KB
```

### 2. Claude ищет дубликаты

```
1. Claude вызывает MCP tool: FindPotentialDuplicates(...)
2. Droid проксирует запрос на Overlord
3. Overlord ищет в ВСЕХ проектах через MultiProjectVectorStore
4. Находит дубликаты в:
   - MyApp (2 matches)
   - TeamProject1 (1 match)
   - SharedLibraries (1 match - можно переиспользовать!)
5. Возвращает результаты Claude

Latency: ~500-2000ms (поиск в нескольких БД)
Network traffic: ~10-50 KB
```

## Преимущества

✅ **Легкий клиент:** 32 MB vs 103 MB (3.2x меньше)
✅ **Прямой доступ к файлам:** как Local Mode
✅ **Cross-project search:** уникальная возможность
✅ **Централизованное хранение:** экономия storage
✅ **Team insights:** анализ всей кодовой базы команды
✅ **Мощный анализ:** на стороне сервера

## Недостатки

⚠️ Требует network connectivity
⚠️ Latency +200-500ms vs pure local
⚠️ Нужен Overlord сервер (Kubernetes/VM)

## Сборка и запуск

### Agent

**Публикация:**
```bash
# Windows
publish-agent.cmd

# PowerShell
pwsh Dev.Scripts/publish-agent.ps1
```

**Результат:** `Run.Publish/Agent/UltrasharpTools.Agent.exe` (32 MB)

**Запуск:**
```bash
UltrasharpTools.Agent.exe \
  --project-name MyProject \
  --server-url http://overlord.company.com:3001
```

### Overlord

**Запуск:**
```bash
cd UltrasharpTools.Overlord
dotnet run -- --port 3001
```

**API endpoints:**
- `http://localhost:3001/api/agent/health` - health check
- `http://localhost:3001/api/agent/file-changed` - file events
- `http://localhost:3001/mcp` - MCP server

## Storage

### Для команды 10 разработчиков, 20 проектов

**Векторные базы:**
```
~/data/multi-project-vectors/
├─ Project1/
│  ├─ main/vectors.db (50 MB)
│  ├─ feature-123/vectors.db (10 MB delta)
│  └─ feature-456/vectors.db (8 MB delta)
├─ Project2/
│  └─ main/vectors.db (120 MB)
└─ Project3/...

Total: ~1.5 GB для всех проектов и веток
```

**vs Current approach:**
- 10 developers × 103 MB app = 1030 MB
- 10 developers × ~500 MB local cache = 5 GB
- **Total: 6 GB локально**

**Hybrid:**
- 10 developers × 32 MB agent = 320 MB локально
- 1.5 GB на сервере
- **Total: 1.82 GB (70% экономия!)**

## Next Steps (Phase 3+)

- [ ] MCP proxy implementation (Agent → Overlord MCP tools)
- [ ] Real-time notifications (server → agents)
- [ ] Conflict detection (duplicate code alerts)
- [ ] Code reuse recommendations
- [ ] Team analytics dashboard
- [ ] Performance optimization (lazy loading, caching)

## См. также

- [HYBRID_ARCHITECTURE.md](HYBRID_ARCHITECTURE.md) - оригинальный дизайн
- [UltrasharpTools.Agent/](../../UltrasharpTools.Agent/) - исходный код Agent
- [UltrasharpTools.Overlord/](../../UltrasharpTools.Overlord/) - исходный код Overlord

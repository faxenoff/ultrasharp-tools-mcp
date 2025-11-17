# Hybrid Architecture: ultrasharp-tools-droid

**Lightweight local client + Powerful remote server**

---

## 🎯 Концепция

**Проблема текущей архитектуры:**
- **Local Mode:** Каждый разработчик индексирует весь проект локально (~103 MB app + gigabytes cache)
- **Remote Mode:** Нет доступа к локальным файлам, нужно клонировать в pod

**Hybrid решение:**

```
┌─────────────────────────────────────────────────────────┐
│ Developer Machine                                       │
│                                                         │
│  ultrasharp-tools-droid (~32 MB) ← ЛЕГКИЙ!             │
│  ├─ Git Watcher (отслеживает изменения)                │
│  ├─ File Watcher (мониторит файлы)                     │
│  ├─ Embeddings Client (векторизация локально)          │
│  └─ MCP Bridge (перенаправляет запросы)                │
│                                                         │
│  Локальные файлы: D:\MyProject\                        │
│  Git repo: .git/                                        │
└──────────┬──────────────────────────────────────────────┘
           │
           ↓ lightweight protocol (только vectors + metadata)
           │
┌──────────────────────────────────────────────────────────┐
│ Remote Server (Kubernetes / Powerful VM)                 │
│                                                          │
│  ultrasharp-tools-server (~103 MB) ← МОЩНЫЙ!            │
│  ├─ Roslyn Analysis Engine                              │
│  ├─ Symbol Index (all projects, all branches)           │
│  ├─ Vector Store (centralized, deduplicated)            │
│  ├─ Quality Tools (CSharpier, Analyzers)                │
│  └─ Tracing & Debugging (Z3, CFG)                       │
│                                                          │
│  Team Storage:                                           │
│  ├─ Project 1: 50 MB vectors, 100 branches              │
│  ├─ Project 2: 120 MB vectors, 200 branches             │
│  └─ ... all team projects                               │
│                                                          │
│  Total: ~3-5 GB для команды 10 разработчиков            │
└──────────────────────────────────────────────────────────┘
```

**Ключевое отличие:**
- Droid имеет **прямой доступ** к локальным файлам (как stdio mode)
- Но тяжелый анализ делает **мощный сервер** (как remote mode)
- **Best of both worlds!**

---

## 📦 Размер ultrasharp-tools-droid

### Текущий MCPServer (~103 MB)

```
.NET Runtime: ~15 MB
Roslyn SDK: ~30 MB  ← БОЛЬШОЙ!
LibGit2Sharp: ~10 MB
CSharpier: ~5 MB
Analyzers: ~8 MB
Z3 Solver: ~15 MB   ← БОЛЬШОЙ!
Other deps: ~20 MB
Total: ~103 MB (published, trimmed)
```

### ultrasharp-tools-droid (~32 MB) - 3.2x меньше!

```
.NET Runtime essentials: ~15 MB
LibGit2Sharp: ~10 MB (Git integration)
MCP Protocol client: ~2 MB (JSON-RPC, SSE)
File Watcher: ~1 MB (FileSystemWatcher)
Embeddings Client: ~3 MB (HTTP client к Ollama/TEI)
Vector Serialization: ~1 MB
Total: ~32 MB

Compressed (single-file exe): ~18-22 MB
```

**Что УБРАНО из droid:**
- ❌ Roslyn SDK (~30 MB) - на сервере
- ❌ CSharpier (~5 MB) - на сервере
- ❌ Analyzers (~8 MB) - на сервере
- ❌ Z3 Solver (~15 MB) - на сервере
- ❌ SQLite Vector Store (~5 MB) - только отправка на сервер
- ❌ Symbol Index storage - на сервере

**Экономия:** 103 MB - 32 MB = **71 MB на каждого разработчика!**

Для команды 10 человек: **710 MB vs 1030 MB** локально.

---

## 🏗️ Архитектура компонентов

### ultrasharp-tools-droid (Local Client)

**Минимальные зависимости:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Git integration -->
    <PackageReference Include="LibGit2Sharp" Version="0.30.0" />

    <!-- MCP protocol -->
    <PackageReference Include="System.Text.Json" Version="10.0.0" />

    <!-- HTTP client для server communication -->
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />

    <!-- Hosting для background services -->
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />

    <!-- Logging -->
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
  </ItemGroup>
</Project>
```

**Структура:**

```
UltrasharpTools.Droid/
├─ Program.cs                    # Entry point
├─ Services/
│  ├─ GitWatcherService.cs       # Отслеживает Git changes
│  ├─ FileWatcherService.cs      # Отслеживает file changes
│  ├─ EmbeddingsService.cs       # Векторизация через Ollama/TEI
│  └─ ServerBridgeService.cs     # Communication с server
├─ Protocol/
│  ├─ MCPServerClient.cs         # MCP protocol over SSE/HTTP
│  ├─ Messages/                  # Message types
│  └─ VectorSerializer.cs        # Сериализация vectors
└─ Config/
   └─ DroidConfig.cs             # Configuration
```

**Функционал:**

1. **Git Watcher:**
   ```csharp
   // Отслеживает изменения в Git
   - git status (unstaged changes)
   - git diff (file changes)
   - Branch switches
   - New commits

   → Отправляет на server для индексации
   ```

2. **File Watcher:**
   ```csharp
   // FileSystemWatcher для .cs файлов
   - File created/modified/deleted
   - Debouncing (не спамим на каждое изменение)

   → Векторизует изменения
   → Отправляет vectors на server
   ```

3. **Embeddings Client:**
   ```csharp
   // HTTP client к Ollama/TEI (локально или remote)
   POST http://localhost:11434/api/embeddings
   {
     "model": "nomic-embed-text",
     "prompt": "public class User { }"
   }

   → Получает 768-dim vector
   → Отправляет на server
   ```

4. **MCP Bridge:**
   ```csharp
   // Перенаправляет MCP requests на server
   Claude → droid: FindPotentialDuplicates(...)
   droid → server: POST /mcp/findDuplicates
   server → droid: results
   droid → Claude: results
   ```

---

### ultrasharp-tools-server (Remote)

**Остается как сейчас, но добавляются:**

1. **Multi-Project Vector Store:**
   ```csharp
   // Хранит векторы ВСЕХ проектов команды
   /app/data/vectors/
   ├─ project1/
   │  ├─ main/
   │  │  └─ vectors.db (50 MB)
   │  ├─ feature-123/
   │  └─ feature-456/
   ├─ project2/
   │  └─ main/
   │     └─ vectors.db (120 MB)
   └─ project3/
   ```

2. **Cross-Project Search:**
   ```csharp
   // Поиск дубликатов во ВСЕХ проектах команды
   FindPotentialDuplicates(
       targetCode: "...",
       threshold: 0.7,
       scope: "all_team_projects" // ← НОВОЕ!
   )

   → Ищет в векторных базах всех проектов
   → Возвращает matches из разных проектов
   ```

3. **Team Insights:**
   ```csharp
   // Анализ всей кодовой базы команды
   - Самые сложные методы (complexity > 20)
   - Дубликаты кода между проектами
   - Technical debt hotspots
   - Code reuse opportunities
   ```

---

## 🔄 Протокол коммуникации

### Droid → Server

**1. File Change Event:**

```json
{
  "type": "file_changed",
  "project": "MyApp",
  "branch": "feature-auth",
  "file": "src/Services/UserService.cs",
  "action": "modified",
  "content": "public class UserService { ... }",
  "vectors": [0.123, 0.456, ..., 0.789], // 768 floats
  "symbols": [
    {
      "name": "UserService.ValidateEmail",
      "kind": "method",
      "line": 45
    }
  ],
  "timestamp": "2025-01-17T12:34:56Z"
}
```

**Размер:** ~6 KB (768 floats * 4 bytes + metadata)

**2. Branch Switch Event:**

```json
{
  "type": "branch_switched",
  "project": "MyApp",
  "from_branch": "main",
  "to_branch": "feature-auth",
  "timestamp": "2025-01-17T12:34:56Z"
}
```

**Размер:** ~200 bytes

**3. Git Commit Event:**

```json
{
  "type": "git_commit",
  "project": "MyApp",
  "branch": "feature-auth",
  "commit_sha": "abc123...",
  "files_changed": ["src/Services/UserService.cs", "..."],
  "timestamp": "2025-01-17T12:34:56Z"
}
```

**Размер:** ~500 bytes

---

### Claude → Droid → Server

**MCP Request (proxied):**

```json
// Claude → droid (MCP stdio)
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "UltrasharpTool_FindPotentialDuplicates",
    "arguments": {
      "targetCode": "async Task ProcessAsync() { ... }",
      "threshold": 0.7,
      "scope": "all_team_projects" // ← droid перенаправит на server
    }
  }
}

// Droid → server (HTTP)
POST https://ultrasharp-server.company.com/mcp/findDuplicates
{
  "project": "MyApp", // текущий проект droid
  "targetCode": "async Task ProcessAsync() { ... }",
  "threshold": 0.7,
  "scope": "all_team_projects",
  "user": "developer@company.com"
}

// Server → droid
{
  "matches": [
    {
      "project": "TeamProject1",
      "file": "Services/OrderService.cs",
      "line": 45,
      "similarity": 0.92,
      "code": "async Task ProcessOrderAsync() { ... }"
    },
    {
      "project": "MyApp", // текущий проект
      "file": "Services/PaymentService.cs",
      "line": 123,
      "similarity": 0.87,
      "code": "async Task ProcessPaymentAsync() { ... }"
    }
  ]
}

// Droid → Claude (MCP stdio)
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "matches": [...] // те же results
  }
}
```

---

### Server → Droid

**Push notifications (опционально):**

```json
{
  "type": "team_insight",
  "message": "New duplicate code detected in TeamProject2 similar to your code",
  "similarity": 0.91,
  "location": "TeamProject2/Services/EmailService.cs:67"
}
```

**Droid может показать notification разработчику.**

---

## 🚀 Workflow примеры

### Workflow 1: Developer работает локально

```
1. Developer открывает Visual Studio Code
2. Редактирует src/Services/UserService.cs
3. Сохраняет файл

→ FileWatcher (droid) обнаруживает изменение
→ EmbeddingsService векторизует код (через local Ollama)
→ ServerBridge отправляет vectors на server
→ Server сохраняет в vector store для "MyApp/feature-auth"
→ Server отвечает "OK"

Latency: ~200-500ms (embeddings + network)
Network traffic: ~6 KB per file change
```

---

### Workflow 2: Claude ищет дубликаты

```
Claude Desktop:
"Найди дубликаты этого метода"

→ Claude вызывает MCP tool: FindPotentialDuplicates(...)
→ Droid (MCP bridge) перенаправляет на server
→ Server ищет в vector stores ВСЕХ проектов команды
→ Server находит:
  - 2 дубликата в текущем проекте
  - 1 дубликат в TeamProject1
  - 1 дубликат в TeamProject2
→ Server возвращает results
→ Droid передает Claude
→ Claude показывает пользователю

Latency: ~500-2000ms (vector search в нескольких БД)
Network traffic: ~10-50 KB (зависит от количества matches)
```

**Преимущество:** Поиск ПО ВСЕМ проектам команды, не только локально!

---

### Workflow 3: Git branch switch

```
Developer:
git checkout feature-payment

→ GitWatcher (droid) обнаруживает switch
→ Отправляет event на server
→ Server переключает context на "MyApp/feature-payment"
→ Загружает vector store для этой ветки (если есть)
→ Отвечает "Ready"

Latency: ~50-200ms
Network traffic: ~200 bytes

→ Claude теперь работает с контекстом feature-payment ветки
```

---

## 💾 Storage на сервере

### Для команды 10 разработчиков, 20 проектов

**Векторные базы:**
```
Project sizes (символов → vectors):
- Small (10K symbols): ~10 MB vectors
- Medium (50K symbols): ~50 MB vectors
- Large (200K symbols): ~200 MB vectors

Team 20 projects:
- 10 small: 10 * 10 MB = 100 MB
- 6 medium: 6 * 50 MB = 300 MB
- 4 large: 4 * 200 MB = 800 MB
Total: ~1.2 GB vectors (main branches)

Branches (avg 50 branches per project):
- Most branches share 80%+ code with main
- Delta storage: ~20% of full size
- 20 projects * 50 branches * 20% = ~240 MB
Total with branches: ~1.5 GB
```

**Symbol Indexes:**
```
- SQLite indexes: ~500 MB для всех проектов
- Call graphs cache: ~200 MB
Total: ~700 MB
```

**Git metadata:**
```
- Не нужно клонировать репозитории!
- Только metadata от droid'ов: ~100 MB
```

**Total storage: ~2.3 GB для всей команды**

**vs Current approach:**
- 10 developers * 103 MB app = 1030 MB
- 10 developers * ~500 MB local cache = 5 GB
- **Total: 6 GB локально на всех машинах**

**Экономия:** 6 GB → 2.3 GB (централизованно) + 10 * 32 MB = **2.6 GB total**
**~60% меньше storage!**

---

## ⚡ Performance сравнение

### Local Mode (Current)

```
LoadSolution: 4.8s (with cache)
FindDuplicates: < 100ms (local vector search)
Total latency: ~5s
```

**Минус:** Каждый разработчик индексирует сам, дубликаты только в своем проекте.

---

### Remote Mode (Current)

```
LoadSolution: нужно git clone в pod, нет локальных файлов
Network latency: +50-200ms на каждый request
Total: медленнее + нет доступа к локальным файлам
```

---

### Hybrid Mode (Droid + Server)

```
File change detected: 200ms (local embedding + send)
FindDuplicates: 500-1000ms (server search across all projects)
LoadSolution: не нужен! Server уже имеет все проекты
Total latency: ~500-1000ms

Network traffic per file change: ~6 KB
Network traffic per search: ~10-50 KB
```

**Преимущества:**
✅ Доступ к локальным файлам (как Local Mode)
✅ Cross-project search (уникально!)
✅ Централизованное хранение (экономия storage)
✅ Мощный анализ на сервере (больше RAM/CPU)

**Недостатки:**
⚠️ Требует network connectivity
⚠️ Latency +200-500ms vs pure local

---

## 🆕 Новые возможности

### 1. Cross-Project Duplicate Detection

```
Claude: "Найди дубликаты этого кода во ВСЕХ проектах команды"

→ Server ищет в векторных базах:
  - MyApp (текущий проект)
  - TeamProject1
  - TeamProject2
  - SharedLibraries
  - ... all 20 projects

→ Результат:
  "Найдено 5 дубликатов:
   - 2 в текущем проекте
   - 1 в TeamProject1 (можно переиспользовать!)
   - 1 в SharedLibraries (уже есть готовая реализация!)
   - 1 в TeamProject2"
```

**Value:** Избегаем дубликатов между проектами, находим code reuse opportunities!

---

### 2. Team Code Insights

```
Claude: "Покажи самые сложные методы во всех проектах команды"

→ Server анализирует complexity metrics всех проектов:

Top 10 most complex methods (team-wide):
1. TeamProject1.OrderProcessor.ProcessOrder() - CC: 45
2. MyApp.PaymentService.ValidatePayment() - CC: 38
3. TeamProject2.ReportGenerator.Generate() - CC: 35
...

→ Technical debt hotspots identified!
```

---

### 3. Shared Symbol Index

```
Claude: "Где используется интерфейс IUserRepository?"

→ Server ищет ПО ВСЕМ проектам:
  - MyApp: 15 references
  - SharedLibraries: 3 references
  - TeamProject1: 8 references

→ Cross-project refactoring opportunities identified!
```

---

### 4. Real-time Team Collaboration

```
Developer A работает над feature-auth в MyApp
Developer B работает над feature-payment в TeamProject1

→ Server обнаруживает похожий код:
  "Developer A's UserService.ValidateEmail похож на
   Developer B's CustomerService.ValidateCustomerEmail (similarity: 0.89)"

→ Push notification обоим разработчикам:
  "Похожий код обнаружен, можно консолидировать?"
```

---

## 🛠️ Техническая реализация

### Phase 1: ultrasharp-tools-droid (MVP)

**Создать новый проект:**

```bash
# Новая структура
UltrasharpTools.sln
├─ UltrasharpTools.Tools/      # существующий (shared library)
├─ UltrasharpTools.MCPServer/  # существующий (local stdio)
├─ UltrasharpTools.RemoteServer/ # существующий (remote HTTP/SSE)
└─ UltrasharpTools.Droid/      # НОВЫЙ! (hybrid client)
   ├─ Program.cs
   ├─ Services/
   │  ├─ GitWatcherService.cs
   │  ├─ FileWatcherService.cs
   │  ├─ EmbeddingsService.cs
   │  └─ ServerBridgeService.cs
   └─ UltrasharpTools.Droid.csproj
```

**Зависимости:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifiers>win-x64;linux-x64;osx-arm64</RuntimeIdentifiers>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="LibGit2Sharp" Version="0.30.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
  </ItemGroup>
</Project>
```

**GitWatcherService.cs:**

```csharp
public class GitWatcherService : BackgroundService
{
    private readonly string _repoPath;
    private readonly IServerBridgeService _bridge;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var repo = new Repository(_repoPath);

        // Watch for branch switches
        var currentBranch = repo.Head.FriendlyName;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            var newBranch = repo.Head.FriendlyName;
            if (newBranch != currentBranch)
            {
                await _bridge.SendBranchSwitchEvent(currentBranch, newBranch);
                currentBranch = newBranch;
            }

            // Check for new commits
            var status = repo.RetrieveStatus();
            if (status.IsDirty)
            {
                // Files changed
                foreach (var item in status.Modified)
                {
                    await ProcessChangedFile(item.FilePath);
                }
            }
        }
    }
}
```

**Размер после build:**

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

Output:
UltrasharpTools.Droid.exe: ~32 MB
```

---

### Phase 2: Server расширения

**Добавить в UltrasharpTools.RemoteServer:**

```csharp
// Services/MultiProjectVectorStore.cs
public class MultiProjectVectorStore
{
    private readonly Dictionary<string, VectorStore> _projectStores = new();

    public async Task<List<Match>> SearchAcrossProjects(
        string targetCode,
        double threshold,
        string[] projects = null) // null = all projects
    {
        var allMatches = new List<Match>();

        var storesToSearch = projects == null
            ? _projectStores.Values
            : _projectStores.Where(kv => projects.Contains(kv.Key)).Select(kv => kv.Value);

        foreach (var store in storesToSearch)
        {
            var matches = await store.Search(targetCode, threshold);
            allMatches.AddRange(matches);
        }

        return allMatches
            .OrderByDescending(m => m.Similarity)
            .ToList();
    }
}
```

**API endpoint:**

```csharp
// Controllers/DroidController.cs
[ApiController]
[Route("api/droid")]
public class DroidController : ControllerBase
{
    [HttpPost("file-changed")]
    public async Task<IActionResult> FileChanged([FromBody] FileChangedEvent evt)
    {
        // Store vectors in project-specific vector store
        await _vectorStore.StoreVectors(
            project: evt.Project,
            branch: evt.Branch,
            file: evt.File,
            vectors: evt.Vectors,
            symbols: evt.Symbols
        );

        return Ok();
    }

    [HttpPost("find-duplicates")]
    public async Task<IActionResult> FindDuplicates([FromBody] FindDuplicatesRequest req)
    {
        var matches = await _multiProjectStore.SearchAcrossProjects(
            targetCode: req.TargetCode,
            threshold: req.Threshold,
            projects: req.Scope == "all_team_projects" ? null : new[] { req.Project }
        );

        return Ok(new { matches });
    }
}
```

---

## 📊 Сравнительная таблица

| Аспект | Local Mode | Remote Mode | **Hybrid (Droid)** |
|--------|------------|-------------|--------------------|
| **Размер app** | 103 MB | 0 MB (browser) | **32 MB** ✅ |
| **Доступ к файлам** | Прямой | Нет | **Прямой** ✅ |
| **Cross-project search** | Нет | Нет | **Да!** 🎯 |
| **Storage (10 devs)** | 6 GB | 2.3 GB | **2.6 GB** ✅ |
| **Latency** | < 100ms | 50-500ms | **200-500ms** |
| **Network dependency** | Нет | Да | Да ⚠️ |
| **Offline work** | Да | Нет | Partial ⚠️ |
| **Setup complexity** | Простой | Сложный | **Средний** |
| **Team insights** | Нет | Да | **Да** ✅ |
| **Мощность анализа** | Local CPU | Server CPU | **Server CPU** ✅ |

**Вывод:** Hybrid mode - best of both worlds для команд!

---

## 💰 Cost Analysis

### Для команды 10 разработчиков

**Current Local Mode:**
```
Hardware cost: 10 * $0 = $0 (используют свои машины)
Storage: 10 * 500 MB cache = 5 GB локально (бесплатно)
Total: $0/месяц
```

**Минусы:**
- Каждый индексирует сам (duplicated work)
- Нет cross-project insights
- Медленно на слабых машинах

---

**Hybrid Mode:**
```
Droid на каждой машине: 10 * 32 MB = 320 MB
Server (Kubernetes small node): $50-100/месяц
Storage (5 GB PV): $10/месяц
Total: $60-110/месяц
```

**Плюсы:**
- Централизованный анализ (одна индексация)
- Cross-project search
- Team insights
- Экономия времени разработчиков: ~2-5 часов/неделю

**ROI:** Если hourly rate разработчика $50, экономия 2 часа/неделю = $100/неделю = **$400/месяц**

**Net benefit:** $400 - $110 = **$290/месяц прибыли!**

---

## 🚧 Implementation Roadmap

### Phase 1: Droid MVP (2-3 недели)
- [ ] Создать UltrasharpTools.Droid проект
- [ ] GitWatcherService
- [ ] FileWatcherService
- [ ] EmbeddingsService (Ollama client)
- [ ] ServerBridgeService (HTTP client)
- [ ] MCP stdio bridge
- [ ] Build & package (32 MB target)

### Phase 2: Server Extensions (1-2 недели)
- [ ] MultiProjectVectorStore
- [ ] API endpoints для droid
- [ ] Cross-project search
- [ ] Team insights dashboard

### Phase 3: Advanced Features (2-3 недели)
- [ ] Real-time notifications
- [ ] Conflict detection (duplicate code)
- [ ] Code reuse recommendations
- [ ] Team analytics

### Phase 4: Production (1 неделя)
- [ ] Documentation
- [ ] Deployment scripts
- [ ] CI/CD pipeline
- [ ] Release

**Total: ~6-9 недель development**

---

## 🎯 Conclusion

**ultrasharp-tools-droid - отличная идея!**

**Размер:** ~32 MB (vs 103 MB) - **3.2x меньше**

**Преимущества:**
✅ Легкий клиент на каждой машине
✅ Прямой доступ к локальным файлам
✅ Централизованное хранение (экономия storage)
✅ Cross-project search (уникально!)
✅ Team insights
✅ Мощный анализ на сервере
✅ ROI: $290/месяц для команды 10 человек

**Недостатки:**
⚠️ Требует network connectivity
⚠️ Latency +200-500ms
⚠️ Сложнее setup (server + droid)

**Рекомендация:**
- Solo developers → Local Mode
- Teams 5+ → **Hybrid Mode (droid)**
- CI/CD → Remote Mode

**Next steps:** Прототип droid'а для proof-of-concept!

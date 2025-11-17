# Unified Dual-Mode Architecture: ultrasharp-tool

**Один универсальный инструмент для локальной и сетевой работы.**

---

## 🎯 Концепция

**Проблема текущего подхода:**
- Droid (local) vs Overlord → два разных приложения
- Пользователи должны выбирать и настраивать разные системы
- Нет гибридного режима

**Новое решение: ultrasharp-tool**

```
┌──────────────────────────────────────────────────────┐
│                                                      │
│           ultrasharp-tool (103 MB)                   │
│              Универсальный клиент                    │
│                                                      │
│  ┌────────────────┐        ┌────────────────┐       │
│  │  Local Mode    │        │  Network Mode  │       │
│  │                │        │                │       │
│  │ • Всё локально │        │ • Local + Server│      │
│  │ • Offline      │   ←→   │ • Синхронизация│      │
│  │ • Fast         │        │ • GPU Semantic │      │
│  └────────────────┘        └────────────────┘       │
│                                                      │
│  Выбор режима: config.json или --mode flag          │
└──────────────────────────────────────────────────────┘
```

**Ключевая идея:** Один инструмент, режим выбирается конфигурацией.

---

## 🏗️ Архитектура

### ultrasharp-tool (Universal Client)

**Структура проекта:**

```
UltrasharpTools.sln
├─ UltrasharpTools.Tools/           # Core library (shared)
│  ├─ Services/
│  │  ├─ SolutionManager
│  │  ├─ CodeModificationService
│  │  ├─ SemanticSearchService     ← поддержка local + remote
│  │  ├─ SyncService               ← NEW! синхронизация с сервером
│  │  └─ RemoteExecutionService    ← NEW! выполнение на сервере
│  └─ Storage/
│     ├─ LocalVectorStore          ← локальная база
│     └─ RemoteVectorStoreClient   ← NEW! клиент к серверу
│
├─ UltrasharpTools.Droid/       → RENAME TO: UltrasharpTools/
│  ├─ Program.cs                    # Entry point
│  ├─ Config/
│  │  └─ ultrasharp-config.json    ← режим работы
│  └─ UltrasharpTools.csproj
│
└─ UltrasharpTools.Overlord/    # Server (unchanged)
   └─ (AI Agent capabilities added)
```

**Конфигурация (ultrasharp-config.json):**

```json
{
  "mode": "network",  // "local" или "network"

  "local": {
    "enableCache": true,
    "cacheDirectory": "%TEMP%/UltrasharpTools/Cache",
    "enableGit": true
  },

  "network": {
    "serverUrl": "https://ultrasharp-server.company.com",
    "apiKey": "your-api-key",
    "syncInterval": "30s",                    // синхронизация каждые 30 сек
    "syncMode": "incremental",                // "full" или "incremental"
    "enableLocalFallback": true,              // использовать local при недоступности сервера
    "semanticSearchMode": "server",           // "local", "server", "hybrid"
    "instrumentalMode": "auto"                // "local", "server", "auto" (выбирает сам)
  },

  "embedding": {
    "provider": "ollama",                     // "ollama", "tei", "server"
    "ollamaUrl": "http://localhost:11434",
    "model": "nomic-embed-text"
  }
}
```

---

## 🔄 Dual-Mode Behavior

### Local Mode (как сейчас)

```
┌────────────────────────────────────────┐
│ Developer Machine                      │
│                                        │
│ ultrasharp-tool (local mode)           │
│ ├─ Roslyn Analysis ─────────┐         │
│ ├─ Local Vector Store       │         │
│ ├─ Symbol Index (SQLite)    │ Всё     │
│ ├─ Quality Tools            │ локально│
│ ├─ Git Integration          │         │
│ └─ Embeddings (local Ollama)┘         │
│                                        │
│ Локальные файлы: D:\MyProject\        │
│ Cache: %TEMP%/UltrasharpTools/         │
└────────────────────────────────────────┘
```

**Когда использовать:**
- ✅ Solo developer
- ✅ Offline work
- ✅ No network infrastructure
- ✅ Maximum privacy

**Performance:**
- LoadSolution: 4.8s (with cache)
- Semantic search: < 100ms (local vectors)
- No network latency

---

### Network Mode (hybrid local + server)

```
┌──────────────────────────────────────────────────────┐
│ Developer Machine                                    │
│                                                      │
│ ultrasharp-tool (network mode)                       │
│ ├─ Local Vector Store (always available!) ───┐      │
│ ├─ Local Symbol Index (synced from server)   │      │
│ ├─ Git Watcher (tracks changes)              │      │
│ ├─ Sync Service (background)                 │ Local│
│ └─ Embeddings (local Ollama)                 ┘      │
│                                                      │
│ Локальные файлы: D:\MyProject\                      │
└───────────┬──────────────────────────────────────────┘
            │
            ↓ sync protocol (vectors + metadata)
            │
┌───────────────────────────────────────────────────────┐
│ Remote Server (Kubernetes + GPU)                      │
│                                                       │
│ ultrasharp-server                                     │
│ ├─ Multi-Project Vector Store ──────┐                │
│ │  • Project1: 50 MB, 100 branches  │                │
│ │  • Project2: 120 MB, 200 branches │ Centralized   │
│ │  • ... all team projects          │                │
│ ├─ GPU Semantic Search (large model)│                │
│ ├─ Multi-threaded Roslyn Analysis   │                │
│ ├─ Advanced AI Agent                │                │
│ │  • JIRA integration               │ Server        │
│ │  • Wiki/documentation reader      │                │
│ │  • Contract checker               │                │
│ │  • Vulnerability scanner          │                │
│ │  • Quality optimizer              │                │
│ └─ Team Insights Dashboard          ┘                │
│                                                       │
│ Storage: ~2-5 GB для команды 10-20 разработчиков     │
└───────────────────────────────────────────────────────┘
```

**Ключевые особенности:**

1. **Локальная база ВСЕГДА есть**
   - Synced snapshot с сервера
   - Fallback при потере сети
   - Инструментальные запросы работают offline

2. **Семантический поиск → GPU на сервере**
   - Более мощная модель (например, large embedding model 1024-dim)
   - Поиск по ВСЕМ проектам команды
   - Cross-project insights

3. **Инструментальные запросы: Auto-routing**
   ```csharp
   ViewDefinition(fullyQualifiedName: "...")

   → Local cache есть? → Выполнить локально (< 100ms)
   → Нет в cache? → Запросить сервер → Обновить cache
   ```

4. **Background sync**
   ```
   Every 30s (configurable):
   - Отправить новые vectors на сервер
   - Получить updates от других разработчиков
   - Sync branch metadata
   ```

**Когда использовать:**
- ✅ Team 5+ developers
- ✅ Multiple projects
- ✅ Need cross-project search
- ✅ Advanced AI features (JIRA, contracts, etc)

**Performance:**
- LoadSolution: 4.8s (local cache)
- Semantic search: 500-1000ms (server GPU, cross-project)
- Instrumental queries: < 100ms (local cache) или 200-500ms (server)

---

## 🔄 Sync Protocol (Local ↔ Server)

### Upload: Local → Server

**Событие: File changed**

```json
POST /api/sync/upload-vectors
{
  "project": "MyApp",
  "branch": "feature-auth",
  "commitSha": "abc123...", // или null для uncommitted
  "changes": [
    {
      "file": "src/Services/UserService.cs",
      "action": "modified",
      "symbols": [
        {
          "fullyQualifiedName": "MyApp.Services.UserService.ValidateEmail",
          "kind": "method",
          "line": 45,
          "vector": [0.123, 0.456, ..., 0.789], // 768 floats
          "metadata": {
            "complexity": 8,
            "linesOfCode": 12
          }
        }
      ]
    }
  ],
  "timestamp": "2025-01-17T12:34:56Z"
}
```

**Response:**
```json
{
  "status": "ok",
  "stored": 3, // количество сохраненных symbols
  "serverVersion": 12345 // версия серверной базы
}
```

**Размер:** ~6 KB per symbol (768 floats * 4 bytes + metadata)

---

### Download: Server → Local

**Событие: Background sync (every 30s)**

```json
GET /api/sync/get-updates?project=MyApp&branch=feature-auth&localVersion=12340

Response:
{
  "updates": [
    {
      "file": "src/Controllers/UserController.cs",
      "action": "modified",
      "author": "developer2@company.com",
      "symbols": [
        {
          "fullyQualifiedName": "MyApp.Controllers.UserController.GetUser",
          "kind": "method",
          "line": 23,
          "vector": [0.234, 0.567, ..., 0.890],
          "metadata": { ... }
        }
      ]
    }
  ],
  "serverVersion": 12345,
  "hasMore": false
}
```

**Что происходит:**
1. Local tool запрашивает updates с версии 12340
2. Server отдает изменения 12340 → 12345
3. Local tool обновляет свою базу
4. Local cache теперь синхронизирован

**Incremental sync:** Только изменения, не вся база!

---

### Full Sync (initial или recovery)

```json
POST /api/sync/full-sync
{
  "project": "MyApp",
  "branch": "main"
}

Response (chunked):
{
  "chunk": 1,
  "totalChunks": 10,
  "symbols": [
    // 1000 symbols
  ]
}
```

**Когда используется:**
- Первый запуск в network mode
- После длительного offline
- После конфликтов

---

## 🧠 Overlord Advanced Capabilities

### 1. Multi-threaded Instrumental Processing

**Проблема:** Один запрос = один поток = медленно для множества запросов.

**Решение:**

```csharp
// Overlord/Services/ParallelExecutionService.cs
public class ParallelExecutionService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;

    public ParallelExecutionService(int maxConcurrency = 16)
    {
        _maxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency);
    }

    public async Task<List<TResult>> ExecuteParallel<TRequest, TResult>(
        List<TRequest> requests,
        Func<TRequest, Task<TResult>> executor)
    {
        var tasks = requests.Select(async req =>
        {
            await _semaphore.WaitAsync();
            try
            {
                return await executor(req);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}
```

**Пример использования:**

```csharp
// Batch request: ViewDefinition для 50 symbols
var requests = symbols.Select(s => new ViewDefinitionRequest { FQN = s }).ToList();

var results = await _parallelExecution.ExecuteParallel(
    requests,
    async req => await _solutionManager.ViewDefinition(req.FQN)
);

// 16 threads параллельно → 16x faster!
```

**Performance:**
- Sequential: 50 symbols * 100ms = 5000ms
- Parallel (16 threads): 50 / 16 * 100ms = **312ms** (16x faster!)

---

### 2. GPU Orchestration для Semantic Search

**Проблема:** CPU embedding медленный для large-scale search.

**Решение: GPU-accelerated embeddings**

```
┌────────────────────────────────────────────────┐
│ Overlord                                   │
│                                                │
│ ┌────────────────────────────────────────┐    │
│ │ GPU Embedding Service                  │    │
│ │                                        │    │
│ │ ┌──────────────┐  ┌──────────────┐    │    │
│ │ │ TEI Instance │  │ TEI Instance │    │    │
│ │ │   (GPU 0)    │  │   (GPU 1)    │    │    │
│ │ │ Port: 8080   │  │ Port: 8081   │    │    │
│ │ └──────────────┘  └──────────────┘    │    │
│ │                                        │    │
│ │ Load Balancer (round-robin)           │    │
│ └────────────────────────────────────────┘    │
│                                                │
│ Throughput: ~1000 embeddings/sec (2x GPU)     │
└────────────────────────────────────────────────┘
```

**Configuration:**

```yaml
# kubernetes/tei-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tei-embedding-service
spec:
  replicas: 2  # 2 instances на 2 GPUs
  template:
    spec:
      containers:
      - name: tei
        image: ghcr.io/huggingface/text-embeddings-inference:latest
        args:
          - --model-id=BAAI/bge-large-en-v1.5  # Large model, 1024-dim
          - --port=8080
          - --max-batch-size=128
        resources:
          limits:
            nvidia.com/gpu: 1  # 1 GPU per instance
```

**GPU Embedding Service:**

```csharp
public class GpuEmbeddingService
{
    private readonly List<string> _teiEndpoints = new()
    {
        "http://tei-0.tei-service:8080",
        "http://tei-1.tei-service:8081"
    };

    private int _currentIndex = 0;

    public async Task<float[]> GetEmbedding(string text)
    {
        // Round-robin load balancing
        var endpoint = _teiEndpoints[_currentIndex];
        _currentIndex = (_currentIndex + 1) % _teiEndpoints.Count;

        var response = await _httpClient.PostAsJsonAsync(
            $"{endpoint}/embed",
            new { inputs = text }
        );

        return await response.Content.ReadFromJsonAsync<float[]>();
    }

    public async Task<List<float[]>> GetEmbeddingsBatch(List<string> texts)
    {
        // Batch processing для efficiency
        var response = await _httpClient.PostAsJsonAsync(
            $"{_teiEndpoints[0]}/embed",
            new { inputs = texts }
        );

        return await response.Content.ReadFromJsonAsync<List<float[]>>();
    }
}
```

**Performance:**

| Model | Dimensions | CPU Speed | GPU Speed (single) | GPU Speed (2x) |
|-------|------------|-----------|-------------------|----------------|
| nomic-embed-text | 768 | ~20 emb/sec | ~500 emb/sec | ~1000 emb/sec |
| bge-large-en-v1.5 | 1024 | ~10 emb/sec | ~300 emb/sec | ~600 emb/sec |

**Для cross-project search (10K symbols):**
- CPU: 10000 / 20 = **500 seconds** ❌
- GPU (2x): 10000 / 1000 = **10 seconds** ✅

**50x faster!**

---

### 3. Advanced AI Agent

**Концепция:** Server не просто хранит векторы, а **активно анализирует** код.

#### 3.1 JIRA/Wiki Integration

```csharp
public class JiraIntegrationService
{
    public async Task<CommitTaskAlignment> CheckCommitAlignment(
        string commitSha,
        string commitMessage)
    {
        // Parse JIRA ticket from commit message
        var ticketMatch = Regex.Match(commitMessage, @"([A-Z]+-\d+)");
        if (!ticketMatch.Success)
            return new CommitTaskAlignment { Status = "NoTicket" };

        var ticketId = ticketMatch.Groups[1].Value;

        // Fetch JIRA ticket
        var ticket = await _jiraClient.GetIssue(ticketId);

        // Get commit code changes
        var changes = await _git.GetCommitChanges(commitSha);

        // Semantic comparison
        var ticketEmbedding = await _embedding.GetEmbedding(ticket.Description);
        var codeEmbedding = await _embedding.GetEmbeddingsBatch(
            changes.Select(c => c.Code).ToList()
        );

        var similarity = CosineSimilarity(ticketEmbedding, codeEmbedding.Average());

        return new CommitTaskAlignment
        {
            TicketId = ticketId,
            TicketDescription = ticket.Description,
            Similarity = similarity,
            Status = similarity > 0.7 ? "Aligned" : "Misaligned",
            Recommendation = similarity < 0.7
                ? $"Код не соответствует задаче {ticketId}. Проверьте требования."
                : "Код соответствует задаче."
        };
    }
}
```

**Использование:**

```
Developer делает commit:
git commit -m "PROJ-123 Add email validation"

→ Git hook вызывает server API
→ Server анализирует соответствие коду и JIRA-123
→ Если similarity < 0.7 → Warning в PR comment
```

---

#### 3.2 Contract Checking (API Contracts)

```csharp
public class ContractCheckerService
{
    public async Task<ContractViolation[]> CheckApiContracts(
        string changedFile,
        string oldCode,
        string newCode)
    {
        // Parse API contracts from documentation
        var apiDocs = await _wikiReader.GetApiDocumentation();

        // Extract method signatures
        var oldMethods = await _roslyn.ExtractMethodSignatures(oldCode);
        var newMethods = await _roslyn.ExtractMethodSignatures(newCode);

        var violations = new List<ContractViolation>();

        foreach (var oldMethod in oldMethods)
        {
            var newMethod = newMethods.FirstOrDefault(m => m.Name == oldMethod.Name);

            if (newMethod == null)
            {
                // Method removed
                violations.Add(new ContractViolation
                {
                    Type = "Breaking Change",
                    Method = oldMethod.Name,
                    Description = "Public API method removed",
                    Severity = "Critical"
                });
                continue;
            }

            // Check signature changes
            if (oldMethod.Signature != newMethod.Signature)
            {
                // Check if documented in API spec
                var apiSpec = apiDocs.FirstOrDefault(d => d.Method == oldMethod.Name);

                if (apiSpec != null && !apiSpec.AllowsSignatureChange)
                {
                    violations.Add(new ContractViolation
                    {
                        Type = "Contract Violation",
                        Method = oldMethod.Name,
                        Description = $"Signature changed: {oldMethod.Signature} → {newMethod.Signature}",
                        Severity = "High",
                        Recommendation = "Update API documentation or revert breaking change"
                    });
                }
            }
        }

        return violations.ToArray();
    }
}
```

**Использование:**

```
Developer изменяет API method:
- Old: Task<User> GetUser(int id)
- New: Task<User> GetUser(string userId) ← breaking change!

→ Pre-commit hook вызывает server
→ Server проверяет contract
→ Violation detected → Блокирует commit с предупреждением
```

---

#### 3.3 Vulnerability Scanner

```csharp
public class VulnerabilityScanner
{
    private readonly Dictionary<string, VulnerabilityPattern> _patterns = new()
    {
        ["SQL Injection"] = new VulnerabilityPattern
        {
            Pattern = @"ExecuteRawSql\(.*\+.*\)", // string concatenation in SQL
            Severity = "Critical",
            CweId = "CWE-89"
        },
        ["XSS"] = new VulnerabilityPattern
        {
            Pattern = @"\.InnerHtml\s*=\s*[^@]", // direct HTML assignment without encoding
            Severity = "High",
            CweId = "CWE-79"
        },
        ["Hardcoded Secrets"] = new VulnerabilityPattern
        {
            Pattern = @"(password|apiKey|secret)\s*=\s*""[^""]+""",
            Severity = "Critical",
            CweId = "CWE-798"
        }
    };

    public async Task<Vulnerability[]> ScanCode(string code)
    {
        var vulnerabilities = new List<Vulnerability>();

        foreach (var (name, pattern) in _patterns)
        {
            var matches = Regex.Matches(code, pattern.Pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                // AI-enhanced: check if this is a false positive
                var context = GetContextAround(code, match.Index, 5); // 5 lines context

                var isFalsePositive = await _aiAgent.CheckFalsePositive(
                    vulnerabilityType: name,
                    code: context
                );

                if (!isFalsePositive)
                {
                    vulnerabilities.Add(new Vulnerability
                    {
                        Type = name,
                        Severity = pattern.Severity,
                        Line = GetLineNumber(code, match.Index),
                        Code = match.Value,
                        CweId = pattern.CweId,
                        Recommendation = GetRecommendation(name)
                    });
                }
            }
        }

        return vulnerabilities.ToArray();
    }
}
```

**AI-enhanced false positive detection:**

```csharp
public async Task<bool> CheckFalsePositive(string vulnerabilityType, string code)
{
    var prompt = $@"
Analyze this code for {vulnerabilityType} vulnerability.
Return 'false_positive' if safe, 'vulnerable' if not.

Code:
{code}

Analysis:";

    var response = await _llmClient.Complete(prompt);
    return response.Contains("false_positive");
}
```

**Использование:**

```
Developer пишет:
var sql = $"SELECT * FROM Users WHERE Id = {userId}"; ← SQL injection!

→ Pre-commit hook → Server scan
→ Vulnerability detected → Блокирует commit
→ Recommendation: "Use parameterized queries"
```

---

#### 3.4 Quality Optimizer

```csharp
public class QualityOptimizer
{
    public async Task<OptimizationSuggestion[]> OptimizeCode(string code)
    {
        var suggestions = new List<OptimizationSuggestion>();

        // Complexity analysis
        var complexity = await _roslyn.AnalyzeComplexity(code);

        if (complexity.Cyclomatic > 15)
        {
            // AI: suggest how to refactor
            var refactoringIdeas = await _aiAgent.SuggestRefactoring(code);

            suggestions.Add(new OptimizationSuggestion
            {
                Type = "Complexity Reduction",
                Current = $"Cyclomatic complexity: {complexity.Cyclomatic}",
                Target = "Cyclomatic complexity < 10",
                Ideas = refactoringIdeas
            });
        }

        // Performance analysis
        var perfIssues = await AnalyzePerformance(code);

        foreach (var issue in perfIssues)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = "Performance",
                Current = issue.Description,
                Recommendation = issue.Fix
            });
        }

        // Code duplication
        var duplicates = await _semantic.FindDuplicates(code, threshold: 0.85);

        if (duplicates.Any())
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = "Code Reuse",
                Current = $"Found {duplicates.Count} similar code blocks",
                Recommendation = "Extract shared logic to common method",
                Locations = duplicates.Select(d => d.Location).ToList()
            });
        }

        return suggestions.ToArray();
    }

    private async Task<PerformanceIssue[]> AnalyzePerformance(string code)
    {
        var issues = new List<PerformanceIssue>();

        // Example: N+1 query detection
        if (Regex.IsMatch(code, @"foreach.*\{[^}]*await.*Query"))
        {
            issues.Add(new PerformanceIssue
            {
                Description = "Potential N+1 query in loop",
                Fix = "Consider using batch query or Include() for eager loading"
            });
        }

        // Example: String concatenation in loop
        if (Regex.IsMatch(code, @"for.*\{[^}]*\+\s*"""))
        {
            issues.Add(new PerformanceIssue
            {
                Description = "String concatenation in loop",
                Fix = "Use StringBuilder for better performance"
            });
        }

        return issues.ToArray();
    }
}
```

**Использование:**

```
Developer пишет:
foreach (var user in users)
{
    var orders = await db.Orders.Where(o => o.UserId == user.Id).ToListAsync();
} ← N+1 query!

→ Pre-commit hook → Server analysis
→ Performance issue detected
→ Suggestion: "Use db.Users.Include(u => u.Orders)"
```

---

## 📊 Overlord Architecture

```
┌──────────────────────────────────────────────────────────────┐
│ Overlord (Kubernetes)                                    │
│                                                              │
│ ┌────────────────────────────────────────────────────────┐  │
│ │ API Layer (ASP.NET Core)                               │  │
│ │ ├─ /api/sync/*         (sync protocol)                 │  │
│ │ ├─ /api/search/*       (semantic search)               │  │
│ │ ├─ /api/instrumental/* (Roslyn operations)             │  │
│ │ └─ /api/insights/*     (team insights)                 │  │
│ └────────────────────────────────────────────────────────┘  │
│                                                              │
│ ┌────────────────────────────────────────────────────────┐  │
│ │ Core Services                                          │  │
│ │ ├─ MultiProjectVectorStore                             │  │
│ │ ├─ ParallelExecutionService (16 threads)               │  │
│ │ ├─ GpuEmbeddingService (2x GPU, 1000 emb/sec)          │  │
│ │ └─ SyncCoordinator                                     │  │
│ └────────────────────────────────────────────────────────┘  │
│                                                              │
│ ┌────────────────────────────────────────────────────────┐  │
│ │ AI Agent Services                                      │  │
│ │ ├─ JiraIntegrationService                              │  │
│ │ ├─ WikiReaderService                                   │  │
│ │ ├─ ContractCheckerService                              │  │
│ │ ├─ VulnerabilityScanner                                │  │
│ │ └─ QualityOptimizer                                    │  │
│ └────────────────────────────────────────────────────────┘  │
│                                                              │
│ ┌────────────────────────────────────────────────────────┐  │
│ │ Storage                                                │  │
│ │ ├─ Vector Store (Vectorlite HNSW)                      │  │
│ │ │  • Project1: 50 MB, 100 branches                    │  │
│ │ │  • Project2: 120 MB, 200 branches                   │  │
│ │ │  • ... all team projects                            │  │
│ │ ├─ Symbol Index (SQLite)                               │  │
│ │ ├─ Call Graph Cache                                    │  │
│ │ └─ Insights Database (PostgreSQL)                      │  │
│ └────────────────────────────────────────────────────────┘  │
│                                                              │
│ ┌────────────────────────────────────────────────────────┐  │
│ │ External Integrations                                  │  │
│ │ ├─ JIRA API                                            │  │
│ │ ├─ Confluence/Wiki API                                 │  │
│ │ ├─ Git webhooks                                        │  │
│ │ └─ LLM API (для AI agent)                              │  │
│ └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

---

## 🚀 Workflows

### Workflow 1: Developer работает в Network Mode

```
1. Developer запускает ultrasharp-tool в network mode
   → Читает config: mode = "network"
   → Подключается к server
   → Загружает local snapshot (if needed)

2. Developer редактирует UserService.cs
   → FileWatcher обнаруживает изменение
   → EmbeddingService векторизует (local Ollama, 200ms)
   → SyncService отправляет на server (6 KB)
   → Server сохраняет в MultiProjectVectorStore
   → Local cache updated

3. Claude: "Найди дубликаты этого метода во всех проектах"
   → ultrasharp-tool: semanticSearchMode = "server"
   → Request → server
   → Server: GPU embedding + HNSW search across ALL projects
   → Results: 5 matches (2 local, 3 in other projects)
   → ultrasharp-tool → Claude: results

4. Claude: "Покажи определение UserService.ValidateEmail"
   → ultrasharp-tool: instrumentalMode = "auto"
   → Check local cache: есть? → Return immediately (< 100ms)
   → Нет? → Request server → Update cache → Return

5. Developer делает commit
   → Git hook вызывает server API
   → Server:
     - Проверяет JIRA alignment
     - Сканирует vulnerabilities
     - Проверяет API contracts
     - Анализирует качество
   → Если OK → Commit allowed
   → Если issues → Warning/Block commit
```

**Latency:**
- File change sync: 200-300ms
- Semantic search (cross-project): 500-1000ms
- Instrumental (cached): < 100ms
- Instrumental (server): 200-500ms

**Network traffic:**
- File change: ~6 KB
- Semantic search: ~10-50 KB
- Background sync: ~100-500 KB / 30s

---

### Workflow 2: Team Insights (Server-side)

```
Every hour (background job on server):

1. Analyze all team projects:
   → Complexity hotspots
   → Code duplicates across projects
   → Vulnerability trends
   → API contract violations

2. Generate insights:
   → "Project1.OrderService.ProcessOrder has CC=45 (highest in team)"
   → "15 duplicate code blocks found across 3 projects"
   → "TeamProject2 has 8 unresolved vulnerabilities"

3. Push notifications to developers:
   → Email digest
   → Slack integration
   → Dashboard update

4. Store in insights database for historical analysis
```

---

### Workflow 3: Offline → Online Transition

```
Developer был offline 2 дня:

1. ultrasharp-tool работал в local fallback mode
   → Все запросы обрабатывались локально
   → Changes накапливались в pending queue

2. Network восстановился:
   → SyncService обнаруживает подключение
   → Triggers full sync
   → Upload pending changes (batched)
   → Download updates from server (2 дня изменений)
   → Merge changes (conflict resolution if needed)

3. Resume normal network mode
   → Semantic search → server
   → Instrumental → auto (cache или server)
   → Background sync каждые 30s
```

---

## 💾 Storage Requirements

### Для команды 20 разработчиков, 30 проектов

**Local (каждый разработчик):**
```
ultrasharp-tool app:       ~103 MB
Local vector snapshot:     ~200-500 MB (synced from server)
Symbol index snapshot:     ~50-100 MB
Cache:                     ~100-200 MB
Total per developer:       ~450-900 MB

20 developers:             ~9-18 GB total (distributed)
```

**Server:**
```
Vector stores (all projects):
- 15 small (10K symbols):  15 * 10 MB = 150 MB
- 10 medium (50K symbols): 10 * 50 MB = 500 MB
- 5 large (200K symbols):  5 * 200 MB = 1000 MB
Total vectors (main branches): ~1.7 GB

Branches (avg 50 per project):
- Delta storage (20% of full): 30 * 50 * 20% = ~300 MB
Total with branches: ~2 GB

Symbol indexes:            ~500 MB
Call graph cache:          ~200 MB
Insights database:         ~100 MB

Total server storage:      ~2.8 GB
```

**vs Current approach:**
- Local mode: 20 * (103 MB + 500 MB cache) = **12 GB** distributed
- Network mode: 2.8 GB server + 20 * 450 MB = **11.8 GB**

**Похоже, но:**
- ✅ Server storage дедуплицирован
- ✅ Local snapshots меньше (только нужное)
- ✅ Cross-project search (огромная ценность!)

---

## 🎯 Implementation Roadmap

### Phase 1: Dual-Mode Support (2-3 недели)
- [ ] Переименовать Droid → UltrasharpTools
- [ ] Добавить config: mode selection
- [ ] LocalVectorStore (существует)
- [ ] RemoteVectorStoreClient
- [ ] SyncService (background sync)
- [ ] Auto-routing (local vs server)

### Phase 2: Overlord Multi-threading (1 неделя)
- [ ] ParallelExecutionService
- [ ] Thread pool configuration
- [ ] Benchmarking

### Phase 3: GPU Orchestration (1-2 недели)
- [ ] TEI deployment (2x GPU)
- [ ] GpuEmbeddingService
- [ ] Load balancing
- [ ] Benchmarking

### Phase 4: AI Agent - Phase 1 (2-3 недели)
- [ ] JIRA integration
- [ ] Contract checker
- [ ] Vulnerability scanner
- [ ] Git hooks integration

### Phase 5: AI Agent - Phase 2 (2-3 недели)
- [ ] Wiki reader
- [ ] Quality optimizer
- [ ] Team insights
- [ ] Dashboard

**Total: ~9-14 недель development**

---

## 🎯 Conclusion

**Unified ultrasharp-tool с dual-mode - идеальное решение!**

**Преимущества:**
✅ **Один инструмент** - не нужно выбирать между разными версиями
✅ **Гибкость** - local или network mode по конфигурации
✅ **Local fallback** - работает offline даже в network mode
✅ **Cross-project search** - уникальная возможность!
✅ **GPU acceleration** - 50x faster semantic search
✅ **AI Agent** - JIRA, contracts, vulnerabilities, quality
✅ **Team insights** - видимость кодовой базы всей команды

**Use Cases:**

| Scenario | Mode | Config |
|----------|------|--------|
| Solo developer | local | `mode: "local"` |
| Team member (online) | network | `mode: "network"` |
| Team member (offline) | network (fallback) | `enableLocalFallback: true` |
| CI/CD pipeline | network | `mode: "network"` |

**Next steps:**
1. Implement Phase 1 (dual-mode support)
2. Test на реальной команде
3. Add GPU orchestration
4. Build AI Agent capabilities

Готов начать реализацию?

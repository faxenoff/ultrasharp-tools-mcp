# UltrasharpTools - Архитектура системы

**Версия:** 1.0.1
**Статус:** Production Ready
**Дата обновления:** 2025-11-18

---

## Обзор

UltrasharpTools - это MCP-сервер для интеллектуального анализа и модификации C# кода через Roslyn. Система предоставляет AI-агентам (Claude, GPT) прямой доступ к семантической структуре кода, а не просто к тексту.

### Ключевые компоненты

```
┌─────────────────────────────────────────────────────────────┐
│                  UltrasharpTools Ecosystem                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐        ┌──────────────────┐          │
│  │  Droid (Stdio)   │        │  Overlord (HTTP) │          │
│  │  Local Client    │        │  Team Server     │          │
│  └────────┬─────────┘        └────────┬─────────┘          │
│           │                           │                     │
│           └──────────┬────────────────┘                     │
│                      │                                      │
│           ┌──────────▼───────────────────────┐             │
│           │  UltrasharpTools.Tools (Core)    │             │
│           │  ────────────────────────────     │             │
│           │  • 52 MCP инструмента            │             │
│           │  • Roslyn Analysis & Modification│             │
│           │  • Git Integration               │             │
│           │  • Semantic Search & Indexing    │             │
│           │  • Quality Tools (CSharpier)     │             │
│           │  • Advanced Tracing (CFG, Z3)    │             │
│           │  • Layered Symbol Index          │             │
│           └──────────────────────────────────┘             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. Архитектурные принципы

### 1.1 Три-проектная структура

**Разделение ответственности:**

```
UltrasharpTools.Tools (Class Library)
├─ Вся бизнес-логика
├─ Сервисы (SolutionManager, CodeAnalysisService, etc.)
├─ MCP tool implementations
└─ Зависимости: Roslyn, LibGit2Sharp, NuGet.Protocol

         ↑                           ↑
         │                           │

Droid (Console App)         Overlord (Web App)
├─ Stdio transport              ├─ HTTP/SSE transport
├─ Local mode                   ├─ Team collaboration
├─ Claude Desktop integration   ├─ Multi-project vectorstore
└─ Single-user focus            └─ Cross-project search
```

**Преимущества:**
- Общая кодовая база для локального и сетевого режимов
- Легкое тестирование (Tools - pure business logic)
- Возможность создания новых транспортов без дублирования кода

### 1.2 Symbol-First подход

**Не текст, а символы:**

```
Традиционный подход:          UltrasharpTools:
┌───────────────────┐         ┌──────────────────────────┐
│ File: UserService │         │ ISymbol: UserService     │
│ Read as text      │         │ Roslyn Semantic Model    │
│ Parse with regex  │         │ Full metadata            │
│ Guess locations   │  →→→    │ Precise navigation       │
│ String replace    │         │ Semantic modifications   │
│ Hope it works     │         │ Guaranteed correctness   │
└───────────────────┘         └──────────────────────────┘
```

**Результат:**
- 100% точность в поиске и замене
- Автоматическое добавление `using` директив
- Рефакторинг с учётом контекста (переименования, перемещения)
- Compile-time проверка изменений

### 1.3 FQN-First Navigation

**Fully Qualified Name как primary key:**

```
AI Query: "Покажи UserService.ValidateEmail"
   ↓
FQN: MyApp.Services.UserService.ValidateEmail
   ↓
FastSymbolIndex.FindByFQN(fqn)
   ↓
IMethodSymbol (< 100ms)
   ↓
source_code + dependencies + references
```

**Без полного чтения файлов!** Token efficiency: 10-15x меньше токенов vs file-based.

---

## 2. Режимы работы

### 2.1 Local Mode (Stdio)

**Для кого:** Индивидуальные разработчики

```
┌──────────────────────────────────┐
│ Claude Desktop (local machine)  │
│   ↓ stdio                        │
│ UltrasharpTools.Droid.exe        │
│   ↓ прямой доступ к ФС           │
│ D:\MyProject\MyApp.sln           │
│   ↓                              │
│ • Roslyn analysis (in-process)  │
│ • Git operations (LibGit2Sharp)  │
│ • Local vector embeddings        │
│ • SQLite symbol cache            │
└──────────────────────────────────┘
```

**Характеристики:**
- ⚡ Максимальная скорость (без сетевых задержек)
- 🔒 Полная приватность (всё локально)
- 📦 103 MB standalone exe
- 💾 Работает с файлами на диске напрямую

### 2.2 Hybrid Mode (Local + Remote)

**Для кого:** Команды разработчиков

```
┌─────────────────────────────────────────┐
│ Developer Machine (Droid hybrid)        │
│ • Local Roslyn analysis                 │
│ • Local Git tracking                    │
│ • FileWatcher → detect changes          │
│ • EmbeddingService (local Ollama)       │
└──────────────┬──────────────────────────┘
               │ HTTP/REST
               ↓
┌─────────────────────────────────────────┐
│ Overlord Server (Kubernetes + GPU)     │
│ • MultiProjectVectorStore               │
│ • Cross-project semantic search         │
│ • Team insights & analytics             │
│ • Centralized vector database           │
└─────────────────────────────────────────┘
```

**Характеристики:**
- 🌐 Cross-project search по всей команде
- 🚀 GPU-accelerated embeddings на сервере
- 📊 Team insights (code duplication, complexity trends)
- 💾 Централизованное хранение (экономия 70% storage vs local)

### 2.3 Overlord Mode (Pure Remote)

**Для кого:** CI/CD, shared environments

```
┌──────────────────────────────────────┐
│ Client (любой HTTP клиент)           │
│   ↓ HTTP/SSE                         │
│ Overlord Server                      │
│   ↓ mounted volumes                  │
│ /app/projects/ (PersistentVolume)   │
│   ← git clone from GitHub/GitLab     │
└──────────────────────────────────────┘
```

**⚠️ Важно:** Overlord **НЕ имеет доступа** к файлам на вашей машине! Проекты должны быть в mounted volumes (NFS/git clone).

---

## 3. Ключевые подсистемы

### 3.1 Layered Symbol Indexing

**Проблема:** При переключении Git веток полная переиндексация занимает 30+ секунд.

**Решение:** Трёхслойная архитектура индекса

```
Layer 0: Base Index (SQLite cache)
├─ Main branch (or most used)
├─ 355K symbols
├─ Load: 4.8s (warm cache)
└─ 500-800 MB RAM

Layer 1: Branch Deltas (per branch)
├─ Added symbols: [...]
├─ Modified symbols: [...]
├─ Deleted symbol IDs: [...]
├─ Load: 16.6ms per branch (!)
└─ 10-50 MB RAM per branch

Layer 2: Working Deltas (uncommitted)
├─ Per client isolation
├─ Real-time updates (< 1ms)
└─ 5-20 MB RAM
```

**Query Flow:**
```
User: FindAsync(clientId, branch, term)
  ↓
baseResults = Layer0.Find(term)           // 10-50ms
  ↓
layer1Results = Layer1.Apply(baseResults) // 2-5ms
  ↓
finalResults = Layer2.Apply(layer1Results) // < 1ms
  ↓
return finalResults
```

**Performance:**
- Initial build: 48.3s (cold)
- Warm cache: 8.9s (5.4x faster)
- Branch switch: **16.6ms** (330x faster!)
- Document change: 100-500ms (66-330x faster vs full rebuild)

**Детали:** [Dev.Docs/Development/LAYERED_INDEXING_DESIGN.md](Dev.Docs/Development/LAYERED_INDEXING_DESIGN.md)

### 3.2 Universal Semantic Mode

**Революционный подход:** Semantic capabilities для **ВСЕХ** 52 инструментов, а не только для "semantic tools".

```
┌────────────────────────────────────────┐
│ MCP Tool Handler (любой инструмент)   │
└──────────────┬─────────────────────────┘
               │
               ▼
┌────────────────────────────────────────┐
│ ToolEnricher (15 strategies)           │
│ • ViewDefinition → Similar definitions│
│ • FindReferences → Similar usages     │
│ • ModifyCode → Similar changes        │
│ • ... и т.д.                          │
└──────────────┬─────────────────────────┘
               │
               ▼
┌────────────────────────────────────────┐
│ SemanticModeProvider (auto-detect)    │
│ ┌──────────────┬────────────────────┐  │
│ │ Local        │ Overlord (Remote)  │  │
│ │ TEI/Ollama   │ GPU embeddings     │  │
│ │ Fast         │ Cross-project      │  │
│ └──────────────┴────────────────────┘  │
└────────────────────────────────────────┘
```

**Пример обогащения:**

```
User: view_definition("UserService.ValidateEmail")

Original Result:
{
  "sourceCode": "public bool ValidateEmail(...) { ... }",
  "dependencies": ["System.Text.RegularExpressions"]
}

↓ Enriched Result (автоматически):

{
  "sourceCode": "...",
  "dependencies": ["..."],
  "semantic": {
    "similarDefinitions": [
      {
        "project": "TeamProject1",
        "method": "EmailValidator.IsValid",
        "similarity": 0.92,
        "recommendation": "Можно переиспользовать!"
      }
    ]
  }
}
```

**Покрытие:** 15 стратегий, 18 инструментов (85% топ-20)

**Детали:** [UNIVERSAL_SEMANTIC_MODE.md](UNIVERSAL_SEMANTIC_MODE.md)

### 3.3 Advanced Tracing

**Control Flow Graph трассировка:**

```
TraceExecution(method, maxDepth=5)
  ↓
Build CFG (Control Flow Graph)
  ↓
Traverse all execution paths
  ↓
Return step-by-step trace:
[1] 🚀 ENTRY: ProcessOrder(order)
[2] ✏️  ASSIGN: total = order.Total
[3] 🔀 BRANCH: if (total > 1000)
[4]   💰 CALL: ApplyDiscount(total)
[5] 🔀 BRANCH: else
[6]   💳 CALL: ProcessPayment(total)
```

**TraceBackwards с Z3 solver:**

```
analyze_path_feasibility(
  crashLocation: "PaymentService.cs:45",
  conditions: ["userId != null", "amount > 0"]
)
  ↓
Symbolic Execution (Z3 SMT Solver)
  ↓
Result:
{
  "feasible": false,
  "reason": "Contradiction: userId is null at line 42"
}
```

**Call Graph Caching:** 5-10x speedup на warm cache (SQLite persistence).

**Детали:** [Dev.Docs/Features/Tracing/ADVANCED_TRACING.md](Dev.Docs/Features/Tracing/ADVANCED_TRACING.md), [TRACING_OPTIMIZATION.md](Dev.Docs/Features/Tracing/TRACING_OPTIMIZATION.md)

### 3.4 Semantic Merge

**Умное 3-way слияние с пониманием структуры кода:**

```
Base:                   Ours:                  Theirs:
class User {            class User {           class Person {
  string Name;            string Name;           string Name;
}                         string Email;        }
                        }

                           ↓
          SemanticMerger (movement + rename detection)
                           ↓
                    Merged:
                    class Person {
                      string Name;
                      string Email;
                    }
```

**Возможности:**
- Movement detection (методы перемещены между классами)
- Rename detection (класс/метод переименован)
- Conflict resolution с ML-based recommendations

**Детали:** [Dev.Docs/Features/SemanticMerge/](Dev.Docs/Features/SemanticMerge/)

### 3.5 Quality Tools

**Интеграция с C# ecosystem:**

```
┌──────────────────────────────────────┐
│ format_code (CSharpier integration)  │
│ • Автоформатирование .cs файлов      │
│ • Respects .editorconfig             │
│ • Parallel processing                │
└──────────────────────────────────────┘

┌──────────────────────────────────────┐
│ analyze_code_style (Roslyn analyzers)│
│ • CA*, IDE*, CS* diagnostics         │
│ • Severity filtering                 │
│ • Pagination support                 │
└──────────────────────────────────────┘

┌──────────────────────────────────────┐
│ apply_code_fixes (auto-fix)          │
│ • Remove unused usings               │
│ • Preview mode                       │
│ • Auto git commit                    │
└──────────────────────────────────────┘
```

---

## 4. Технологический стек

### 4.1 Core Technologies

| Компонент | Технология | Версия |
|-----------|------------|--------|
| **Runtime** | .NET | 10.0 |
| **Language** | C# | 13.0 |
| **Code Analysis** | Roslyn | 5.0.0-2.final |
| **Git** | LibGit2Sharp | 0.31.0 |
| **Decompilation** | ICSharpCode.Decompiler | 10.0.0 |
| **Formatting** | CSharpier.Core | 1.2.1 |
| **Symbolic Execution** | Z3 Solver | 4.12.2 |

### 4.2 Performance Optimizations

**SIMD Vectorization:**
```csharp
// CosineSimilarity with System.Numerics.Vector<float>
// 4-8x faster на AVX2 processors
public static float ComputeSimilarity(float[] a, float[] b)
{
    var vA = new Vector<float>(a, 0);
    var vB = new Vector<float>(b, 0);
    return Vector.Dot(vA, vB);  // SIMD-accelerated
}
```

**xxHash32 hashing:**
- 2-3x быстрее SHA256
- Non-cryptographic (достаточно для cache keys)

**ReadyToRun (R2R) + Dynamic PGO:**
- ~30-50% faster startup
- Runtime adaptive optimizations

**Bloom Filters:**
- 99.9% accuracy, < 0.01% false positives
- O(1) symbol existence checks

### 4.3 Storage & Caching

```
.ultrasharp/
├── cache/
│   └── symbols/           # Symbol cache (50-100 MB)
│       └── <hash>.json
│
├── layered/
│   ├── deltas.db          # Branch deltas (SQLite)
│   └── vector_deltas.db   # Vector embeddings per branch
│
├── vectors/
│   └── embeddings.db      # Main vector store (Vectorlite HNSW)
│
└── call-graph/
    └── traces.db          # Call graph cache for tracing
```

**Backend Selection (адаптивный):**
- **< 10K symbols:** SqliteVec (brute-force SIMD, 100% accuracy)
- **> 10K symbols:** Vectorlite HNSW (3-100x faster, 99.9%+ recall)

---

## 5. Performance Benchmarks

### 5.1 Production Load Test

**Тестовый проект:** 890,868 символов, 61 проект, 112 git веток

| Операция | Cold | Warm | Speedup |
|----------|------|------|---------|
| **Solution load** | 48.3s | 8.9s | **5.4x** ✨ |
| **Symbol search** | 100ms | < 100ms | N/A |
| **Branch switch** | 48.3s | **16.6ms** | **330x** 🚀 |
| **TraceBackwards** | 2-3s | 400-600ms | **5-7x** ✨ |
| **Background cleanup** | 2.2s | N/A | N/A |

### 5.2 Memory Footprint

| Сценарий | Base | With Caching | Trade-off |
|----------|------|--------------|-----------|
| Small project (< 50K symbols) | 200 MB | +100 MB | 2x faster |
| Medium (50-200K) | 500 MB | +300 MB | 5x faster |
| Large (200K+) | 800 MB | +596 MB | 10x faster |

**Вывод:** Memory trade-off оправдан для production использования.

---

## 6. Deployment

### 6.1 Local (Claude Desktop)

```json
{
  "Droids": {
    "ultrasharp-tools": {
      "type": "stdio",
      "command": "D:/path/to/UltrasharpTools.Droid.exe",
      "args": ["--log-level", "Information"],
      "env": {}
    }
  }
}
```

### 6.2 Remote (Kubernetes)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ultrasharp-overlord
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: overlord
        image: ultrasharp-tools:3.0.0
        ports:
        - containerPort: 3001
        volumeMounts:
        - name: projects
          mountPath: /app/projects
      volumes:
      - name: projects
        persistentVolumeClaim:
          claimName: projects-pvc
```

**Подробнее:** [Run.Docs/Deployment/](Run.Docs/Deployment/)

---

## 7. Расширяемость

### 7.1 Добавление новых инструментов

```csharp
// 1. Создайте метод с атрибутом [DroidTool]
public class MyCustomTools
{
    [DroidTool(
        name: "my_custom_tool",
        description: "Does something useful"
    )]
    public static async Task<string> MyCustomTool(
        [Description("Input parameter")] string input)
    {
        // Implementation
        return result;
    }
}

// 2. Регистрируйте в DI
services.WithToolsFromAssembly(typeof(MyCustomTools).Assembly);

// 3. Done! Автоматически доступен через MCP
```

### 7.2 Semantic Enrichment Strategy

```csharp
public class MyEnrichmentStrategy : IEnrichmentStrategy
{
    public string ToolName => "my_tool";
    public bool CanEnrich(ToolContext ctx) => true;

    public async Task<EnrichmentResult> EnrichAsync(
        ToolContext context,
        CancellationToken ct)
    {
        // Ваша логика enrichment
        var semanticMatches = await FindSimilarCode(...);
        return new EnrichmentResult { ... };
    }
}

// Регистрация
services.AddSingleton<IEnrichmentStrategy, MyEnrichmentStrategy>();
```

---

## 8. Ограничения

### Текущие ограничения

1. **C# only:** Поддержка только .NET/C# проектов
2. **Git required:** Layered indexing требует Git репозиторий
3. **MSBuildWorkspace:** Нужен соответствующий .NET SDK
4. **Windows/Linux/macOS:** Cross-platform, но с нюансами (paths, line endings)

### Известные quirks

- **Newline handling:** modify_code может изменять пробелы
- **Incremental compilation:** Перезагрузка solution сбрасывает кеш
- **Large solutions:** > 1M символов может потребовать >2GB RAM

---

## 9. Дальнейшее развитие

См. [ROADMAP.md](ROADMAP.md) для детального плана.

### Near-term (Phase 13-14)

- Улучшение hybrid mode (полный MCP proxy)
- Real-time notifications через SSE
- Team analytics dashboard

### Mid-term

- VS Code extension
- Performance profiling integration
- Interactive debugging mode

### Long-term

- Multi-language support (TypeScript, Java, Python)
- Cloud-based semantic search
- AI-powered code recommendations

---

## 10. Ссылки

### Основная документация
- [README.md](README.md) - Обзор и quick start
- [CHANGELOG.md](CHANGELOG.md) - История изменений проекта
- [ROADMAP.md](ROADMAP.md) - Планы развития на 2025-2026
- [Dev.Docs/CLAUDE.md](Dev.Docs/CLAUDE.md) - Инструкции для Claude Code

### Технические детали
- [Layered Indexing](Dev.Docs/Development/LAYERED_INDEXING_DESIGN.md)
- [Universal Semantic Mode](UNIVERSAL_SEMANTIC_MODE.md)
- [Semantic Merge](Dev.Docs/Features/SemanticMerge/)
- [Advanced Tracing](Dev.Docs/Features/Tracing/)

### Guides
- [Setup Guide](SEMANTIC_SETUP_GUIDE.md)
- [Performance Report](Dev.Docs/Performance/Test_Report.md)
- [TODO List](Dev.Docs/Development/TODO.md)

---

**Версия документа:** 1.0
**Последнее обновление:** 2025-11-18
**Авторы:** UltrasharpTools Team

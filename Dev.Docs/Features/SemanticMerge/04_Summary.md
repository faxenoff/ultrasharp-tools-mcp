# Semantic Merge System - Краткое резюме

## 🎯 Главная идея

**Проблема**: Текстовые git merge ломаются когда код перемещается или рефакторится.

**Решение**: Сравнивать код по **смыслу** (семантике), а не по текстовому расположению.

## 🏗️ Архитектура в 4 слоя

```
┌─────────────────────────────────────────────────┐
│  Layer 0: NORMALIZATION (preprocessing)         │
│  Encoding/BOM/Line Endings → Unified Format     │
│  ✅ UTF-8 no BOM, LF endings, trim whitespace   │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  Layer 1: FAST PATH (90% случаев)               │
│  Hash/Signature matching - O(1)                 │
│  ✅ Мгновенно находит идентичный код            │
└─────────────────────────────────────────────────┘
                    ↓ (10% случаев)
┌─────────────────────────────────────────────────┐
│  Layer 2: SLOW PATH (когда Fast не сработал)    │
│  Vector Embeddings - O(n log n)                 │
│  ✅ Находит перемещённый/рефакторенный код      │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│  Layer 3: INTELLIGENT MERGE                     │
│  Intent-based + CFG Preservation                │
│  ✅ Объединяет изменения сохраняя поток         │
└─────────────────────────────────────────────────┘
```

## 🔑 Ключевые компоненты

### 1. CodeUnit - Универсальная единица кода
```csharp
record CodeUnit {
    string Id;                  // Стабильный ID
    string ContentHash;         // SHA256 (для Fast Path)
    string StructuralHash;      // AST hash (игнорирует whitespace)
    float[] Embedding;          // Вектор (генерируется лениво)
    ControlFlowGraph CFG;       // Для методов
}
```

**Поддерживаемые уровни**:
- File → Type → Method → Block
- JSON: File → JsonObject → JsonProperty
- Swagger: File → Path → Method → Response

### 2. FastPathMatcher - Быстрое сопоставление
```
Шаг 1: Content Hash совпадает? → 100% match (instant)
Шаг 2: Structural Hash совпадает? → 95% match (AST идентичен)
Шаг 3: Signature совпадает? → 85% match (FQN + params)
Шаг 4: ID совпадает? → 70% match (переименование?)

Если НИ ОДИН не сработал → отправить в Slow Path
```

**Производительность**: O(1) lookup через Dictionary

### 3. ContentNormalizer - Preprocessing файлов
```
КРИТИЧНО: Перед сравнением нужно нормализовать:

1. Encoding:
   UTF-8 (no BOM), UTF-8 (BOM), UTF-16 → UTF-8 no BOM

2. BOM (Byte Order Mark):
   Удаляется если присутствует

3. Line Endings:
   CR/LF (Windows), LF (Unix), CR (Mac) → LF

4. Trailing Whitespace:
   Пробелы/табы в конце строк → удаляются

5. Trailing Empty Lines:
   Несколько пустых строк в конце → одна LF

Результат: Одинаковый hash для семантически идентичного кода
```

**Пример проблемы без нормализации**:
```
File A (Windows): UTF-8 BOM, CR/LF
File B (Linux):   UTF-8 no BOM, LF

Без нормализации: Hash разный → ложный конфликт
С нормализацией: Hash одинаковый → Fast Path match ✅
```

### 4. SemanticMatcher - Поиск перемещений
```
Вход: CodeUnit который не нашёлся в Fast Path

Шаг 1: Генерировать embedding (если ещё нет)
Шаг 2: Vector search в целевой версии (top 5)
Шаг 3: Для каждого кандидата:
  - Проверить совместимость типов (Method с Method)
  - Сравнить CFG (control flow)
  - Combined score = Vector (70%) + Structural (30%)

Выход: Список похожих units с score

Threshold: 70%+ → считаем match
```

**Использование**: Только для 10% units (Fast Path не нашёл)

### 5. IntentClassifier - Понимание намерений
```
Анализирует ЧТО и ЗАЧЕМ изменилось:

- BugFix: добавлены try-catch, validation
- Refactoring: CFG не изменился
- FeatureAddition: новые методы/классы
- APIChange: signature изменилась
- CodeCleanup: только formatting
```

**Зачем?** Если обе ветки делают BugFix → merge автоматически. Если одна BugFix, другая APIChange → конфликт.

### 6. SemanticMergeEngine - Orchestrator
```
Алгоритм 3-way merge:

1. Индексировать 4 версии:
   - Base (общий предок)
   - Branch A
   - Branch B
   - Merged (предполагаемый результат)

2. Detect Changes:
   changesA = Diff(Base, BranchA)  // Fast + Slow Path
   changesB = Diff(Base, BranchB)

3. Classify Intents:
   intentsA = ClassifyIntents(changesA)
   intentsB = ClassifyIntents(changesB)

4. Find Conflicts:
   overlaps = FindOverlappingChanges(changesA, changesB)

   Для каждого overlap:
     Если intents совместимы → auto-merge
     Иначе → создать конфликт с предложениями

5. Apply Non-Conflicting:
   Все изменения без overlap → применить автоматически

6. Return:
   - MergeActions (что делать)
   - Conflicts (что требует ручного решения)
   - Stats (метрики)
```

## 📊 Пример работы

### Сценарий: Feature ветки
```
Base (main):
  class UserService {
    User GetUser(id) { ... }
    void UpdateUser(user) { ... }
  }

Branch A (feature/async-refactor):
  class UserService {
    async Task<User> GetUserAsync(id) { ... }  // Рефакторинг
    void UpdateUser(user) { ... }
    void DeleteUser(id) { ... }                // Новый метод
  }

Branch B (feature/validation):
  class UserService {
    User GetUser(id) {                        // Добавлена валидация
      if (id <= 0) throw ...
      ...
    }
    void UpdateUser(user) {
      if (user == null) throw ...             // Валидация
      ...
    }
  }
```

### Анализ изменений
```
Fast Path Results:
✅ UpdateUser (A vs Base): StructuralHash match → 95% (не изменён в A)
✅ UpdateUser (B vs Base): SignatureMatch → 85% (signature тот же, но тело изменилось)

Slow Path Results:
🔍 GetUser (A vs B): Semantic similarity 85%
   - Branch A: сделал async (refactoring intent)
   - Branch B: добавил validation (bugfix intent)
   - Intents: Compatible ✅

Movements Detected:
📦 GetUser → GetUserAsync (renamed + async)

New Additions:
➕ DeleteUser (только в A)

Intent Classification:
🔧 Branch A: Refactoring (async pattern)
🐛 Branch B: BugFix (validation added)
```

### Merge Result
```
Auto-Merged:
✅ GetUser → GetUserAsync + validation
   Combined:
   async Task<User> GetUserAsync(id) {
     if (id <= 0) throw ...        // From B
     ...                           // From A (async)
   }

✅ UpdateUser → validation added (from B)

✅ DeleteUser → new method (from A)

Conflicts: 0 🎉
```

## 🚀 Производительность

### Метрики
- **Fast Path Coverage**: 90-95% (зависит от степени рефакторинга)
- **Embedding Generation**: ~50ms/метод (768-dim, TEI)
- **Vector Search**: ~5ms для 10K vectors (SqliteVec)
- **Total Merge Time**: ~10-30 сек для проекта 100K LOC

### Оптимизации
1. **Lazy Embeddings**: Генерируем только когда нужно
2. **Caching**: Сохраняем индексы между запусками
3. **Parallel Processing**: Индексация классов параллельно
4. **Incremental Updates**: Только изменённые файлы

## 📦 Поддержка форматов

### C# (через Roslyn)
```csharp
CSharpParser:
  - Полная поддержка синтаксиса
  - CFG analysis (TraceExecution)
  - Semantic model
  - Multi-level: File → Namespace → Type → Method → Block
```

### JSON (swagger.json, config)
```csharp
JsonParser:
  - Generic JSON support
  - Hierarchical units: Object → Property → Array

SwaggerParser (специализация):
  - paths/* → каждый endpoint как unit
  - schemas/* → каждая модель как unit
  - Semantic comparison API endpoints
```

### Extensibility
```csharp
interface ICodeParser {
    bool CanParse(string filePath);
    Task<CodeUnit> ParseAsync(string content);
    Task<string> SerializeAsync(CodeUnit unit);
}

// Регистрация
ParserFactory.Register(new YamlParser());
ParserFactory.Register(new XmlParser());
```

## 🎯 MCP API

### Инструменты
```csharp
// 1. Создать merge session
UltrasharpTool_CreateMergeSession(
    solutionPath: "...",
    baseBranch: "main",
    branchA: "feature/auth",
    branchB: "feature/payments"
)

// 2. Индексация
UltrasharpTool_IndexMergeVersions(sessionId)
// → Indexed 1250 units (96% Fast Path)

// 3. Анализ
UltrasharpTool_AnalyzeMergeConflicts(sessionId)
// → Found 3 conflicts, 45 auto-mergeable changes

// 4. Выполнить merge
UltrasharpTool_PerformSemanticMerge(
    sessionId,
    strategy: "IntentPreserving"
)

// 5. Разрешить конфликт
UltrasharpTool_ResolveMergeConflict(
    sessionId,
    conflictId,
    resolution: "CombineBoth"
)
```

## 📈 Roadmap (7 фаз, ~20-25 дней)

1. **Foundation** (2-3 дня): CodeUnit, ContentNormalizer, FastPathMatcher, CSharpParser
2. **Semantic Matching** (3-4 дня): SemanticMatcher, MovementDetector
3. **Multi-Version Indexing** (2-3 дня): Git integration, 4 версии
4. **Merge Engine** (4-5 дней): IntentClassifier, ThreeWayMerger
5. **Multi-Format** (2-3 дня): JSON, Swagger, extensibility
6. **MCP Tools** (1-2 дня): API, documentation
7. **Optimization** (2-3 дня): Performance, caching, polish

## 💡 Ключевые преимущества

1. **Меньше конфликтов**: 50-70% reduction vs git merge
2. **Понимание намерений**: Auto-merge совместимых изменений
3. **Обнаружение перемещений**: Код переместился? Найдём!
4. **CFG Preservation**: Сохраняем control flow
5. **Multi-Format**: C#, JSON, XML, и др.
6. **Fast**: 90% через Fast Path (instant)
7. **Accurate**: 95%+ правильных предложений

## 🎓 Научная база

### Алгоритмы
- **3-Way Merge**: Классический алгоритм (Khanna et al.)
- **Tree Edit Distance**: Zhang-Shasha для AST
- **Vector Similarity**: Cosine similarity на embeddings
- **Control Flow**: Graph isomorphism detection

### ML/AI
- **Embeddings**: Transformer models (768-dim)
- **Intent Classification**: Heuristics + (future: supervised ML)
- **Conflict Resolution**: Rule-based + (future: RL)

---

**См. также**: SEMANTIC_MERGE_DESIGN.md для полной технической спецификации

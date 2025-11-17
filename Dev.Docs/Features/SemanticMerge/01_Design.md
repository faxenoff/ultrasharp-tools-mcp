# Semantic Merge System - Детальный план реализации

## 🎯 Цель
Создать систему интеллектуального слияния Git-веток на основе семантического анализа кода, а не текстовых различий. Система должна:
- Обнаруживать перемещение кода (rename, refactoring)
- Сохранять Control Flow при мерже
- Работать с C#, JSON (swagger.json), и другими форматами
- Минимизировать конфликты через понимание намерений (intents)

## 🏗️ Архитектурные принципы

### Принцип 1: Hybrid Matching (Fast + Slow Path)
```
┌─────────────────────────────────────────────────┐
│  Fast Path (90% случаев)                        │
│  ├─ Hash-based matching (SHA256 content)        │
│  ├─ FQN/Signature matching                      │
│  ├─ Structural fingerprint (AST hash)           │
│  └─ Result: O(1) lookup, мгновенно              │
└─────────────────────────────────────────────────┘
                    ↓ (если не совпало)
┌─────────────────────────────────────────────────┐
│  Slow Path (10% случаев)                        │
│  ├─ Vector embedding similarity                 │
│  ├─ Control Flow Graph comparison               │
│  ├─ Semantic intent classification              │
│  └─ Result: O(n log n), используется только     │
│     когда Fast Path не дал результата           │
└─────────────────────────────────────────────────┘
```

### Принцип 2: Multi-Level Granularity
Индексация на разных уровнях детализации:
- **Level 0**: File (весь файл целиком)
- **Level 1**: Type (class, interface, enum)
- **Level 2**: Member (method, property, field)
- **Level 3**: Block (if/else, loop, try-catch)

**Зачем?** Если метод переместился между классами - сопоставление на Level 2. Если код внутри метода изменился - Level 3.

### Принцип 3: Lazy Embedding
```csharp
// Генерация embeddings только когда нужно
if (!FastPathMatch(unitA, unitB))
{
    // Только теперь генерируем embeddings
    var embeddingA = await GenerateEmbedding(unitA);
    var embeddingB = await GenerateEmbedding(unitB);
    similarity = CosineSimilarity(embeddingA, embeddingB);
}
```

## 📁 Структура проекта

```
UltrasharpTools.Tools/
├─ Merge/                           # Новая подсистема
│  ├─ Models/
│  │  ├─ CodeUnit.cs                # Базовая единица кода
│  │  ├─ VersionedIndex.cs          # Индекс для одной версии
│  │  ├─ MergeResult.cs             # Результат мерджа
│  │  ├─ SemanticConflict.cs        # Конфликт
│  │  └─ ChangeIntent.cs            # Намерение изменения
│  │
│  ├─ Indexing/
│  │  ├─ MultiVersionIndexer.cs    # Индексация 4 версий
│  │  ├─ CodeUnitExtractor.cs      # Извлечение units из кода
│  │  ├─ StructuralFingerprint.cs  # Быстрые хэши структур
│  │  ├─ ContentNormalizer.cs      # Нормализация encoding/line endings
│  │  └─ LazyEmbeddingGenerator.cs # Ленивая генерация embeddings
│  │
│  ├─ Matching/
│  │  ├─ FastPathMatcher.cs        # Hash/FQN matching
│  │  ├─ SemanticMatcher.cs        # Embedding-based matching
│  │  ├─ MovementDetector.cs       # Обнаружение перемещений
│  │  └─ StructuralAligner.cs      # Нормализация порядка
│  │
│  ├─ Analysis/
│  │  ├─ ControlFlowAnalyzer.cs    # CFG сравнение
│  │  ├─ IntentClassifier.cs       # Определение intent
│  │  ├─ ConflictDetector.cs       # Поиск конфликтов
│  │  └─ CompatibilityChecker.cs   # Проверка совместимости
│  │
│  ├─ Engine/
│  │  ├─ SemanticMergeEngine.cs    # Главный orchestrator
│  │  ├─ ThreeWayMerger.cs         # 3-way merge алгоритм
│  │  ├─ ConflictResolver.cs       # Разрешение конфликтов
│  │  └─ MergeStrategySelector.cs  # Выбор стратегии
│  │
│  ├─ Parsers/
│  │  ├─ ICodeParser.cs            # Интерфейс парсера
│  │  ├─ CSharpParser.cs           # C# через Roslyn
│  │  ├─ JsonParser.cs             # JSON (swagger, config)
│  │  ├─ SwaggerParser.cs          # Специализация для OpenAPI
│  │  └─ ParserFactory.cs          # Factory pattern
│  │
│  └─ Storage/
│     ├─ MergeIndexStore.cs        # Хранение индексов
│     ├─ CacheManager.cs           # Кэш для embeddings
│     └─ VersionManager.cs         # Управление версиями
│
└─ Mcp/Tools/
   └─ MergeTools.cs                # MCP инструменты для мерджа
```

## 🔧 Ключевые компоненты

### 1. CodeUnit - Базовая единица кода

```csharp
namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Универсальная единица кода на любом уровне гранулярности.
/// Может быть: файлом, классом, методом, блоком кода, JSON объектом.
/// </summary>
public sealed record CodeUnit
{
    /// <summary>Уникальный стабильный ID (переживает rename/move)</summary>
    public required string Id { get; init; }

    /// <summary>Тип единицы (File, Type, Method, Block)</summary>
    public required CodeUnitType Type { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Исходный код/контент</summary>
    public required string Content { get; init; }

    /// <summary>SHA256 hash контента (для Fast Path)</summary>
    public required string ContentHash { get; init; }

    /// <summary>Структурный fingerprint (AST hash, игнорирует whitespace/comments)</summary>
    public required string StructuralHash { get; init; }

    /// <summary>Signature (для методов: FQN + параметры)</summary>
    public string? Signature { get; init; }

    /// <summary>Embedding (генерируется лениво, может быть null)</summary>
    public float[]? Embedding { get; init; }

    /// <summary>AST representation (для структурного анализа)</summary>
    public CodeStructure? Structure { get; init; }

    /// <summary>Control Flow Graph (только для методов)</summary>
    public ControlFlowGraph? CFG { get; init; }

    /// <summary>Иерархия: ID родителя</summary>
    public string? ParentId { get; init; }

    /// <summary>Иерархия: ID детей</summary>
    public IReadOnlySet<string> ChildIds { get; init; } = new HashSet<string>();

    /// <summary>Metadata (зависит от типа парсера)</summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Позиция в исходном файле (строка, колонка)</summary>
    public SourceLocation? Location { get; init; }
}

public enum CodeUnitType
{
    File,           // Весь файл
    Namespace,      // Namespace
    Type,           // Class, Interface, Struct, Enum
    Method,         // Method, Constructor, Property getter/setter
    Field,          // Field, Property
    Block,          // Control flow block (if, loop, try-catch)
    Statement,      // Single statement
    JsonObject,     // JSON object (для .json файлов)
    JsonArray,      // JSON array
    JsonProperty    // JSON property
}

public sealed record CodeStructure
{
    /// <summary>Нормализованный AST (без whitespace, comments)</summary>
    public required string NormalizedAst { get; init; }

    /// <summary>Список всех идентификаторов (variables, methods, types)</summary>
    public required IReadOnlySet<string> Identifiers { get; init; }

    /// <summary>Список всех типов, используемых в коде</summary>
    public required IReadOnlySet<string> UsedTypes { get; init; }

    /// <summary>Канонический порядок членов (для классов)</summary>
    public IReadOnlyList<string>? CanonicalOrder { get; init; }
}

public sealed record SourceLocation
{
    public required int StartLine { get; init; }
    public required int StartColumn { get; init; }
    public required int EndLine { get; init; }
    public required int EndColumn { get; init; }
}
```

### 2. FastPathMatcher - Быстрое сопоставление

```csharp
namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Fast Path matching: O(1) lookup по hash/signature.
/// Обрабатывает 90% случаев без embeddings.
/// </summary>
public sealed class FastPathMatcher
{
    private readonly ILogger<FastPathMatcher> _logger;

    public FastPathMatcher(ILogger<FastPathMatcher>? logger = null)
    {
        _logger = logger ?? NullLogger<FastPathMatcher>.Instance;
    }

    /// <summary>
    /// Попытка быстрого сопоставления двух CodeUnit.
    /// </summary>
    public FastPathMatchResult? TryMatch(CodeUnit unitA, CodeUnit unitB)
    {
        // Level 1: Exact content match (100% идентичны)
        if (unitA.ContentHash == unitB.ContentHash)
        {
            _logger.LogDebug("Exact match (content hash): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.ExactContent,
                Confidence = 1.0f
            };
        }

        // Level 2: Structural match (одинаковая структура, разные комментарии/whitespace)
        if (unitA.StructuralHash == unitB.StructuralHash)
        {
            _logger.LogDebug("Structural match (AST hash): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.StructuralSame,
                Confidence = 0.95f
            };
        }

        // Level 3: Signature match (для методов - FQN + parameters)
        if (unitA.Signature != null &&
            unitB.Signature != null &&
            unitA.Signature == unitB.Signature)
        {
            // Та же сигнатура, но разное тело метода
            _logger.LogDebug("Signature match: {Signature}", unitA.Signature);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.SignatureMatch,
                Confidence = 0.85f
            };
        }

        // Level 4: ID match (переименование, но тот же символ)
        if (unitA.Id == unitB.Id)
        {
            _logger.LogDebug("ID match (renamed?): {Id}", unitA.Id);
            return new FastPathMatchResult
            {
                UnitA = unitA,
                UnitB = unitB,
                MatchType = FastPathMatchType.IdMatch,
                Confidence = 0.7f
            };
        }

        // Fast path не сработал - нужен Slow Path
        return null;
    }

    /// <summary>
    /// Bulk matching для всех units в версии.
    /// </summary>
    public Dictionary<string, FastPathMatchResult> BulkMatch(
        VersionedIndex baseVersion,
        VersionedIndex targetVersion)
    {
        var matches = new Dictionary<string, FastPathMatchResult>();

        // Создать lookup таблицы для O(1) доступа
        var hashToUnits = targetVersion.Units.Values
            .GroupBy(u => u.ContentHash)
            .ToDictionary(g => g.Key, g => g.ToList());

        var structHashToUnits = targetVersion.Units.Values
            .GroupBy(u => u.StructuralHash)
            .ToDictionary(g => g.Key, g => g.ToList());

        var signatureToUnits = targetVersion.Units.Values
            .Where(u => u.Signature != null)
            .GroupBy(u => u.Signature!)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var baseUnit in baseVersion.Units.Values)
        {
            // Попробовать content hash
            if (hashToUnits.TryGetValue(baseUnit.ContentHash, out var exactMatches))
            {
                // Обычно должен быть только один
                var match = exactMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.ExactContent,
                    Confidence = 1.0f
                };
                continue;
            }

            // Попробовать structural hash
            if (structHashToUnits.TryGetValue(baseUnit.StructuralHash, out var structMatches))
            {
                var match = structMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.StructuralSame,
                    Confidence = 0.95f
                };
                continue;
            }

            // Попробовать signature
            if (baseUnit.Signature != null &&
                signatureToUnits.TryGetValue(baseUnit.Signature, out var sigMatches))
            {
                var match = sigMatches.First();
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = match,
                    MatchType = FastPathMatchType.SignatureMatch,
                    Confidence = 0.85f
                };
                continue;
            }

            // ID match
            if (targetVersion.Units.TryGetValue(baseUnit.Id, out var idMatch))
            {
                matches[baseUnit.Id] = new FastPathMatchResult
                {
                    UnitA = baseUnit,
                    UnitB = idMatch,
                    MatchType = FastPathMatchType.IdMatch,
                    Confidence = 0.7f
                };
            }

            // Если ничего не нашли - unit попадёт в Slow Path
        }

        _logger.LogInformation(
            "Fast Path matched {Matched}/{Total} units ({Percent:F1}%)",
            matches.Count,
            baseVersion.Units.Count,
            matches.Count * 100.0 / baseVersion.Units.Count
        );

        return matches;
    }
}

public sealed record FastPathMatchResult
{
    public required CodeUnit UnitA { get; init; }
    public required CodeUnit UnitB { get; init; }
    public required FastPathMatchType MatchType { get; init; }
    public required float Confidence { get; init; }
}

public enum FastPathMatchType
{
    ExactContent,       // 100% совпадение контента
    StructuralSame,     // Одинаковая AST структура
    SignatureMatch,     // Совпадает сигнатура (FQN + params)
    IdMatch             // Совпадает ID (возможно переименование)
}
```

### 3. ContentNormalizer - Нормализация перед сравнением

```csharp
namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Нормализует содержимое файлов перед сравнением/хэшированием.
/// КРИТИЧНО: Encoding, BOM, line endings могут различаться,
/// но семантически код идентичен.
/// </summary>
public sealed class ContentNormalizer
{
    private readonly ILogger<ContentNormalizer> _logger;

    // Целевая нормализация для всех файлов
    private static readonly Encoding TargetEncoding = new UTF8Encoding(encoderShouldEmitBOM: false);
    private const string TargetLineEnding = "\n";  // LF (Unix-style)

    public ContentNormalizer(ILogger<ContentNormalizer>? logger = null)
    {
        _logger = logger ?? NullLogger<ContentNormalizer>.Instance;
    }

    /// <summary>
    /// Нормализовать содержимое файла для точного сравнения.
    /// </summary>
    public async Task<NormalizedContent> NormalizeAsync(
        string filePath,
        CancellationToken ct = default)
    {
        // 1. Прочитать raw bytes
        var rawBytes = await File.ReadAllBytesAsync(filePath, ct);

        // 2. Определить encoding
        var detectedEncoding = DetectEncoding(rawBytes, out var hasBom);

        // 3. Декодировать в string
        var content = detectedEncoding.GetString(rawBytes);

        // 4. Удалить BOM если есть (в начале string)
        if (hasBom && content.StartsWith('\uFEFF'))
        {
            content = content.Substring(1);
            _logger.LogDebug("Removed BOM from {FilePath}", filePath);
        }

        // 5. Нормализовать line endings (CR/LF → LF)
        var normalizedContent = NormalizeLineEndings(content);

        // 6. Опционально: trim trailing whitespace на каждой строке
        if (_config.TrimTrailingWhitespace)
        {
            normalizedContent = TrimTrailingWhitespace(normalizedContent);
        }

        // 7. Опционально: удалить trailing empty lines в конце файла
        if (_config.RemoveTrailingEmptyLines)
        {
            normalizedContent = normalizedContent.TrimEnd('\n', '\r');
            normalizedContent += "\n";  // Всегда заканчиваем на один \n
        }

        return new NormalizedContent
        {
            Content = normalizedContent,
            OriginalEncoding = detectedEncoding,
            HadBOM = hasBom,
            OriginalLineEnding = DetectLineEnding(content),
            NormalizedEncoding = TargetEncoding,
            NormalizedLineEnding = TargetLineEnding
        };
    }

    /// <summary>
    /// Определить encoding файла.
    /// </summary>
    private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
    {
        hasBom = false;

        if (bytes.Length < 2)
            return Encoding.UTF8;

        // BOM detection
        // UTF-8 BOM: EF BB BF
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            hasBom = true;
            return new UTF8Encoding(encoderShouldEmitBOM: true);
        }

        // UTF-16 LE BOM: FF FE
        if (bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            hasBom = true;
            return Encoding.Unicode;
        }

        // UTF-16 BE BOM: FE FF
        if (bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            hasBom = true;
            return Encoding.BigEndianUnicode;
        }

        // UTF-32 LE BOM: FF FE 00 00
        if (bytes.Length >= 4 &&
            bytes[0] == 0xFF && bytes[1] == 0xFE &&
            bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            hasBom = true;
            return Encoding.UTF32;
        }

        // Heuristic: если все байты < 128 → ASCII/UTF-8
        // Иначе → скорее всего UTF-8 без BOM
        return Encoding.UTF8;
    }

    /// <summary>
    /// Нормализовать line endings: CR/LF, CR → LF.
    /// </summary>
    private static string NormalizeLineEndings(string content)
    {
        // Replace CR/LF (Windows) → LF
        content = content.Replace("\r\n", "\n");

        // Replace CR (old Mac) → LF
        content = content.Replace("\r", "\n");

        return content;
    }

    /// <summary>
    /// Определить какой line ending используется (для метаданных).
    /// </summary>
    private static string DetectLineEnding(string content)
    {
        if (content.Contains("\r\n"))
            return "\r\n";  // Windows (CR/LF)
        if (content.Contains("\n"))
            return "\n";    // Unix (LF)
        if (content.Contains("\r"))
            return "\r";    // Old Mac (CR)

        return "\n";        // Default: Unix
    }

    /// <summary>
    /// Удалить trailing whitespace в конце каждой строки.
    /// </summary>
    private static string TrimTrailingWhitespace(string content)
    {
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd(' ', '\t');
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Вычислить hash нормализованного контента.
    /// ВАЖНО: используется для FastPath matching.
    /// </summary>
    public static string ComputeContentHash(string normalizedContent)
    {
        using var sha256 = SHA256.Create();
        var bytes = TargetEncoding.GetBytes(normalizedContent);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}

public sealed record NormalizedContent
{
    public required string Content { get; init; }
    public required Encoding OriginalEncoding { get; init; }
    public required bool HadBOM { get; init; }
    public required string OriginalLineEnding { get; init; }
    public required Encoding NormalizedEncoding { get; init; }
    public required string NormalizedLineEnding { get; init; }
}

public sealed record ContentNormalizerConfig
{
    /// <summary>
    /// Удалять trailing whitespace в конце строк.
    /// </summary>
    public bool TrimTrailingWhitespace { get; init; } = true;

    /// <summary>
    /// Удалять пустые строки в конце файла.
    /// </summary>
    public bool RemoveTrailingEmptyLines { get; init; } = true;

    /// <summary>
    /// Игнорировать whitespace-only изменения при сравнении.
    /// </summary>
    public bool IgnoreWhitespaceChanges { get; init; } = false;

    public static ContentNormalizerConfig Default => new();

    /// <summary>
    /// Строгая нормализация (для production).
    /// </summary>
    public static ContentNormalizerConfig Strict => new()
    {
        TrimTrailingWhitespace = true,
        RemoveTrailingEmptyLines = true,
        IgnoreWhitespaceChanges = false
    };

    /// <summary>
    /// Relaxed mode (игнорирует whitespace различия).
    /// </summary>
    public static ContentNormalizerConfig Relaxed => new()
    {
        TrimTrailingWhitespace = true,
        RemoveTrailingEmptyLines = true,
        IgnoreWhitespaceChanges = true
    };
}
```

**Использование в FastPathMatcher**:

```csharp
// BEFORE matching
var normalizedA = await _normalizer.NormalizeAsync(unitA.FilePath);
var normalizedB = await _normalizer.NormalizeAsync(unitB.FilePath);

// Compute hashes на НОРМАЛИЗОВАННОМ контенте
unitA.ContentHash = ContentNormalizer.ComputeContentHash(normalizedA.Content);
unitB.ContentHash = ContentNormalizer.ComputeContentHash(normalizedB.Content);

// Теперь можно сравнивать
if (unitA.ContentHash == unitB.ContentHash)
{
    // 100% match (даже если encoding/line endings различались)
}
```

**Что нормализуется**:

1. ✅ **Encoding**: UTF-8 (no BOM), UTF-8 (BOM), UTF-16 → все в UTF-8 no BOM
2. ✅ **BOM**: Удаляется если присутствует
3. ✅ **Line Endings**: CR/LF (Windows), LF (Unix), CR (Mac) → все в LF
4. ✅ **Trailing Whitespace**: Пробелы/табы в конце строк → удаляются
5. ✅ **Trailing Empty Lines**: Множество пустых строк в конце → одна LF

**Что НЕ нормализуется**:

- ❌ Leading whitespace (отступы) - важны для синтаксиса
- ❌ Пустые строки внутри кода - могут быть семантичны
- ❌ Комментарии - обрабатываются на уровне AST

### 4. SemanticMatcher - Медленный путь (embeddings)

```csharp
namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Slow Path: vector embedding similarity для обнаружения
/// перемещённого/рефакторенного кода.
/// Используется только когда Fast Path не нашёл совпадения.
/// </summary>
public sealed class SemanticMatcher
{
    private readonly LazyEmbeddingGenerator _embeddingGen;
    private readonly ILogger<SemanticMatcher> _logger;

    public SemanticMatcher(
        LazyEmbeddingGenerator embeddingGen,
        ILogger<SemanticMatcher>? logger = null)
    {
        _embeddingGen = embeddingGen;
        _logger = logger ?? NullLogger<SemanticMatcher>.Instance;
    }

    /// <summary>
    /// Найти семантически похожие units через embeddings.
    /// </summary>
    public async Task<List<SemanticMatchResult>> FindSimilarUnitsAsync(
        CodeUnit queryUnit,
        VersionedIndex targetVersion,
        float minSimilarity = 0.7f,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        // 1. Сгенерировать embedding для query (если ещё нет)
        var queryEmbedding = queryUnit.Embedding ??
            await _embeddingGen.GenerateAsync(queryUnit, ct);

        // 2. Найти кандидатов через vector search
        var candidates = await targetVersion.VectorStore.SearchAsync(
            queryEmbedding,
            limit: maxResults * 2, // Берём с запасом для фильтрации
            minSimilarity,
            ct
        );

        var results = new List<SemanticMatchResult>();

        foreach (var candidate in candidates.Take(maxResults))
        {
            var targetUnit = targetVersion.Units[candidate.Id];

            // 3. Дополнительные проверки совместимости
            var typeCompatible = AreTypesCompatible(queryUnit, targetUnit);
            if (!typeCompatible)
            {
                _logger.LogDebug(
                    "Skipping {TargetId}: incompatible types ({QueryType} vs {TargetType})",
                    targetUnit.Id, queryUnit.Type, targetUnit.Type
                );
                continue;
            }

            // 4. Structural similarity (CFG comparison для методов)
            var structuralSim = await CalculateStructuralSimilarityAsync(
                queryUnit,
                targetUnit,
                ct
            );

            // 5. Комбинированный score (vector + structural)
            var combinedScore = (candidate.Similarity * 0.7f) + (structuralSim * 0.3f);

            if (combinedScore >= minSimilarity)
            {
                results.Add(new SemanticMatchResult
                {
                    QueryUnit = queryUnit,
                    MatchedUnit = targetUnit,
                    VectorSimilarity = candidate.Similarity,
                    StructuralSimilarity = structuralSim,
                    CombinedScore = combinedScore,
                    MatchType = DetermineMatchType(queryUnit, targetUnit, combinedScore)
                });
            }
        }

        // Сортировка по combined score
        results.Sort((a, b) => b.CombinedScore.CompareTo(a.CombinedScore));

        _logger.LogInformation(
            "Semantic search for {QueryId}: found {Count} matches (min sim: {MinSim})",
            queryUnit.Id, results.Count, minSimilarity
        );

        return results;
    }

    private bool AreTypesCompatible(CodeUnit a, CodeUnit b)
    {
        // Method может матчиться только с Method
        // Type с Type, и т.д.
        if (a.Type != b.Type)
        {
            // Исключение: Block может матчиться с Method (извлечённый блок)
            if ((a.Type == CodeUnitType.Block && b.Type == CodeUnitType.Method) ||
                (a.Type == CodeUnitType.Method && b.Type == CodeUnitType.Block))
            {
                return true;
            }
            return false;
        }
        return true;
    }

    private async Task<float> CalculateStructuralSimilarityAsync(
        CodeUnit a,
        CodeUnit b,
        CancellationToken ct)
    {
        // Для методов: сравнить CFG
        if (a.Type == CodeUnitType.Method && a.CFG != null && b.CFG != null)
        {
            return CompareControlFlowGraphs(a.CFG, b.CFG);
        }

        // Для классов: сравнить список членов
        if (a.Type == CodeUnitType.Type && a.Structure != null && b.Structure != null)
        {
            return CompareStructures(a.Structure, b.Structure);
        }

        // Для остальных: базовое сравнение
        return 0.5f;
    }

    private float CompareControlFlowGraphs(ControlFlowGraph cfgA, ControlFlowGraph cfgB)
    {
        // Сравнить количество блоков, веток, циклов
        var blockSim = 1.0f - Math.Abs(cfgA.BlockCount - cfgB.BlockCount) /
            (float)Math.Max(cfgA.BlockCount, cfgB.BlockCount);

        var branchSim = 1.0f - Math.Abs(cfgA.BranchCount - cfgB.BranchCount) /
            (float)Math.Max(cfgA.BranchCount, cfgB.BranchCount);

        var loopSim = 1.0f - Math.Abs(cfgA.LoopCount - cfgB.LoopCount) /
            (float)Math.Max(cfgA.LoopCount, cfgB.LoopCount);

        return (blockSim + branchSim + loopSim) / 3.0f;
    }

    private float CompareStructures(CodeStructure a, CodeStructure b)
    {
        // Jaccard similarity на идентификаторах
        var intersection = a.Identifiers.Intersect(b.Identifiers).Count();
        var union = a.Identifiers.Union(b.Identifiers).Count();

        return union > 0 ? intersection / (float)union : 0.0f;
    }

    private SemanticMatchType DetermineMatchType(
        CodeUnit query,
        CodeUnit match,
        float similarity)
    {
        if (similarity >= 0.95f)
            return SemanticMatchType.NearIdentical;
        if (similarity >= 0.85f)
            return SemanticMatchType.Refactored;
        if (similarity >= 0.75f)
            return SemanticMatchType.Modified;
        return SemanticMatchType.WeakMatch;
    }
}

public sealed record SemanticMatchResult
{
    public required CodeUnit QueryUnit { get; init; }
    public required CodeUnit MatchedUnit { get; init; }
    public required float VectorSimilarity { get; init; }
    public required float StructuralSimilarity { get; init; }
    public required float CombinedScore { get; init; }
    public required SemanticMatchType MatchType { get; init; }
}

public enum SemanticMatchType
{
    NearIdentical,      // 95%+ - почти идентичен
    Refactored,         // 85-95% - рефакторинг
    Modified,           // 75-85% - модификация
    WeakMatch           // 70-75% - слабое совпадение
}
```

### 4. IntentClassifier - Определение намерений

```csharp
namespace UltrasharpTools.Tools.Merge.Analysis;

/// <summary>
/// Классифицирует изменения по намерениям (bug fix, refactoring, new feature).
/// Использует комбинацию heuristics и ML.
/// </summary>
public sealed class IntentClassifier
{
    private readonly ILogger<IntentClassifier> _logger;

    public IntentClassifier(ILogger<IntentClassifier>? logger = null)
    {
        _logger = logger ?? NullLogger<IntentClassifier>.Instance;
    }

    /// <summary>
    /// Определить намерение изменения.
    /// </summary>
    public async Task<ChangeIntent> ClassifyAsync(
        CodeUnit baseUnit,
        CodeUnit modifiedUnit,
        CancellationToken ct = default)
    {
        var intent = new ChangeIntent
        {
            Type = IntentType.Unknown,
            Description = "Unknown change",
            AffectedSymbols = new List<string>(),
            Confidence = 0.0f
        };

        // Heuristic 1: Только whitespace/comments изменились
        if (baseUnit.StructuralHash == modifiedUnit.StructuralHash)
        {
            intent = intent with
            {
                Type = IntentType.CodeCleanup,
                Description = "Formatting/comments changed",
                Confidence = 1.0f
            };
            return intent;
        }

        // Heuristic 2: CFG не изменился (для методов)
        if (baseUnit.CFG != null && modifiedUnit.CFG != null &&
            AreControlFlowsEquivalent(baseUnit.CFG, modifiedUnit.CFG))
        {
            intent = intent with
            {
                Type = IntentType.Refactoring,
                Description = "Internal refactoring (control flow preserved)",
                Confidence = 0.9f
            };
            return intent;
        }

        // Heuristic 3: Добавлены error handling блоки
        var addedTryCatch = CountTryCatchBlocks(modifiedUnit) >
            CountTryCatchBlocks(baseUnit);

        var addedValidation = CountValidationStatements(modifiedUnit) >
            CountValidationStatements(baseUnit);

        if (addedTryCatch || addedValidation)
        {
            intent = intent with
            {
                Type = IntentType.BugFix,
                Description = "Added error handling/validation",
                Confidence = 0.85f
            };
            return intent;
        }

        // Heuristic 4: Signature изменилась (API change)
        if (baseUnit.Signature != modifiedUnit.Signature)
        {
            intent = intent with
            {
                Type = IntentType.APIChange,
                Description = "Method signature changed",
                Confidence = 0.95f
            };
            return intent;
        }

        // Heuristic 5: Добавлены новые методы/классы
        if (baseUnit.ChildIds.Count < modifiedUnit.ChildIds.Count)
        {
            intent = intent with
            {
                Type = IntentType.FeatureAddition,
                Description = "New members added",
                Confidence = 0.8f
            };
            return intent;
        }

        // Heuristic 6: Оптимизация (меньше операций, но тот же результат)
        // TODO: более сложная эвристика

        // Default: generic modification
        intent = intent with
        {
            Type = IntentType.Modification,
            Description = "Code modified",
            Confidence = 0.6f
        };

        return intent;
    }

    private bool AreControlFlowsEquivalent(ControlFlowGraph a, ControlFlowGraph b)
    {
        // Упрощённая проверка: одинаковое количество путей выполнения
        return a.BlockCount == b.BlockCount &&
               a.BranchCount == b.BranchCount &&
               a.LoopCount == b.LoopCount;
    }

    private int CountTryCatchBlocks(CodeUnit unit)
    {
        return unit.Structure?.NormalizedAst.Split("try").Length - 1 ?? 0;
    }

    private int CountValidationStatements(CodeUnit unit)
    {
        var content = unit.Content.ToLower();
        var count = 0;

        if (content.Contains("throw") || content.Contains("argumentexception"))
            count++;
        if (content.Contains("if") && content.Contains("null"))
            count++;

        return count;
    }
}

public sealed record ChangeIntent
{
    public required IntentType Type { get; init; }
    public required string Description { get; init; }
    public required List<string> AffectedSymbols { get; init; }
    public required float Confidence { get; init; }
}

public enum IntentType
{
    Unknown,            // Неизвестно
    BugFix,             // Исправление бага
    Refactoring,        // Рефакторинг (не меняет поведение)
    FeatureAddition,    // Новая функциональность
    PerformanceOpt,     // Оптимизация производительности
    CodeCleanup,        // Форматирование, комментарии
    APIChange,          // Изменение API (signatures)
    Modification        // Общая модификация
}
```

## 📊 Этапы реализации (Roadmap)

### Phase 1: Foundation (2-3 дня)
**Цель**: Базовая инфраструктура

- [ ] `CodeUnit` model + `VersionedIndex`
- [ ] `ContentNormalizer` (encoding/BOM/line endings normalization)
- [ ] `StructuralFingerprint` (AST hashing)
- [ ] `FastPathMatcher` (hash/signature matching)
- [ ] `CSharpParser` (интеграция с Roslyn)
- [ ] Unit tests для базовых компонентов

**Результат**: Можем индексировать C# код и делать fast matching с корректной нормализацией

### Phase 2: Semantic Matching (3-4 дня)
**Цель**: Slow path через embeddings

- [ ] `LazyEmbeddingGenerator` (интеграция с существующим `EmbeddingGenerator`)
- [ ] `SemanticMatcher` (vector similarity)
- [ ] `MovementDetector` (обнаружение перемещений)
- [ ] `ControlFlowAnalyzer` (CFG comparison)
- [ ] Integration tests

**Результат**: Можем находить перемещённый/рефакторенный код

### Phase 3: Multi-Version Indexing (2-3 дня)
**Цель**: Индексация 4 версий

- [ ] `MultiVersionIndexer` (base, branchA, branchB, merged)
- [ ] Git integration (checkout веток, diff)
- [ ] `VersionManager` (управление версиями)
- [ ] `CacheManager` (кэш embeddings)

**Результат**: Можем индексировать все 4 состояния кода

### Phase 4: Merge Engine (4-5 дней)
**Цель**: Основной алгоритм мерджа

- [ ] `IntentClassifier` (определение намерений)
- [ ] `ConflictDetector` (поиск конфликтов)
- [ ] `ThreeWayMerger` (3-way merge алгоритм)
- [ ] `ConflictResolver` (предложения по разрешению)
- [ ] `SemanticMergeEngine` (orchestrator)

**Результат**: Можем делать intelligent merge

### Phase 5: Multi-Format Support (2-3 дня)
**Цель**: Поддержка JSON, XML и др.

- [ ] `ICodeParser` interface
- [ ] `JsonParser` (generic JSON)
- [ ] `SwaggerParser` (OpenAPI specific)
- [ ] `ParserFactory`
- [ ] Tests для разных форматов

**Результат**: Работаем не только с C#

### Phase 6: MCP Tools (1-2 дня)
**Цель**: API для использования

- [ ] `IndexMerge` - индексация веток
- [ ] `AnalyzeMerge` - анализ конфликтов
- [ ] `PerformMerge` - выполнение merge
- [ ] `ResolveMergeConflict` - разрешение конфликта
- [ ] Documentation

**Результат**: Готовые MCP инструменты

### Phase 7: Optimization & Polish (2-3 дня)
**Цель**: Производительность и UX

- [ ] Benchmarking (BenchmarkDotNet)
- [ ] Оптимизация embeddings (batch generation)
- [ ] Parallel processing
- [ ] Caching improvements
- [ ] Error handling & logging

**Результат**: Production-ready система

## 🎯 Пример использования (API)

```csharp
// 1. Создать semantic merge session
var session = await semanticMergeEngine.CreateSessionAsync(
    solutionPath: "D:/MyProject/MyProject.sln",
    baseBranch: "main",
    branchA: "feature/auth",
    branchB: "feature/payments"
);

// 2. Индексация всех версий
await session.IndexAllVersionsAsync();
// Output:
// - Indexed base (main): 1250 units
// - Indexed branchA: 1280 units (30 added)
// - Indexed branchB: 1265 units (15 added)

// 3. Анализ изменений
var analysis = await session.AnalyzeChangesAsync();
// Output:
// - Fast Path matched: 1200/1250 (96%)
// - Slow Path processed: 50/1250 (4%)
// - Detected movements: 12
// - Potential conflicts: 3

// 4. Выполнить merge
var mergeResult = await session.PerformMergeAsync(
    strategy: MergeStrategy.IntentPreserving
);

// 5. Обработать конфликты
foreach (var conflict in mergeResult.Conflicts)
{
    Console.WriteLine($"Conflict in {conflict.FilePath}:{conflict.LineNumber}");
    Console.WriteLine($"Type: {conflict.ConflictType}");

    // AI предлагает решения
    foreach (var resolution in conflict.SuggestedResolutions)
    {
        Console.WriteLine($"  Option {resolution.Priority}: {resolution.Description}");
    }
}

// 6. Применить выбранные решения
await session.ApplyResolutionsAsync(resolutions);

// 7. Создать merged commit
await session.CommitMergedVersionAsync("Semantic merge: feature/auth + feature/payments");
```

## 💡 Оптимизации

### 1. Incremental Indexing
Не индексировать весь код заново - только изменённые файлы.

```csharp
// Сохранить индекс на диск
await session.SaveIndexAsync("merge-cache/main.idx");

// При следующем запуске - загрузить
var cachedIndex = await LoadIndexAsync("merge-cache/main.idx");

// Инкрементальное обновление
var changedFiles = await gitService.GetChangedFilesAsync("main", "feature/auth");
await session.UpdateIndexAsync(cachedIndex, changedFiles);
```

### 2. Parallel Processing
Индексация и matching параллельно.

```csharp
// Параллельная индексация классов
await Parallel.ForEachAsync(classes, async (classUnit, ct) =>
{
    var embedding = await embeddingGen.GenerateAsync(classUnit, ct);
    await vectorStore.InsertAsync(embedding, ct);
});
```

### 3. Adaptive Embedding Dimension
Для малых кодовых баз - 384 dim (быстрее), для больших - 768 dim (точнее).

```csharp
var dimension = codeUnits.Count > 10000 ? 768 : 384;
var embeddingGen = new LazyEmbeddingGenerator(dimension);
```

## 🧪 Testing Strategy

```
Unit Tests:
├─ FastPathMatcher_Tests
├─ SemanticMatcher_Tests
├─ IntentClassifier_Tests
└─ ControlFlowAnalyzer_Tests

Integration Tests:
├─ MultiVersionIndexer_Tests
├─ SemanticMergeEngine_Tests
└─ EndToEnd_MergeScenarios_Tests

Benchmark Tests:
├─ IndexingPerformance_Benchmarks
├─ MatchingPerformance_Benchmarks
└─ EmbeddingGeneration_Benchmarks
```

## 📈 Метрики успеха

- **Fast Path Coverage**: >90% (большинство unit сопоставляются без embeddings)
- **Conflict Reduction**: 50-70% меньше конфликтов чем git merge
- **Accuracy**: >95% правильных предложений по разрешению
- **Performance**: Индексация <10 сек на 1000 методов
- **Memory**: <2GB RAM для крупных проектов (100K LOC)

---

**Итого**: ~20-25 дней разработки для полной реализации всех фаз.

# Рекомендации по нормализации для существующей инфраструктуры SharpTools

## 🎯 Текущая ситуация

### ✅ Что работает хорошо
- **C# код через Roslyn**: Автоматическая нормализация encoding/line endings
- **CodeSemanticIndexer**: Использует Roslyn → нет проблем

### ⚠️ Где нужны улучшения
- **Raw file operations**: `DocumentOperationsService.ReadFileAsync`
- **Non-C# файлы**: JSON, TXT, config, и т.д.
- **Semantic comparison**: Нужна консистентная нормализация

---

## 🔧 Краткосрочное решение (Quick Win)

### 1. Обновить EmbeddingGenerator cache key

**Проблема**: Текущий cache key не учитывает line endings

```csharp
// СЕЙЧАС (примитивная нормализация)
private string GetCacheKey(string text)
{
    return _config.NormalizeTextForCache
        ? text.Trim().ToLowerInvariant()
        : text;
}
```

**РЕШЕНИЕ**: Добавить нормализацию line endings

```csharp
// УЛУЧШЕННАЯ версия
private string GetCacheKey(string text)
{
    if (!_config.NormalizeTextForCache)
        return text;

    // 1. Trim whitespace
    text = text.Trim();

    // 2. Normalize line endings (CR/LF → LF)
    text = text.Replace("\r\n", "\n").Replace("\r", "\n");

    // 3. Normalize to lowercase (опционально)
    if (_config.NormalizeCaseForCache)
    {
        text = text.ToLowerInvariant();
    }

    return text;
}
```

**Конфигурация**:
```csharp
public sealed record EmbeddingGeneratorConfig
{
    public bool NormalizeTextForCache { get; init; } = true;
    public bool NormalizeCaseForCache { get; init; } = true;  // NEW
}
```

---

### 2. Создать FileNormalizer utility

**Для use cases где читаются raw файлы**

```csharp
// UltrasharpTools.Tools/Infrastructure/FileNormalizer.cs
namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Утилита для нормализации содержимого файлов.
/// Используется для raw file operations (не C# через Roslyn).
/// </summary>
public static class FileNormalizer
{
    private static readonly Encoding TargetEncoding = new UTF8Encoding(encoderShouldEmitBOM: false);

    /// <summary>
    /// Прочитать и нормализовать файл.
    /// </summary>
    public static async Task<string> ReadNormalizedAsync(
        string filePath,
        CancellationToken ct = default)
    {
        // 1. Прочитать raw bytes
        var bytes = await File.ReadAllBytesAsync(filePath, ct);

        // 2. Определить encoding
        var encoding = DetectEncoding(bytes, out var hasBom);

        // 3. Декодировать
        var content = encoding.GetString(bytes);

        // 4. Удалить BOM если есть
        if (hasBom && content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content.Substring(1);
        }

        // 5. Нормализовать line endings
        content = NormalizeLineEndings(content);

        return content;
    }

    /// <summary>
    /// Определить encoding файла (BOM detection).
    /// </summary>
    private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
    {
        hasBom = false;

        if (bytes.Length < 2)
            return Encoding.UTF8;

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

        // Default: UTF-8 без BOM
        return Encoding.UTF8;
    }

    /// <summary>
    /// Нормализовать line endings: CR/LF, CR → LF.
    /// </summary>
    private static string NormalizeLineEndings(string content)
    {
        // Replace CR/LF → LF
        content = content.Replace("\r\n", "\n");

        // Replace CR → LF
        content = content.Replace("\r", "\n");

        return content;
    }

    /// <summary>
    /// Записать нормализованный файл.
    /// </summary>
    public static async Task WriteNormalizedAsync(
        string filePath,
        string content,
        CancellationToken ct = default)
    {
        // Всегда используем UTF-8 без BOM
        var bytes = TargetEncoding.GetBytes(content);
        await File.WriteAllBytesAsync(filePath, bytes, ct);
    }
}
```

---

### 3. Обновить DocumentOperationsService

```csharp
// БЫЛО
public async Task<(string contents, int lines)> ReadFileAsync(...)
{
    string content = await File.ReadAllTextAsync(filePath, cancellationToken);
    // ...
}

// СТАЛО
public async Task<(string contents, int lines)> ReadFileAsync(...)
{
    // Используем FileNormalizer для raw файлов
    string content = await FileNormalizer.ReadNormalizedAsync(filePath, cancellationToken);

    // Line endings уже нормализованы, split просто по \n
    var lines = content.Split('\n', StringSplitOptions.None);

    // ...
}
```

---

## 📊 Долгосрочное решение (для Semantic Merge)

Для новой **Semantic Merge** системы используйте полноценный `ContentNormalizer` из дизайна:

```csharp
// UltrasharpTools.Tools/Merge/Indexing/ContentNormalizer.cs
public sealed class ContentNormalizer
{
    // Полная реализация из SEMANTIC_MERGE_DESIGN.md
    // Включает:
    // - BOM detection и удаление
    // - Encoding detection (UTF-8, UTF-16, UTF-32)
    // - Line endings normalization
    // - Trailing whitespace cleanup
    // - Configurable behavior
}
```

**Использование**:
```csharp
// При индексации файлов для merge
var normalizer = new ContentNormalizer();
var normalized = await normalizer.NormalizeAsync(filePath);

// Compute hash на нормализованном контенте
var hash = ContentNormalizer.ComputeContentHash(normalized.Content);

// Теперь hash стабилен независимо от encoding/line endings
```

---

## 🧪 Тестирование

### Test Case 1: Encoding variations
```csharp
[Test]
public async Task SameContent_DifferentEncoding_ShouldHaveSameHash()
{
    // Arrange
    var content = "public void Test() { }";

    // File 1: UTF-8 with BOM
    await File.WriteAllBytesAsync("file1.cs",
        new byte[] { 0xEF, 0xBB, 0xBF }
        .Concat(Encoding.UTF8.GetBytes(content))
        .ToArray());

    // File 2: UTF-8 without BOM
    await File.WriteAllBytesAsync("file2.cs",
        Encoding.UTF8.GetBytes(content));

    // Act
    var normalized1 = await FileNormalizer.ReadNormalizedAsync("file1.cs");
    var normalized2 = await FileNormalizer.ReadNormalizedAsync("file2.cs");

    // Assert
    Assert.Equal(normalized1, normalized2);
}
```

### Test Case 2: Line ending variations
```csharp
[Test]
public async Task SameContent_DifferentLineEndings_ShouldMatch()
{
    // Arrange
    var content = "line1\nline2\nline3";

    // File 1: Windows (CR/LF)
    await File.WriteAllTextAsync("file1.txt", content.Replace("\n", "\r\n"));

    // File 2: Unix (LF)
    await File.WriteAllTextAsync("file2.txt", content);

    // File 3: Old Mac (CR)
    await File.WriteAllTextAsync("file3.txt", content.Replace("\n", "\r"));

    // Act
    var norm1 = await FileNormalizer.ReadNormalizedAsync("file1.txt");
    var norm2 = await FileNormalizer.ReadNormalizedAsync("file2.txt");
    var norm3 = await FileNormalizer.ReadNormalizedAsync("file3.txt");

    // Assert
    Assert.Equal(norm1, norm2);
    Assert.Equal(norm2, norm3);
}
```

---

## ✅ Checklist для внедрения

### Phase 1: Quick wins (1 день)
- [ ] Обновить `EmbeddingGenerator.GetCacheKey()` - нормализация line endings
- [ ] Создать `FileNormalizer` utility класс
- [ ] Добавить unit tests для нормализации
- [ ] Обновить `DocumentOperationsService.ReadFileAsync()`

### Phase 2: Semantic Merge support (в рамках Merge feature)
- [ ] Создать полноценный `ContentNormalizer` (из дизайна)
- [ ] Интегрировать в `CodeUnit` model
- [ ] Использовать в `FastPathMatcher`
- [ ] Integration tests

### Phase 3: Документация
- [ ] Обновить README с рекомендациями по encoding
- [ ] Добавить в CLAUDE.md примечание про нормализацию
- [ ] Документировать best practices

---

## 📌 Рекомендации

### Для C# кода
✅ **Продолжайте использовать Roslyn** - автоматическая нормализация работает отлично

### Для JSON/config файлов
⚠️ **Используйте FileNormalizer** при raw file operations

### Для Semantic Merge
🎯 **Обязательно используйте ContentNormalizer** перед любым сравнением/хэшированием

### Для существующего кода
📝 **Постепенная миграция**:
1. Сначала quick wins (EmbeddingGenerator cache)
2. Затем критичные места (DocumentOperationsService)
3. Новые фичи (Semantic Merge) - с нуля правильно

---

## 🔗 См. также

- `SEMANTIC_MERGE_DESIGN.md` - полная спецификация ContentNormalizer
- `CLAUDE.md` - project guidelines
- [C# Encoding Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/base-types/best-practices)

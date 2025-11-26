using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Layered;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Ленивая генерация embeddings для CodeUnits.
/// Генерирует embeddings только когда Fast Path не смог найти match.
/// Это экономит ~90% вычислительных ресурсов.
/// </summary>
public sealed class LazyEmbeddingGenerator {
    private readonly EmbeddingGenerator? _embeddingGenerator;
    private readonly ILogger<LazyEmbeddingGenerator> _logger;
    private readonly int _maxTextLength;

    /// <summary>
    /// Дефолтная длина если провайдер не указал (fallback для ~512 токенов).
    /// </summary>
    private const int DefaultMaxTextLength = 2000;

    /// <summary>
    /// Символов на токен (приблизительно, для кода обычно ~3-4 символа на токен).
    /// </summary>
    private const double CharsPerToken = 3.5;

    /// <summary>
    /// Returns true if embedding generation is available (EmbeddingGenerator is configured).
    /// When false, SemanticMerge will work in Fast Path only mode (no semantic similarity matching).
    /// </summary>
    public bool IsAvailable => _embeddingGenerator != null;

    /// <summary>
    /// Максимальная длина текста для embedding (в символах).
    /// </summary>
    public int MaxTextLength => _maxTextLength;

    public LazyEmbeddingGenerator(
        EmbeddingGenerator? embeddingGenerator,
        ILogger<LazyEmbeddingGenerator>? logger = null
    ) {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger ?? NullLogger<LazyEmbeddingGenerator>.Instance;

        // Получаем MaxTokens из провайдера и конвертируем в символы
        if (_embeddingGenerator != null) {
            var providerInfo = _embeddingGenerator.GetMetrics().ProviderInfo;
            _maxTextLength = (int)(providerInfo.MaxTokens * CharsPerToken);
            _logger.LogDebug(
                "[EMBED] Provider {Provider} max tokens: {MaxTokens}, using max text length: {MaxChars} chars",
                providerInfo.Name, providerInfo.MaxTokens, _maxTextLength
            );
        } else {
            _maxTextLength = DefaultMaxTextLength;
            _logger.LogWarning(
                "LazyEmbeddingGenerator initialized without EmbeddingGenerator. " +
                "SemanticMerge will work in Fast Path only mode (no semantic similarity matching)."
            );
        }
    }

    /// <summary>
    /// Сгенерировать embeddings для списка CodeUnits.
    /// </summary>
    public async Task<List<CodeUnit>> GenerateEmbeddingsAsync(
        List<CodeUnit> units,
        CancellationToken ct = default
    ) {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogDebug("[EMBED] === GenerateEmbeddingsAsync started for {Count} units ===", units.Count);

        // Check if embedding generator is available
        if (_embeddingGenerator == null) {
            _logger.LogWarning("[EMBED] EmbeddingGenerator not available - returning units without embeddings");
            return units;
        }

        _logger.LogDebug("[EMBED] Generating embeddings for {Count} units", units.Count);

        var updatedUnits = new List<CodeUnit>();
        var generated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var unit in units) {
            ct.ThrowIfCancellationRequested();
            try {
                // Если embedding уже есть - пропустить
                if (unit.Embedding != null) {
                    updatedUnits.Add(unit);
                    skipped++;
                    continue;
                }

                // Генерировать embedding
                _logger.LogDebug("[EMBED] Generating embedding for unit {Id} ({Type})...", unit.Id, unit.Type);
                var embedding = await GenerateEmbeddingForUnitAsync(unit, ct);

                // Обновить unit
                var updatedUnit = unit with {
                    Embedding = embedding,
                };
                updatedUnits.Add(updatedUnit);
                if (embedding != null)
                    generated++;
                else
                    skipped++;

                if (generated % 10 == 0 && generated > 0) {
                    _logger.LogDebug("[EMBED] Progress: {Generated} generated, {Skipped} skipped, {Failed} failed", generated, skipped, failed);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "[EMBED] Failed to generate embedding for unit {Id}", unit.Id);
                failed++;

                // Добавить без embedding
                updatedUnits.Add(unit);
            }
        }

        sw.Stop();
        _logger.LogDebug(
            "[EMBED] === GenerateEmbeddingsAsync completed in {Ms}ms: {Generated} generated, {Skipped} skipped, {Failed} failed ===",
            sw.ElapsedMilliseconds, generated, skipped, failed
        );

        return updatedUnits;
    }

    /// <summary>
    /// Сгенерировать embedding для одного CodeUnit.
    /// </summary>
    private async Task<float[]?> GenerateEmbeddingForUnitAsync(CodeUnit unit, CancellationToken ct) {
        // Check if embedding generator is available
        if (_embeddingGenerator == null) {
            return null;
        }

        // Выбрать текст для embedding
        var textToEmbed = SelectTextForEmbedding(unit);

        if (string.IsNullOrWhiteSpace(textToEmbed)) {
            _logger.LogDebug("No text to embed for unit {Id}", unit.Id);
            return null;
        }

        // Генерировать embedding
        var embedding = await _embeddingGenerator.EmbedAsync(textToEmbed, ct);

        return embedding;
    }

    /// <summary>
    /// Выбрать текст для embedding в зависимости от типа unit.
    /// </summary>
    private string SelectTextForEmbedding(CodeUnit unit) {
        var text = unit.Type switch {
            CodeUnitType.File =>
                // Для файлов - не генерируем embedding (слишком большой)
                string.Empty,

            CodeUnitType.Namespace =>
                // Для namespace - имя + список типов
                $"{unit.Name} {string.Join(" ", unit.ChildIds)}",

            CodeUnitType.Type =>
                // Для типов - signature + имена членов
                $"{unit.Signature} {(unit.ChildIds.Count > 0 ? $"members: {string.Join(", ", unit.ChildIds.Take(10))}" : "")}",

            CodeUnitType.Method =>
                // Для методов - signature + тело (будет обрезано)
                $"{unit.Signature}\n{unit.Content}",

            CodeUnitType.Property or CodeUnitType.Field =>
                // Для свойств/полей - signature
                unit.Signature ?? unit.Content,

            CodeUnitType.Block =>
                // Для блоков - контент
                unit.Content,

            CodeUnitType.JsonObject or CodeUnitType.JsonArray or CodeUnitType.JsonProperty =>
                // Для JSON - signature + структура
                $"{unit.Signature}\n{unit.Content}",

            _ => unit.Content
        };

        return TruncateText(text, _maxTextLength);
    }

    /// <summary>
    /// Обрезать текст до максимальной длины, сохраняя целостность.
    /// Приоритет: signature (начало) + конец контента.
    /// </summary>
    private static string TruncateText(string text, int maxLength) {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Ищем разделитель signature/content (первый перевод строки)
        var signatureEnd = text.IndexOf('\n');
        if (signatureEnd > 0 && signatureEnd < maxLength / 2) {
            // Сохраняем signature полностью + обрезаем content
            var signature = text[..(signatureEnd + 1)];
            var content = text[(signatureEnd + 1)..];
            var remainingLength = maxLength - signature.Length - 10; // -10 для "... [truncated]"

            if (remainingLength > 100) {
                // Берём начало и конец content для лучшего семантического покрытия
                var halfLength = remainingLength / 2;
                var contentStart = content[..Math.Min(halfLength, content.Length)];
                var contentEnd = content.Length > halfLength
                    ? content[^Math.Min(halfLength, content.Length - halfLength)..]
                    : "";

                return $"{signature}{contentStart}...{contentEnd}";
            }
        }

        // Простая обрезка с многоточием
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Обновить VersionedIndex с embeddings.
    /// </summary>
    public async Task<VersionedIndex> EnrichIndexWithEmbeddingsAsync(
        VersionedIndex index,
        List<CodeUnit> unitsNeedingEmbeddings,
        CancellationToken ct = default
    ) {
        _logger.LogDebug(
            "Enriching index {Version} with embeddings for {Count} units",
            index.Version,
            unitsNeedingEmbeddings.Count
        );

        // Генерировать embeddings
        var enrichedUnits = await GenerateEmbeddingsAsync(unitsNeedingEmbeddings, ct);

        // Обновить units в индексе
        var updatedUnits = new Dictionary<string, CodeUnit>(index.Units);
        foreach (var enrichedUnit in enrichedUnits) {
            updatedUnits[enrichedUnit.Id] = enrichedUnit;
        }

        // Обновить VectorStore (если инициализирован)
        var updatedVectorStore = index.VectorStore;
        foreach (var unit in enrichedUnits.Where(u => u.Embedding != null)) {
            // VectorStore требует инициализации, пока пропускаем
            // TODO: Инициализировать VectorStore перед использованием
            // var vectorEmbedding = new VectorEmbedding
            // {
            //     Id = unit.Id,
            //     Content = unit.Content,
            //     Vector = unit.Embedding!,
            //     Dimension = unit.Embedding!.Length
            // };
            // await updatedVectorStore.InsertAsync(vectorEmbedding, ct);
        }

        // Обновить статистику
        var unitsWithEmbeddings = updatedUnits.Values.Count(u => u.Embedding != null);
        var updatedStats = index.Statistics with { UnitsWithEmbeddings = unitsWithEmbeddings };

        return index with {
            Units = updatedUnits,
            VectorStore = updatedVectorStore,
            Statistics = updatedStats,
        };
    }

    /// <summary>
    /// Batch генерация embeddings (более эффективно).
    /// </summary>
    public async Task<List<CodeUnit>> GenerateEmbeddingsBatchAsync(
        List<CodeUnit> units,
        int batchSize = 32,
        CancellationToken ct = default
    ) {
        // Check if embedding generator is available
        if (_embeddingGenerator == null) {
            _logger.LogDebug("EmbeddingGenerator not available, returning units without embeddings");
            return units;
        }

        _logger.LogDebug("Generating embeddings in batches of {BatchSize}", batchSize);

        var updatedUnits = new List<CodeUnit>();
        var batches = units.Where(u => u.Embedding == null).Chunk(batchSize);

        foreach (var batch in batches) {
            var texts = batch
                .Select(SelectTextForEmbedding)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (texts.Count == 0) {
                updatedUnits.AddRange(batch);
                continue;
            }

            try {
                // TODO: Batch embedding generation если поддерживается провайдером
                // Пока генерируем по одному
                var embeddings = new List<float[]>();
                foreach (var text in texts) {
                    var embedding = await _embeddingGenerator.EmbedAsync(text, ct);
                    embeddings.Add(embedding);
                }

                // Обновить units
                for (int i = 0; i < batch.Length; i++) {
                    var unit = batch[i];
                    var embedding = i < embeddings.Count ? embeddings[i] : null;

                    updatedUnits.Add(unit with { Embedding = embedding });
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to generate embeddings for batch");

                updatedUnits.AddRange(batch);
            }
        }

        // Добавить units, которые уже имели embeddings
        updatedUnits.AddRange(units.Where(u => u.Embedding != null));

        return updatedUnits;
    }
}

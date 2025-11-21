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
public sealed class LazyEmbeddingGenerator
{
    private readonly EmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<LazyEmbeddingGenerator> _logger;

    public LazyEmbeddingGenerator(
        EmbeddingGenerator embeddingGenerator,
        ILogger<LazyEmbeddingGenerator>? logger = null
    )
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger ?? NullLogger<LazyEmbeddingGenerator>.Instance;
    }

    /// <summary>
    /// Сгенерировать embeddings для списка CodeUnits.
    /// </summary>
    public async Task<List<CodeUnit>> GenerateEmbeddingsAsync(
        List<CodeUnit> units,
        CancellationToken ct = default
    )
    {
        _logger.LogInformation("Generating embeddings for {Count} units", units.Count);

        var updatedUnits = new List<CodeUnit>();

        foreach (var unit in units)
        {
            try
            {
                // Если embedding уже есть - пропустить
                if (unit.Embedding != null)
                {
                    updatedUnits.Add(unit);
                    continue;
                }

                // Генерировать embedding
                var embedding = await GenerateEmbeddingForUnitAsync(unit, ct);

                // Обновить unit
                var updatedUnit = unit with
                {
                    Embedding = embedding,
                };
                updatedUnits.Add(updatedUnit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for unit {Id}", unit.Id);

                // Добавить без embedding
                updatedUnits.Add(unit);
            }
        }

        _logger.LogInformation(
            "Generated {Count} embeddings",
            updatedUnits.Count(u => u.Embedding != null)
        );

        return updatedUnits;
    }

    /// <summary>
    /// Сгенерировать embedding для одного CodeUnit.
    /// </summary>
    private async Task<float[]?> GenerateEmbeddingForUnitAsync(CodeUnit unit, CancellationToken ct)
    {
        // Выбрать текст для embedding
        var textToEmbed = SelectTextForEmbedding(unit);

        if (string.IsNullOrWhiteSpace(textToEmbed))
        {
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
    private string SelectTextForEmbedding(CodeUnit unit)
    {
        switch (unit.Type)
        {
            case CodeUnitType.File:
                // Для файлов - не генерируем embedding (слишком большой)
                return string.Empty;

            case CodeUnitType.Namespace:
                // Для namespace - имя + список типов
                return $"{unit.Name} {string.Join(" ", unit.ChildIds)}";

            case CodeUnitType.Type:
                // Для типов - signature + имена членов
                var members = unit.ChildIds.Count > 0 ? $"members: {string.Join(", ", unit.ChildIds.Take(10))}"
                    : "";
                return $"{unit.Signature} {members}";

            case CodeUnitType.Method:
                // Для методов - signature + тело
                return $"{unit.Signature}\n{unit.Content}";

            case CodeUnitType.Property:
            case CodeUnitType.Field:
                // Для свойств/полей - signature
                return unit.Signature ?? unit.Content;

            case CodeUnitType.Block:
                // Для блоков - весь контент
                return unit.Content;

            case CodeUnitType.JsonObject:
            case CodeUnitType.JsonArray:
            case CodeUnitType.JsonProperty:
                // Для JSON - signature + структура
                return $"{unit.Signature}\n{unit.Content.Substring(0, Math.Min(500, unit.Content.Length))}";

            default:
                return unit.Content;
        }
    }

    /// <summary>
    /// Обновить VersionedIndex с embeddings.
    /// </summary>
    public async Task<VersionedIndex> EnrichIndexWithEmbeddingsAsync(
        VersionedIndex index,
        List<CodeUnit> unitsNeedingEmbeddings,
        CancellationToken ct = default
    )
    {
        _logger.LogInformation(
            "Enriching index {Version} with embeddings for {Count} units",
            index.Version,
            unitsNeedingEmbeddings.Count
        );

        // Генерировать embeddings
        var enrichedUnits = await GenerateEmbeddingsAsync(unitsNeedingEmbeddings, ct);

        // Обновить units в индексе
        var updatedUnits = new Dictionary<string, CodeUnit>(index.Units);
        foreach (var enrichedUnit in enrichedUnits)
        {
            updatedUnits[enrichedUnit.Id] = enrichedUnit;
        }

        // Обновить VectorStore (если инициализирован)
        var updatedVectorStore = index.VectorStore;
        foreach (var unit in enrichedUnits.Where(u => u.Embedding != null))
        {
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

        return index with
        {
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
    )
    {
        _logger.LogInformation("Generating embeddings in batches of {BatchSize}", batchSize);

        var updatedUnits = new List<CodeUnit>();
        var batches = units.Where(u => u.Embedding == null).Chunk(batchSize);

        foreach (var batch in batches)
        {
            var texts = batch
                .Select(SelectTextForEmbedding)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (texts.Count == 0)
            {
                updatedUnits.AddRange(batch);
                continue;
            }

            try
            {
                // TODO: Batch embedding generation если поддерживается провайдером
                // Пока генерируем по одному
                var embeddings = new List<float[]>();
                foreach (var text in texts)
                {
                    var embedding = await _embeddingGenerator.EmbedAsync(text, ct);
                    embeddings.Add(embedding);
                }

                // Обновить units
                for (int i = 0; i < batch.Length; i++)
                {
                    var unit = batch[i];
                    var embedding = i < embeddings.Count ? embeddings[i] : null;

                    updatedUnits.Add(unit with { Embedding = embedding });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embeddings for batch");

                updatedUnits.AddRange(batch);
            }
        }

        // Добавить units, которые уже имели embeddings
        updatedUnits.AddRange(units.Where(u => u.Embedding != null));

        return updatedUnits;
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Engine;

/// <summary>
/// Семантическое разрешение конфликтов.
/// Когда Fast Path находит unit в обеих ветках, но контент разный -
/// пытается разрешить конфликт через анализ намерений изменений.
/// </summary>
public sealed class SemanticConflictResolver
{
    private readonly LazyEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<SemanticConflictResolver> _logger;

    /// <summary>
    /// Минимальная схожесть для auto-merge (если изменения очень похожи - берём любое).
    /// </summary>
    private const float SimilarityThresholdForAutoMerge = 0.95f;

    /// <summary>
    /// Порог для определения "ортогональных" изменений (можно объединить).
    /// </summary>
    private const float OrthogonalityThreshold = 0.3f;

    /// <summary>
    /// Разделители строк для Split.
    /// </summary>
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    public SemanticConflictResolver(
        LazyEmbeddingGenerator embeddingGenerator,
        ILogger<SemanticConflictResolver>? logger = null)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger ?? NullLogger<SemanticConflictResolver>.Instance;
    }

    /// <summary>
    /// Попытаться разрешить конфликт семантически.
    /// </summary>
    /// <param name="baseUnit">Базовая версия (общий предок)</param>
    /// <param name="unitA">Версия из ветки A (source)</param>
    /// <param name="unitB">Версия из ветки B (target)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Результат разрешения: Resolved с merged content, или Conflict</returns>
    public async Task<ConflictResolutionResult> TryResolveAsync(
        CodeUnit baseUnit,
        CodeUnit unitA,
        CodeUnit unitB,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[RESOLVE] Attempting to resolve conflict for {Symbol}",
            baseUnit.FullyQualifiedName);

        // 1. Проверить тривиальные случаи
        var trivialResult = TryResolveTrivial(baseUnit, unitA, unitB);
        if (trivialResult != null)
        {
            _logger.LogDebug("[RESOLVE] Resolved trivially: {Reason}", trivialResult.Resolution);
            return trivialResult;
        }

        // 2. Анализ на уровне строк (line-based diff)
        var lineBasedResult = TryResolveByLineDiff(baseUnit, unitA, unitB);
        if (lineBasedResult != null)
        {
            _logger.LogDebug("[RESOLVE] Resolved by line diff: {Reason}", lineBasedResult.Resolution);
            return lineBasedResult;
        }

        // 3. Семантический анализ через embeddings
        if (_embeddingGenerator.IsAvailable)
        {
            var semanticResult = await TryResolveBySemanticAnalysisAsync(baseUnit, unitA, unitB, ct);
            if (semanticResult != null)
            {
                _logger.LogDebug("[RESOLVE] Resolved semantically: {Reason}", semanticResult.Resolution);
                return semanticResult;
            }
        }
        else
        {
            _logger.LogTrace("[RESOLVE] Embeddings not available, skipping semantic analysis");
        }

        // 4. Не удалось разрешить - возвращаем конфликт
        _logger.LogDebug("[RESOLVE] Could not resolve conflict for {Symbol}", baseUnit.FullyQualifiedName);
        return ConflictResolutionResult.Unresolved(baseUnit, unitA, unitB,
            "Both branches modified the same code in incompatible ways");
    }

    /// <summary>
    /// Тривиальные случаи разрешения.
    /// </summary>
    private ConflictResolutionResult? TryResolveTrivial(CodeUnit baseUnit, CodeUnit unitA, CodeUnit unitB)
    {
        // Случай 1: A и B идентичны (оба сделали одинаковое изменение)
        if (unitA.ContentHash == unitB.ContentHash)
        {
            return ConflictResolutionResult.Resolved(unitA, "BothIdentical",
                "Both branches made identical changes");
        }

        // Случай 2: A не изменил (вернулся к base или не трогал)
        if (unitA.ContentHash == baseUnit.ContentHash)
        {
            return ConflictResolutionResult.Resolved(unitB, "OnlyBChanged",
                "Only branch B modified this code");
        }

        // Случай 3: B не изменил
        if (unitB.ContentHash == baseUnit.ContentHash)
        {
            return ConflictResolutionResult.Resolved(unitA, "OnlyAChanged",
                "Only branch A modified this code");
        }

        return null;
    }

    /// <summary>
    /// Разрешение через анализ построчных изменений.
    /// Если изменения в разных строках - можно объединить.
    /// </summary>
    private ConflictResolutionResult? TryResolveByLineDiff(CodeUnit baseUnit, CodeUnit unitA, CodeUnit unitB)
    {
        var baseLines = SplitLines(baseUnit.Content);
        var linesA = SplitLines(unitA.Content);
        var linesB = SplitLines(unitB.Content);

        // Найти изменённые строки в каждой ветке
        var changesA = FindChangedLineRanges(baseLines, linesA);
        var changesB = FindChangedLineRanges(baseLines, linesB);

        _logger.LogTrace("[RESOLVE] Line changes: A={CountA} ranges, B={CountB} ranges",
            changesA.Count, changesB.Count);

        // Проверить пересечения
        if (!RangesOverlap(changesA, changesB))
        {
            // Изменения не пересекаются - можно объединить!
            var merged = MergeNonOverlappingChanges(baseLines, linesA, linesB, changesA, changesB);
            if (merged != null)
            {
                var mergedUnit = unitA with { Content = merged, ContentHash = ComputeHash(merged) };
                return ConflictResolutionResult.Resolved(mergedUnit, "NonOverlappingChanges",
                    $"Changes in different lines: A modified lines {FormatRanges(changesA)}, B modified lines {FormatRanges(changesB)}");
            }
        }

        return null;
    }

    /// <summary>
    /// Семантический анализ через embeddings.
    /// </summary>
    private async Task<ConflictResolutionResult?> TryResolveBySemanticAnalysisAsync(
        CodeUnit baseUnit, CodeUnit unitA, CodeUnit unitB, CancellationToken ct)
    {
        _logger.LogTrace("[RESOLVE] Starting semantic analysis for {Symbol}", baseUnit.FullyQualifiedName);

        // Генерируем embeddings для всех версий
        var unitsToEmbed = new List<CodeUnit> { baseUnit, unitA, unitB };
        var enrichedUnits = await _embeddingGenerator.GenerateEmbeddingsAsync(unitsToEmbed, ct);

        var baseEmb = enrichedUnits.FirstOrDefault(u => u.Id == baseUnit.Id)?.Embedding;
        var embA = enrichedUnits.FirstOrDefault(u => u.Id == unitA.Id)?.Embedding;
        var embB = enrichedUnits.FirstOrDefault(u => u.Id == unitB.Id)?.Embedding;

        if (baseEmb == null || embA == null || embB == null)
        {
            _logger.LogTrace("[RESOLVE] Could not generate embeddings for all versions");
            return null;
        }

        // Вычисляем similarity
        var simAB = CosineSimilarity(embA, embB);
        var simBaseA = CosineSimilarity(baseEmb, embA);
        var simBaseB = CosineSimilarity(baseEmb, embB);

        _logger.LogTrace("[RESOLVE] Similarities: A-B={SimAB:F3}, Base-A={SimBA:F3}, Base-B={SimBB:F3}",
            simAB, simBaseA, simBaseB);

        // Случай 1: A и B семантически почти идентичны
        if (simAB >= SimilarityThresholdForAutoMerge)
        {
            // Берём более "полную" версию (ту что больше отличается от base)
            var takeA = simBaseA < simBaseB; // A более изменённый
            var chosen = takeA ? unitA : unitB;
            return ConflictResolutionResult.Resolved(chosen, "SemanticallySimilar",
                $"Semantically similar changes (similarity={simAB:F3}), taking {(takeA ? "A" : "B")}");
        }

        // Случай 2: Изменения "ортогональны" - одно добавляет функционал, другое рефакторит
        var changeVectorA = SubtractVectors(embA, baseEmb);
        var changeVectorB = SubtractVectors(embB, baseEmb);
        var changeOrthogonality = 1.0f - Math.Abs(CosineSimilarity(changeVectorA, changeVectorB));

        _logger.LogTrace("[RESOLVE] Change orthogonality: {Orth:F3}", changeOrthogonality);

        if (changeOrthogonality >= OrthogonalityThreshold)
        {
            // Попробовать объединить изменения
            var mergedContent = TryMergeOrthogonalChanges(baseUnit.Content, unitA.Content, unitB.Content);
            if (mergedContent != null)
            {
                var mergedUnit = unitA with { Content = mergedContent, ContentHash = ComputeHash(mergedContent) };
                return ConflictResolutionResult.Resolved(mergedUnit, "OrthogonalChanges",
                    $"Orthogonal changes detected (orthogonality={changeOrthogonality:F3}), merged both");
            }
        }

        return null;
    }

    /// <summary>
    /// Попытка объединить ортогональные изменения.
    /// </summary>
    private string? TryMergeOrthogonalChanges(string baseContent, string contentA, string contentB)
    {
        var baseLines = SplitLines(baseContent);
        var linesA = SplitLines(contentA);
        var linesB = SplitLines(contentB);

        // Найти добавленные строки в начале и конце для каждой ветки
        var prefixA = FindAddedPrefix(baseLines, linesA);
        var suffixA = FindAddedSuffix(baseLines, linesA);
        var prefixB = FindAddedPrefix(baseLines, linesB);
        var suffixB = FindAddedSuffix(baseLines, linesB);

        // Если A добавил в начало, B в конец (или наоборот) - объединяем
        if ((prefixA.Count > 0 && suffixB.Count > 0 && prefixB.Count == 0 && suffixA.Count == 0) ||
            (prefixB.Count > 0 && suffixA.Count > 0 && prefixA.Count == 0 && suffixB.Count == 0))
        {
            var merged = new List<string>();
            merged.AddRange(prefixA);
            merged.AddRange(prefixB);
            merged.AddRange(baseLines);
            merged.AddRange(suffixA);
            merged.AddRange(suffixB);
            return string.Join("\n", merged);
        }

        return null;
    }

    #region Helper Methods

    private static string[] SplitLines(string content)
    {
        return content.Split(LineSeparators, StringSplitOptions.None);
    }

    private List<(int Start, int End)> FindChangedLineRanges(string[] baseLines, string[] newLines)
    {
        var ranges = new List<(int Start, int End)>();

        int i = 0, j = 0;
        while (i < baseLines.Length && j < newLines.Length)
        {
            if (baseLines[i] == newLines[j])
            {
                i++;
                j++;
            }
            else
            {
                int startBase = i;

                while (i < baseLines.Length && j < newLines.Length && baseLines[i] != newLines[j])
                {
                    if (i + 1 < baseLines.Length && baseLines[i + 1] == newLines[j])
                    {
                        i++;
                    }
                    else if (j + 1 < newLines.Length && baseLines[i] == newLines[j + 1])
                    {
                        j++;
                    }
                    else
                    {
                        i++;
                        j++;
                    }
                }

                if (startBase != i)
                {
                    ranges.Add((startBase, i));
                }
            }
        }

        if (i < baseLines.Length)
        {
            ranges.Add((i, baseLines.Length));
        }

        return ranges;
    }

    private static bool RangesOverlap(List<(int Start, int End)> rangesA, List<(int Start, int End)> rangesB)
    {
        foreach (var a in rangesA)
        {
            foreach (var b in rangesB)
            {
                if (a.Start < b.End && b.Start < a.End)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private string? MergeNonOverlappingChanges(
        string[] baseLines,
        string[] linesA,
        string[] linesB,
        List<(int Start, int End)> changesA,
        List<(int Start, int End)> changesB)
    {
        try
        {
            var result = new List<string>(baseLines);

            var allChanges = new List<(int Start, int End, string[] NewLines, string Source)>();

            foreach (var (start, end) in changesA)
            {
                var newLines = linesA.Skip(start).Take(end - start).ToArray();
                allChanges.Add((start, end, newLines, "A"));
            }

            foreach (var (start, end) in changesB)
            {
                var newLines = linesB.Skip(start).Take(end - start).ToArray();
                allChanges.Add((start, end, newLines, "B"));
            }

            allChanges = allChanges.OrderByDescending(c => c.Start).ToList();

            foreach (var (start, end, newLines, source) in allChanges)
            {
                if (end > start && start < result.Count)
                {
                    var removeCount = Math.Min(end - start, result.Count - start);
                    result.RemoveRange(start, removeCount);
                }

                result.InsertRange(start, newLines);
            }

            return string.Join("\n", result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RESOLVE] Failed to merge non-overlapping changes");
            return null;
        }
    }

    private static List<string> FindAddedPrefix(string[] baseLines, string[] newLines)
    {
        var prefix = new List<string>();

        for (int i = 0; i < newLines.Length; i++)
        {
            if (baseLines.Length > 0 && newLines[i] == baseLines[0])
                break;
            prefix.Add(newLines[i]);
        }

        return prefix;
    }

    private static List<string> FindAddedSuffix(string[] baseLines, string[] newLines)
    {
        var suffix = new List<string>();

        if (baseLines.Length == 0) return suffix;

        int lastBaseInNew = -1;
        for (int i = newLines.Length - 1; i >= 0; i--)
        {
            if (newLines[i] == baseLines[^1])
            {
                lastBaseInNew = i;
                break;
            }
        }

        if (lastBaseInNew >= 0 && lastBaseInNew < newLines.Length - 1)
        {
            for (int i = lastBaseInNew + 1; i < newLines.Length; i++)
            {
                suffix.Add(newLines[i]);
            }
        }

        return suffix;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? (float)(dot / denom) : 0;
    }

    private static float[] SubtractVectors(float[] a, float[] b)
    {
        var result = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] - b[i];
        }
        return result;
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    private static string FormatRanges(List<(int Start, int End)> ranges)
    {
        return string.Join(", ", ranges.Select(r => r.Start == r.End - 1 ? $"{r.Start}" : $"{r.Start}-{r.End - 1}"));
    }

    #endregion
}

/// <summary>
/// Результат разрешения конфликта.
/// </summary>
public sealed record ConflictResolutionResult
{
    public bool IsResolved { get; init; }
    public CodeUnit? MergedUnit { get; init; }
    public string Resolution { get; init; } = "";
    public string Description { get; init; } = "";

    public CodeUnit? BaseUnit { get; init; }
    public CodeUnit? UnitA { get; init; }
    public CodeUnit? UnitB { get; init; }

    public static ConflictResolutionResult Resolved(CodeUnit merged, string resolution, string description)
    {
        return new ConflictResolutionResult
        {
            IsResolved = true,
            MergedUnit = merged,
            Resolution = resolution,
            Description = description
        };
    }

    public static ConflictResolutionResult Unresolved(CodeUnit baseUnit, CodeUnit unitA, CodeUnit unitB, string reason)
    {
        return new ConflictResolutionResult
        {
            IsResolved = false,
            BaseUnit = baseUnit,
            UnitA = unitA,
            UnitB = unitB,
            Resolution = "Unresolved",
            Description = reason
        };
    }
}


using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Slow Path: Semantic matching через embeddings.
/// Используется когда Fast Path не смог найти match.
/// </summary>
public sealed class SemanticMatcher
{
private readonly ILogger<SemanticMatcher> _logger;

// Пороги similarity для разных типов matches
private const float HighSimilarityThreshold = 0.90f;   // Очень похожий код
private const float MediumSimilarityThreshold = 0.75f; // Возможно тот же код
private const float LowSimilarityThreshold = 0.60f;    // Сомнительное совпадение

public SemanticMatcher(ILogger<SemanticMatcher>? logger = null)
{
_logger = logger ?? NullLogger<SemanticMatcher>.Instance;
}

/// <summary>
/// Найти semantic match для CodeUnit.
/// </summary>
public async Task<SemanticMatchResult?> FindSemanticMatchAsync(
CodeUnit sourceUnit,
VersionedIndex targetVersion,
CancellationToken ct = default)
{
if (sourceUnit.Embedding == null)
{
_logger.LogWarning(
"Source unit {Id} has no embedding, cannot perform semantic matching",
sourceUnit.Id);
return null;
}

// 1. Поиск по VectorStore
var similarUnits = await targetVersion.VectorStore.SearchAsync(
sourceUnit.Embedding,
limit: 10,
minSimilarity: LowSimilarityThreshold,
cancellationToken: ct);

if (similarUnits.Count == 0)
{
_logger.LogDebug(
"No semantic matches found for {Id}",
sourceUnit.Id);
return null;
}

// 2. Найти лучший match с учётом типа
var bestMatch = FindBestMatch(
sourceUnit,
similarUnits,
targetVersion);

if (bestMatch == null)
return null;

_logger.LogInformation(
"Semantic match found: {SourceId} -> {TargetId} (similarity: {Similarity:F3})",
sourceUnit.Id,
bestMatch.TargetUnit.Id,
bestMatch.Similarity);

return bestMatch;
}

/// <summary>
/// Batch semantic matching для нескольких units.
/// </summary>
public async Task<Dictionary<string, SemanticMatchResult>> FindSemanticMatchesBatchAsync(
List<CodeUnit> sourceUnits,
VersionedIndex targetVersion,
CancellationToken ct = default)
{
_logger.LogInformation(
"Performing batch semantic matching for {Count} units",
sourceUnits.Count);

var matches = new Dictionary<string, SemanticMatchResult>();

foreach (var sourceUnit in sourceUnits)
{
var match = await FindSemanticMatchAsync(
sourceUnit,
targetVersion,
ct);

if (match != null)
{
matches[sourceUnit.Id] = match;
}
}

_logger.LogInformation(
"Semantic matching: {Matched}/{Total} units matched",
matches.Count,
sourceUnits.Count);

return matches;
}

/// <summary>
/// Найти лучший match среди кандидатов.
/// </summary>
private SemanticMatchResult? FindBestMatch(
CodeUnit sourceUnit,
List<SimilarityResult> candidates,
VersionedIndex targetVersion)
{
SemanticMatchResult? bestMatch = null;
float bestScore = 0f;

foreach (var candidate in candidates)
{
if (!targetVersion.Units.TryGetValue(candidate.Id, out var candidateUnit))
continue;

var similarity = candidate.Similarity;

// Вычислить composite score (similarity + type match + name similarity)
var score = ComputeMatchScore(
sourceUnit,
candidateUnit,
similarity);

if (score > bestScore && score >= LowSimilarityThreshold)
{
bestScore = score;

var confidence = ComputeConfidence(similarity);

bestMatch = new SemanticMatchResult
{
SourceUnit = sourceUnit,
TargetUnit = candidateUnit,
Similarity = similarity,
MatchType = ClassifyMatchType(similarity),
Confidence = confidence,
Score = score
};
}
}

return bestMatch;
}

/// <summary>
/// Вычислить composite score для matching.
/// </summary>
private float ComputeMatchScore(
CodeUnit sourceUnit,
CodeUnit candidateUnit,
float baseSimilarity)
{
float score = baseSimilarity;

// Bonus: одинаковый тип
if (sourceUnit.Type == candidateUnit.Type)
{
score += 0.05f;
}

// Bonus: похожие имена
var nameSimilarity = ComputeNameSimilarity(
sourceUnit.Name,
candidateUnit.Name);

score += nameSimilarity * 0.1f;

// Penalty: сильно разная длина
var lengthRatio = (float)Math.Min(
sourceUnit.Content.Length,
candidateUnit.Content.Length) /
Math.Max(
sourceUnit.Content.Length,
candidateUnit.Content.Length);

if (lengthRatio < 0.5f)
{
score -= 0.1f;
}

return Math.Clamp(score, 0f, 1f);
}

/// <summary>
/// Вычислить similarity имён (Levenshtein-based).
/// </summary>
private float ComputeNameSimilarity(string name1, string name2)
{
if (string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
return 1.0f;

var distance = LevenshteinDistance(
name1.ToLowerInvariant(),
name2.ToLowerInvariant());

var maxLength = Math.Max(name1.Length, name2.Length);
if (maxLength == 0)
return 1.0f;

return 1.0f - (float)distance / maxLength;
}

/// <summary>
/// Levenshtein distance между строками.
/// </summary>
private int LevenshteinDistance(string s1, string s2)
{
var len1 = s1.Length;
var len2 = s2.Length;
var matrix = new int[len1 + 1, len2 + 1];

for (int i = 0; i <= len1; i++)
matrix[i, 0] = i;

for (int j = 0; j <= len2; j++)
matrix[0, j] = j;

for (int i = 1; i <= len1; i++)
{
for (int j = 1; j <= len2; j++)
{
var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

matrix[i, j] = Math.Min(
Math.Min(
matrix[i - 1, j] + 1,
matrix[i, j - 1] + 1),
matrix[i - 1, j - 1] + cost);
}
}

return matrix[len1, len2];
}

/// <summary>
/// Классифицировать тип match по similarity.
/// </summary>
private SemanticMatchType ClassifyMatchType(float similarity)
{
if (similarity >= HighSimilarityThreshold)
return SemanticMatchType.HighSimilarity;

if (similarity >= MediumSimilarityThreshold)
return SemanticMatchType.MediumSimilarity;

return SemanticMatchType.LowSimilarity;
}

/// <summary>
/// Вычислить confidence score.
/// </summary>
private float ComputeConfidence(float similarity)
{
// Нормализовать similarity в confidence [0..1]
if (similarity >= HighSimilarityThreshold)
return 0.90f + (similarity - HighSimilarityThreshold) * 1.0f;

if (similarity >= MediumSimilarityThreshold)
return 0.70f + (similarity - MediumSimilarityThreshold) * 1.33f;

if (similarity >= LowSimilarityThreshold)
return 0.50f + (similarity - LowSimilarityThreshold) * 1.33f;

return similarity;
}
}

/// <summary>
/// Результат semantic matching.
/// </summary>
public sealed record SemanticMatchResult
{
public required CodeUnit SourceUnit { get; init; }
public required CodeUnit TargetUnit { get; init; }
public required float Similarity { get; init; }
public required SemanticMatchType MatchType { get; init; }
public required float Confidence { get; init; }
public required float Score { get; init; }
}

/// <summary>
/// Тип semantic match.
/// </summary>
public enum SemanticMatchType
{
HighSimilarity,    // >= 0.90 - очень похожий код
MediumSimilarity,  // >= 0.75 - вероятно тот же код
LowSimilarity      // >= 0.60 - сомнительное совпадение
}

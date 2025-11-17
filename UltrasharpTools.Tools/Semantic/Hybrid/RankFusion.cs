namespace UltrasharpTools.Tools.Semantic.Hybrid;

/// <summary>
/// Реализация Reciprocal Rank Fusion (RRF) для объединения результатов
/// из разных систем поиска (vector-based + feature-based).
///
/// RRF Formula: score(d) = Σ (1 / (k + rank_i(d)))
/// где:
/// - d - документ (метод/класс)
/// - rank_i(d) - ранг документа в i-том списке (0-based)
/// - k - константа (обычно 60), снижает влияние top-ranked результатов
///
/// Преимущества RRF:
/// - Не требует нормализации scores между системами
/// - Устойчив к outliers
/// - Простой и эффективный
/// - Широко используется в search systems
/// </summary>
public static class RankFusion
{
/// <summary>
/// Применить RRF к нескольким спискам результатов.
/// </summary>
/// <typeparam name="T">Тип результата (должен иметь уникальный ID).</typeparam>
/// <param name="rankedLists">Списки результатов (каждый список отсортирован по релевантности).</param>
/// <param name="idExtractor">Функция для извлечения уникального ID из элемента.</param>
/// <param name="k">RRF константа (default: 60).</param>
/// <returns>Объединённый список с RRF scores.</returns>
public static List<RrfResult<T>> ReciprocalRankFusion<T>(
IEnumerable<List<T>> rankedLists,
Func<T, string> idExtractor,
int k = 60)
{
if (k < 0)
{
throw new ArgumentException("RRF constant k must be non-negative", nameof(k));
}

// Накопить RRF scores для каждого документа
var rrfScores = new Dictionary<string, RrfAccumulator<T>>();

int listIndex = 0;
foreach (var rankedList in rankedLists)
{
for (int rank = 0; rank < rankedList.Count; rank++)
{
var item = rankedList[rank];
var id = idExtractor(item);

if (!rrfScores.TryGetValue(id, out var accumulator))
{
accumulator = new RrfAccumulator<T>
{
Id = id,
Item = item,
RrfScore = 0.0,
RanksByList = new Dictionary<int, int>()
};
rrfScores[id] = accumulator;
}

// RRF: score += 1 / (k + rank)
var rrfContribution = 1.0 / (k + rank);
accumulator.RrfScore += rrfContribution;
accumulator.RanksByList[listIndex] = rank;
}

listIndex++;
}

// Конвертировать в результаты и сортировать по RRF score (desc)
var results = rrfScores.Values
.Select(acc => new RrfResult<T>
{
Id = acc.Id,
Item = acc.Item,
RrfScore = acc.RrfScore,
RanksByList = acc.RanksByList,
AppearanceCount = acc.RanksByList.Count
})
.OrderByDescending(r => r.RrfScore)
.ToList();

// Присвоить финальные ranks
for (int i = 0; i < results.Count; i++)
{
results[i] = results[i] with { FinalRank = i };
}

return results;
}

/// <summary>
/// Применить weighted RRF (разные веса для разных списков).
/// </summary>
/// <typeparam name="T">Тип результата.</typeparam>
/// <param name="rankedLists">Списки результатов с весами.</param>
/// <param name="idExtractor">Функция для извлечения ID.</param>
/// <param name="k">RRF константа.</param>
/// <returns>Объединённый список с weighted RRF scores.</returns>
public static List<RrfResult<T>> WeightedReciprocalRankFusion<T>(
IEnumerable<(List<T> List, double Weight)> rankedLists,
Func<T, string> idExtractor,
int k = 60)
{
if (k < 0)
{
throw new ArgumentException("RRF constant k must be non-negative", nameof(k));
}

// Нормализовать веса (чтобы сумма = 1.0)
var listsArray = rankedLists.ToArray();
var totalWeight = listsArray.Sum(x => x.Weight);

if (totalWeight <= 0)
{
throw new ArgumentException("Total weight must be positive", nameof(rankedLists));
}

var normalizedWeights = listsArray.Select(x => x.Weight / totalWeight).ToArray();

// Накопить weighted RRF scores
var rrfScores = new Dictionary<string, RrfAccumulator<T>>();

for (int listIndex = 0; listIndex < listsArray.Length; listIndex++)
{
var (rankedList, _) = listsArray[listIndex];
var weight = normalizedWeights[listIndex];

for (int rank = 0; rank < rankedList.Count; rank++)
{
var item = rankedList[rank];
var id = idExtractor(item);

if (!rrfScores.TryGetValue(id, out var accumulator))
{
accumulator = new RrfAccumulator<T>
{
Id = id,
Item = item,
RrfScore = 0.0,
RanksByList = new Dictionary<int, int>()
};
rrfScores[id] = accumulator;
}

// Weighted RRF: score += weight * (1 / (k + rank))
var rrfContribution = weight * (1.0 / (k + rank));
accumulator.RrfScore += rrfContribution;
accumulator.RanksByList[listIndex] = rank;
}
}

// Конвертировать в результаты
var results = rrfScores.Values
.Select(acc => new RrfResult<T>
{
Id = acc.Id,
Item = acc.Item,
RrfScore = acc.RrfScore,
RanksByList = acc.RanksByList,
AppearanceCount = acc.RanksByList.Count
})
.OrderByDescending(r => r.RrfScore)
.ToList();

// Присвоить финальные ranks
for (int i = 0; i < results.Count; i++)
{
results[i] = results[i] with { FinalRank = i };
}

return results;
}

/// <summary>
/// Применить min-max нормализацию к scores перед объединением.
/// Полезно когда scores имеют разные масштабы.
/// </summary>
public static List<T> NormalizeScores<T>(
List<T> items,
Func<T, double> scoreExtractor,
Func<T, double, T> scoreUpdater)
{
if (items.Count == 0)
{
return items;
}

var scores = items.Select(scoreExtractor).ToList();
var minScore = scores.Min();
var maxScore = scores.Max();

// Избежать деления на ноль
if (Math.Abs(maxScore - minScore) < 1e-10)
{
return items.Select(item => scoreUpdater(item, 1.0)).ToList();
}

return items.Select(item =>
{
var score = scoreExtractor(item);
var normalized = (score - minScore) / (maxScore - minScore);
return scoreUpdater(item, normalized);
}).ToList();
}

// Private helpers

private sealed class RrfAccumulator<T>
{
public required string Id { get; init; }
public required T Item { get; set; }
public double RrfScore { get; set; }
public required Dictionary<int, int> RanksByList { get; init; }
}
}

/// <summary>
/// Результат RRF fusion.
/// </summary>
/// <typeparam name="T">Тип исходного элемента.</typeparam>
public sealed record RrfResult<T>
{
/// <summary>
/// Уникальный ID элемента.
/// </summary>
public required string Id { get; init; }

/// <summary>
/// Исходный элемент.
/// </summary>
public required T Item { get; init; }

/// <summary>
/// RRF score (выше = лучше).
/// </summary>
public required double RrfScore { get; init; }

/// <summary>
/// Финальный ранг после RRF (0-based).
/// </summary>
public int FinalRank { get; init; }

/// <summary>
/// Ranks в каждом исходном списке (listIndex -> rank).
/// </summary>
public required Dictionary<int, int> RanksByList { get; init; }

/// <summary>
/// Количество списков, в которых появился элемент.
/// </summary>
public required int AppearanceCount { get; init; }

/// <summary>
/// Минимальный ранг среди всех списков (лучший ранг).
/// </summary>
public int MinRank => RanksByList.Values.DefaultIfEmpty(int.MaxValue).Min();

/// <summary>
/// Максимальный ранг среди всех списков (худший ранг).
/// </summary>
public int MaxRank => RanksByList.Values.DefaultIfEmpty(0).Max();

/// <summary>
/// Средний ранг среди всех списков.
/// </summary>
public double AverageRank => RanksByList.Values.DefaultIfEmpty(0).Average();
}

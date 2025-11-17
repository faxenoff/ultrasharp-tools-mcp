using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic.Backends;

/// <summary>
/// Выбор оптимального vector store backend на основе размера кодовой базы.
/// Автоматически переключается между SqliteVec (brute-force) и Vectorlite (HNSW ANN).
/// </summary>
public sealed class BackendSelector
{
private readonly BackendSelectorConfig _config;

public BackendSelector(BackendSelectorConfig? config = null)
{
_config = config ?? BackendSelectorConfig.Default;
}

/// <summary>
/// Выбрать backend на основе текущего количества векторов.
/// </summary>
/// <param name="currentVectorCount">Текущее количество векторов в базе</param>
/// <returns>Рекомендуемый backend type</returns>
public VectorStoreBackendType SelectBackend(int currentVectorCount)
{
return currentVectorCount <= _config.SwitchThreshold
? VectorStoreBackendType.SqliteVec
: VectorStoreBackendType.Vectorlite;
}

/// <summary>
/// Создать instance выбранного backend.
/// </summary>
/// <param name="backendType">Тип backend (или Auto для автовыбора)</param>
/// <param name="currentVectorCount">Текущее количество векторов (для Auto режима)</param>
/// <returns>Экземпляр IVectorStoreBackend</returns>
public IVectorStoreBackend CreateBackend(
VectorStoreBackendType backendType,
int currentVectorCount = 0)
{
var selectedType = backendType == VectorStoreBackendType.Auto
? SelectBackend(currentVectorCount)
: backendType;

return selectedType switch
{
VectorStoreBackendType.SqliteVec => new SqliteVecBackend(),
VectorStoreBackendType.Vectorlite => new VectorliteBackend(_config.VectorliteConfig),
_ => throw new ArgumentException($"Unsupported backend type: {selectedType}", nameof(backendType))
};
}

/// <summary>
/// Проверить, нужно ли переключиться на другой backend.
/// </summary>
/// <param name="currentBackend">Текущий backend</param>
/// <param name="currentVectorCount">Текущее количество векторов</param>
/// <returns>True, если нужно переключиться</returns>
public bool ShouldSwitchBackend(VectorStoreBackendType currentBackend, int currentVectorCount)
{
var optimalBackend = SelectBackend(currentVectorCount);
return currentBackend != optimalBackend;
}

/// <summary>
/// Получить рекомендации по настройке Vectorlite на основе размера кодовой базы.
/// </summary>
/// <param name="estimatedVectorCount">Ожидаемое количество векторов</param>
/// <returns>Рекомендуемая конфигурация Vectorlite</returns>
public static VectorliteConfig GetRecommendedVectorliteConfig(int estimatedVectorCount)
{
return estimatedVectorCount switch
{
<= 50_000 => VectorliteConfig.ForSmallCodebase, // 10K-50K: M=16, ef=100/50
<= 200_000 => VectorliteConfig.ForMediumCodebase, // 50K-200K: M=24, ef=150/75
_ => VectorliteConfig.ForLargeCodebase // >200K: M=32, ef=200/100
};
}

/// <summary>
/// Получить описание выбранного backend.
/// </summary>
public string GetBackendDescription(VectorStoreBackendType backendType)
{
return backendType switch
{
VectorStoreBackendType.SqliteVec =>
"SqliteVec (brute-force SIMD): 100% accuracy, fast indexing, best for <10K vectors",

VectorStoreBackendType.Vectorlite =>
"Vectorlite (HNSW ANN): 99.9%+ recall, 3x-100x faster search, best for >10K vectors",

VectorStoreBackendType.Auto =>
$"Auto-select: SqliteVec if ≤{_config.SwitchThreshold:N0} vectors, Vectorlite if >{_config.SwitchThreshold:N0} vectors",

_ => "Unknown backend"
};
}
}

/// <summary>
/// Конфигурация для BackendSelector.
/// </summary>
public sealed record BackendSelectorConfig
{
/// <summary>
/// Порог переключения между SqliteVec и Vectorlite (по количеству векторов).
/// Default: 10,000 векторов.
/// </summary>
public int SwitchThreshold { get; init; } = 10_000;

/// <summary>
/// Конфигурация для Vectorlite backend (используется при переключении).
/// </summary>
public VectorliteConfig VectorliteConfig { get; init; } = VectorliteConfig.Default;

/// <summary>
/// Включить автоматическое переключение backend при достижении порога.
/// Если false, backend будет выбран один раз при инициализации.
/// </summary>
public bool EnableAutoSwitching { get; init; } = true;

/// <summary>
/// Минимальное количество векторов перед созданием HNSW индекса в Vectorlite.
/// Создание индекса на малом датасете неэффективно.
/// Default: 1,000 векторов.
/// </summary>
public int MinVectorsForHnswIndex { get; init; } = 1_000;

public static BackendSelectorConfig Default => new();

/// <summary>
/// Конфигурация для корпоративных кодовых баз (большие проекты).
/// </summary>
public static BackendSelectorConfig ForEnterprise => new()
{
SwitchThreshold = 50_000, // Переключение на 50K векторов
VectorliteConfig = VectorliteConfig.ForLargeCodebase,
EnableAutoSwitching = true,
MinVectorsForHnswIndex = 10_000
};

/// <summary>
/// Конфигурация для небольших проектов.
/// </summary>
public static BackendSelectorConfig ForSmallProjects => new()
{
SwitchThreshold = 5_000, // Переключение на 5K векторов
VectorliteConfig = VectorliteConfig.ForSmallCodebase,
EnableAutoSwitching = true,
MinVectorsForHnswIndex = 500
};
}

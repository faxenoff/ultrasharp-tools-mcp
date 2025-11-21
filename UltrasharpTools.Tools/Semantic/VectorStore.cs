
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Semantic.Backends;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Vector store facade с автоматическим выбором backend (SqliteVec или Vectorlite).
/// Предоставляет единый API для хранения и поиска vector embeddings.
/// </summary>
public sealed class VectorStore : IAsyncDisposable
{
private readonly VectorStoreConfig _config;
private readonly BackendSelector _backendSelector;
private readonly ILogger<VectorStore> _logger;

private IVectorStoreBackend? _currentBackend;
private VectorStoreBackendType _currentBackendType;
private bool _initialized;

public VectorStore(
VectorStoreConfig? config = null,
ILogger<VectorStore>? logger = null)
{
_config = config ?? VectorStoreConfig.Default;
_backendSelector = new BackendSelector(_config.BackendSelectorConfig);
_logger = logger ?? NullLogger<VectorStore>.Instance;
}

/// <summary>
/// Инициализировать vector store.
/// </summary>
public async Task InitializeAsync(
string connectionString,
int dimension,
CancellationToken cancellationToken = default)
{
_logger.LogInformation(
"Initializing VectorStore with dimension={Dimension}, backend={BackendType}",
dimension, _config.PreferredBackend);

// Создать начальный backend
var initialBackendType = _config.PreferredBackend == VectorStoreBackendType.Auto
? VectorStoreBackendType.SqliteVec // Начинаем с SqliteVec для малых баз
: _config.PreferredBackend;

_currentBackend = _backendSelector.CreateBackend(initialBackendType);
_currentBackendType = initialBackendType;

await _currentBackend.InitializeAsync(connectionString, dimension, cancellationToken);

_initialized = true;

_logger.LogInformation(
"VectorStore initialized with {BackendType} backend",
_currentBackendType);
}

/// <summary>
/// Вставить один embedding.
/// </summary>
public async Task InsertAsync(VectorEmbedding embedding, CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

await _currentBackend!.InsertAsync(embedding, cancellationToken);

// Проверить нужно ли переключить backend (если Auto mode)
await CheckAndSwitchBackendIfNeededAsync(cancellationToken);
}

/// <summary>
/// Вставить batch embeddings.
/// </summary>
public async Task InsertBatchAsync(
IEnumerable<VectorEmbedding> embeddings,
CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

await _currentBackend!.InsertBatchAsync(embeddings, cancellationToken);

// Проверить нужно ли переключить backend
await CheckAndSwitchBackendIfNeededAsync(cancellationToken);
}

/// <summary>
/// Поиск по similarity.
/// </summary>
public async Task<List<SimilarityResult>> SearchAsync(
float[] queryVector,
int limit,
float minSimilarity = 0.0f,
CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

return await _currentBackend!.SearchAsync(queryVector, limit, minSimilarity, cancellationToken);
}

/// <summary>
/// Получить количество векторов.
/// </summary>
public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

return await _currentBackend!.GetCountAsync(cancellationToken);
}

/// <summary>
/// Удалить embedding по ID.
/// </summary>
public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

await _currentBackend!.DeleteAsync(id, cancellationToken);
}

/// <summary>
/// Очистить все embeddings.
/// </summary>
public async Task ClearAsync(CancellationToken cancellationToken = default)
{
ThrowIfNotInitialized();

await _currentBackend!.ClearAsync(cancellationToken);
}

/// <summary>
/// Получить текущий backend type.
/// </summary>
public VectorStoreBackendType GetCurrentBackend() => _currentBackendType;

/// <summary>
/// Получить описание текущего backend.
/// </summary>
public string GetBackendDescription() =>
_backendSelector.GetBackendDescription(_currentBackendType);

/// <summary>
/// Health check.
/// </summary>
public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
{
if (!_initialized || _currentBackend == null)
{
return false;
}

return await _currentBackend.HealthCheckAsync(cancellationToken);
}

public async ValueTask DisposeAsync()
{
if (_currentBackend != null)
{
await _currentBackend.DisposeAsync();
_currentBackend = null;
}
}

// Private helpers

private void ThrowIfNotInitialized()
{
if (!_initialized || _currentBackend == null)
{
throw new InvalidOperationException(
"VectorStore not initialized. Call InitializeAsync first.");
}
}

private async Task CheckAndSwitchBackendIfNeededAsync(CancellationToken cancellationToken)
{
// Если Auto mode выключен или не используется Auto backend, не переключаем
if (!_config.BackendSelectorConfig.EnableAutoSwitching ||
_config.PreferredBackend != VectorStoreBackendType.Auto)
{
return;
}

var currentCount = await _currentBackend!.GetCountAsync(cancellationToken);

// Проверить нужно ли переключиться
if (!_backendSelector.ShouldSwitchBackend(_currentBackendType, currentCount))
{
return;
}

var newBackendType = _backendSelector.SelectBackend(currentCount);

_logger.LogInformation(
"Switching backend from {OldBackend} to {NewBackend} (vector count: {Count})",
_currentBackendType, newBackendType, currentCount);

// TODO: Миграция данных между backend (будет реализовано позже)
// Пока просто логируем предупреждение
_logger.LogWarning(
"Backend switching detected but data migration not yet implemented. " +
"Manual reindexing required after switching from {OldBackend} to {NewBackend}.",
_currentBackendType, newBackendType);

// Можно добавить здесь автоматическую миграцию:
// 1. Экспортировать все embeddings из старого backend
// 2. Создать новый backend
// 3. Импортировать embeddings в новый backend
// 4. Dispose старый backend
}
}

/// <summary>
/// Конфигурация для VectorStore.
/// </summary>
public sealed record VectorStoreConfig
{
/// <summary>
/// Предпочитаемый backend: Auto, SqliteVec, или Vectorlite.
/// Default: Auto (автоматический выбор на основе размера).
/// </summary>
public VectorStoreBackendType PreferredBackend { get; init; } = VectorStoreBackendType.Auto;

/// <summary>
/// Конфигурация для BackendSelector.
/// </summary>
public BackendSelectorConfig BackendSelectorConfig { get; init; } = BackendSelectorConfig.Default;

/// <summary>
/// Connection string для SQLite database.
/// Default: ":memory:" (in-memory database).
/// </summary>
public string ConnectionString { get; init; } = ":memory:";

/// <summary>
/// Dimension векторов (384, 768, и т.д.).
/// Должен совпадать с dimension используемой embedding модели.
/// </summary>
public int Dimension { get; init; } = 768;

public static VectorStoreConfig Default => new();

/// <summary>
/// Конфигурация для production с persistent storage.
/// </summary>
public static VectorStoreConfig ForProduction(string databasePath, int dimension = 768) => new()
{
ConnectionString = $"Data Source={databasePath}",
Dimension = dimension,
PreferredBackend = VectorStoreBackendType.Auto,
BackendSelectorConfig = BackendSelectorConfig.Default
};

/// <summary>
/// Конфигурация для корпоративных проектов (большие кодовые базы).
/// </summary>
public static VectorStoreConfig ForEnterprise(string databasePath, int dimension = 768) => new()
{
ConnectionString = $"Data Source={databasePath}",
Dimension = dimension,
PreferredBackend = VectorStoreBackendType.Auto,
BackendSelectorConfig = BackendSelectorConfig.ForEnterprise
};

/// <summary>
/// Конфигурация для тестирования (in-memory).
/// </summary>
public static VectorStoreConfig ForTesting(int dimension = 768) => new()
{
ConnectionString = ":memory:",
Dimension = dimension,
PreferredBackend = VectorStoreBackendType.SqliteVec // Для тестов используем SqliteVec
};
}

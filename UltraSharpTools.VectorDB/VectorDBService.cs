using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltraSharpTools.VectorDB.Semantic;

namespace UltraSharpTools.VectorDB;

/// <summary>
/// Сервис семантической индексации и векторного поиска
/// Обрабатывает запросы от Droid через IPC
/// Поддерживает degraded режим при недоступности embedding provider
/// </summary>
public sealed partial class VectorDBService : IAsyncDisposable {
    private readonly VectorDBSemanticService? _semanticService;
    private readonly ILogger<VectorDBService> _logger;
    private readonly PowerManagementService _powerManagement;

    // Информация о состоянии провайдера
    private readonly string? _providerName;
    private readonly string _providerStatus;
    private readonly string? _providerError;

    /// <summary>
    /// Создает VectorDBService в нормальном режиме работы
    /// </summary>
    public VectorDBService(
    VectorDBSemanticService semanticService,
    ILogger<VectorDBService> logger,
    PowerManagementService powerManagement,
    string providerName) {
        _semanticService = semanticService;
        _logger = logger;
        _powerManagement = powerManagement;
        _providerName = providerName;
        _providerStatus = "available";
        _providerError = null;
    }

    /// <summary>
    /// Создает VectorDBService в degraded режиме (без embedding provider)
    /// </summary>
    public VectorDBService(
    ILogger<VectorDBService> logger,
    PowerManagementService powerManagement,
    string providerError) {
        _semanticService = null;
        _logger = logger;
        _powerManagement = powerManagement;
        _providerName = null;
        _providerStatus = "unavailable";
        _providerError = providerError;
    }

    /// <summary>
    /// Проверяет, доступен ли semantic service
    /// </summary>
    public bool IsSemanticAvailable => _semanticService != null;

    /// <summary>
    /// Обрабатывает запросы от Droid
    /// Протокол: newline-delimited JSON
    /// </summary>
    public async Task ProcessRequestsAsync(Stream stream, CancellationToken cancellationToken = default) {
        var reader = PipeReader.Create(stream);
        var writer = PipeWriter.Create(stream);

        try {
            while (!cancellationToken.IsCancellationRequested) {
                // Читаем запрос (newline-delimited JSON)
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (TryReadLine(ref buffer, out var line)) {
                    // Обрабатываем запрос
                    var response = await HandleRequestAsync(line, cancellationToken);

                    // Отправляем ответ
                    await writer.WriteAsync(Encoding.UTF8.GetBytes(response + "\n"), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) {
                    break;
                }
            }
        } finally {
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Обрабатывает один запрос
    /// </summary>
    private async Task<string> HandleRequestAsync(ReadOnlyMemory<byte> requestBytes, CancellationToken cancellationToken) {
        // Регистрируем активность - выходим из idle режима при необходимости
        _powerManagement.RecordActivity();

        try {
            var requestJson = Encoding.UTF8.GetString(requestBytes.Span);
            using var request = JsonDocument.Parse(requestJson);

            var root = request.RootElement;
            var method = root.GetProperty("method").GetString() ?? "unknown";

            LogHandlingRequest(method);

            // Роутинг по типу запроса
            return method switch {
                "index_code" => await IndexCodeAsync(root, cancellationToken),
                "index_batch" => await IndexBatchAsync(root, cancellationToken),
                "search_similar" => await SearchSimilarAsync(root, cancellationToken),
                "get_status" => await GetStatusAsync(cancellationToken),
                "clear" => await ClearAsync(cancellationToken),
                _ => CreateErrorResponse($"Unknown method: {method}")
            };
        } catch (Exception ex) {
            LogErrorHandlingRequest(ex);
            return CreateErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Проверяет доступность semantic service и возвращает ошибку если недоступен
    /// </summary>
    private string? CheckSemanticAvailable() {
        if (_semanticService == null) {
            return CreateErrorResponse($"Embedding provider unavailable: {_providerError ?? "not initialized"}");
        }
        return null;
    }

    /// <summary>
    /// Индексирует код (добавляет в векторную БД)
    /// </summary>
    private async Task<string> IndexCodeAsync(JsonElement request, CancellationToken cancellationToken) {
        var unavailableResponse = CheckSemanticAvailable();
        if (unavailableResponse != null)
            return unavailableResponse;

        try {
            var parameters = request.GetProperty("parameters");
            var code = parameters.GetProperty("code").GetString() ?? "";
            var documentPath = parameters.GetProperty("documentPath").GetString() ?? "";
            var metadata = parameters.TryGetProperty("metadata", out var metadataElement)
            ? metadataElement.GetString()
            : null;

            await _semanticService!.IndexCodeAsync(code, documentPath, metadata, cancellationToken);

            var response = new IndexCodeResponse(
            Success: true,
            Message: "Code indexed successfully",
            DocumentPath: documentPath
            );

            return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.IndexCodeResponse);
        } catch (Exception ex) {
            LogErrorIndexingCode(ex);
            return CreateErrorResponse($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Batch-индексация кода (оптимизированная версия для множества элементов)
    /// </summary>
    private async Task<string> IndexBatchAsync(JsonElement request, CancellationToken cancellationToken) {
        var unavailableResponse = CheckSemanticAvailable();
        if (unavailableResponse != null)
            return unavailableResponse;

        try {
            var parameters = request.GetProperty("parameters");
            var itemsElement = parameters.GetProperty("items");

            var items = new List<(string Code, string DocumentPath, string? Metadata)>();

            foreach (var item in itemsElement.EnumerateArray()) {
                var code = item.GetProperty("code").GetString() ?? "";
                var documentPath = item.GetProperty("documentPath").GetString() ?? "";
                var metadata = item.TryGetProperty("metadata", out var metadataElement)
                ? metadataElement.GetString()
                : null;

                items.Add((code, documentPath, metadata));
            }

            var indexedCount = await _semanticService!.IndexBatchAsync(items, cancellationToken);

            var response = new IndexBatchResponse(
            Success: true,
            Message: $"Batch indexed {indexedCount} items",
            IndexedCount: indexedCount
            );

            return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.IndexBatchResponse);
        } catch (Exception ex) {
            LogErrorIndexingBatch(ex);
            return CreateErrorResponse($"Batch indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Поиск семантически похожего кода
    /// </summary>
    private async Task<string> SearchSimilarAsync(JsonElement request, CancellationToken cancellationToken) {
        var unavailableResponse = CheckSemanticAvailable();
        if (unavailableResponse != null)
            return unavailableResponse;

        try {
            var parameters = request.GetProperty("parameters");
            var query = parameters.GetProperty("query").GetString() ?? "";
            var topK = parameters.TryGetProperty("topK", out var topKElement)
            ? topKElement.GetInt32()
            : 10;
            var minSimilarity = parameters.TryGetProperty("minSimilarity", out var minSimElement)
            ? (float)minSimElement.GetDouble()
            : 0.0f;

            var results = await _semanticService!.SearchSimilarAsync(query, topK, minSimilarity, cancellationToken);

            var response = new SearchSimilarResponse(
            Success: true,
            Count: results.Count,
            Matches: results.Select(r => new SearchMatch(
            Id: r.Id,
            Content: r.Content,
            Similarity: r.Similarity,
            Metadata: r.Metadata
            )).ToArray()
            );

            return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.SearchSimilarResponse);
        } catch (Exception ex) {
            LogErrorSearching(ex);
            return CreateErrorResponse($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает статус индексатора
    /// </summary>
    private async Task<string> GetStatusAsync(CancellationToken cancellationToken) {
        try {
            var count = 0;
            if (_semanticService != null) {
                count = await _semanticService.GetCountAsync(cancellationToken);
            }

            var status = _semanticService != null ? "running" : "degraded";

            var response = new GetStatusResponse(
            Success: true,
            Status: status,
            IndexedCount: count,
            Version: Program.ApplicationVersion,
            ProviderStatus: _providerStatus,
            ProviderName: _providerName,
            ProviderError: _providerError
            );

            return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.GetStatusResponse);
        } catch (Exception ex) {
            LogErrorGettingStatus(ex);
            return CreateErrorResponse($"Status check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Очищает все embeddings
    /// </summary>
    private async Task<string> ClearAsync(CancellationToken cancellationToken) {
        var unavailableResponse = CheckSemanticAvailable();
        if (unavailableResponse != null)
            return unavailableResponse;

        try {
            await _semanticService!.ClearAsync(cancellationToken);

            var response = new ClearResponse(
            Success: true,
            Message: "All embeddings cleared"
            );

            return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.ClearResponse);
        } catch (Exception ex) {
            LogErrorClearing(ex);
            return CreateErrorResponse($"Clear failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Создает ответ с ошибкой
    /// </summary>
    private static string CreateErrorResponse(string message) {
        var response = new ErrorResponse(
        Success: false,
        Error: message
        );

        return JsonSerializer.Serialize(response, VectorDBJsonContext.Default.ErrorResponse);
    }

    /// <summary>
    /// Читает одну строку из буфера
    /// </summary>
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> line) {
        var position = buffer.PositionOf((byte)'\n');

        if (position == null) {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value).ToArray();
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    /// <summary>
    /// Освобождает ресурсы сервиса
    /// </summary>
    public async ValueTask DisposeAsync() {
        _logger.LogInformation("[VectorDBService] Disposing...");

        if (_semanticService != null) {
            await _semanticService.DisposeAsync();
        }

        _logger.LogInformation("[VectorDBService] Disposed.");
    }
}

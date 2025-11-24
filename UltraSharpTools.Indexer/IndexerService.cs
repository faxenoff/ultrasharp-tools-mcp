using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltraSharpTools.Indexer.Semantic;

namespace UltraSharpTools.Indexer;

/// <summary>
/// Сервис семантической индексации и векторного поиска
/// Обрабатывает запросы от Droid через IPC
/// </summary>
public sealed class IndexerService
{
    private readonly IndexerSemanticService _semanticService;
    private readonly ILogger<IndexerService> _logger;

    public IndexerService(
        IndexerSemanticService semanticService,
        ILogger<IndexerService> logger)
    {
        _semanticService = semanticService;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает запросы от Droid
    /// Протокол: newline-delimited JSON
    /// </summary>
    public async Task ProcessRequestsAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var reader = PipeReader.Create(stream);
        var writer = PipeWriter.Create(stream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Читаем запрос (newline-delimited JSON)
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (TryReadLine(ref buffer, out var line))
                {
                    // Обрабатываем запрос
                    var response = await HandleRequestAsync(line, cancellationToken);

                    // Отправляем ответ
                    await writer.WriteAsync(Encoding.UTF8.GetBytes(response + "\n"), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Обрабатывает один запрос
    /// </summary>
    private async Task<string> HandleRequestAsync(ReadOnlyMemory<byte> requestBytes, CancellationToken cancellationToken)
    {
        try
        {
            var requestJson = Encoding.UTF8.GetString(requestBytes.Span);
            using var request = JsonDocument.Parse(requestJson);

            var root = request.RootElement;
            var method = root.GetProperty("method").GetString();

            _logger.LogDebug("[IndexerService] Handling request: {Method}", method);

            // Роутинг по типу запроса
            return method switch
            {
                "index_code" => await IndexCodeAsync(root, cancellationToken),
                "search_similar" => await SearchSimilarAsync(root, cancellationToken),
                "get_status" => await GetStatusAsync(cancellationToken),
                "clear" => await ClearAsync(cancellationToken),
                _ => CreateErrorResponse($"Unknown method: {method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexerService] Error handling request");
            return CreateErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Индексирует код (добавляет в векторную БД)
    /// </summary>
    private async Task<string> IndexCodeAsync(JsonElement request, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = request.GetProperty("parameters");
            var code = parameters.GetProperty("code").GetString() ?? "";
            var documentPath = parameters.GetProperty("documentPath").GetString() ?? "";
            var metadata = parameters.TryGetProperty("metadata", out var metadataElement)
                ? metadataElement.GetString()
                : null;

            await _semanticService.IndexCodeAsync(code, documentPath, metadata, cancellationToken);

            var response = new IndexCodeResponse(
                Success: true,
                Message: "Code indexed successfully",
                DocumentPath: documentPath
            );

            return JsonSerializer.Serialize(response, IndexerJsonContext.Default.IndexCodeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexerService] Error indexing code");
            return CreateErrorResponse($"Indexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Поиск семантически похожего кода
    /// </summary>
    private async Task<string> SearchSimilarAsync(JsonElement request, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = request.GetProperty("parameters");
            var query = parameters.GetProperty("query").GetString() ?? "";
            var topK = parameters.TryGetProperty("topK", out var topKElement)
                ? topKElement.GetInt32()
                : 10;
            var minSimilarity = parameters.TryGetProperty("minSimilarity", out var minSimElement)
                ? (float)minSimElement.GetDouble()
                : 0.0f;

            var results = await _semanticService.SearchSimilarAsync(query, topK, minSimilarity, cancellationToken);

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

            return JsonSerializer.Serialize(response, IndexerJsonContext.Default.SearchSimilarResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexerService] Error searching");
            return CreateErrorResponse($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает статус индексатора
    /// </summary>
    private async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _semanticService.GetCountAsync(cancellationToken);

            var response = new GetStatusResponse(
                Success: true,
                Status: "running",
                IndexedCount: count,
                Version: "3.0.7"
            );

            return JsonSerializer.Serialize(response, IndexerJsonContext.Default.GetStatusResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexerService] Error getting status");
            return CreateErrorResponse($"Status check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Очищает все embeddings
    /// </summary>
    private async Task<string> ClearAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _semanticService.ClearAsync(cancellationToken);

            var response = new ClearResponse(
                Success: true,
                Message: "All embeddings cleared"
            );

            return JsonSerializer.Serialize(response, IndexerJsonContext.Default.ClearResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IndexerService] Error clearing");
            return CreateErrorResponse($"Clear failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Создает ответ с ошибкой
    /// </summary>
    private static string CreateErrorResponse(string message)
    {
        var response = new ErrorResponse(
            Success: false,
            Error: message
        );

        return JsonSerializer.Serialize(response, IndexerJsonContext.Default.ErrorResponse);
    }

    /// <summary>
    /// Читает одну строку из буфера
    /// </summary>
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> line)
    {
        var position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value).ToArray();
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }
}

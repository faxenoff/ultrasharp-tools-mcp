using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Ipc;

/// <summary>
/// IPC клиент для подключения к Indexer процессу
/// Делегирует семантические операции (индексация, поиск) в Indexer
/// </summary>
public sealed class IndexerClient : IDisposable
{
private const string PipeName = "UltraSharpTools_Indexer";
private readonly ILogger<IndexerClient> _logger;
private NamedPipeClientStream? _pipeClient;
private Process? _indexerProcess;
private readonly SemaphoreSlim _requestLock = new(1, 1);

public IndexerClient(ILogger<IndexerClient> logger)
{
_logger = logger;
}

/// <summary>
/// Подключается к Indexer (запускает процесс если нужно)
/// </summary>
public async Task ConnectAsync(CancellationToken cancellationToken = default)
{
// Пробуем подключиться к существующему Indexer
try
{
_pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await _pipeClient.ConnectAsync(1000, cancellationToken);
_logger.LogInformation("[Indexer] Connected to existing Indexer process");
return;
}
catch (TimeoutException)
{
// Indexer не запущен, запускаем его
}

// Запускаем Indexer процесс
await StartIndexerAsync(cancellationToken);

// Подключаемся к новому процессу
_pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await _pipeClient.ConnectAsync(5000, cancellationToken);
_logger.LogInformation("[Indexer] Connected to new Indexer process");
}

/// <summary>
/// Запускает Indexer процесс
/// </summary>
private async Task StartIndexerAsync(CancellationToken cancellationToken)
{
var currentDir = AppContext.BaseDirectory;
var indexerPath = Path.Combine(currentDir, "UltraSharpTools.Indexer.exe");

if (!File.Exists(indexerPath))
{
throw new FileNotFoundException(
$"Indexer executable not found: {indexerPath}. " +
"Ensure UltraSharpTools.Indexer.exe is in the same directory as Droid."
);
}

var startInfo = new ProcessStartInfo
{
FileName = indexerPath,
UseShellExecute = false,
CreateNoWindow = true,
RedirectStandardError = true
};

_indexerProcess = Process.Start(startInfo);

if (_indexerProcess == null)
{
throw new InvalidOperationException("Failed to start Indexer process");
}

_logger.LogInformation("[Indexer] Started Indexer process (PID: {Pid})", _indexerProcess.Id);

// Ждем пока Indexer создаст Named Pipe
await Task.Delay(2000, cancellationToken);
}

/// <summary>
/// Отправляет запрос в Indexer и возвращает ответ
/// </summary>
public async Task<TResponse?> SendRequestAsync<TResponse>(
string method,
object? parameters,
CancellationToken cancellationToken = default)
{
if (_pipeClient == null || !_pipeClient.IsConnected)
{
await ConnectAsync(cancellationToken);
}

await _requestLock.WaitAsync(cancellationToken);
try
{
// Формируем запрос
var request = new
{
method,
parameters
};

var requestJson = JsonSerializer.Serialize(request);
var requestBytes = Encoding.UTF8.GetBytes(requestJson + "\n");

// Отправляем запрос
await _pipeClient!.WriteAsync(requestBytes, cancellationToken);
await _pipeClient.FlushAsync(cancellationToken);

// Читаем ответ (newline-delimited)
using var reader = new StreamReader(_pipeClient, Encoding.UTF8, leaveOpen: true);
var responseLine = await reader.ReadLineAsync(cancellationToken);

if (responseLine == null)
{
throw new InvalidOperationException("Indexer connection closed");
}

// Парсим ответ
return JsonSerializer.Deserialize<TResponse>(responseLine);
}
finally
{
_requestLock.Release();
}
}

/// <summary>
/// Индексирует код в векторную БД
/// </summary>
public async Task<bool> IndexCodeAsync(
    string code,
    string documentPath,
    string? metadata = null,
    CancellationToken cancellationToken = default)
{
    var response = await SendRequestAsync<JsonElement>(
        "index_code",
        new { code, documentPath, metadata },
        cancellationToken
    );

    return response.TryGetProperty("success", out var success) && success.GetBoolean();
}

/// <summary>
/// Ищет семантически похожий код
/// </summary>
public async Task<List<SimilarityMatch>> SearchSimilarAsync(
    string query,
    int topK = 10,
    float minSimilarity = 0.0f,
    CancellationToken cancellationToken = default)
{
    var response = await SendRequestAsync<JsonElement>(
        "search_similar",
        new { query, topK, minSimilarity },
        cancellationToken
    );

    var matches = new List<SimilarityMatch>();

    if (response.TryGetProperty("matches", out var matchesArray))
    {
        foreach (var match in matchesArray.EnumerateArray())
        {
            matches.Add(new SimilarityMatch
            {
                Id = match.GetProperty("id").GetString() ?? "",
                Content = match.GetProperty("content").GetString() ?? "",
                Similarity = (float)match.GetProperty("similarity").GetDouble(),
                Metadata = match.TryGetProperty("metadata", out var meta)
                    ? meta.GetString()
                    : null
            });
        }
    }

    return matches;
}

/// <summary>
/// Получает статус Indexer
/// </summary>
public async Task<IndexerStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
{
    var response = await SendRequestAsync<JsonElement>(
        "get_status",
        null,
        cancellationToken
    );

    if (response.TryGetProperty("success", out var success) && success.GetBoolean())
    {
        return new IndexerStatus
        {
            Status = response.GetProperty("status").GetString() ?? "unknown",
            IndexedCount = response.GetProperty("indexed_count").GetInt32(),
            Version = response.GetProperty("version").GetString() ?? ""
        };
    }

    return null;
}

/// <summary>
/// Очищает все embeddings
/// </summary>
public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
{
    var response = await SendRequestAsync<JsonElement>(
        "clear",
        null,
        cancellationToken
    );

    return response.TryGetProperty("success", out var success) && success.GetBoolean();
}

public void Dispose()
{
_pipeClient?.Dispose();
_indexerProcess?.Dispose();
_requestLock.Dispose();
}
}

/// <summary>
/// Результат поиска семантически похожего кода
/// </summary>
public sealed record SimilarityMatch
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required float Similarity { get; init; }
    public string? Metadata { get; init; }
}

/// <summary>
/// Статус Indexer сервиса
/// </summary>
public sealed record IndexerStatus
{
    public required string Status { get; init; }
    public required int IndexedCount { get; init; }
    public required string Version { get; init; }
}

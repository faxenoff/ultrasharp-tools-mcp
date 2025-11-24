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
public sealed class VectorDBClient : IDisposable {
    private const string PipeName = "UltraSharpTools_VectorDB";
    private readonly ILogger<VectorDBClient> _logger;
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Process? _indexerProcess;
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    public VectorDBClient(ILogger<VectorDBClient> logger) {
        _logger = logger;
    }

    /// <summary>
    /// Подключается к Indexer (запускает процесс если нужно)
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        // Пробуем подключиться к существующему Indexer
        try {
            _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipeClient.ConnectAsync(2000, cancellationToken);
            InitializeStreams();
            _logger.LogInformation("[VectorDB] Connected to existing VectorDB process");
            return;
        } catch (TimeoutException) {
            // Indexer не запущен, запускаем его
            _logger.LogInformation("[VectorDB] No existing process found (timeout), starting new VectorDB...");
        } catch (IOException) when (!cancellationToken.IsCancellationRequested) {
            // Named Pipe не существует или соединение разорвано - запускаем новый процесс
            _logger.LogInformation("[VectorDB] No existing process found (IO error), starting new VectorDB...");
        }

        // Запускаем Indexer процесс
        await StartIndexerAsync(cancellationToken);

        // Подключаемся к новому процессу (увеличенный таймаут для инициализации)
        _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipeClient.ConnectAsync(60000, cancellationToken); // 60 секунд на первый запуск
        InitializeStreams();
        _logger.LogInformation("[VectorDB] Connected to new VectorDB process");
    }

    /// <summary>
    /// Инициализирует StreamReader/Writer для pipe соединения
    /// </summary>
    private void InitializeStreams() {
        _reader = new StreamReader(_pipeClient!, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_pipeClient!, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
    }
    /// <summary>
    /// Запускает Indexer процесс
    /// </summary>
    private async Task StartIndexerAsync(CancellationToken cancellationToken) {
        var currentDir = AppContext.BaseDirectory;
        var indexerPath = Path.Combine(currentDir, "UltraSharpTools.VectorDB.exe");

        if (!File.Exists(indexerPath)) {
            throw new FileNotFoundException(
                $"VectorDB executable not found: {indexerPath}. " +
                "Ensure UltraSharpTools.VectorDB.exe is in the same directory as Droid."
            );
        }

        var startInfo = new ProcessStartInfo {
            FileName = indexerPath,
            WorkingDirectory = currentDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            // НЕ перенаправляем stdout/stderr - это блокирует процесс при заполнении буфера!
            RedirectStandardError = false,
            RedirectStandardOutput = false
        };

        _indexerProcess = Process.Start(startInfo);

        if (_indexerProcess == null) {
            throw new InvalidOperationException("Failed to start VectorDB process");
        }

        _logger.LogInformation("[VectorDB] Started VectorDB process (PID: {Pid}) in {WorkingDir}",
            _indexerProcess.Id, currentDir);

        // Ждем пока VectorDB инициализируется и создаст Named Pipe
        // Инициализация может занять 10-20 секунд (Ollama, vector store)
        await Task.Delay(3000, cancellationToken);
    }    /// <summary>
         /// Отправляет запрос в Indexer и возвращает ответ
         /// </summary>
    public async Task<TResponse?> SendRequestAsync<TResponse>(
        string method,
        object? parameters,
        CancellationToken cancellationToken = default) {
        if (_pipeClient == null || !_pipeClient.IsConnected || _reader == null || _writer == null) {
            await ConnectAsync(cancellationToken);
        }

        await _requestLock.WaitAsync(cancellationToken);
        try {
            // Формируем запрос
            var request = new {
                method,
                parameters
            };

            var requestJson = JsonSerializer.Serialize(request);
            _logger.LogDebug("[VectorDB] Sending request: {Method}, JSON: {Json}", method, requestJson);

            // Отправляем запрос - используем WriteLine вместо WriteLineAsync для совместимости
            _writer!.WriteLine(requestJson);
            await _writer.FlushAsync(cancellationToken);

            _logger.LogDebug("[VectorDB] Request sent, waiting for response...");

            // Читаем ответ с таймаутом (90 секунд для больших баз данных)
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(TimeSpan.FromSeconds(90));

            string? responseLine;
            try {
                responseLine = await _reader!.ReadLineAsync(readCts.Token);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                _logger.LogError("[VectorDB] Read timeout for method {Method} - VectorDB not responding", method);
                // Закрываем соединение чтобы при следующем вызове переподключиться
                _reader?.Dispose();
                _writer?.Dispose();
                _pipeClient?.Dispose();
                _pipeClient = null;
                _reader = null;
                _writer = null;
                throw new TimeoutException($"VectorDB did not respond to '{method}' within 90 seconds");
            }

            if (responseLine == null) {
                throw new InvalidOperationException("VectorDB connection closed");
            }

            _logger.LogDebug("[VectorDB] Received response for {Method}: {Length} chars", method, responseLine.Length);

            // Парсим ответ
            return JsonSerializer.Deserialize<TResponse>(responseLine);
        } finally {
            _requestLock.Release();
        }
    }/// <summary>
     /// Индексирует код в векторную БД
     /// </summary>
    public async Task<bool> IndexCodeAsync(
        string code,
        string documentPath,
        string? metadata = null,
        CancellationToken cancellationToken = default) {
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
        CancellationToken cancellationToken = default) {
        var response = await SendRequestAsync<JsonElement>(
            "search_similar",
            new { query, topK, minSimilarity },
            cancellationToken
        );

        var matches = new List<SimilarityMatch>();

        if (response.TryGetProperty("matches", out var matchesArray)) {
            foreach (var match in matchesArray.EnumerateArray()) {
                matches.Add(new SimilarityMatch {
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
    public async Task<IndexerStatus?> GetStatusAsync(CancellationToken cancellationToken = default) {
        var response = await SendRequestAsync<JsonElement>(
            "get_status",
            null,
            cancellationToken
        );

        if (response.TryGetProperty("success", out var success) && success.GetBoolean()) {
            return new IndexerStatus {
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
    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default) {
        var response = await SendRequestAsync<JsonElement>(
            "clear",
            null,
            cancellationToken
        );

        return response.TryGetProperty("success", out var success) && success.GetBoolean();
    }

    public void Dispose() {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipeClient?.Dispose();
        _indexerProcess?.Dispose();
        _requestLock.Dispose();
    }
}

/// <summary>
/// Результат поиска семантически похожего кода
/// </summary>
public sealed record SimilarityMatch {
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required float Similarity { get; init; }
    public string? Metadata { get; init; }
}

/// <summary>
/// Статус Indexer сервиса
/// </summary>
public sealed record IndexerStatus {
    public required string Status { get; init; }
    public required int IndexedCount { get; init; }
    public required string Version { get; init; }
}

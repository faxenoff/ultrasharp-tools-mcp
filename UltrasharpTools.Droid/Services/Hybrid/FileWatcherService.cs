using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;
using UltrasharpTools.Droid.Services;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Фоновый сервис для отслеживания изменений файлов
/// </summary>
public sealed partial class FileWatcherService : BackgroundService {
    private readonly AgentConfig _config;
    private readonly IServerBridgeService _bridge;
    private readonly IEmbeddingService? _embedding;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly PowerManagementService? _powerManagement;
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private FileSystemWatcher? _watcher;

    public FileWatcherService(
        AgentConfig config,
        IServerBridgeService bridge,
        ILogger<FileWatcherService> logger,
        IEmbeddingService? embedding = null,
        PowerManagementService? powerManagement = null
    ) {
        _config = config;
        _bridge = bridge;
        _embedding = embedding;
        _logger = logger;
        _powerManagement = powerManagement;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        LogStarting(_config.RepositoryPath);

        try {
            _watcher = new FileSystemWatcher(_config.RepositoryPath) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true,
            };

            // Подписка на события
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            // Установка фильтров
            foreach (var pattern in _config.WatchPatterns) {
                _watcher.Filters.Add(pattern);
            }

            _watcher.EnableRaisingEvents = true;

            LogStarted(string.Join(", ", _config.WatchPatterns));

            // Debounce loop - обрабатываем накопленные изменения
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(_config.FileWatcherDebounceMs, stoppingToken);
                await ProcessPendingChanges(stoppingToken);
            }
        } catch (OperationCanceledException) {
            LogStopping();
        } catch (Exception ex) {
            LogServiceError(ex);
        } finally {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) {
        if (ShouldIgnoreFile(e.FullPath)) {
            return;
        }

        LogFileChange(e.ChangeType, e.FullPath);

        // Добавляем в очередь с debounce
        _pendingChanges.AddOrUpdate(e.FullPath, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e) {
        if (ShouldIgnoreFile(e.FullPath)) {
            return;
        }

        LogFileRenamed(e.OldFullPath, e.FullPath);

        _pendingChanges.AddOrUpdate(e.FullPath, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }
    private async Task ProcessPendingChanges(CancellationToken cancellationToken) {
        var now = DateTime.UtcNow;
        var debounceThreshold = TimeSpan.FromMilliseconds(_config.FileWatcherDebounceMs);

        // Собираем файлы готовые к обработке
        var readyFiles = new List<string>();
        foreach (var kvp in _pendingChanges.ToArray()) {
            var filePath = kvp.Key;
            var lastChangeTime = kvp.Value;

            // Проверяем, прошло ли достаточно времени с последнего изменения
            if (now - lastChangeTime >= debounceThreshold) {
                // Удаляем из очереди
                if (_pendingChanges.TryRemove(filePath, out _)) {
                    readyFiles.Add(filePath);
                }
            }
        }

        if (readyFiles.Count == 0) {
            return;
        }

        // Регистрируем активность - есть работа, выходим из idle режима
        _powerManagement?.RecordActivity();

        // Параллельная обработка файлов (CPU + I/O bound)
        var parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8), // Не больше 8 параллельных
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(readyFiles, parallelOptions, async (filePath, ct) => {
            await ProcessFileChange(filePath, ct);
        });
    }
    private async Task ProcessFileChange(string fullPath, CancellationToken cancellationToken) {
        try {
            // Определяем относительный путь
            var relativePath = Path.GetRelativePath(_config.RepositoryPath, fullPath);

            // Определяем действие
            var action = File.Exists(fullPath) ? "modified" : "deleted";

            // Читаем содержимое (если файл существует и не слишком большой)
            string? content = null;
            if (action == "modified") {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length < 1024 * 1024) // < 1 MB
                {
                    content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                }
            }

            // Получаем текущую ветку (упрощенно - из .git/HEAD)
            var branch = GetCurrentBranch() ?? "main";

            // Векторизация контента (если доступен embedding service и есть контент)
            float[]? vectors = null;
            if (_config.AutoVectorizeEnabled && _embedding != null && content != null) {
                try {
                    vectors = await _embedding.GetEmbeddingAsync(content, cancellationToken);
                    if (vectors != null) {
                        LogGeneratedEmbedding(vectors.Length);
                    }
                } catch (Exception ex) {
                    LogEmbeddingFailed(ex);
                }
            }

            // Извлечение символов из C# кода
            SymbolInfo[]? symbols = null;
            if (
                action == "modified"
                && content != null
                && fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ) {
                try {
                    symbols = SymbolExtractor.ExtractSymbols(content, relativePath);
                    if (symbols.Length > 0) {
                        LogExtractedSymbols(symbols.Length, relativePath);
                    }
                } catch (Exception ex) {
                    LogSymbolExtractionFailed(ex, relativePath);
                }
            }

            // Создаем событие
            var evt = new FileChangedEvent {
                Project = _config.ProjectName,
                Branch = branch,
                File = relativePath.Replace('\\', '/'),
                Action = action,
                Content = content,
                Vectors = vectors,
                Symbols = symbols,
            };

            // Отправляем на сервер
            await _bridge.SendFileChangedEventAsync(evt, cancellationToken);

            LogProcessedChange(evt.Project, evt.Branch, evt.File, evt.Action);
        } catch (Exception ex) {
            LogProcessFileFailed(ex, fullPath);
        }
    }

    private bool ShouldIgnoreFile(string path) {
        var relativePath = Path.GetRelativePath(_config.RepositoryPath, path);

        foreach (var pattern in _config.IgnorePatterns) {
            // Простая проверка паттернов (можно улучшить с glob matching)
            if (pattern.Contains("**")) {
                var dir = pattern.Replace("**", "").TrimEnd('/');
                if (relativePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            } else if (pattern.StartsWith("*.")) {
                var ext = pattern[1..];
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        }

        return false;
    }

    private string? GetCurrentBranch() {
        try {
            var gitHeadPath = Path.Combine(_config.RepositoryPath, ".git", "HEAD");
            if (!File.Exists(gitHeadPath)) {
                return null;
            }

            var headContent = File.ReadAllText(gitHeadPath).Trim();
            if (headContent.StartsWith("ref: refs/heads/")) {
                return headContent["ref: refs/heads/".Length..];
            }

            return null;
        } catch {
            return null;
        }
    }
}

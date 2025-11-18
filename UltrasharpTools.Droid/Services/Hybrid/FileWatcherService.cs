using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;
using System.Collections.Concurrent;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Фоновый сервис для отслеживания изменений файлов
/// </summary>
public sealed class FileWatcherService : BackgroundService
{
    private readonly AgentConfig _config;
    private readonly IServerBridgeService _bridge;
    private readonly IEmbeddingService? _embedding;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private FileSystemWatcher? _watcher;

    public FileWatcherService(
        AgentConfig config,
        IServerBridgeService bridge,
        ILogger<FileWatcherService> logger,
        IEmbeddingService? embedding = null)
    {
        _config = config;
        _bridge = bridge;
        _embedding = embedding;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "FileWatcherService starting for path: {Path}",
            _config.RepositoryPath);

        try
        {
            _watcher = new FileSystemWatcher(_config.RepositoryPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true
            };

            // Подписка на события
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            // Установка фильтров
            foreach (var pattern in _config.WatchPatterns)
            {
                _watcher.Filters.Add(pattern);
            }

            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation(
                "FileWatcher started. Monitoring patterns: {Patterns}",
                string.Join(", ", _config.WatchPatterns));

            // Debounce loop - обрабатываем накопленные изменения
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_config.FileWatcherDebounceMs, stoppingToken);
                await ProcessPendingChanges(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FileWatcherService stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileWatcherService error");
        }
        finally
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
        {
            return;
        }

        _logger.LogDebug(
            "File {ChangeType}: {Path}",
            e.ChangeType,
            e.FullPath);

        // Добавляем в очередь с debounce
        _pendingChanges.AddOrUpdate(
            e.FullPath,
            DateTime.UtcNow,
            (_, _) => DateTime.UtcNow);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
        {
            return;
        }

        _logger.LogDebug(
            "File renamed: {OldPath} -> {NewPath}",
            e.OldFullPath,
            e.FullPath);

        _pendingChanges.AddOrUpdate(
            e.FullPath,
            DateTime.UtcNow,
            (_, _) => DateTime.UtcNow);
    }

    private async Task ProcessPendingChanges(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var debounceThreshold = TimeSpan.FromMilliseconds(_config.FileWatcherDebounceMs);

        foreach (var kvp in _pendingChanges.ToArray())
        {
            var filePath = kvp.Key;
            var lastChangeTime = kvp.Value;

            // Проверяем, прошло ли достаточно времени с последнего изменения
            if (now - lastChangeTime < debounceThreshold)
            {
                continue;
            }

            // Удаляем из очереди
            _pendingChanges.TryRemove(filePath, out _);

            // Обрабатываем изменение
            await ProcessFileChange(filePath, cancellationToken);
        }
    }

    private async Task ProcessFileChange(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            // Определяем относительный путь
            var relativePath = Path.GetRelativePath(_config.RepositoryPath, fullPath);

            // Определяем действие
            var action = File.Exists(fullPath) ? "modified" : "deleted";

            // Читаем содержимое (если файл существует и не слишком большой)
            string? content = null;
            if (action == "modified")
            {
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
            if (_config.AutoVectorizeEnabled && _embedding != null && content != null)
            {
                try
                {
                    vectors = await _embedding.GetEmbeddingAsync(content, cancellationToken);
                    if (vectors != null)
                    {
                        _logger.LogDebug(
                            "Generated embedding: {Dimensions} dimensions",
                            vectors.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate embedding, continuing without vectors");
                }
            }

            // Извлечение символов из C# кода
            SymbolInfo[]? symbols = null;
            if (action == "modified" && content != null && fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    symbols = SymbolExtractor.ExtractSymbols(content, relativePath);
                    if (symbols.Length > 0)
                    {
                        _logger.LogDebug(
                            "Extracted {SymbolCount} symbols from {File}",
                            symbols.Length,
                            relativePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract symbols from {File}, continuing without symbols", relativePath);
                }
            }

            // Создаем событие
            var evt = new FileChangedEvent
            {
                Project = _config.ProjectName,
                Branch = branch,
                File = relativePath.Replace('\\', '/'),
                Action = action,
                Content = content,
                Vectors = vectors,
                Symbols = symbols
            };

            // Отправляем на сервер
            await _bridge.SendFileChangedEventAsync(evt, cancellationToken);

            _logger.LogInformation(
                "Processed file change: {Project}/{Branch}/{File} ({Action})",
                evt.Project,
                evt.Branch,
                evt.File,
                evt.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process file change: {Path}",
                fullPath);
        }
    }

    private bool ShouldIgnoreFile(string path)
    {
        var relativePath = Path.GetRelativePath(_config.RepositoryPath, path);

        foreach (var pattern in _config.IgnorePatterns)
        {
            // Простая проверка паттернов (можно улучшить с glob matching)
            if (pattern.Contains("**"))
            {
                var dir = pattern.Replace("**", "").TrimEnd('/');
                if (relativePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (pattern.StartsWith("*."))
            {
                var ext = pattern[1..];
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string? GetCurrentBranch()
    {
        try
        {
            var gitHeadPath = Path.Combine(_config.RepositoryPath, ".git", "HEAD");
            if (!File.Exists(gitHeadPath))
            {
                return null;
            }

            var headContent = File.ReadAllText(gitHeadPath).Trim();
            if (headContent.StartsWith("ref: refs/heads/"))
            {
                return headContent["ref: refs/heads/".Length..];
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

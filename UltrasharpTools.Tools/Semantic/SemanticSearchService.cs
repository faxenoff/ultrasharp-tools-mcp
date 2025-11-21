using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Tools.Semantic;

/// <summary>
/// Сервис для semantic code search с использованием vector embeddings.
/// Интегрирует CodeSemanticIndexer для индексации и поиска.
/// </summary>
public sealed class SemanticSearchService : IAsyncDisposable
{
    private readonly CodeSemanticIndexer _indexer;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<SemanticSearchService> _logger;
    private readonly SemanticSearchServiceConfig _config;

    private bool _isIndexed;

    /// <summary>
    /// Indicates whether semantic search is available (has valid indexer).
    /// Returns false for dummy instances when semantic mode is not configured.
    /// </summary>
    public bool IsAvailable => _indexer != null && _solutionManager != null;

    public SemanticSearchService(
        CodeSemanticIndexer indexer,
        ISolutionManager solutionManager,
        SemanticSearchServiceConfig? config = null,
        ILogger<SemanticSearchService>? logger = null
    )
    {
        _indexer = indexer;
        _solutionManager = solutionManager;
        _config = config ?? SemanticSearchServiceConfig.Default;
        _logger = logger ?? NullLogger<SemanticSearchService>.Instance;
    }

    /// <summary>
    /// Индексировать текущий solution.
    /// </summary>
    public async Task IndexCurrentSolutionAsync(CancellationToken ct = default)
    {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null)
        {
            throw new InvalidOperationException(
                "No solution loaded. Call LoadSolutionAsync first."
            );
        }

        _logger.LogInformation("Starting semantic indexing of current solution");

        await _indexer.IndexSolutionAsync(_solutionManager.CurrentWorkspace.CurrentSolution, ct);

        _isIndexed = true;

        _logger.LogInformation("Semantic indexing complete");
    }

    /// <summary>
    /// Переиндексировать конкретный project.
    /// </summary>
    public async Task ReindexProjectAsync(string projectName, CancellationToken ct = default)
    {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null)
        {
            throw new InvalidOperationException("No solution loaded.");
        }

        var project = _solutionManager.CurrentWorkspace.CurrentSolution.Projects.FirstOrDefault(p =>
            p.Name == projectName
        );

        if (project == null)
        {
            throw new ArgumentException($"Project {projectName} not found", nameof(projectName));
        }

        _logger.LogInformation("Reindexing project {ProjectName}", projectName);

        await _indexer.IndexProjectAsync(project, ct);

        _logger.LogInformation("Project {ProjectName} reindexed", projectName);
    }

    /// <summary>
    /// Инкрементально переиндексировать изменённые файлы.
    /// </summary>
    public async Task ReindexChangedFilesAsync(string[] filePaths, CancellationToken ct = default)
    {
        if (_solutionManager.CurrentWorkspace?.CurrentSolution == null)
        {
            throw new InvalidOperationException("No solution loaded.");
        }

        _logger.LogInformation("Incrementally reindexing {Count} changed files", filePaths.Length);

        var solution = _solutionManager.CurrentWorkspace.CurrentSolution;
        var documentsToReindex = new List<(Document, Compilation)>();

        // Найти документы и их компиляции
        foreach (var filePath in filePaths)
        {
            foreach (var project in solution.Projects)
            {
                var document = project.Documents.FirstOrDefault(d =>
                    string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                );

                if (document != null)
                {
                    var compilation = await project.GetCompilationAsync(ct);
                    if (compilation != null)
                    {
                        documentsToReindex.Add((document, compilation));
                    }
                    break;
                }
            }
        }

        if (documentsToReindex.Count == 0)
        {
            _logger.LogWarning("No documents found for reindexing");
            return;
        }

        // Переиндексировать документы
        foreach (var (document, compilation) in documentsToReindex)
        {
            await _indexer.ReindexDocumentAsync(document, compilation, ct);
        }

        _logger.LogInformation(
            "Incremental reindex complete: {Count} files",
            documentsToReindex.Count
        );
    }

    /// <summary>
    /// Удалить индекс для конкретных файлов (например, при удалении файлов).
    /// </summary>
    public async Task DeleteFileIndexesAsync(string[] filePaths, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting indexes for {Count} files", filePaths.Length);

        foreach (var filePath in filePaths)
        {
            await _indexer.DeleteDocumentIndexAsync(filePath, ct);
        }

        _logger.LogInformation("Deleted indexes for {Count} files", filePaths.Length);
    }

    /// <summary>
    /// Найти методы, похожие на данный код.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsAsync(
        string code,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        EnsureIndexed();

        var results = await _indexer.SearchSimilarMethodsAsync(code, limit, minSimilarity, ct);

        return results
            .Select(r => new SemanticCodeMatch
            {
                Id = r.Id,
                Code = r.Content,
                Similarity = r.Similarity,
                Rank = r.Rank,
                FilePath = ExtractFilePath(r.Metadata),
                LineNumber = ExtractLineNumber(r.Metadata),
                FullyQualifiedName = ExtractFqn(r.Metadata),
                Type = CodeMatchType.Method,
            })
            .ToList();
    }

    /// <summary>
    /// Найти классы, похожие на данный код.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarClassesAsync(
        string code,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        EnsureIndexed();

        var results = await _indexer.SearchSimilarClassesAsync(code, limit, minSimilarity, ct);

        return results
            .Select(r => new SemanticCodeMatch
            {
                Id = r.Id,
                Code = r.Content,
                Similarity = r.Similarity,
                Rank = r.Rank,
                FilePath = ExtractFilePath(r.Metadata),
                LineNumber = ExtractLineNumber(r.Metadata),
                FullyQualifiedName = ExtractFqn(r.Metadata),
                Type = CodeMatchType.Class,
            })
            .ToList();
    }

    /// <summary>
    /// Найти любой код (методы + классы), похожий на данный.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarCodeAsync(
        string code,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        EnsureIndexed();

        // Поиск методов
        var methodTask = FindSimilarMethodsAsync(code, limit, minSimilarity, ct);

        // Поиск классов
        var classTask = FindSimilarClassesAsync(code, limit, minSimilarity, ct);

        await Task.WhenAll(methodTask, classTask);

        // Объединить и отсортировать по similarity
        var combined = methodTask
            .Result.Concat(classTask.Result)
            .OrderByDescending(m => m.Similarity)
            .Take(limit)
            .ToList();

        return combined;
    }

    /// <summary>
    /// Найти методы в конкретном файле, похожие на код.
    /// </summary>
    public async Task<List<SemanticCodeMatch>> FindSimilarMethodsInFileAsync(
        string code,
        string filePath,
        int limit = 10,
        float minSimilarity = 0.7f,
        CancellationToken ct = default
    )
    {
        var allResults = await FindSimilarMethodsAsync(code, limit * 2, minSimilarity, ct);

        // Фильтровать по файлу
        return allResults
            .Where(m => m.FilePath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Получить метрики индексации.
    /// </summary>
    public CodeSemanticIndexerMetrics GetIndexerMetrics()
    {
        return _indexer.GetMetrics();
    }

    /// <summary>
    /// Проверить статус индексации.
    /// </summary>
    public bool IsIndexed() => _isIndexed;

    public async ValueTask DisposeAsync()
    {
        await _indexer.DisposeAsync();
    }

    // Private helpers

    private void EnsureIndexed()
    {
        if (!_isIndexed)
        {
            throw new InvalidOperationException(
                "Solution not indexed. Call IndexCurrentSolutionAsync first."
            );
        }
    }

    private static string? ExtractFilePath(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return null;
        }

        // Простой JSON parsing (для production лучше использовать System.Text.Json)
        var fileMatch = System.Text.RegularExpressions.Regex.Match(
            metadata,
            @"""file"":""([^""]*)"""
        );
        return fileMatch.Success ? fileMatch.Groups[1].Value : null;
    }

    private static int ExtractLineNumber(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return 0;
        }

        var lineMatch = System.Text.RegularExpressions.Regex.Match(metadata, @"""line"":(\d+)");
        return lineMatch.Success ? int.Parse(lineMatch.Groups[1].Value) : 0;
    }

    private static string? ExtractFqn(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return null;
        }

        var fqnMatch = System.Text.RegularExpressions.Regex.Match(
            metadata,
            @"""fqn"":""([^""]*)"""
        );
        return fqnMatch.Success ? fqnMatch.Groups[1].Value : null;
    }
}

/// <summary>
/// Конфигурация для SemanticSearchService.
/// </summary>
public sealed record SemanticSearchServiceConfig
{
    /// <summary>
    /// Автоматически индексировать solution при загрузке.
    /// </summary>
    public bool AutoIndexOnLoad { get; init; } = false;

    /// <summary>
    /// Переиндексировать измененные файлы автоматически.
    /// </summary>
    public bool AutoReindexOnChange { get; init; } = false;

    public static SemanticSearchServiceConfig Default => new();

    /// <summary>
    /// Конфигурация с автоматической индексацией.
    /// </summary>
    public static SemanticSearchServiceConfig WithAutoIndex =>
        new() { AutoIndexOnLoad = true, AutoReindexOnChange = true };
}

/// <summary>
/// Результат semantic code search.
/// </summary>
public sealed record SemanticCodeMatch
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required float Similarity { get; init; }
    public required int Rank { get; init; }
    public string? FilePath { get; init; }
    public int LineNumber { get; init; }
    public string? FullyQualifiedName { get; init; }
    public required CodeMatchType Type { get; init; }
}

/// <summary>
/// Тип найденного кода.
/// </summary>
public enum CodeMatchType
{
    Method,
    Class,
}

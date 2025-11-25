using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Overlord.Models.Agent;
using UltrasharpTools.Tools.Semantic;
using UltrasharpTools.Tools.Semantic.Models;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация MultiProjectVectorStoreService для hybrid архитектуры
/// Управляет векторными хранилищами для ВСЕХ проектов команды
/// </summary>
public sealed partial class MultiProjectVectorStoreService
    : IMultiProjectVectorStoreService,
        IAsyncDisposable
{
    private readonly ILogger<MultiProjectVectorStoreService> _logger;
    private readonly string _basePath;
    private readonly int _dimension;

    // Кэш VectorStore'ов для каждого проекта/ветки
    // Key: "project/branch"
    private readonly ConcurrentDictionary<string, VectorStore> _stores = new();

    public MultiProjectVectorStoreService(
        ILogger<MultiProjectVectorStoreService> logger,
        string? basePath = null,
        int dimension = 768
    )
    {
        _logger = logger;
        _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, "data", "vectors");
        _dimension = dimension;

        // Создаем базовую директорию если не существует
        Directory.CreateDirectory(_basePath);

        LogInitialized(_basePath, _dimension);
    }

    public async Task StoreVectorsAsync(
        string project,
        string branch,
        string filePath,
        float[] vectors,
        string? content = null,
        SymbolInfoDto[]? symbols = null,
        CancellationToken cancellationToken = default
    )
    {
        var store = await GetOrCreateStoreAsync(project, branch, cancellationToken);

        var embedding = new VectorEmbedding
        {
            Id = $"{project}/{branch}/{filePath}",
            Content = content ?? string.Empty,
            Vector = vectors,
            Dimension = vectors.Length,
            Metadata = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    project,
                    branch,
                    filePath,
                    timestamp = DateTime.UtcNow,
                }
            ),
        };

        await store.InsertAsync(embedding, cancellationToken);

        LogStoredVectors(project, branch, filePath);
    }

    public async Task DeleteVectorsAsync(
        string project,
        string branch,
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var key = $"{project}/{branch}";
        if (_stores.TryGetValue(key, out var store))
        {
            // TODO: Реализовать удаление в VectorStore
            // Сейчас VectorStore не имеет метода Delete
            LogDeleteNotImplemented(project, branch, filePath);
        }
    }

    public async Task<List<VectorMatch>> SearchAcrossProjectsAsync(
        float[] queryVector,
        double threshold = 0.7,
        int limit = 10,
        string[]? projects = null,
        CancellationToken cancellationToken = default
    )
    {
        var allMatches = new List<VectorMatch>();

        // Определяем какие stores искать
        var storesToSearch =
            projects == null
                ? _stores.Values
                : _stores
                    .Where(kv => projects.Any(p => kv.Key.StartsWith(p + "/")))
                    .Select(kv => kv.Value);

        // Параллельный поиск по всем stores
        var searchTasks = storesToSearch.Select(async store =>
        {
            try
            {
                var results = await store.SearchAsync(
                    queryVector,
                    limit,
                    (float)threshold,
                    cancellationToken
                );

                return results
                    .Select(r =>
                    {
                        var metadata = ParseMetadata(r.Metadata);
                        return new VectorMatch
                        {
                            Project = metadata.GetValueOrDefault("project") ?? "unknown",
                            Branch = metadata.GetValueOrDefault("branch") ?? "unknown",
                            FilePath = metadata.GetValueOrDefault("filePath") ?? "unknown",
                            Line = 0, // TODO: извлечь из metadata
                            Similarity = r.Similarity,
                            Code = r.Content,
                        };
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                LogSearchFailed(ex);
                return new List<VectorMatch>();
            }
        });

        var results = await Task.WhenAll(searchTasks);

        // Объединяем результаты и сортируем по similarity
        foreach (var result in results)
        {
            allMatches.AddRange(result);
        }

        return allMatches.OrderByDescending(m => m.Similarity).Take(limit).ToList();
    }

    public async Task<List<VectorMatch>> SearchInProjectAsync(
        string project,
        string branch,
        float[] queryVector,
        double threshold = 0.7,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var store = await GetOrCreateStoreAsync(project, branch, cancellationToken);

        var results = await store.SearchAsync(
            queryVector,
            limit,
            (float)threshold,
            cancellationToken
        );

        return results
            .Select(r =>
            {
                var metadata = ParseMetadata(r.Metadata);
                return new VectorMatch
                {
                    Project = project,
                    Branch = branch,
                    FilePath = metadata.GetValueOrDefault("filePath") ?? "unknown",
                    Line = 0,
                    Similarity = r.Similarity,
                    Code = r.Content,
                };
            })
            .ToList();
    }

    public Task<List<string>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = _stores.Keys.Select(key => key.Split('/')[0]).Distinct().ToList();

        return Task.FromResult(projects);
    }

    public Task<List<string>> GetBranchesAsync(
        string project,
        CancellationToken cancellationToken = default
    )
    {
        var branches = _stores
            .Keys.Where(key => key.StartsWith(project + "/"))
            .Select(key => key.Split('/')[1])
            .Distinct()
            .ToList();

        return Task.FromResult(branches);
    }

    public async Task<MultiProjectStats> GetStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var projectStats = new Dictionary<string, ProjectStats>();
        var projects = await GetProjectsAsync(cancellationToken);

        foreach (var project in projects)
        {
            var branches = await GetBranchesAsync(project, cancellationToken);

            // TODO: Получить реальную статистику из VectorStore
            projectStats[project] = new ProjectStats
            {
                Name = project,
                BranchCount = branches.Count,
                VectorCount = 0, // TODO
                SizeMB = 0, // TODO
                LastUpdate = DateTime.UtcNow,
            };
        }

        return new MultiProjectStats
        {
            TotalProjects = projects.Count,
            TotalBranches = _stores.Count,
            TotalVectors = 0, // TODO
            TotalSizeMB = 0, // TODO
            Projects = projectStats,
        };
    }

    private async Task<VectorStore> GetOrCreateStoreAsync(
        string project,
        string branch,
        CancellationToken cancellationToken
    )
    {
        var key = $"{project}/{branch}";

        if (_stores.TryGetValue(key, out var existingStore))
        {
            return existingStore;
        }

        // Создаем новый VectorStore
        var dbPath = Path.Combine(_basePath, project, branch, "vectors.db");
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        var store = new VectorStore(logger: _logger as ILogger<VectorStore>);
        await store.InitializeAsync($"Data Source={dbPath}", _dimension, cancellationToken);

        _stores[key] = store;

        LogStoreCreated(project, branch);

        return store;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var store in _stores.Values)
        {
            await store.DisposeAsync();
        }
        _stores.Clear();
    }

    private static Dictionary<string, string> ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            var result = new Dictionary<string, string>();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ToString();
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}

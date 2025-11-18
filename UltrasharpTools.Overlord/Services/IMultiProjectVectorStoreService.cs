using UltrasharpTools.Overlord.Models.Agent;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для хранения векторов ВСЕХ проектов команды
/// </summary>
public interface IMultiProjectVectorStoreService
{
    /// <summary>
    /// Сохранить векторы для файла в конкретном проекте/ветке
    /// </summary>
    Task StoreVectorsAsync(
        string project,
        string branch,
        string filePath,
        float[] vectors,
        string? content = null,
        SymbolInfoDto[]? symbols = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удалить векторы для файла
    /// </summary>
    Task DeleteVectorsAsync(
        string project,
        string branch,
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Поиск похожих векторов ПО ВСЕМ проектам команды
    /// </summary>
    Task<List<VectorMatch>> SearchAcrossProjectsAsync(
        float[] queryVector,
        double threshold = 0.7,
        int limit = 10,
        string[]? projects = null, // null = all projects
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Поиск похожих векторов в конкретном проекте/ветке
    /// </summary>
    Task<List<VectorMatch>> SearchInProjectAsync(
        string project,
        string branch,
        float[] queryVector,
        double threshold = 0.7,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить список всех проектов
    /// </summary>
    Task<List<string>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить список веток для проекта
    /// </summary>
    Task<List<string>> GetBranchesAsync(string project, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить статистику по проектам
    /// </summary>
    Task<MultiProjectStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат поиска вектора
/// </summary>
public sealed class VectorMatch
{
    public required string Project { get; init; }
    public required string Branch { get; init; }
    public required string FilePath { get; init; }
    public int Line { get; init; }
    public double Similarity { get; init; }
    public string? Code { get; init; }
    public SymbolInfoDto? Symbol { get; init; }
}

/// <summary>
/// Статистика по всем проектам
/// </summary>
public sealed class MultiProjectStats
{
    public int TotalProjects { get; init; }
    public int TotalBranches { get; init; }
    public long TotalVectors { get; init; }
    public long TotalSizeMB { get; init; }
    public Dictionary<string, ProjectStats> Projects { get; init; } = new();
}

/// <summary>
/// Статистика по проекту
/// </summary>
public sealed class ProjectStats
{
    public required string Name { get; init; }
    public int BranchCount { get; init; }
    public long VectorCount { get; init; }
    public long SizeMB { get; init; }
    public DateTime LastUpdate { get; init; }
}

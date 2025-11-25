using System.Security.Cryptography;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Manages persistent caching of FastSymbolIndex to disk
/// Provides 10x faster solution loading (33s → 3-5s)
/// </summary>
public partial class SymbolCacheManager
{
    private readonly ILogger _logger;
    private const int CacheFormatVersion = 1;
    private const string CacheFileExtension = ".symbolcache.json";
    private readonly string _cacheDirectory;

    public SymbolCacheManager(ILogger logger, string? cacheDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDirectory = cacheDirectory ?? ProjectPathHelper.GetSymbolCachePath();

        // Ensure cache directory exists
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            LogCacheDirectoryCreated(_cacheDirectory);
        }
    }

    /// <summary>
    /// Get cache file path for a solution
    /// </summary>
    private string GetCacheFilePath(string solutionPath)
    {
        var solutionHash = ComputeFileHash(solutionPath);
        var fileName =
            $"{Path.GetFileNameWithoutExtension(solutionPath)}_{solutionHash}{CacheFileExtension}";
        return Path.Combine(_cacheDirectory, fileName);
    }

    /// <summary>
    /// Try to load cached symbol index for a solution
    /// </summary>
    public async Task<(
        bool success,
        List<SerializableSymbolEntry>? entries,
        SymbolCacheMetadata? metadata
    )> TryLoadCacheAsync(Solution solution, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(solution.FilePath))
            {
                LogNoSolutionPathForLoad();
                return (false, null, null);
            }

            var cacheFilePath = GetCacheFilePath(solution.FilePath);

            if (!File.Exists(cacheFilePath))
            {
                LogNoCacheFile(cacheFilePath);
                return (false, null, null);
            }

            LogLoadingCache(cacheFilePath);

            var json = await OptimizedFileIO.ReadAllTextAsync(cacheFilePath, null, cancellationToken);
            var cacheData = JsonSerializer.Deserialize<SymbolCacheData>(json);

            if (cacheData == null)
            {
                LogDeserializationFailed();
                return (false, null, null);
            }

            // Validate cache version
            if (cacheData.Metadata.Version != CacheFormatVersion)
            {
                LogVersionMismatch(CacheFormatVersion, cacheData.Metadata.Version);
                return (false, null, null);
            }

            // Validate solution hasn't changed
            if (!await ValidateSolutionAsync(solution, cacheData.Metadata, cancellationToken))
            {
                LogSolutionChanged();
                return (false, null, null);
            }

            LogSymbolsLoaded(cacheData.Symbols.Count);
            return (true, cacheData.Symbols, cacheData.Metadata);
        }
        catch (Exception ex)
        {
            LogLoadError(ex);
            return (false, null, null);
        }
    }

    /// <summary>
    /// Save symbol index to cache
    /// </summary>
    public async Task SaveCacheAsync(
        Solution solution,
        List<SymbolIndexEntry> entries,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (string.IsNullOrEmpty(solution.FilePath))
            {
                LogNoSolutionPathForSave();
                return;
            }

            var cacheFilePath = GetCacheFilePath(solution.FilePath);

            LogSavingCache(entries.Count, cacheFilePath);

            var metadata = await BuildMetadataAsync(solution, entries.Count, cancellationToken);
            var serializableEntries = await ConvertToSerializableAsync(
                solution,
                entries,
                cancellationToken
            );

            var cacheData = new SymbolCacheData
            {
                Metadata = metadata,
                Symbols = serializableEntries,
            };

            var json = JsonSerializer.Serialize(
                cacheData,
                new JsonSerializerOptions
                {
                    WriteIndented = false, // Compact format for faster I/O
                }
            );

            await OptimizedFileIO.WriteAllTextAsync(cacheFilePath, json, null, cancellationToken);

            LogCacheSaved(json.Length);
        }
        catch (Exception ex)
        {
            LogSaveError(ex);
        }
    }

    /// <summary>
    /// Convert SymbolIndexEntry list to serializable format
    /// </summary>
    private async Task<List<SerializableSymbolEntry>> ConvertToSerializableAsync(
        Solution solution,
        List<SymbolIndexEntry> entries,
        CancellationToken cancellationToken
    )
    {
        var serializableEntries = new List<SerializableSymbolEntry>(entries.Count);

        // Build project name → assembly name mapping
        var projectAssemblyMap = new Dictionary<string, string>();
        foreach (var project in solution.Projects)
        {
            if (!string.IsNullOrEmpty(project.AssemblyName))
            {
                projectAssemblyMap[project.Name] = project.AssemblyName;
            }
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Try to find which project this symbol belongs to
            var projectName = FindProjectForSymbol(entry.Symbol, solution);
            var assemblyName =
                projectName != null && projectAssemblyMap.TryGetValue(projectName, out var asm)
                    ? asm
                    : entry.Symbol.ContainingAssembly?.Name ?? "Unknown";

            serializableEntries.Add(
                SerializableSymbolEntry.FromIndexEntry(
                    entry,
                    projectName ?? "Unknown",
                    assemblyName
                )
            );
        }

        return await Task.FromResult(serializableEntries);
    }

    /// <summary>
    /// Find which project a symbol belongs to
    /// </summary>
    private string? FindProjectForSymbol(ISymbol symbol, Solution solution)
    {
        // Try to find the project by examining the symbol's containing assembly
        var assemblyName = symbol.ContainingAssembly?.Name;
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        var project = solution.Projects.FirstOrDefault(p => p.AssemblyName == assemblyName);
        return project?.Name;
    }

    /// <summary>
    /// Build cache metadata for current solution
    /// </summary>
    private async Task<SymbolCacheMetadata> BuildMetadataAsync(
        Solution solution,
        int symbolCount,
        CancellationToken cancellationToken
    )
    {
        var metadata = new SymbolCacheMetadata
        {
            Version = CacheFormatVersion,
            Created = DateTimeOffset.UtcNow,
            SolutionPath = solution.FilePath!,
            SolutionHash = ComputeFileHash(solution.FilePath!),
            SymbolCount = symbolCount,
        };

        // Build project metadata
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(project.FilePath))
                continue;

            var projectInfo = new ProjectCacheInfo
            {
                Name = project.Name,
                Path = project.FilePath,
                Hash = ComputeFileHash(project.FilePath),
                AssemblyName = project.AssemblyName ?? project.Name,
                LastWriteTime = File.GetLastWriteTimeUtc(project.FilePath),
            };

            metadata.Projects.Add(projectInfo);
        }

        return await Task.FromResult(metadata);
    }

    /// <summary>
    /// Validate that solution hasn't changed since cache was created
    /// </summary>
    private async Task<bool> ValidateSolutionAsync(
        Solution solution,
        SymbolCacheMetadata metadata,
        CancellationToken cancellationToken
    )
    {
        // Check solution hash
        if (string.IsNullOrEmpty(solution.FilePath))
            return false;

        var currentSolutionHash = ComputeFileHash(solution.FilePath);
        if (currentSolutionHash != metadata.SolutionHash)
        {
            LogSolutionHashMismatch();
            return false;
        }

        // Check project count
        if (solution.Projects.Count() != metadata.Projects.Count)
        {
            LogProjectCountMismatch(metadata.Projects.Count, solution.Projects.Count());
            return false;
        }

        // Check each project
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(project.FilePath))
                continue;

            var cachedProject = metadata.Projects.FirstOrDefault(p => p.Name == project.Name);
            if (cachedProject == null)
            {
                LogProjectNotInCache(project.Name);
                return false;
            }

            // Quick check: last write time
            var currentLastWrite = File.GetLastWriteTimeUtc(project.FilePath);
            if (currentLastWrite != cachedProject.LastWriteTime)
            {
                LogProjectModified(project.Name);
                return false;
            }

            // Detailed check: file hash (only if timestamp matches but we want to be sure)
            var currentHash = ComputeFileHash(project.FilePath);
            if (currentHash != cachedProject.Hash)
            {
                LogProjectHashMismatch(project.Name);
                return false;
            }
        }

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Compute SHA256 hash of a file
    /// </summary>
    private string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Delete all cached files (for cleanup/debugging) - optimized with parallel deletion
    /// </summary>
    public void ClearAllCaches()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var files = Directory.GetFiles(_cacheDirectory, $"*{CacheFileExtension}");
                var deletedCount = 0;

                // Параллельное удаление файлов (4 потока)
                Parallel.ForEach(
                    files,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    file =>
                    {
                        try
                        {
                            File.Delete(file);
                            Interlocked.Increment(ref deletedCount);
                        }
                        catch (Exception ex)
                        {
                            LogDeleteFailed(ex, file);
                        }
                    }
                );

                LogCacheCleared(deletedCount);
            }
        }
        catch (Exception ex)
        {
            LogClearError(ex);
        }
    }
}

/// <summary>
/// Container for serialized cache data
/// </summary>
internal class SymbolCacheData
{
    [System.Text.Json.Serialization.JsonPropertyName("metadata")]
    public SymbolCacheMetadata Metadata { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("symbols")]
    public List<SerializableSymbolEntry> Symbols { get; set; } = new();
}

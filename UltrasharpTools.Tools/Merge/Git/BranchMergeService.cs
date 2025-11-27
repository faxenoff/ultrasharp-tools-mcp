using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Git;

/// <summary>
/// Сервис для semantic merge веток.
/// Упрощает работу - принимает названия веток вместо путей к worktrees.
/// </summary>
public sealed class BranchMergeService {
    private readonly SemanticMergeService _mergeService;
    private readonly ILogger<BranchMergeService> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public BranchMergeService(
    SemanticMergeService mergeService,
    ILoggerFactory? loggerFactory = null,
    ILogger<BranchMergeService>? logger = null) {
        _mergeService = mergeService;
        _loggerFactory = loggerFactory;
        _logger = logger ?? NullLogger<BranchMergeService>.Instance;
    }
    public async Task<BranchMergeResult> MergeBranchesAsync(
        string repositoryPath,
        string sourceBranch,
        string targetBranch,
        string? instructions = null,
        string filePatterns = "*",
        bool applyChanges = true,
        CancellationToken ct = default) {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogDebug("[MERGE] === Starting MergeBranchesAsync ===");
        _logger.LogDebug("[MERGE] Repository: {Repo}", repositoryPath);
        _logger.LogDebug("[MERGE] Source: {Source}, Target: {Target}", sourceBranch, targetBranch);
        _logger.LogDebug("[MERGE] FilePatterns: {Patterns}, ApplyChanges: {Apply}", filePatterns, applyChanges);

        var gitLogger = _loggerFactory?.CreateLogger<GitBranchReader>();
        var gitReader = new GitBranchReader(repositoryPath, gitLogger);
        var parsedInstructions = MergeInstructionsParser.Parse(instructions);

        _logger.LogDebug(
            "[MERGE] Starting branch merge: {Source} → {Target} in {Repo}",
            sourceBranch, targetBranch, repositoryPath);

        // 1. Проверить существование веток
        _logger.LogDebug("[MERGE] Step 1: Checking if branches exist...");
        if (!await gitReader.BranchExistsAsync(sourceBranch, ct)) {
            _logger.LogWarning("[MERGE] Source branch not found: {Branch}", sourceBranch);
            return BranchMergeResult.Error($"Source branch '{sourceBranch}' not found");
        }

        if (!await gitReader.BranchExistsAsync(targetBranch, ct)) {
            _logger.LogWarning("[MERGE] Target branch not found: {Branch}", targetBranch);
            return BranchMergeResult.Error($"Target branch '{targetBranch}' not found");
        }

        // 2. Найти merge-base
        _logger.LogDebug("[MERGE] Step 2: Finding merge-base...");
        var mergeBase = await gitReader.FindMergeBaseAsync(sourceBranch, targetBranch, ct);
        if (string.IsNullOrEmpty(mergeBase)) {
            _logger.LogWarning("[MERGE] Cannot find merge-base between {Source} and {Target}", sourceBranch, targetBranch);
            return BranchMergeResult.Error($"Cannot find common ancestor between '{sourceBranch}' and '{targetBranch}'");
        }

        var baseShort = await gitReader.GetShortHashAsync(mergeBase, ct) ?? mergeBase[..7];
        _logger.LogDebug("[MERGE] Merge base found: {Hash}", baseShort);

        // Получить SHA для веток (для кэширования)
        var sourceShort = await gitReader.GetShortHashAsync(sourceBranch, ct) ?? sourceBranch;
        var targetShort = await gitReader.GetShortHashAsync(targetBranch, ct) ?? targetBranch;
        _logger.LogDebug("[MERGE] Branch SHAs: source={SourceSha}, target={TargetSha}", sourceShort, targetShort);

        // 3. Получить список изменённых файлов СО СТАТУСОМ
        _logger.LogDebug("[MERGE] Step 3: Getting changed files with status...");
        var changesInSource = await gitReader.GetChangedFilesWithStatusAsync(mergeBase, sourceBranch, ct);
        var changesInTarget = await gitReader.GetChangedFilesWithStatusAsync(mergeBase, targetBranch, ct);
        _logger.LogDebug("[MERGE] Changed files: source={SourceCount}, target={TargetCount}", changesInSource.Count, changesInTarget.Count);

        // Построить словари статусов для быстрого поиска
        var sourceStatuses = changesInSource.ToDictionary(c => c.Path, c => c.Status);
        var targetStatuses = changesInTarget.ToDictionary(c => c.Path, c => c.Status);

        // Все изменённые файлы (объединение)
        var allChangedFiles = changesInSource.Select(c => c.Path)
            .Union(changesInTarget.Select(c => c.Path))
            .Where(f => MatchesFilePattern(f, filePatterns))
            .Where(f => !parsedInstructions.ShouldExcludeFile(f))
            .Where(f => parsedInstructions.ShouldIncludeFile(f))
            .Distinct()
            .ToList();

        // 3.5. Разделить файлы по категориям
        var binaryFiles = allChangedFiles.Where(IsBinaryFile).ToList();
        var semanticFiles = allChangedFiles.Where(f => !IsBinaryFile(f) && SupportsSemanticMerge(f)).ToList();
        var textFiles = allChangedFiles.Where(f => !IsBinaryFile(f) && !SupportsSemanticMerge(f)).ToList();

        _logger.LogDebug("[MERGE] File categories: binary={Binary} (skipped), semantic={Semantic}, text={Text}",
            binaryFiles.Count, semanticFiles.Count, textFiles.Count);

        if (binaryFiles.Count > 0) {
            _logger.LogWarning("[MERGE] Skipping {Count} binary files: {Files}",
                binaryFiles.Count, string.Join(", ", binaryFiles.Take(5)));
        }

        // Исключить binary файлы из обработки
        allChangedFiles = semanticFiles.Concat(textFiles).ToList();

        // Определить какие файлы существуют в каждой версии:
        var filesInBase = allChangedFiles
            .Where(f => sourceStatuses.GetValueOrDefault(f) != GitFileStatus.Added
                     && targetStatuses.GetValueOrDefault(f) != GitFileStatus.Added)
            .ToList();

        var filesInSource = allChangedFiles
            .Where(f => sourceStatuses.GetValueOrDefault(f) != GitFileStatus.Deleted)
            .ToList();

        var filesInTarget = allChangedFiles
            .Where(f => targetStatuses.GetValueOrDefault(f) != GitFileStatus.Deleted)
            .ToList();

        _logger.LogDebug(
            "[MERGE] Files by version: base={BaseCount}, source={SourceCount}, target={TargetCount}, total={TotalCount}",
            filesInBase.Count, filesInSource.Count, filesInTarget.Count, allChangedFiles.Count);

        if (allChangedFiles.Count == 0) {
            var summary = binaryFiles.Count > 0
                ? $"No mergeable files (skipped {binaryFiles.Count} binary files)"
                : "No files to merge after applying filters";
            _logger.LogDebug("[MERGE] {Summary}", summary);
            return new BranchMergeResult {
                Success = true,
                Summary = summary,
                ChangedFiles = new List<string>(),
                Conflicts = new List<MergeConflictInfo>(),
                Actions = new List<MergeActionInfo>(),
            };
        }

        // 4. Создать временные директории
        var tempDir = Path.Combine(Path.GetTempPath(), $"semantic-merge-{Guid.NewGuid():N}");
        _logger.LogDebug("[MERGE] Step 4: Creating temp directories at {TempDir}", tempDir);
        try {
            var baseDir = Path.Combine(tempDir, "base");
            var sourceDir = Path.Combine(tempDir, "source");
            var targetDir = Path.Combine(tempDir, "target");

            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);

            // Извлечь файлы из каждой версии
            _logger.LogDebug("[MERGE] Extracting files from branches (batch mode)...");
            var baseExtracted = await gitReader.ExtractFilesToDirectoryAsync(mergeBase, filesInBase, baseDir, ct);
            var sourceExtracted = await gitReader.ExtractFilesToDirectoryAsync(sourceBranch, filesInSource, sourceDir, ct);
            var targetExtracted = await gitReader.ExtractFilesToDirectoryAsync(targetBranch, filesInTarget, targetDir, ct);
            _logger.LogDebug("[MERGE] Extracted files: base={Base}, source={Source}, target={Target}",
                baseExtracted, sourceExtracted, targetExtracted);

            // 5. Гибридный merge: сначала traditional для text, затем semantic
            var traditionalMergedFiles = new Dictionary<string, string>(); // path -> merged content
            var filesForSemanticMerge = new List<string>(semanticFiles); // C# файлы всегда идут на semantic

            _logger.LogDebug("[MERGE] Step 5: Trying traditional merge for {Count} text files...", textFiles.Count);
            foreach (var file in textFiles) {
                var basePath = Path.Combine(baseDir, file.Replace('/', Path.DirectorySeparatorChar));
                var sourcePath = Path.Combine(sourceDir, file.Replace('/', Path.DirectorySeparatorChar));
                var targetPath = Path.Combine(targetDir, file.Replace('/', Path.DirectorySeparatorChar));
                var traditionalResult = await TryTraditionalMergeAsync(basePath, sourcePath, targetPath, ct);

                if (traditionalResult.Success && !traditionalResult.HasConflicts && traditionalResult.MergedContent != null) {
                    _logger.LogDebug("[MERGE] Traditional merge succeeded for: {File}", file);
                    traditionalMergedFiles[file] = traditionalResult.MergedContent;
                } else {
                    _logger.LogDebug("[MERGE] Traditional merge failed for {File}: {Reason}, falling back to semantic",
                        file, traditionalResult.ErrorMessage ?? "conflicts");
                    filesForSemanticMerge.Add(file);
                }
            }

            _logger.LogDebug("[MERGE] Traditional merge: {Success} succeeded, {Fallback} need semantic merge",
                traditionalMergedFiles.Count, filesForSemanticMerge.Count);

            // 6. Semantic merge для C# и файлов с конфликтами
            MergeResult? semanticResult = null;
            if (filesForSemanticMerge.Count > 0) {
                _logger.LogDebug("[MERGE] Step 6: Starting SemanticMergeService.MergeAsync for {Count} files...",
                    filesForSemanticMerge.Count);

                // Фильтруем паттерны только для файлов которые нужны для semantic merge
                var semanticPatterns = filesForSemanticMerge
                    .Select(f => Path.GetExtension(f))
                    .Distinct()
                    .Select(ext => $"*{ext}")
                    .ToArray();

                var request = new IndexingRequest {
                    BaseDirectory = baseDir,
                    BranchADirectory = sourceDir,
                    BranchBDirectory = targetDir,
                    FilePatterns = semanticPatterns,
                    BaseCommitSha = baseShort,
                    BranchACommitSha = sourceShort,
                    BranchBCommitSha = targetShort,
                    BaseBranch = "merge-base",
                    BranchA = sourceBranch,
                    BranchB = targetBranch,
                };

                semanticResult = await _mergeService.MergeAsync(request, ct);

                // Применить инструкции к результату
                if (parsedInstructions.AutoResolveConflicts) {
                    semanticResult = ApplyInstructionsToResult(semanticResult, parsedInstructions);
                }
            }

            // 7. Объединить результаты
            var allActions = new List<MergeActionInfo>();

            // Добавить традиционно смердженные файлы как actions
            foreach (var (filePath, content) in traditionalMergedFiles) {
                allActions.Add(new MergeActionInfo {
                    FilePath = filePath,
                    ActionType = "Modify",
                    Source = "traditional",
                    Confidence = 1.0f,
                });
            }

            // Добавить semantic merge results
            if (semanticResult != null) {
                allActions.AddRange(semanticResult.Actions.Select(a => new MergeActionInfo {
                    FilePath = a.TargetPath,
                    ActionType = a.Type.ToString(),
                    Source = a.Source,
                    Confidence = a.Confidence,
                }));
            }

            var conflicts = semanticResult?.Conflicts.Select(c => new MergeConflictInfo {
                FilePath = c.BaseUnit?.FilePath ?? "",
                Symbol = c.BaseUnit?.FullyQualifiedName ?? "",
                ConflictType = c.ConflictType.ToString(),
                Description = c.Description,
                Severity = c.Severity.ToString(),
            }).ToList() ?? new List<MergeConflictInfo>();

            var result = new BranchMergeResult {
                Success = true,
                MergeBase = baseShort,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                Instructions = parsedInstructions,
                ChangedFiles = allChangedFiles,
                Actions = allActions,
                Conflicts = conflicts,
                Statistics = new MergeStatisticsInfo {
                    TotalChanges = allActions.Count + conflicts.Count,
                    AutoMerged = allActions.Count,
                    Conflicts = conflicts.Count,
                    FastPathMatches = semanticResult?.Statistics.FastPathMatches ?? 0,
                    SlowPathMatches = semanticResult?.Statistics.SlowPathMatches ?? 0,
                    MergeTimeMs = sw.ElapsedMilliseconds,
                },
            };

            // 8. Применить изменения
            if (applyChanges && result.Success && result.Conflicts.Count == 0) {
                // Записать традиционно смердженные файлы
                foreach (var (filePath, content) in traditionalMergedFiles) {
                    var fullPath = Path.Combine(repositoryPath, filePath.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(fullPath, content, ct);
                }

                // Записать semantic merge результаты
                if (semanticResult != null) {
                    await ApplyMergeResultAsync(gitReader, repositoryPath, semanticResult, ct);
                }

                var binaryNote = binaryFiles.Count > 0 ? $" ({binaryFiles.Count} binary files skipped)" : "";
                result.Summary = $"Merged {result.Actions.Count} changes from {sourceBranch} to {targetBranch}{binaryNote}. " +
                    $"Traditional: {traditionalMergedFiles.Count}, Semantic: {(semanticResult?.Actions.Count ?? 0)}. " +
                    $"Files written as unstaged changes. Use 'git diff' to review.";
            } else if (result.Conflicts.Count > 0) {
                result.Summary = $"Merge has {result.Conflicts.Count} conflicts that need manual resolution. " +
                    $"Review conflicts and resolve before applying.";
            } else {
                result.Summary = $"Dry run: {result.Actions.Count} changes would be merged. " +
                    $"Set applyChanges=true to apply.";
            }

            sw.Stop();
            _logger.LogDebug("[MERGE] Merge completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return result;
        } catch (Exception ex) {
            sw.Stop();
            _logger.LogError(ex, "[MERGE] Exception in MergeBranchesAsync after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        } finally {
            // Cleanup temp directory
            try {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            } catch { /* ignore cleanup errors */ }
        }
    }
    private async Task WriteFilesFromBranchAsync(
            GitBranchReader gitReader,
            string branchOrCommit,
            List<string> files,
            string targetDir,
            CancellationToken ct) {
        _logger.LogDebug("[MERGE] WriteFilesFromBranchAsync: {Branch}, {Count} files", branchOrCommit, files.Count);
        var written = 0;
        var skipped = 0;
        foreach (var file in files) {
            ct.ThrowIfCancellationRequested();
            try {
                var content = await gitReader.GetFileContentAsync(branchOrCommit, file, ct);
                if (content != null) {
                    var targetPath = Path.Combine(targetDir, file.Replace('/', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(targetPath, content, ct);
                    written++;
                } else {
                    skipped++;
                    _logger.LogDebug("[MERGE] File not found in {Branch}: {File}", branchOrCommit, file);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "[MERGE] Error reading file {File} from {Branch}", file, branchOrCommit);
                skipped++;
            }
        }
        _logger.LogDebug("[MERGE] WriteFilesFromBranchAsync completed: {Written} written, {Skipped} skipped", written, skipped);
    }

    private MergeResult ApplyInstructionsToResult(MergeResult result, MergeInstructions instructions) {
        var filteredConflicts = new List<SemanticConflict>();

        foreach (var conflict in result.Conflicts) {
            var priority = instructions.GetBranchPriority(
            conflict.BaseUnit?.FilePath ?? "",
            conflict.VersionA?.Content ?? conflict.VersionB?.Content);

            if (priority == BranchPriority.PreferSource || priority == BranchPriority.PreferTarget) {
                // Авто-разрешён по инструкции
                _logger.LogDebug("Auto-resolved conflict by instruction: {Path}", conflict.BaseUnit?.FilePath);
                continue;
            }

            filteredConflicts.Add(conflict);
        }

        return result with { Conflicts = filteredConflicts };
    }

    private async Task ApplyMergeResultAsync(
    GitBranchReader gitReader,
    string repositoryPath,
    MergeResult mergeResult,
    CancellationToken ct) {
        foreach (var action in mergeResult.Actions) {
            if (action.Type == MergeActionType.Delete) {
                _logger.LogInformation("Would delete: {Path}", action.TargetPath);
                continue;
            }

            if (!string.IsNullOrEmpty(action.Content) && !string.IsNullOrEmpty(action.TargetPath)) {
                var fullPath = Path.Combine(repositoryPath, action.TargetPath.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(fullPath, action.Content, ct);
            }
        }
    }

    private static bool MatchesFilePattern(string filePath, string pattern) {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
            return true;

        var patterns = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in patterns) {
            var trimmed = p.Trim();

            if (trimmed.StartsWith("*.")) {
                var ext = trimmed.Substring(1);
                if (filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            } else if (filePath.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Расширения файлов, которые считаются бинарными и не подлежат merge.
    /// </summary>
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase) {
    // Images
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp", ".tiff", ".psd",
    // Archives
    ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
    // Binaries
    ".exe", ".dll", ".so", ".dylib", ".bin", ".obj", ".o", ".a", ".lib",
    // Documents
    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
    // Media
    ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".wav", ".flac", ".ogg",
    // Fonts
    ".ttf", ".otf", ".woff", ".woff2", ".eot",
    // Other
    ".db", ".sqlite", ".mdb", ".snk", ".pfx", ".p12",
};

    /// <summary>
    /// Расширения файлов, которые поддерживают семантический merge (Roslyn).
    /// </summary>
    private static readonly HashSet<string> SemanticMergeExtensions = new(StringComparer.OrdinalIgnoreCase) {
    ".cs",
};

    /// <summary>
    /// Проверяет, является ли файл бинарным по расширению.
    /// </summary>
    private static bool IsBinaryFile(string filePath) {
        var ext = Path.GetExtension(filePath);
        return BinaryExtensions.Contains(ext);
    }

    /// <summary>
    /// Проверяет, поддерживает ли файл семантический merge.
    /// </summary>
    private static bool SupportsSemanticMerge(string filePath) {
        var ext = Path.GetExtension(filePath);
        return SemanticMergeExtensions.Contains(ext);
    }/// <summary>
     /// Результат традиционного 3-way merge.
     /// </summary>
    private record TraditionalMergeResult(
        bool Success,
        bool HasConflicts,
        string? MergedContent,
        string? ErrorMessage
    );

    /// <summary>
    /// Выполняет традиционный 3-way merge для текстового файла через git merge-file.
    /// </summary>
    private async Task<TraditionalMergeResult> TryTraditionalMergeAsync(
        string baseFilePath,
        string sourceFilePath,
        string targetFilePath,
        CancellationToken ct) {
        try {
            // Проверяем наличие файлов
            var baseExists = File.Exists(baseFilePath);
            var sourceExists = File.Exists(sourceFilePath);
            var targetExists = File.Exists(targetFilePath);

            // Если файл добавлен только в одной ветке - просто берём его
            if (!baseExists && sourceExists && !targetExists) {
                return new TraditionalMergeResult(true, false, await File.ReadAllTextAsync(sourceFilePath, ct), null);
            }
            if (!baseExists && !sourceExists && targetExists) {
                return new TraditionalMergeResult(true, false, await File.ReadAllTextAsync(targetFilePath, ct), null);
            }
            if (!baseExists && sourceExists && targetExists) {
                // Оба добавили файл - конфликт, нужен semantic merge
                return new TraditionalMergeResult(false, true, null, "File added in both branches");
            }

            // Если файл удалён в одной ветке
            if (baseExists && !sourceExists && targetExists) {
                // Удалён в source - конфликт modify/delete
                return new TraditionalMergeResult(false, true, null, "File deleted in source but modified in target");
            }
            if (baseExists && sourceExists && !targetExists) {
                // Удалён в target - конфликт modify/delete
                return new TraditionalMergeResult(false, true, null, "File deleted in target but modified in source");
            }

            // Стандартный случай: файл существует во всех трёх версиях
            if (!baseExists || !sourceExists || !targetExists) {
                return new TraditionalMergeResult(false, false, null, "Missing files for merge");
            }

            // Создаём временные файлы для git merge-file (он модифицирует первый файл in-place)
            var tempDir = Path.Combine(Path.GetTempPath(), $"merge-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try {
                var tempSource = Path.Combine(tempDir, "source");
                var tempBase = Path.Combine(tempDir, "base");
                var tempTarget = Path.Combine(tempDir, "target");

                File.Copy(sourceFilePath, tempSource, overwrite: true);
                File.Copy(baseFilePath, tempBase, overwrite: true);
                File.Copy(targetFilePath, tempTarget, overwrite: true);

                // git merge-file <current> <base> <other>
                // Exit code: 0 = success, >0 = conflicts (число конфликтов), <0 = error
                var psi = new ProcessStartInfo {
                    FileName = "git",
                    Arguments = $"merge-file -p \"{tempSource}\" \"{tempBase}\" \"{tempTarget}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(psi);
                if (process == null) {
                    return new TraditionalMergeResult(false, false, null, "Failed to start git merge-file");
                }

                var output = await process.StandardOutput.ReadToEndAsync(ct);
                var error = await process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                if (process.ExitCode == 0) {
                    // Успешный merge без конфликтов
                    return new TraditionalMergeResult(true, false, output, null);
                } else if (process.ExitCode > 0) {
                    // Есть конфликты - нужен semantic merge
                    return new TraditionalMergeResult(false, true, output, $"Conflicts detected ({process.ExitCode})");
                } else {
                    // Ошибка
                    return new TraditionalMergeResult(false, false, null, $"git merge-file error: {error}");
                }
            } finally {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Traditional merge failed, will try semantic merge");
            return new TraditionalMergeResult(false, false, null, ex.Message);
        }
    }
}

/// <summary>
/// Результат merge веток.
/// </summary>
public sealed record BranchMergeResult {
    public bool Success { get; init; }
    public string Summary { get; set; } = "";
    public string? ErrorMessage { get; init; }
    public string? MergeBase { get; init; }
    public string? SourceBranch { get; init; }
    public string? TargetBranch { get; init; }
    public MergeInstructions? Instructions { get; init; }
    public List<string> ChangedFiles { get; init; } = new();
    public List<MergeActionInfo> Actions { get; init; } = new();
    public List<MergeConflictInfo> Conflicts { get; init; } = new();
    public MergeStatisticsInfo? Statistics { get; init; }

    public static BranchMergeResult Error(string message) => new() {
        Success = false,
        ErrorMessage = message,
        Summary = $"Error: {message}",
    };
}

public sealed record MergeActionInfo {
    public string FilePath { get; init; } = "";
    public string ActionType { get; init; } = "";
    public string Source { get; init; } = "";
    public float Confidence { get; init; }
}

public sealed record MergeConflictInfo {
    public string FilePath { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string ConflictType { get; init; } = "";
    public string Description { get; init; } = "";
    public string Severity { get; init; } = "";
}

public sealed record MergeStatisticsInfo {
    public int TotalChanges { get; init; }
    public int AutoMerged { get; init; }
    public int Conflicts { get; init; }
    public int FastPathMatches { get; init; }
    public int SlowPathMatches { get; init; }
    public long MergeTimeMs { get; init; }
}

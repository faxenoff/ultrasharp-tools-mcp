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

        // Также получить прямой diff source..target для информации
        var directDiff = await gitReader.GetChangedFilesWithStatusAsync(sourceBranch, targetBranch, ct);

        _logger.LogInformation("[MERGE] Changes: source vs base={SourceCount}, target vs base={TargetCount}, source vs target={DirectCount}",
            changesInSource.Count, changesInTarget.Count, directDiff.Count);

        // Если в source нет изменений относительно merge-base
        if (changesInSource.Count == 0 && changesInTarget.Count > 0) {
            _logger.LogWarning("[MERGE] Source branch ({Source}) has no changes relative to merge-base ({Base}). " +
                "Target ({Target}) has {Count} changes. Direct diff shows {DirectCount} files different.",
                sourceBranch, baseShort, targetBranch, changesInTarget.Count, directDiff.Count);
        }

        // Построить словари статусов для быстрого поиска
        var sourceStatuses = changesInSource.ToDictionary(c => c.Path, c => c.Status);
        var targetStatuses = changesInTarget.ToDictionary(c => c.Path, c => c.Status);

        // При мерже source → target нам нужны ТОЛЬКО файлы изменённые в source.
        // Файлы изменённые только в target уже там есть - их мержить не нужно.
        // Файлы изменённые в обоих - это потенциальные конфликты (они входят в changesInSource).
        var allChangedFiles = changesInSource.Select(c => c.Path)
            .Where(f => MatchesFilePattern(f, filePatterns))
            .Where(f => !parsedInstructions.ShouldExcludeFile(f))
            .Where(f => parsedInstructions.ShouldIncludeFile(f))
            .Distinct()
            .ToList();

        _logger.LogDebug("[MERGE] Files from source: {SourceCount}, files only in target (skipped): {TargetOnlyCount}",
            allChangedFiles.Count,
            changesInTarget.Count(t => !sourceStatuses.ContainsKey(t.Path)));

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

        // Построить mapping для переименованных файлов в target (старый путь -> новый путь)
        var targetRenames = changesInTarget
            .Where(c => c.Status == GitFileStatus.Renamed && !string.IsNullOrEmpty(c.OldPath))
            .ToDictionary(c => c.OldPath!, c => c.Path);

        // Определить какие файлы существуют в каждой версии:
        var filesInBase = allChangedFiles
            .Where(f => sourceStatuses.GetValueOrDefault(f) != GitFileStatus.Added
                     && targetStatuses.GetValueOrDefault(f) != GitFileStatus.Added)
            .ToList();

        var filesInSource = allChangedFiles
            .Where(f => sourceStatuses.GetValueOrDefault(f) != GitFileStatus.Deleted)
            .ToList();

        // Для target: исключить файлы которые:
        // 1. Были переименованы в target (их нет по старому пути)
        // 2. Добавлены ТОЛЬКО в source (их нет в target) - но если Added в обоих, то включить (add/add conflict)
        // 3. Удалены в target
        var filesInTarget = allChangedFiles
            .Where(f => sourceStatuses.GetValueOrDefault(f) != GitFileStatus.Deleted)
            .Where(f => {
                var srcStatus = sourceStatuses.GetValueOrDefault(f);
                var tgtStatus = targetStatuses.GetValueOrDefault(f);
                // Если Added в source - включить только если также Added в target (add/add)
                if (srcStatus == GitFileStatus.Added) {
                    return tgtStatus == GitFileStatus.Added;
                }
                return true;
            })
            .Where(f => !targetRenames.ContainsKey(f)) // Переименован в target - нет по старому пути
            .Where(f => targetStatuses.GetValueOrDefault(f) != GitFileStatus.Deleted) // Удалён в target
            .ToList();

        // Логировать переименования (это потенциальные конфликты rename/modify)
        var renamedInSource = allChangedFiles.Where(f => targetRenames.ContainsKey(f)).ToList();
        if (renamedInSource.Count > 0) {
            _logger.LogWarning("[MERGE] {Count} files were renamed in target (rename/modify conflicts): {Files}",
                renamedInSource.Count, string.Join(", ", renamedInSource.Take(5)));
        }

        _logger.LogInformation(
            "[MERGE] Files by version: base={BaseCount}, source={SourceCount}, target={TargetCount}, total={TotalCount}, renamed={Renamed}",
            filesInBase.Count, filesInSource.Count, filesInTarget.Count, allChangedFiles.Count, renamedInSource.Count);

        // Детальный лог каждого файла
        foreach (var f in allChangedFiles) {
            var srcStatus = sourceStatuses.GetValueOrDefault(f);
            var tgtStatus = targetStatuses.GetValueOrDefault(f, GitFileStatus.Unknown);
            var inBase = filesInBase.Contains(f);
            var inSrc = filesInSource.Contains(f);
            var inTgt = filesInTarget.Contains(f);
            _logger.LogInformation("[MERGE] File: {File} | srcStatus={SrcStatus}, tgtStatus={TgtStatus} | inBase={InBase}, inSrc={InSrc}, inTgt={InTgt}",
                f, srcStatus, tgtStatus, inBase, inSrc, inTgt);
        }

        if (allChangedFiles.Count == 0) {
            string summary;
            if (binaryFiles.Count > 0) {
                summary = $"No mergeable files (skipped {binaryFiles.Count} binary files)";
            } else if (changesInSource.Count == 0 && changesInTarget.Count > 0) {
                // Нет изменений в source, но есть в target - скорее всего направление неправильное
                summary = $"⚠️ No changes in {sourceBranch} relative to merge-base ({baseShort}).\n" +
                    $"   {targetBranch} has {changesInTarget.Count} changes vs base.\n" +
                    $"   Direct diff {sourceBranch}..{targetBranch}: {directDiff.Count} files.\n" +
                    $"   💡 Try: merge {targetBranch} → {sourceBranch}";
            } else if (changesInSource.Count == 0 && changesInTarget.Count == 0) {
                summary = "Both branches are identical to merge-base. Nothing to merge.";
            } else {
                // DEBUG: changesInSource > 0 но allChangedFiles = 0 после фильтрации
                summary = $"DEBUG: changesInSource={changesInSource.Count}, " +
                    $"changesInTarget={changesInTarget.Count}, " +
                    $"directDiff={directDiff.Count}, " +
                    $"binaryFiles={binaryFiles.Count}, " +
                    $"semanticFiles={semanticFiles.Count}, " +
                    $"textFiles={textFiles.Count}, " +
                    $"filePatterns={filePatterns}, " +
                    $"sourceFiles=[{string.Join(",", changesInSource.Take(5).Select(c => c.Path))}]";
            }
            _logger.LogInformation("[MERGE] {Summary}", summary);
            return new BranchMergeResult {
                Success = true,
                MergeBase = baseShort,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
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
            _logger.LogInformation("[MERGE] Extracting files: base={BaseFiles}, source={SourceFiles}, target={TargetFiles}",
                string.Join(", ", filesInBase), string.Join(", ", filesInSource), string.Join(", ", filesInTarget));
            var baseExtracted = await gitReader.ExtractFilesToDirectoryAsync(mergeBase, filesInBase, baseDir, ct);
            var sourceExtracted = await gitReader.ExtractFilesToDirectoryAsync(sourceBranch, filesInSource, sourceDir, ct);
            var targetExtracted = await gitReader.ExtractFilesToDirectoryAsync(targetBranch, filesInTarget, targetDir, ct);
            _logger.LogInformation("[MERGE] Extracted files: base={Base}/{BaseTotal}, source={Source}/{SourceTotal}, target={Target}/{TargetTotal}",
                baseExtracted, filesInBase.Count, sourceExtracted, filesInSource.Count, targetExtracted, filesInTarget.Count);

            // 5. Гибридный merge: сначала traditional для text, затем semantic
            var traditionalMergedFiles = new Dictionary<string, string>(); // path -> merged content
            var textFileConflicts = new List<string>(); // text файлы с конфликтами (маркеры в контенте)
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
                } else if (traditionalResult.HasConflicts && traditionalResult.MergedContent != null) {
                    // Есть конфликты но git merge-file вернул результат с маркерами - используем его
                    _logger.LogWarning("[MERGE] Traditional merge has conflicts for {File}, using merge output with conflict markers", file);
                    traditionalMergedFiles[file] = traditionalResult.MergedContent;
                    // Добавим в список конфликтов для отчёта
                    textFileConflicts.Add(file);
                } else {
                    // Merge не удался совсем - берём source версию
                    _logger.LogWarning("[MERGE] Traditional merge failed for {File}: {Reason}. Taking source version.",
                        file, traditionalResult.ErrorMessage ?? "unknown error");
                    if (File.Exists(sourcePath)) {
                        var sourceContent = await File.ReadAllTextAsync(sourcePath, ct);
                        traditionalMergedFiles[file] = sourceContent;
                        _logger.LogInformation("[MERGE] Using source version for {File} ({Len} bytes)", file, sourceContent.Length);
                    } else {
                        _logger.LogWarning("[MERGE] Source file not found for {File}, skipping", file);
                    }
                }
            }

            _logger.LogInformation("[MERGE] Traditional merge: {Success} succeeded, {Fallback} need semantic merge. Text files: {TextCount}",
                traditionalMergedFiles.Count, filesForSemanticMerge.Count, textFiles.Count);
            if (traditionalMergedFiles.Count > 0) {
                _logger.LogInformation("[MERGE] Traditional merged: [{Files}]", string.Join(", ", traditionalMergedFiles.Keys));
            }
            if (filesForSemanticMerge.Count > 0) {
                _logger.LogInformation("[MERGE] Semantic fallback: [{Files}]", string.Join(", ", filesForSemanticMerge));
            }

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

            // 7. Объединить результаты (дедуплицировать по файлу!)
            var allActions = new List<MergeActionInfo>();
            var totalInsertions = 0;
            var totalDeletions = 0;

            // Добавить традиционно смердженные файлы как actions (кроме файлов с конфликтами)
            foreach (var (filePath, mergedContent) in traditionalMergedFiles) {
                // Файлы с конфликтами не добавляем в actions - они будут в conflicts
                if (textFileConflicts.Contains(filePath)) continue;

                // Сравнить с оригинальным файлом в target для подсчёта строк
                var targetFilePath = Path.Combine(targetDir, filePath.Replace('/', Path.DirectorySeparatorChar));
                var originalContent = File.Exists(targetFilePath) ? await File.ReadAllTextAsync(targetFilePath, ct) : "";
                var (ins, del) = CountLineDiff(originalContent, mergedContent);
                totalInsertions += ins;
                totalDeletions += del;

                allActions.Add(new MergeActionInfo {
                    FilePath = filePath,
                    ActionType = "Modify",
                    Source = "traditional",
                    Confidence = 1.0f,
                    Insertions = ins,
                    Deletions = del,
                });
            }

            // Добавить semantic merge results (дедуплицировать по файлу - один action на файл)
            var filesWithSemanticActions = new HashSet<string>();
            if (semanticResult != null) {
                var semanticByFile = semanticResult.Actions
                    .Where(a => !string.IsNullOrEmpty(a.TargetPath))
                    .GroupBy(a => a.TargetPath)
                    .Select(g => g.OrderByDescending(a => a.Content?.Length ?? 0).First());

                foreach (var a in semanticByFile) {
                    filesWithSemanticActions.Add(a.TargetPath);
                    // Не добавлять если уже есть traditional action для этого файла
                    if (!traditionalMergedFiles.ContainsKey(a.TargetPath)) {
                        var targetFilePath = Path.Combine(targetDir, a.TargetPath.Replace('/', Path.DirectorySeparatorChar));
                        var originalContent = File.Exists(targetFilePath) ? await File.ReadAllTextAsync(targetFilePath, ct) : "";
                        var (ins, del) = CountLineDiff(originalContent, a.Content ?? "");
                        totalInsertions += ins;
                        totalDeletions += del;

                        allActions.Add(new MergeActionInfo {
                            FilePath = a.TargetPath,
                            ActionType = a.Type.ToString(),
                            Source = a.Source,
                            Confidence = a.Confidence,
                            Insertions = ins,
                            Deletions = del,
                        });
                    }
                }
            }

            // Fallback: C# файлы без semantic actions - берём source версию
            foreach (var file in filesForSemanticMerge) {
                if (!traditionalMergedFiles.ContainsKey(file) && !filesWithSemanticActions.Contains(file)) {
                    var sourcePath = Path.Combine(sourceDir, file.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(sourcePath)) {
                        var sourceContent = await File.ReadAllTextAsync(sourcePath, ct);
                        var targetFilePath = Path.Combine(targetDir, file.Replace('/', Path.DirectorySeparatorChar));
                        var originalContent = File.Exists(targetFilePath) ? await File.ReadAllTextAsync(targetFilePath, ct) : "";
                        var (ins, del) = CountLineDiff(originalContent, sourceContent);
                        totalInsertions += ins;
                        totalDeletions += del;

                        _logger.LogWarning("[MERGE] Semantic merge produced no action for {File}, using source version", file);

                        // Добавляем в traditionalMergedFiles для записи
                        traditionalMergedFiles[file] = sourceContent;

                        allActions.Add(new MergeActionInfo {
                            FilePath = file,
                            ActionType = "Modify",
                            Source = "source-fallback",
                            Confidence = 0.5f,
                            Insertions = ins,
                            Deletions = del,
                        });
                    }
                }
            }

            var conflicts = semanticResult?.Conflicts.Select(c => new MergeConflictInfo {
                FilePath = c.BaseUnit?.FilePath ?? "",
                Symbol = c.BaseUnit?.FullyQualifiedName ?? "",
                ConflictType = c.ConflictType.ToString(),
                Description = c.Description,
                Severity = c.Severity.ToString(),
            }).ToList() ?? new List<MergeConflictInfo>();

            // Добавить text file конфликты (с маркерами в контенте)
            foreach (var file in textFileConflicts) {
                conflicts.Add(new MergeConflictInfo {
                    FilePath = file,
                    Symbol = "",
                    ConflictType = "TextMergeConflict",
                    Description = "File has conflict markers (<<<<<<< ======= >>>>>>>) that need manual resolution",
                    Severity = "Warning",
                });
            }

            // Дедуплицировать конфликты по файлу
            var conflictsByFile = conflicts
                .GroupBy(c => c.FilePath)
                .Select(g => g.First())
                .ToList();

            // Применить маппинг rename к путям (для корректного отчёта)
            var finalActions = allActions.Select(a => targetRenames.TryGetValue(a.FilePath, out var renamedPath)
                ? a with { FilePath = renamedPath }
                : a).ToList();
            var finalConflicts = conflictsByFile.Select(c => targetRenames.TryGetValue(c.FilePath, out var renamedPath)
                ? c with { FilePath = renamedPath }
                : c).ToList();

            var result = new BranchMergeResult {
                Success = true,
                MergeBase = baseShort,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                Instructions = parsedInstructions,
                ChangedFiles = allChangedFiles,
                Actions = finalActions,
                Conflicts = finalConflicts,
                Statistics = new MergeStatisticsInfo {
                    FilesToMerge = finalActions.Count + finalConflicts.Count,
                    AutoMergedFiles = finalActions.Count,
                    ConflictFiles = finalConflicts.Count,
                    TotalInsertions = totalInsertions,
                    TotalDeletions = totalDeletions,
                    FastPathMatches = semanticResult?.Statistics.FastPathMatches ?? 0,
                    SlowPathMatches = semanticResult?.Statistics.SlowPathMatches ?? 0,
                    MergeTimeMs = sw.ElapsedMilliseconds,
                },
            };

            // 8. Применить изменения (даже с конфликтами - они будут с маркерами для ручного разрешения)
            if (applyChanges && result.Success) {
                _logger.LogInformation("[MERGE] === APPLYING CHANGES to: {Repo} ===", repositoryPath);
                if (result.Conflicts.Count > 0) {
                    _logger.LogWarning("[MERGE] Applying {Count} files with conflict markers for manual resolution",
                        result.Conflicts.Count);
                }

                // Записать традиционно смердженные файлы
                var writtenCount = 0;
                foreach (var (filePath, content) in traditionalMergedFiles) {
                    // Если файл был переименован в target - писать по новому пути
                    var actualPath = targetRenames.TryGetValue(filePath, out var renamedPath) ? renamedPath : filePath;
                    if (actualPath != filePath) {
                        _logger.LogInformation("[MERGE] File renamed in target: {OldPath} → {NewPath}", filePath, actualPath);
                    }

                    var fullPath = Path.Combine(repositoryPath, actualPath.Replace('/', Path.DirectorySeparatorChar));
                    _logger.LogInformation("[MERGE] Writing: {Path} ({Len} bytes)", fullPath, content?.Length ?? 0);
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(fullPath, content, ct);
                    writtenCount++;
                }
                _logger.LogInformation("[MERGE] Written {Count} traditional files to {Repo}", writtenCount, repositoryPath);

                // Записать semantic merge результаты
                if (semanticResult != null) {
                    _logger.LogInformation("[MERGE] Applying {Count} semantic merge results", semanticResult.Actions.Count);
                    await ApplyMergeResultAsync(gitReader, repositoryPath, semanticResult, ct);
                }

                var binaryNote = binaryFiles.Count > 0 ? $" ({binaryFiles.Count} binary files skipped)" : "";
                var conflictNote = result.Conflicts.Count > 0
                    ? $" ⚠️ {result.Conflicts.Count} files have conflict markers - resolve manually!"
                    : "";
                result.Summary = $"Merged {result.Actions.Count + result.Conflicts.Count} files from {sourceBranch} to {targetBranch}{binaryNote}.{conflictNote} " +
                    $"Files written to {repositoryPath}. Use 'git diff' to review.";
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
        // Группировать actions по файлу и выбрать наиболее полный контент
        // (File-level actions содержат весь файл, Type/Method - только фрагменты)
        var actionsByFile = mergeResult.Actions
            .Where(a => a.Type != MergeActionType.Delete && !string.IsNullOrEmpty(a.Content) && !string.IsNullOrEmpty(a.TargetPath))
            .GroupBy(a => a.TargetPath)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (filePath, actions) in actionsByFile) {
            // Выбрать action с максимальным Content (File-level будет иметь полный контент)
            var bestAction = actions.OrderByDescending(a => a.Content?.Length ?? 0).First();

            _logger.LogDebug("[MERGE] Writing file {FilePath}: selected action with {ContentLength} chars (from {Count} actions)",
                filePath, bestAction.Content?.Length ?? 0, actions.Count);

            var fullPath = Path.Combine(repositoryPath, filePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(fullPath, bestAction.Content!, ct);
        }

        // Обработать Delete actions
        foreach (var action in mergeResult.Actions.Where(a => a.Type == MergeActionType.Delete)) {
            _logger.LogInformation("Would delete: {Path}", action.TargetPath);
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
    }

    /// <summary>
    /// Простой подсчёт изменений строк между двумя текстами.
    /// </summary>
    private static (int insertions, int deletions) CountLineDiff(string original, string modified) {
        if (string.IsNullOrEmpty(original) && string.IsNullOrEmpty(modified))
            return (0, 0);

        var originalLines = original.Split('\n').Length;
        var modifiedLines = modified.Split('\n').Length;

        if (string.IsNullOrEmpty(original))
            return (modifiedLines, 0);
        if (string.IsNullOrEmpty(modified))
            return (0, originalLines);

        var diff = modifiedLines - originalLines;
        if (diff >= 0)
            return (diff, 0);
        else
            return (0, -diff);
    }

    /// <summary>
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

            _logger.LogInformation("[TRADITIONAL] File check: base={BaseExists} ({BasePath}), source={SourceExists}, target={TargetExists}",
                baseExists, Path.GetFileName(baseFilePath), sourceExists, targetExists);

            // Если файл добавлен только в одной ветке - просто берём его
            if (!baseExists && sourceExists && !targetExists) {
                return new TraditionalMergeResult(true, false, await File.ReadAllTextAsync(sourceFilePath, ct), null);
            }
            if (!baseExists && !sourceExists && targetExists) {
                return new TraditionalMergeResult(true, false, await File.ReadAllTextAsync(targetFilePath, ct), null);
            }
            if (!baseExists && sourceExists && targetExists) {
                // Оба добавили файл - используем пустой base для 3-way merge
                // git merge-file справится с этим
                _logger.LogInformation("[TRADITIONAL] File added in both branches, using empty base for merge");
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

            // Для merge нужны source и target, base может быть пустым (add/add случай)
            if (!sourceExists || !targetExists) {
                return new TraditionalMergeResult(false, false, null, "Missing source or target file for merge");
            }

            // Создаём временные файлы для git merge-file (он модифицирует первый файл in-place)
            var tempDir = Path.Combine(Path.GetTempPath(), $"merge-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try {
                var tempSource = Path.Combine(tempDir, "source");
                var tempBase = Path.Combine(tempDir, "base");
                var tempTarget = Path.Combine(tempDir, "target");

                File.Copy(sourceFilePath, tempSource, overwrite: true);
                // Для add/add случая base пустой
                if (baseExists) {
                    File.Copy(baseFilePath, tempBase, overwrite: true);
                } else {
                    await File.WriteAllTextAsync(tempBase, "", ct);
                }
                File.Copy(targetFilePath, tempTarget, overwrite: true);

                // git merge-file <current> <base> <other>
                // Для merge source → target:
                //   current = target (куда мержим)
                //   base = merge-base
                //   other = source (откуда мержим)
                // Exit code: 0 = success, >0 = conflicts (число конфликтов), <0 = error
                var psi = new ProcessStartInfo {
                    FileName = "git",
                    Arguments = $"merge-file -p \"{tempTarget}\" \"{tempBase}\" \"{tempSource}\"",
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
    public int Insertions { get; init; }
    public int Deletions { get; init; }
}

public sealed record MergeConflictInfo {
    public string FilePath { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string ConflictType { get; init; } = "";
    public string Description { get; init; } = "";
    public string Severity { get; init; } = "";
}

public sealed record MergeStatisticsInfo {
    public int FilesToMerge { get; init; }
    public int AutoMergedFiles { get; init; }
    public int ConflictFiles { get; init; }
    public int TotalInsertions { get; init; }
    public int TotalDeletions { get; init; }
    public int FastPathMatches { get; init; }
    public int SlowPathMatches { get; init; }
    public long MergeTimeMs { get; init; }

    // Legacy properties for compatibility
    public int TotalChanges => FilesToMerge;
    public int AutoMerged => AutoMergedFiles;
    public int Conflicts => ConflictFiles;
}

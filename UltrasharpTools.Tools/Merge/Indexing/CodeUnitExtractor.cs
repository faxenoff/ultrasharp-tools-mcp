using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Merge.Parsing;
using UltrasharpTools.Tools.Infrastructure.HighPerformanceIO;
using System.Threading.Tasks.Dataflow;

namespace UltrasharpTools.Tools.Merge.Indexing;
/// <summary>
/// Универсальный экстрактор CodeUnits из различных файлов.
/// Автоматически выбирает парсер в зависимости от типа файла.
/// Оптимизирован для высокопроизводительной обработки с использованием TPL Dataflow.
/// </summary>
public sealed class CodeUnitExtractor {
    private readonly CSharpParser _csharpParser;
    private readonly JsonParser _jsonParser;
    private readonly XmlParser _xmlParser;
    private readonly YamlParser _yamlParser;
    private readonly PowerShellParser _powershellParser;
    private readonly ShellParser _shellParser;
    private readonly ILogger<CodeUnitExtractor> _logger;

    // Настройки параллелизма
    private const int IoParallelism = 4; // 4 потока для чтения файлов
    private const int CpuParallelism = 8; // 8 потоков для парсинга

    public CodeUnitExtractor(
    CSharpParser csharpParser,
    JsonParser jsonParser,
    XmlParser xmlParser,
    YamlParser yamlParser,
    PowerShellParser powershellParser,
    ShellParser shellParser,
    ILogger<CodeUnitExtractor>? logger = null
    ) {
        _csharpParser = csharpParser;
        _jsonParser = jsonParser;
        _xmlParser = xmlParser;
        _yamlParser = yamlParser;
        _powershellParser = powershellParser;
        _shellParser = shellParser;
        _logger = logger ?? NullLogger<CodeUnitExtractor>.Instance;
    }

    /// <summary>
    /// Извлечь CodeUnits из файла (автоматически выбирает парсер).
    /// </summary>
    public async Task<List<CodeUnit>> ExtractFromFileAsync(
    string filePath,
    CancellationToken ct = default
    ) {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        _logger.LogDebug(
        "Extracting CodeUnits from {FilePath} (extension: {Extension})",
        filePath,
        extension
        );

        try {
            return extension switch {
                ".cs" => await _csharpParser.ParseFileAsync(filePath, ct),
                ".json" => await _jsonParser.ParseFileAsync(filePath, ct),
                ".xml" or ".csproj" or ".targets" or ".props" => await _xmlParser.ParseFileAsync(
                filePath,
                ct
                ),
                ".yaml" or ".yml" => await _yamlParser.ParseFileAsync(filePath, ct),
                ".ps1" => await _powershellParser.ParseFileAsync(filePath, ct),
                ".sh" or ".bash" or ".cmd" or ".bat" => await _shellParser.ParseFileAsync(
                filePath,
                ct
                ),
                _ => await ExtractGenericFileAsync(filePath, ct),
            };
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to extract CodeUnits from {FilePath}", filePath);

            // Fallback: создать только file-level unit
            return new List<CodeUnit> { await CreateFallbackFileUnit(filePath, ct) };
        }
    }
    /// <summary>
    /// Извлечь CodeUnits из нескольких файлов (оптимизированная версия с TPL Dataflow).
    /// Использует пайплайн: чтение (4 потока IO) -> обработка (параллельно).
    /// </summary>
    public async Task<List<CodeUnit>> ExtractFromFilesAsync(
    IEnumerable<string> filePaths,
    CancellationToken ct = default
    ) {
        var allUnits = new ConcurrentBag<CodeUnit>();
        var fileCount = 0;

        var filePathsList = filePaths is IList<string> list ? list : filePaths.ToList();
        var totalFiles = filePathsList.Count;

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        // Блок 1: IO-bound чтение и парсинг файлов (4 потока)
        var processBlock = new TransformBlock<string, (string, List<CodeUnit>)>(
        async filePath => {
            try {
                var units = await ExtractFromFileAsync(filePath, ct);
                Interlocked.Increment(ref fileCount);
                return (filePath, units);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to process {FilePath}", filePath);
                return (filePath, new List<CodeUnit>());
            }
        },
        new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct,
            BoundedCapacity = 8
        }
        );

        // Блок 2: Сбор результатов
        var collectBlock = new ActionBlock<(string, List<CodeUnit>)>(
        item => {
            foreach (var unit in item.Item2) {
                allUnits.Add(unit);
            }

            if (fileCount % 10 == 0) {
                _logger.LogDebug("Processed {Current}/{Total} files", fileCount, totalFiles);
            }
        },
        new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = 1,
            CancellationToken = ct
        }
        );

        processBlock.LinkTo(collectBlock, linkOptions);

        foreach (var filePath in filePathsList) {
            await processBlock.SendAsync(filePath, ct);
        }

        processBlock.Complete();
        await collectBlock.Completion;

        var result = allUnits.ToList();

        _logger.LogInformation(
        "Extracted {Count} CodeUnits from {FileCount} files (parallel: 4 threads)",
        result.Count,
        fileCount
        );

        return result;
    }    /// <summary>
         /// Извлечь CodeUnits из директории (рекурсивно) с оптимизированным сканированием.
         /// Использует EnumerateFiles для экономии памяти и параллельное сканирование паттернов.
         /// </summary>
    public async Task<List<CodeUnit>> ExtractFromDirectoryAsync(
    string directoryPath,
    string[] filePatterns,
    CancellationToken ct = default
    ) {
        // Предварительная загрузка путей с использованием параллельного сканирования
        var filesBag = new ConcurrentBag<string>();

        // Параллельно сканируем все паттерны
        await Parallel.ForEachAsync(
        filePatterns,
        new ParallelOptions {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        },
        async (pattern, token) => {
            await Task.Run(
    () => {
        // EnumerateFiles для экономии памяти
        var matchedFiles = Directory.EnumerateFiles(
directoryPath,
pattern,
SearchOption.AllDirectories
);

        foreach (var file in matchedFiles) {
            filesBag.Add(file);
        }
    },
    token
    );
        }
        );

        var files = filesBag.ToList();

        _logger.LogInformation(
        "Found {Count} files matching patterns in {Directory} (parallel scan)",
        files.Count,
        directoryPath
        );

        return await ExtractFromFilesAsync(files, ct);
    }

    /// <summary>
    /// Создать file-level unit для неподдерживаемых типов файлов.
    /// </summary>
    private async Task<List<CodeUnit>> ExtractGenericFileAsync(
    string filePath,
    CancellationToken ct
    ) {
        _logger.LogDebug("Using generic extraction for {FilePath}", filePath);

        var unit = await CreateFallbackFileUnit(filePath, ct);
        return new List<CodeUnit> { unit };
    }

    /// <summary>
    /// Создать fallback file unit (если парсинг не удался).
    /// </summary>
    private async Task<CodeUnit> CreateFallbackFileUnit(string filePath, CancellationToken ct) {
        var content = await OptimizedFileIO.ReadAllTextAsync(filePath, null, ct);
        var contentHash = ContentNormalizer.ComputeContentHash(content);

        // Для fallback используем тот же hash
        var structuralHash = contentHash;

        return new CodeUnit {
            Id = $"file:{filePath}",
            Type = CodeUnitType.File,
            FilePath = filePath,
            Name = Path.GetFileName(filePath),
            FullyQualifiedName = filePath,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = null,
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = null,
            ChildIds = new HashSet<string>(),
            StartLine = 1,
            EndLine = content.Split('\n').Length,
            Metadata = new Dictionary<string, object> {
                ["FileSize"] = content.Length,
                ["Extension"] = Path.GetExtension(filePath),
                ["ParsingFailed"] = true,
            },
        };
    }

    /// <summary>
    /// Построить иерархию parent-child relationships.
    /// </summary>
    public void BuildHierarchy(List<CodeUnit> units) {
        var unitsById = units.ToDictionary(u => u.Id);

        // Итерируемся по индексам чтобы можно было безопасно модифицировать список
        for (int i = 0; i < units.Count; i++) {
            var unit = units[i];
            if (unit.ParentId != null && unitsById.TryGetValue(unit.ParentId, out var parent)) {
                // Обновить ChildIds родителя
                var updatedParent = parent with {
                    ChildIds = parent.ChildIds.Append(unit.Id).ToHashSet(),
                };

                // Заменить в словаре
                unitsById[parent.Id] = updatedParent;

                // Заменить в списке
                var index = units.IndexOf(parent);
                units[index] = updatedParent;
            }
        }

        _logger.LogDebug("Built hierarchy for {Count} units", units.Count);
    }
}

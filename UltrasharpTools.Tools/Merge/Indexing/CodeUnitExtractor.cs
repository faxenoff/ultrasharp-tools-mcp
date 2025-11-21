
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;
using UltrasharpTools.Tools.Merge.Parsing;

namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Универсальный экстрактор CodeUnits из различных файлов.
/// Автоматически выбирает парсер в зависимости от типа файла.
/// </summary>
public sealed class CodeUnitExtractor
{
    private readonly CSharpParser _csharpParser;
    private readonly JsonParser _jsonParser;
    private readonly XmlParser _xmlParser;
    private readonly YamlParser _yamlParser;
    private readonly PowerShellParser _powershellParser;
    private readonly ShellParser _shellParser;
    private readonly ILogger<CodeUnitExtractor> _logger;

    public CodeUnitExtractor(
    CSharpParser csharpParser,
    JsonParser jsonParser,
    XmlParser xmlParser,
    YamlParser yamlParser,
    PowerShellParser powershellParser,
    ShellParser shellParser,
    ILogger<CodeUnitExtractor>? logger = null)
    {
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
    CancellationToken ct = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        _logger.LogDebug("Extracting CodeUnits from {FilePath} (extension: {Extension})",
        filePath, extension);

        try
        {
            return extension switch
            {
                ".cs" => await _csharpParser.ParseFileAsync(filePath, ct),
                ".json" => await _jsonParser.ParseFileAsync(filePath, ct),
                ".xml" or ".csproj" or ".targets" or ".props" => await _xmlParser.ParseFileAsync(filePath, ct),
                ".yaml" or ".yml" => await _yamlParser.ParseFileAsync(filePath, ct),
                ".ps1" => await _powershellParser.ParseFileAsync(filePath, ct),
                ".sh" or ".bash" or ".cmd" or ".bat" => await _shellParser.ParseFileAsync(filePath, ct),
                _ => await ExtractGenericFileAsync(filePath, ct)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
            "Failed to extract CodeUnits from {FilePath}",
            filePath);

            // Fallback: создать только file-level unit
            return new List<CodeUnit> { await CreateFallbackFileUnit(filePath, ct) };
        }
    }

    /// <summary>
    /// Извлечь CodeUnits из нескольких файлов.
    /// </summary>
    public async Task<List<CodeUnit>> ExtractFromFilesAsync(
    IEnumerable<string> filePaths,
    CancellationToken ct = default)
    {
        var allUnits = new List<CodeUnit>();

        foreach (var filePath in filePaths)
        {
            var units = await ExtractFromFileAsync(filePath, ct);
            allUnits.AddRange(units);
        }

        _logger.LogInformation(
        "Extracted {Count} CodeUnits from {FileCount} files",
        allUnits.Count,
        filePaths.Count());

        return allUnits;
    }

    /// <summary>
    /// Извлечь CodeUnits из директории (рекурсивно).
    /// </summary>
    public async Task<List<CodeUnit>> ExtractFromDirectoryAsync(
    string directoryPath,
    string[] filePatterns,
    CancellationToken ct = default)
    {
        var files = new List<string>();

        foreach (var pattern in filePatterns)
        {
            var matchedFiles = Directory.GetFiles(
            directoryPath,
            pattern,
            SearchOption.AllDirectories);

            files.AddRange(matchedFiles);
        }

        _logger.LogInformation(
        "Found {Count} files matching patterns in {Directory}",
        files.Count,
        directoryPath);

        return await ExtractFromFilesAsync(files, ct);
    }

    /// <summary>
    /// Создать file-level unit для неподдерживаемых типов файлов.
    /// </summary>
    private async Task<List<CodeUnit>> ExtractGenericFileAsync(
    string filePath,
    CancellationToken ct)
    {
        _logger.LogDebug(
        "Using generic extraction for {FilePath}",
        filePath);

        var unit = await CreateFallbackFileUnit(filePath, ct);
        return new List<CodeUnit> { unit };
    }

    /// <summary>
    /// Создать fallback file unit (если парсинг не удался).
    /// </summary>
    private async Task<CodeUnit> CreateFallbackFileUnit(
    string filePath,
    CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var contentHash = ContentNormalizer.ComputeContentHash(content);

        // Для fallback используем тот же hash
        var structuralHash = contentHash;

        return new CodeUnit
        {
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
            Metadata = new Dictionary<string, object>
            {
                ["FileSize"] = content.Length,
                ["Extension"] = Path.GetExtension(filePath),
                ["ParsingFailed"] = true
            }
        };
    }

    /// <summary>
    /// Построить иерархию parent-child relationships.
    /// </summary>
    public void BuildHierarchy(List<CodeUnit> units)
    {
        var unitsById = units.ToDictionary(u => u.Id);

        // Итерируемся по индексам чтобы можно было безопасно модифицировать список
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            if (unit.ParentId != null && unitsById.TryGetValue(unit.ParentId, out var parent))
            {
                // Обновить ChildIds родителя
                var updatedParent = parent with
                {
                    ChildIds = parent.ChildIds.Append(unit.Id).ToHashSet()
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

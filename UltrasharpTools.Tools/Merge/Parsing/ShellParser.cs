using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер Shell/Bash/CMD скриптов для извлечения CodeUnits.
/// Поддерживает: .sh, .cmd, .bat
/// Использует regex-based парсинг для извлечения функций и блоков.
/// </summary>
public sealed partial class ShellParser
{
    private readonly ILogger<ShellParser> _logger;
    private readonly StructuralFingerprint _fingerprint;
    private readonly ContentNormalizer _normalizer;

    // Regex для bash функций: function_name() { ... } или function function_name { ... }
    [GeneratedRegex(@"^\s*(function\s+)?(?<name>[\w_-]+)\s*\(\)\s*(\{|$)", RegexOptions.Multiline)]
    private static partial Regex BashFunctionRegex();

    // Regex для cmd labels: :label_name
    [GeneratedRegex(@"^\s*:(?<name>[\w_-]+)\s*$", RegexOptions.Multiline)]
    private static partial Regex CmdLabelRegex();

    public ShellParser(
        StructuralFingerprint fingerprint,
        ContentNormalizer normalizer,
        ILogger<ShellParser>? logger = null
    )
    {
        _fingerprint = fingerprint;
        _normalizer = normalizer;
        _logger = logger ?? NullLogger<ShellParser>.Instance;
    }

    /// <summary>
    /// Парсить Shell файл и извлечь CodeUnits.
    /// </summary>
    public async Task<List<CodeUnit>> ParseFileAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        // 1. Нормализовать контент
        var normalized = await _normalizer.NormalizeAsync(filePath, ct);
        var content = normalized.Content;

        // 2. Извлечь units
        var units = new List<CodeUnit>();

        // File-level unit
        var fileUnit = CreateFileUnit(filePath, content);
        units.Add(fileUnit);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // 3. Извлечь функции/labels в зависимости от типа
        if (extension == ".sh" || extension == ".bash")
        {
            ExtractBashFunctions(content, filePath, fileUnit.Id, units);
        }
        else if (extension == ".cmd" || extension == ".bat")
        {
            ExtractCmdLabels(content, filePath, fileUnit.Id, units);
        }

        _logger.LogInformation(
            "Parsed Shell {FilePath}: extracted {Count} units",
            filePath,
            units.Count
        );

        return units;
    }

    /// <summary>
    /// Создать File-level CodeUnit.
    /// </summary>
    private CodeUnit CreateFileUnit(string filePath, string content)
    {
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var language = extension switch
        {
            ".sh" or ".bash" => "Bash",
            ".cmd" or ".bat" => "CMD",
            _ => "Shell",
        };

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
                ["Extension"] = extension,
                ["Language"] = language,
            },
        };
    }

    /// <summary>
    /// Извлечь bash функции.
    /// </summary>
    private void ExtractBashFunctions(
        string content,
        string filePath,
        string parentId,
        List<CodeUnit> units
    )
    {
        var lines = content.Split('\n');
        var functionMatches = BashFunctionRegex().Matches(content);

        foreach (Match match in functionMatches)
        {
            try
            {
                var functionName = match.Groups["name"].Value;
                var functionStart = GetLineNumber(content, match.Index);
                var functionEnd = FindBashFunctionEnd(lines, functionStart);

                var functionContent = ExtractLines(lines, functionStart, functionEnd);
                var functionUnit = CreateFunctionUnit(
                    functionName,
                    functionContent,
                    filePath,
                    parentId,
                    functionStart,
                    functionEnd,
                    "Bash"
                );

                units.Add(functionUnit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to extract bash function at line {Line} in {FilePath}",
                    GetLineNumber(content, match.Index),
                    filePath
                );
            }
        }
    }

    /// <summary>
    /// Извлечь CMD labels (псевдо-функции).
    /// </summary>
    private void ExtractCmdLabels(
        string content,
        string filePath,
        string parentId,
        List<CodeUnit> units
    )
    {
        var lines = content.Split('\n');
        var labelMatches = CmdLabelRegex().Matches(content);

        foreach (Match match in labelMatches)
        {
            try
            {
                var labelName = match.Groups["name"].Value;

                // Пропустить стандартные labels
                if (labelName.Equals("EOF", StringComparison.OrdinalIgnoreCase))
                    continue;

                var labelStart = GetLineNumber(content, match.Index);

                // Найти следующий label или конец файла
                var labelEnd = FindNextCmdLabel(lines, labelStart) - 1;
                if (labelEnd < labelStart)
                    labelEnd = lines.Length;

                var labelContent = ExtractLines(lines, labelStart, labelEnd);
                var labelUnit = CreateFunctionUnit(
                    labelName,
                    labelContent,
                    filePath,
                    parentId,
                    labelStart,
                    labelEnd,
                    "CMD"
                );

                units.Add(labelUnit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to extract CMD label at line {Line} in {FilePath}",
                    GetLineNumber(content, match.Index),
                    filePath
                );
            }
        }
    }

    /// <summary>
    /// Создать Function/Label CodeUnit.
    /// </summary>
    private CodeUnit CreateFunctionUnit(
        string name,
        string content,
        string filePath,
        string parentId,
        int startLine,
        int endLine,
        string language
    )
    {
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        return new CodeUnit
        {
            Id = $"function:{name}@{filePath}:{startLine}",
            Type = CodeUnitType.ScriptFunction,
            FilePath = filePath,
            Name = name,
            FullyQualifiedName = name,
            Content = content,
            ContentHash = contentHash,
            StructuralHash = structuralHash,
            Signature = language == "Bash" ? $"{name}() {{}}" : $":{name}",
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = startLine,
            EndLine = endLine,
            Metadata = new Dictionary<string, object> { ["Language"] = language },
        };
    }

    /// <summary>
    /// Найти конец bash функции (закрывающую фигурную скобку).
    /// </summary>
    private int FindBashFunctionEnd(string[] lines, int startLine)
    {
        var depth = 0;
        var found = false;

        for (int i = startLine - 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // Подсчитать { и }
            foreach (var ch in line)
            {
                if (ch == '{')
                {
                    depth++;
                    found = true;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (found && depth == 0)
                    {
                        return i + 1; // Линии 1-based
                    }
                }
            }
        }

        // Если не нашли закрывающую скобку, вернем последнюю строку
        return lines.Length;
    }

    /// <summary>
    /// Найти следующий CMD label.
    /// </summary>
    private int FindNextCmdLabel(string[] lines, int startLine)
    {
        for (int i = startLine; i < lines.Length; i++)
        {
            if (CmdLabelRegex().IsMatch(lines[i]))
            {
                return i + 1; // Линии 1-based
            }
        }

        return lines.Length + 1;
    }

    /// <summary>
    /// Извлечь строки от startLine до endLine (1-based).
    /// </summary>
    private string ExtractLines(string[] lines, int startLine, int endLine)
    {
        var start = Math.Max(0, startLine - 1);
        var end = Math.Min(lines.Length, endLine);
        var count = end - start;

        return string.Join('\n', lines.Skip(start).Take(count));
    }

    /// <summary>
    /// Получить номер строки по индексу в тексте.
    /// </summary>
    private int GetLineNumber(string content, int index)
    {
        return content.Substring(0, index).Count(c => c == '\n') + 1;
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер PowerShell скриптов для извлечения CodeUnits.
/// Поддерживает: .ps1
/// Использует regex-based парсинг для извлечения функций и блоков.
/// </summary>
public sealed class PowerShellParser
{
    private readonly ILogger<PowerShellParser> _logger;
    private readonly StructuralFingerprint _fingerprint;
    private readonly ContentNormalizer _normalizer;

    // Regex для функций: function FunctionName { ... }
    private static readonly Regex FunctionRegex = new(
        @"^\s*function\s+(?<name>[\w-]+)\s*(\{|$)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // Regex для param блоков
    private static readonly Regex ParamRegex = new(
        @"^\s*param\s*\(",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public PowerShellParser(
        StructuralFingerprint fingerprint,
        ContentNormalizer normalizer,
        ILogger<PowerShellParser>? logger = null
    )
    {
        _fingerprint = fingerprint;
        _normalizer = normalizer;
        _logger = logger ?? NullLogger<PowerShellParser>.Instance;
    }

    /// <summary>
    /// Парсить PowerShell файл и извлечь CodeUnits.
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

        // 3. Извлечь функции
        var lines = content.Split('\n');
        var functionMatches = FunctionRegex.Matches(content);

        foreach (Match match in functionMatches)
        {
            try
            {
                var functionName = match.Groups["name"].Value;
                var functionStart = GetLineNumber(content, match.Index);
                var functionEnd = FindFunctionEnd(lines, functionStart);

                var functionContent = ExtractLines(lines, functionStart, functionEnd);
                var functionUnit = CreateFunctionUnit(
                    functionName,
                    functionContent,
                    filePath,
                    fileUnit.Id,
                    functionStart,
                    functionEnd
                );

                units.Add(functionUnit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to extract function at line {Line} in {FilePath}",
                    GetLineNumber(content, match.Index),
                    filePath
                );
            }
        }

        _logger.LogInformation(
            "Parsed PowerShell {FilePath}: extracted {Count} units",
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
                ["Extension"] = ".ps1",
                ["Language"] = "PowerShell",
            },
        };
    }

    /// <summary>
    /// Создать Function CodeUnit.
    /// </summary>
    private CodeUnit CreateFunctionUnit(
        string name,
        string content,
        string filePath,
        string parentId,
        int startLine,
        int endLine
    )
    {
        var contentHash = ContentNormalizer.ComputeContentHash(content);
        var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

        // Извлечь параметры если есть
        var parameters = ExtractParameters(content);

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
            Signature = $"function {name}({string.Join(", ", parameters)})",
            Embedding = null,
            Structure = null,
            CFG = null,
            ParentId = parentId,
            ChildIds = new HashSet<string>(),
            StartLine = startLine,
            EndLine = endLine,
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = parameters.Count,
                ["Parameters"] = parameters,
            },
        };
    }

    /// <summary>
    /// Извлечь параметры из контента функции.
    /// </summary>
    private List<string> ExtractParameters(string content)
    {
        var parameters = new List<string>();
        var paramMatch = ParamRegex.Match(content);

        if (!paramMatch.Success)
            return parameters;

        // Найти закрывающую скобку param блока
        var startIndex = paramMatch.Index + paramMatch.Length;
        var depth = 1;
        var endIndex = startIndex;

        for (int i = startIndex; i < content.Length && depth > 0; i++)
        {
            if (content[i] == '(')
                depth++;
            else if (content[i] == ')')
                depth--;
            endIndex = i;
        }

        if (depth == 0 && endIndex > startIndex)
        {
            var paramBlock = content.Substring(startIndex, endIndex - startIndex);

            // Простое извлечение параметров через запятую
            // (не учитывает сложные случаи вложенности, но достаточно для merge)
            var parts = paramBlock.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    // Извлечь имя параметра (после $ и до пробела/скобки)
                    var paramNameMatch = Regex.Match(trimmed, @"\$(?<name>[\w]+)");
                    if (paramNameMatch.Success)
                    {
                        parameters.Add(paramNameMatch.Groups["name"].Value);
                    }
                }
            }
        }

        return parameters;
    }

    /// <summary>
    /// Найти конец функции (закрывающую фигурную скобку).
    /// </summary>
    private int FindFunctionEnd(string[] lines, int startLine)
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

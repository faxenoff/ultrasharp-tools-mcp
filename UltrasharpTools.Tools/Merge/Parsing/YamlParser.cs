using YamlDotNet.RepresentationModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер YAML файлов для извлечения CodeUnits.
/// Поддерживает: .yaml, .yml
/// </summary>
public sealed class YamlParser
{
private readonly ILogger<YamlParser> _logger;
private readonly StructuralFingerprint _fingerprint;
private readonly ContentNormalizer _normalizer;

public YamlParser(
StructuralFingerprint fingerprint,
ContentNormalizer normalizer,
ILogger<YamlParser>? logger = null)
{
_fingerprint = fingerprint;
_normalizer = normalizer;
_logger = logger ?? NullLogger<YamlParser>.Instance;
}

/// <summary>
/// Парсить YAML файл и извлечь CodeUnits.
/// </summary>
public async Task<List<CodeUnit>> ParseFileAsync(
string filePath,
CancellationToken ct = default)
{
// 1. Нормализовать контент
var normalized = await _normalizer.NormalizeAsync(filePath, ct);
var content = normalized.Content;

// 2. Парсить YAML
YamlStream yaml;
try
{
yaml = new YamlStream();
using var reader = new StringReader(content);
yaml.Load(reader);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to parse YAML {FilePath}, using fallback", filePath);
// Если YAML невалидный, вернем только file-level unit
return new List<CodeUnit> { CreateFallbackFileUnit(filePath, content) };
}

// 3. Извлечь units
var units = new List<CodeUnit>();

// File-level unit
var fileUnit = CreateFileUnit(filePath, content);
units.Add(fileUnit);

// YAML может содержать несколько документов
var docIndex = 0;
foreach (var document in yaml.Documents)
{
if (document.RootNode != null)
{
var rootPath = docIndex > 0 ? $"doc[{docIndex}]" : "$";
ExtractNode(document.RootNode, rootPath, filePath, fileUnit.Id, units, lineNumber: 1);
}
docIndex++;
}

_logger.LogInformation(
"Parsed YAML {FilePath}: extracted {Count} units",
filePath,
units.Count);

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
["Extension"] = Path.GetExtension(filePath)
}
};
}

/// <summary>
/// Создать fallback file unit для невалидного YAML.
/// </summary>
private CodeUnit CreateFallbackFileUnit(string filePath, string content)
{
var contentHash = ContentNormalizer.ComputeContentHash(content);

return new CodeUnit
{
Id = $"file:{filePath}",
Type = CodeUnitType.File,
FilePath = filePath,
Name = Path.GetFileName(filePath),
FullyQualifiedName = filePath,
Content = content,
ContentHash = contentHash,
StructuralHash = contentHash,
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
/// Рекурсивно извлечь YAML узел.
/// </summary>
private void ExtractNode(
YamlNode node,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
switch (node)
{
case YamlMappingNode mapping:
ExtractMapping(mapping, path, filePath, parentId, units, lineNumber);
break;

case YamlSequenceNode sequence:
ExtractSequence(sequence, path, filePath, parentId, units, lineNumber);
break;

case YamlScalarNode scalar:
ExtractScalar(scalar, path, filePath, parentId, units, lineNumber);
break;
}
}

/// <summary>
/// Извлечь YAML Mapping (dict/object).
/// </summary>
private void ExtractMapping(
YamlMappingNode mapping,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
var content = mapping.ToString();
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

// Для YAML mapping используем Start.Line если доступно
if (mapping.Start.Line > 0)
{
lineNumber = (int)mapping.Start.Line;
}

var mappingUnit = new CodeUnit
{
Id = $"yaml-mapping:{path}@{filePath}",
Type = CodeUnitType.YamlMapping,
FilePath = filePath,
Name = path.Split('.', '/').Last(),
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = $"mapping {path}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = lineNumber,
EndLine = lineNumber + CountLines(content),
Metadata = new Dictionary<string, object>
{
["KeyCount"] = mapping.Children.Count
}
};

units.Add(mappingUnit);

// Рекурсивно обработать ключи
foreach (var entry in mapping.Children)
{
var keyNode = entry.Key as YamlScalarNode;
var key = keyNode?.Value ?? "unknown";
var childPath = $"{path}.{key}";
var childLineNumber = entry.Value.Start.Line > 0 ? (int)entry.Value.Start.Line : lineNumber;
ExtractNode(entry.Value, childPath, filePath, mappingUnit.Id, units, childLineNumber);
}
}

/// <summary>
/// Извлечь YAML Sequence (array).
/// </summary>
private void ExtractSequence(
YamlSequenceNode sequence,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
var content = sequence.ToString();
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

if (sequence.Start.Line > 0)
{
lineNumber = (int)sequence.Start.Line;
}

var sequenceUnit = new CodeUnit
{
Id = $"yaml-sequence:{path}@{filePath}",
Type = CodeUnitType.YamlSequence,
FilePath = filePath,
Name = path.Split('.', '/').Last(),
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = $"sequence {path}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = lineNumber,
EndLine = lineNumber + CountLines(content),
Metadata = new Dictionary<string, object>
{
["Length"] = sequence.Children.Count
}
};

units.Add(sequenceUnit);

// Рекурсивно обработать элементы
var index = 0;
foreach (var item in sequence.Children)
{
var itemPath = $"{path}[{index}]";
var itemLineNumber = item.Start.Line > 0 ? (int)item.Start.Line : lineNumber;
ExtractNode(item, itemPath, filePath, sequenceUnit.Id, units, itemLineNumber);
index++;
}
}

/// <summary>
/// Извлечь YAML Scalar (простое значение).
/// </summary>
private void ExtractScalar(
YamlScalarNode scalar,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
var content = scalar.Value ?? "";
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

if (scalar.Start.Line > 0)
{
lineNumber = (int)scalar.Start.Line;
}

var scalarUnit = new CodeUnit
{
Id = $"yaml-node:{path}@{filePath}",
Type = CodeUnitType.YamlNode,
FilePath = filePath,
Name = path.Split('.', '/').Last(),
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = $"{path}: {scalar.Style}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = lineNumber,
EndLine = lineNumber,
Metadata = new Dictionary<string, object>
{
["Value"] = content,
["Style"] = scalar.Style.ToString()
}
};

units.Add(scalarUnit);
}

/// <summary>
/// Подсчитать количество строк в тексте.
/// </summary>
private int CountLines(string text)
{
if (string.IsNullOrEmpty(text))
return 0;

return text.Count(c => c == '\n');
}
}

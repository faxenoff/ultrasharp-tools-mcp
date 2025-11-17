using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер JSON файлов для извлечения CodeUnits.
/// Полезно для больших JSON (например swagger.json).
/// </summary>
public sealed class JsonParser
{
private readonly ILogger<JsonParser> _logger;
private readonly StructuralFingerprint _fingerprint;
private readonly ContentNormalizer _normalizer;

public JsonParser(
StructuralFingerprint fingerprint,
ContentNormalizer normalizer,
ILogger<JsonParser>? logger = null)
{
_fingerprint = fingerprint;
_normalizer = normalizer;
_logger = logger ?? NullLogger<JsonParser>.Instance;
}

/// <summary>
/// Парсить JSON файл и извлечь CodeUnits.
/// </summary>
public async Task<List<CodeUnit>> ParseFileAsync(
string filePath,
CancellationToken ct = default)
{
// 1. Нормализовать контент
var normalized = await _normalizer.NormalizeAsync(filePath, ct);
var content = normalized.Content;

// 2. Парсить JSON
using var doc = JsonDocument.Parse(content);
var root = doc.RootElement;

// 3. Извлечь units
var units = new List<CodeUnit>();

// File-level unit
var fileUnit = CreateFileUnit(filePath, content);
units.Add(fileUnit);

// Root element
var rootPath = "$";
ExtractJsonElement(root, rootPath, filePath, fileUnit.Id, units, lineNumber: 1);

_logger.LogInformation(
"Parsed JSON {FilePath}: extracted {Count} units",
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
["Extension"] = ".json"
}
};
}

/// <summary>
/// Рекурсивно извлечь JSON элементы.
/// </summary>
private void ExtractJsonElement(
JsonElement element,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
switch (element.ValueKind)
{
case JsonValueKind.Object:
ExtractJsonObject(element, path, filePath, parentId, units, lineNumber);
break;

case JsonValueKind.Array:
ExtractJsonArray(element, path, filePath, parentId, units, lineNumber);
break;

// Primitive values не создаем отдельными units
}
}

/// <summary>
/// Извлечь JSON Object.
/// </summary>
private void ExtractJsonObject(
JsonElement obj,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
var content = obj.GetRawText();
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

var objUnit = new CodeUnit
{
Id = $"json-object:{path}@{filePath}",
Type = CodeUnitType.JsonObject,
FilePath = filePath,
Name = path.Split('.', '/').Last(),
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = $"object {path}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = lineNumber,
EndLine = lineNumber + CountLines(content),
Metadata = new Dictionary<string, object>
{
["PropertyCount"] = obj.EnumerateObject().Count()
}
};

units.Add(objUnit);

// Properties
int currentLine = lineNumber;
foreach (var property in obj.EnumerateObject())
{
var propPath = $"{path}.{property.Name}";
var propContent = property.Value.GetRawText();
var propContentHash = ContentNormalizer.ComputeContentHash(propContent);
var propStructuralHash = _fingerprint.ComputeStructuralHash(propContent, filePath);

var propUnit = new CodeUnit
{
Id = $"json-property:{propPath}@{filePath}",
Type = CodeUnitType.JsonProperty,
FilePath = filePath,
Name = property.Name,
FullyQualifiedName = propPath,
Content = propContent,
ContentHash = propContentHash,
StructuralHash = propStructuralHash,
Signature = $"{property.Name}: {property.Value.ValueKind}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = objUnit.Id,
ChildIds = new HashSet<string>(),
StartLine = currentLine,
EndLine = currentLine + CountLines(propContent),
Metadata = new Dictionary<string, object>
{
["ValueKind"] = property.Value.ValueKind.ToString()
}
};

units.Add(propUnit);

// Рекурсивно обработать значение
ExtractJsonElement(
property.Value,
propPath,
filePath,
propUnit.Id,
units,
currentLine);

currentLine += CountLines(propContent);
}
}

/// <summary>
/// Извлечь JSON Array.
/// </summary>
private void ExtractJsonArray(
JsonElement array,
string path,
string filePath,
string parentId,
List<CodeUnit> units,
int lineNumber)
{
var content = array.GetRawText();
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

var arrayUnit = new CodeUnit
{
Id = $"json-array:{path}@{filePath}",
Type = CodeUnitType.JsonArray,
FilePath = filePath,
Name = path.Split('.', '/').Last(),
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = $"array {path}",
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = lineNumber,
EndLine = lineNumber + CountLines(content),
Metadata = new Dictionary<string, object>
{
["Length"] = array.GetArrayLength()
}
};

units.Add(arrayUnit);

// Array elements (только для объектов/массивов)
int currentLine = lineNumber;
int index = 0;
foreach (var item in array.EnumerateArray())
{
if (item.ValueKind == JsonValueKind.Object ||
item.ValueKind == JsonValueKind.Array)
{
var itemPath = $"{path}[{index}]";
ExtractJsonElement(
item,
itemPath,
filePath,
arrayUnit.Id,
units,
currentLine);
}

currentLine += CountLines(item.GetRawText());
index++;
}
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

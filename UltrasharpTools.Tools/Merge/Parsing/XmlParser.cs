using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Indexing;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Parsing;

/// <summary>
/// Парсер XML файлов для извлечения CodeUnits.
/// Поддерживает: .xml, .csproj, .targets, .props
/// </summary>
public sealed class XmlParser
{
private readonly ILogger<XmlParser> _logger;
private readonly StructuralFingerprint _fingerprint;
private readonly ContentNormalizer _normalizer;

public XmlParser(
StructuralFingerprint fingerprint,
ContentNormalizer normalizer,
ILogger<XmlParser>? logger = null)
{
_fingerprint = fingerprint;
_normalizer = normalizer;
_logger = logger ?? NullLogger<XmlParser>.Instance;
}

/// <summary>
/// Парсить XML файл и извлечь CodeUnits.
/// </summary>
public async Task<List<CodeUnit>> ParseFileAsync(
string filePath,
CancellationToken ct = default)
{
// 1. Нормализовать контент
var normalized = await _normalizer.NormalizeAsync(filePath, ct);
var content = normalized.Content;

// 2. Парсить XML
XDocument doc;
try
{
doc = XDocument.Parse(content, LoadOptions.SetLineInfo);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to parse XML {FilePath}, using fallback", filePath);
// Если XML невалидный, вернем только file-level unit
return new List<CodeUnit> { CreateFallbackFileUnit(filePath, content) };
}

// 3. Извлечь units
var units = new List<CodeUnit>();

// File-level unit
var fileUnit = CreateFileUnit(filePath, content);
units.Add(fileUnit);

// Root element
if (doc.Root != null)
{
var rootPath = doc.Root.Name.LocalName;
ExtractElement(doc.Root, rootPath, filePath, fileUnit.Id, units);
}

_logger.LogInformation(
"Parsed XML {FilePath}: extracted {Count} units",
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
/// Создать fallback file unit для невалидного XML.
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
StructuralHash = contentHash, // Для fallback - тот же hash
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
/// Рекурсивно извлечь XML элемент и его дочерние элементы.
/// </summary>
private void ExtractElement(
XElement element,
string path,
string filePath,
string parentId,
List<CodeUnit> units)
{
var content = element.ToString();
var contentHash = ContentNormalizer.ComputeContentHash(content);
var structuralHash = _fingerprint.ComputeStructuralHash(content, filePath);

// Получить номер строки из LineInfo
var lineInfo = (IXmlLineInfo)element;
var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
var endLine = startLine + CountLines(content);

// Создать metadata с атрибутами
var metadata = new Dictionary<string, object>
{
["ElementName"] = element.Name.LocalName,
["ChildCount"] = element.Elements().Count()
};

// Добавить атрибуты в metadata
if (element.HasAttributes)
{
var attributes = new Dictionary<string, string>();
foreach (var attr in element.Attributes())
{
attributes[attr.Name.LocalName] = attr.Value;
}
metadata["Attributes"] = attributes;
}

// Создать CodeUnit для элемента
var elementUnit = new CodeUnit
{
Id = $"xml-element:{path}@{filePath}",
Type = CodeUnitType.XmlElement,
FilePath = filePath,
Name = element.Name.LocalName,
FullyQualifiedName = path,
Content = content,
ContentHash = contentHash,
StructuralHash = structuralHash,
Signature = BuildElementSignature(element),
Embedding = null,
Structure = null,
CFG = null,
ParentId = parentId,
ChildIds = new HashSet<string>(),
StartLine = startLine,
EndLine = endLine,
Metadata = metadata
};

units.Add(elementUnit);

// Рекурсивно обработать дочерние элементы
var childIndex = 0;
foreach (var child in element.Elements())
{
var childPath = $"{path}/{child.Name.LocalName}[{childIndex}]";
ExtractElement(child, childPath, filePath, elementUnit.Id, units);
childIndex++;
}
}

/// <summary>
/// Построить сигнатуру элемента (имя + ключевые атрибуты).
/// </summary>
private string BuildElementSignature(XElement element)
{
var signature = $"<{element.Name.LocalName}";

// Для .csproj важные атрибуты
var importantAttrs = new[] { "Include", "Update", "Remove", "Version", "Name", "Condition" };
foreach (var attrName in importantAttrs)
{
var attr = element.Attribute(attrName);
if (attr != null)
{
signature += $" {attrName}=\"{attr.Value}\"";
}
}

signature += ">";
return signature;
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

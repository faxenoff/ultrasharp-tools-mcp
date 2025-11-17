using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для форматирования C# кода с использованием Roslyn Formatter
/// </summary>
public class FormattingService(ILogger<FormattingService> logger) : IFormattingService
{
private readonly ILogger<FormattingService> _logger = logger;
private static readonly string[] SupportedExtensions = [".cs", ".csproj", ".xml"];

public async Task<FormattingResult> FormatAsync(
string path,
bool checkOnly,
CancellationToken cancellationToken = default
)
{
_logger.LogInformation("Starting formatting for path: {Path}, CheckOnly: {CheckOnly}", path, checkOnly);

var filesNeedingFormatting = new List<string>();
var filesFormatted = new List<string>();
var errors = new List<(string FilePath, string Error)>();

// Определяем файлы для форматирования
var filesToCheck = GetFilesToFormat(path);
if (filesToCheck.Count == 0)
{
_logger.LogWarning("No files found to format at path: {Path}", path);
return new FormattingResult
{
FilesNeedingFormatting = [],
FilesFormatted = [],
TotalFilesChecked = 0
};
}

_logger.LogInformation("Found {Count} files to check", filesToCheck.Count);

// ✅ OPTIMIZATION: Parallel file formatting with Task.WhenAll
var formatTasks = filesToCheck.Select(async filePath =>
{
try
{
var originalCode = await File.ReadAllTextAsync(filePath, cancellationToken);
var ext = Path.GetExtension(filePath).ToLowerInvariant();

string formattedCode;
if (ext == ".cs")
{
formattedCode = await FormatCSharpCodeAsync(originalCode, cancellationToken);
}
else // .csproj, .xml
{
formattedCode = FormatXmlCode(originalCode);
}

if (formattedCode != originalCode)
{
return (FilePath: filePath, NeedsFormatting: true, FormattedCode: formattedCode, Error: (string?)null);
}

return (FilePath: filePath, NeedsFormatting: false, FormattedCode: (string?)null, Error: (string?)null);
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to format file: {FilePath}", filePath);
return (FilePath: filePath, NeedsFormatting: false, FormattedCode: (string?)null, Error: ex.Message);
}
});

// Wait for all files to be processed in parallel
var formatResults = await Task.WhenAll(formatTasks);

// Process results
foreach (var result in formatResults)
{
if (result.Error != null)
{
errors.Add((result.FilePath, result.Error));
continue;
}

if (result.NeedsFormatting)
{
filesNeedingFormatting.Add(result.FilePath);

if (!checkOnly)
{
try
{
await File.WriteAllTextAsync(result.FilePath, result.FormattedCode!, cancellationToken);
filesFormatted.Add(result.FilePath);
_logger.LogInformation("Formatted file: {FilePath}", result.FilePath);
}
catch (Exception ex)
{
_logger.LogError(ex, "Failed to write formatted file: {FilePath}", result.FilePath);
errors.Add((result.FilePath, $"Write failed: {ex.Message}"));
}
}
}
}

_logger.LogInformation(
"Formatting complete. Total checked: {Total}, Need formatting: {NeedFormatting}, Formatted: {Formatted}",
filesToCheck.Count,
filesNeedingFormatting.Count,
filesFormatted.Count
);

return new FormattingResult
{
FilesNeedingFormatting = filesNeedingFormatting,
FilesFormatted = filesFormatted,
TotalFilesChecked = filesToCheck.Count,
Errors = errors
};
}

/// <summary>
/// Форматирует C# код с использованием Roslyn Formatter
/// </summary>
private async Task<string> FormatCSharpCodeAsync(string code, CancellationToken cancellationToken)
{
try
{
// Parse code into syntax tree
var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
var root = await tree.GetRootAsync(cancellationToken);

// Create workspace and apply formatting
using var workspace = new AdhocWorkspace();
var formattedRoot = Formatter.Format(root, workspace, cancellationToken: cancellationToken);

return formattedRoot.ToFullString();
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to format C# code with Roslyn, returning original");
return code;
}
}

/// <summary>
/// Форматирует XML код с использованием XDocument
/// </summary>
private string FormatXmlCode(string xml)
{
try
{
var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
return doc.ToString();
}
catch (Exception ex)
{
_logger.LogWarning(ex, "Failed to format XML code, returning original");
return xml;
}
}

private List<string> GetFilesToFormat(string path)
{
var filesToCheck = new List<string>();

if (File.Exists(path))
{
var ext = Path.GetExtension(path).ToLowerInvariant();
if (SupportedExtensions.Contains(ext))
{
filesToCheck.Add(path);
}
else
{
_logger.LogWarning("Unsupported file extension: {Ext}", ext);
}
}
else if (Directory.Exists(path))
{
foreach (var ext in SupportedExtensions)
{
filesToCheck.AddRange(
Directory.GetFiles(path, $"*{ext}", SearchOption.AllDirectories)
);
}
}
else
{
_logger.LogWarning("Path does not exist: {Path}", path);
}

return filesToCheck;
}
}

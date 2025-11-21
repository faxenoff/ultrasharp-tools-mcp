
using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Analysis;

/// <summary>
/// Классификация намерений изменений кода.
/// </summary>
public sealed class IntentClassifier
{
private readonly ILogger<IntentClassifier> _logger;

public IntentClassifier(ILogger<IntentClassifier>? logger = null)
{
_logger = logger ?? NullLogger<IntentClassifier>.Instance;
}

/// <summary>
/// Классифицировать изменение между двумя версиями unit.
/// </summary>
public ChangeIntent ClassifyChange(CodeUnit baseUnit, CodeUnit modifiedUnit)
{
// Эвристики для классификации
float confidence = 0.7f;
IntentType intentType;
string description;
var affectedSymbols = new List<string> { modifiedUnit.FullyQualifiedName };

// 1. Только whitespace/comments изменились
if (baseUnit.StructuralHash == modifiedUnit.StructuralHash)
{
intentType = IntentType.CodeCleanup;
description = "Formatting/comment changes only";
confidence = 0.95f;
}
// 2. Signature изменилась
else if (baseUnit.Signature != modifiedUnit.Signature)
{
intentType = IntentType.APIChange;
description = $"Signature changed from '{baseUnit.Signature}' to '{modifiedUnit.Signature}'";
confidence = 0.9f;
}
// 3. Содержимое изменилось
else
{
intentType = IntentType.Modification;
description = $"Content modified in {modifiedUnit.Name}";
confidence = 0.7f;
}

return new ChangeIntent
{
Type = intentType,
Description = description,
AffectedSymbols = affectedSymbols,
Confidence = confidence
};
}
}

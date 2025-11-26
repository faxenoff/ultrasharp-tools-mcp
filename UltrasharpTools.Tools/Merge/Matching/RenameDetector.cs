using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Matching;

/// <summary>
/// Обнаружение переименований методов/классов/переменных.
/// Использует структурное сопоставление AST игнорируя identifiers.
/// </summary>
public sealed class RenameDetector {
    private readonly ILogger<RenameDetector> _logger;

    // Пороги similarity для rename detection
    private const float HighConfidenceThreshold = 0.95f; // Очень похоже - скорее всего rename
    private const float MediumConfidenceThreshold = 0.85f; // Вероятно rename
    private const float LowConfidenceThreshold = 0.70f; // Возможно rename
    private static readonly char[] separator = new[] { '\r', '\n' };
    private static readonly char[] separatorArray = new[] { ' ', '\t' };

    public RenameDetector(ILogger<RenameDetector>? logger = null) {
        _logger = logger ?? NullLogger<RenameDetector>.Instance;
    }

    /// <summary>
    /// Проверить, является ли unitB переименованной версией unitA.
    /// </summary>
    public RenameDetectionResult? DetectRename(CodeUnit unitA, CodeUnit unitB) {
        // Проверить базовые критерии
        if (unitA.Type != unitB.Type) {
            // Разные типы - точно не rename
            return null;
        }

        // Проверить совпадение сигнатуры (для методов)
        var signatureMatch = CheckSignatureMatch(unitA, unitB);
        if (!signatureMatch) {
            // Разные сигнатуры - скорее всего не rename
            return null;
        }

        // Вычислить структурное сходство без имён
        var structuralSimilarity = ComputeStructuralSimilarityIgnoringNames(unitA, unitB);

        if (structuralSimilarity < LowConfidenceThreshold) {
            // Слишком разная структура - не rename
            return null;
        }

        // Вычислить composite score
        var score = ComputeRenameScore(unitA, unitB, structuralSimilarity);

        if (score < LowConfidenceThreshold) {
            return null;
        }

        var confidence = ClassifyConfidence(score);

        _logger.LogDebug(
            "Rename detected: {NameA} -> {NameB} (similarity: {Similarity:F3}, confidence: {Confidence})",
            unitA.Name,
            unitB.Name,
            structuralSimilarity,
            confidence
        );

        return new RenameDetectionResult {
            OriginalUnit = unitA,
            RenamedUnit = unitB,
            StructuralSimilarity = structuralSimilarity,
            Confidence = confidence,
            Score = score,
        };
    }

    /// <summary>
    /// Проверить совпадение сигнатуры (для методов: параметры, возвращаемый тип).
    /// </summary>
    private bool CheckSignatureMatch(CodeUnit unitA, CodeUnit unitB) {
        // Для не-методов всегда true
        if (unitA.Type != CodeUnitType.Method) {
            return true;
        }

        // Парсим оба метода
        var treeA = CSharpSyntaxTree.ParseText(unitA.Content);
        var treeB = CSharpSyntaxTree.ParseText(unitB.Content);

        var methodA = treeA
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        var methodB = treeB
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (methodA == null || methodB == null) {
            // Если не смогли распарсить как метод - используем текстовую проверку сигнатуры
            // Извлекаем сигнатуру из первой строки (обычно это объявление метода)
            var signatureA = ExtractMethodSignatureFromContent(unitA.Content);
            var signatureB = ExtractMethodSignatureFromContent(unitB.Content);

            // Сравниваем нормализованные сигнатуры
            return AreSignaturesSimilar(signatureA, signatureB);
        }

        // Сравнить возвращаемые типы
        var returnTypeA = methodA.ReturnType.ToString();
        var returnTypeB = methodB.ReturnType.ToString();

        if (returnTypeA != returnTypeB) {
            return false;
        }

        // Сравнить параметры (типы, модификаторы, но не имена)
        var paramsA = methodA.ParameterList.Parameters;
        var paramsB = methodB.ParameterList.Parameters;

        if (paramsA.Count != paramsB.Count) {
            return false;
        }

        for (int i = 0; i < paramsA.Count; i++) {
            var paramA = paramsA[i];
            var paramB = paramsB[i];

            // Сравнить типы
            if (paramA.Type?.ToString() != paramB.Type?.ToString()) {
                return false;
            }

            // Сравнить модификаторы (ref/out/in/params)
            if (
                !paramA
                    .Modifiers.Select(m => m.ValueText)
                    .SequenceEqual(paramB.Modifiers.Select(m => m.ValueText))
            ) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Вычислить структурное сходство AST игнорируя identifiers.
    /// </summary>
    private float ComputeStructuralSimilarityIgnoringNames(CodeUnit unitA, CodeUnit unitB) {
        try {
            // Парсим оба куска кода
            var treeA = CSharpSyntaxTree.ParseText(unitA.Content);
            var treeB = CSharpSyntaxTree.ParseText(unitB.Content);

            var rootA = treeA.GetRoot();
            var rootB = treeB.GetRoot();

            // Нормализуем AST: заменяем все identifiers на placeholder
            var normalizedA = NormalizeIdentifiers(rootA);
            var normalizedB = NormalizeIdentifiers(rootB);

            // Сравниваем normalized AST
            var textA = normalizedA.ToFullString();
            var textB = normalizedB.ToFullString();

            // Вычисляем similarity через normalized Levenshtein
            var similarity = ComputeTextSimilarity(textA, textB);

            return similarity;
        } catch (Exception ex) {
            _logger.LogWarning(
                ex,
                "Failed to compute structural similarity for {IdA} vs {IdB}",
                unitA.Id,
                unitB.Id
            );

            // Fallback: простое текстовое сравнение
            return ComputeTextSimilarity(unitA.Content, unitB.Content);
        }
    }

    /// <summary>
    /// Нормализовать AST: заменить все identifiers на плейсхолдеры.
    /// </summary>
    private SyntaxNode NormalizeIdentifiers(SyntaxNode node) {
        // Rewriter который заменяет identifiers
        var rewriter = new IdentifierNormalizingRewriter();
        return rewriter.Visit(node);
    }

    /// <summary>
    /// Rewriter для нормализации identifiers в AST.
    /// </summary>
    private class IdentifierNormalizingRewriter : CSharpSyntaxRewriter {
        private int _counter = 0;
        private readonly Dictionary<string, string> _nameMapping = new();

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var originalName = node.Identifier.ValueText;

            // Не нормализуем ключевые слова и примитивные типы
            if (IsKeywordOrPrimitiveType(originalName)) {
                return base.VisitIdentifierName(node);
            }

            // Заменяем на placeholder
            if (!_nameMapping.TryGetValue(originalName, out var placeholder)) {
                placeholder = $"ID{_counter++}";
                _nameMapping[originalName] = placeholder;
            }

            return node.WithIdentifier(SyntaxFactory.Identifier(placeholder));
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node) {
            // Нормализуем имя параметра
            var originalName = node.Identifier.ValueText;

            if (!_nameMapping.TryGetValue(originalName, out var placeholder)) {
                placeholder = $"PARAM{_counter++}";
                _nameMapping[originalName] = placeholder;
            }

            var normalized = node.WithIdentifier(SyntaxFactory.Identifier(placeholder));
            return base.VisitParameter(normalized);
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node) {
            // Нормализуем имя переменной
            var originalName = node.Identifier.ValueText;

            if (!_nameMapping.TryGetValue(originalName, out var placeholder)) {
                placeholder = $"VAR{_counter++}";
                _nameMapping[originalName] = placeholder;
            }

            var normalized = node.WithIdentifier(SyntaxFactory.Identifier(placeholder));
            return base.VisitVariableDeclarator(normalized);
        }

        private bool IsKeywordOrPrimitiveType(string name) {
            // Примитивные типы C#
            var primitives = new HashSet<string>
            {
                "int",
                "long",
                "short",
                "byte",
                "sbyte",
                "uint",
                "ulong",
                "ushort",
                "float",
                "double",
                "decimal",
                "bool",
                "char",
                "string",
                "object",
                "void",
                "var",
                "dynamic",
            };

            return primitives.Contains(name)
                || SyntaxFacts.IsKeywordKind(SyntaxFacts.GetKeywordKind(name));
        }
    }

    /// <summary>
    /// Вычислить текстовое сходство двух строк (normalized Levenshtein).
    /// </summary>
    private float ComputeTextSimilarity(string textA, string textB) {
        // Нормализуем whitespace
        textA = NormalizeWhitespace(textA);
        textB = NormalizeWhitespace(textB);

        var distance = LevenshteinDistance(textA, textB);
        var maxLength = Math.Max(textA.Length, textB.Length);

        if (maxLength == 0) {
            return 1.0f;
        }

        return 1.0f - (float)distance / maxLength;
    }

    /// <summary>
    /// Нормализовать whitespace в тексте.
    /// </summary>
    private string NormalizeWhitespace(string text) {
        // Заменить все whitespace на пробелы
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    /// <summary>
    /// Levenshtein distance между строками.
    /// </summary>
    private int LevenshteinDistance(string s1, string s2) {
        var len1 = s1.Length;
        var len2 = s2.Length;
        var matrix = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= len2; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= len1; i++) {
            for (int j = 1; j <= len2; j++) {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1, // deletion
                        matrix[i, j - 1] + 1
                    ), // insertion
                    matrix[i - 1, j - 1] + cost
                ); // substitution
            }
        }

        return matrix[len1, len2];
    }

    /// <summary>
    /// Вычислить composite score для rename detection.
    /// </summary>
    private float ComputeRenameScore(CodeUnit unitA, CodeUnit unitB, float structuralSimilarity) {
        float score = structuralSimilarity;

        // Bonus: одинаковая позиция в файле (относительная)
        var positionSimilarity = ComputePositionSimilarity(unitA, unitB);
        score += positionSimilarity * 0.05f;

        // Bonus: похожие имена (но меньший вес чем структура)
        var nameSimilarity = ComputeNameSimilarity(unitA.Name, unitB.Name);
        score += nameSimilarity * 0.05f;

        // Penalty: сильно разная длина
        var lengthRatio =
            (float)Math.Min(unitA.Content.Length, unitB.Content.Length)
            / Math.Max(unitA.Content.Length, unitB.Content.Length);

        if (lengthRatio < 0.7f) {
            score -= 0.1f;
        }

        return Math.Clamp(score, 0f, 1f);
    }

    /// <summary>
    /// Вычислить similarity позиций в файле (относительная позиция).
    /// </summary>
    private float ComputePositionSimilarity(CodeUnit unitA, CodeUnit unitB) {
        // Если разные файлы - низкий score
        if (!IsSameFileOrSimilarPath(unitA.FilePath, unitB.FilePath)) {
            return 0f;
        }

        // Вычислить относительную позицию (0..1)
        // Для простоты используем StartLine, но можно улучшить

        // TODO: получить общее количество строк в файле
        // Пока используем эвристику
        var positionDiff = Math.Abs(unitA.StartLine - unitB.StartLine);

        // Если позиции очень близко - высокий score
        if (positionDiff <= 5) {
            return 1.0f;
        } else if (positionDiff <= 20) {
            return 0.7f;
        } else if (positionDiff <= 50) {
            return 0.3f;
        }

        return 0f;
    }

    /// <summary>
    /// Проверить, тот же ли это файл или похожий путь.
    /// </summary>
    private bool IsSameFileOrSimilarPath(string pathA, string pathB) {
        var fileNameA = Path.GetFileName(pathA);
        var fileNameB = Path.GetFileName(pathB);

        // Одинаковое имя файла
        return string.Equals(fileNameA, fileNameB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Вычислить similarity имён.
    /// </summary>
    private float ComputeNameSimilarity(string nameA, string nameB) {
        if (string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase)) {
            return 1.0f;
        }

        var distance = LevenshteinDistance(nameA.ToLowerInvariant(), nameB.ToLowerInvariant());

        var maxLength = Math.Max(nameA.Length, nameB.Length);
        if (maxLength == 0) {
            return 1.0f;
        }

        return 1.0f - (float)distance / maxLength;
    }

    /// <summary>
    /// Классифицировать confidence по score.
    /// </summary>
    private RenameConfidence ClassifyConfidence(float score) {
        if (score >= HighConfidenceThreshold) {
            return RenameConfidence.High;
        } else if (score >= MediumConfidenceThreshold) {
            return RenameConfidence.Medium;
        } else {
            return RenameConfidence.Low;
        }
    }

    /// <summary>
    /// Batch rename detection для нескольких units.
    /// </summary>
    public Dictionary<string, RenameDetectionResult> DetectRenamesBatch(
        List<CodeUnit> sourceUnits,
        List<CodeUnit> targetUnits
    ) {
        _logger.LogDebug(
            "Performing batch rename detection for {SourceCount} source units vs {TargetCount} target units",
            sourceUnits.Count,
            targetUnits.Count
        );

        var results = new Dictionary<string, RenameDetectionResult>();

        // Фильтруем target units по типу для оптимизации
        var targetByType = targetUnits
            .GroupBy(u => u.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var sourceUnit in sourceUnits) {
            // Ищем только среди units того же типа
            if (!targetByType.TryGetValue(sourceUnit.Type, out var candidates)) {
                continue;
            }

            RenameDetectionResult? bestMatch = null;
            float bestScore = 0f;

            foreach (var candidate in candidates) {
                var result = DetectRename(sourceUnit, candidate);

                if (result != null && result.Score > bestScore) {
                    bestScore = result.Score;
                    bestMatch = result;
                }
            }

            if (bestMatch != null && bestScore >= LowConfidenceThreshold) {
                results[sourceUnit.Id] = bestMatch;
            }
        }

        _logger.LogDebug(
            "Rename detection: {Matched}/{Total} renames detected",
            results.Count,
            sourceUnits.Count
        );

        return results;
    }

    /// <summary>
    /// Извлечь сигнатуру метода из content (первая строка с объявлением).
    /// </summary>
    private string ExtractMethodSignatureFromContent(string content) {
        // Берём первую строку (обычно это объявление метода)
        var lines = content.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) {
            return string.Empty;
        }

        var signature = lines[0].Trim();

        // Убираем модификаторы доступа для сравнения
        signature = System.Text.RegularExpressions.Regex.Replace(
            signature,
            @"\b(public|private|protected|internal|static|virtual|override|abstract|sealed|async)\s+",
            ""
        );

        return signature;
    }

    /// <summary>
    /// Сравнить сигнатуры методов (игнорируя имена параметров).
    /// </summary>
    private bool AreSignaturesSimilar(string signatureA, string signatureB) {
        // Нормализуем: убираем имена параметров, оставляем только типы
        var normalizedA = NormalizeSignature(signatureA);
        var normalizedB = NormalizeSignature(signatureB);

        return normalizedA == normalizedB;
    }

    /// <summary>
    /// Нормализовать сигнатуру: убрать имя метода и имена параметров, оставить только типы.
    /// </summary>
    private string NormalizeSignature(string signature) {
        // Простая эвристика: убираем имена параметров в скобках
        // Например: "void Method(int x, string y)" -> "void (int,string)"
        // Также убираем имя метода, оставляя только возвращаемый тип и типы параметров

        // Найти скобки с параметрами
        var match = System.Text.RegularExpressions.Regex.Match(signature, @"\(([^)]*)\)");
        if (!match.Success) {
            // Нет параметров - вернуть только возвращаемый тип
            var beforeParen = signature.Split('(')[0].Trim();
            // Убрать имя метода (последнее слово перед скобкой)
            var words = beforeParen.Split(
                separatorArray,
                StringSplitOptions.RemoveEmptyEntries
            );
            if (words.Length > 0) {
                // Возвращаемый тип - всё кроме последнего слова (имени метода)
                return words.Length > 1 ? string.Join(" ", words.Take(words.Length - 1)) : words[0];
            }
            return signature;
        }

        var paramsText = match.Groups[1].Value;
        var paramParts = paramsText.Split(',');

        // Для каждого параметра оставляем только тип (первое слово после модификаторов)
        var normalizedParams = new List<string>();
        foreach (var param in paramParts) {
            var trimmed = param.Trim();
            if (string.IsNullOrEmpty(trimmed)) {
                continue;
            }

            // Убрать модификаторы (ref/out/in/params)
            trimmed = System.Text.RegularExpressions.Regex.Replace(
                trimmed,
                @"^(ref|out|in|params)\s+",
                ""
            );

            // Взять первое слово (тип)
            var words = trimmed.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0) {
                normalizedParams.Add(words[0]);
            }
        }

        var normalizedParamsStr = string.Join(",", normalizedParams);

        // Извлечь возвращаемый тип (всё до имени метода перед скобкой)
        var beforeParams = signature.Substring(0, match.Index).Trim();
        var returnTypeParts = beforeParams.Split(
            separatorArray,
            StringSplitOptions.RemoveEmptyEntries
        );

        // Возвращаемый тип - всё кроме последнего слова (имени метода)
        var returnType =
            returnTypeParts.Length > 1
                ? string.Join(" ", returnTypeParts.Take(returnTypeParts.Length - 1))
            : returnTypeParts.Length == 1 ? returnTypeParts[0]
            : "";

        return $"{returnType}({normalizedParamsStr})";
    }
}

/// <summary>
/// Результат rename detection.
/// </summary>
public sealed record RenameDetectionResult {
    public required CodeUnit OriginalUnit { get; init; }
    public required CodeUnit RenamedUnit { get; init; }
    public required float StructuralSimilarity { get; init; }
    public required RenameConfidence Confidence { get; init; }
    public required float Score { get; init; }
}

/// <summary>
/// Уровень уверенности в rename detection.
/// </summary>
public enum RenameConfidence {
    Low, // >= 0.70 - возможно rename
    Medium, // >= 0.85 - вероятно rename
    High, // >= 0.95 - скорее всего rename
}

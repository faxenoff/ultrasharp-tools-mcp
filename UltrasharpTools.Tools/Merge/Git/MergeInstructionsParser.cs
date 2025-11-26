using UltrasharpTools.Tools.Merge.Models;

namespace UltrasharpTools.Tools.Merge.Git;

/// <summary>
/// Парсит инструкции для merge из естественного языка.
/// Поддерживает русский и английский языки.
/// </summary>
public static class MergeInstructionsParser {
    // Паттерны для исключения файлов
    private static readonly (Regex Pattern, string[] Patterns)[] ExcludeRules = new[]
    {
(new Regex(@"(?:не\s+мерж|игнор|исключ|пропуст|skip|exclude|ignore)\w*\s+(?:файл\w*\s+)?(.+?)(?:\s*[,;]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
Array.Empty<string>()),

(new Regex(@"swagger\s+(?:файл\w*\s+)?(?:не|исключ|игнор|skip)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "*.swagger.json", "**/swagger/**" }),

(new Regex(@"(?:не|исключ|игнор|skip)\w*\s+swagger", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "*.swagger.json", "**/swagger/**" }),

(new Regex(@"appsettings?\s+(?:не|исключ|игнор|skip)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "appsettings*.json" }),

(new Regex(@"(?:не|исключ|игнор|skip)\w*\s+appsettings?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "appsettings*.json" }),

(new Regex(@"(?:не|исключ|игнор|skip)\w*\s+(?:json|\.json)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "*.json" }),

(new Regex(@"(?:не|исключ|игнор|skip)\w*\s+(?:config|конфиг)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new[] { "*.config", "*.json", "web.config", "app.config" }),
};

    // Паттерны для приоритета веток
    private static readonly (Regex Pattern, BranchPriority Priority, string[]? Keywords)[] PriorityRules = new[]
    {
// "в приоритете на ветке source/feature/release"
(new Regex(@"(?:приоритет|prefer|priority)\w*\s+(?:на\s+)?(?:ветк[еуи]|branch)?\s*[:\s]?\s*(source|target|feature|main|master|release\S*)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
BranchPriority.PreferSource,
(string[]?)null),

// "кеширование в приоритете на source"
(new Regex(@"(кеш\w*|cach\w*|token\w*|токен\w*)\s+(?:в\s+)?приоритет\w*\s+(?:на\s+)?(source|target|feature)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
BranchPriority.PreferSource,
new[] { "cache", "caching", "token", "кеш" }),

// "брать из source"
(new Regex(@"(?:брать|взять|take|use)\s+(?:из|from)\s+(source|target|feature)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
BranchPriority.PreferSource,
null),

// "source имеет приоритет"
(new Regex(@"(source|target|feature)\s+(?:имеет|has)\s+приоритет", RegexOptions.IgnoreCase | RegexOptions.Compiled),
BranchPriority.PreferSource,
null),
};

    // Ключевые слова для приоритета source
    private static readonly Regex SourceKeywordsPattern = new(
    @"(?:приоритет|prefer)\w*\s+(?:source|feature|откуда)\s+(?:для|for)?\s*[:\s]?\s*(.+?)(?:\s*[,;]|$)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Ключевые слова для приоритета target
    private static readonly Regex TargetKeywordsPattern = new(
    @"(?:приоритет|prefer)\w*\s+(?:target|main|master|куда)\s+(?:для|for)?\s*[:\s]?\s*(.+?)(?:\s*[,;]|$)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Сохранять форматирование
    private static readonly Regex PreserveFormattingPattern = new(
    @"(?:сохран|preserv)\w*\s+(?:формат|format|стиль|style)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Разделители для Split операций (CA1861)
    private static readonly char[] InstructionSeparators = [';', '\n', '\r'];
    private static readonly char[] TokenSeparators = [',', ' ', '\t'];

    // Стоп-слова (CA1861)
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase) {
        "файл", "файлы", "файлов", "files", "file",
        "и", "или", "а", "но", "в", "на", "из", "для",
        "and", "or", "the", "a", "an", "for", "from", "to", "in", "of",
        "не", "нет", "not", "no",
        "все", "всё", "all", "any",
    };

    /// <summary>
    /// Распарсить инструкции из текста.
    /// </summary>
    public static MergeInstructions Parse(string? instructions) {
        if (string.IsNullOrWhiteSpace(instructions))
            return MergeInstructions.Empty;

        var excludePatterns = new List<string>();
        var includePatterns = new List<string>();
        var branchPriorities = new Dictionary<string, BranchPriority>();
        var preferSourceKeywords = new List<string>();
        var preferTargetKeywords = new List<string>();
        var preserveFormatting = false;

        // Разбить на отдельные инструкции
        var parts = instructions.Split(InstructionSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts) {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Проверить паттерны исключения
            foreach (var (pattern, defaultPatterns) in ExcludeRules) {
                var match = pattern.Match(trimmed);
                if (match.Success) {
                    if (defaultPatterns.Length > 0) {
                        excludePatterns.AddRange(defaultPatterns);
                    } else if (match.Groups.Count > 1) {
                        var extracted = ExtractFilePatterns(match.Groups[1].Value);
                        excludePatterns.AddRange(extracted);
                    }
                }
            }

            // Проверить паттерны приоритета
            foreach (var (pattern, priority, keywords) in PriorityRules) {
                var match = pattern.Match(trimmed);
                if (match.Success) {
                    if (keywords != null) {
                        preferSourceKeywords.AddRange(keywords);
                    } else if (match.Groups.Count > 1) {
                        var branchName = match.Groups[1].Value.ToLowerInvariant();
                        var actualPriority = branchName switch {
                            "source" or "feature" => BranchPriority.PreferSource,
                            "target" or "main" or "master" => BranchPriority.PreferTarget,
                            _ when branchName.StartsWith("release") => BranchPriority.PreferSource,
                            _ => priority
                        };
                        branchPriorities["*"] = actualPriority;
                    }
                }
            }

            // Проверить ключевые слова для source
            var sourceMatch = SourceKeywordsPattern.Match(trimmed);
            if (sourceMatch.Success && sourceMatch.Groups.Count > 1) {
                var keywords = ExtractKeywords(sourceMatch.Groups[1].Value);
                preferSourceKeywords.AddRange(keywords);
            }

            // Проверить ключевые слова для target
            var targetMatch = TargetKeywordsPattern.Match(trimmed);
            if (targetMatch.Success && targetMatch.Groups.Count > 1) {
                var keywords = ExtractKeywords(targetMatch.Groups[1].Value);
                preferTargetKeywords.AddRange(keywords);
            }

            // Проверить сохранение форматирования
            if (PreserveFormattingPattern.IsMatch(trimmed)) {
                preserveFormatting = true;
            }
        }

        return new MergeInstructions {
            RawInstructions = instructions,
            ExcludePatterns = excludePatterns.Distinct().ToList(),
            IncludePatterns = includePatterns.Distinct().ToList(),
            BranchPriorities = branchPriorities,
            PreferSourceKeywords = preferSourceKeywords.Distinct().ToList(),
            PreferTargetKeywords = preferTargetKeywords.Distinct().ToList(),
            PreserveTargetFormatting = preserveFormatting,
            AutoResolveConflicts = true,
        };
    }

    private static List<string> ExtractFilePatterns(string text) {
        var patterns = new List<string>();
        var parts = text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts) {
            var trimmed = part.Trim().ToLowerInvariant();

            // Пропустить служебные слова
            if (IsStopWord(trimmed))
                continue;

            // Добавить расширение если похоже на тип файла
            if (trimmed.Length <= 10 && !trimmed.Contains('.') && !trimmed.Contains('/')) {
                if (trimmed == "swagger")
                    patterns.Add("*.swagger.json");
                else if (trimmed == "json")
                    patterns.Add("*.json");
                else if (trimmed == "cs")
                    patterns.Add("*.cs");
                else if (trimmed == "config")
                    patterns.Add("*.config");
                else
                    patterns.Add($"*{trimmed}*");
            } else if (trimmed.StartsWith("*.") || trimmed.Contains("/")) {
                patterns.Add(trimmed);
            } else if (trimmed.Contains(".")) {
                patterns.Add($"*{trimmed}");
            }
        }

        return patterns;
    }

    private static List<string> ExtractKeywords(string text) {
        var keywords = new List<string>();
        var parts = text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts) {
            var trimmed = part.Trim().ToLowerInvariant();
            if (!IsStopWord(trimmed) && trimmed.Length > 2) {
                keywords.Add(trimmed);
            }
        }

        return keywords;
    }

    private static bool IsStopWord(string word) => StopWords.Contains(word);
}

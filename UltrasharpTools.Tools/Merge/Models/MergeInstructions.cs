namespace UltrasharpTools.Tools.Merge.Models;

/// <summary>
/// Инструкции для семантического merge.
/// Позволяют настраивать поведение merge через естественный язык.
/// </summary>
public sealed record MergeInstructions {
    /// <summary>
    /// Оригинальный текст инструкций от пользователя.
    /// </summary>
    public string RawInstructions { get; init; } = "";

    /// <summary>
    /// Паттерны файлов для исключения из merge.
    /// Например: ["*.swagger.json", "appsettings.*.json"]
    /// </summary>
    public List<string> ExcludePatterns { get; init; } = new();

    /// <summary>
    /// Паттерны файлов для включения (если указано, остальные игнорируются).
    /// </summary>
    public List<string> IncludePatterns { get; init; } = new();

    /// <summary>
    /// Правила приоритета веток для конкретных паттернов.
    /// Key = паттерн файла/кода, Value = какую ветку предпочесть (source/target)
    /// </summary>
    public Dictionary<string, BranchPriority> BranchPriorities { get; init; } = new();

    /// <summary>
    /// Ключевые слова/концепции для приоритета source ветки.
    /// Например: ["кеширование", "caching", "token"]
    /// </summary>
    public List<string> PreferSourceKeywords { get; init; } = new();

    /// <summary>
    /// Ключевые слова/концепции для приоритета target ветки.
    /// </summary>
    public List<string> PreferTargetKeywords { get; init; } = new();

    /// <summary>
    /// Сохранять форматирование target ветки.
    /// </summary>
    public bool PreserveTargetFormatting { get; init; } = false;

    /// <summary>
    /// Автоматически разрешать конфликты по инструкциям.
    /// </summary>
    public bool AutoResolveConflicts { get; init; } = true;

    /// <summary>
    /// Создать пустые инструкции.
    /// </summary>
    public static MergeInstructions Empty => new();

    /// <summary>
    /// Проверить должен ли файл быть исключён.
    /// </summary>
    public bool ShouldExcludeFile(string filePath) {
        foreach (var pattern in ExcludePatterns) {
            if (MatchesPattern(filePath, pattern))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Проверить должен ли файл быть включён.
    /// </summary>
    public bool ShouldIncludeFile(string filePath) {
        if (IncludePatterns.Count == 0)
            return true;

        foreach (var pattern in IncludePatterns) {
            if (MatchesPattern(filePath, pattern))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Получить приоритет ветки для файла/контента.
    /// </summary>
    public BranchPriority GetBranchPriority(string filePath, string? content = null) {
        // Проверить паттерны файлов
        foreach (var (pattern, priority) in BranchPriorities) {
            if (MatchesPattern(filePath, pattern))
                return priority;
        }

        // Проверить ключевые слова в контенте
        if (!string.IsNullOrEmpty(content)) {
            foreach (var keyword in PreferSourceKeywords) {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return BranchPriority.PreferSource;
            }

            foreach (var keyword in PreferTargetKeywords) {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return BranchPriority.PreferTarget;
            }
        }

        return BranchPriority.Auto;
    }

    private static bool MatchesPattern(string filePath, string pattern) {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // Нормализуем путь
        filePath = filePath.Replace('\\', '/').ToLowerInvariant();
        pattern = pattern.Replace('\\', '/').ToLowerInvariant();

        // Простой glob
        if (pattern.StartsWith("*.")) {
            return filePath.EndsWith(pattern.Substring(1));
        }

        if (pattern.StartsWith("**/")) {
            return filePath.Contains(pattern.Substring(3));
        }

        if (pattern.Contains("*")) {
            var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/]*") + "$";
            return Regex.IsMatch(filePath, regexPattern);
        }

        return filePath.Contains(pattern);
    }
}

/// <summary>
/// Приоритет ветки при конфликтах.
/// </summary>
public enum BranchPriority {
    /// <summary>
    /// Автоматически определить.
    /// </summary>
    Auto,

    /// <summary>
    /// Предпочитать source ветку.
    /// </summary>
    PreferSource,

    /// <summary>
    /// Предпочитать target ветку.
    /// </summary>
    PreferTarget,

    /// <summary>
    /// Требует ручного разрешения.
    /// </summary>
    Manual
}

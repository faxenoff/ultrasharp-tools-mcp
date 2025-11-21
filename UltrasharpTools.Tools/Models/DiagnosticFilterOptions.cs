namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Опции фильтрации диагностик
/// </summary>
public record DiagnosticFilterOptions
{
    /// <summary>
    /// Минимальный уровень серьезности
    /// </summary>
    public DiagnosticSeverity SeverityFilter { get; init; } = DiagnosticSeverity.Warning;

    /// <summary>
    /// Конкретные коды диагностик для фильтрации (CA1822, CS8019, и т.д.)
    /// Если null или пусто - показать все
    /// </summary>
    public IReadOnlyCollection<string>? DiagnosticIds { get; init; }

    /// <summary>
    /// Glob patterns для фильтрации файлов (например, "**/Services/*.cs")
    /// Если null или пусто - показать все файлы
    /// </summary>
    public IReadOnlyCollection<string>? FilePatterns { get; init; }

    /// <summary>
    /// Имена проектов для фильтрации
    /// Если null или пусто - показать все проекты
    /// </summary>
    public IReadOnlyCollection<string>? ProjectNames { get; init; }

    /// <summary>
    /// Имя preset для быстрого выбора наборов правил
    /// Например: "performance", "security", "critical", "high"
    /// </summary>
    public string? PresetName { get; init; }

    /// <summary>
    /// Пропустить результатов (для пагинации)
    /// </summary>
    public int Skip { get; init; } = 0;

    /// <summary>
    /// Взять результатов (для пагинации)
    /// </summary>
    public int Take { get; init; } = 100;

    /// <summary>
    /// Проверяет, нужно ли включить диагностику в результаты
    /// </summary>
    public bool ShouldIncludeDiagnostic(Diagnostic diagnostic, string filePath, string projectName)
    {
        // Severity filter
        if (diagnostic.Severity < SeverityFilter)
            return false;

        // Suppressed diagnostics
        if (diagnostic.IsSuppressed)
            return false;

        // Diagnostic ID filter
        if (DiagnosticIds?.Count() > 0)
        {
            if (!DiagnosticIds.Contains(diagnostic.Id))
                return false;
        }

        // Project name filter
        if (ProjectNames?.Count() > 0)
        {
            if (!ProjectNames.Contains(projectName, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // File pattern filter
        if (FilePatterns?.Count() > 0)
        {
            var normalizedPath = filePath.Replace('\\', '/');
            var matched = false;

            foreach (var pattern in FilePatterns)
            {
                var normalizedPattern = pattern.Replace('\\', '/');

                // Simple glob matching: ** for any path, * for any characters
                if (MatchesGlobPattern(normalizedPath, normalizedPattern))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Простое сопоставление с glob pattern
    /// </summary>
    private static bool MatchesGlobPattern(string path, string pattern)
    {
        // Поддержка ** для любого количества директорий
        if (pattern.Contains("**"))
        {
            var parts = pattern.Split("**", StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return true;

            if (parts.Length == 1)
            {
                // Только начало или конец
                if (pattern.StartsWith("**"))
                    return path.EndsWith(parts[0].TrimStart('/'));
                else
                    return path.StartsWith(parts[0].TrimEnd('/'));
            }

            // И начало, и конец
            return path.StartsWith(parts[0].TrimEnd('/')) &&
                   path.EndsWith(parts[1].TrimStart('/'));
        }

        // Простое сопоставление с *
        if (pattern.Contains('*'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                path,
                regex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        // Точное совпадение или содержание
        return path.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
               path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создает опции с применением preset
    /// </summary>
    public static DiagnosticFilterOptions WithPreset(string presetName, DiagnosticSeverity? severityFilter = null)
    {
        var diagnosticIds = DiagnosticPresets.GetPreset(presetName);

        if (diagnosticIds == null)
        {
            throw new ArgumentException(
                $"Unknown preset: {presetName}. Available: {string.Join(", ", DiagnosticPresets.GetAvailablePresets())}",
                nameof(presetName)
            );
        }

        return new DiagnosticFilterOptions
        {
            PresetName = presetName,
            DiagnosticIds = diagnosticIds,
            SeverityFilter = severityFilter ?? DiagnosticSeverity.Info,
        };
    }
}

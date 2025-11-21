

using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Helper для автоматического линтинга при модификации кода
/// </summary>
public static class LintingHelper
{
    /// <summary>
    /// Оборачивает операцию модификации кода в линтинг до/после
    /// </summary>
    /// <param name="modificationAction">Действие модификации, возвращающее список измененных файлов</param>
    /// <param name="quickLintService">Сервис быстрого линтинга</param>
    /// <param name="solutionPath">Путь к solution</param>
    /// <param name="logger">Logger</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Объект с результатами линтинга до/после</returns>
    public static async Task<LintingResult> WrapWithLintingAsync(
        Func<Task<IEnumerable<string>>> modificationAction,
        IQuickLintService quickLintService,
        string solutionPath,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 1. Получаем список файлов которые будут изменены (выполняем действие)
            logger.LogDebug("Executing modification action...");
            var changedFiles = await modificationAction();
            var changedFilesList = changedFiles.ToList();

            if (!changedFilesList.Any())
            {
                logger.LogDebug("No files changed, skipping lint");
                return new LintingResult
                {
                    Before = null,
                    After = null,
                    ChangedFiles = [],
                    HasLintResults = false
                };
            }

            logger.LogDebug("Files changed: {Count}, running post-modification lint...", changedFilesList.Count);

            // 2. Запускаем линтинг ПОСЛЕ изменения (в отдельной задаче)
            var lintAfter = await quickLintService.LintFilesAsync(
                solutionPath,
                changedFilesList,
                cancellationToken
            );

            logger.LogInformation(
                "Lint completed: After={Errors}E/{Warnings}W",
                lintAfter.ErrorCount,
                lintAfter.WarningCount
            );

            return new LintingResult
            {
                Before = null, // До линтинг не делаем - экономим время
                After = lintAfter,
                ChangedFiles = changedFilesList,
                HasLintResults = true
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to perform linting wrapper");
            return new LintingResult
            {
                Before = null,
                After = null,
                ChangedFiles = [],
                HasLintResults = false
            };
        }
    }

    /// <summary>
    /// Форматирует результаты линтинга для вывода пользователю
    /// </summary>
    public static string FormatLintingResults(LintingResult lintingResult)
    {
        if (!lintingResult.HasLintResults || lintingResult.After == null)
        {
            return string.Empty;
        }

        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        sb.AppendLine();
        sb.AppendLine("## 📊 Code Quality Check");
        sb.AppendLine();

        var after = lintingResult.After;

        // Показываем текущее состояние
        if (after.TotalIssues == 0)
        {
            sb.AppendLine("✅ **No errors or warnings in modified files!**");
        }
        else
        {
            sb.AppendLine($"**Modified files:** {lintingResult.ChangedFiles.Count}");
            sb.AppendLine();

            // Текущее состояние
            if (after.ErrorCount > 0)
            {
                sb.AppendLine($"❌ **{after.ErrorCount} error(s)**");
            }
            if (after.WarningCount > 0)
            {
                sb.AppendLine($"⚠️ **{after.WarningCount} warning(s)**");
            }

            // Топ проблемы
            if (after.TopIssues.Any())
            {
                sb.AppendLine();
                sb.AppendLine("**Top issues:**");
                foreach (var issue in after.TopIssues.Take(5))
                {
                    var emoji = issue.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ? "❌" : "⚠️";
                    var fileName = Path.GetFileName(issue.FilePath);
                    sb.AppendLine($"  {emoji} **{issue.Id}**: {issue.Message}");
                    sb.AppendLine($"     `{fileName}:{issue.Line}`");
                }

                if (after.TopIssues.Count > 5)
                {
                    sb.AppendLine($"     _... and {after.TopIssues.Count - 5} more_");
                }
            }

            sb.AppendLine();
            sb.AppendLine("💡 **Tip:** Use `UltrasharpTool_AnalyzeCodeStyle` for full analysis or `UltrasharpTool_ApplyCodeFixes` to auto-fix common issues.");
        }

        var result = sb.ToString();
        ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        return result;
    }
    /// <summary>
    /// Проверяет есть ли критические проблемы в результатах линтинга
    /// </summary>
    public static bool HasCriticalIssues(LintingResult result)
    {
        return result.HasLintResults && result.After?.ErrorCount > 0;
    }
    /// <summary>
    /// Checks if the linting result is clean (no errors or warnings).
    /// </summary>
    public static bool IsCleanResult(LintingResult result)
    {
        return !result.HasLintResults || result.After == null || result.After.TotalIssues == 0;
    }
    /// <summary>
    /// Gets a summary message for the linting result.
    /// </summary>
    public static string GetSummaryMessage(LintingResult result)
    {
        if (!result.HasLintResults || result.After == null)
            return "No linting performed";

        if (result.After.TotalIssues == 0)
            return "Code is clean - no issues found";

        return $"{result.After.ErrorCount} error(s), {result.After.WarningCount} warning(s)";
    }
}

/// <summary>
/// Результат линтинга до/после модификации
/// </summary>
public record LintingResult
{
    public QuickLintResult? Before { get; init; }
    public QuickLintResult? After { get; init; }
    public List<string> ChangedFiles { get; init; } = [];
    public bool HasLintResults { get; init; }
}

using System.Text;
using UltrasharpTools.Tools.Interfaces;
using UltrasharpTools.Tools.Models.SemanticEnrichment;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Сервис для генерации .editorconfig рекомендаций (Phase 2)
/// Преобразует результаты semantic enrichment в готовый .editorconfig файл
/// </summary>
public class EditorConfigGenerator(ILogger<EditorConfigGenerator> logger) : IEditorConfigGenerator
{
    private readonly ILogger<EditorConfigGenerator> _logger = logger;

    /// <summary>
    /// Генерирует .editorconfig рекомендации на основе semantic enrichment
    /// </summary>
    public Task<EditorConfigRecommendations> GenerateAsync(
        SemanticEnrichmentResult enrichmentResult,
        EditorConfigOptions options,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Starting EditorConfig generation for {ClusterCount} clusters",
            enrichmentResult.Clusters.Count
        );

        var recommendations = new EditorConfigRecommendations();

        // Разделяем clusters на auto-approved и needs-review
        var autoApprovedClusters = enrichmentResult
            .Clusters.Where(c => c.ConfidenceScore >= options.MinConfidenceForAutoApproval)
            .ToList();

        var needsReviewClusters = enrichmentResult
            .Clusters.Where(c => c.ConfidenceScore < options.MinConfidenceForAutoApproval)
            .ToList();

        _logger.LogInformation(
            "Clusters analysis: {AutoApproved} auto-approved, {NeedsReview} needs review",
            autoApprovedClusters.Count,
            needsReviewClusters.Count
        );

        // Генерируем правила для auto-approved clusters
        var rules = new List<EditorConfigRule>();
        foreach (var cluster in autoApprovedClusters)
        {
            var rule = CreateRule(cluster, options);
            rules.Add(rule);
        }

        // Генерируем manual review cases
        var manualReviewCases = new List<ManualReviewCase>();
        foreach (var cluster in needsReviewClusters)
        {
            var reviewCase = CreateManualReviewCase(cluster, options);
            manualReviewCases.Add(reviewCase);
        }

        // Группируем по категориям если включено
        if (options.GroupByCategory)
        {
            rules = rules.OrderBy(r => r.Category).ThenBy(r => r.DiagnosticId).ToList();
        }
        else
        {
            rules = rules.OrderBy(r => r.DiagnosticId).ToList();
        }

        // Генерируем контент .editorconfig
        var content = GenerateEditorConfigContent(rules, options);

        // Генерируем markdown summary
        var summary = GenerateSummary(rules, manualReviewCases, enrichmentResult, options);

        // Собираем статистику
        var stats = new EditorConfigStats
        {
            TotalRulesGenerated = rules.Count,
            AutoApprovedRules = rules.Count,
            NeedsReviewRules = manualReviewCases.Count,
            ByCategory = rules.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => g.Count()),
        };

        recommendations.Content = content;
        recommendations.Summary = summary;
        recommendations.Rules = rules;
        recommendations.RequiresManualReview = manualReviewCases;
        recommendations.Stats = stats;

        _logger.LogInformation(
            "EditorConfig generation complete. Rules: {RulesCount}, Manual review: {ReviewCount}",
            rules.Count,
            manualReviewCases.Count
        );

        return Task.FromResult(recommendations);
    }

    /// <summary>
    /// Создает правило для .editorconfig из cluster
    /// </summary>
    private EditorConfigRule CreateRule(DiagnosticCluster cluster, EditorConfigOptions options)
    {
        var severityString = cluster.RecommendedSeverity switch
        {
            EditorConfigSeverity.None => "none",
            EditorConfigSeverity.Silent => "silent",
            EditorConfigSeverity.Suggestion => "suggestion",
            EditorConfigSeverity.Warning => "warning",
            EditorConfigSeverity.Error => "error",
            _ => "warning",
        };

        var editorConfigLine = $"dotnet_diagnostic.{cluster.DiagnosticId}.severity = {severityString}";

        return new EditorConfigRule
        {
            DiagnosticId = cluster.DiagnosticId,
            Severity = cluster.RecommendedSeverity,
            Justification = cluster.Justification,
            Category = cluster.Category,
            EditorConfigLine = editorConfigLine,
            Statistics = new RuleStatistics
            {
                Occurrences = cluster.Occurrences,
                AffectedFiles = cluster.AffectedFiles.Count,
                AffectedProjects = cluster.AffectedProjects.Count,
            },
        };
    }

    /// <summary>
    /// Создает manual review case из cluster
    /// </summary>
    private ManualReviewCase CreateManualReviewCase(
        DiagnosticCluster cluster,
        EditorConfigOptions options
    )
    {
        var reason = cluster.ConfidenceScore < 0.5
            ? "Низкая уверенность в классификации"
            : cluster.Category == DiagnosticCategory.NeedsManualReview
                ? "Требуется ручной анализ для определения категории"
                : "Средняя уверенность в классификации";

        var suggestedActions = new List<string>();

        // Предлагаем действия в зависимости от категории
        if (cluster.Category == DiagnosticCategory.FalsePositive)
        {
            suggestedActions.Add(
                $"Проверьте примеры и если это ложные срабатывания, добавьте: dotnet_diagnostic.{cluster.DiagnosticId}.severity = none"
            );
        }
        else if (
            cluster.Category == DiagnosticCategory.Security
            || cluster.Category == DiagnosticCategory.Reliability
        )
        {
            suggestedActions.Add(
                "ВАЖНО: Проверьте все примеры - это может быть критичная проблема"
            );
            suggestedActions.Add("Рассмотрите повышение severity до error если проблема реальна");
        }
        else
        {
            suggestedActions.Add("Проанализируйте примеры срабатываний");
            suggestedActions.Add(
                $"Определите подходящий severity и добавьте в .editorconfig: dotnet_diagnostic.{cluster.DiagnosticId}.severity = <your-choice>"
            );
        }

        return new ManualReviewCase
        {
            DiagnosticId = cluster.DiagnosticId,
            Reason = reason,
            Occurrences = cluster.Occurrences,
            ConfidenceScore = cluster.ConfidenceScore,
            Examples = cluster.RepresentativeExamples.Take(3).ToList(),
            SuggestedActions = suggestedActions,
        };
    }

    /// <summary>
    /// Генерирует контент .editorconfig файла
    /// </summary>
    private string GenerateEditorConfigContent(
        List<EditorConfigRule> rules,
        EditorConfigOptions options
    )
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Auto-generated .editorconfig rules");
        sb.AppendLine(
            $"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by UltrasharpTools Semantic Enrichment"
        );
        sb.AppendLine(
            $"# Format: {options.Format}, Confidence threshold: {options.MinConfidenceForAutoApproval}"
        );
        sb.AppendLine();

        if (options.Format == EditorConfigFormat.Detailed)
        {
            sb.AppendLine("# ========================================");
            sb.AppendLine("# SUMMARY");
            sb.AppendLine("# ========================================");
            sb.AppendLine($"# Total rules: {rules.Count}");
            sb.AppendLine(
                $"# Categories: {string.Join(", ", rules.Select(r => r.Category).Distinct())}"
            );
            sb.AppendLine();
        }

        // Группируем по категориям если включено
        if (options.GroupByCategory)
        {
            var groupedRules = rules.GroupBy(r => r.Category);

            foreach (var group in groupedRules)
            {
                sb.AppendLine("# ========================================");
                sb.AppendLine($"# CATEGORY: {group.Key}");
                sb.AppendLine("# ========================================");
                sb.AppendLine();

                foreach (var rule in group)
                {
                    AppendRule(sb, rule, options);
                }

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("# ========================================");
            sb.AppendLine("# DIAGNOSTIC RULES");
            sb.AppendLine("# ========================================");
            sb.AppendLine();

            foreach (var rule in rules)
            {
                AppendRule(sb, rule, options);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Добавляет правило в StringBuilder
    /// </summary>
    private void AppendRule(StringBuilder sb, EditorConfigRule rule, EditorConfigOptions options)
    {
        if (options.Format == EditorConfigFormat.Detailed)
        {
            // Detailed format with comments
            sb.AppendLine($"# {rule.DiagnosticId}: {rule.Category}");

            if (options.IncludeStatistics)
            {
                sb.AppendLine(
                    $"#   Occurrences: {rule.Statistics.Occurrences}, Files: {rule.Statistics.AffectedFiles}, Projects: {rule.Statistics.AffectedProjects}"
                );
            }

            sb.AppendLine($"#   {rule.Justification}");
            sb.AppendLine(rule.EditorConfigLine);
            sb.AppendLine();
        }
        else
        {
            // Standard format without comments
            sb.AppendLine(rule.EditorConfigLine);
        }
    }

    /// <summary>
    /// Генерирует markdown summary
    /// </summary>
    private string GenerateSummary(
        List<EditorConfigRule> rules,
        List<ManualReviewCase> manualReviewCases,
        SemanticEnrichmentResult enrichmentResult,
        EditorConfigOptions options
    )
    {
        var sb = new StringBuilder();

        sb.AppendLine("# EditorConfig Generation Summary");
        sb.AppendLine();
        sb.AppendLine("## Statistics");
        sb.AppendLine();
        sb.AppendLine($"- **Total diagnostics analyzed**: {enrichmentResult.Summary.TotalDiagnostics}");
        sb.AppendLine($"- **Total clusters**: {enrichmentResult.Clusters.Count}");
        sb.AppendLine($"- **Auto-approved rules**: {rules.Count}");
        sb.AppendLine($"- **Needs manual review**: {manualReviewCases.Count}");
        sb.AppendLine();

        // Breakdown by category
        sb.AppendLine("## Rules by Category");
        sb.AppendLine();
        var byCategory = rules.GroupBy(r => r.Category);
        foreach (var group in byCategory.OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"- **{group.Key}**: {group.Count()} rules");
        }
        sb.AppendLine();

        // Breakdown by severity
        sb.AppendLine("## Rules by Severity");
        sb.AppendLine();
        var bySeverity = rules.GroupBy(r => r.Severity);
        foreach (var group in bySeverity.OrderBy(g => g.Key))
        {
            sb.AppendLine($"- **{group.Key}**: {group.Count()} rules");
        }
        sb.AppendLine();

        // Manual review cases
        if (manualReviewCases.Count > 0)
        {
            sb.AppendLine("## Requires Manual Review");
            sb.AppendLine();
            sb.AppendLine(
                $"The following {manualReviewCases.Count} diagnostic(s) require manual review:"
            );
            sb.AppendLine();

            foreach (var reviewCase in manualReviewCases.OrderByDescending(c => c.Occurrences))
            {
                sb.AppendLine(
                    $"### {reviewCase.DiagnosticId} ({reviewCase.Occurrences} occurrences)"
                );
                sb.AppendLine();
                sb.AppendLine($"**Reason**: {reviewCase.Reason}");
                sb.AppendLine($"**Confidence**: {reviewCase.ConfidenceScore:F2}");
                sb.AppendLine();
                sb.AppendLine("**Suggested actions**:");
                foreach (var action in reviewCase.SuggestedActions)
                {
                    sb.AppendLine($"- {action}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

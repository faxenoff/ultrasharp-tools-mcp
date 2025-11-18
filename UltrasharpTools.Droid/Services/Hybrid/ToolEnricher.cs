using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Реализация обогащения результатов инструментов семантическими данными
/// </summary>
public sealed class ToolEnricher : IToolEnricher
{
    private readonly ILogger<ToolEnricher> _logger;
    private readonly ISemanticModeProvider _semanticProvider;
    private readonly List<IEnrichmentStrategy> _strategies;
    private readonly TimeSpan _enrichmentTimeout;

    public ToolEnricher(
        ILogger<ToolEnricher> logger,
        ISemanticModeProvider semanticProvider,
        TimeSpan? enrichmentTimeout = null)
    {
        _logger = logger;
        _semanticProvider = semanticProvider;
        _enrichmentTimeout = enrichmentTimeout ?? TimeSpan.FromSeconds(5);

        // Регистрируем enrichment strategies
        _strategies = new List<IEnrichmentStrategy>
        {
            new ViewDefinitionEnrichmentStrategy(),
            new FindReferencesEnrichmentStrategy(),
            new ModifyCodeEnrichmentStrategy(),
            new GetMembersEnrichmentStrategy(),
            new AnalyzeComplexityEnrichmentStrategy()
        };

        _logger.LogInformation(
            "ToolEnricher initialized with {StrategyCount} strategies, timeout: {Timeout}ms",
            _strategies.Count,
            _enrichmentTimeout.TotalMilliseconds);
    }

    /// <inheritdoc/>
    public async Task<EnrichedToolResult> EnrichAsync(
        string toolName,
        object originalResult,
        Dictionary<string, object>? toolArguments = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Проверяем доступность семантического режима
        var availability = await _semanticProvider.CheckAvailabilityAsync(ct);
        if (!availability.IsAvailable)
        {
            _logger.LogTrace("Semantic mode not available - returning original result for {Tool}", toolName);
            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    EnrichmentTimeMs = sw.ElapsedMilliseconds,
                    Source = SemanticModeSource.None,
                    ErrorMessage = "Semantic mode not available"
                }
            };
        }

        // Находим подходящую strategy
        var strategy = _strategies.FirstOrDefault(s => s.SupportedTools.Contains(toolName));
        if (strategy == null)
        {
            _logger.LogTrace("No enrichment strategy found for {Tool}", toolName);
            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    EnrichmentTimeMs = sw.ElapsedMilliseconds,
                    Source = availability.Source,
                    StrategyName = "none"
                }
            };
        }

        _logger.LogDebug("Enriching {Tool} with {Strategy}", toolName, strategy.Name);

        try
        {
            // Выполняем enrichment с timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_enrichmentTimeout);

            var enrichment = await strategy.EnrichAsync(
                originalResult,
                toolArguments,
                _semanticProvider,
                cts.Token);

            sw.Stop();

            var matchCount = 0;
            if (enrichment != null)
            {
                matchCount += enrichment.SimilarDefinitions?.Count ?? 0;
                matchCount += enrichment.SimilarUsages?.Count ?? 0;
                matchCount += enrichment.SimilarChanges?.Count ?? 0;
                matchCount += enrichment.SimilarStructures?.Count ?? 0;
            }

            _logger.LogInformation(
                "Enriched {Tool} with {Matches} semantic matches in {Time}ms",
                toolName,
                matchCount,
                sw.ElapsedMilliseconds);

            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = enrichment,
                Metadata = new EnrichmentMetadata
                {
                    EnrichmentTimeMs = sw.ElapsedMilliseconds,
                    SemanticMatchCount = matchCount,
                    Source = availability.Source,
                    StrategyName = strategy.Name,
                    TimedOut = false
                }
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning(
                "Enrichment for {Tool} timed out after {Timeout}ms",
                toolName,
                _enrichmentTimeout.TotalMilliseconds);

            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    EnrichmentTimeMs = sw.ElapsedMilliseconds,
                    Source = availability.Source,
                    StrategyName = strategy.Name,
                    TimedOut = true,
                    ErrorMessage = "Enrichment timeout"
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Enrichment failed for {Tool}", toolName);

            return new EnrichedToolResult
            {
                OriginalResult = originalResult,
                Semantic = null,
                Metadata = new EnrichmentMetadata
                {
                    EnrichmentTimeMs = sw.ElapsedMilliseconds,
                    Source = availability.Source,
                    StrategyName = strategy.Name,
                    ErrorMessage = ex.Message
                }
            };
        }
    }

    /// <inheritdoc/>
    public bool SupportsEnrichment(string toolName)
    {
        return _strategies.Any(s => s.SupportedTools.Contains(toolName));
    }
}

// === Enrichment Strategies ===

/// <summary>
/// Enrichment strategy для view_definition
/// </summary>
internal sealed class ViewDefinitionEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "ViewDefinition";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "view_definition"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        // Извлекаем FQN из аргументов
        if (arguments == null || !arguments.TryGetValue("fqn", out var fqnObj))
        {
            return null;
        }

        var fqn = fqnObj?.ToString();
        if (string.IsNullOrEmpty(fqn))
        {
            return null;
        }

        // Получаем код из результата
        var code = originalResult?.ToString();
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        // Формируем запрос для поиска похожих определений
        var query = $"Find similar class/method definitions to: {fqn}";
        var similarDefinitions = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.75, ct);

        // Формируем рекомендации
        var recommendations = new List<string>();
        if (similarDefinitions.Any())
        {
            recommendations.Add($"Found {similarDefinitions.Count()} similar definitions in other projects");
            recommendations.Add("Consider reviewing these for consistency and best practices");
        }

        return new SemanticEnrichment
        {
            SimilarDefinitions = similarDefinitions.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для find_references
/// </summary>
internal sealed class FindReferencesEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "FindReferences";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "find_references"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("fqn", out var fqnObj))
        {
            return null;
        }

        var fqn = fqnObj?.ToString();
        if (string.IsNullOrEmpty(fqn))
        {
            return null;
        }

        // Ищем похожие usage patterns
        var query = $"Find similar usage patterns for: {fqn}";
        var similarUsages = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.7, ct);

        return new SemanticEnrichment
        {
            SimilarUsages = similarUsages.ToList(),
            Recommendations = similarUsages.Any()
                ? new List<string> { "Review similar usage patterns from other projects" }
                : null
        };
    }
}

/// <summary>
/// Enrichment strategy для modify_code
/// </summary>
internal sealed class ModifyCodeEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "ModifyCode";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "overwrite_member",
        "add_member",
        "rename_symbol"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("fqn", out var fqnObj))
        {
            return null;
        }

        var fqn = fqnObj?.ToString();
        if (string.IsNullOrEmpty(fqn))
        {
            return null;
        }

        // Ищем похожие изменения из истории
        var query = $"Find similar code modifications for: {fqn}";
        var similarChanges = await semanticProvider.SearchByTextAsync(query, topK: 3, threshold: 0.8, ct);

        var recommendations = new List<string>
        {
            "Auto-committed to sharptools/* branch",
            "Review with: git diff HEAD~1"
        };

        if (similarChanges.Any())
        {
            recommendations.Add("Similar changes found in commit history");
        }

        return new SemanticEnrichment
        {
            SimilarChanges = similarChanges.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для get_members
/// </summary>
internal sealed class GetMembersEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "GetMembers";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "get_members"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("fqn", out var fqnObj))
        {
            return null;
        }

        var fqn = fqnObj?.ToString();
        if (string.IsNullOrEmpty(fqn))
        {
            return null;
        }

        // Ищем классы с похожей структурой
        var query = $"Find classes with similar structure to: {fqn}";
        var similarStructures = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.75, ct);

        return new SemanticEnrichment
        {
            SimilarStructures = similarStructures.ToList(),
            Recommendations = similarStructures.Any()
                ? new List<string> { "Found classes with similar member structure in other projects" }
                : null
        };
    }
}

/// <summary>
/// Enrichment strategy для analyze_complexity
/// </summary>
internal sealed class AnalyzeComplexityEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "AnalyzeComplexity";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "analyze_complexity"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("fqn", out var fqnObj))
        {
            return null;
        }

        var fqn = fqnObj?.ToString();
        if (string.IsNullOrEmpty(fqn))
        {
            return null;
        }

        // Ищем методы с похожей сложностью для сравнения
        var query = $"Find methods with similar complexity level to: {fqn}";
        var similarComplexity = await semanticProvider.SearchByTextAsync(query, topK: 3, threshold: 0.7, ct);

        var recommendations = new List<string>();

        // Анализируем результат complexity analysis
        var resultStr = originalResult?.ToString() ?? "";
        if (resultStr.Contains("High") || resultStr.Contains("Very High"))
        {
            recommendations.Add("Consider refactoring: high complexity detected");
            recommendations.Add("Review similar methods for refactoring patterns");
        }

        return new SemanticEnrichment
        {
            SimilarDefinitions = similarComplexity.ToList(),
            Recommendations = recommendations
        };
    }
}

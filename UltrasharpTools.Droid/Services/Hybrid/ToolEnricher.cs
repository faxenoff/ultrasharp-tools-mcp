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
            // Phase 12.1 - Core strategies
            new ViewDefinitionEnrichmentStrategy(),
            new FindReferencesEnrichmentStrategy(),
            new ModifyCodeEnrichmentStrategy(),
            new GetMembersEnrichmentStrategy(),
            new AnalyzeComplexityEnrichmentStrategy(),

            // Phase 12.2 - Extended strategies
            new FindAllReferencesEnrichmentStrategy(),
            new ListTypesEnrichmentStrategy(),
            new SearchSymbolsEnrichmentStrategy(),
            new TraceExecutionEnrichmentStrategy(),
            new AnalyzeCodeStyleEnrichmentStrategy(),
            new GetTypeHierarchyEnrichmentStrategy(),
            new GetProjectStructureEnrichmentStrategy(),
            new FindUsagesEnrichmentStrategy(),
            new GetDiagnosticsEnrichmentStrategy(),
            new ApplyCodeFixesEnrichmentStrategy()
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

/// <summary>
/// Enrichment strategy для find_all_references
/// </summary>
internal sealed class FindAllReferencesEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "FindAllReferences";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "find_all_references"
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

        // Ищем похожие usage patterns в других проектах
        var query = $"Find similar usage patterns and references for: {fqn}";
        var similarUsages = await semanticProvider.SearchByTextAsync(query, topK: 10, threshold: 0.65, ct);

        var recommendations = new List<string>();
        if (similarUsages.Any())
        {
            recommendations.Add($"Found {similarUsages.Count()} similar usage patterns across projects");
            recommendations.Add("Review cross-project usage for consistency");
        }

        return new SemanticEnrichment
        {
            SimilarUsages = similarUsages.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для list_types
/// </summary>
internal sealed class ListTypesEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "ListTypes";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "list_types"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        // Получаем namespace filter если есть
        var namespaceFilter = arguments?.TryGetValue("namespaceFilter", out var nsObj) == true
            ? nsObj?.ToString()
            : null;

        if (string.IsNullOrEmpty(namespaceFilter))
        {
            return null; // Без фильтра слишком широкий запрос
        }

        // Ищем похожие типы в других проектах
        var query = $"Find similar types in namespace: {namespaceFilter}";
        var similarTypes = await semanticProvider.SearchByTextAsync(query, topK: 8, threshold: 0.7, ct);

        var recommendations = new List<string>();
        if (similarTypes.Any())
        {
            recommendations.Add($"Found {similarTypes.Count()} similar type definitions in other projects");
            recommendations.Add("Consider reviewing for reusable patterns");
        }

        return new SemanticEnrichment
        {
            SimilarDefinitions = similarTypes.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для search_symbols
/// </summary>
internal sealed class SearchSymbolsEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "SearchSymbols";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "search_symbols"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("query", out var queryObj))
        {
            return null;
        }

        var searchQuery = queryObj?.ToString();
        if (string.IsNullOrEmpty(searchQuery))
        {
            return null;
        }

        // Semantic search в дополнение к fuzzy matching
        var semanticQuery = $"Find symbols semantically similar to: {searchQuery}";
        var semanticMatches = await semanticProvider.SearchByTextAsync(semanticQuery, topK: 10, threshold: 0.6, ct);

        var recommendations = new List<string>();
        if (semanticMatches.Any())
        {
            recommendations.Add("Semantic search found additional relevant symbols");
            recommendations.Add("Review cross-project matches for reusable implementations");
        }

        return new SemanticEnrichment
        {
            SimilarDefinitions = semanticMatches.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для trace_execution
/// </summary>
internal sealed class TraceExecutionEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "TraceExecution";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "trace_execution"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        if (arguments == null || !arguments.TryGetValue("entryPoint", out var entryObj))
        {
            return null;
        }

        var entryPoint = entryObj?.ToString();
        if (string.IsNullOrEmpty(entryPoint))
        {
            return null;
        }

        // Ищем похожие execution paths
        var query = $"Find similar execution flows and call patterns for: {entryPoint}";
        var similarFlows = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.75, ct);

        var recommendations = new List<string>();
        if (similarFlows.Any())
        {
            recommendations.Add("Found similar execution patterns in other projects");
            recommendations.Add("Compare call graphs for architectural insights");
        }

        return new SemanticEnrichment
        {
            SimilarUsages = similarFlows.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для analyze_code_style
/// </summary>
internal sealed class AnalyzeCodeStyleEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "AnalyzeCodeStyle";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "analyze_code_style"
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

        // Ищем best practices examples
        var query = $"Find well-styled code examples similar to: {fqn}";
        var bestPractices = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.7, ct);

        var recommendations = new List<string>();

        // Анализируем результат code style analysis
        var resultStr = originalResult?.ToString() ?? "";
        if (resultStr.Contains("Warning") || resultStr.Contains("Issue"))
        {
            recommendations.Add("Code style issues detected");
            if (bestPractices.Any())
            {
                recommendations.Add("Review best practice examples from other projects");
            }
        }

        return new SemanticEnrichment
        {
            SimilarDefinitions = bestPractices.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для get_type_hierarchy
/// </summary>
internal sealed class GetTypeHierarchyEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "GetTypeHierarchy";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "get_type_hierarchy"
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

        // Ищем похожие type hierarchies в других проектах
        var query = $"Find similar type hierarchies and inheritance patterns for: {fqn}";
        var similarHierarchies = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.75, ct);

        var recommendations = new List<string>();
        if (similarHierarchies.Any())
        {
            recommendations.Add("Found similar inheritance patterns in other projects");
            recommendations.Add("Review for common architectural approaches");
        }

        return new SemanticEnrichment
        {
            SimilarStructures = similarHierarchies.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для get_project_structure
/// </summary>
internal sealed class GetProjectStructureEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "GetProjectStructure";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "get_project_structure"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        // Ищем проекты с похожей структурой
        var query = "Find projects with similar folder structure and organization";
        var similarProjects = await semanticProvider.SearchByTextAsync(query, topK: 3, threshold: 0.65, ct);

        var recommendations = new List<string>();
        if (similarProjects.Any())
        {
            recommendations.Add("Found projects with similar organizational patterns");
            recommendations.Add("Compare architectural decisions across team projects");
        }

        return new SemanticEnrichment
        {
            SimilarStructures = similarProjects.ToList(),
            CrossProjectMatches = similarProjects.Any()
                ? new Dictionary<string, List<SemanticMatch>>
                {
                    ["similar_structures"] = similarProjects.ToList()
                }
                : null,
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для find_usages
/// </summary>
internal sealed class FindUsagesEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "FindUsages";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "find_usages"
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

        // Ищем похожие usage examples
        var query = $"Find similar usage examples and patterns for: {fqn}";
        var similarUsages = await semanticProvider.SearchByTextAsync(query, topK: 8, threshold: 0.7, ct);

        var recommendations = new List<string>();
        if (similarUsages.Any())
        {
            recommendations.Add($"Found {similarUsages.Count()} similar usage patterns");
            recommendations.Add("Review for common anti-patterns or best practices");
        }

        return new SemanticEnrichment
        {
            SimilarUsages = similarUsages.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для get_diagnostics
/// </summary>
internal sealed class GetDiagnosticsEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "GetDiagnostics";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "get_diagnostics"
    };

    public async Task<SemanticEnrichment?> EnrichAsync(
        object originalResult,
        Dictionary<string, object>? arguments,
        ISemanticModeProvider semanticProvider,
        CancellationToken ct)
    {
        // Анализируем diagnostics из результата
        var resultStr = originalResult?.ToString() ?? "";

        // Ищем похожие ошибки и их решения
        var query = "Find similar diagnostic issues and their resolutions";
        var similarIssues = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.7, ct);

        var recommendations = new List<string>();

        if (resultStr.Contains("Error") || resultStr.Contains("Warning"))
        {
            recommendations.Add("Diagnostics contain errors or warnings");
            if (similarIssues.Any())
            {
                recommendations.Add("Review similar issues and resolutions from other projects");
            }
        }

        return new SemanticEnrichment
        {
            SimilarChanges = similarIssues.ToList(),
            Recommendations = recommendations
        };
    }
}

/// <summary>
/// Enrichment strategy для apply_code_fixes
/// </summary>
internal sealed class ApplyCodeFixesEnrichmentStrategy : IEnrichmentStrategy
{
    public string Name => "ApplyCodeFixes";

    public IReadOnlySet<string> SupportedTools { get; } = new HashSet<string>
    {
        "apply_code_fixes"
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

        // Ищем похожие code fixes из истории
        var query = $"Find similar code fixes and diagnostic resolutions for: {fqn}";
        var similarFixes = await semanticProvider.SearchByTextAsync(query, topK: 5, threshold: 0.75, ct);

        var recommendations = new List<string>
        {
            "Code fixes applied automatically",
            "Review changes with git diff"
        };

        if (similarFixes.Any())
        {
            recommendations.Add("Similar fixes found in project history");
        }

        return new SemanticEnrichment
        {
            SimilarChanges = similarFixes.ToList(),
            Recommendations = recommendations
        };
    }
}

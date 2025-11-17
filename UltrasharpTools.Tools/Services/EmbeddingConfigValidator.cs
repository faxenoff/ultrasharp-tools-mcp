using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Config;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Validates embedding configuration and provides detailed error messages
/// </summary>
public class EmbeddingConfigValidator
{
    private readonly ILogger<EmbeddingConfigValidator> _logger;
    private readonly EmbeddingServiceHealthChecker _healthChecker;

    public EmbeddingConfigValidator(
        ILogger<EmbeddingConfigValidator> logger,
        EmbeddingServiceHealthChecker healthChecker)
    {
        _logger = logger;
        _healthChecker = healthChecker;
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();

        public bool HasCriticalIssues => Issues.Any(i => i.Severity == IssueSeverity.Critical);
        public bool HasWarnings => Issues.Any(i => i.Severity == IssueSeverity.Warning) || Warnings.Any();
    }

    public class ValidationIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Details { get; set; }
        public string? Solution { get; set; }
    }

    public enum IssueSeverity
    {
        Critical,   // Cannot work
        Warning,    // Can work but not optimal
        Info        // Just information
    }

    /// <summary>
    /// Validate global configuration
    /// </summary>
    public async Task<ValidationResult> ValidateGlobalConfigAsync(
        SemanticEmbeddingConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        _logger.LogInformation("Validating global embedding configuration...");

        // Validate platform
        ValidatePlatform(config, result);

        // Validate architecture
        ValidateArchitecture(config, result);

        // Platform-specific validation
        switch (config.Embedding.Platform.ToLowerInvariant())
        {
            case "tei":
                await ValidateTeiConfigAsync(config, result, cancellationToken);
                break;

            case "ollama":
                await ValidateOllamaConfigAsync(config, result, cancellationToken);
                break;

            case "memory":
                ValidateMemoryConfig(config, result);
                break;

            default:
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = "Platform",
                    Message = $"Unknown platform: '{config.Embedding.Platform}'",
                    Details = "Valid platforms: tei, ollama, memory",
                    Solution = @"Update semantic-config.json with valid platform or run: .\setup-semantic-embedding.ps1"
                });
                break;
        }

        // Summary
        if (result.HasCriticalIssues)
        {
            result.IsValid = false;
            _logger.LogError("✗ Configuration validation FAILED with {Count} critical issue(s)",
                result.Issues.Count(i => i.Severity == IssueSeverity.Critical));
        }
        else if (result.HasWarnings)
        {
            _logger.LogWarning("⚠ Configuration is valid but has {Count} warning(s)",
                result.Issues.Count(i => i.Severity == IssueSeverity.Warning));
        }
        else
        {
            _logger.LogInformation("✓ Configuration is valid");
        }

        return result;
    }

    private void ValidatePlatform(SemanticEmbeddingConfig config, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(config.Embedding.Platform))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = "Platform",
                Message = "Platform is not configured",
                Solution = @"Run: .\setup-semantic-embedding.ps1"
            });
        }
    }

    private void ValidateArchitecture(SemanticEmbeddingConfig config, ValidationResult result)
    {
        var validArchitectures = new[] { "auto", "cpu", "turing", "ampere-80", "ampere-86", "ada", "hopper", "blackwell" };

        if (!validArchitectures.Contains(config.Embedding.Architecture.ToLowerInvariant()))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Category = "Architecture",
                Message = $"Unknown architecture: '{config.Embedding.Architecture}'",
                Details = $"Valid options: {string.Join(", ", validArchitectures)}",
                Solution = @"Run: .\detect-gpu-architecture.ps1 to auto-detect, or set to 'cpu'"
            });
        }

        if (config.Embedding.Architecture.ToLowerInvariant() == "blackwell")
        {
            result.Warnings.Add("Blackwell GPU support is experimental - TEI may not work properly");
            result.Recommendations.Add("Consider using 'cpu' or 'ada' architecture, or use Ollama platform");
        }
    }

    private async Task ValidateTeiConfigAsync(
        SemanticEmbeddingConfig config,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        var teiConfig = config.Embedding.Tei;

        // Check endpoint
        if (string.IsNullOrWhiteSpace(teiConfig.Endpoint))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = "TEI",
                Message = "TEI endpoint is not configured",
                Solution = "Set endpoint in semantic-config.json or ENV: TEI_ENDPOINT=http://localhost:8080"
            });
            return;
        }

        // Health check
        var health = await _healthChecker.CheckTeiHealthAsync(teiConfig.Endpoint, cancellationToken);

        if (!health.IsHealthy)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = "TEI",
                Message = health.ErrorMessage ?? "TEI server is not available",
                Details = health.Details,
                Solution = $"Start TEI server:\n  .\\Dev.Scripts\\setup-tei.ps1\n\nOr switch to Ollama:\n  Update platform to 'ollama' in semantic-config.json"
            });
        }
        else
        {
            _logger.LogInformation("✓ {Details}", health.Details);
        }

        // Check models
        if (!teiConfig.Models.Any())
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Category = "TEI",
                Message = "No models configured for TEI",
                Solution = "Add models to semantic-config.json"
            });
        }

        if (string.IsNullOrWhiteSpace(teiConfig.SelectedModel))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Warning,
                Category = "TEI",
                Message = "No model selected for TEI",
                Solution = "Set selected_model in semantic-config.json"
            });
        }
    }

    private async Task ValidateOllamaConfigAsync(
        SemanticEmbeddingConfig config,
        ValidationResult result,
        CancellationToken cancellationToken)
    {
        var ollamaConfig = config.Embedding.Ollama;

        // Check endpoint
        if (string.IsNullOrWhiteSpace(ollamaConfig.Endpoint))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = "Ollama",
                Message = "Ollama endpoint is not configured",
                Solution = "Set endpoint in semantic-config.json or ENV: OLLAMA_ENDPOINT=http://localhost:11434"
            });
            return;
        }

        // Health check
        var health = await _healthChecker.CheckOllamaHealthAsync(ollamaConfig.Endpoint, cancellationToken);

        if (!health.IsHealthy)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = "Ollama",
                Message = health.ErrorMessage ?? "Ollama server is not available",
                Details = health.Details,
                Solution = "Install and start Ollama:\n  1. Download from https://ollama.ai\n  2. Install\n  3. Pull model: ollama pull granite-embedding\n\nOr switch to TEI:\n  Update platform to 'tei' in semantic-config.json"
            });
        }
        else
        {
            _logger.LogInformation("✓ {Details}", health.Details);

            // Check if selected model exists
            if (!string.IsNullOrWhiteSpace(ollamaConfig.SelectedModel))
            {
                var hasModel = await _healthChecker.CheckOllamaModelAsync(
                    ollamaConfig.Endpoint,
                    ollamaConfig.SelectedModel,
                    cancellationToken);

                if (!hasModel)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Ollama",
                        Message = $"Selected model '{ollamaConfig.SelectedModel}' is not available",
                        Solution = $"Pull the model: ollama pull {ollamaConfig.SelectedModel}"
                    });
                }
            }
        }
    }

    private void ValidateMemoryConfig(SemanticEmbeddingConfig config, ValidationResult result)
    {
        result.Warnings.Add("Memory platform is experimental and not recommended for production");
        result.Recommendations.Add("Consider using TEI or Ollama for better performance");
    }

    /// <summary>
    /// Print validation results to console
    /// </summary>
    public void PrintValidationResults(ValidationResult result)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("Configuration Validation Results");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        if (result.IsValid && !result.HasWarnings)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Configuration is valid and ready to use!");
            Console.ResetColor();
            Console.WriteLine();
            return;
        }

        // Critical issues
        var criticalIssues = result.Issues.Where(i => i.Severity == IssueSeverity.Critical).ToList();
        if (criticalIssues.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ CRITICAL ISSUES ({criticalIssues.Count}):");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var issue in criticalIssues)
            {
                PrintIssue(issue);
            }
        }

        // Warnings
        var warnings = result.Issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
        if (warnings.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ WARNINGS ({warnings.Count}):");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var warning in warnings)
            {
                PrintIssue(warning);
            }
        }

        // Recommendations
        if (result.Recommendations.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("💡 RECOMMENDATIONS:");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var rec in result.Recommendations)
            {
                Console.WriteLine($"  • {rec}");
            }
            Console.WriteLine();
        }
    }

    private void PrintIssue(ValidationIssue issue)
    {
        Console.ForegroundColor = issue.Severity == IssueSeverity.Critical ? ConsoleColor.Red : ConsoleColor.Yellow;
        Console.WriteLine($"[{issue.Category}] {issue.Message}");
        Console.ResetColor();

        if (!string.IsNullOrWhiteSpace(issue.Details))
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  Details: {issue.Details}");
            Console.ResetColor();
        }

        if (!string.IsNullOrWhiteSpace(issue.Solution))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Solution:");
            foreach (var line in issue.Solution.Split('\n'))
            {
                Console.WriteLine($"    {line}");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}

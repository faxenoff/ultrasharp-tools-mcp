using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Extensions;

namespace UltrasharpTools.Test.Common;

/// <summary>
/// Helper for setting up test service providers
/// </summary>
public static class TestServiceProvider
{
    /// <summary>
    /// Create service provider for layered index tests
    /// </summary>
    public static ServiceProvider CreateForLayeredIndexTest(
        TestConfiguration config,
        string preset = "Development",
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(ParseLogLevel(config.LogLevel));
        });

        // Register UltrasharpTools services
        services.WithUltrasharpToolsServices(
            enableGit: config.EnableGit,
            buildConfiguration: config.BuildConfiguration,
            gitOptions: null,
            reloadOptions: null,
            symbolCacheOptions: null
        );

        // Configure layered indexing
        var layeredConfig = preset == "Production"
            ? config.LayeredIndexing.Production
            : config.LayeredIndexing.Development;

        services.WithLayeredIndexing(
            maxBranchDeltas: layeredConfig.MaxBranchDeltas,
            enablePersistence: layeredConfig.EnablePersistence,
            deltaCompactionThreshold: layeredConfig.DeltaCompactionThreshold
        );

        // Allow custom service configuration
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create service provider for semantic merge tests
    /// </summary>
    public static ServiceProvider CreateForSemanticMergeTest(
        TestConfiguration config,
        string provider = "memory",
        int? dimension = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        // Configure logging (simpler for merge tests)
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(ParseLogLevel(config.LogLevel));
        });

        // Register UltrasharpTools services
        services.WithUltrasharpToolsServices(
            enableGit: config.EnableGit,
            buildConfiguration: config.BuildConfiguration,
            gitOptions: null,
            reloadOptions: null,
            symbolCacheOptions: null
        );

        // Configure Semantic RAG
        var ragDimension = dimension ?? config.SemanticRag.Dimension;
        services.WithSemanticRag(
            databasePath: config.SemanticRag.DatabasePath,
            dimension: ragDimension,
            configureEmbedding: opts =>
            {
                if (provider.ToLower() == "ollama")
                {
                    // Configure Ollama provider if needed
                    // This would require additional configuration
                }
                // Default is MemoryEmbeddingProvider
            }
        );

        // Register Semantic Merge services
        services.WithSemanticMerge();

        // Allow custom service configuration
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create service provider for semantic analysis tools tests (semantic_search, semantic_diff, detect_code_clones)
    /// </summary>
    public static ServiceProvider CreateForSemanticAnalysisTest(
        TestConfiguration config,
        string provider = "memory",
        int? dimension = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(ParseLogLevel(config.LogLevel));
        });

        // Register UltrasharpTools services
        services.WithUltrasharpToolsServices(
            enableGit: config.EnableGit,
            buildConfiguration: config.BuildConfiguration,
            gitOptions: null,
            reloadOptions: null,
            symbolCacheOptions: null
        );

        // Configure Semantic RAG (required for SemanticSearchService)
        var ragDimension = dimension ?? config.SemanticRag.Dimension;
        services.WithSemanticRag(
            databasePath: config.SemanticRag.DatabasePath,
            dimension: ragDimension,
            configureEmbedding: opts =>
            {
                if (provider.ToLower() == "ollama")
                {
                    opts.Provider = "ollama";
                    opts.Ollama.Model = "granite-embedding:latest";
                    opts.Ollama.BaseUrl = "http://localhost:11434";
                    opts.Ollama.Concurrency = 8;
                }
                else
                {
                    opts.Provider = "memory";
                }
            }
        );

        // Allow custom service configuration
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static LogLevel ParseLogLevel(string logLevel)
    {
        return logLevel.ToLower() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}

using Microsoft.Extensions.Configuration;

namespace UltrasharpTools.Test.Common;

/// <summary>
/// Configuration for test projects
/// </summary>
public class TestConfiguration
{
    public Dictionary<string, string> TestSolutions { get; set; } = new();
    public string LogLevel { get; set; } = "Information";
    public string BuildConfiguration { get; set; } = "Debug";
    public bool EnableGit { get; set; } = true;
    public LayeredIndexingConfig LayeredIndexing { get; set; } = new();
    public SemanticRagConfig SemanticRag { get; set; } = new();

    public class LayeredIndexingConfig
    {
        public LayeredIndexingPreset Development { get; set; } = new();
        public LayeredIndexingPreset Production { get; set; } = new();
    }

    public class LayeredIndexingPreset
    {
        public int MaxBranchDeltas { get; set; }
        public bool EnablePersistence { get; set; }
        public int DeltaCompactionThreshold { get; set; }
    }

    public class SemanticRagConfig
    {
        public string DatabasePath { get; set; } = "./data/vectors.db";
        public int Dimension { get; set; } = 384;
        public string Provider { get; set; } = "memory";
    }

    /// <summary>
    /// Load configuration from test-config.json
    /// </summary>
    public static TestConfiguration Load(string? configPath = null)
    {
        configPath ??= Path.Combine(AppContext.BaseDirectory, "test-config.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        var config = new TestConfiguration();
        configuration.Bind(config);
        return config;
    }

    /// <summary>
    /// Get test solution path by name
    /// </summary>
    public string GetSolutionPath(string name)
    {
        if (!TestSolutions.TryGetValue(name, out var path))
        {
            throw new ArgumentException(
                $"Solution '{name}' not found in test-config.json. Available: {string.Join(", ", TestSolutions.Keys)}"
            );
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Solution path '{path}' does not exist");
        }

        return path;
    }
}

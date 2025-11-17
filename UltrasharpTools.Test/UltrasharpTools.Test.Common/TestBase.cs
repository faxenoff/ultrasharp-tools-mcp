using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Test.Common;

/// <summary>
/// Base class for integration tests
/// </summary>
public abstract class TestBase : IAsyncDisposable
{
    protected TestConfiguration Config { get; }
    protected ServiceProvider ServiceProvider { get; }
    protected ILogger Logger { get; }

    protected TestBase(string? configPath = null)
    {
        Config = TestConfiguration.Load(configPath);
        ServiceProvider = CreateServiceProvider();
        Logger = ServiceProvider.GetRequiredService<ILogger<TestBase>>();
    }

    /// <summary>
    /// Override to create custom service provider
    /// </summary>
    protected abstract ServiceProvider CreateServiceProvider();

    /// <summary>
    /// Get solution path by name from config
    /// </summary>
    protected string GetSolutionPath(string name) => Config.GetSolutionPath(name);

    public virtual async ValueTask DisposeAsync()
    {
        if (ServiceProvider != null)
        {
            await ServiceProvider.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Base class for layered index tests
/// </summary>
public abstract class LayeredIndexTestBase : TestBase
{
    protected string Preset { get; }

    protected LayeredIndexTestBase(string preset = "Development", string? configPath = null)
        : base(configPath)
    {
        Preset = preset;
    }

    protected override ServiceProvider CreateServiceProvider()
    {
        return TestServiceProvider.CreateForLayeredIndexTest(Config, Preset);
    }
}

/// <summary>
/// Base class for semantic merge tests
/// </summary>
public abstract class SemanticMergeTestBase : TestBase
{
    protected string Provider { get; }
    protected int Dimension { get; }

    protected SemanticMergeTestBase(string provider = "memory", int dimension = 384, string? configPath = null)
        : base(configPath)
    {
        Provider = provider;
        Dimension = dimension;
    }

    protected override ServiceProvider CreateServiceProvider()
    {
        return TestServiceProvider.CreateForSemanticMergeTest(Config, Provider, Dimension);
    }
}

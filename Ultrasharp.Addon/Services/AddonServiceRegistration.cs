using UltrasharpTools.Tools.Extensions;
using UltrasharpTools.Tools.Models;

namespace Ultrasharp.Addon.Services;

/// <summary>
/// Registers a subset of Roslyn services for Addon mode.
/// Excludes: MCP, SQLite/VectorDB, Git, Semantic RAG, SemanticMerge, Layered Indexing.
/// </summary>
public static class AddonServiceRegistration
{
    public static void RegisterServices(IServiceCollection services)
    {
        // Use the standard registration from Tools with minimal options:
        // - No Git (TS side handles that)
        // - No VectorStore/Semantic RAG (TS owns embeddings)
        // - No Layered Indexing
        services.WithUltrasharpToolsServices(
            enableGit: false,
            buildConfiguration: null,
            gitOptions: null,
            reloadOptions: new SolutionReloadOptions
            {
                AutoReloadEnabled = false,
            },
            symbolCacheOptions: new SymbolCacheOptions
            {
                Enabled = true, // Use symbol cache for faster restarts
            },
            lowMemoryMode: true  // Addon: use SQLite for reflection type cache instead of FrozenDictionary (~200-500 MB savings)
        );
    }
}

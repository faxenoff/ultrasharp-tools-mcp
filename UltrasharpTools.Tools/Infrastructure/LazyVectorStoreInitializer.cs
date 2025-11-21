using Microsoft.Extensions.Logging.Abstractions;
using UltrasharpTools.Tools.Semantic;

namespace UltrasharpTools.Tools.Infrastructure;

/// <summary>
/// Lazy initializer for VectorStore that resolves database path using ProjectPathHelper.
/// Initialized after solution is loaded to use correct .ultrasharp/ directory.
/// </summary>
public sealed class LazyVectorStoreInitializer
{
    private readonly VectorStore _vectorStore;
    private readonly VectorStoreConfig _config;
    private readonly ILogger<LazyVectorStoreInitializer> _logger;

    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LazyVectorStoreInitializer(
        VectorStore vectorStore,
        VectorStoreConfig config,
        ILogger<LazyVectorStoreInitializer>? logger = null
    )
    {
        _vectorStore = vectorStore;
        _config = config;
        _logger = logger ?? NullLogger<LazyVectorStoreInitializer>.Instance;
    }

    /// <summary>
    /// Initialize VectorStore with resolved database path.
    /// Safe to call multiple times - initialization happens only once.
    /// </summary>
    /// <param name="solutionPath">Optional solution path for resolving .ultrasharp/ directory</param>
    public async Task InitializeAsync(
        string? solutionPath = null,
        CancellationToken cancellationToken = default
    )
    {
        if (_initialized)
        {
            return; // Already initialized
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return; // Double-check after acquiring lock
            }

            // Resolve database path using ProjectPathHelper if not explicitly set
            string connectionString;

            if (
                !string.IsNullOrEmpty(_config.ConnectionString)
                && _config.ConnectionString != ":memory:"
            )
            {
                // Explicit path provided - use as-is
                connectionString = _config.ConnectionString;
                _logger.LogInformation(
                    "Initializing VectorStore with explicit database path: {Path}",
                    connectionString
                );
            }
            else
            {
                // Auto-resolve path using ProjectPathHelper
                var ultrasharpDir = ProjectPathHelper.GetProjectUltrasharpDir(solutionPath);
                var vectorDbPath = Path.Combine(ultrasharpDir, "vectors", "embeddings.db");

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(vectorDbPath)!);

                connectionString = $"Data Source={vectorDbPath}";

                _logger.LogInformation(
                    "Initializing VectorStore with auto-resolved path: {Path} (solution: {SolutionPath})",
                    vectorDbPath,
                    solutionPath ?? "<not specified>"
                );
            }

            // Initialize VectorStore
            await _vectorStore.InitializeAsync(
                connectionString,
                _config.Dimension,
                cancellationToken
            );

            _initialized = true;

            _logger.LogInformation("VectorStore initialized successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Check if VectorStore is initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Ensure VectorStore is initialized before use.
    /// Throws if not initialized.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "VectorStore is not initialized. "
                    + "Call LazyVectorStoreInitializer.InitializeAsync() after loading solution."
            );
        }
    }
}

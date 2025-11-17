namespace UltrasharpTools.Tools.Semantic.Embedding;

/// <summary>
/// Configuration options for embedding providers
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// Provider to use: "auto", "tei", "ollama", "memory"
    /// Default: "auto" (auto-detect based on GPU)
    /// </summary>
    public string Provider { get; set; } = "auto";

    /// <summary>
    /// Enable embeddings
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Auto-detect GPU and select optimal provider
    /// </summary>
    public bool AutoDetectGPU { get; set; } = true;

    /// <summary>
    /// TEI provider options
    /// </summary>
    public TEIOptions TEI { get; set; } = new();

    /// <summary>
    /// Ollama provider options
    /// </summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>
    /// Memory provider options
    /// </summary>
    public MemoryOptions Memory { get; set; } = new();
}

/// <summary>
/// TEI (Text Embeddings Inference) provider options
/// </summary>
public sealed class TEIOptions
{
    /// <summary>
    /// Base URL for TEI server
    /// Default: http://127.0.0.1:8080
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    /// <summary>
    /// Model to use
    /// Default: ibm-granite/granite-embedding-english-r2 (8192 tokens)
    /// </summary>
    public string Model { get; set; } = "ibm-granite/granite-embedding-english-r2";

    /// <summary>
    /// Timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Concurrency level for parallel requests
    /// </summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// Check server availability on startup
    /// </summary>
    public bool CheckServer { get; set; } = true;

    /// <summary>
    /// Auto-start Docker container if not running
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Docker container name
    /// </summary>
    public string ContainerName { get; set; } = "tei-server";
}

/// <summary>
/// Ollama provider options
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Base URL for Ollama server
    /// Default: http://127.0.0.1:11434
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    /// <summary>
    /// Model to use
    /// Default: granite-embedding (512 tokens, multilingual)
    /// </summary>
    public string Model { get; set; } = "granite-embedding";

    /// <summary>
    /// Timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Concurrency level for parallel requests
    /// </summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>
    /// Auto-pull model if not found
    /// </summary>
    public bool AutoPull { get; set; } = true;

    /// <summary>
    /// Check server availability on startup
    /// </summary>
    public bool CheckServer { get; set; } = true;
}

/// <summary>
/// Memory provider options (fallback, no ML)
/// </summary>
public sealed class MemoryOptions
{
    /// <summary>
    /// Use deterministic hash for embeddings
    /// </summary>
    public bool UseDeterministicHash { get; set; } = true;
}

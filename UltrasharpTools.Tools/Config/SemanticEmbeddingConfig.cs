using System.Collections.Generic;

namespace UltrasharpTools.Tools.Config;

/// <summary>
/// Global embedding configuration (near executable)
/// </summary>
public class SemanticEmbeddingConfig
{
    public EmbeddingSettings Embedding { get; set; } = new();
    public AutoDetectionSettings AutoDetection { get; set; } = new();
}

public class EmbeddingSettings
{
    /// <summary>
    /// Platform: tei, ollama, memory
    /// </summary>
    public string Platform { get; set; } = "tei";

    /// <summary>
    /// Architecture: auto, cpu, turing, ampere-80, ampere-86, ada, hopper, blackwell
    /// </summary>
    public string Architecture { get; set; } = "auto";

    public TeiSettings Tei { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public MemorySettings Memory { get; set; } = new();
}

public class TeiSettings
{
    public string Endpoint { get; set; } = "http://localhost:8080";
    public List<ModelInfo> Models { get; set; } = new()
    {
        new()
        {
            Id = "sentence-transformers/all-MiniLM-L6-v2",
            Languages = new() { "english" },
            VectorSize = 384
        },
        new()
        {
            Id = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2",
            Languages = new() { "multilingual" },
            VectorSize = 384
        }
    };
    public string SelectedModel { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";
}

public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public List<ModelInfo> Models { get; set; } = new()
    {
        new()
        {
            Id = "granite-embedding:latest",
            Languages = new() { "english" },
            VectorSize = 384
        },
        new()
        {
            Id = "mxbai-embed-large:latest",
            Languages = new() { "multilingual" },
            VectorSize = 1024
        }
    };
    public string SelectedModel { get; set; } = "granite-embedding:latest";
}

public class MemorySettings
{
    public string ModelPath { get; set; } = "./models/embedding";
    public int VectorSize { get; set; } = 384;
}

public class ModelInfo
{
    public string Id { get; set; } = "";
    public List<string> Languages { get; set; } = new();
    public int VectorSize { get; set; }
}

public class AutoDetectionSettings
{
    public bool GpuArchitecture { get; set; } = true;
    public bool Language { get; set; } = true;
    public bool CodebaseSize { get; set; } = true;
}

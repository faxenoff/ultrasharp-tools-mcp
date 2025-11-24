using System.Diagnostics.CodeAnalysis;

// JSON сериализация использует простые типы (float[], string[]), которые корректно работают с AOT
[assembly: UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:RequiresUnreferencedCode",
    Justification = "Simple types (float[], string[]) are preserved in AOT",
    Scope = "member",
    Target = "~M:UltraSharpTools.Indexer.Semantic.Embedding.Providers.TEIProvider.GetEmbeddingAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Single[]}")]

[assembly: UnconditionalSuppressMessage(
    "AOT",
    "IL3050:RequiresDynamicCode",
    Justification = "Simple types (float[], string[]) work with AOT",
    Scope = "member",
    Target = "~M:UltraSharpTools.Indexer.Semantic.Embedding.Providers.TEIProvider.GetEmbeddingAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Single[]}")]

[assembly: UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:RequiresUnreferencedCode",
    Justification = "Simple types (float[][]) are preserved in AOT",
    Scope = "member",
    Target = "~M:UltraSharpTools.Indexer.Semantic.Embedding.Providers.TEIProvider.GetEmbeddingsAsync(System.Collections.Generic.IReadOnlyList{System.String},System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Collections.Generic.IReadOnlyList{System.Single[]}}")]

[assembly: UnconditionalSuppressMessage(
    "AOT",
    "IL3050:RequiresDynamicCode",
    Justification = "Simple types (float[][]) work with AOT",
    Scope = "member",
    Target = "~M:UltraSharpTools.Indexer.Semantic.Embedding.Providers.TEIProvider.GetEmbeddingsAsync(System.Collections.Generic.IReadOnlyList{System.String},System.Threading.CancellationToken)~System.Threading.Tasks.Task{System.Collections.Generic.IReadOnlyList{System.Single[]}}")]

[assembly: UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:RequiresUnreferencedCode",
    Justification = "OllamaProvider uses DTOs that are preserved",
    Scope = "type",
    Target = "~T:UltraSharpTools.Indexer.Semantic.Embedding.Providers.OllamaProvider")]

[assembly: UnconditionalSuppressMessage(
    "AOT",
    "IL3050:RequiresDynamicCode",
    Justification = "OllamaProvider uses DTOs that work with AOT",
    Scope = "type",
    Target = "~T:UltraSharpTools.Indexer.Semantic.Embedding.Providers.OllamaProvider")]

[assembly: UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:RequiresUnreferencedCode",
    Justification = "VectorDBService uses simple response types",
    Scope = "type",
    Target = "~T:UltraSharpTools.Indexer.VectorDBService")]

[assembly: UnconditionalSuppressMessage(
    "AOT",
    "IL3050:RequiresDynamicCode",
    Justification = "VectorDBService uses simple response types",
    Scope = "type",
    Target = "~T:UltraSharpTools.Indexer.VectorDBService")]

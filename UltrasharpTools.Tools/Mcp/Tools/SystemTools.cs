
using ModelContextProtocol;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// System-level MCP tools for server capabilities and health checks
/// </summary>
[McpServerToolType]
public static class SystemTools
{
    [McpServerTool(
        Name = "get_capabilities",
        Idempotent = true,
        ReadOnly = true
    )]
    [Description("Returns server capabilities including semantic mode status, hybrid mode availability, and enabled features. Use this to discover available capabilities at runtime.")]
    public static async Task<object> GetCapabilities(
        ISemanticModeProvider semanticProvider,
        CancellationToken cancellationToken = default)
    {
        var availability = await semanticProvider.CheckAvailabilityAsync(cancellationToken);

        return new
        {
            serverInfo = new
            {
                name = "UltrasharpTools MCP Droid",
                version = "3.0.0",
                protocol = "MCP 1.0"
            },
            capabilities = new
            {
                semanticMode = new
                {
                    enabled = availability.IsAvailable,
                    source = availability.Source.ToString(),
                    modelName = availability.ModelName,
                    vectorDimension = availability.VectorDimension,
                    localEmbeddingUrl = availability.LocalEmbeddingUrl,
                    overlordUrl = availability.OverlordUrl,
                    dynamic = true,
                    cacheValiditySeconds = 60,
                    description = "Semantic code search and similarity analysis using vector embeddings"
                },
                features = new
                {
                    gitIntegration = true,
                    editorConfigSupport = true,
                    universalSemanticMode = true,
                    hybridMode = !string.IsNullOrEmpty(availability.OverlordUrl),
                    tracing = true,
                    codeModification = true,
                    projectAnalysis = true,
                    symbolCaching = true
                }
            },
            timestamp = DateTime.UtcNow
        };
    }
}

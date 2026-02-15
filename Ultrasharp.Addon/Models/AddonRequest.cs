using System.Text.Json.Serialization;

namespace Ultrasharp.Addon.Models;

/// <summary>
/// IPC request message from TS → C#.
/// Wire: { "id": "uuid", "type": "request", "method": "parse", "params": { ... } }
/// </summary>
public sealed class AddonRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "request";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public System.Text.Json.JsonElement? Params { get; set; }

    [JsonPropertyName("projectPath")]
    public string? ProjectPath { get; set; }
}

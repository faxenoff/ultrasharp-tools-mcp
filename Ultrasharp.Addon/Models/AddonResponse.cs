using System.Text.Json.Serialization;

namespace Ultrasharp.Addon.Models;

/// <summary>
/// IPC response message from C# → TS.
/// Wire: { "id": "uuid", "type": "response", "result": { ... } }
///   or: { "id": "uuid", "type": "response", "error": { "code": -1, "message": "..." } }
/// </summary>
public sealed class AddonResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "response";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AddonError? Error { get; set; }

    public static AddonResponse Success(string requestId, object? result) => new()
    {
        Id = requestId,
        Result = result,
    };

    public static AddonResponse Fail(string requestId, int code, string message) => new()
    {
        Id = requestId,
        Error = new AddonError { Code = code, Message = message },
    };
}

public sealed class AddonError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// IPC event message from C# → TS (push notification).
/// Wire: { "id": "uuid", "type": "event", "event": "phaseChanged", "data": { ... } }
/// </summary>
public sealed class AddonEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "event";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; set; }
}

/// <summary>
/// Standard IPC error codes (compatible with JSON-RPC and ipc-protocol.ts).
/// </summary>
public static class ErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ProjectNotFound = -32001;
    public const int NotReady = -32002;
}

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Ultrasharp.Addon.Ipc;

/// <summary>
/// Binary frame codec compatible with ipc-protocol.ts:
/// Wire format: [4 bytes Big-Endian length][JSON payload UTF-8]
/// </summary>
public static class BinaryFrameCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Encode an object to a binary frame: [4 BE length][JSON UTF-8 bytes].
    /// </summary>
    public static byte[] Encode<T>(T message)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var frame = new byte[4 + json.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), (uint)json.Length);
        json.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>
    /// Try to decode one complete frame from the buffer.
    /// Returns the deserialized message and advances the buffer position.
    /// Returns null if the buffer doesn't contain a complete frame yet.
    /// </summary>
    public static T? TryDecode<T>(ref ReadOnlyMemory<byte> buffer) where T : class
    {
        if (buffer.Length < 4)
            return null;

        var span = buffer.Span;
        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(span);

        if (buffer.Length < 4 + (int)payloadLength)
            return null;

        var payload = buffer.Slice(4, (int)payloadLength);
        var result = JsonSerializer.Deserialize<T>(payload.Span, JsonOptions);

        buffer = buffer.Slice(4 + (int)payloadLength);
        return result;
    }

    /// <summary>
    /// Decode JSON from raw bytes (no frame header).
    /// </summary>
    public static T? DeserializeJson<T>(ReadOnlySpan<byte> utf8Json) where T : class
    {
        return JsonSerializer.Deserialize<T>(utf8Json, JsonOptions);
    }

    /// <summary>
    /// Serialize to JSON string for logging.
    /// </summary>
    public static string ToJsonString<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    /// <summary>
    /// Get shared JSON options for consistent serialization.
    /// </summary>
    public static JsonSerializerOptions SharedJsonOptions => JsonOptions;
}

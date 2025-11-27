using Microsoft.Extensions.DependencyInjection;

namespace UltrasharpTools.Droid.Ipc;

/// <summary>
/// Extension methods для создания MCP сервера с Named Pipe транспортом.
/// </summary>
public static class McpPipeExtensions {
    /// <summary>
    /// Добавляет MCP сервер с транспортом через pipe streams.
    /// Делегирует к встроенному WithStreamServerTransport.
    /// </summary>
    public static IMcpServerBuilder WithPipeTransport(
    this IMcpServerBuilder builder,
    Stream inputStream,
    Stream outputStream,
    string? serverName = null) {
        // Используем встроенный метод MCP SDK
        return builder.WithStreamServerTransport(inputStream, outputStream);
    }
}

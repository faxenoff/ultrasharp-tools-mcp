namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Provides the unique client identifier for the current MCP session.
/// In pipe server mode, each client connection gets a unique ID.
/// In stdio mode (single client), this returns a constant ID.
/// </summary>
public interface IClientIdProvider {
    /// <summary>
    /// Gets the unique identifier for the current client session.
    /// </summary>
    string ClientId { get; }
}

/// <summary>
/// Default implementation for stdio mode (single client).
/// </summary>
public sealed class SingleClientIdProvider : IClientIdProvider {
    public string ClientId => "stdio-client";
}

/// <summary>
/// Implementation for pipe server mode where each client gets a unique ID.
/// </summary>
public sealed class PipeClientIdProvider : IClientIdProvider {
    public string ClientId { get; }

    public PipeClientIdProvider(string clientId) {
        ClientId = clientId;
    }
}

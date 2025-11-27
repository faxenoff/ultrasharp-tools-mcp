namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for tracking client-solution mappings and reference counting.
/// Allows unloading solutions only when the last client using them disconnects.
/// </summary>
public interface IClientContextService {
    /// <summary>
    /// Registers that a client has loaded a solution.
    /// If the client already had a different solution, the old one's ref count is decremented.
    /// </summary>
    /// <param name="clientId">Unique client identifier</param>
    /// <param name="solutionPath">Path to the loaded solution</param>
    void RegisterSolutionLoad(string clientId, string solutionPath);

    /// <summary>
    /// Called when a client disconnects.
    /// Decrements ref count for their solution and unloads if this was the last client.
    /// </summary>
    /// <param name="clientId">Unique client identifier</param>
    /// <returns>True if the solution was unloaded</returns>
    bool OnClientDisconnected(string clientId);

    /// <summary>
    /// Gets the number of active clients using a solution.
    /// </summary>
    /// <param name="solutionPath">Path to the solution</param>
    /// <returns>Reference count (0 if not loaded)</returns>
    int GetSolutionRefCount(string solutionPath);

    /// <summary>
    /// Gets the solution path for a client (or null if not registered).
    /// </summary>
    /// <param name="clientId">Unique client identifier</param>
    /// <returns>Solution path or null</returns>
    string? GetClientSolution(string clientId);
}

/// <summary>
/// Null implementation for stdio mode (single client, no ref counting needed).
/// </summary>
public sealed class NullClientContextService : IClientContextService {
    public void RegisterSolutionLoad(string clientId, string solutionPath) { }
    public bool OnClientDisconnected(string clientId) => false;
    public int GetSolutionRefCount(string solutionPath) => 1;
    public string? GetClientSolution(string clientId) => null;
}

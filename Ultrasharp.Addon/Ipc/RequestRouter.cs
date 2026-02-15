using Ultrasharp.Addon.Models;

namespace Ultrasharp.Addon.Ipc;

/// <summary>
/// Routes IPC requests to registered handlers by method name.
/// </summary>
public sealed class RequestRouter
{
    private readonly Dictionary<string, Func<AddonRequest, CancellationToken, Task<object?>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<RequestRouter> _logger;

    public RequestRouter(ILogger<RequestRouter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a handler for a method name.
    /// </summary>
    public void Register(string method, Func<AddonRequest, CancellationToken, Task<object?>> handler)
    {
        _handlers[method] = handler;
        _logger.LogDebug("[Router] Registered handler: {Method}", method);
    }

    /// <summary>
    /// Handle an incoming request by routing to the appropriate handler.
    /// </summary>
    public async Task<AddonResponse> HandleAsync(AddonRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Method))
        {
            return AddonResponse.Fail(request.Id, ErrorCodes.InvalidRequest, "Missing method");
        }

        if (!_handlers.TryGetValue(request.Method, out var handler))
        {
            _logger.LogWarning("[Router] Unknown method: {Method}", request.Method);
            return AddonResponse.Fail(request.Id, ErrorCodes.MethodNotFound, $"Unknown method: {request.Method}");
        }

        try
        {
            var result = await handler(request, ct);
            return AddonResponse.Success(request.Id, result);
        }
        catch (OperationCanceledException)
        {
            return AddonResponse.Fail(request.Id, ErrorCodes.InternalError, "Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Router] Error handling {Method}", request.Method);
            return AddonResponse.Fail(request.Id, ErrorCodes.InternalError, ex.Message);
        }
    }

    public IReadOnlyCollection<string> RegisteredMethods => _handlers.Keys;
}

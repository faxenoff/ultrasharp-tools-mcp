using System.Diagnostics;
using System.Text.Json;
using Ultrasharp.Addon.Models;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles "status", "loadSolution", "unloadSolution" requests.
/// </summary>
public sealed class StatusHandler
{
    private readonly AddonLifecycleService _lifecycle;
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<StatusHandler> _logger;

    public StatusHandler(
        AddonLifecycleService lifecycle,
        ISolutionManager solutionManager,
        ILogger<StatusHandler> logger)
    {
        _lifecycle = lifecycle;
        _solutionManager = solutionManager;
        _logger = logger;
    }

    /// <summary>
    /// Return current status: phase, loaded solution, memory, symbol count.
    /// </summary>
    public Task<object?> HandleStatusAsync(AddonRequest request, CancellationToken ct)
    {
        var process = Process.GetCurrentProcess();
        var solution = _solutionManager.CurrentSolution;

        object result = new
        {
            phase = _lifecycle.Phase,
            solutionPath = _lifecycle.LoadedSolutionPath,
            projectCount = solution?.ProjectIds.Count ?? 0,
            documentCount = solution?.Projects.Sum(p => p.DocumentIds.Count) ?? 0,
            memoryMB = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 1),
            version = Program.ApplicationVersion,
            uptime = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
        };

        return Task.FromResult<object?>(result);
    }

    /// <summary>
    /// Load a solution (triggers Phase 2).
    /// Params: { "path": string }
    /// </summary>
    public async Task<object?> HandleLoadSolutionAsync(AddonRequest request, CancellationToken ct)
    {
        var path = request.Params?.GetProperty("path").GetString();
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Missing 'path' parameter");

        _logger.LogInformation("[Status] Loading solution: {Path}", path);
        var success = await _lifecycle.LoadSolutionAsync(path, ct);

        return new
        {
            success,
            phase = _lifecycle.Phase,
            solutionPath = _lifecycle.LoadedSolutionPath,
        };
    }

    /// <summary>
    /// Unload current solution (back to Phase 1).
    /// </summary>
    public Task<object?> HandleUnloadSolutionAsync(AddonRequest request, CancellationToken ct)
    {
        _lifecycle.UnloadSolution();
        return Task.FromResult<object?>(new { success = true, phase = _lifecycle.Phase });
    }
}

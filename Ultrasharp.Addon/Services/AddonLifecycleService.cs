using Ultrasharp.Addon.Handlers;
using Ultrasharp.Addon.Ipc;
using Ultrasharp.Addon.Models;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Services;

/// <summary>
/// Manages Addon lifecycle phases:
///   Phase 1 (instant): Pipe server ready, syntax parsing available
///   Phase 2 (background, 5-30s): Solution loaded, semantic analysis available
///   Phase 3 (background): Background validation with debounce
/// </summary>
public sealed class AddonLifecycleService
{
    private readonly IServiceProvider _services;
    private readonly PipeServer _pipeServer;
    private readonly ILogger<AddonLifecycleService> _logger;
    private int _phase;

    public int Phase => _phase;
    public string? LoadedSolutionPath { get; private set; }

    public AddonLifecycleService(
        IServiceProvider services,
        PipeServer pipeServer,
        ILogger<AddonLifecycleService> logger)
    {
        _services = services;
        _pipeServer = pipeServer;
        _logger = logger;
    }

    /// <summary>
    /// Start Phase 1 (immediate) and optionally begin Phase 2 in background.
    /// </summary>
    public async Task StartAsync(string? slnPath, CancellationToken ct)
    {
        // Initialize handler routes
        var initializer = _services.GetRequiredService<IHandlerInitializer>();
        initializer.Initialize();

        // Phase 1: Pipe ready, syntax parsing available
        _phase = 1;
        _logger.LogInformation("[Lifecycle] Phase 1 reached — pipe ready, syntax parsing available.");

        // If solution path provided, start Phase 2 in background
        if (!string.IsNullOrEmpty(slnPath))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadSolutionAsync(slnPath, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Lifecycle] Phase 2 failed for: {Path}", slnPath);
                }
            }, ct);
        }
    }

    /// <summary>
    /// Load a solution (Phase 2). Can be called from handler or startup.
    /// </summary>
    public async Task<bool> LoadSolutionAsync(string slnPath, CancellationToken ct)
    {
        _logger.LogInformation("[Lifecycle] Phase 2 starting — loading solution: {Path}", slnPath);

        var orchestrator = _services.GetRequiredService<ILoadingOrchestrator>();
        var result = await orchestrator.RequestLoadingAsync(slnPath, LoadingSource.BackgroundStartup, ct);

        if (result.Success)
        {
            _phase = 2;
            LoadedSolutionPath = result.SolutionPath;

            _logger.LogInformation(
                "[Lifecycle] Phase 2 reached — solution loaded: {Path} ({ProjectCount} projects)",
                result.SolutionPath, result.ProjectCount);

            // Notify TS via event
            await SendPhaseEventAsync(2);
            return true;
        }

        _logger.LogError("[Lifecycle] Solution load failed: {Error}", result.ErrorMessage);
        return false;
    }

    /// <summary>
    /// Unload current solution.
    /// </summary>
    public void UnloadSolution()
    {
        var solutionManager = _services.GetRequiredService<ISolutionManager>();
        solutionManager.UnloadSolution();
        LoadedSolutionPath = null;
        _phase = 1;
        _logger.LogInformation("[Lifecycle] Solution unloaded, back to Phase 1.");
    }

    private async ValueTask SendPhaseEventAsync(int phase)
    {
        if (_pipeServer.SendEvent != null)
        {
            await _pipeServer.SendEvent(new AddonEvent
            {
                Event = "phaseChanged",
                Data = new { phase },
            });
        }
    }
}

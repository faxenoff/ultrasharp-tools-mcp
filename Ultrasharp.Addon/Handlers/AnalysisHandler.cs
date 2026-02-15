using System.Text.Json;
using Microsoft.CodeAnalysis;
using Ultrasharp.Addon.Models;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles semantic analysis requests: findReferences, getDefinition, getCallGraph,
/// getImplementations, traceFlow, traceBackwards.
/// All require Phase 2 (loaded solution).
/// </summary>
public sealed class AnalysisHandler
{
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeAnalysisService _codeAnalysis;
    private readonly ISourceResolutionService _sourceResolution;
    private readonly IExecutionTraceService _executionTrace;
    private readonly IBacktraceService _backtrace;
    private readonly AddonLifecycleService _lifecycle;
    private readonly ILogger<AnalysisHandler> _logger;

    public AnalysisHandler(
        ISolutionManager solutionManager,
        ICodeAnalysisService codeAnalysis,
        ISourceResolutionService sourceResolution,
        IExecutionTraceService executionTrace,
        IBacktraceService backtrace,
        AddonLifecycleService lifecycle,
        ILogger<AnalysisHandler> logger)
    {
        _solutionManager = solutionManager;
        _codeAnalysis = codeAnalysis;
        _sourceResolution = sourceResolution;
        _executionTrace = executionTrace;
        _backtrace = backtrace;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>
    /// Find all references to a symbol.
    /// Params: { "fqn": string }
    /// </summary>
    public async Task<object?> HandleFindReferencesAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var fqn = request.Params?.GetProperty("fqn").GetString() ?? "";
        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, ct);
        if (symbol == null)
            return new { error = $"Symbol not found: {fqn}", references = Array.Empty<object>() };

        var refs = await _codeAnalysis.FindReferencesAsync(symbol, ct);
        var results = refs.SelectMany(r => r.Locations.Select(loc =>
        {
            var lineSpan = loc.Location.GetLineSpan();
            return new
            {
                filePath = lineSpan.Path,
                line = lineSpan.StartLinePosition.Line + 1,
                column = lineSpan.StartLinePosition.Character + 1,
                text = loc.Location.SourceTree?.GetText()?.GetSubText(loc.Location.SourceSpan).ToString() ?? "",
            };
        })).ToList();

        return new { fqn, count = results.Count, references = results };
    }

    /// <summary>
    /// Get definition/source of a symbol.
    /// Params: { "fqn": string }
    /// </summary>
    public async Task<object?> HandleGetDefinitionAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var fqn = request.Params?.GetProperty("fqn").GetString() ?? "";
        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, ct);
        if (symbol == null)
            return new { error = $"Symbol not found: {fqn}" };

        var source = await _sourceResolution.ResolveSourceAsync(symbol, ct);
        if (source == null)
        {
            // Fallback: use symbol locations directly
            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
            if (location != null)
            {
                var lineSpan = location.GetLineSpan();
                return new
                {
                    fqn,
                    filePath = lineSpan.Path,
                    line = lineSpan.StartLinePosition.Line + 1,
                    content = location.SourceTree?.GetText(ct)?.ToString() ?? "",
                };
            }

            return new { error = $"No source found for: {fqn}" };
        }

        return new
        {
            fqn,
            filePath = source.FilePath,
            content = source.Source,
            isOriginalSource = source.IsOriginalSource,
            isDecompiled = source.IsDecompiled,
            resolutionMethod = source.ResolutionMethod,
        };
    }

    /// <summary>
    /// Get call graph (callers + outgoing calls) for a method.
    /// Params: { "fqn": string, "direction"?: "callers"|"callees"|"both" }
    /// </summary>
    public async Task<object?> HandleGetCallGraphAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var fqn = request.Params?.GetProperty("fqn").GetString() ?? "";
        string direction = "both";
        if (request.Params?.TryGetProperty("direction", out var dir) == true)
            direction = dir.GetString() ?? "both";

        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, ct);
        if (symbol == null)
            return new { error = $"Symbol not found: {fqn}" };

        object? callers = null;
        object? callees = null;

        if (direction is "callers" or "both")
        {
            var callerInfos = await _codeAnalysis.FindCallersAsync(symbol, ct);
            callers = callerInfos.Select(c =>
            {
                var loc = c.CallingSymbol.Locations.FirstOrDefault()?.GetLineSpan();
                return new
                {
                    name = c.CallingSymbol.Name,
                    fqn = c.CallingSymbol.ToDisplayString(),
                    filePath = loc?.Path ?? "",
                    line = (loc?.StartLinePosition.Line ?? 0) + 1,
                };
            }).ToList();
        }

        if (direction is "callees" or "both" && symbol is IMethodSymbol methodSymbol)
        {
            var outgoing = await _codeAnalysis.FindOutgoingCallsAsync(methodSymbol, ct);
            callees = outgoing.Select(s =>
            {
                var loc = s.Locations.FirstOrDefault()?.GetLineSpan();
                return new
                {
                    name = s.Name,
                    fqn = s.ToDisplayString(),
                    filePath = loc?.Path ?? "",
                    line = (loc?.StartLinePosition.Line ?? 0) + 1,
                };
            }).ToList();
        }

        return new { fqn, callers, callees };
    }

    /// <summary>
    /// Find implementations of an interface/abstract class.
    /// Params: { "fqn": string }
    /// </summary>
    public async Task<object?> HandleGetImplementationsAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var fqn = request.Params?.GetProperty("fqn").GetString() ?? "";
        var symbol = await _solutionManager.FindRoslynSymbolAsync(fqn, ct);
        if (symbol == null)
            return new { error = $"Symbol not found: {fqn}" };

        var implementations = await _codeAnalysis.FindImplementationsAsync(symbol, ct);
        var results = implementations.Select(impl =>
        {
            var loc = impl.Locations.FirstOrDefault(l => l.IsInSource)?.GetLineSpan();
            return new
            {
                name = impl.Name,
                fqn = impl.ToDisplayString(),
                filePath = loc?.Path ?? "",
                line = (loc?.StartLinePosition.Line ?? 0) + 1,
                kind = impl.Kind.ToString().ToLowerInvariant(),
            };
        }).ToList();

        return new { fqn, count = results.Count, implementations = results };
    }

    /// <summary>
    /// Trace execution flow from entry point to optional exit point.
    /// Params: { "entryPoint": string, "exitPoint"?: string, "maxDepth"?: number }
    /// </summary>
    public async Task<object?> HandleTraceFlowAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var entryPoint = request.Params?.GetProperty("entryPoint").GetString() ?? "";
        string? exitPoint = null;
        if (request.Params?.TryGetProperty("exitPoint", out var ep) == true)
            exitPoint = ep.GetString();
        int maxDepth = 10;
        if (request.Params?.TryGetProperty("maxDepth", out var md) == true)
            maxDepth = md.GetInt32();

        var trace = await _executionTrace.TraceExecutionAsync(
            entryPoint,
            exitPoint,
            maxDepth,
            includeExternalCalls: true,
            cancellationToken: ct);

        return trace;
    }

    /// <summary>
    /// Trace backwards from crash point.
    /// Params: { "crashPoint": string, "startPoint"?: string, "maxDepth"?: number, "maxPaths"?: number }
    /// </summary>
    public async Task<object?> HandleTraceBackwardsAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var crashPoint = request.Params?.GetProperty("crashPoint").GetString() ?? "";
        string? startPoint = null;
        if (request.Params?.TryGetProperty("startPoint", out var sp) == true)
            startPoint = sp.GetString();
        int maxDepth = 15;
        if (request.Params?.TryGetProperty("maxDepth", out var md) == true)
            maxDepth = md.GetInt32();
        int maxPaths = 5;
        if (request.Params?.TryGetProperty("maxPaths", out var mp) == true)
            maxPaths = mp.GetInt32();

        var result = await _backtrace.BacktraceFromCrashAsync(
            crashPoint,
            startPoint,
            maxDepth: maxDepth,
            maxPaths: maxPaths,
            cancellationToken: ct);

        return result;
    }
}

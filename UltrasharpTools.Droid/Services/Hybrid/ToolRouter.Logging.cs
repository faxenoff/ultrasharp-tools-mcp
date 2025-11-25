using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class ToolRouter
{
    // Routing decisions (5800-5804)
    [LoggerMessage(EventId = 5800, Level = LogLevel.Trace,
        Message = "Local mode: routing {ToolName} to LOCAL")]
    private partial void LogLocalRouting(string toolName);

    [LoggerMessage(EventId = 5801, Level = LogLevel.Debug,
        Message = "Semantic tool {ToolName}: routing to OVERLORD")]
    private partial void LogSemanticRouting(string toolName);

    [LoggerMessage(EventId = 5802, Level = LogLevel.Debug,
        Message = "Resource-intensive tool {ToolName}: routing to OVERLORD with fallback")]
    private partial void LogResourceIntensiveRouting(string toolName);

    [LoggerMessage(EventId = 5803, Level = LogLevel.Debug,
        Message = "Hybrid tool {ToolName}: routing to {Decision} based on arguments")]
    private partial void LogHybridRouting(string toolName, ToolRoutingDecision decision);

    [LoggerMessage(EventId = 5804, Level = LogLevel.Trace,
        Message = "Default routing for {ToolName}: LOCAL")]
    private partial void LogDefaultRouting(string toolName);

    // Availability check (5805-5806)
    [LoggerMessage(EventId = 5805, Level = LogLevel.Debug,
        Message = "Overlord availability check: {IsAvailable}")]
    private partial void LogAvailabilityCheck(bool isAvailable);

    [LoggerMessage(EventId = 5806, Level = LogLevel.Warning,
        Message = "Failed to check Overlord availability")]
    private partial void LogAvailabilityCheckFailed(Exception exception);
}

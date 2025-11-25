using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class McpToolInterceptor
{
    // Initialization (5200)
    [LoggerMessage(EventId = 5200, Level = LogLevel.Information,
        Message = "McpToolInterceptor initialized")]
    private partial void LogInitialized();

    [LoggerMessage(EventId = 5201, Level = LogLevel.Debug,
        Message = "Registered local executor for {Tool}")]
    private partial void LogRegisteredExecutor(string tool);

    // Execution (5202-5207)
    [LoggerMessage(EventId = 5202, Level = LogLevel.Debug,
        Message = "Executing MCP tool: {Tool} with {ArgCount} arguments")]
    private partial void LogExecutingTool(string tool, int argCount);

    [LoggerMessage(EventId = 5203, Level = LogLevel.Debug,
        Message = "Routing decision for {Tool}: {Decision}")]
    private partial void LogRoutingDecision(string tool, ToolRoutingDecision decision);

    [LoggerMessage(EventId = 5204, Level = LogLevel.Trace,
        Message = "Applying semantic enrichment for {Tool}")]
    private partial void LogApplyingEnrichment(string tool);

    [LoggerMessage(EventId = 5205, Level = LogLevel.Information,
        Message = "Tool {Tool} executed successfully in {Time}ms (routing: {Routing}, enriched: {Enriched})")]
    private partial void LogToolExecuted(string tool, long time, ToolRoutingDecision routing, bool enriched);

    [LoggerMessage(EventId = 5206, Level = LogLevel.Error,
        Message = "Tool {Tool} execution failed after {Time}ms")]
    private partial void LogToolFailed(Exception exception, string tool, long time);

    // Local execution (5207-5208)
    [LoggerMessage(EventId = 5207, Level = LogLevel.Trace,
        Message = "Executing {Tool} locally")]
    private partial void LogExecutingLocally(string tool);

    // Overlord execution (5209-5212)
    [LoggerMessage(EventId = 5209, Level = LogLevel.Trace,
        Message = "Executing {Tool} on Overlord")]
    private partial void LogExecutingOnOverlord(string tool);

    [LoggerMessage(EventId = 5210, Level = LogLevel.Trace,
        Message = "Executing {Tool} with Overlord fallback")]
    private partial void LogExecutingWithFallback(string tool);

    [LoggerMessage(EventId = 5211, Level = LogLevel.Debug,
        Message = "Overlord available - executing {Tool} remotely")]
    private partial void LogOverlordExecuting(string tool);

    [LoggerMessage(EventId = 5212, Level = LogLevel.Warning,
        Message = "Overlord execution failed for {Tool} - falling back to LOCAL")]
    private partial void LogOverlordFallback(Exception exception, string tool);

    [LoggerMessage(EventId = 5213, Level = LogLevel.Debug,
        Message = "Falling back to LOCAL execution for {Tool}")]
    private partial void LogFallingBackToLocal(string tool);
}

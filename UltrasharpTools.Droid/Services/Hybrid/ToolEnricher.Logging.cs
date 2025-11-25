using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class ToolEnricher
{
    // Initialization (5500)
    [LoggerMessage(EventId = 5500, Level = LogLevel.Information,
        Message = "ToolEnricher initialized: strategies={Count}, timeout={Timeout}s, maxConcurrency={Concurrency}, gracefulDegradation={Graceful}")]
    private partial void LogInitialized(int count, int timeout, int concurrency, bool graceful);

    // Enrichment checks (5501-5503)
    [LoggerMessage(EventId = 5501, Level = LogLevel.Trace,
        Message = "Semantic mode not available - returning original result for {Tool}")]
    private partial void LogSemanticNotAvailable(string tool);

    [LoggerMessage(EventId = 5502, Level = LogLevel.Trace,
        Message = "Enrichment disabled for {Tool} by configuration")]
    private partial void LogEnrichmentDisabled(string tool);

    [LoggerMessage(EventId = 5503, Level = LogLevel.Trace,
        Message = "No enrichment strategy found for {Tool}")]
    private partial void LogNoStrategy(string tool);

    // Enrichment execution (5504-5507)
    [LoggerMessage(EventId = 5504, Level = LogLevel.Debug,
        Message = "Enriching {Tool} with {Strategy}")]
    private partial void LogEnriching(string tool, string strategy);

    [LoggerMessage(EventId = 5505, Level = LogLevel.Information,
        Message = "Enriched {Tool} with {Matches} semantic matches in {Time}ms")]
    private partial void LogEnriched(string tool, int matches, long time);

    [LoggerMessage(EventId = 5506, Level = LogLevel.Warning,
        Message = "Enrichment for {Tool} timed out after {Timeout}s")]
    private partial void LogEnrichmentTimeout(string tool, int timeout);

    [LoggerMessage(EventId = 5507, Level = LogLevel.Error,
        Message = "Enrichment failed for {Tool}")]
    private partial void LogEnrichmentFailed(Exception exception, string tool);
}

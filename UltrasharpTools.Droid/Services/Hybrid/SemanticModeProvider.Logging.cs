using Microsoft.Extensions.Logging;
using UltrasharpTools.Droid.Models.Hybrid;

namespace UltrasharpTools.Droid.Services.Hybrid;

public sealed partial class SemanticModeProvider
{
    // Initialization (5000)
    [LoggerMessage(EventId = 5000, Level = LogLevel.Debug,
        Message = "SemanticModeProvider initialized: enabled={Enabled}, cacheValidity={CacheSeconds}s, localTimeout={LocalTimeout}s, overlordTimeout={OverlordTimeout}s")]
    private partial void LogProviderInitialized(bool enabled, int cacheSeconds, int localTimeout, int overlordTimeout);

    // Availability checks (5010-5020)
    [LoggerMessage(EventId = 5010, Level = LogLevel.Debug,
        Message = "Semantic mode is DISABLED by configuration")]
    private partial void LogSemanticModeDisabled();

    [LoggerMessage(EventId = 5011, Level = LogLevel.Trace,
        Message = "Returning cached semantic mode availability: {Source}")]
    private partial void LogCachedAvailability(SemanticModeSource source);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Debug,
        Message = "Checking semantic mode availability...")]
    private partial void LogCheckingAvailability();

    [LoggerMessage(EventId = 5013, Level = LogLevel.Information,
        Message = "Semantic mode available from BOTH sources (Local + Overlord)")]
    private partial void LogBothSourcesAvailable();

    [LoggerMessage(EventId = 5014, Level = LogLevel.Information,
        Message = "Semantic mode available from LOCAL embedding service")]
    private partial void LogLocalSourceAvailable();

    [LoggerMessage(EventId = 5015, Level = LogLevel.Information,
        Message = "Semantic mode available from OVERLORD")]
    private partial void LogOverlordSourceAvailable();

    [LoggerMessage(EventId = 5016, Level = LogLevel.Warning,
        Message = "Semantic mode is NOT available - no embedding services detected")]
    private partial void LogNoSourcesAvailable();

    // Embedding operations (5020-5030)
    [LoggerMessage(EventId = 5020, Level = LogLevel.Warning,
        Message = "Cannot get embedding for empty text")]
    private partial void LogEmptyTextEmbedding();

    [LoggerMessage(EventId = 5021, Level = LogLevel.Warning,
        Message = "Semantic mode not available - cannot generate embedding")]
    private partial void LogEmbeddingNotAvailable();

    [LoggerMessage(EventId = 5022, Level = LogLevel.Trace,
        Message = "Getting embedding from LOCAL service")]
    private partial void LogGettingLocalEmbedding();

    [LoggerMessage(EventId = 5023, Level = LogLevel.Trace,
        Message = "Getting embedding from OVERLORD")]
    private partial void LogGettingOverlordEmbedding();

    [LoggerMessage(EventId = 5024, Level = LogLevel.Warning,
        Message = "Unexpected result type from Overlord embedding: {Type}")]
    private partial void LogUnexpectedEmbeddingType(string? type);

    [LoggerMessage(EventId = 5025, Level = LogLevel.Error,
        Message = "Failed to get embedding for text (length: {Length})")]
    private partial void LogEmbeddingFailed(Exception exception, int length);

    // Search operations (5030-5040)
    [LoggerMessage(EventId = 5030, Level = LogLevel.Warning,
        Message = "Cannot search with empty query vector")]
    private partial void LogEmptyQueryVector();

    [LoggerMessage(EventId = 5031, Level = LogLevel.Warning,
        Message = "Semantic mode not available - cannot perform search")]
    private partial void LogSearchNotAvailable();

    [LoggerMessage(EventId = 5032, Level = LogLevel.Trace,
        Message = "Performing semantic search via OVERLORD (topK: {TopK}, threshold: {Threshold})")]
    private partial void LogOverlordSearch(int topK, double threshold);

    [LoggerMessage(EventId = 5033, Level = LogLevel.Warning,
        Message = "Unexpected result type from Overlord search: {Type}")]
    private partial void LogUnexpectedSearchType(string? type);

    [LoggerMessage(EventId = 5034, Level = LogLevel.Debug,
        Message = "Local-only semantic search not implemented yet - returning empty results")]
    private partial void LogLocalSearchNotImplemented();

    [LoggerMessage(EventId = 5035, Level = LogLevel.Error,
        Message = "Semantic search failed (vector dim: {Dim}, topK: {TopK})")]
    private partial void LogSearchFailed(Exception exception, int dim, int topK);

    // Text search (5040-5045)
    [LoggerMessage(EventId = 5040, Level = LogLevel.Warning,
        Message = "Cannot search with empty query text")]
    private partial void LogEmptyQueryText();

    [LoggerMessage(EventId = 5041, Level = LogLevel.Debug,
        Message = "Converting query to vector: {Query}")]
    private partial void LogConvertingQuery(string query);

    [LoggerMessage(EventId = 5042, Level = LogLevel.Warning,
        Message = "Failed to vectorize query - cannot perform search")]
    private partial void LogVectorizeFailed();

    // Local availability check (5050-5060)
    [LoggerMessage(EventId = 5050, Level = LogLevel.Warning,
        Message = "Local embedding service not registered (_localEmbedding is null)")]
    private partial void LogLocalNotRegistered();

    [LoggerMessage(EventId = 5051, Level = LogLevel.Information,
        Message = "Local embedding available (attempt {Attempt}/{MaxRetries})")]
    private partial void LogLocalAvailable(int attempt, int maxRetries);

    [LoggerMessage(EventId = 5052, Level = LogLevel.Warning,
        Message = "Local embedding not available - GetEmbeddingAsync returned null/empty (attempt {Attempt}/{MaxRetries})")]
    private partial void LogLocalNotAvailableNoException(int attempt, int maxRetries);

    [LoggerMessage(EventId = 5053, Level = LogLevel.Warning,
        Message = "Local embedding check timed out (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...")]
    private partial void LogLocalTimeout(int attempt, int maxRetries, double delay);

    [LoggerMessage(EventId = 5054, Level = LogLevel.Warning,
        Message = "Local embedding service check timed out after {Attempts} attempts")]
    private partial void LogLocalTimeoutFinal(int attempts);

    [LoggerMessage(EventId = 5055, Level = LogLevel.Warning,
        Message = "Local embedding check failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...")]
    private partial void LogLocalCheckFailed(Exception exception, int attempt, int maxRetries, double delay);

    [LoggerMessage(EventId = 5056, Level = LogLevel.Warning,
        Message = "Local embedding service check failed after {Attempts} attempts")]
    private partial void LogLocalCheckFailedFinal(Exception exception, int attempts);

    // Overlord availability check (5060-5070)
    [LoggerMessage(EventId = 5060, Level = LogLevel.Trace,
        Message = "Overlord not configured")]
    private partial void LogOverlordNotConfigured();

    [LoggerMessage(EventId = 5061, Level = LogLevel.Trace,
        Message = "Overlord available (attempt {Attempt}/{MaxRetries})")]
    private partial void LogOverlordAvailable(int attempt, int maxRetries);

    [LoggerMessage(EventId = 5062, Level = LogLevel.Trace,
        Message = "Overlord not available (no exception, attempt {Attempt}/{MaxRetries})")]
    private partial void LogOverlordNotAvailableNoException(int attempt, int maxRetries);

    [LoggerMessage(EventId = 5063, Level = LogLevel.Trace,
        Message = "Overlord check timed out (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...")]
    private partial void LogOverlordTimeout(int attempt, int maxRetries, double delay);

    [LoggerMessage(EventId = 5064, Level = LogLevel.Trace,
        Message = "Overlord availability check timed out after {Attempts} attempts")]
    private partial void LogOverlordTimeoutFinal(int attempts);

    [LoggerMessage(EventId = 5065, Level = LogLevel.Trace,
        Message = "Overlord check failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...")]
    private partial void LogOverlordCheckFailed(Exception exception, int attempt, int maxRetries, double delay);

    [LoggerMessage(EventId = 5066, Level = LogLevel.Trace,
        Message = "Overlord availability check failed after {Attempts} attempts")]
    private partial void LogOverlordCheckFailedFinal(Exception exception, int attempts);
}

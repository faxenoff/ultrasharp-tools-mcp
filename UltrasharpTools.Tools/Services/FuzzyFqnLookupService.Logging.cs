using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class FuzzyFqnLookupService
{
    [LoggerMessage(EventId = 4030, Level = LogLevel.Warning,
        Message = "Cannot perform fuzzy FQN lookup: No solution loaded.")]
    private partial void LogNoSolutionLoaded();

    [LoggerMessage(EventId = 4031, Level = LogLevel.Warning,
        Message = "Could not get semantic model for document {DocumentPath}")]
    private partial void LogSemanticModelFailed(string? documentPath);

    [LoggerMessage(EventId = 4032, Level = LogLevel.Debug,
        Message = "Collected {SymbolCount} symbols from solution documents (parallel mode)")]
    private partial void LogSymbolsCollected(int symbolCount);

    [LoggerMessage(EventId = 4033, Level = LogLevel.Debug,
        Message = "Filtered {PrefilterCount} symbols to {SymbolCount} after removing duplicates")]
    private partial void LogSymbolsFiltered(int prefilterCount, int symbolCount);

    [LoggerMessage(EventId = 4034, Level = LogLevel.Debug,
        Message = "Perfect match found for '{FuzzyFqn}', stopping search early")]
    private partial void LogPerfectMatchFound(string fuzzyFqn);

    [LoggerMessage(EventId = 4035, Level = LogLevel.Debug,
        Message = "Symbol matching stopped early (perfect match or enough results found)")]
    private partial void LogSearchStoppedEarly();

    [LoggerMessage(EventId = 4036, Level = LogLevel.Debug,
        Message = "Found {MatchCount} matches for fuzzy FQN '{FuzzyFqn}'")]
    private partial void LogMatchesFound(int matchCount, string fuzzyFqn);

    [LoggerMessage(EventId = 4037, Level = LogLevel.Debug,
        Message = "Filtered to single perfect match for '{FuzzyFqn}'")]
    private partial void LogFilteredToPerfect(string fuzzyFqn);

    [LoggerMessage(EventId = 4038, Level = LogLevel.Warning,
        Message = "Ambiguity detected for input '{FuzzyFqn}': Found {HighScoreCount} high-scoring matches (>= {Threshold})")]
    private partial void LogAmbiguityDetected(string fuzzyFqn, int highScoreCount, double threshold);

    [LoggerMessage(EventId = 4039, Level = LogLevel.Warning,
        Message = "Cross-project ambiguity detected: Matches span {ProjectCount} projects")]
    private partial void LogCrossProjectAmbiguity(int projectCount);

    [LoggerMessage(EventId = 4040, Level = LogLevel.Information,
        Message = "Project '{ProjectName}' has {MatchCount} ambiguous matches")]
    private partial void LogProjectAmbiguousMatches(string projectName, int matchCount);

    [LoggerMessage(EventId = 4041, Level = LogLevel.Warning,
        Message = "Ambiguous Match #{Rank}: Score={Score:F3}, Reason='{Reason}'")]
    private partial void LogAmbiguousMatch(int rank, double score, string reason);

    [LoggerMessage(EventId = 4042, Level = LogLevel.Information,
        Message = "  Symbol Details:")]
    private partial void LogSymbolDetails();

    [LoggerMessage(EventId = 4043, Level = LogLevel.Information,
        Message = "    FQN: {CanonicalFqn}")]
    private partial void LogFqn(string canonicalFqn);

    [LoggerMessage(EventId = 4044, Level = LogLevel.Information,
        Message = "    Formatted Signature: {FormattedSignature}")]
    private partial void LogFormattedSignature(string formattedSignature);

    [LoggerMessage(EventId = 4045, Level = LogLevel.Information,
        Message = "    Symbol Kind: {SymbolKind}")]
    private partial void LogSymbolKind(string symbolKind);

    [LoggerMessage(EventId = 4046, Level = LogLevel.Information,
        Message = "    Symbol Name: {SymbolName}")]
    private partial void LogSymbolName(string symbolName);

    [LoggerMessage(EventId = 4047, Level = LogLevel.Information,
        Message = "    Project: {ProjectName}")]
    private partial void LogProject(string projectName);

    [LoggerMessage(EventId = 4048, Level = LogLevel.Information,
        Message = "    Assembly: {AssemblyName}")]
    private partial void LogAssembly(string assemblyName);

    [LoggerMessage(EventId = 4049, Level = LogLevel.Information,
        Message = "    Location: {Location}")]
    private partial void LogLocation(string location);

    [LoggerMessage(EventId = 4050, Level = LogLevel.Information,
        Message = "    Containing Type: {ContainingType}")]
    private partial void LogContainingType(string containingType);

    [LoggerMessage(EventId = 4051, Level = LogLevel.Information,
        Message = "    Accessibility: {Accessibility}")]
    private partial void LogAccessibility(string accessibility);

    [LoggerMessage(EventId = 4052, Level = LogLevel.Information,
        Message = "    Modifiers: {Modifiers}")]
    private partial void LogModifiers(string modifiers);

    [LoggerMessage(EventId = 4053, Level = LogLevel.Error,
        Message = "Error logging detailed match info for symbol {SymbolName}")]
    private partial void LogDetailedMatchError(Exception exception, string symbolName);

    [LoggerMessage(EventId = 4054, Level = LogLevel.Information,
        Message = "    Type Kind: {TypeKind}")]
    private partial void LogTypeKind(string typeKind);

    [LoggerMessage(EventId = 4055, Level = LogLevel.Information,
        Message = "    Is Generic: {IsGeneric}")]
    private partial void LogIsGeneric(bool isGeneric);

    [LoggerMessage(EventId = 4056, Level = LogLevel.Information,
        Message = "    Arity: {Arity}")]
    private partial void LogArity(int arity);

    [LoggerMessage(EventId = 4057, Level = LogLevel.Information,
        Message = "    Member Count: {MemberCount}")]
    private partial void LogMemberCount(int memberCount);

    [LoggerMessage(EventId = 4058, Level = LogLevel.Information,
        Message = "    Base Type: {BaseType}")]
    private partial void LogBaseType(string baseType);

    [LoggerMessage(EventId = 4059, Level = LogLevel.Information,
        Message = "    Implements: {InterfaceCount} interfaces")]
    private partial void LogImplementsInterfaces(int interfaceCount);

    [LoggerMessage(EventId = 4060, Level = LogLevel.Information,
        Message = "    Method Kind: {MethodKind}")]
    private partial void LogMethodKind(string methodKind);

    [LoggerMessage(EventId = 4061, Level = LogLevel.Information,
        Message = "    Return Type: {ReturnType}")]
    private partial void LogReturnType(string returnType);

    [LoggerMessage(EventId = 4062, Level = LogLevel.Information,
        Message = "    Parameter Count: {ParameterCount}")]
    private partial void LogParameterCount(int parameterCount);

    [LoggerMessage(EventId = 4063, Level = LogLevel.Information,
        Message = "    Is Extension: {IsExtension}")]
    private partial void LogIsExtension(bool isExtension);

    [LoggerMessage(EventId = 4064, Level = LogLevel.Information,
        Message = "    Is Async: {IsAsync}")]
    private partial void LogIsAsync(bool isAsync);

    [LoggerMessage(EventId = 4065, Level = LogLevel.Information,
        Message = "    Parameters: {ParameterTypes}")]
    private partial void LogParameters(string parameterTypes);

    [LoggerMessage(EventId = 4066, Level = LogLevel.Information,
        Message = "    Property Type: {PropertyType}")]
    private partial void LogPropertyType(string propertyType);

    [LoggerMessage(EventId = 4067, Level = LogLevel.Information,
        Message = "    Is ReadOnly: {IsReadOnly}")]
    private partial void LogIsReadOnly(bool isReadOnly);

    [LoggerMessage(EventId = 4068, Level = LogLevel.Information,
        Message = "    Is WriteOnly: {IsWriteOnly}")]
    private partial void LogIsWriteOnly(bool isWriteOnly);

    [LoggerMessage(EventId = 4069, Level = LogLevel.Information,
        Message = "    Is Indexer: {IsIndexer}")]
    private partial void LogIsIndexer(bool isIndexer);

    [LoggerMessage(EventId = 4070, Level = LogLevel.Information,
        Message = "    Field Type: {FieldType}")]
    private partial void LogFieldType(string fieldType);

    [LoggerMessage(EventId = 4071, Level = LogLevel.Information,
        Message = "    Is Const: {IsConst}")]
    private partial void LogIsConst(bool isConst);

    [LoggerMessage(EventId = 4072, Level = LogLevel.Information,
        Message = "    Is Static: {IsStatic}")]
    private partial void LogIsStatic(bool isStatic);

    [LoggerMessage(EventId = 4073, Level = LogLevel.Information,
        Message = "    Constant Value: {ConstantValue}")]
    private partial void LogConstantValue(object? constantValue);

    [LoggerMessage(EventId = 4074, Level = LogLevel.Information,
        Message = "    Event Type: {EventType}")]
    private partial void LogEventType(string eventType);

    [LoggerMessage(EventId = 4075, Level = LogLevel.Information,
        Message = "Ambiguity Summary for '{FuzzyFqn}':")]
    private partial void LogAmbiguitySummaryHeader(string fuzzyFqn);

    [LoggerMessage(EventId = 4076, Level = LogLevel.Information,
        Message = "  Total matches: {TotalMatches}")]
    private partial void LogTotalMatches(int totalMatches);

    [LoggerMessage(EventId = 4077, Level = LogLevel.Information,
        Message = "  Perfect matches (>= {PerfectThreshold:F3}): {PerfectCount}")]
    private partial void LogPerfectMatchCount(double perfectThreshold, int perfectCount);

    [LoggerMessage(EventId = 4078, Level = LogLevel.Information,
        Message = "  High-scoring matches (>= 0.8): {HighScoreCount}")]
    private partial void LogHighScoreMatchCount(int highScoreCount);

    [LoggerMessage(EventId = 4079, Level = LogLevel.Information,
        Message = "  {Label} matches ({Lower:F1}-{Upper:F1}): {Count}")]
    private partial void LogScoreRangeCount(string label, double lower, double upper, int count);

    [LoggerMessage(EventId = 4080, Level = LogLevel.Information,
        Message = "  Symbol kinds in high-scoring matches:")]
    private partial void LogSymbolKindsHeader();

    [LoggerMessage(EventId = 4081, Level = LogLevel.Information,
        Message = "    {SymbolKind}: {Count}")]
    private partial void LogSymbolKindCount(string symbolKind, int count);

    [LoggerMessage(EventId = 4082, Level = LogLevel.Information,
        Message = "  Project distribution:")]
    private partial void LogProjectDistributionHeader();

    [LoggerMessage(EventId = 4083, Level = LogLevel.Information,
        Message = "    {ProjectName}: {Count}")]
    private partial void LogProjectCount(string projectName, int count);
}

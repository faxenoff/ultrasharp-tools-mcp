using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class CodeAnalysisService
{
    [LoggerMessage(EventId = 3750, Level = LogLevel.Debug,
        Message = "Finding implementations for symbol: {SymbolName}")]
    private partial void LogFindingImplementations(string symbolName);

    [LoggerMessage(EventId = 3751, Level = LogLevel.Debug,
        Message = "Finding overrides for symbol: {SymbolName}")]
    private partial void LogFindingOverrides(string symbolName);

    [LoggerMessage(EventId = 3752, Level = LogLevel.Debug,
        Message = "Finding references for symbol: {SymbolName}")]
    private partial void LogFindingReferences(string symbolName);

    [LoggerMessage(EventId = 3753, Level = LogLevel.Debug,
        Message = "Cache hit for FindReferences: {SymbolName} ({Count} locations)")]
    private partial void LogFindReferencesCacheHit(string symbolName, int count);

    [LoggerMessage(EventId = 3754, Level = LogLevel.Debug,
        Message = "Finding derived classes for type: {TypeName}")]
    private partial void LogFindingDerivedClasses(string typeName);

    [LoggerMessage(EventId = 3755, Level = LogLevel.Debug,
        Message = "Finding derived interfaces for type: {TypeName}")]
    private partial void LogFindingDerivedInterfaces(string typeName);

    [LoggerMessage(EventId = 3756, Level = LogLevel.Debug,
        Message = "Finding callers for symbol: {SymbolName}")]
    private partial void LogFindingCallers(string symbolName);

    [LoggerMessage(EventId = 3757, Level = LogLevel.Debug,
        Message = "Cache hit for FindCallers: {SymbolName} ({Count} callers)")]
    private partial void LogFindCallersCacheHit(string symbolName, int count);

    [LoggerMessage(EventId = 3758, Level = LogLevel.Debug,
        Message = "Cached FindCallers result for {SymbolName} ({Count} callers)")]
    private partial void LogFindCallersCached(string symbolName, int count);

    [LoggerMessage(EventId = 3759, Level = LogLevel.Debug,
        Message = "Finding outgoing calls for method: {MethodName}")]
    private partial void LogFindingOutgoingCalls(string methodName);

    [LoggerMessage(EventId = 3760, Level = LogLevel.Debug,
        Message = "Cache hit for FindOutgoingCalls: {MethodName} ({Count} calls)")]
    private partial void LogFindOutgoingCallsCacheHit(string methodName, int count);

    [LoggerMessage(EventId = 3761, Level = LogLevel.Warning,
        Message = "Method {MethodName} has no declaring syntax references, cannot find outgoing calls.")]
    private partial void LogMethodNoSyntaxReferences(string methodName);

    [LoggerMessage(EventId = 3762, Level = LogLevel.Warning,
        Message = "Could not get document for syntax tree {FilePath} of method {MethodName}")]
    private partial void LogCouldNotGetDocument(string filePath, string methodName);

    [LoggerMessage(EventId = 3763, Level = LogLevel.Warning,
        Message = "Could not get semantic model for method {MethodName} in document {DocumentPath}")]
    private partial void LogCouldNotGetSemanticModel(string methodName, string? documentPath);

    [LoggerMessage(EventId = 3764, Level = LogLevel.Debug,
        Message = "Cached FindOutgoingCalls result for {MethodName} ({Count} calls)")]
    private partial void LogFindOutgoingCallsCached(string methodName, int count);

    [LoggerMessage(EventId = 3765, Level = LogLevel.Warning,
        Message = "Cannot analyze referenced types: Type symbol is null.")]
    private partial void LogTypeSymbolNull();

    [LoggerMessage(EventId = 3766, Level = LogLevel.Debug,
        Message = "Cache hit for FindReferencedTypes: {TypeName} ({Count} types)")]
    private partial void LogFindReferencedTypesCacheHit(string typeName, int count);

    [LoggerMessage(EventId = 3767, Level = LogLevel.Warning,
        Message = "Error finding referenced types for type {TypeName}")]
    private partial void LogFindReferencedTypesError(Exception exception, string typeName);

    [LoggerMessage(EventId = 3768, Level = LogLevel.Debug,
        Message = "Cached FindReferencedTypes result for {TypeName} ({Count} types)")]
    private partial void LogFindReferencedTypesCached(string typeName, int count);
}

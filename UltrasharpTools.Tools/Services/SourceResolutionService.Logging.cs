using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SourceResolutionService
{
    [LoggerMessage(EventId = 3940, Level = LogLevel.Warning,
        Message = "Cannot resolve source: Symbol is null")]
    private partial void LogSymbolNull();

    [LoggerMessage(EventId = 3941, Level = LogLevel.Information,
        Message = "Attempting to retrieve source via Source Link for {SymbolName}")]
    private partial void LogAttemptingSourceLink(string symbolName);

    [LoggerMessage(EventId = 3942, Level = LogLevel.Warning,
        Message = "No containing assembly found for symbol {SymbolName}")]
    private partial void LogNoContainingAssembly(string symbolName);

    [LoggerMessage(EventId = 3943, Level = LogLevel.Warning,
        Message = "No metadata reference found for assembly {AssemblyName}")]
    private partial void LogNoMetadataReference(string assemblyName);

    [LoggerMessage(EventId = 3944, Level = LogLevel.Warning,
        Message = "Assembly file not found: {Path}")]
    private partial void LogAssemblyNotFound(string? path);

    [LoggerMessage(EventId = 3945, Level = LogLevel.Warning,
        Message = "PDB file not found: {PdbPath}")]
    private partial void LogPdbNotFound(string pdbPath);

    [LoggerMessage(EventId = 3946, Level = LogLevel.Information,
        Message = "Found PDB file: {PdbPath}")]
    private partial void LogFoundPdb(string pdbPath);

    [LoggerMessage(EventId = 3947, Level = LogLevel.Warning,
        Message = "No Source Link information found in PDB")]
    private partial void LogNoSourceLinkInPdb();

    [LoggerMessage(EventId = 3948, Level = LogLevel.Information,
        Message = "Found Source Link JSON: {Json}")]
    private partial void LogFoundSourceLinkJson(string json);

    [LoggerMessage(EventId = 3949, Level = LogLevel.Warning,
        Message = "Could not determine document path for symbol {SymbolName}")]
    private partial void LogCannotDetermineDocumentPath(string symbolName);

    [LoggerMessage(EventId = 3950, Level = LogLevel.Warning,
        Message = "No matching source URL found for document {Path}")]
    private partial void LogNoMatchingSourceUrl(string path);

    [LoggerMessage(EventId = 3951, Level = LogLevel.Information,
        Message = "Downloading source from URL: {Url}")]
    private partial void LogDownloadingSource(string url);

    [LoggerMessage(EventId = 3952, Level = LogLevel.Error,
        Message = "Error retrieving source via Source Link for {SymbolName}")]
    private partial void LogSourceLinkError(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3953, Level = LogLevel.Information,
        Message = "Attempting to retrieve embedded source for {SymbolName}")]
    private partial void LogAttemptingEmbeddedSource(string symbolName);

    [LoggerMessage(EventId = 3954, Level = LogLevel.Information,
        Message = "Checking for embedded source in assembly: {AssemblyPath}")]
    private partial void LogCheckingEmbeddedSource(string assemblyPath);

    [LoggerMessage(EventId = 3955, Level = LogLevel.Information,
        Message = "No embedded source info available for {SymbolName}")]
    private partial void LogNoEmbeddedSourceInfo(string symbolName);

    [LoggerMessage(EventId = 3956, Level = LogLevel.Debug,
        Message = "Error reading embedded sources from assembly: {AssemblyPath}")]
    private partial void LogEmbeddedSourceReadError(Exception exception, string assemblyPath);

    [LoggerMessage(EventId = 3957, Level = LogLevel.Information,
        Message = "Checking standalone PDB file: {PdbPath}")]
    private partial void LogCheckingStandalonePdb(string pdbPath);

    [LoggerMessage(EventId = 3958, Level = LogLevel.Debug,
        Message = "Error reading embedded sources from PDB: {PdbPath}")]
    private partial void LogPdbSourceReadError(Exception exception, string pdbPath);

    [LoggerMessage(EventId = 3959, Level = LogLevel.Information,
        Message = "No embedded sources found in assembly or PDB for {SymbolName}")]
    private partial void LogNoEmbeddedSourcesFound(string symbolName);

    [LoggerMessage(EventId = 3960, Level = LogLevel.Information,
        Message = "Found exact matching source file: {FileName}")]
    private partial void LogFoundExactMatch(string fileName);

    [LoggerMessage(EventId = 3961, Level = LogLevel.Information,
        Message = "Found matching source file by name: {FileName}")]
    private partial void LogFoundFileNameMatch(string fileName);

    [LoggerMessage(EventId = 3962, Level = LogLevel.Information,
        Message = "Found source file for containing type: {TypeName}")]
    private partial void LogFoundContainingTypeSource(string typeName);

    [LoggerMessage(EventId = 3963, Level = LogLevel.Information,
        Message = "Using single available source file: {FileName}")]
    private partial void LogUsingSingleSource(string fileName);

    [LoggerMessage(EventId = 3964, Level = LogLevel.Warning,
        Message = "No matching embedded source found for symbol {SymbolName} among {Count} available files")]
    private partial void LogNoMatchingEmbeddedSource(string symbolName, int count);

    [LoggerMessage(EventId = 3965, Level = LogLevel.Error,
        Message = "Error retrieving embedded source for {SymbolName}")]
    private partial void LogEmbeddedSourceError(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3966, Level = LogLevel.Information,
        Message = "Attempting decompilation for {SymbolName}")]
    private partial void LogAttemptingDecompilation(string symbolName);

    [LoggerMessage(EventId = 3967, Level = LogLevel.Information,
        Message = "Decompiling from assembly: {AssemblyPath}")]
    private partial void LogDecompilingFromAssembly(string assemblyPath);

    [LoggerMessage(EventId = 3968, Level = LogLevel.Warning,
        Message = "Could not determine type name for symbol {SymbolName}")]
    private partial void LogCannotDetermineTypeName(string symbolName);

    [LoggerMessage(EventId = 3969, Level = LogLevel.Warning,
        Message = "Could not find type definition for {TypeName}")]
    private partial void LogTypeDefinitionNotFound(string typeName);

    [LoggerMessage(EventId = 3970, Level = LogLevel.Warning,
        Message = "Could not find member {MemberName} in type {TypeName}")]
    private partial void LogMemberNotFound(string memberName, string typeName);

    [LoggerMessage(EventId = 3971, Level = LogLevel.Warning,
        Message = "Error during specific decompilation for {SymbolName}, falling back to full type decompilation")]
    private partial void LogDecompilationFallback(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3972, Level = LogLevel.Error,
        Message = "Fallback decompilation failed for {SymbolName}")]
    private partial void LogFallbackDecompilationFailed(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3973, Level = LogLevel.Error,
        Message = "Error during decompilation for {SymbolName}")]
    private partial void LogDecompilationError(Exception exception, string symbolName);

    [LoggerMessage(EventId = 3974, Level = LogLevel.Warning,
        Message = "Cannot get metadata reference: Solution not loaded")]
    private partial void LogSolutionNotLoaded();
}

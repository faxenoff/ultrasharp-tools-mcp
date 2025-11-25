using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class PdbSymbolResolver
{
    [LoggerMessage(EventId = 3550, Level = LogLevel.Warning,
        Message = "Assembly not found: {Path}")]
    private partial void LogAssemblyNotFound(string path);

    [LoggerMessage(EventId = 3551, Level = LogLevel.Error,
        Message = "Failed to load PDB for assembly: {Path}")]
    private partial void LogPdbLoadFailed(Exception exception, string path);

    [LoggerMessage(EventId = 3552, Level = LogLevel.Debug,
        Message = "Loaded embedded PDB for: {Path}")]
    private partial void LogEmbeddedPdbLoaded(string path);

    [LoggerMessage(EventId = 3553, Level = LogLevel.Warning,
        Message = "PDB file not found: {Path}")]
    private partial void LogPdbNotFound(string path);

    [LoggerMessage(EventId = 3554, Level = LogLevel.Debug,
        Message = "Loaded external PDB for: {Path}")]
    private partial void LogExternalPdbLoaded(string path);
}

using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SqliteSymbolIndex
{
    [LoggerMessage(EventId = 3560, Level = LogLevel.Information,
        Message = "SQLite symbol index initialized at {DbPath}")]
    private partial void LogInitialized(string dbPath);

    [LoggerMessage(EventId = 3561, Level = LogLevel.Information,
        Message = "Building SQLite symbol index from solution...")]
    private partial void LogBuildingIndex();

    [LoggerMessage(EventId = 3562, Level = LogLevel.Information,
        Message = "SQLite symbol index is up-to-date with {Count} symbols")]
    private partial void LogIndexUpToDate(int count);

    [LoggerMessage(EventId = 3563, Level = LogLevel.Debug,
        Message = "Inserted {Count} symbols...")]
    private partial void LogInsertedSymbols(int count);

    [LoggerMessage(EventId = 3564, Level = LogLevel.Information,
        Message = "SQLite symbol index built: {Count} symbols in {ElapsedMs}ms. DB size: {Size}")]
    private partial void LogIndexBuilt(int count, long elapsedMs, string size);

    [LoggerMessage(EventId = 3565, Level = LogLevel.Debug,
        Message = "Removed {Count} symbols for document {DocId}")]
    private partial void LogRemovedSymbols(int count, string docId);
}

using Microsoft.Extensions.Logging;
using UltraSharpTools.VectorDB.Semantic.Models;

namespace UltraSharpTools.VectorDB.Semantic;

public sealed partial class VectorStore
{
    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Initializing VectorStore with dimension={Dimension}, backend={BackendType}")]
    private partial void LogInitializing(int dimension, VectorStoreBackendType backendType);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "VectorStore initialized with {BackendType} backend")]
    private partial void LogInitialized(VectorStoreBackendType backendType);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Information,
        Message = "Switching backend from {OldBackend} to {NewBackend} (vector count: {Count})")]
    private partial void LogBackendSwitching(VectorStoreBackendType oldBackend, VectorStoreBackendType newBackend, int count);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Warning,
        Message = "Backend switching detected but data migration not yet implemented. Manual reindexing required after switching from {OldBackend} to {NewBackend}.")]
    private partial void LogMigrationRequired(VectorStoreBackendType oldBackend, VectorStoreBackendType newBackend);
}

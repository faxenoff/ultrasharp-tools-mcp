using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class EditorConfigProvider
{
    [LoggerMessage(EventId = 3540, Level = LogLevel.Information,
        Message = "Root .editorconfig found at: {Path}")]
    private partial void LogEditorConfigFound(string path);

    [LoggerMessage(EventId = 3541, Level = LogLevel.Information,
        Message = ".editorconfig not found in solution directory or parent directories up to repository root.")]
    private partial void LogEditorConfigNotFound();
}

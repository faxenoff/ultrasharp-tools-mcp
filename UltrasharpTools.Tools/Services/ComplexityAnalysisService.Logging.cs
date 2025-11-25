using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class ComplexityAnalysisService
{
    // Complexity analysis (3410-3419)
    [LoggerMessage(EventId = 3410, Level = LogLevel.Warning,
        Message = "Method {Method} has no syntax reference")]
    private partial void LogMethodNoSyntaxRef(string method);

    [LoggerMessage(EventId = 3411, Level = LogLevel.Warning,
        Message = "Could not get method syntax for {Method}")]
    private partial void LogMethodSyntaxNotFound(string method);

    [LoggerMessage(EventId = 3412, Level = LogLevel.Warning,
        Message = "Cannot analyze method dependencies: No solution loaded")]
    private partial void LogNoSolutionForMethodAnalysis();

    [LoggerMessage(EventId = 3413, Level = LogLevel.Warning,
        Message = "Cannot analyze type dependencies: No solution loaded")]
    private partial void LogNoSolutionForTypeAnalysis();
}

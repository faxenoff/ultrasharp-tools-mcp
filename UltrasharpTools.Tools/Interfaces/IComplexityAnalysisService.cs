

namespace UltrasharpTools.Tools.Interfaces;

public interface IComplexityAnalysisService {
    Task AnalyzeMethodAsync(
        IMethodSymbol methodSymbol,
        Dictionary<string, object> metrics,
        List<string> recommendations,
        CancellationToken cancellationToken,
        Compilation? compilation = null);

    Task AnalyzeTypeAsync(
        INamedTypeSymbol typeSymbol,
        Dictionary<string, object> metrics,
        List<string> recommendations,
        bool includeGeneratedCode,
        CancellationToken cancellationToken,
        Compilation? compilation = null);

    Task AnalyzeProjectAsync(
        Project project,
        Dictionary<string, object> metrics,
        List<string> recommendations,
        bool includeGeneratedCode,
        CancellationToken cancellationToken);
}

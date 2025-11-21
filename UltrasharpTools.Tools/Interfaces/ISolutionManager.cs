
using UltrasharpTools.Tools.Layered;

namespace UltrasharpTools.Tools.Interfaces;

public interface ISolutionManager : IDisposable {
    [MemberNotNullWhen(true, nameof(CurrentWorkspace), nameof(CurrentSolution))]
    bool IsSolutionLoaded { get; }
    MSBuildWorkspace? CurrentWorkspace { get; }
    Solution? CurrentSolution { get; }
    FastSymbolIndex SymbolIndex { get; }

    /// <summary>
    /// Layered symbol index with branch and working directory support (optional, Phase 1+).
    /// Returns null if layered indexing is not enabled.
    /// </summary>
    ILayeredIndex? LayeredIndex { get; }

    /// <summary>
    /// Git workflow service for coordinating git operations with layered indexing (optional, Phase 5).
    /// Returns null if git integration is not enabled.
    /// </summary>
    GitWorkflowService? GitWorkflowService { get; }

    Task LoadSolutionAsync(string solutionPath, CancellationToken cancellationToken);
    Task<bool> TryAutoLoadSolutionAsync(CancellationToken cancellationToken);
    void UnloadSolution();

    Task<ISymbol?> FindRoslynSymbolAsync(string fullyQualifiedName, CancellationToken cancellationToken);
    Task<INamedTypeSymbol?> FindRoslynNamedTypeSymbolAsync(string fullyQualifiedTypeName, CancellationToken cancellationToken);
    Task<Type?> FindReflectionTypeAsync(string fullyQualifiedTypeName, CancellationToken cancellationToken);
    Task<IEnumerable<Type>> SearchReflectionTypesAsync(string regexPattern, CancellationToken cancellationToken);

    IEnumerable<Project> GetProjects();
    Project? GetProjectByName(string projectName);
    ValueTask<SemanticModel?> GetSemanticModelAsync(DocumentId documentId, CancellationToken cancellationToken);
    ValueTask<Compilation?> GetCompilationAsync(ProjectId projectId, CancellationToken cancellationToken);
    Task ReloadSolutionFromDiskAsync(CancellationToken cancellationToken);
    void RefreshCurrentSolution();
}
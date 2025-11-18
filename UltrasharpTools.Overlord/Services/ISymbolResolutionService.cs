using Microsoft.CodeAnalysis;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для резолва FQN → ISymbol на стороне сервера
/// Ключевой компонент для реализации MCP proxy
/// </summary>
public interface ISymbolResolutionService
{
    /// <summary>
    /// Найти Roslyn ISymbol по FQN
    /// </summary>
    Task<ISymbol?> FindSymbolAsync(string fullyQualifiedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Найти Roslyn INamedTypeSymbol по FQN
    /// </summary>
    Task<INamedTypeSymbol?> FindNamedTypeSymbolAsync(string fullyQualifiedTypeName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить, загружена ли solution
    /// </summary>
    bool IsSolutionLoaded { get; }
}

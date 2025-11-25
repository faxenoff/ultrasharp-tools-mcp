using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация Symbol Resolution для MCP proxy
/// Использует существующие ISolutionManager и IFuzzyFqnLookupService
/// </summary>
public sealed partial class SymbolResolutionService : ISymbolResolutionService
{
    private readonly ILogger<SymbolResolutionService> _logger;
    private readonly ISolutionManager _solutionManager;
    private readonly IFuzzyFqnLookupService _fuzzyLookup;

    public SymbolResolutionService(
        ILogger<SymbolResolutionService> logger,
        ISolutionManager solutionManager,
        IFuzzyFqnLookupService fuzzyLookup
    )
    {
        _logger = logger;
        _solutionManager = solutionManager;
        _fuzzyLookup = fuzzyLookup;
    }

    public bool IsSolutionLoaded => _solutionManager.IsSolutionLoaded;

    public async Task<ISymbol?> FindSymbolAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_solutionManager.IsSolutionLoaded)
        {
            LogNoSolutionLoaded(fullyQualifiedName);
            return null;
        }

        try
        {
            // Используем существующий метод из ISolutionManager
            var symbol = await _solutionManager.FindRoslynSymbolAsync(
                fullyQualifiedName,
                cancellationToken
            );

            if (symbol != null)
            {
                LogFoundSymbol(fullyQualifiedName);
                return symbol;
            }

            // Fallback: попытка через fuzzy lookup
            LogTryingFuzzyLookup(fullyQualifiedName);
            var matches = await _fuzzyLookup.FindMatchesAsync(
                fullyQualifiedName,
                _solutionManager,
                cancellationToken
            );
            var bestMatch = matches.OrderByDescending(m => m.Score).FirstOrDefault();

            if (bestMatch != null)
            {
                LogFoundViaFuzzy(bestMatch.CanonicalFqn, bestMatch.Score, bestMatch.MatchReason);
                return bestMatch.Symbol;
            }

            LogSymbolNotFound(fullyQualifiedName);
            return null;
        }
        catch (Exception ex)
        {
            LogFindSymbolError(ex, fullyQualifiedName);
            return null;
        }
    }

    public async Task<INamedTypeSymbol?> FindNamedTypeSymbolAsync(
        string fullyQualifiedTypeName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_solutionManager.IsSolutionLoaded)
        {
            LogNoSolutionLoadedForType(fullyQualifiedTypeName);
            return null;
        }

        try
        {
            // Используем существующий метод из ISolutionManager
            var symbol = await _solutionManager.FindRoslynNamedTypeSymbolAsync(
                fullyQualifiedTypeName,
                cancellationToken
            );

            if (symbol != null)
            {
                LogFoundNamedType(fullyQualifiedTypeName);
                return symbol;
            }

            // Fallback: попытка через fuzzy lookup
            LogTryingFuzzyLookupForType(fullyQualifiedTypeName);
            var matches = await _fuzzyLookup.FindMatchesAsync(
                fullyQualifiedTypeName,
                _solutionManager,
                cancellationToken
            );
            var bestMatch = matches
                .Where(m => m.Symbol is INamedTypeSymbol)
                .OrderByDescending(m => m.Score)
                .FirstOrDefault();

            if (bestMatch != null)
            {
                LogFoundTypeViaFuzzy(bestMatch.CanonicalFqn, bestMatch.Score);
                return (INamedTypeSymbol)bestMatch.Symbol;
            }

            LogTypeNotFound(fullyQualifiedTypeName);
            return null;
        }
        catch (Exception ex)
        {
            LogFindTypeError(ex, fullyQualifiedTypeName);
            return null;
        }
    }
}

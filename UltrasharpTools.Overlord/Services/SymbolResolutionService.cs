using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Interfaces;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Реализация Symbol Resolution для MCP proxy
/// Использует существующие ISolutionManager и IFuzzyFqnLookupService
/// </summary>
public sealed class SymbolResolutionService : ISymbolResolutionService
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
            _logger.LogWarning("Cannot find symbol {Fqn}: no solution loaded", fullyQualifiedName);
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
                _logger.LogDebug("Found symbol {Fqn} via SolutionManager", fullyQualifiedName);
                return symbol;
            }

            // Fallback: попытка через fuzzy lookup
            _logger.LogDebug(
                "Symbol {Fqn} not found via SolutionManager, trying fuzzy lookup",
                fullyQualifiedName
            );
            var matches = await _fuzzyLookup.FindMatchesAsync(
                fullyQualifiedName,
                _solutionManager,
                cancellationToken
            );
            var bestMatch = matches.OrderByDescending(m => m.Score).FirstOrDefault();

            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "Found symbol {Fqn} via fuzzy lookup (score: {Score}, reason: {Reason})",
                    bestMatch.CanonicalFqn,
                    bestMatch.Score,
                    bestMatch.MatchReason
                );
                return bestMatch.Symbol;
            }

            _logger.LogWarning("Symbol {Fqn} not found in solution", fullyQualifiedName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding symbol {Fqn}", fullyQualifiedName);
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
            _logger.LogWarning(
                "Cannot find type {Fqn}: no solution loaded",
                fullyQualifiedTypeName
            );
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
                _logger.LogDebug("Found named type symbol {Fqn}", fullyQualifiedTypeName);
                return symbol;
            }

            // Fallback: попытка через fuzzy lookup
            _logger.LogDebug(
                "Type {Fqn} not found via SolutionManager, trying fuzzy lookup",
                fullyQualifiedTypeName
            );
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
                _logger.LogInformation(
                    "Found type {Fqn} via fuzzy lookup (score: {Score})",
                    bestMatch.CanonicalFqn,
                    bestMatch.Score
                );
                return (INamedTypeSymbol)bestMatch.Symbol;
            }

            _logger.LogWarning("Type {Fqn} not found in solution", fullyQualifiedTypeName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding type {Fqn}", fullyQualifiedTypeName);
            return null;
        }
    }
}

using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpTools.Tools.Interfaces {
    /// <summary>
    /// Service for performing fuzzy lookups of fully qualified names in the solution
    /// </summary>
    public interface IFuzzyFqnLookupService {
        /// <summary>
        /// Finds symbols matching the provided fuzzy FQN input
        /// </summary>
        /// <param name="fuzzyFqnInput">The fuzzy fully qualified name to search for</param>
        /// <returns>A collection of match results ordered by relevance</returns>
        Task<IEnumerable<FuzzyMatchResult>> FindMatchesAsync(string fuzzyFqnInput, ISolutionManager solutionManager, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a result from a fuzzy FQN lookup
    /// </summary>
    /// <param name="CanonicalFqn">The canonical fully qualified name</param>
    /// <param name="Symbol">The matched symbol</param>
    /// <param name="Score">The match score (higher is better, 1.0 is perfect)</param>
    /// <param name="MatchReason">Description of why this was considered a match</param>
    public record FuzzyMatchResult(
        string CanonicalFqn,
        ISymbol Symbol,
        double Score,
        string MatchReason
    );
}
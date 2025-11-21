namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Provides fuzzy matching for stack trace hints with tolerance for incomplete/corrupted traces.
/// Uses Levenshtein distance and partial FQN matching.
/// </summary>
public static class FuzzyStackTraceMatcher
{
    /// <summary>
    /// Matches a method symbol against stack trace hints with fuzzy matching.
    /// Returns a confidence score between 0.0 and 1.0.
    /// </summary>
    public static double MatchConfidence(IMethodSymbol method, List<string>? stackTraceHints)
    {
        if (stackTraceHints == null || stackTraceHints.Count == 0)
        {
            return 0.0;
        }

        var methodName = method.Name;
        var typeName = method.ContainingType?.Name;
        var fullTypeName = method.ContainingType?.ToDisplayString();
        var namespaceName = method.ContainingNamespace?.ToDisplayString();
        var fullMethodSignature = method.ToDisplayString();

        double bestScore = 0.0;

        foreach (var hint in stackTraceHints)
        {
            // Exact match (highest score)
            if (hint.Contains(fullMethodSignature, StringComparison.Ordinal))
            {
                return 1.0;
            }

            // Type + method name exact match
            if (
                typeName != null
                && hint.Contains($"{typeName}.{methodName}", StringComparison.OrdinalIgnoreCase)
            )
            {
                bestScore = Math.Max(bestScore, 0.95);
                continue;
            }

            // Namespace partial match
            if (
                namespaceName != null
                && hint.Contains(namespaceName, StringComparison.OrdinalIgnoreCase)
            )
            {
                bestScore = Math.Max(bestScore, 0.7);
            }

            // Type name fuzzy match
            if (typeName != null)
            {
                var typeScore = FuzzyMatchString(hint, typeName);
                if (typeScore > 0.8)
                {
                    bestScore = Math.Max(bestScore, 0.6 * typeScore);
                }
            }

            // Method name fuzzy match
            var methodScore = FuzzyMatchString(hint, methodName);
            if (methodScore > 0.8)
            {
                bestScore = Math.Max(bestScore, 0.5 * methodScore);
            }

            // Full type name partial match (handles generics, nested types)
            if (fullTypeName != null)
            {
                var fullTypeScore = PartialMatch(hint, fullTypeName);
                if (fullTypeScore > 0.7)
                {
                    bestScore = Math.Max(bestScore, 0.65 * fullTypeScore);
                }
            }
        }

        return bestScore;
    }

    /// <summary>
    /// Fuzzy string matching using normalized Levenshtein distance.
    /// Returns similarity score between 0.0 and 1.0.
    /// </summary>
    private static double FuzzyMatchString(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return 0.0;
        }

        // Case-insensitive comparison
        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // Exact match
        if (source.Contains(target) || target.Contains(source))
        {
            return 1.0;
        }

        // Levenshtein distance
        var distance = LevenshteinDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);
        var similarity = 1.0 - (double)distance / maxLength;

        return Math.Max(0.0, similarity);
    }

    /// <summary>
    /// Partial matching for FQN-like strings (handles dots, generics, nested types).
    /// </summary>
    private static double PartialMatch(string hint, string target)
    {
        if (string.IsNullOrEmpty(hint) || string.IsNullOrEmpty(target))
        {
            return 0.0;
        }

        hint = hint.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // Split by dots and match segments
        var hintSegments = hint.Split(
            new[] { '.', ',', '<', '>', ' ', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries
        );
        var targetSegments = target.Split(
            new[] { '.', '<', '>' },
            StringSplitOptions.RemoveEmptyEntries
        );

        int matchedSegments = 0;
        foreach (var targetSeg in targetSegments)
        {
            foreach (var hintSeg in hintSegments)
            {
                if (targetSeg.Contains(hintSeg) || hintSeg.Contains(targetSeg))
                {
                    matchedSegments++;
                    break;
                }

                // Fuzzy match individual segments
                if (FuzzyMatchString(hintSeg, targetSeg) > 0.85)
                {
                    matchedSegments++;
                    break;
                }
            }
        }

        return targetSegments.Length > 0 ? (double)matchedSegments / targetSegments.Length : 0.0;
    }

    /// <summary>
    /// Calculates Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return target?.Length ?? 0;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int sourceLength = source.Length;
        int targetLength = target.Length;

        // Optimization: if length difference is too large, early exit
        if (Math.Abs(sourceLength - targetLength) > Math.Max(sourceLength, targetLength) / 2)
        {
            return Math.Max(sourceLength, targetLength);
        }

        var matrix = new int[sourceLength + 1, targetLength + 1];

        // Initialize matrix
        for (int i = 0; i <= sourceLength; i++)
        {
            matrix[i, 0] = i;
        }

        for (int j = 0; j <= targetLength; j++)
        {
            matrix[0, j] = j;
        }

        // Compute distances
        for (int i = 1; i <= sourceLength; i++)
        {
            for (int j = 1; j <= targetLength; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost
                );
            }
        }

        return matrix[sourceLength, targetLength];
    }
}

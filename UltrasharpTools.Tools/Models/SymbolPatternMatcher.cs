namespace UltrasharpTools.Tools.Models;

/// <summary>
/// Matches symbol names against patterns supporting wildcards (* and ?) or regex.
/// Also handles pattern-based replacement (e.g., "test*" → "nonmod*").
/// </summary>
public class SymbolPatternMatcher
{
    private readonly string _pattern;
    private readonly PatternType _patternType;
    private readonly Regex? _regex;

    public SymbolPatternMatcher(string pattern, PatternType patternType = PatternType.Auto)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        _patternType = patternType == PatternType.Auto ? DetectPatternType(pattern) : patternType;

        // Compile regex for performance
        if (_patternType == PatternType.Regex)
        {
            _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }
        else if (_patternType == PatternType.Wildcard)
        {
            // Convert wildcard to regex
            var regexPattern =
                "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            _regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }
    }

    /// <summary>
    /// Check if a symbol name matches the pattern
    /// </summary>
    public bool IsMatch(string symbolName)
    {
        if (string.IsNullOrEmpty(symbolName))
            return false;

        return _patternType switch
        {
            PatternType.Exact => symbolName.Equals(_pattern, StringComparison.Ordinal),
            PatternType.Wildcard => _regex!.IsMatch(symbolName),
            PatternType.Regex => _regex!.IsMatch(symbolName),
            _ => false,
        };
    }

    /// <summary>
    /// Apply replacement pattern to a matched symbol name.
    /// Example: pattern "test*" with replacement "nonmod*" transforms "testItem" → "nonmodItem"
    /// </summary>
    public string ApplyReplacement(string symbolName, string replacementPattern)
    {
        if (string.IsNullOrEmpty(symbolName))
            throw new ArgumentException("Symbol name cannot be null or empty", nameof(symbolName));

        if (string.IsNullOrEmpty(replacementPattern))
            throw new ArgumentException(
                "Replacement pattern cannot be null or empty",
                nameof(replacementPattern)
            );

        // For exact match, just return the replacement
        if (_patternType == PatternType.Exact)
        {
            return replacementPattern;
        }

        // For regex, use regex replacement
        if (_patternType == PatternType.Regex && _regex != null)
        {
            return _regex.Replace(symbolName, replacementPattern);
        }

        // For wildcard patterns
        if (_patternType == PatternType.Wildcard)
        {
            return ApplyWildcardReplacement(symbolName, _pattern, replacementPattern);
        }

        return symbolName;
    }

    /// <summary>
    /// Apply wildcard-based replacement.
    /// Handles patterns like "test*" → "nonmod*" for "testItem" → "nonmodItem"
    /// </summary>
    private static string ApplyWildcardReplacement(
        string symbolName,
        string pattern,
        string replacementPattern
    )
    {
        // Find all wildcard positions in pattern and replacement
        var wildcardPositions = FindWildcardPositions(pattern);
        var replacementWildcards = FindWildcardPositions(replacementPattern);

        // Extract captured parts from symbol name using pattern
        var capturedParts = ExtractWildcardCaptures(symbolName, pattern, wildcardPositions);

        // Build result by substituting wildcards in replacement pattern
        var result = replacementPattern;
        for (int i = 0; i < Math.Min(capturedParts.Count, replacementWildcards.Count); i++)
        {
            // Replace first occurrence of * or ?
            var wildcardChar = replacementWildcards[i].Type == WildcardType.Star ? "*" : "?";
            var indexOf = result.IndexOf(wildcardChar);
            if (indexOf >= 0)
            {
                result =
                    result.Substring(0, indexOf) + capturedParts[i] + result.Substring(indexOf + 1);
            }
        }

        return result;
    }

    private static List<(int Position, WildcardType Type)> FindWildcardPositions(string pattern)
    {
        var positions = new List<(int, WildcardType)>();
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '*')
                positions.Add((i, WildcardType.Star));
            else if (pattern[i] == '?')
                positions.Add((i, WildcardType.Question));
        }
        return positions;
    }

    private static List<string> ExtractWildcardCaptures(
        string input,
        string pattern,
        List<(int Position, WildcardType Type)> wildcardPositions
    )
    {
        var captures = new List<string>();

        if (wildcardPositions.Count == 0)
        {
            return captures;
        }

        int inputIndex = 0;
        int patternIndex = 0;

        foreach (var (position, type) in wildcardPositions)
        {
            // Match literal part before wildcard
            while (patternIndex < position)
            {
                if (inputIndex >= input.Length || input[inputIndex] != pattern[patternIndex])
                {
                    return captures; // Mismatch
                }
                inputIndex++;
                patternIndex++;
            }

            // Capture wildcard content
            if (type == WildcardType.Question)
            {
                // ? matches exactly one character
                if (inputIndex < input.Length)
                {
                    captures.Add(input[inputIndex].ToString());
                    inputIndex++;
                }
                patternIndex++; // Skip the ?
            }
            else if (type == WildcardType.Star)
            {
                // * matches zero or more characters until next literal or end
                patternIndex++; // Skip the *

                int captureStart = inputIndex;

                // Find where the capture should end
                if (patternIndex < pattern.Length)
                {
                    // Find next literal character in pattern
                    char nextLiteral = pattern[patternIndex];
                    int nextLiteralIndex = input.IndexOf(nextLiteral, inputIndex);

                    if (nextLiteralIndex >= 0)
                    {
                        captures.Add(
                            input.Substring(captureStart, nextLiteralIndex - captureStart)
                        );
                        inputIndex = nextLiteralIndex;
                    }
                    else
                    {
                        // No match found for next literal
                        return captures;
                    }
                }
                else
                {
                    // * is at the end, capture rest of string
                    captures.Add(input.Substring(captureStart));
                    inputIndex = input.Length;
                }
            }
        }

        return captures;
    }

    private static PatternType DetectPatternType(string pattern)
    {
        // Check for regex special characters (excluding * and ?)
        if (
            pattern.Contains('[')
            || pattern.Contains('(')
            || pattern.Contains('^')
            || pattern.Contains('$')
            || pattern.Contains('{')
            || pattern.Contains('|')
            || pattern.Contains('+')
            || pattern.Contains('.') && pattern.Contains('*')
        )
        {
            return PatternType.Regex;
        }

        // Check for wildcards
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            return PatternType.Wildcard;
        }

        // Exact match
        return PatternType.Exact;
    }

    private enum WildcardType
    {
        Star,
        Question,
    }
}

public enum PatternType
{
    /// <summary>
    /// Auto-detect pattern type (default)
    /// </summary>
    Auto,

    /// <summary>
    /// Exact string match
    /// </summary>
    Exact,

    /// <summary>
    /// Wildcard pattern (* and ?)
    /// </summary>
    Wildcard,

    /// <summary>
    /// Regular expression pattern
    /// </summary>
    Regex,
}

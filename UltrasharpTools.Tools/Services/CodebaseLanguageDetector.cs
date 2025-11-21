namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Detects the primary language used in codebase comments/strings
/// </summary>
public class CodebaseLanguageDetector
{
    // ASCII printable range (English and basic symbols)
    private static readonly Regex EnglishWordPattern = new(
        @"\b[a-zA-Z]{2,}\b",
        RegexOptions.Compiled
    );

    // Non-ASCII letters (Cyrillic, Chinese, Japanese, etc.)
    private static readonly Regex NonAsciiPattern = new(
        @"[\u0400-\u04FF\u4E00-\u9FFF\u3040-\u309F\u30A0-\u30FF\u3400-\u4DBF]+",
        RegexOptions.Compiled
    );
    private static readonly char[] separator = new[] { ' ', '\t', '\n', '\r' };

    public class LanguageStats
    {
        public int TotalFiles { get; set; }
        public int TotalComments { get; set; }
        public int EnglishWords { get; set; }
        public int NonEnglishWords { get; set; }
        public int TotalWords => EnglishWords + NonEnglishWords;
        public double NonEnglishPercentage =>
            TotalWords > 0 ? (double)NonEnglishWords / TotalWords * 100 : 0;

        public string RecommendedLanguage(int threshold = 20)
        {
            if (TotalWords < 50)
                return "english"; // Not enough data, default to English

            return NonEnglishPercentage >= threshold ? "multilingual" : "english";
        }
    }

    /// <summary>
    /// Analyze language distribution across all C# files in workspace
    /// </summary>
    public async Task<LanguageStats> AnalyzeAsync(
        Solution solution,
        CancellationToken cancellationToken = default
    )
    {
        var stats = new LanguageStats();

        var projects = solution.Projects.ToList();

        foreach (var project in projects)
        {
            var documents = project.Documents.ToList();

            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tree = await document.GetSyntaxTreeAsync(cancellationToken);
                if (tree == null)
                    continue;

                var root = await tree.GetRootAsync(cancellationToken);

                stats.TotalFiles++;

                // Extract all comments
                var trivias = root.DescendantTrivia(descendIntoTrivia: true)
                    .Where(t =>
                        t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                        || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                    );

                foreach (var trivia in trivias)
                {
                    stats.TotalComments++;
                    var text = trivia
                        .ToString()
                        .Replace("//", "")
                        .Replace("/*", "")
                        .Replace("*/", "")
                        .Replace("///", "")
                        .Trim();

                    AnalyzeText(text, stats);
                }

                // Also analyze string literals (optional, but can be useful)
                var literals = root.DescendantNodes()
                    .OfType<LiteralExpressionSyntax>()
                    .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression));

                foreach (var literal in literals)
                {
                    var text = literal.Token.ValueText;
                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                    {
                        AnalyzeText(text, stats);
                    }
                }
            }
        }

        return stats;
    }

    private static void AnalyzeText(string text, LanguageStats stats)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return;

        // Count English words (ASCII letters only)
        var englishMatches = EnglishWordPattern.Matches(text);
        foreach (Match match in englishMatches)
        {
            // Filter out very common code keywords
            var word = match.Value.ToLowerInvariant();
            if (!IsCommonKeyword(word))
            {
                stats.EnglishWords++;
            }
        }

        // Count non-ASCII words (Cyrillic, CJK, etc.)
        var nonAsciiMatches = NonAsciiPattern.Matches(text);
        foreach (Match match in nonAsciiMatches)
        {
            var nonAsciiText = match.Value;

            // Each non-ASCII sequence counts as words
            // For CJK, each character is roughly a word
            // For Cyrillic/other alphabets, split by whitespace
            if (IsCjk(nonAsciiText))
            {
                // CJK: each character ≈ word
                stats.NonEnglishWords += nonAsciiText.Length;
            }
            else
            {
                // Cyrillic/other: split by whitespace
                var words = nonAsciiText.Split(
                    separator,
                    StringSplitOptions.RemoveEmptyEntries
                );
                stats.NonEnglishWords += words.Length;
            }
        }
    }

    private static bool IsCommonKeyword(string word)
    {
        // Very common code-related words to ignore
        return word
            is "var"
                or "int"
                or "string"
                or "bool"
                or "void"
                or "null"
                or "true"
                or "false"
                or "get"
                or "set"
                or "if"
                or "else"
                or "for"
                or "while"
                or "return"
                or "new"
                or "this"
                or "base"
                or "using"
                or "namespace"
                or "class"
                or "public"
                or "private"
                or "static"
                or "async"
                or "await"
                or "task"
                or "list"
                or "dict"
                or "array";
    }

    private static bool IsCjk(string text)
    {
        // Check if text contains CJK characters
        return text.Any(c =>
            (c >= 0x4E00 && c <= 0x9FFF)
            || // Chinese
            (c >= 0x3040 && c <= 0x309F)
            || // Hiragana
            (c >= 0x30A0 && c <= 0x30FF)
            || // Katakana
            (c >= 0x3400 && c <= 0x4DBF)
        ); // CJK Ext A
    }
}

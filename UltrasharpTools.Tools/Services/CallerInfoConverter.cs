using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Converts between SerializableCallerInfo (cacheable) and SymbolCallerInfo (Roslyn API)
/// </summary>
internal static class CallerInfoConverter
{
    /// <summary>
    /// Convert SymbolCallerInfo to SerializableCallerInfo for caching
    /// </summary>
    public static SerializableCallerInfo ToSerializable(SymbolCallerInfo callerInfo)
    {
        return new SerializableCallerInfo
        {
            CallingSymbolFqn = callerInfo.CallingSymbol.ToDisplayString(),
            IsDirect = callerInfo.IsDirect,
            CallSiteLocations = callerInfo
                .Locations.Where(loc => loc.IsInSource)
                .Select(ToSerializableLocation)
                .ToList(),
        };
    }

    /// <summary>
    /// Convert Location to SerializableLocation
    /// </summary>
    private static SerializableLocation ToSerializableLocation(Location location)
    {
        var lineSpan = location.GetLineSpan();

        return new SerializableLocation
        {
            FilePath = lineSpan.Path,
            StartLine = lineSpan.StartLinePosition.Line,
            StartCharacter = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndCharacter = lineSpan.EndLinePosition.Character,
            SourceSnippet = TryGetSourceSnippet(location),
        };
    }

    /// <summary>
    /// Try to extract source snippet from location (for debugging/display)
    /// </summary>
    private static string? TryGetSourceSnippet(Location location)
    {
        try
        {
            if (location.SourceTree == null)
            {
                return null;
            }

            var text = location.SourceTree.GetText();
            var snippet = text.GetSubText(location.SourceSpan).ToString();

            // Limit snippet length
            if (snippet.Length > 100)
            {
                snippet = string.Concat(snippet.AsSpan(0, 97), "...");
            }

            return snippet.Trim();
        }
        catch
        {
            return null;
        }
    }
}

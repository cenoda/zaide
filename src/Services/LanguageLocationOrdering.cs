using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Services;

/// <summary>Deterministic ordering for definition multi-result choosers.</summary>
internal static class LanguageLocationOrdering
{
    public static IReadOnlyList<LanguageLocation> Order(IEnumerable<LanguageLocation> locations)
    {
        return locations
            .OrderBy(l => l.FilePath ?? l.DocumentUri, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Range.StartLine)
            .ThenBy(l => l.Range.StartCharacter)
            .ThenBy(l => l.Range.EndLine)
            .ThenBy(l => l.Range.EndCharacter)
            .ThenBy(l => l.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Keeps only locations with a usable local path and non-inverted range.
    /// </summary>
    public static IReadOnlyList<LanguageLocation> FilterValid(IEnumerable<LanguageLocation> locations)
    {
        var result = new List<LanguageLocation>();
        foreach (var location in locations)
        {
            if (string.IsNullOrWhiteSpace(location.DocumentUri))
                continue;

            if (string.IsNullOrWhiteSpace(location.FilePath))
                continue;

            var range = location.Range;
            if (range.StartLine < 0 || range.StartCharacter < 0 ||
                range.EndLine < 0 || range.EndCharacter < 0)
            {
                continue;
            }

            if (range.EndLine < range.StartLine ||
                (range.EndLine == range.StartLine && range.EndCharacter < range.StartCharacter))
            {
                continue;
            }

            result.Add(location);
        }

        return Order(result);
    }
}

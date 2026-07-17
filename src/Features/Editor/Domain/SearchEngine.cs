using System;
using System.Collections.Generic;

namespace Zaide.Features.Editor.Domain;

/// <summary>
/// Pure literal-text search engine. No Avalonia, no View, no ViewModel dependencies.
/// All methods are static and side-effect-free.
/// <para>
/// Matching contract (locked by M3):
/// <list type="bullet">
/// <item>Literal substring only — regex is never used.</item>
/// <item>Case-sensitive by default (<c>caseSensitive = true</c>).</item>
/// <item>Case-sensitive uses <see cref="StringComparison.Ordinal"/>;
/// case-insensitive uses <see cref="StringComparison.OrdinalIgnoreCase"/>.</item>
/// <item>Empty query returns zero matches.</item>
/// <item>Special regex characters (e.g. <c>.</c>, <c>*</c>, <c>\</c>) are treated as literals.</item>
/// </list>
/// </para>
/// </summary>
public static class SearchEngine
{
    /// <summary>
    /// Finds all non-overlapping occurrences of <paramref name="query"/> in <paramref name="text"/>.
    /// Returns an empty list when <paramref name="query"/> is null, empty, or not found.
    /// </summary>
    public static IReadOnlyList<SearchMatch> FindAll(string text, string query, bool caseSensitive = true)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return Array.Empty<SearchMatch>();

        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var matches = new List<SearchMatch>();
        var index = 0;

        while (index <= text.Length - query.Length)
        {
            var found = text.IndexOf(query, index, comparison);
            if (found < 0)
                break;

            matches.Add(new SearchMatch(found, query.Length));
            index = found + query.Length;
        }

        return matches;
    }

    /// <summary>
    /// Computes the next match index after <paramref name="currentIndex"/>.
    /// Wraps to 0 when at the end of the list. Returns -1 when the list is empty.
    /// </summary>
    public static int NextMatchIndex(int currentIndex, int matchCount)
    {
        if (matchCount <= 0) return -1;
        if (currentIndex < 0) return 0;
        return (currentIndex + 1) % matchCount;
    }

    /// <summary>
    /// Computes the previous match index before <paramref name="currentIndex"/>.
    /// Wraps to the last index when at the beginning. Returns -1 when the list is empty.
    /// </summary>
    public static int PreviousMatchIndex(int currentIndex, int matchCount)
    {
        if (matchCount <= 0) return -1;
        if (currentIndex <= 0) return matchCount - 1;
        return currentIndex - 1;
    }

    /// <summary>
    /// Replaces all non-overlapping occurrences of <paramref name="query"/> in <paramref name="text"/>
    /// with <paramref name="replacement"/>. Returns the new text.
    /// Uses the same literal matching as <see cref="FindAll"/>.
    /// </summary>
    public static string ReplaceAll(string text, string query, string replacement, bool caseSensitive = true)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return text;

        var comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var result = new System.Text.StringBuilder(text.Length);
        var index = 0;

        while (index <= text.Length - query.Length)
        {
            var found = text.IndexOf(query, index, comparison);
            if (found < 0)
                break;

            result.Append(text, index, found - index);
            result.Append(replacement);
            index = found + query.Length;
        }

        result.Append(text, index, text.Length - index);
        return result.ToString();
    }
}

/// <summary>
/// A single search match: an offset and length in the document text.
/// </summary>
public readonly record struct SearchMatch(int Offset, int Length);

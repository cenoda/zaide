using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zaide.Features.Terminal.Presentation;

/// <summary>
/// One substring match found inside a <see cref="TerminalSnapshot"/>, expressed
/// in absolute snapshot (row, column) coordinates. Using row/column pairs (rather
/// than raw concatenated-text offsets) keeps the renderer's highlight projection
/// trivially mappable to the on-screen cell grid.
/// </summary>
public readonly struct TerminalSearchMatch : IEquatable<TerminalSearchMatch>
{
    /// <summary>Absolute snapshot row (scrollback rows first, then visible rows).</summary>
    public int Row { get; }

    /// <summary>Inclusive start column of the match on <see cref="Row"/>.</summary>
    public int StartCol { get; }

    /// <summary>Exclusive end column of the match on <see cref="Row"/>.</summary>
    public int EndCol { get; }

    public TerminalSearchMatch(int row, int startCol, int endCol)
    {
        Row = row;
        StartCol = startCol;
        EndCol = endCol;
    }

    public bool Equals(TerminalSearchMatch other) =>
        Row == other.Row && StartCol == other.StartCol && EndCol == other.EndCol;

    public override bool Equals(object? obj) => obj is TerminalSearchMatch other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Row, StartCol, EndCol);
}

/// <summary>
/// Immutable result of a terminal snapshot search: the full ordered list of
/// matches plus the currently active match index. Navigation produces a new
/// instance (next/previous wrap predictably), so the state stays deterministic
/// and unit-testable without hidden mutable counters.
/// </summary>
public sealed class TerminalSearchResult
{
    /// <summary>All matches discovered in the searched snapshot, in row/column order.</summary>
    public IReadOnlyList<TerminalSearchMatch> Matches { get; }

    /// <summary>Index of the active match within <see cref="Matches"/>, or -1 when empty.</summary>
    public int ActiveIndex { get; }

    /// <summary>Whether any matches were found.</summary>
    public bool HasMatches => Matches.Count > 0;

    /// <summary>Total number of matches.</summary>
    public int MatchCount => Matches.Count;

    /// <summary>The active match, or <c>null</c> when there are no matches.</summary>
    public TerminalSearchMatch? ActiveMatch => HasMatches ? Matches[ActiveIndex] : null;

    internal TerminalSearchResult(IReadOnlyList<TerminalSearchMatch> matches, int activeIndex)
    {
        Matches = matches;
        ActiveIndex = matches.Count == 0 ? -1 : activeIndex;
    }

    /// <summary>Singleton empty result (no matches).</summary>
    public static readonly TerminalSearchResult Empty =
        new TerminalSearchResult(Array.Empty<TerminalSearchMatch>(), -1);

    /// <summary>
    /// Returns a result with the active match advanced to the next one, wrapping
    /// from the last match back to the first. No-op when there are no matches.
    /// </summary>
    public TerminalSearchResult MoveToNext()
    {
        if (!HasMatches)
        {
            return this;
        }

        int next = (ActiveIndex + 1) % Matches.Count;
        return new TerminalSearchResult(Matches, next);
    }

    /// <summary>
    /// Returns a result with the active match moved to the previous one, wrapping
    /// from the first match back to the last. No-op when there are no matches.
    /// </summary>
    public TerminalSearchResult MoveToPrevious()
    {
        if (!HasMatches)
        {
            return this;
        }

        int previous = (ActiveIndex - 1 + Matches.Count) % Matches.Count;
        return new TerminalSearchResult(Matches, previous);
    }
}

/// <summary>
/// Plain substring search over a <see cref="TerminalSnapshot"/> — visible rows
/// and retained scrollback rows only. No hidden backend history is consulted.
///
/// Default match mode is <b>case-insensitive</b> substring matching. Case
/// sensitivity is intentionally not exposed in the UI for M3; this keeps the
/// feature narrow and predictable. There is no regex support and no persisted
/// search history.
/// </summary>
public static class TerminalSnapshotSearch
{
    /// <summary>
    /// Finds every occurrence of <paramref name="query"/> in the snapshot.
    /// Returns <see cref="TerminalSearchResult.Empty"/> for a null/empty query or
    /// a null snapshot. The active match of a non-empty result is the first match.
    /// </summary>
    public static TerminalSearchResult Search(TerminalSnapshot snapshot, string query)
    {
        if (snapshot is null || string.IsNullOrEmpty(query))
        {
            return TerminalSearchResult.Empty;
        }

        int queryLength = query.Length;
        var comparison = StringComparison.OrdinalIgnoreCase;
        var matches = new List<TerminalSearchMatch>();

        int totalRows = snapshot.TotalRows;
        for (int row = 0; row < totalRows; row++)
        {
            string line = GetAbsoluteLine(snapshot, row);
            if (line.Length < queryLength)
            {
                continue;
            }

            int index = line.IndexOf(query, comparison);
            while (index >= 0)
            {
                matches.Add(new TerminalSearchMatch(row, index, index + queryLength));

                // Advance at least one column so zero-width queries can never loop,
                // and continue scanning overlapping/adjacent occurrences.
                int nextStart = index + 1;
                if (nextStart + queryLength > line.Length)
                {
                    break;
                }

                index = line.IndexOf(query, nextStart, comparison);
            }
        }

        if (matches.Count == 0)
        {
            return TerminalSearchResult.Empty;
        }

        return new TerminalSearchResult(matches, activeIndex: 0);
    }

    private static string GetAbsoluteLine(TerminalSnapshot snapshot, int absoluteRow)
    {
        if (absoluteRow < snapshot.ScrollbackLines.Count)
        {
            return snapshot.ScrollbackLines[absoluteRow];
        }

        return snapshot.Lines[absoluteRow - snapshot.ScrollbackLines.Count];
    }
}

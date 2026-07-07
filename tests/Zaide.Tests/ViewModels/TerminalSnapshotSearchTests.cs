using System.Linq;
using Zaide.ViewModels;
using Zaide.Views;
using Xunit;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Contract tests for the terminal snapshot search helper introduced in M3.
/// Search is a pure function over a <see cref="TerminalSnapshot"/> (visible
/// rows + retained scrollback only) and is deterministic and unit-testable
/// without any Avalonia platform.
/// </summary>
public class TerminalSnapshotSearchTests
{
    // ── fixture builders ────────────────────────────────────────────

    /// <summary>
    /// Builds a snapshot with the given visible/scrollback lines. Cells are
    /// generated from the line text (space-padded to the widest line) so search
    /// matches map cleanly to absolute row/column coordinates.
    /// </summary>
    private static TerminalSnapshot BuildSnapshot(string[] visible, string[] scrollback)
    {
        int cols = 1;
        foreach (var l in visible) cols = System.Math.Max(cols, l.Length);
        foreach (var l in scrollback) cols = System.Math.Max(cols, l.Length);

        var cells = new TerminalCell[visible.Length * cols];
        for (int r = 0; r < visible.Length; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                char ch = c < visible[r].Length ? visible[r][c] : ' ';
                cells[r * cols + c] = new TerminalCell(ch, -1, -1, false, false);
            }
        }

        var sbCells = new TerminalCell[scrollback.Length * cols];
        for (int r = 0; r < scrollback.Length; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                char ch = c < scrollback[r].Length ? scrollback[r][c] : ' ';
                sbCells[r * cols + c] = new TerminalCell(ch, -1, -1, false, false);
            }
        }

        return new TerminalSnapshot(cols, visible.Length, visible, cells, scrollback, sbCells);
    }

    /// <summary>
    /// Snapshot with two scrollback rows and three visible rows, all containing
    /// at least one "hello" occurrence so matches span both regions.
    /// </summary>
    private static TerminalSnapshot BuildHelloSnapshot() =>
        BuildSnapshot(
            visible: new[] { "hello world", "foo bar baz", "hello again" },
            scrollback: new[] { "old hello", "nomatch here" });

    // ── required tests ─────────────────────────────────────────────

    [Fact]
    public void SearchSnapshot_FindsMatches_InVisibleRows()
    {
        var snapshot = BuildHelloSnapshot();
        var result = TerminalSnapshotSearch.Search(snapshot, "hello");

        Assert.True(result.HasMatches);

        // Visible rows are absolute rows 2..4 (2 scrollback rows precede them).
        Assert.Contains(result.Matches, m => m.Row == 2 && m.StartCol == 0); // "hello world"
        Assert.Contains(result.Matches, m => m.Row == 4 && m.StartCol == 0); // "hello again"
    }

    [Fact]
    public void SearchSnapshot_FindsMatches_InScrollbackRows()
    {
        var snapshot = BuildHelloSnapshot();
        var result = TerminalSnapshotSearch.Search(snapshot, "hello");

        Assert.True(result.HasMatches);

        // "old hello" is the first scrollback row (absolute row 0), at col 4.
        var scrollbackMatch = Assert.Single(result.Matches, m => m.Row == 0);
        Assert.Equal(0, scrollbackMatch.Row);
        Assert.Equal(4, scrollbackMatch.StartCol);
        Assert.Equal(9, scrollbackMatch.EndCol); // exclusive end: "hello" length 5
    }

    [Fact]
    public void SearchSnapshot_NextPreviousWrapsPredictably()
    {
        var snapshot = BuildHelloSnapshot();
        var result = TerminalSnapshotSearch.Search(snapshot, "hello");

        Assert.Equal(3, result.MatchCount);
        Assert.Equal(0, result.ActiveIndex);

        var afterNext1 = result.MoveToNext();
        Assert.Equal(1, afterNext1.ActiveIndex);

        var afterNext2 = afterNext1.MoveToNext();
        Assert.Equal(2, afterNext2.ActiveIndex);

        // Wraps from last back to first.
        var afterNext3 = afterNext2.MoveToNext();
        Assert.Equal(0, afterNext3.ActiveIndex);

        // Previous from the first wraps to the last.
        var afterPrev = afterNext3.MoveToPrevious();
        Assert.Equal(2, afterPrev.ActiveIndex);

        // Underlying match list is unchanged by navigation.
        Assert.Equal(3, afterPrev.MatchCount);
        Assert.Equal(result.Matches, afterPrev.Matches);
    }

    [Fact]
    public void SearchSnapshot_NoMatches_ClearsActiveMatch()
    {
        var snapshot = BuildHelloSnapshot();
        var result = TerminalSnapshotSearch.Search(snapshot, "zzz-not-present");

        Assert.False(result.HasMatches);
        Assert.Equal(-1, result.ActiveIndex);
        Assert.Null(result.ActiveMatch);
        Assert.Equal(TerminalSearchResult.Empty, result);
    }

    [Fact]
    public void SearchSnapshot_DoesNotExposeMainBuffer_WhenAlternateScreenActive()
    {
        // The renderer is the single gate: even if a result covering main-buffer
        // (scrollback) matches is pushed in, the renderer must suppress it while
        // a full-screen TUI owns the terminal, so no hidden content is shown.
        var snapshot = BuildHelloSnapshot();
        var result = TerminalSnapshotSearch.Search(snapshot, "hello");

        var control = new TerminalRenderControl
        {
            Snapshot = snapshot,
            SearchResult = result
        };

        // Normal main-buffer state: the result is surfaced for rendering.
        Assert.Same(result, control.EffectiveSearchResult);

        // Full-screen TUI active: search is suppressed, nothing leaks.
        control.IsAlternateScreenActive = true;
        Assert.Null(control.EffectiveSearchResult);
    }

    // ── focused helper tests ───────────────────────────────────────

    [Fact]
    public void SearchSnapshot_MapsRowAndColumn_ForOverlappingOccurrences()
    {
        // "haha" contains "ha" at cols 0 and 2 — both must be reported.
        var snapshot = BuildSnapshot(visible: new[] { "haha" }, scrollback: System.Array.Empty<string>());
        var result = TerminalSnapshotSearch.Search(snapshot, "ha");

        Assert.Equal(2, result.MatchCount);
        Assert.Equal((0, 0, 2), (result.Matches[0].Row, result.Matches[0].StartCol, result.Matches[0].EndCol));
        Assert.Equal((0, 2, 4), (result.Matches[1].Row, result.Matches[1].StartCol, result.Matches[1].EndCol));
    }

    [Fact]
    public void SearchSnapshot_IsCaseInsensitive_ByDefault()
    {
        var snapshot = BuildSnapshot(visible: new[] { "Hello World" }, scrollback: System.Array.Empty<string>());
        var result = TerminalSnapshotSearch.Search(snapshot, "hello");

        Assert.True(result.HasMatches);
        Assert.Equal((0, 0, 5), (result.Matches[0].Row, result.Matches[0].StartCol, result.Matches[0].EndCol));
    }

    [Fact]
    public void SearchSnapshot_EmptyQuery_ReturnsEmptyResult()
    {
        var snapshot = BuildHelloSnapshot();
        Assert.Equal(TerminalSearchResult.Empty, TerminalSnapshotSearch.Search(snapshot, ""));
        Assert.Equal(TerminalSearchResult.Empty, TerminalSnapshotSearch.Search(snapshot, "   "));
        Assert.Equal(TerminalSearchResult.Empty, TerminalSnapshotSearch.Search(null!, "hello"));
    }

    [Fact]
    public void RenderControl_BringSearchMatchIntoView_MovesViewportToMatch()
    {
        // 1 visible row + 10 scrollback rows. The match sits far above the
        // default live-bottom viewport.
        var control = new TerminalRenderControl();
        var snapshot = BuildScrollbackSnapshot(rows: 1, cols: 4, scrollbackCount: 10);

        // Inject a synthetic result whose match is in scrollback (absolute row 2).
        var match = new TerminalSearchMatch(2, 0, 2);
        var result = new TerminalSearchResult(new[] { match }, activeIndex: 0);
        control.Snapshot = snapshot;
        control.SearchResult = result;

        // Default viewport is pinned to the live bottom (row 10, off-screen for the match).
        Assert.Equal(10, control.GetViewportTop(snapshot));

        control.BringSearchMatchIntoView(match);

        // Viewport must have moved so the match row is now visible.
        int top = control.GetViewportTop(snapshot);
        Assert.True(match.Row >= top && match.Row < top + snapshot.Rows,
            "active match row should be within the viewport after bring-into-view");
    }

    [Fact]
    public void RenderControl_BringSearchMatchIntoView_Noop_WhenAlreadyVisible()
    {
        var control = new TerminalRenderControl();
        var snapshot = BuildScrollbackSnapshot(rows: 10, cols: 4, scrollbackCount: 20);
        control.Snapshot = snapshot;

        int topBefore = control.GetViewportTop(snapshot);
        var visibleMatch = new TerminalSearchMatch(topBefore, 0, 2);
        control.BringSearchMatchIntoView(visibleMatch);

        Assert.Equal(topBefore, control.GetViewportTop(snapshot));
    }

    // Replicates the scrollback fixture used by the render-control tests so this
    // file stays self-contained for viewport-targeting coverage.
    private static TerminalSnapshot BuildScrollbackSnapshot(int rows, int cols, int scrollbackCount)
    {
        var lines = new string[rows];
        var cells = new TerminalCell[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            lines[r] = new string('a', cols);
            for (int c = 0; c < cols; c++)
            {
                cells[r * cols + c] = new TerminalCell('a', -1, -1, false, false);
            }
        }

        var sb = new string[scrollbackCount];
        var sbc = new TerminalCell[scrollbackCount * cols];
        for (int r = 0; r < scrollbackCount; r++)
        {
            sb[r] = new string('b', cols);
            for (int c = 0; c < cols; c++)
            {
                sbc[r * cols + c] = new TerminalCell('b', -1, -1, false, false);
            }
        }

        return new TerminalSnapshot(cols, rows, lines, cells, sb, sbc);
    }
}

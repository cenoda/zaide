using System;
using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TerminalScreen"/> — the M2 screen-buffer model.
/// Covers write/erase/scroll/resize/cursor/SGR behavior without any
/// Avalonia or PTY dependency.
/// </summary>
public class TerminalScreenTests
{
    // ── write & wrap ──────────────────────────────────────────────

    [Fact]
    public void Write_PlacesCharacterAtCursorAndAdvances()
    {
        var screen = new TerminalScreen(5, 3);
        screen.Write('A');
        Assert.Equal('A', screen.GetCell(0, 0).Char);
        Assert.Equal(1, screen.CursorCol);
        Assert.Equal(0, screen.CursorRow);
    }

    [Fact]
    public void Write_WrapsAtRightEdge()
    {
        var screen = new TerminalScreen(3, 3);
        screen.WriteText("abc");
        Assert.Equal('a', screen.GetCell(0, 0).Char);
        Assert.Equal('b', screen.GetCell(0, 1).Char);
        Assert.Equal('c', screen.GetCell(0, 2).Char);
        Assert.Equal(0, screen.CursorCol);
        Assert.Equal(1, screen.CursorRow);

        screen.Write('d');
        Assert.Equal('d', screen.GetCell(1, 0).Char);
        Assert.Equal(1, screen.CursorCol);
        Assert.Equal(1, screen.CursorRow);
    }

    [Fact]
    public void Write_WrapsAndScrollsAtBottom()
    {
        var screen = new TerminalScreen(3, 2);
        screen.WriteText("abc");  // fills row 0, wraps to row 1
        screen.WriteText("de");   // row 1 col 0..1
        Assert.Equal(2, screen.CursorCol);
        Assert.Equal(1, screen.CursorRow);

        // One more char triggers wrap + scroll
        screen.Write('f');
        // Row 0 was "abc", row 1 was "de ". Write 'f' at (1,2),
        // wraps to (2,0) → scroll. Old row 0 discarded, old row 1 → new row 0.
        Assert.Equal('d', screen.GetCell(0, 0).Char);
        Assert.Equal('e', screen.GetCell(0, 1).Char);
        Assert.Equal('f', screen.GetCell(0, 2).Char);
        Assert.Equal(' ', screen.GetCell(1, 0).Char);
        Assert.Equal(' ', screen.GetCell(1, 1).Char);
        Assert.Equal(' ', screen.GetCell(1, 2).Char);
        Assert.Equal(1, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
    }

    [Fact]
    public void WriteText_WritesMultipleCharacters()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hello");
        Assert.Equal("hello     ", screen.GetLine(0).ToString());
        Assert.Equal(5, screen.CursorCol);
        Assert.Equal(0, screen.CursorRow);
    }

    // ── newline scroll at bottom ──────────────────────────────────

    [Fact]
    public void LineFeed_AtBottom_Scrolls()
    {
        var screen = new TerminalScreen(5, 3);
        screen.WriteText("row0");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("row1");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("row2");
        screen.ExecuteC0(AnsiC0Control.LineFeed);

        Assert.Equal("row1 ", screen.GetLine(0).ToString());
        Assert.Equal("row2 ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
    }

    // ── carriage return to column 0 ──────────────────────────────

    [Fact]
    public void CarriageReturn_MovesCursorToColumnZero()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("12345");
        Assert.Equal(5, screen.CursorCol);
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        Assert.Equal(0, screen.CursorCol);
    }

    // ── cursor motion clamps at edges ─────────────────────────────

    [Fact]
    public void CursorUp_ClampsAtTop()
    {
        var screen = new TerminalScreen(10, 3);
        screen.CursorDown(1);
        Assert.Equal(1, screen.CursorRow);
        screen.CursorUp(5);
        Assert.Equal(0, screen.CursorRow);
    }

    [Fact]
    public void CursorDown_ClampsAtBottom()
    {
        var screen = new TerminalScreen(10, 3);
        screen.CursorDown(10);
        Assert.Equal(2, screen.CursorRow);
    }

    [Fact]
    public void CursorForward_ClampsAtRightEdge()
    {
        var screen = new TerminalScreen(5, 3);
        screen.CursorForward(10);
        Assert.Equal(4, screen.CursorCol);
    }

    [Fact]
    public void CursorBack_ClampsAtLeftEdge()
    {
        var screen = new TerminalScreen(5, 3);
        screen.CursorForward(3);
        screen.CursorBack(10);
        Assert.Equal(0, screen.CursorCol);
    }

    [Fact]
    public void CursorZeroOrNegativeMotion_IsNoOp()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hi");
        screen.CursorUp(0);
        screen.CursorDown(0);
        screen.CursorForward(0);
        screen.CursorBack(0);
        Assert.Equal(2, screen.CursorCol);
        Assert.Equal(0, screen.CursorRow);

        screen.CursorUp(-1);
        screen.CursorDown(-1);
        screen.CursorForward(-1);
        screen.CursorBack(-1);
        Assert.Equal(2, screen.CursorCol);
        Assert.Equal(0, screen.CursorRow);
    }

    // ── 1-based cursor position ──────────────────────────────────

    [Fact]
    public void CursorPosition_SetsCorrectZeroBasedCell()
    {
        var screen = new TerminalScreen(10, 5);
        screen.CursorPosition(3, 5);
        Assert.Equal(2, screen.CursorRow);
        Assert.Equal(4, screen.CursorCol);
    }

    [Fact]
    public void CursorPosition_DefaultsToOne()
    {
        var screen = new TerminalScreen(10, 5);
        Assert.Equal(0, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
        screen.CursorPosition(1, 1);
        Assert.Equal(0, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
    }

    [Fact]
    public void CursorPosition_ClampsToBounds()
    {
        var screen = new TerminalScreen(5, 3);
        screen.CursorPosition(999, 999);
        Assert.Equal(2, screen.CursorRow);
        Assert.Equal(4, screen.CursorCol);
        screen.CursorPosition(0, 0);
        Assert.Equal(0, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
    }

    // ── erase display ─────────────────────────────────────────────

    [Fact]
    public void EraseDisplay_Param2_ClearsAllCells()
    {
        var screen = new TerminalScreen(5, 3);
        screen.WriteText("AAAAA");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("BBBBB");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("CCCCC");

        screen.EraseDisplay(2);
        Assert.Equal("     ", screen.GetLine(0).ToString());
        Assert.Equal("     ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
    }

    [Fact]
    public void EraseDisplay_Param0_ErasesFromCursorToEnd()
    {
        var screen = new TerminalScreen(5, 3);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 5; c++)
                screen.Write('X');

        screen.CursorPosition(2, 3);
        screen.EraseDisplay(0);
        Assert.Equal("XXXXX", screen.GetLine(0).ToString());
        Assert.Equal("XX   ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
    }

    [Fact]
    public void EraseDisplay_Param1_ErasesFromStartToCursor()
    {
        var screen = new TerminalScreen(5, 3);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 5; c++)
                screen.Write('X');

        screen.CursorPosition(2, 3);
        screen.EraseDisplay(1);
        // After loop writes, scroll cleared row 2; erase(1) clears rows 0 and the
        // start of row 1 up to cursor.
        Assert.Equal("     ", screen.GetLine(0).ToString());
        Assert.Equal("   XX", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
    }

    [Fact]
    public void EraseDisplay_Param3_ClearsVisibleCellsOnly()
    {
        var screen = new TerminalScreen(5, 3);
        screen.WriteText("hello");
        screen.EraseDisplay(3);
        Assert.Equal("     ", screen.GetLine(0).ToString());
        Assert.Equal("     ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
    }

    [Fact]
    public void EraseDisplay_Param3_ClearsScrollback()
    {
        var screen = new TerminalScreen(3, 2);
        screen.WriteText("abc");
        screen.WriteText("def");
        screen.WriteText("ghi");

        Assert.True(screen.ScrollbackRowCount > 0);

        screen.EraseDisplay(3);

        Assert.Equal(0, screen.ScrollbackRowCount);
    }

    // ── erase line ─────────────────────────────────────────────────

    [Fact]
    public void EraseLine_Param2_ClearsEntireCurrentLine()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hello");
        screen.EraseLine(2);
        Assert.Equal("          ", screen.GetLine(0).ToString());
    }

    [Fact]
    public void EraseLine_Param0_ErasesFromCursorToEndOfLine()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hello");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.WriteText("hi");
        screen.EraseLine(0);
        Assert.Equal("hi        ", screen.GetLine(0).ToString());
    }

    [Fact]
    public void EraseLine_Param1_ErasesFromStartOfLineToCursor()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hello");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.WriteText("hi");
        screen.EraseLine(1);
        // "hello" overwritten with "hi" at cols 0-1 → "hillo";
        // erase cols 0..2 inclusive → "   lo"
        Assert.Equal("   lo     ", screen.GetLine(0).ToString());
    }

    // ── SGR attribute application and reset ───────────────────────

    [Fact]
    public void SetSgr_Reset_RestoresDefaultAttributes()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 31 });
        screen.SetSgr(new[] { 0 });
        screen.Write('X');
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Background);
        Assert.False(screen.GetCell(0, 0).Attribute.Bold);
        Assert.False(screen.GetCell(0, 0).Attribute.Inverse);
    }

    [Fact]
    public void SetSgr_Bold_AppliesBoldToSubsequentWrites()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 1 });
        screen.Write('X');
        Assert.True(screen.GetCell(0, 0).Attribute.Bold);
    }

    [Fact]
    public void SetSgr_Inverse_AppliesInverseToSubsequentWrites()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 7 });
        screen.Write('X');
        Assert.True(screen.GetCell(0, 0).Attribute.Inverse);
    }

    [Fact]
    public void SetSgr_StandardForeground_AppliesCorrectColor()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 31 });
        screen.Write('R');
        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Foreground);

        screen.SetSgr(new[] { 36 });
        screen.Write('C');
        Assert.Equal(6, screen.GetCell(0, 1).Attribute.Foreground);
    }

    [Fact]
    public void SetSgr_StandardBackground_AppliesCorrectColor()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 41 });
        screen.Write('X');
        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Background);
    }

    [Fact]
    public void SetSgr_BrightForeground_AppliesColors8To15()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 90 });
        screen.Write('X');
        Assert.Equal(8, screen.GetCell(0, 0).Attribute.Foreground);

        screen.SetSgr(new[] { 97 });
        screen.Write('Y');
        Assert.Equal(15, screen.GetCell(0, 1).Attribute.Foreground);
    }

    [Fact]
    public void SetSgr_BrightBackground_AppliesColors8To15()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 100 });
        screen.Write('X');
        Assert.Equal(8, screen.GetCell(0, 0).Attribute.Background);

        screen.SetSgr(new[] { 107 });
        screen.Write('Y');
        Assert.Equal(15, screen.GetCell(0, 1).Attribute.Background);
    }

    [Fact]
    public void SetSgr_DefaultForeground_ResetsToMinusOne()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 31 });
        screen.SetSgr(new[] { 39 });
        screen.Write('X');
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);
    }

    [Fact]
    public void SetSgr_DefaultBackground_ResetsToMinusOne()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 41 });
        screen.SetSgr(new[] { 49 });
        screen.Write('X');
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Background);
    }

    [Fact]
    public void SetSgr_BoldRed_AppliesBoth()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 1, 31 });
        screen.Write('X');
        Assert.True(screen.GetCell(0, 0).Attribute.Bold);
        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Foreground);
    }

    [Fact]
    public void SetSgr_256Color_IsConsumedAndIgnored()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 31, 38, 5, 196 });
        screen.Write('X');
        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Foreground);

        screen.SetSgr(new[] { 44, 48, 5, 21 });
        screen.Write('Y');
        Assert.Equal(4, screen.GetCell(0, 1).Attribute.Background);
    }

    [Fact]
    public void SetSgr_UnsupportedParameters_AreSilentlyIgnored()
    {
        var screen = new TerminalScreen(10, 3);
        screen.SetSgr(new[] { 99, 200 });
        screen.Write('X');
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Background);
        Assert.False(screen.GetCell(0, 0).Attribute.Bold);
        Assert.False(screen.GetCell(0, 0).Attribute.Inverse);
    }

    [Fact]
    public void Sgr_AttributeChangesAffectSubsequentWritesOnly()
    {
        var screen = new TerminalScreen(10, 3);
        screen.Write('A');
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);

        screen.SetSgr(new[] { 31 });
        screen.Write('B');
        Assert.Equal(1, screen.GetCell(0, 1).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);
    }

    // ── scroll behavior ───────────────────────────────────────────

    [Fact]
    public void Scroll_PushesContentUpAndClearsBottomRow()
    {
        var screen = new TerminalScreen(5, 3);
        screen.WriteText("row0");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("row1");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("row2");

        screen.Scroll();
        Assert.Equal("row1 ", screen.GetLine(0).ToString());
        Assert.Equal("row2 ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
        Assert.Equal(2, screen.CursorRow);
    }

    [Fact]
    public void Scroll_RetainsScrolledRowInScrollback()
    {
        var screen = new TerminalScreen(3, 2);
        screen.WriteText("abc");
        screen.WriteText("def");
        screen.WriteText("ghi");

        Assert.Equal(2, screen.ScrollbackRowCount);
        Assert.Equal('a', screen.GetScrollbackRow(0)[0].Char);
        Assert.Equal('b', screen.GetScrollbackRow(0)[1].Char);
        Assert.Equal('c', screen.GetScrollbackRow(0)[2].Char);
        Assert.Equal('d', screen.GetScrollbackRow(1)[0].Char);
        Assert.Equal('e', screen.GetScrollbackRow(1)[1].Char);
        Assert.Equal('f', screen.GetScrollbackRow(1)[2].Char);
    }

    // ── backspace does not cross row boundary ─────────────────────

    [Fact]
    public void Backspace_StopsAtColumnZero()
    {
        var screen = new TerminalScreen(5, 3);
        screen.ExecuteC0(AnsiC0Control.Backspace);
        Assert.Equal(0, screen.CursorCol);
        Assert.Equal(0, screen.CursorRow);
    }

    [Fact]
    public void Backspace_DoesNotCrossIntoPreviousRow()
    {
        var screen = new TerminalScreen(3, 3);
        screen.WriteText("abc");
        // wraps to (1,0)
        Assert.Equal(1, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
        screen.ExecuteC0(AnsiC0Control.Backspace);
        Assert.Equal(0, screen.CursorCol);
        Assert.Equal(1, screen.CursorRow);
    }

    // ── tab goes to next 8-column stop ─────────────────────────────

    [Fact]
    public void Tab_AdvancesToNextMultipleOfEight()
    {
        var screen = new TerminalScreen(32, 3);
        screen.ExecuteC0(AnsiC0Control.Tab);
        Assert.Equal(8, screen.CursorCol);

        screen.ExecuteC0(AnsiC0Control.Tab);
        Assert.Equal(16, screen.CursorCol);

        screen.CursorPosition(1, 4);
        screen.ExecuteC0(AnsiC0Control.Tab);
        Assert.Equal(8, screen.CursorCol);
    }

    [Fact]
    public void Tab_AtEndOfLine_StaysAtLastColumn()
    {
        var screen = new TerminalScreen(10, 3);
        screen.CursorForward(9);
        screen.ExecuteC0(AnsiC0Control.Tab);
        Assert.Equal(9, screen.CursorCol);
    }

    // ── overwrite ordering: AB + back + C → AC ────────────────────

    [Fact]
    public void Overwrite_BackspaceThenWrite_ReplacesPreviousCharacter()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("AB");
        Assert.Equal("AB        ", screen.GetLine(0).ToString());
        // Cursor at col 2. Backspace moves to col 1 (the 'B' position).
        screen.ExecuteC0(AnsiC0Control.Backspace);
        // Write 'C' at col 1 → "AC".
        screen.Write('C');
        Assert.Equal("AC        ", screen.GetLine(0).ToString());
    }

    // ── resize larger preserves top-left and fills new cells ─────

    [Fact]
    public void Resize_Larger_PreservesTopLeftAndFillsWithSpaces()
    {
        var screen = new TerminalScreen(3, 2);
        screen.WriteText("AB");
        screen.ExecuteC0(AnsiC0Control.CarriageReturn);
        screen.ExecuteC0(AnsiC0Control.LineFeed);
        screen.WriteText("CD");

        screen.Resize(5, 4);

        Assert.Equal("AB   ", screen.GetLine(0).ToString());
        Assert.Equal("CD   ", screen.GetLine(1).ToString());
        Assert.Equal("     ", screen.GetLine(2).ToString());
        Assert.Equal("     ", screen.GetLine(3).ToString());
        Assert.Equal(5, screen.Columns);
        Assert.Equal(4, screen.Rows);
    }

    [Fact]
    public void Resize_Larger_FillsNewCellsWithDefaultAttributes()
    {
        var screen = new TerminalScreen(2, 2);
        screen.SetSgr(new[] { 31 });
        screen.WriteText("AB");

        screen.Resize(4, 2);

        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Foreground);
        Assert.Equal(1, screen.GetCell(0, 1).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 2).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 3).Attribute.Foreground);
    }

    // ── resize smaller discards overflow and clamps cursor ────────

    [Fact]
    public void Resize_Smaller_DiscardsOverflowAndClampsCursor()
    {
        var screen = new TerminalScreen(5, 5);
        screen.WriteText("HELLO");
        screen.CursorPosition(5, 5);

        screen.Resize(3, 2);

        Assert.Equal(3, screen.Columns);
        Assert.Equal(2, screen.Rows);
        Assert.Equal("HEL", screen.GetLine(0).ToString());
        Assert.Equal("   ", screen.GetLine(1).ToString());
        Assert.Equal(1, screen.CursorRow);
        Assert.Equal(2, screen.CursorCol);
    }

    [Fact]
    public void Resize_Smaller_CursorAtEdgeClamps()
    {
        var screen = new TerminalScreen(10, 10);
        screen.CursorPosition(10, 10);
        screen.Resize(3, 3);

        Assert.Equal(3, screen.Columns);
        Assert.Equal(3, screen.Rows);
        Assert.Equal(2, screen.CursorRow);
        Assert.Equal(2, screen.CursorCol);
    }

    [Fact]
    public void Resize_RejectsNonPositiveDimensions()
    {
        var screen = new TerminalScreen(10, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Resize(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Resize(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Resize(-1, 10));
    }

    // ── default constructor ───────────────────────────────────────

    [Fact]
    public void Constructor_CreatesDefault80x24Grid()
    {
        var screen = new TerminalScreen();
        Assert.Equal(80, screen.Columns);
        Assert.Equal(24, screen.Rows);
    }

    [Fact]
    public void Constructor_FillsWithSpaces()
    {
        var screen = new TerminalScreen(5, 3);
        for (int r = 0; r < 3; r++)
            Assert.Equal("     ", screen.GetLine(r).ToString());
    }

    // ── regression: defaulted cursor actions with zero-equivalent ─

    [Fact]
    public void CursorMotion_DefaultCountOne_MovesExactlyOne()
    {
        var screen = new TerminalScreen(10, 5);
        screen.CursorPosition(3, 3);

        screen.CursorUp(1);
        Assert.Equal(1, screen.CursorRow);

        screen.CursorDown(1);
        Assert.Equal(2, screen.CursorRow);

        screen.CursorForward(1);
        Assert.Equal(3, screen.CursorCol);

        screen.CursorBack(1);
        Assert.Equal(2, screen.CursorCol);
    }

    [Fact]
    public void CursorPosition_HomePosition_GoesToTopLeft()
    {
        var screen = new TerminalScreen(10, 5);
        screen.WriteText("hello");
        screen.CursorPosition(1, 1);
        Assert.Equal(0, screen.CursorRow);
        Assert.Equal(0, screen.CursorCol);
    }

    // ── regression: attribute changes affect subsequent writes only ─

    [Fact]
    public void SetSgr_MidLine_OnlyAffectsCellsWrittenAfter()
    {
        var screen = new TerminalScreen(10, 3);

        screen.Write('A');
        screen.Write('B');
        screen.SetSgr(new[] { 31 });
        screen.Write('C');

        Assert.Equal(-1, screen.GetCell(0, 0).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 1).Attribute.Foreground);
        Assert.Equal(1, screen.GetCell(0, 2).Attribute.Foreground);
    }

    [Fact]
    public void SetSgr_ResetMidLine_ClearsOnlySubsequentAttributes()
    {
        var screen = new TerminalScreen(10, 3);

        screen.SetSgr(new[] { 31 });
        screen.Write('A');
        screen.Write('B');
        screen.SetSgr(new[] { 0 });
        screen.Write('C');

        Assert.Equal(1, screen.GetCell(0, 0).Attribute.Foreground);
        Assert.Equal(1, screen.GetCell(0, 1).Attribute.Foreground);
        Assert.Equal(-1, screen.GetCell(0, 2).Attribute.Foreground);
    }

    // ── Bell is a no-op ────────────────────────────────────────────

    [Fact]
    public void Bell_DoesNotChangeCursorOrContent()
    {
        var screen = new TerminalScreen(10, 3);
        screen.WriteText("hello");
        int colBefore = screen.CursorCol;
        int rowBefore = screen.CursorRow;

        screen.ExecuteC0(AnsiC0Control.Bell);

        Assert.Equal(colBefore, screen.CursorCol);
        Assert.Equal(rowBefore, screen.CursorRow);
        Assert.Equal("hello     ", screen.GetLine(0).ToString());
    }

    // ── error on unsupported C0 ───────────────────────────────────

    [Fact]
    public void ExecuteC0_UnsupportedControl_Throws()
    {
        var screen = new TerminalScreen(10, 3);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => screen.ExecuteC0((AnsiC0Control)999));
    }
}

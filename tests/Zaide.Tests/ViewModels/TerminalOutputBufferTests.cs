using System;
using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TerminalOutputBuffer"/> — the M4 raw-output
/// control-character subset (carriage return, backspace, linefeed) plus
/// front-trim on overflow. Pure; no Avalonia or PTY involved.
/// </summary>
public class TerminalOutputBufferTests
{
    private static TerminalOutputBuffer New(int max = 200_000) => new(max);

    // --- plain text (behavior unchanged from the old StringBuilder) ---

    [Fact]
    public void Append_PlainText_Accumulates()
    {
        var buf = New();
        buf.Append("hello");
        buf.Append(" world");
        Assert.Equal("hello world", buf.Text);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buf = New();
        buf.Append("something");
        buf.Clear();
        Assert.Equal(string.Empty, buf.Text);
    }

    // --- carriage return overwrite ---

    [Fact]
    public void CarriageReturn_OverwritesCurrentLine()
    {
        var buf = New();
        buf.Append("Progress: 0%\rProgress: 50%");
        Assert.Equal("Progress: 50%", buf.Text);
    }

    [Fact]
    public void CarriageReturn_LeavesTrailingCharsOfLongerOldLine()
    {
        // Real terminal behavior: \r does not erase; shorter new text leaves
        // the tail of the previous (longer) line in place.
        var buf = New();
        buf.Append("Loading...\rDone");
        Assert.Equal("Doneing...", buf.Text);
    }

    [Fact]
    public void CarriageReturn_OnlyAffectsCurrentLine()
    {
        var buf = New();
        buf.Append("line1\r\nabc\rXY");
        Assert.Equal("line1\nXYc", buf.Text);
    }

    [Fact]
    public void CarriageReturn_SplitAcrossChunks_PreservesCursor()
    {
        var buf = New();
        buf.Append("12345\r");   // cursor parked at line start
        buf.Append("ab");        // overwrites first two chars
        Assert.Equal("ab345", buf.Text);
    }

    // --- backspace ---

    [Fact]
    public void Backspace_ThenChar_Overwrites()
    {
        var buf = New();
        buf.Append("cat\bX");
        Assert.Equal("caX", buf.Text);
    }

    [Fact]
    public void Backspace_SpaceBackspace_ErasesOneChar()
    {
        // Common shell erase sequence: \b \b
        var buf = New();
        buf.Append("ab\b \b");
        Assert.Equal("a ", buf.Text);
    }

    [Fact]
    public void Backspace_StopsAtLineStart()
    {
        var buf = New();
        buf.Append("ab\b\b\b\bZ"); // more backspaces than chars
        Assert.Equal("Zb", buf.Text);
    }

    [Fact]
    public void Backspace_DoesNotCrossIntoPreviousLine()
    {
        var buf = New();
        buf.Append("hello\nx\b\b\bY");
        Assert.Equal("hello\nY", buf.Text);
    }

    // --- linefeed / crlf ---

    [Fact]
    public void CrLf_ProducesSingleNewline()
    {
        var buf = New();
        buf.Append("a\r\nb");
        Assert.Equal("a\nb", buf.Text);
    }

    [Fact]
    public void Newline_AfterOverwrite_StartsFreshLine()
    {
        var buf = New();
        buf.Append("done\rOK\nnext");
        Assert.Equal("OKne\nnext", buf.Text);
    }

    // --- escape sequences remain verbatim (deferred ANSI) ---

    [Fact]
    public void EscapeSequences_AreLeftVerbatim()
    {
        var buf = New();
        buf.Append("\x1B[31mred\x1B[0m");
        Assert.Equal("\x1B[31mred\x1B[0m", buf.Text);
    }

    // --- trimming ---

    [Fact]
    public void Trim_RemovesOldestCharsWhenOverCapacity()
    {
        var buf = New(max: 10);
        buf.Append("0123456789ABCDE");
        Assert.Equal("56789ABCDE", buf.Text);
        Assert.Equal(10, buf.Length);
    }

    [Fact]
    public void Trim_KeepsOverwriteCorrectAfterTrim()
    {
        var buf = New(max: 5);
        buf.Append("abcdefg"); // trims to "cdefg"
        buf.Append("\rXY");    // cursor to line start, overwrite first two
        Assert.Equal("XYefg", buf.Text);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalOutputBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalOutputBuffer(-1));
    }
}

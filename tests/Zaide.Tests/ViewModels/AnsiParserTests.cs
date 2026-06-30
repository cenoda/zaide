using System.Linq;
using Xunit;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="AnsiParser"/>. Verifies the pure ANSI/CSI parser
/// contract for Phase 3.6 M1 without involving any screen buffer or UI code.
/// </summary>
public class AnsiParserTests
{
    [Fact]
    public void Parse_PlainText_EmitsSinglePrintAction()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("hello world");

        var print = Assert.Single(actions);
        var printAction = Assert.IsType<PrintAction>(print);
        Assert.Equal("hello world", printAction.Text);
    }

    [Fact]
    public void Parse_SgrRed_EmitsCsiDispatch()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[31m");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 31 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_EraseDisplay_EmitsCsiDispatch()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[2J");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 2 }, dispatch.Parameters);
        Assert.Equal('J', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_CursorUp_DefaultsToOne()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[A");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 1 }, dispatch.Parameters);
        Assert.Equal('A', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_CursorPosition_EmitsRowAndColumn()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[3;5H");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 3, 5 }, dispatch.Parameters);
        Assert.Equal('H', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_SgrReset_EmitsResetParameter()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[0m");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 0 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_SgrBoldRed_EmitsBothParameters()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[1;31m");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 1, 31 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_PrivateSequence_DropsUnsupportedSequence()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[?25h");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_SplitSequenceAcrossCalls_CompletesOnSecondCall()
    {
        var parser = new AnsiParser();

        var first = parser.Parse("\x1B");
        var second = parser.Parse("[31m");

        Assert.Empty(first);
        var csi = Assert.Single(second);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 31 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_SplitSequenceAtFinalByteBoundary_CompletesOnSecondCall()
    {
        var parser = new AnsiParser();

        var first = parser.Parse("\x1B[31");
        var second = parser.Parse("m");

        Assert.Empty(first);
        var csi = Assert.Single(second);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 31 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_SplitOscAcrossCalls_DropsSequenceWhenTerminatorArrives()
    {
        var parser = new AnsiParser();

        var first = parser.Parse("\x1B]0;ti");
        var second = parser.Parse("tle\a");

        Assert.Empty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Parse_SplitDcsAcrossCalls_DropsSequenceWhenTerminatorArrives()
    {
        var parser = new AnsiParser();

        var first = parser.Parse("\x1BPde");
        var second = parser.Parse("mo\x1B\\");

        Assert.Empty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Parse_EscInsideCsi_AbortsMalformedSequenceAndStartsNewEscape()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[\x1B[31m");

        var csi = Assert.Single(actions);
        var dispatch = Assert.IsType<CsiDispatchAction>(csi);
        Assert.Equal(new[] { 31 }, dispatch.Parameters);
        Assert.Equal('m', dispatch.FinalByte);
    }

    [Fact]
    public void Parse_BareEscBeforePrintable_ReprocessesPrintableCharacter()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1Bw");

        var print = Assert.Single(actions);
        var printAction = Assert.IsType<PrintAction>(print);
        Assert.Equal("w", printAction.Text);
    }

    [Fact]
    public void Parse_ScsSequence_ConsumesPrefixAndDesignator()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B(M");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_UnterminatedOsc_ResetsAfterGuardLimitAndPrintsFollowingText()
    {
        var parser = new AnsiParser();
        var unterminatedOsc = "\x1B]" + new string('a', 4095) + "tail";

        var actions = parser.Parse(unterminatedOsc);

        var print = Assert.Single(actions);
        var printAction = Assert.IsType<PrintAction>(print);
        Assert.Equal("tail", printAction.Text);
    }

    [Fact]
    public void Parse_NegativeCursorParameter_DropsUnsupportedSequence()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[-1A");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_CsiIntermediateByte_DropsUnsupportedSequence()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B[#A");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_RisEscape_IsDroppedWithoutEmittingText()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B" + "c");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_NewlineAndCarriageReturn_EmitExecuteActions()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("a\r\nb");

        Assert.Collection(
            actions,
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("a", print.Text);
            },
            action =>
            {
                var execute = Assert.IsType<ExecuteAction>(action);
                Assert.Equal(AnsiC0Control.CarriageReturn, execute.Control);
            },
            action =>
            {
                var execute = Assert.IsType<ExecuteAction>(action);
                Assert.Equal(AnsiC0Control.LineFeed, execute.Control);
            },
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("b", print.Text);
            });
    }

    [Fact]
    public void Parse_Backspace_EmitsExecuteAction()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("a\bb");

        Assert.Collection(
            actions,
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("a", print.Text);
            },
            action =>
            {
                var execute = Assert.IsType<ExecuteAction>(action);
                Assert.Equal(AnsiC0Control.Backspace, execute.Control);
            },
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("b", print.Text);
            });
    }

    [Fact]
    public void Parse_Tab_EmitsExecuteAction()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("a\tb");

        Assert.Collection(
            actions,
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("a", print.Text);
            },
            action =>
            {
                var execute = Assert.IsType<ExecuteAction>(action);
                Assert.Equal(AnsiC0Control.Tab, execute.Control);
            },
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("b", print.Text);
            });
    }

    [Fact]
    public void Parse_Bell_EmitsExecuteAction()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("a\ab");

        Assert.Collection(
            actions,
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("a", print.Text);
            },
            action =>
            {
                var execute = Assert.IsType<ExecuteAction>(action);
                Assert.Equal(AnsiC0Control.Bell, execute.Control);
            },
            action =>
            {
                var print = Assert.IsType<PrintAction>(action);
                Assert.Equal("b", print.Text);
            });
    }

    [Fact]
    public void Parse_OscSequenceTerminatedByBell_IsDropped()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1B]0;title\a");

        Assert.Empty(actions);
    }

    [Fact]
    public void Parse_DcsSequenceTerminatedByEscBackslash_IsDropped()
    {
        var parser = new AnsiParser();

        var actions = parser.Parse("\x1BPdemo\x1B\\");

        Assert.Empty(actions);
    }
}

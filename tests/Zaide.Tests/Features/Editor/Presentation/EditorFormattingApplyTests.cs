using System;
using AvaloniaEdit.Document;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Focused caret-mapping and undo-group policy tests for whole-document formatting.
/// Headless TextDocument proofs (AvaloniaEdit) match the M0 locked rules.
/// </summary>
public sealed class EditorFormattingApplyTests
{
    [Fact]
    public void MapCaretAfterFullReplace_SameLineStillExists_ClampsColumn()
    {
        var original = new TextDocument("abcdefghij\nsecond line");
        var caret = 5; // 'f'
        var pre = original.GetLocation(caret);

        var formatted = new TextDocument("ab\nsecond line");
        var mapped = EditorView.MapCaretAfterFullReplace(formatted, pre, caret);

        // Line 1 still exists; column clamped to line length + 1.
        var line1 = formatted.GetLineByNumber(1);
        Assert.InRange(mapped, 0, formatted.TextLength);
        Assert.Equal(formatted.GetOffset(1, Math.Min(pre.Column, line1.Length + 1)), mapped);
    }

    [Fact]
    public void MapCaretAfterFullReplace_LineRemoved_ClampsToDocument()
    {
        var original = new TextDocument("line1\nline2\nline3");
        var caret = original.TextLength - 1;
        var pre = original.GetLocation(caret);

        var formatted = new TextDocument("only");
        var mapped = EditorView.MapCaretAfterFullReplace(formatted, pre, caret);

        Assert.InRange(mapped, 0, formatted.TextLength);
    }

    [Fact]
    public void UndoGroup_WholeDocumentReplace_SingleUndoRestoresOriginal()
    {
        var doc = new TextDocument("original text");
        var original = doc.Text;
        var formatted = "formatted text\nwith more lines";

        var stack = doc.UndoStack;
        stack.StartUndoGroup();
        try
        {
            doc.Text = formatted;
        }
        finally
        {
            stack.EndUndoGroup();
        }

        Assert.Equal(formatted, doc.Text);
        Assert.True(stack.CanUndo);

        stack.Undo();
        Assert.Equal(original, doc.Text);
    }

    [Fact]
    public void UndoGroup_MultiAssignInsideGroup_StillOneUndoStep()
    {
        var doc = new TextDocument("start");
        var stack = doc.UndoStack;

        stack.StartUndoGroup();
        try
        {
            doc.Text = "A";
            doc.Text = "AB";
            doc.Text = "ABC";
        }
        finally
        {
            stack.EndUndoGroup();
        }

        Assert.Equal("ABC", doc.Text);
        stack.Undo();
        Assert.Equal("start", doc.Text);
    }

    [Fact]
    public void NonBmp_MapCaret_UsesUtf16Offsets()
    {
        var doc = new TextDocument("a🎉b");
        Assert.Equal(4, doc.TextLength);
        var caret = 3; // after emoji, on 'b' start... actually index 3 is 'b'
        Assert.Equal('b', doc.GetCharAt(caret));
        var pre = doc.GetLocation(caret);

        var formatted = new TextDocument("a🎉b\n");
        var mapped = EditorView.MapCaretAfterFullReplace(formatted, pre, caret);
        Assert.InRange(mapped, 0, formatted.TextLength);
    }
}

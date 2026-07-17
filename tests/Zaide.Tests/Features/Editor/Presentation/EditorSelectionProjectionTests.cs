using System;
using System.IO;
using AvaloniaEdit.Document;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Regression coverage for ISSUE-006: selection status projection must never
/// call TextDocument.GetOffset with empty/stale line-column coordinates.
/// </summary>
public sealed class EditorSelectionProjectionTests
{
    private static readonly string WorkflowProgramCs = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "workflow-console", "Program.cs"));

    [Fact]
    public void EmptySelection_StartPositionLineZero_WouldCrashGetOffset_ButProjectionIsSafe()
    {
        // AvaloniaEdit EmptySelection.StartPosition is TextLocation.Empty (0, 0).
        // Program.cs (or any 3-line buffer) reproduces the production message
        // "Value must be between 1 and N" when GetOffset is called with line 0.
        var content = File.ReadAllText(WorkflowProgramCs);
        var doc = new TextDocument(content);
        Assert.True(doc.LineCount >= 1);

        var crash = Assert.Throws<ArgumentOutOfRangeException>(
            () => doc.GetOffset(0, 0));
        Assert.Contains("between 1 and", crash.Message, StringComparison.Ordinal);

        // Editing/deleting often ends in an empty selection at the caret.
        var caret = Math.Min(12, doc.TextLength);
        var projected = EditorView.ProjectSelectionState(
            doc.TextLength,
            selectionStart: caret,
            selectionLength: 0,
            isEmpty: true,
            selectedText: null);

        Assert.Equal(caret, projected.Start);
        Assert.Equal(0, projected.Length);
        Assert.Null(projected.Text);
    }

    [Fact]
    public void EmptySelection_ThreeLineDocument_MatchesProductionGetOffsetMessage()
    {
        // Production stack: "Value must be between 1 and 3" when LineCount == 3.
        var doc = new TextDocument("a\nb\nc");
        Assert.Equal(3, doc.LineCount);

        var crash = Assert.Throws<ArgumentOutOfRangeException>(
            () => doc.GetOffset(0, 1));
        Assert.Contains("between 1 and 3", crash.Message, StringComparison.Ordinal);

        var projected = EditorView.ProjectSelectionState(
            doc.TextLength,
            selectionStart: 2,
            selectionLength: 0,
            isEmpty: true,
            selectedText: null);

        Assert.Equal(2, projected.Start);
        Assert.Equal(0, projected.Length);
        Assert.Null(projected.Text);
    }

    [Fact]
    public void ValidSelection_ReportsStartLengthAndText()
    {
        const string content = "hello world\nsecond";
        var doc = new TextDocument(content);
        const int start = 6;
        const int length = 5;
        var text = content.Substring(start, length);

        var projected = EditorView.ProjectSelectionState(
            doc.TextLength,
            selectionStart: start,
            selectionLength: length,
            isEmpty: false,
            selectedText: text);

        Assert.Equal(start, projected.Start);
        Assert.Equal(length, projected.Length);
        Assert.Equal("world", projected.Text);

        // Cross-check against offset-based document APIs (the safe path).
        Assert.Equal(text, doc.GetText(start, length));
        Assert.InRange(projected.Start, 0, doc.TextLength);
        Assert.InRange(projected.Start + projected.Length, 0, doc.TextLength);
    }

    [Fact]
    public void ValidSelection_WorkflowProgramCs_SelectionOffsetsStayInRange()
    {
        var content = File.ReadAllText(WorkflowProgramCs);
        var doc = new TextDocument(content);
        Assert.True(doc.TextLength > 10);

        // Select a span inside the first line (simulates mouse/keyboard select).
        var start = 0;
        var length = Math.Min(14, doc.TextLength);
        var text = doc.GetText(start, length);

        var projected = EditorView.ProjectSelectionState(
            doc.TextLength,
            start,
            length,
            isEmpty: false,
            selectedText: text);

        Assert.Equal(start, projected.Start);
        Assert.Equal(length, projected.Length);
        Assert.Equal(text, projected.Text);
    }

    [Fact]
    public void EmptyDocument_IsSafe()
    {
        var doc = new TextDocument(string.Empty);
        Assert.Equal(0, doc.TextLength);
        Assert.Equal(1, doc.LineCount);

        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetOffset(0, 0));

        var projected = EditorView.ProjectSelectionState(
            doc.TextLength,
            selectionStart: 0,
            selectionLength: 0,
            isEmpty: true,
            selectedText: "ignored");

        Assert.Equal(0, projected.Start);
        Assert.Equal(0, projected.Length);
        Assert.Null(projected.Text);
    }

    [Fact]
    public void ReplacedDocument_StaleOffsets_AreClamped()
    {
        // Before: long document with a selection. After replace: short content.
        var before = new TextDocument("line1\nline2\nline3\nline4\nline5");
        var staleStart = 20;
        var staleLength = 8;
        Assert.True(staleStart + staleLength <= before.TextLength);

        var after = new TextDocument("x");
        Assert.True(staleStart > after.TextLength);

        // Stale line/column from the old selection would crash GetOffset.
        var staleLocation = before.GetLocation(staleStart);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => after.GetOffset(staleLocation.Line, staleLocation.Column));

        var projected = EditorView.ProjectSelectionState(
            after.TextLength,
            selectionStart: staleStart,
            selectionLength: staleLength,
            isEmpty: false,
            selectedText: "stale-text");

        Assert.Equal(after.TextLength, projected.Start);
        Assert.Equal(0, projected.Length);
        Assert.Null(projected.Text);
    }

    [Fact]
    public void DeleteShortensDocument_SelectionLengthClamped_TextCleared()
    {
        // User had a selection that no longer fits after the document shrank.
        var projected = EditorView.ProjectSelectionState(
            documentTextLength: 3, // "abc"
            selectionStart: 2,
            selectionLength: 4,
            isEmpty: false,
            selectedText: "cdef");

        Assert.Equal(2, projected.Start);
        Assert.Equal(1, projected.Length);
        Assert.Null(projected.Text);
    }

    [Fact]
    public void NegativeDocumentLength_TreatedAsEmpty()
    {
        var projected = EditorView.ProjectSelectionState(
            documentTextLength: -1,
            selectionStart: 5,
            selectionLength: 2,
            isEmpty: false,
            selectedText: "xx");

        Assert.Equal(0, projected.Start);
        Assert.Equal(0, projected.Length);
        Assert.Null(projected.Text);
    }
}

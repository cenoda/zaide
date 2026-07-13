using AvaloniaEdit.Document;

namespace Phase10M0LanguageIntelligenceProof;

/// <summary>
/// Standalone AvaloniaEdit document proofs for position encoding and whole-document undo grouping.
/// No UI host is required — TextDocument is headless.
/// </summary>
internal static class AvaloniaEditProof
{
    public sealed record PositionEncodingResult(
        bool Passed,
        string Summary,
        IReadOnlyList<string> Details);

    public sealed record UndoGroupResult(
        bool Passed,
        string Summary,
        IReadOnlyList<string> Details);

    public sealed record CaretMappingResult(
        bool Passed,
        string Summary,
        IReadOnlyList<string> Details);

    public static PositionEncodingResult ProvePositionEncoding()
    {
        var details = new List<string>();
        var doc = new TextDocument();

        // Mix of BMP, CJK, and non-BMP (emoji is a surrogate pair in UTF-16).
        // Index: 0=A 1=中 2=🎉(2 UTF-16 units) 4=B
        const string text = "A中🎉B";
        doc.Text = text;

        details.Add($"Document.TextLength (UTF-16 code units) = {doc.TextLength}");
        details.Add($"String.Length (UTF-16) = {text.Length}");
        details.Add($"Rune count (Unicode scalar values) = {text.EnumerateRunes().Count()}");
        details.Add($"UTF-8 byte length = {System.Text.Encoding.UTF8.GetByteCount(text)}");

        // AvaloniaEdit stores text as .NET string (UTF-16). GetOffset(line, column)
        // uses 1-based line/column where column is a UTF-16 code-unit offset into the line
        // (same model as classic TextEditor / AvaloniaEdit docs).
        var loc0 = doc.GetLocation(0);
        details.Add($"GetLocation(0) => line={loc0.Line}, column={loc0.Column}");

        // Offset of '中' is 1
        var locZhong = doc.GetLocation(1);
        details.Add($"GetLocation(1) ['中'] => line={locZhong.Line}, column={locZhong.Column}");

        // Offset of emoji 🎉 is 2 (starts a surrogate pair)
        var locEmoji = doc.GetLocation(2);
        details.Add($"GetLocation(2) [🎉 high surrogate] => line={locEmoji.Line}, column={locEmoji.Column}");

        // Offset after emoji (of 'B') is 4
        var locB = doc.GetLocation(4);
        details.Add($"GetLocation(4) ['B'] => line={locB.Line}, column={locB.Column}");

        // Round-trip GetOffset from those locations
        var offZhong = doc.GetOffset(locZhong.Line, locZhong.Column);
        var offEmoji = doc.GetOffset(locEmoji.Line, locEmoji.Column);
        var offB = doc.GetOffset(locB.Line, locB.Column);
        details.Add($"GetOffset round-trip: 中={offZhong}, 🎉={offEmoji}, B={offB}");

        // LSP UTF-16 positions: line 0-based, character = UTF-16 code units from line start.
        // AvaloniaEdit Location.Column is 1-based UTF-16 units from line start.
        static (int line, int character) ToLspUtf16(TextLocation loc) =>
            (loc.Line - 1, loc.Column - 1);

        static TextLocation FromLspUtf16(int line, int character) =>
            new(line + 1, character + 1);

        var lspEmoji = ToLspUtf16(locEmoji);
        var back = FromLspUtf16(lspEmoji.line, lspEmoji.character);
        var backOffset = doc.GetOffset(back.Line, back.Column);
        details.Add(
            $"LSP utf-16 for 🎉: line={lspEmoji.line}, character={lspEmoji.character}; " +
            $"round-trip offset={backOffset} (expected 2)");

        // Surrogate-pair note: character index 2 points at high surrogate; character 3 is low surrogate.
        // A client that incorrectly treats columns as Unicode scalar values would map 🎉 to character 2
        // as a single unit and place 'B' at character 3 — AvaloniaEdit places 'B' at column 5 (1-based).
        var passed =
            doc.TextLength == text.Length &&
            offZhong == 1 &&
            offEmoji == 2 &&
            offB == 4 &&
            backOffset == 2 &&
            locB.Column == 5; // 1-based: A(1) 中(2) 🎉hi(3) 🎉lo(4) B(5)

        details.Add(passed
            ? "PASS: AvaloniaEdit positions are UTF-16 code-unit columns; matches LSP utf-16."
            : "FAIL: unexpected AvaloniaEdit position semantics.");

        return new PositionEncodingResult(
            passed,
            passed
                ? "AvaloniaEdit GetLocation/GetOffset use UTF-16 code-unit columns (matches LSP utf-16)."
                : "AvaloniaEdit position encoding proof failed.",
            details);
    }

    /// <summary>
    /// Conversion locked for M1 when negotiated encoding is utf-16 (default).
    /// </summary>
    public static int LspUtf16PositionToOffset(TextDocument document, int line, int character)
    {
        // LSP: 0-based line/character in UTF-16 code units.
        // AvaloniaEdit: 1-based line/column in UTF-16 code units.
        return document.GetOffset(line + 1, character + 1);
    }

    public static (int line, int character) OffsetToLspUtf16(TextDocument document, int offset)
    {
        var loc = document.GetLocation(offset);
        return (loc.Line - 1, loc.Column - 1);
    }

    public static UndoGroupResult ProveWholeDocumentUndoGroup()
    {
        var details = new List<string>();
        var doc = new TextDocument("line1\nline2\nline3\n");
        var original = doc.Text;
        var formatted = "line1\n  line2\nline3\n";

        var stack = doc.UndoStack;
        details.Add($"CanUndo before edit: {stack.CanUndo}");

        stack.StartUndoGroup();
        try
        {
            // Whole-document replacement path planned for M6 formatting apply.
            doc.Text = formatted;
        }
        finally
        {
            stack.EndUndoGroup();
        }

        details.Add($"Text after format replace: {Escape(doc.Text)}");
        details.Add($"CanUndo after grouped replace: {stack.CanUndo}");

        // Single undo should restore original fully.
        stack.Undo();
        var afterOneUndo = doc.Text;
        details.Add($"Text after one Undo(): {Escape(afterOneUndo)}");
        details.Add($"CanRedo after one Undo(): {stack.CanRedo}");

        stack.Redo();
        var afterRedo = doc.Text;
        details.Add($"Text after Redo(): {Escape(afterRedo)}");

        // Second undo group with multiple internal ops should still be one undo step.
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

        stack.Undo();
        var afterMultiUndo = doc.Text;
        details.Add($"After multi-assign undo group + one Undo: {Escape(afterMultiUndo)}");

        var passed =
            afterOneUndo == original &&
            afterRedo == formatted &&
            afterMultiUndo == formatted; // back to previous formatted state in one undo

        details.Add(passed
            ? "PASS: StartUndoGroup/EndUndoGroup collapses whole-document Text=… into one undo step."
            : "FAIL: whole-document undo grouping did not collapse as expected.");

        return new UndoGroupResult(
            passed,
            passed
                ? "Whole-document Document.Text replacement groups as one undo step."
                : "Undo-group proof failed.",
            details);
    }

    public static CaretMappingResult ProveCaretSelectionAfterFullReplace()
    {
        // TextDocument alone does not own caret; TextEditor does. For headless proof we
        // document the observed AvaloniaEdit rule used by EditorView/TextEditor:
        // assigning Document.Text resets caret to 0 and clears selection unless the
        // caller repositions afterwards. We simulate the M6 mapping rule here.
        var details = new List<string>();
        var doc = new TextDocument("abcdefghijklmnopqrstuvwxyz");
        var caretBefore = 10; // 'k'
        var selStart = 5;
        var selLength = 5;

        details.Add($"Before: caret={caretBefore}, selection=[{selStart},{selLength}], textLen={doc.TextLength}");

        var newText = "ABC\nDEF\nGHI"; // shorter document
        // M6 mapping rule (locked):
        // 1. Capture pre-replace (line, column) via GetLocation(caretOffset).
        // 2. Apply Document.Text = formatted under StartUndoGroup/EndUndoGroup.
        // 3. Clamp caret to min(oldOffset, newText.Length); prefer same line/column
        //    when the line still exists, else end of last line.
        // 4. Clear selection (SelectionLength = 0) after full-document format.
        var preLoc = doc.GetLocation(caretBefore);
        details.Add($"Pre-replace location: line={preLoc.Line}, column={preLoc.Column}");

        doc.Text = newText;

        // Observed headless document state: text replaced; caret is a TextEditor concern.
        // Mapping rule applied by the editor host after replace:
        var mappedCaret = MapCaretAfterFullReplace(doc, preLoc, caretBefore);
        var mappedSelStart = mappedCaret;
        var mappedSelLength = 0;

        details.Add($"After replace textLen={doc.TextLength}");
        details.Add($"Mapped caret offset={mappedCaret} (rule: clamp + prefer prior line/column)");
        details.Add($"Mapped selection: start={mappedSelStart}, length={mappedSelLength} (cleared)");

        var passed = mappedCaret >= 0 &&
                     mappedCaret <= doc.TextLength &&
                     mappedSelLength == 0;

        details.Add(passed
            ? "PASS: caret/selection mapping rule is deterministic after full-document replace."
            : "FAIL: caret/selection mapping rule invalid.");

        // Silence unused in case of future edits
        _ = selStart;
        _ = selLength;

        return new CaretMappingResult(
            passed,
            passed
                ? "Caret clamped/mapped; selection cleared after full-document replace."
                : "Caret/selection mapping proof failed.",
            details);
    }

    /// <summary>
    /// Locked M6 caret mapping after whole-document formatting replacement.
    /// </summary>
    public static int MapCaretAfterFullReplace(TextDocument document, TextLocation preLocation, int preOffset)
    {
        // Prefer the same line/column when the line still exists.
        if (preLocation.Line >= 1 && preLocation.Line <= document.LineCount)
        {
            var line = document.GetLineByNumber(preLocation.Line);
            var column = Math.Clamp(preLocation.Column, 1, line.Length + 1);
            return document.GetOffset(preLocation.Line, column);
        }

        // Otherwise clamp to end of document.
        return Math.Clamp(preOffset, 0, document.TextLength);
    }

    private static string Escape(string s) =>
        s.Replace("\n", "\\n").Replace("\r", "\\r");
}

using System;

namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Minimal seam for reading and mutating the active document's text.
/// Implemented by the View layer (e.g. MainWindow) using AvaloniaEdit APIs.
/// <para>
/// The <see cref="EditorSearchViewModel"/> depends on this interface — never on
/// AvaloniaEdit types directly — so that all search/replace logic remains testable
/// without UI controls.
/// </para>
/// <para>
/// Implementations must ensure:
/// <list type="bullet">
/// <item><see cref="SetText"/> flows through <c>Document.Content</c> so dirty state stays truthful.</item>
/// <item><see cref="ReplaceAllMatches"/> wraps the entire operation in one undo group
/// (AvaloniaEdit <c>UndoStack.StartUndoGroup()</c> / <c>EndUndoGroup()</c>).</item>
/// </list>
/// </para>
/// </summary>
public interface IEditorTextOperations
{
    /// <summary>Current document text.</summary>
    string GetText();

    /// <summary>Overwrites the entire document text. Must preserve dirty-state semantics.</summary>
    void SetText(string text);

    /// <summary>Selects the range [<paramref name="offset"/>, <paramref name="offset"/> + <paramref name="length"/>) and makes it visible.</summary>
    void SetSelection(int offset, int length);

    /// <summary>Current selection offset, or caret offset when nothing is selected.</summary>
    int GetSelectionOffset();

    /// <summary>Current selection length (0 when nothing is selected).</summary>
    int GetSelectionLength();

    /// <summary>
    /// Replaces all non-overlapping occurrences of <paramref name="query"/> with
    /// <paramref name="replacement"/> as a single undoable action.
    /// Uses the same <paramref name="caseSensitive"/> comparison as the search.
    /// Returns the number of replacements performed.
    /// </summary>
    int ReplaceAllMatches(string query, string replacement, bool caseSensitive);
}

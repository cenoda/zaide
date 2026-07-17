namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Editor seam for active-document language input routing and commit application.
/// </summary>
public interface IEditorLanguageOperations : IEditorTextOperations
{
    /// <summary>Current caret offset in UTF-16 code units.</summary>
    int GetCaretOffset();

    /// <summary>Replaces <c>[start, start + length)</c> with <paramref name="newText"/>.</summary>
    void ReplaceRange(int start, int length, string newText);

    /// <summary>Returns the character immediately before the caret, if any.</summary>
    char? GetCharBeforeCaret();

    /// <summary>
    /// Applies whole-document formatting as one undoable operation with the
    /// M0-locked caret/selection mapping rule. Returns false when rejected.
    /// </summary>
    bool ApplyFormattedDocument(string formattedText);

    /// <summary>
    /// Undoes the last AvaloniaEdit undo step (for tests and host commands).
    /// </summary>
    bool TryUndo();
}

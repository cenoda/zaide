namespace Zaide.ViewModels;

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
}

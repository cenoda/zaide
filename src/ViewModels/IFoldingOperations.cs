using System;

namespace Zaide.ViewModels;

/// <summary>
/// Minimal seam for managing code folding in the active editor.
/// Implemented by the View layer using AvaloniaEdit
/// <c>FoldingManager</c> / <c>FoldingMargin</c> APIs.
/// <para>
/// Commands in <see cref="EditorTabViewModel"/> depend on this interface —
/// never on AvaloniaEdit types directly — so that command availability logic
/// remains testable without UI controls.
/// </para>
/// <para><b>Tab-switch contract:</b></para>
/// <list type="bullet">
/// <item><see cref="Clear"/> must be called on every active-tab switch and
/// tab close before the new tab's content is shown.</item>
/// <item><see cref="Install"/> is called with the new tab's full document
/// text after the content is loaded.</item>
/// <item>Folding state is discarded per tab in M4; it must never leak to
/// another tab.</item>
/// <item>Every operation must preserve a valid caret position and call
/// <c>BringCaretToView</c> afterward.</item>
/// </list>
/// </summary>
public interface IFoldingOperations
{
    /// <summary>
    /// True when the underlying editor has a <c>FoldingManager</c>
    /// installed and is ready for fold operations. False when no editor
    /// is available or folding has been cleared.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Discovers foldable regions in <paramref name="text"/> and installs
    /// them in the editor's <c>FoldingManager</c>. Clears any previous
    /// folding state first. Does nothing when the underlying editor is
    /// not ready.
    /// </summary>
    void Install(string text);

    /// <summary>
    /// Removes all folding sections from the <c>FoldingManager</c> and
    /// disables the folding margin. Safe to call multiple times.
    /// </summary>
    void Clear();

    /// <summary>
    /// Toggles the fold state of the region at the editor's current caret
    /// position. When multiple nested regions contain the caret, toggles
    /// the innermost one. Reads the caret offset from the underlying editor.
    /// Returns true when a region was found and toggled; false when
    /// no region contains the caret.
    /// </summary>
    bool ToggleCurrent();

    /// <summary>
    /// Folds all currently installed folding sections.
    /// </summary>
    void FoldAll();

    /// <summary>
    /// Unfolds all currently installed folding sections.
    /// </summary>
    void UnfoldAll();
}

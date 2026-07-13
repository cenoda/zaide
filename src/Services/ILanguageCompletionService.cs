using System;

namespace Zaide.Services;

/// <summary>
/// UI-independent active-document completion ownership.
/// </summary>
public interface ILanguageCompletionService : IDisposable
{
    /// <summary>Current immutable completion snapshot.</summary>
    LanguageCompletionSnapshot Current { get; }

    /// <summary>Emits each new <see cref="LanguageCompletionSnapshot"/>.</summary>
    IObservable<LanguageCompletionSnapshot> WhenChanged { get; }

    /// <summary>
    /// Schedules an explicit completion request for the active document at
    /// <paramref name="caretOffset"/>.
    /// </summary>
    void RequestExplicit(string filePath, int caretOffset);

    /// <summary>
    /// Schedules a debounced automatic completion request when
    /// <paramref name="triggerCharacter"/> is server-supported.
    /// </summary>
    void RequestAutomatic(string filePath, int caretOffset, char triggerCharacter);

    /// <summary>Moves the selected completion item by <paramref name="delta"/>.</summary>
    void MoveSelection(int delta);

    /// <summary>
    /// Creates a validated commit for the selected item and dismisses the popup.
    /// Returns <c>null</c> when no item is selected.
    /// </summary>
    LanguageCompletionCommit? TryCommitSelected();

    /// <summary>Dismisses any open or in-flight completion state.</summary>
    void Dismiss();
}

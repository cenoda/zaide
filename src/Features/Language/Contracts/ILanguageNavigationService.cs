using System;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Language.Contracts;

/// <summary>
/// UI-independent Go to Definition ownership with stale-result protection.
/// Does not open tabs or mutate editor selection; callers navigate after validation.
/// </summary>
public interface ILanguageNavigationService : IDisposable
{
    /// <summary>Current immutable definition snapshot.</summary>
    LanguageNavigationSnapshot Current { get; }

    /// <summary>Emits each new <see cref="LanguageNavigationSnapshot"/>.</summary>
    IObservable<LanguageNavigationSnapshot> WhenChanged { get; }

    /// <summary>
    /// Requests <c>textDocument/definition</c> for the active document at
    /// <paramref name="caretOffset"/>. Cancels any outstanding definition work.
    /// </summary>
    void RequestDefinition(string filePath, int caretOffset);

    /// <summary>Moves multi-result chooser selection by <paramref name="delta"/>.</summary>
    void MoveSelection(int delta);

    /// <summary>
    /// Accepts the currently selected multi-result location when the chooser is open
    /// and the source request is still live. Returns null when not choosable/stale.
    /// </summary>
    LanguageLocation? TryAcceptSelected();

    /// <summary>
    /// Takes the single Ready location when still live and dismisses state.
    /// Returns null when not single-ready or stale.
    /// </summary>
    LanguageLocation? TryTakeSingleLocation();

    /// <summary>Dismisses any chooser or in-flight definition state.</summary>
    void Dismiss();
}

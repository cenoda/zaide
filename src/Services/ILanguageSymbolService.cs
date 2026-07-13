using System;

namespace Zaide.Services;

/// <summary>
/// UI-independent document and workspace symbol ownership.
/// Selection navigation is performed by callers through the editor-tab path.
/// </summary>
public interface ILanguageSymbolService : IDisposable
{
    /// <summary>Current immutable symbol surface snapshot.</summary>
    LanguageSymbolSnapshot Current { get; }

    /// <summary>Emits each new <see cref="LanguageSymbolSnapshot"/>.</summary>
    IObservable<LanguageSymbolSnapshot> WhenChanged { get; }

    /// <summary>
    /// Requests document symbols for the active, still-live document.
    /// </summary>
    void RequestDocumentSymbols(string filePath);

    /// <summary>
    /// Opens the workspace-symbol surface and schedules a debounced query.
    /// Empty query still requests (server may return all or empty).
    /// </summary>
    void RequestWorkspaceSymbols(string query);

    /// <summary>
    /// Updates the workspace-symbol query, cancelling/replacing outstanding work.
    /// No-op when the surface is not in workspace scope.
    /// </summary>
    void SetWorkspaceQuery(string query);

    /// <summary>Moves symbol list selection by <paramref name="delta"/>.</summary>
    void MoveSelection(int delta);

    /// <summary>
    /// Accepts the selected symbol location when still valid for the surface.
    /// Returns null when nothing is selectable or the surface is stale.
    /// </summary>
    LanguageLocation? TryAcceptSelected();

    /// <summary>Dismisses any symbol surface and cancels in-flight work.</summary>
    void Dismiss();
}

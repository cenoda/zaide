using System;
using System.Reactive.Disposables;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Marshals language-intelligence snapshot observers onto the editor UI thread.
/// </summary>
internal static class EditorLanguageUiProjection
{
    internal static IDisposable Subscribe<T>(
        IObservable<T> source,
        IEditorUiDispatcher dispatcher,
        Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(apply);

        return source.Subscribe(snapshot => dispatcher.Invoke(() => apply(snapshot)));
    }
}
